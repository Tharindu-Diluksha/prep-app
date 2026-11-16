namespace PrepApi.Services;

/// <summary>Bound from the "Auth" config section (env override: Auth__UsersFilePath, Auth__SessionHours).</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Relative paths resolve against the content root.</summary>
    public string UsersFilePath { get; set; } = "App_Data/users.json";

    /// <summary>Absolute session lifetime; no sliding expiration.</summary>
    public double SessionHours { get; set; } = 8;

    public string CookieName { get; set; } = "prep.session";
}
