using System.Security.Cryptography;
using System.Text;
using FotoHavn.Protocol;

namespace FotoHavn.Host.Headless;

/// <summary>
/// The per-role startup tokens. A WebSocket client proves its role with <c>?token=</c>; the page never declares a role.
/// Each token is 256 random bits from the OS CSPRNG, in lower-case hex, new at every start.
/// </summary>
internal sealed class RoleTokens
{
    private const int TokenBytes = 32;

    public RoleTokens(string operatorToken, string guestToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(operatorToken);
        ArgumentException.ThrowIfNullOrEmpty(guestToken);
        if (string.Equals(operatorToken, guestToken, StringComparison.Ordinal))
        {
            throw new ArgumentException("The operator and guest tokens must differ.", nameof(guestToken));
        }

        Operator = operatorToken;
        Guest = guestToken;
    }

    public string Operator { get; }

    public string Guest { get; }

    public static RoleTokens Create()
    {
        string guest;
        var @operator = NewToken();
        do
        {
            guest = NewToken();
        }
        while (string.Equals(guest, @operator, StringComparison.Ordinal));

        return new RoleTokens(@operator, guest);
    }

    public string For(Role role) => role == Role.Operator ? Operator : Guest;

    /// <summary>Maps a token to its role, comparing in constant time. Null, empty, and unknown tokens map to nothing.</summary>
    public bool TryGetRole(string? token, out Role role)
    {
        role = default;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var candidate = Encoding.UTF8.GetBytes(token);
        var isOperator = CryptographicOperations.FixedTimeEquals(candidate, Encoding.UTF8.GetBytes(Operator));
        var isGuest = CryptographicOperations.FixedTimeEquals(candidate, Encoding.UTF8.GetBytes(Guest));
        if (isOperator)
        {
            role = Role.Operator;
            return true;
        }

        if (isGuest)
        {
            role = Role.Guest;
            return true;
        }

        return false;
    }

    private static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenBytes));
}
