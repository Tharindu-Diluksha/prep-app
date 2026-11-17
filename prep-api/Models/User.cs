namespace PrepApi.Models;

/// <summary>A persisted account. Email is stored normalised (trimmed, lower-case).</summary>
public sealed record UserRecord(string Id, string Name, string Email, string PasswordHash, DateTimeOffset CreatedAt);
