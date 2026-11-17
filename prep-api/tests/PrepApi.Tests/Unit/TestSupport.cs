using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PrepApi.Models;

namespace PrepApi.Tests.Unit;

/// <summary>Unique temp directory per test, deleted on dispose.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "prep-api-tests", Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
    }
}

public sealed class FakeHostEnvironment(string contentRoot) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "PrepApi.Tests";
    public string ContentRootPath { get; set; } = contentRoot;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public static class Users
{
    public static UserRecord Make(string email, string name = "Test User", string hash = "hash") =>
        new(Guid.NewGuid().ToString("N"), name, email, hash, DateTimeOffset.UtcNow);
}
