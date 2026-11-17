using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PrepApi.Models;
using PrepApi.Services;

namespace PrepApi.Tests.Unit;

/// <summary>Covers the review follow-ups: options validation, hash upgrade, store update and cache.</summary>
public class ReviewFixesTests
{
    // ---- MED-1: Auth:SessionHours validation ----

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(721)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AC13_Invalid_session_hours_fail_validation(double hours)
    {
        var result = new AuthOptionsValidator().Validate(null, new AuthOptions { SessionHours = hours });

        Assert.True(result.Failed);
        Assert.Contains("SessionHours", result.FailureMessage);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(8)]
    [InlineData(720)]
    public void AC13_Valid_session_hours_pass_validation(double hours)
    {
        Assert.True(new AuthOptionsValidator().Validate(null, new AuthOptions { SessionHours = hours }).Succeeded);
    }

    [Fact]
    public void AC10_Blank_users_file_path_fails_validation()
    {
        var result = new AuthOptionsValidator().Validate(null, new AuthOptions { UsersFilePath = " " });

        Assert.True(result.Failed);
        Assert.Contains("UsersFilePath", result.FailureMessage);
    }

    // ---- LOW-2: hash upgrade on SuccessRehashNeeded ----

    [Fact]
    public async Task AC9_Login_upgrades_an_outdated_hash_format()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var legacyHasher = new PasswordHasher<UserRecord>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2,
        }));
        await new AuthService(new FileUserStore(path), legacyHasher)
            .SignupAsync(new SignupRequest("Ada", "ada@example.com", "password123"));
        var legacyHash = (await new FileUserStore(path).FindByEmailAsync("ada@example.com", CancellationToken.None))!.PasswordHash;

        var user = await new AuthService(new FileUserStore(path), new PasswordHasher<UserRecord>())
            .ValidateCredentialsAsync(new LoginRequest("ada@example.com", "password123"));

        Assert.NotNull(user);
        var stored = (await new FileUserStore(path).FindByEmailAsync("ada@example.com", CancellationToken.None))!;
        Assert.NotEqual(legacyHash, stored.PasswordHash);
        Assert.Equal(stored.PasswordHash, user!.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<UserRecord>().VerifyHashedPassword(stored, stored.PasswordHash, "password123"));
    }

    [Fact]
    public async Task AC9_Login_with_current_hash_format_does_not_rewrite_the_file()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var service = new AuthService(new FileUserStore(path), new PasswordHasher<UserRecord>());
        await service.SignupAsync(new SignupRequest("Ada", "ada@example.com", "password123"));
        var before = await File.ReadAllBytesAsync(path);

        Assert.NotNull(await service.ValidateCredentialsAsync(new LoginRequest("ada@example.com", "password123")));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    // ---- Store: TryUpdatePasswordHashAsync ----

    [Fact]
    public async Task AC8_TryUpdatePasswordHash_persists_and_returns_false_for_unknown_id()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var store = new FileUserStore(path);
        var user = Users.Make("a@example.com", hash: "old");
        await store.TryAddAsync(user, CancellationToken.None);

        Assert.True(await store.TryUpdatePasswordHashAsync(user.Id, "new", CancellationToken.None));
        Assert.False(await store.TryUpdatePasswordHashAsync("missing", "x", CancellationToken.None));

        var reloaded = await new FileUserStore(path).FindByEmailAsync("a@example.com", CancellationToken.None);
        Assert.Equal("new", reloaded!.PasswordHash);
        Assert.False(File.Exists(path + ".tmp"));
    }

    // ---- NIT-2: cache must still see changes made outside this instance ----

    [Fact]
    public async Task AC8_Cached_store_picks_up_users_written_by_another_instance()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var reader = new FileUserStore(path);
        Assert.Null(await reader.FindByEmailAsync("b@example.com", CancellationToken.None)); // primes the cache

        await new FileUserStore(path).TryAddAsync(Users.Make("b@example.com"), CancellationToken.None);

        Assert.NotNull(await reader.FindByEmailAsync("b@example.com", CancellationToken.None));
    }

    [Fact]
    public async Task AC11_Cached_store_still_fails_loudly_when_the_file_is_corrupted_later()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var store = new FileUserStore(path);
        await store.TryAddAsync(Users.Make("a@example.com"), CancellationToken.None);

        await File.WriteAllTextAsync(path, "{ corrupt");
        var before = await File.ReadAllBytesAsync(path);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.FindByEmailAsync("a@example.com", CancellationToken.None));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }
}
