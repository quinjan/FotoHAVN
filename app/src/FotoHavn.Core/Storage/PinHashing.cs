using System.Security.Cryptography;
using System.Text;

namespace FotoHavn.Core.Storage;

/// <summary>A stored PIN: the PBKDF2 hash, its salt, and the iteration count (the <c>pin_*</c> columns).</summary>
public sealed record PinCredential(byte[] Hash, byte[] Salt, int Iterations);

/// <summary>Creates and checks PIN credentials. The real one is <see cref="Pbkdf2PinHasher"/>; tests inject a fast one.</summary>
public interface IPinHasher
{
    /// <summary>Hashes <paramref name="pin"/> with a new random salt.</summary>
    PinCredential Create(string pin);

    /// <summary>Whether <paramref name="pin"/> matches <paramref name="credential"/>, compared in constant time.</summary>
    bool Verify(string pin, PinCredential credential);
}

/// <summary>PBKDF2-SHA256 with a 16-byte random salt and at least 210,000 iterations (plan section 10, PIN).</summary>
public sealed class Pbkdf2PinHasher : IPinHasher
{
    public const int SaltLength = 16;
    public const int HashLength = 32;
    public const int MinimumIterations = 210_000;

    private readonly int _iterations;

    public Pbkdf2PinHasher()
        : this(MinimumIterations)
    {
    }

    /// <param name="iterations">At least <see cref="MinimumIterations"/>.</param>
    public Pbkdf2PinHasher(int iterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, MinimumIterations);
        _iterations = iterations;
    }

    public PinCredential Create(string pin)
    {
        ArgumentException.ThrowIfNullOrEmpty(pin);
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        return new PinCredential(Derive(pin, salt, _iterations), salt, _iterations);
    }

    /// <summary>
    /// Re-derives with the credential's own salt and iteration count, so a PIN stored with more iterations later still
    /// verifies. A credential with fewer than <see cref="MinimumIterations"/> never verifies.
    /// </summary>
    public bool Verify(string pin, PinCredential credential)
    {
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentNullException.ThrowIfNull(credential);
        if (credential.Iterations < MinimumIterations || credential.Hash.Length != HashLength || credential.Salt.Length != SaltLength)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Derive(pin, credential.Salt, credential.Iterations), credential.Hash);
    }

    private static byte[] Derive(string pin, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin), salt, iterations, HashAlgorithmName.SHA256, HashLength);
}
