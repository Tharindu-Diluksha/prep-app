using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using PrepApi.Models;
using PrepApi.Services;

namespace PrepApi.Endpoints;

/// <summary>
/// Cookie-session auth endpoints. Temporary, local-dev grade; known gaps (see prep-api/README.md):
/// no CSRF/antiforgery tokens on the POSTs (SameSite=Lax + CORS allow-list only reduce the risk),
/// no rate limiting or lockout on login, SameSite=Lax only works when SPA and API are same-site,
/// and Data Protection keys are not persisted (sessions end on restart, fail across instances).
/// </summary>
public static class AuthEndpoints
{
    public const string InvalidCredentialsMessage = "Invalid email or password.";
    public const string EmailTakenMessage = "An account with this email already exists.";

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, string corsPolicy)
    {
        var group = app.MapGroup("/api/auth").RequireCors(corsPolicy);

        group.MapPost("/signup", SignupAsync);
        group.MapPost("/login", LoginAsync);
        // Cast: a (HttpContext) => Task<T> method group would otherwise bind as a RequestDelegate and drop the result.
        group.MapPost("/logout", (Delegate)LogoutAsync);
        group.MapGet("/me", Me).RequireAuthorization();

        return group;
    }

    private static async Task<Results<JsonHttpResult<UserResponse>, ValidationProblem, ProblemHttpResult>> SignupAsync(
        SignupRequest request, IAuthService auth, CancellationToken ct)
    {
        var errors = AuthValidator.ValidateSignup(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // Signup never signs in: the user is sent to /login (AC-4). Hence 201 without a Location header:
        // there is no resource the anonymous caller could fetch (/me needs a session).
        var result = await auth.SignupAsync(request, ct);
        return result.User is { } user
            ? TypedResults.Json(user, statusCode: StatusCodes.Status201Created)
            : TypedResults.Problem(title: EmailTakenMessage, statusCode: StatusCodes.Status409Conflict);
    }

    private static async Task<Results<Ok<UserResponse>, ValidationProblem, ProblemHttpResult>> LoginAsync(
        LoginRequest request, IAuthService auth, IOptions<AuthOptions> options, HttpContext http, CancellationToken ct)
    {
        var errors = AuthValidator.ValidateLogin(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var user = await auth.ValidateCredentialsAsync(request, ct);
        if (user is null)
        {
            return TypedResults.Problem(title: InvalidCredentialsMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(options.Value.SessionHours),
            });

        return TypedResults.Ok(new UserResponse(user.Name, user.Email));
    }

    private static async Task<NoContent> LogoutAsync(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }

    private static Ok<UserResponse> Me(ClaimsPrincipal user) =>
        TypedResults.Ok(new UserResponse(
            user.FindFirstValue(ClaimTypes.Name) ?? "",
            user.FindFirstValue(ClaimTypes.Email) ?? ""));
}
