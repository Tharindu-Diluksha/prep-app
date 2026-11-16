using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using PrepApi.Models;
using PrepApi.Services;

namespace PrepApi.Tests.Unit;

public class AuthServiceTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly TempDir _dir = new();
        public string Path => _dir.File("users.json");
        public AuthService Service { get; }

        public Fixture() => Service = new AuthService(new FileUserStore(Path), new PasswordHasher<UserRecord>());

        public void Dispose() => _dir.Dispose();
    }

    [Fact]
    public async Task AC5_Signup_returns_created_with_trimmed_name_and_normalised_email()
    {
        using var f = new Fixture();

        var result = await f.Service.SignupAsync(new SignupRequest("  Ada Lovelace  ", "  Ada@Example.COM ", "password123"));

        Assert.False(result.EmailTaken);
        Assert.NotNull(result.User);
        Assert.Equal("Ada Lovelace", result.User!.Name);
        Assert.Equal("ada@example.com", result.User.Email);
    }

    [Fact]
    public async Task AC5_Signup_response_exposes_only_name_and_email()
    {
        using var f = new Fixture();

        var result = await f.Service.SignupAsync(new SignupRequest("Ada", "ada@example.com", "password123"));

        var json = JsonSerializer.Serialize(result.User);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AC5_Second_signup_with_same_email_in_different_case_and_whitespace_is_EmailTaken()
    {
        using var f = new Fixture();
        await f.Service.SignupAsync(new SignupRequest("Ada", "ada@example.com", "password123"));

        var second = await f.Service.SignupAsync(new SignupRequest("Other", "  ADA@Example.com ", "password456"));

        Assert.True(second.EmailTaken);
        Assert.Null(second.User);
    }

    [Fact]
    public async Task AC9_Stored_hash_differs_from_password_and_does_not_contain_it()
    {
        using var f = new Fixture();
        const string password = "correct horse battery";
        await f.Service.SignupAsync(new SignupRequest("Ada", "ada@example.com", password));

        var stored = await new FileUserStore(f.Path).FindByEmailAsync("ada@example.com", CancellationToken.None);

        Assert.NotNull(stored);
        Assert.NotEqual(password, stored!.PasswordHash);
        Assert.DoesNotContain(password, stored.PasswordHash);
        Assert.False(string.IsNullOrWhiteSpace(stored.PasswordHash));
    }

    [Fact]
    public async Task AC9_Same_password_for_two_users_produces_different_hashes_salted()
    {
        using var f = new Fixture();
        await f.Service.SignupAsync(new SignupRequest("A", "a@example.com", "same-password"));
        await f.Service.SignupAsync(new SignupRequest("B", "b@example.com", "same-password"));
        var store = new FileUserStore(f.Path);

        var a = await store.FindByEmailAsync("a@example.com", CancellationToken.None);
        var b = await store.FindByEmailAsync("b@example.com", CancellationToken.None);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotEqual(a!.PasswordHash, b!.PasswordHash);
    }

    [Fact]
    public async Task AC6_ValidateCredentials_returns_user_for_correct_password_case_insensitive_email()
    {
        using var f = new Fixture();
        await f.Service.SignupAsync(new SignupRequest("Ada", "ada@example.com", "password123"));

        var user = await f.Service.ValidateCredentialsAsync(new LoginRequest("  ADA@example.com ", "password123"));

        Assert.NotNull(user);
        Assert.Equal("Ada", user!.Name);
        Assert.Equal("ada@example.com", user.Email);
    }

    [Fact]
    public async Task AC6_ValidateCredentials_returns_null_for_wrong_password()
    {
        using var f = new Fixture();
        await f.Service.SignupAsync(new SignupRequest("Ada", "ada@example.com", "password123"));

        Assert.Null(await f.Service.ValidateCredentialsAsync(new LoginRequest("ada@example.com", "wrong-password")));
    }

    [Fact]
    public async Task AC6_ValidateCredentials_returns_null_for_unknown_email()
    {
        using var f = new Fixture();

        Assert.Null(await f.Service.ValidateCredentialsAsync(new LoginRequest("ghost@example.com", "password123")));
    }

    [Fact]
    public async Task AC7_Password_with_spaces_is_not_trimmed_on_login()
    {
        using var f = new Fixture();
        await f.Service.SignupAsync(new SignupRequest("Ada", "ada@example.com", "  spaced pw  "));

        Assert.NotNull(await f.Service.ValidateCredentialsAsync(new LoginRequest("ada@example.com", "  spaced pw  ")));
        Assert.Null(await f.Service.ValidateCredentialsAsync(new LoginRequest("ada@example.com", "spaced pw")));
    }

    [Fact]
    public async Task AC6_ValidateCredentials_short_password_returns_null_not_exception()
    {
        using var f = new Fixture();
        await f.Service.SignupAsync(new SignupRequest("Ada", "ada@example.com", "password123"));

        Assert.Null(await f.Service.ValidateCredentialsAsync(new LoginRequest("ada@example.com", "abc")));
    }
}
