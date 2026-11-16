using System.Text.RegularExpressions;
using PrepApi.Models;

namespace PrepApi.Services;

/// <summary>Input rules for the auth endpoints. Returns field errors keyed name/email/password; empty = valid.</summary>
public static partial class AuthValidator
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static Dictionary<string, string[]> ValidateSignup(SignupRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (name.Length > NameMaxLength)
        {
            errors["name"] = [$"Name must be at most {NameMaxLength} characters."];
        }

        AddEmailErrors(request.Email, errors);

        // Passwords are never trimmed: spaces are significant.
        var password = request.Password;
        if (string.IsNullOrEmpty(password) || password.Length < PasswordMinLength)
        {
            errors["password"] = [$"Password must be at least {PasswordMinLength} characters."];
        }
        else if (password.Length > PasswordMaxLength)
        {
            errors["password"] = [$"Password must be at most {PasswordMaxLength} characters."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateLogin(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        AddEmailErrors(request.Email, errors);

        // Only presence is checked: a short password is a generic 401 later, revealing nothing.
        if (string.IsNullOrEmpty(request.Password))
        {
            errors["password"] = ["Password is required."];
        }

        return errors;
    }

    private static void AddEmailErrors(string? rawEmail, Dictionary<string, string[]> errors)
    {
        var email = rawEmail?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            errors["email"] = ["Email is required."];
        }
        else if (email.Length > EmailMaxLength || !EmailPattern().IsMatch(email))
        {
            errors["email"] = ["Enter a valid email address."];
        }
    }
}
