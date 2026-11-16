using System.Text.Json;
using Microsoft.Extensions.Options;
using PrepApi.Models;

namespace PrepApi.Services;

/// <summary>Account persistence seam: swap the file implementation for a database later.</summary>
public interface IUserStore
{
    /// <summary>Looks up a user by an already-normalised email.</summary>
    Task<UserRecord?> FindByEmailAsync(string normalizedEmail, CancellationToken ct);

    /// <summary>Adds the user unless the email is taken. Check and write are atomic.</summary>
    Task<bool> TryAddAsync(UserRecord user, CancellationToken ct);
}

/// <summary>
/// Stores users as a JSON array in one file. Every operation re-reads the file inside a single lock,
/// and writes go to a temp file that then replaces the real one, so the file is never half-written.
/// Only safe for one process: register as a singleton.
/// </summary>
public sealed class FileUserStore : IUserStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileUserStore(string absolutePath)
    {
        _path = Path.GetFullPath(absolutePath);
    }

    public FileUserStore(IOptions<AuthOptions> options, IHostEnvironment environment)
        : this(Path.Combine(environment.ContentRootPath, options.Value.UsersFilePath))
    {
        // Path.Combine returns UsersFilePath unchanged when it is already absolute.
    }

    public async Task<UserRecord?> FindByEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var users = await LoadAsync(ct);
            return users.FirstOrDefault(u => EmailEquals(u.Email, normalizedEmail));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> TryAddAsync(UserRecord user, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var users = await LoadAsync(ct);
            if (users.Any(u => EmailEquals(u.Email, user.Email)))
            {
                return false;
            }

            users.Add(user);
            await WriteAtomicallyAsync(users, ct);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool EmailEquals(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Missing file: created as []. Empty file: treated as []. Invalid JSON: throws and leaves the file alone.</summary>
    private async Task<List<UserRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            var empty = new List<UserRecord>();
            await WriteAtomicallyAsync(empty, ct);
            return empty;
        }

        var json = await File.ReadAllTextAsync(_path, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<UserRecord>>(json, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            // Deliberately no file content in the message: it holds personal data.
            throw new InvalidDataException($"The users file '{_path}' is not valid JSON. Fix or remove it manually.", ex);
        }
    }

    private async Task WriteAtomicallyAsync(List<UserRecord> users, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(users, JsonOptions), ct);
            File.Move(tempPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
