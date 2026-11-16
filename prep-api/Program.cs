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
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Read-only in-memory data: one instance for the app's lifetime is enough.
builder.Services.AddSingleton<IProductService, ProductService>();

var app = builder.Build();

// No-arg UseCors adds the middleware; policies are applied per endpoint via RequireCors.
app.UseCors();

app.MapGet("/", () => "Hello World!");

var products = app.MapGroup("/api/products")
    .RequireCors(AngularClientPolicy);

// IProductService is injected as a handler parameter (resolved from the request scope).
products.MapGet("", (IProductService service) => TypedResults.Ok(service.GetAllProducts()));

app.Run();
