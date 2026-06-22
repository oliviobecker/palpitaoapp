using Palpitao.Api.Entities;

namespace Palpitao.Api.Auth;

public interface IJwtTokenService
{
    /// <summary>Generates a signed JWT access token for the given user.</summary>
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
}
