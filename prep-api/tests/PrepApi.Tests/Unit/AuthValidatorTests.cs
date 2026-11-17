using PrepApi.Models;
using PrepApi.Services;

namespace PrepApi.Tests.Unit;

public class AuthValidatorTests
{
    private static SignupRequest ValidSignup() => new("Ada Lovelace", "ada@example.com", "password123");

    [Fact]
    public void AC7_Signup_valid_request_has_no_errors()
    {
        Assert.Empty(AuthValidator.ValidateSignup(ValidSignup()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AC7_Signup_rejects_blank_name(string? name)
    {
        var errors = AuthValidator.ValidateSignup(ValidSignup() with { Name = name });
        Assert.True(errors.ContainsKey("name"));
        Assert.NotEmpty(errors["name"]);
    }

    [Fact]
    public void AC7_Signup_rejects_name_over_100_chars_but_accepts_exactly_100()
    {
        Assert.True(AuthValidator.ValidateSignup(ValidSignup() with { Name = new string('a', 101) }).ContainsKey("name"));
        Assert.Empty(AuthValidator.ValidateSignup(ValidSignup() with { Name = new string('a', 100) }));
    }

    [Fact]
    public void AC7_Signup_accepts_unicode_name()
    {
        Assert.Empty(AuthValidator.ValidateSignup(ValidSignup() with { Name = "Zoë 李" }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plainaddress")]
    [InlineData("missing-at.example.com")]
    [InlineData("a@b")]
    [InlineData("a b@example.com")]
    [InlineData("@example.com")]
    public void AC7_Signup_rejects_missing_or_malformed_email(string? email)
    {
        var errors = AuthValidator.ValidateSignup(ValidSignup() with { Email = email });
        Assert.True(errors.ContainsKey("email"));
        Assert.NotEmpty(errors["email"]);
    }

    [Fact]
    public void AC7_Signup_rejects_email_over_254_chars()
    {
        var local = new string('a', 245);
        var tooLong = $"{local}@example.com"; // 245 + 12 = 257
        Assert.True(tooLong.Length > 254);
        Assert.True(AuthValidator.ValidateSignup(ValidSignup() with { Email = tooLong }).ContainsKey("email"));
    }

    [Fact]
    public void AC7_Signup_accepts_email_with_surrounding_whitespace()
    {
        // Email is trimmed before validation.
        Assert.Empty(AuthValidator.ValidateSignup(ValidSignup() with { Email = "  ada@example.com  " }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    public void AC7_Signup_rejects_short_or_missing_password(string? password)
    {
        var errors = AuthValidator.ValidateSignup(ValidSignup() with { Password = password });
        Assert.True(errors.ContainsKey("password"));
        Assert.NotEmpty(errors["password"]);
    }

    [Fact]
    public void AC7_Signup_password_length_bounds_are_8_to_128()
    {
        Assert.Empty(AuthValidator.ValidateSignup(ValidSignup() with { Password = new string('p', 8) }));
        Assert.Empty(AuthValidator.ValidateSignup(ValidSignup() with { Password = new string('p', 128) }));
        Assert.True(AuthValidator.ValidateSignup(ValidSignup() with { Password = new string('p', 129) }).ContainsKey("password"));
    }

    [Fact]
    public void AC7_Signup_password_is_not_trimmed()
    {
        // 6 visible chars + 2 spaces = 8 chars. Valid only because it is not trimmed.
        Assert.Empty(AuthValidator.ValidateSignup(ValidSignup() with { Password = "  abcd  " }));
        // 8 spaces would be valid un-trimmed too.
        Assert.Empty(AuthValidator.ValidateSignup(ValidSignup() with { Password = "        " }));
    }

    [Fact]
    public void AC7_Signup_reports_every_invalid_field_together()
    {
        var errors = AuthValidator.ValidateSignup(new SignupRequest("", "nope", "short"));
        Assert.Contains("name", errors.Keys);
        Assert.Contains("email", errors.Keys);
        Assert.Contains("password", errors.Keys);
    }

    [Fact]
    public void AC7_Signup_null_fields_do_not_throw()
    {
        var errors = AuthValidator.ValidateSignup(new SignupRequest(null, null, null));
        Assert.Equal(3, errors.Count);
    }

    // ---- Login ----

    [Fact]
    public void AC7_Login_valid_request_has_no_errors()
    {
        Assert.Empty(AuthValidator.ValidateLogin(new LoginRequest("ada@example.com", "password123")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void AC7_Login_rejects_missing_or_malformed_email(string? email)
    {
        var errors = AuthValidator.ValidateLogin(new LoginRequest(email, "password123"));
        Assert.True(errors.ContainsKey("email"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AC7_Login_rejects_empty_password(string? password)
    {
        var errors = AuthValidator.ValidateLogin(new LoginRequest("ada@example.com", password));
        Assert.True(errors.ContainsKey("password"));
    }

    [Fact]
    public void AC6_Login_short_nonempty_password_passes_validation()
    {
        // Handled later as a generic 401, so the length rule reveals nothing about accounts.
        Assert.Empty(AuthValidator.ValidateLogin(new LoginRequest("ada@example.com", "abc")));
    }

    // ---- NormalizeEmail ----

    [Fact]
    public void AC5_NormalizeEmail_trims_and_lowercases()
    {
        Assert.Equal("foo@bar.com", AuthValidator.NormalizeEmail("  Foo@Bar.COM "));
    }
}
