using System.Security.Cryptography;

namespace RemSolution.Infrastructure.Identity;

/// <summary>
/// Generates the one-time password a provisioned account is created with.
/// </summary>
internal static class TemporaryPassword
{
    // Characters a person can read off an email and retype without guessing:
    // no O/0, I/l/1, or S/5. The symbol set is small and keyboard-neutral for
    // the same reason — this password is typed once, by hand, possibly on a
    // phone, and every ambiguous glyph is a support call.
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKLMNPQRTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%*-_";

    private const int Length = 14;

    /// <summary>
    /// A random password that satisfies the default Identity policy (upper,
    /// lower, digit, non-alphanumeric) by construction rather than by retrying
    /// until it happens to pass.
    /// </summary>
    public static string Generate()
    {
        var all = Lower + Upper + Digits + Symbols;

        var characters = new List<char>(Length)
        {
            // One from each required class up front; the shuffle below means
            // their position carries no information.
            Pick(Lower),
            Pick(Upper),
            Pick(Digits),
            Pick(Symbols),
        };

        while (characters.Count < Length)
        {
            characters.Add(Pick(all));
        }

        // Fisher-Yates with a cryptographic source, so the four seeded
        // characters are not pinned to the first four positions.
        for (var i = characters.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (characters[i], characters[j]) = (characters[j], characters[i]);
        }

        return new string(characters.ToArray());
    }

    private static char Pick(string alphabet) =>
        alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
