using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PrepApi.Tests.Integration;

public sealed class AuthApiIntegrationTests : IDisposable
{
    private const string Password = "S3cret-pass word!";
    private readonly string _file;
    private readonly string _dir;

    public AuthApiIntegrationTests() => _file = AuthApiFactory.NewTempFile(out _dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private static object Signup(string email, string name = "Zoë 李", string password = Password) =>
        new { name, email, password };

    private static IEnumerable<string> SetCookies(HttpResponseMessage r) =>
        r.Headers.TryGetValues("Set-Cookie", out var v) ? v : [];

    private static string? SessionCookie(HttpResponseMessage r) =>
        SetCookies(r).FirstOrDefault(c => c.StartsWith("prep.session=", StringComparison.Ordinal));

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static DateTimeOffset ExpiresOf(string setCookie)
    {
        var part = setCookie.Split(';').Select(p => p.Trim())
            .First(p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        return DateTimeOffset.Parse(part["expires=".Length..], System.Globalization.CultureInfo.InvariantCulture);
    }

    // ---- AC-5 / AC-4: signup -------------------------------------------------------------

    [Fact]
    public async Task AC5_AC4_Signup_returns_201_with_name_and_email_only_and_sets_no_cookie()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();

        var res = await c.PostAsJsonAsync("/api/auth/signup", Signup("  Zoe@Example.COM "));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Empty(SetCookies(res));
        var body = await Json(res);
        Assert.Equal("Zoë 李", body.GetProperty("name").GetString());
        Assert.Equal("zoe@example.com", body.GetProperty("email").GetString());
        Assert.Equal(2, body.EnumerateObject().Count());
        var raw = body.GetRawText();
        Assert.DoesNotContain(Password, raw);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AC5_Signup_does_not_log_the_user_in()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));

