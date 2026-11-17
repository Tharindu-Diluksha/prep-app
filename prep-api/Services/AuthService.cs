using Microsoft.AspNetCore.Identity;
using PrepApi.Models;

namespace PrepApi.Services;

/// <summary>Outcome of a signup: either the created user or EmailTaken.</summary>
public sealed record SignupResult(UserResponse? User, bool EmailTaken)
{
    public static SignupResult Created(UserResponse user) => new(user, false);
    public static SignupResult Taken() => new(null, true);
}

public interface IAuthService
{
    /// <summary>Creates the account. Input must already have passed <see cref="AuthValidator.ValidateSignup"/>.</summary>
    Task<SignupResult> SignupAsync(SignupRequest request, CancellationToken ct = default);

    /// <summary>Returns the user when the credentials match, otherwise null (unknown email and wrong password look the same).</summary>
    Task<UserRecord?> ValidateCredentialsAsync(LoginRequest request, CancellationToken ct = default);
}

public sealed class AuthService(IUserStore store, IPasswordHasher<UserRecord> hasher) : IAuthService
{
    // Placeholder user for the dummy hash: PasswordHasher<T> ignores the user, but the contract is non-null.
    private static readonly UserRecord NoUser = new("", "", "", "", DateTimeOffset.MinValue);

    // Verified against for unknown emails so both failure paths cost one hash verification.
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<UserRecord>().HashPassword(NoUser, Guid.NewGuid().ToString()));

    public async Task<SignupResult> SignupAsync(SignupRequest request, CancellationToken ct = default)
    {
        var name = request.Name!.Trim();
        var email = AuthValidator.NormalizeEmail(request.Email!);

        var user = new UserRecord(Guid.NewGuid().ToString("N"), name, email, PasswordHash: "", DateTimeOffset.UtcNow);
        user = user with { PasswordHash = hasher.HashPassword(user, request.Password!) };

        return await store.TryAddAsync(user, ct)
            ? SignupResult.Created(new UserResponse(user.Name, user.Email))
            : SignupResult.Taken();
    }

    public async Task<UserRecord?> ValidateCredentialsAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return null;
        }

        var user = await store.FindByEmailAsync(AuthValidator.NormalizeEmail(request.Email), ct);
        if (user is null)
        {
            hasher.VerifyHashedPassword(NoUser, DummyHash.Value, request.Password);
            return null;
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // The stored hash uses an older format or iteration count: upgrade it now that we know the password.
            var upgraded = user with { PasswordHash = hasher.HashPassword(user, request.Password) };
            if (await store.TryUpdatePasswordHashAsync(user.Id, upgraded.PasswordHash, ct))
            {
                user = upgraded;
            }
        }

        return user;
    }
}
