namespace Palpitao.Application.Abstractions;

/// <summary>One-way password hashing. Strength rules live in <c>PasswordPolicy</c>, not here.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
