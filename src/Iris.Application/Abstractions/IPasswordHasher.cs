namespace Iris.Application.Abstractions;

/// <summary>Hashes and verifies local user passwords. Implemented with PBKDF2 in the infrastructure layer.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
