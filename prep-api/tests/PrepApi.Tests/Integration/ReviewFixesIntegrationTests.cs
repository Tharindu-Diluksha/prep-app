using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PrepApi.Tests.Integration;

/// <summary>Covers the review follow-ups through the real host: startup validation and the signup response shape.</summary>
public sealed class ReviewFixesIntegrationTests : IDisposable
{
    private readonly string _file;
    private readonly string _dir;

    public ReviewFixesIntegrationTests() => _file = AuthApiFactory.NewTempFile(out _dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(10_000)]
    public void AC13_Host_refuses_to_start_with_invalid_session_hours(double hours)
    {
        using var f = new AuthApiFactory(_file, sessionHours: hours);

        var ex = Assert.ThrowsAny<Exception>(() => f.CreateCookieClient());

        var validation = ex as OptionsValidationException ?? ex.InnerException as OptionsValidationException;
        Assert.NotNull(validation);
        Assert.Contains("SessionHours", validation!.Message);
    }

    [Fact]
    public async Task AC5_Signup_201_has_no_Location_header_but_json_body()
    {
        using var f = new AuthApiFactory(_file);
        var c = f.CreateCookieClient();

        var res = await c.PostAsJsonAsync("/api/auth/signup", new { name = "Ada", email = "ada@example.com", password = "password123" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Null(res.Headers.Location);
        Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("ada@example.com", body.GetProperty("email").GetString());
    }
}