        var me = await c.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Theory]
    [InlineData("A@B.CO")]
    [InlineData("  a@b.co  ")]
    public async Task AC5_Signup_duplicate_email_case_or_whitespace_returns_409(string second)
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"))).StatusCode);

        var res = await c.PostAsJsonAsync("/api/auth/signup", Signup(second));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("An account with this email already exists.", (await Json(res)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task AC7_Signup_invalid_input_returns_400_with_field_errors()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();

        var res = await c.PostAsJsonAsync("/api/auth/signup", new { name = "   ", email = "not-an-email", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await Json(res)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.False(File.Exists(_file) && (await File.ReadAllTextAsync(_file)).Contains("not-an-email"));
    }

    [Fact]
    public async Task AC7_Signup_missing_fields_returns_400()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();

        var res = await c.PostAsJsonAsync("/api/auth/signup", new { });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task AC5_Signup_malformed_json_returns_400()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();

        var res = await c.PostAsync("/api/auth/signup",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---- AC-6 / AC-2 / AC-13: login ------------------------------------------------------

    [Fact]
    public async Task AC6_AC2_AC13_Login_returns_200_and_sets_httponly_lax_cookie_expiring_in_8h()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));

        var before = DateTimeOffset.UtcNow;
        var res = await c.PostAsJsonAsync("/api/auth/login", new { email = "A@B.co ", password = Password });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await Json(res);
        Assert.Equal("Zoë 李", body.GetProperty("name").GetString());
        Assert.Equal("a@b.co", body.GetProperty("email").GetString());

        var cookie = SessionCookie(res);
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        var expiry = ExpiresOf(cookie);
        Assert.InRange((expiry - before).TotalMinutes, 8 * 60 - 1, 8 * 60 + 1);

        var value = cookie.Split(';')[0]["prep.session=".Length..];
        Assert.DoesNotContain(Password, value);
        Assert.DoesNotContain(Uri.EscapeDataString(Password), value);
    }

    [Fact]
    public async Task AC13_Session_lifetime_is_configurable()
    {
        using var f = new AuthApiFactory(_file, sessionHours: 1);
        var c = f.CreateCookieClient();
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));

        var before = DateTimeOffset.UtcNow;
        var res = await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = Password });

        var expiry = ExpiresOf(SessionCookie(res)!);
        Assert.InRange((expiry - before).TotalMinutes, 59, 61);
    }

    [Fact]
    public async Task AC6_Login_unknown_email_and_wrong_password_give_identical_401_and_no_cookie()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));

        var unknown = await c.PostAsJsonAsync("/api/auth/login", new { email = "nobody@b.co", password = Password });
        var wrong = await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = "wrong-password" });
        var tooShort = await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tooShort.StatusCode);
        Assert.Null(SessionCookie(unknown));
        Assert.Null(SessionCookie(wrong));
        Assert.Equal("Invalid email or password.", (await Json(unknown)).GetProperty("title").GetString());
        Assert.Equal((await Json(unknown)).GetProperty("title").GetString(), (await Json(wrong)).GetProperty("title").GetString());
        // Same shape and same fields, apart from the per-request traceId.
        static string Normalised(JsonElement e) => string.Join("|", e.EnumerateObject()
            .Where(p => p.Name != "traceId").OrderBy(p => p.Name).Select(p => $"{p.Name}={p.Value}"));
        Assert.Equal(Normalised(await Json(unknown)), Normalised(await Json(wrong)));
    }

    [Fact]
    public async Task AC6_Login_with_password_whitespace_changed_is_rejected()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co", password: "  padded pw  "));

        var trimmed = await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = "padded pw" });
        var exact = await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = "  padded pw  " });

        Assert.Equal(HttpStatusCode.Unauthorized, trimmed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, exact.StatusCode);
    }

    [Fact]
    public async Task AC7_Login_malformed_email_or_empty_password_returns_400()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();

        var badEmail = await c.PostAsJsonAsync("/api/auth/login", new { email = "nope", password = Password });
        var noPassword = await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noPassword.StatusCode);
        Assert.Null(SessionCookie(badEmail));
    }

    // ---- AC-15: /me ----------------------------------------------------------------------

    [Fact]
    public async Task AC15_Me_returns_user_after_login_and_401_without_cookie_not_redirect()
    {
        using var f = new AuthApiFactory(_file);
        var anon = f.CreateCookieClient();
        var anonRes = await anon.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonRes.StatusCode);
        Assert.Null(anonRes.Headers.Location);

        var c = f.CreateCookieClient();
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));
        await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = Password });

        var me = await c.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await Json(me);
        Assert.Equal("Zoë 李", body.GetProperty("name").GetString());
        Assert.Equal("a@b.co", body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task AC15_Me_with_tampered_cookie_returns_401()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));
        var login = await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = Password });
        var value = SessionCookie(login)!.Split(';')[0]["prep.session=".Length..];
        var tampered = value[..^4] + (value.EndsWith("AAAA") ? "BBBB" : "AAAA");

        var raw = f.CreateCookieClient(handleCookies: false);
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        req.Headers.Add("Cookie", $"prep.session={tampered}");
        var res = await raw.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task AC15_Me_with_valid_cookie_sent_manually_returns_200()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));
        var login = await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = Password });
        var pair = SessionCookie(login)!.Split(';')[0];

        var raw = f.CreateCookieClient(handleCookies: false);
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        req.Headers.Add("Cookie", pair);

        Assert.Equal(HttpStatusCode.OK, (await raw.SendAsync(req)).StatusCode);
    }

    // ---- AC-14: logout -------------------------------------------------------------------

    [Fact]
    public async Task AC14_Logout_returns_204_expires_cookie_and_session_ends()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));
        await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = Password });
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/auth/me")).StatusCode);

        var res = await c.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var cookie = SessionCookie(res);
        Assert.NotNull(cookie);
        Assert.True(ExpiresOf(cookie!) < DateTimeOffset.UtcNow, "logout must expire the cookie in the past");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AC14_Logout_without_session_is_idempotent_204()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/auth/logout", null)).StatusCode);
    }

    // ---- AC-8 / AC-9 / AC-10: persistence ------------------------------------------------

    [Fact]
    public async Task AC8_Account_survives_restart_new_factory_same_file()
    {
        using (var a = new AuthApiFactory(_file))
        {
            var c = a.CreateCookieClient();
            Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"))).StatusCode);
        }

        using var b = new AuthApiFactory(_file);
        var c2 = b.CreateCookieClient();
        var login = await c2.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        // The duplicate check also sees the persisted user.
        Assert.Equal(HttpStatusCode.Conflict, (await c2.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"))).StatusCode);
    }

    [Fact]
    public async Task AC9_AC10_File_is_created_at_configured_path_and_has_hash_not_plaintext()
    {
        Assert.False(File.Exists(_file));
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();

        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));

        Assert.True(File.Exists(_file));
        var text = await File.ReadAllTextAsync(_file);
        Assert.DoesNotContain(Password, text);
        var users = JsonDocument.Parse(text).RootElement;
        Assert.Equal(1, users.GetArrayLength());
        var hash = users[0].GetProperty("passwordHash").GetString();
        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.NotEqual(Password, hash);
        Assert.Equal("a@b.co", users[0].GetProperty("email").GetString());
        Assert.False(File.Exists(_file + ".tmp"));
    }

    [Fact]
    public async Task AC10_Missing_directory_is_created_on_first_signup()
    {
        var nested = Path.Combine(_dir, "deep", "er", "users.json");
        using var f = new AuthApiFactory(nested);
        var c = f.CreateCookieClient();

        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"))).StatusCode);
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void AC10_Default_users_file_location_is_git_ignored()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".gitignore")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var lines = File.ReadAllLines(Path.Combine(dir!.FullName, ".gitignore")).Select(l => l.Trim());
        Assert.Contains(lines, l => l is "prep-api/App_Data/" or "prep-api/App_Data" or "prep-api/App_Data/*"
            or "App_Data/" or "App_Data");
    }

    [Fact]
    public async Task AC9_Corrupt_users_file_gives_500_on_signup_and_file_is_untouched()
    {
        const string corrupt = "{ this is : not json";
        await File.WriteAllTextAsync(_file, corrupt);
        // Development: the framework's exception handling turns the throw into a 500. (Production under
        // TestServer rethrows instead, since the host has no exception middleware of its own.)
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();

        var res = await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));

        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
        Assert.Equal(corrupt, await File.ReadAllTextAsync(_file));
        // The user's data (the attempted signup) must not have been written over the corrupt file.
        Assert.False(File.Exists(_file + ".tmp"));
    }

    // ---- AC-11: concurrency --------------------------------------------------------------

    [Fact]
    public async Task AC11_20_parallel_signups_with_distinct_emails_all_succeed_and_all_persist()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient(handleCookies: false);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            c.PostAsJsonAsync("/api/auth/signup", Signup($"user{i}@b.co", name: $"User {i}"))));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var users = JsonDocument.Parse(await File.ReadAllTextAsync(_file)).RootElement;
        Assert.Equal(20, users.GetArrayLength());
        Assert.Equal(20, users.EnumerateArray().Select(u => u.GetProperty("email").GetString()).Distinct().Count());
    }

    [Fact]
    public async Task AC11_10_parallel_signups_with_same_email_exactly_one_wins()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient(handleCookies: false);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            c.PostAsJsonAsync("/api/auth/signup", Signup(i % 2 == 0 ? "Same@B.co" : " same@b.CO "))));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(9, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var users = JsonDocument.Parse(await File.ReadAllTextAsync(_file)).RootElement;
        Assert.Equal(1, users.GetArrayLength());
    }

    // ---- AC-13: CORS ---------------------------------------------------------------------

    [Fact]
    public async Task AC13_Preflight_from_angular_origin_allows_credentials()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient(handleCookies: false);
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        req.Headers.Add("Origin", AuthApiFactory.AngularOrigin);
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "content-type");

        var res = await c.SendAsync(req);

        Assert.True(res.IsSuccessStatusCode, $"preflight returned {(int)res.StatusCode}");
        Assert.Equal(AuthApiFactory.AngularOrigin, res.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", res.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task AC13_Login_response_carries_cors_headers_for_allowed_origin_only()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient(handleCookies: false);

        HttpRequestMessage Login(string origin)
        {
            var r = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "a@b.co", password = Password }),
            };
            r.Headers.Add("Origin", origin);
            return r;
        }

        var ok = await c.SendAsync(Login(AuthApiFactory.AngularOrigin));
        Assert.Equal(AuthApiFactory.AngularOrigin, ok.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", ok.Headers.GetValues("Access-Control-Allow-Credentials").Single());

        var evil = await c.SendAsync(Login("http://evil.test"));
        Assert.False(evil.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(evil.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task AC13_Preflight_from_disallowed_origin_gets_no_allow_origin()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient(handleCookies: false);
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        req.Headers.Add("Origin", "http://evil.test");
        req.Headers.Add("Access-Control-Request-Method", "POST");

        var res = await c.SendAsync(req);

        Assert.False(res.Headers.Contains("Access-Control-Allow-Origin"));
    }

    // ---- Regression: products ------------------------------------------------------------

    [Fact]
    public async Task Regression_Products_still_returns_3_items_with_cors_incl_credentials()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient(handleCookies: false);
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/products");
        req.Headers.Add("Origin", AuthApiFactory.AngularOrigin);

        var res = await c.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(3, (await Json(res)).GetArrayLength());
        Assert.Equal(AuthApiFactory.AngularOrigin, res.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", res.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    // ---- AC-12: logging ------------------------------------------------------------------

    [Fact]
    public async Task AC12_Logs_never_contain_password_or_hash()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));
        await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = Password });
        await c.PostAsJsonAsync("/api/auth/login", new { email = "a@b.co", password = "wrong-password-123" });
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co")); // 409 path

        var hash = JsonDocument.Parse(await File.ReadAllTextAsync(_file)).RootElement[0]
            .GetProperty("passwordHash").GetString()!;
        var logs = f.Logs.Messages;

        Assert.NotEmpty(logs);
        Assert.DoesNotContain(logs, m => m.Contains(Password));
        Assert.DoesNotContain(logs, m => m.Contains("wrong-password-123"));
        Assert.DoesNotContain(logs, m => m.Contains(hash));
    }

    [Fact]
    public async Task AC12_Error_responses_do_not_expose_stack_traces()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();
        await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"));

        var conflict = await (await c.PostAsJsonAsync("/api/auth/signup", Signup("a@b.co"))).Content.ReadAsStringAsync();
        var unauthorized = await (await c.PostAsJsonAsync("/api/auth/login",
            new { email = "a@b.co", password = "nope-nope" })).Content.ReadAsStringAsync();

        foreach (var body in new[] { conflict, unauthorized })
        {
            Assert.DoesNotContain(" at PrepApi", body);
            Assert.DoesNotContain("Exception", body);
        }
    }
}
