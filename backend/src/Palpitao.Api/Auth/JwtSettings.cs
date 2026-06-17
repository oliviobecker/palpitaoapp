namespace Palpitao.Api.Auth;

/// <summary>Strongly-typed JWT configuration (bound from the "Jwt" section).</summary>
public class JwtSettings
{
    public string Issuer { get; set; } = "palpitao";
    public string Audience { get; set; } = "palpitao";
    public string Key { get; set; } = string.Empty;

    /// <summary>Access token lifetime, in hours.</summary>
    public int ExpiresHours { get; set; } = 12;
}
