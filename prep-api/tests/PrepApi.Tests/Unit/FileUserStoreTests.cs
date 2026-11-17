using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PrepApi.Models;
using PrepApi.Services;

namespace PrepApi.Tests.Unit;

public class FileUserStoreTests
{
    private static int UserCount(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetArrayLength();
    }

    [Fact]
    public async Task AC10_Missing_directory_and_file_are_created_on_first_access_with_empty_array()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "nested", "App_Data", "users.json");
        var store = new FileUserStore(path);

        var found = await store.FindByEmailAsync("nobody@example.com", CancellationToken.None);

        Assert.Null(found);
        Assert.True(File.Exists(path));
        Assert.Equal("[]", File.ReadAllText(path).Trim());
    }

    [Fact]
    public async Task AC10_Relative_path_resolves_against_content_root()
    {
        using var dir = new TempDir();
        var options = Options.Create(new AuthOptions { UsersFilePath = "App_Data/users.json" });
        var store = new FileUserStore(options, new FakeHostEnvironment(dir.Path));

        await store.FindByEmailAsync("x@example.com", CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(dir.Path, "App_Data", "users.json")));
    }

    [Fact]
    public async Task AC10_Absolute_path_in_options_is_used_as_is()
    {
        using var dir = new TempDir();
        using var other = new TempDir();
        var abs = other.File("custom.json");
        var options = Options.Create(new AuthOptions { UsersFilePath = abs });
        var store = new FileUserStore(options, new FakeHostEnvironment(dir.Path));

        await store.FindByEmailAsync("x@example.com", CancellationToken.None);

        Assert.True(File.Exists(abs));
    }

    [Fact]
    public async Task AC8_User_added_by_one_instance_is_found_by_a_new_instance_on_same_path()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var user = Users.Make("persist@example.com", "Persisted");

        Assert.True(await new FileUserStore(path).TryAddAsync(user, CancellationToken.None));

        var found = await new FileUserStore(path).FindByEmailAsync("persist@example.com", CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(user.Id, found!.Id);
        Assert.Equal("Persisted", found.Name);
        Assert.Equal("persist@example.com", found.Email);
        Assert.Equal(user.PasswordHash, found.PasswordHash);
    }

    [Fact]
    public async Task AC8_FindByEmail_returns_null_for_unknown_email()
    {
        using var dir = new TempDir();
        var store = new FileUserStore(dir.File("users.json"));
        await store.TryAddAsync(Users.Make("a@example.com"), CancellationToken.None);

        Assert.Null(await store.FindByEmailAsync("b@example.com", CancellationToken.None));
    }

    [Fact]
    public async Task AC5_TryAdd_returns_false_for_duplicate_email_and_keeps_single_entry()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var store = new FileUserStore(path);

        Assert.True(await store.TryAddAsync(Users.Make("dup@example.com"), CancellationToken.None));
        Assert.False(await store.TryAddAsync(Users.Make("dup@example.com"), CancellationToken.None));
        Assert.Equal(1, UserCount(path));
    }

    [Fact]
    public async Task AC9_Raw_file_after_signup_through_AuthService_has_no_plaintext_password()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var service = new AuthService(new FileUserStore(path), new PasswordHasher<UserRecord>());
        const string password = "S3cret-Passw0rd!";

        var result = await service.SignupAsync(new SignupRequest("Grace", "grace@example.com", password));
        Assert.NotNull(result.User);

        var raw = File.ReadAllText(path);
        Assert.DoesNotContain(password, raw);
        Assert.Contains("passwordHash", raw);
    }

    [Fact]
    public async Task AC11_Fifty_parallel_adds_with_distinct_emails_all_succeed_and_file_has_fifty_entries()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var store = new FileUserStore(path);

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            Task.Run(() => store.TryAddAsync(Users.Make($"user{i}@example.com"), CancellationToken.None))));

        Assert.All(results, Assert.True);
        Assert.Equal(50, UserCount(path));
    }

    [Fact]
    public async Task AC11_Twenty_parallel_adds_with_same_email_yield_exactly_one_success()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var store = new FileUserStore(path);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            Task.Run(() => store.TryAddAsync(Users.Make("same@example.com"), CancellationToken.None))));

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(1, UserCount(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n\t ")]
    public async Task AC11_Empty_or_whitespace_file_counts_as_empty(string content)
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        await File.WriteAllTextAsync(path, content);
        var store = new FileUserStore(path);

        Assert.Null(await store.FindByEmailAsync("a@example.com", CancellationToken.None));
        Assert.True(await store.TryAddAsync(Users.Make("a@example.com"), CancellationToken.None));
        Assert.Equal(1, UserCount(path));
    }

    [Fact]
    public async Task AC11_Invalid_json_throws_InvalidDataException_on_read_and_leaves_file_unchanged()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var corrupt = "{ this is not json";
        await File.WriteAllTextAsync(path, corrupt);
        var before = await File.ReadAllBytesAsync(path);
        var store = new FileUserStore(path);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.FindByEmailAsync("a@example.com", CancellationToken.None));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task AC11_Invalid_json_throws_InvalidDataException_on_add_and_leaves_file_unchanged()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        await File.WriteAllTextAsync(path, "[{\"id\": ");
        var before = await File.ReadAllBytesAsync(path);
        var store = new FileUserStore(path);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.TryAddAsync(Users.Make("a@example.com"), CancellationToken.None));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task AC11_No_tmp_file_is_left_after_successful_write()
    {
        using var dir = new TempDir();
        var path = dir.File("users.json");
        var store = new FileUserStore(path);

        await store.TryAddAsync(Users.Make("a@example.com"), CancellationToken.None);
        await store.TryAddAsync(Users.Make("b@example.com"), CancellationToken.None);

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal(new[] { "users.json" }, Directory.GetFiles(dir.Path).Select(Path.GetFileName).ToArray());
    }
}
