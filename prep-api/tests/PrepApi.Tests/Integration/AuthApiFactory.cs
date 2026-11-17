using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PrepApi.Tests.Integration;

/// <summary>
/// Boots the real API (Program) in-process with a temp users file. Only configuration is
/// overridden; handlers, services, the file store and the cookie handler are the real ones.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    public const string AngularOrigin = "http://localhost:4200";

    private readonly Dictionary<string, string?> _config;

    public string UsersFilePath { get; }
    public CapturingLoggerProvider Logs { get; } = new();

    private readonly string _environment;

    public AuthApiFactory(string usersFilePath, double? sessionHours = null, string environment = "Development")
    {
        UsersFilePath = usersFilePath;
        _environment = environment;
        _config = new Dictionary<string, string?>
        {
            ["Auth:UsersFilePath"] = usersFilePath,
            ["AllowedOrigins:0"] = AngularOrigin,
            // Make sure framework logs reach the capturing provider too (appsettings filters them).
            ["Logging:LogLevel:Default"] = "Trace",
            ["Logging:LogLevel:Microsoft.AspNetCore"] = "Trace",
        };
        if (sessionHours is { } h)
        {
            _config["Auth:SessionHours"] = h.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(_config));
        builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddProvider(Logs);
        });
    }

    public HttpClient CreateCookieClient(bool handleCookies = true) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = handleCookies,
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost"),
        });

    public static string NewTempFile(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "prep-api-it", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "users.json");
    }
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _messages);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, System.Collections.Concurrent.ConcurrentQueue<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            sink.Enqueue($"{category}: {formatter(state, exception)} {exception}");
        }
    }
}
