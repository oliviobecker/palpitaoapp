namespace Palpitao.Api.Auth;

/// <summary>Keeps personal data out of log text while leaving enough to correlate a report.</summary>
public static class LogRedaction
{
    /// <summary>"maria@example.com" → "m***@example.com"; anything that is not an address → "***".</summary>
    public static string Email(string? email)
    {
        var at = email?.IndexOf('@') ?? -1;
        return at <= 0 ? "***" : $"{email![0]}***{email[at..]}";
    }
}
