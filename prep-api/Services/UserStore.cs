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

    /// <summary>Replaces a user's password hash (e.g. after a hash-format upgrade). False if the user no longer exists.</summary>
    Task<bool> TryUpdatePasswordHashAsync(string userId, string passwordHash, CancellationToken ct);
}

/// <summary>
/// Stores users as a JSON array in one file. All access is serialised by a single lock, and writes go to
/// a temp file that then replaces the real one, so the file is never half-written.
/// The parsed users are cached and only re-read when the file's timestamp or size changes,
/// so lookups cost a file stat instead of a full read and parse.
/// Only safe for one process: register as a singleton.
/// </summary>
public sealed class FileUserStore : IUserStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Guarded by _lock.
    private List<UserRecord>? _cache;
    private (DateTime LastWriteUtc, long Length) _cachedVersion;

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

            await WriteAtomicallyAsync([.. users, user], ct);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> TryUpdatePasswordHashAsync(string userId, string passwordHash, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var users = await LoadAsync(ct);
            var index = users.FindIndex(u => u.Id == userId);
            if (index < 0)
            {
                return false;
            }

            var updated = new List<UserRecord>(users);
            updated[index] = updated[index] with { PasswordHash = passwordHash };
            await WriteAtomicallyAsync(updated, ct);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool EmailEquals(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Missing file: created as []. Empty file: treated as []. Invalid JSON: throws and leaves the file alone.
    /// Returns the cached list when the file has not changed since it was last read or written.
    /// The returned list must not be mutated: writers build a new list.
    /// </summary>
    private async Task<List<UserRecord>> LoadAsync(CancellationToken ct)
    {
        var info = new FileInfo(_path);
        if (!info.Exists)
        {
            var empty = new List<UserRecord>();
            await WriteAtomicallyAsync(empty, ct);
            return empty;
        }

        var version = (info.LastWriteTimeUtc, info.Length);
        if (_cache is not null && version == _cachedVersion)
        {
            return _cache;
        }

        var json = await File.ReadAllTextAsync(_path, ct);
        List<UserRecord> users;
        if (string.IsNullOrWhiteSpace(json))
        {
            users = [];
        }
        else
        {
            try
            {
                users = JsonSerializer.Deserialize<List<UserRecord>>(json, JsonOptions) ?? [];
            }
            catch (JsonException ex)
            {
                _cache = null;
                // Deliberately no file content in the message: it holds personal data.
                throw new InvalidDataException($"The users file '{_path}' is not valid JSON. Fix or remove it manually.", ex);
            }
        }

        _cache = users;
        _cachedVersion = version;
        return users;
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

        var info = new FileInfo(_path);
        _cache = users;
        _cachedVersion = (info.LastWriteTimeUtc, info.Length);
    }
}
