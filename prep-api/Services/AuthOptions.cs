using Microsoft.Extensions.Options;

namespace PrepApi.Services;

/// <summary>Reports every invalid <see cref="AuthOptions"/> value; registered with ValidateOnStart.</summary>
public sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        var failures = options.Validate().ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>Bound from the "Auth" config section (env override: Auth__UsersFilePath, Auth__SessionHours).</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Upper bound for <see cref="SessionHours"/>: 30 days.</summary>
    public const double MaxSessionHours = 24 * 30;

    /// <summary>Relative paths resolve against the content root.</summary>
    public string UsersFilePath { get; set; } = "App_Data/users.json";

    /// <summary>Absolute session lifetime; no sliding expiration. Must be &gt; 0 and &lt;= <see cref="MaxSessionHours"/>.</summary>
    public double SessionHours { get; set; } = 8;

    public string CookieName { get; set; } = "prep.session";

    /// <summary>Returns the configuration problems, empty when valid. Checked at startup.</summary>
    public IEnumerable<string> Validate()
    {
        // Written so NaN fails too (every comparison with NaN is false).
        if (!(SessionHours > 0 && SessionHours <= MaxSessionHours))
        {
            yield return $"{SectionName}:SessionHours must be greater than 0 and at most {MaxSessionHours}.";
        }
        if (string.IsNullOrWhiteSpace(UsersFilePath))
        {
            yield return $"{SectionName}:UsersFilePath is required.";
        }
        if (string.IsNullOrWhiteSpace(CookieName))
        {
            yield return $"{SectionName}:CookieName is required.";
        }
    }
}
