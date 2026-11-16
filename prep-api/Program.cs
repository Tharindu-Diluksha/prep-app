using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PrepApi.Endpoints;
using PrepApi.Models;
using PrepApi.Services;

const string AngularClientPolicy = "AngularClient";

var builder = WebApplication.CreateBuilder(args);

// Origins come from config: appsettings.Development.json locally,
// App Service configuration (AllowedOrigins__0, AllowedOrigins__1, ...) in Azure.
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularClientPolicy, policy =>
    {
        // AllowCredentials lets the browser send the session cookie; it requires explicit origins.
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Read-only in-memory data: one instance for the app's lifetime is enough.
builder.Services.AddSingleton<IProductService, ProductService>();

// Auth: users in a JSON file (Auth:UsersFilePath), session in an HTTP-only cookie.
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

// Singleton store: its lock must be shared by every request.
builder.Services.AddSingleton<IUserStore, FileUserStore>();
builder.Services.AddSingleton<IPasswordHasher<UserRecord>, PasswordHasher<UserRecord>>();
builder.Services.AddSingleton<IAuthService, AuthService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

// Configured through DI (not read eagerly) so config overrides, e.g. in tests, are honoured.
builder.Services
    .AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthOptions>>((options, auth) =>
    {
        options.Cookie.Name = auth.Value.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // HTTPS is out of scope; Secure follows the request.
        options.ExpireTimeSpan = TimeSpan.FromHours(auth.Value.SessionHours);
        options.SlidingExpiration = false;

        // An API never redirects to a login page: answer 401/403 instead.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();

var app = builder.Build();

// No-arg UseCors adds the middleware; policies are applied per endpoint via RequireCors.
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

var products = app.MapGroup("/api/products")
    .RequireCors(AngularClientPolicy);

// IProductService is injected as a handler parameter (resolved from the request scope).
products.MapGet("", (IProductService service) => TypedResults.Ok(service.GetAllProducts()));

app.MapAuthEndpoints(AngularClientPolicy);

app.Run();

// Exposes the implicit Program class to WebApplicationFactory<Program> in the test project.
public partial class Program;
