namespace PrepApi.Models;

// Request fields are nullable so missing JSON properties reach validation instead of failing binding.
public sealed record SignupRequest(string? Name, string? Email, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

/// <summary>The only user data the API ever returns: no id, password or hash.</summary>
public sealed record UserResponse(string Name, string Email);
