using System.Security.Cryptography;
using System.Text;

namespace PollBuilder.Contracts.Infrastructure
{
    /// <summary>
    /// Creates and hashes the opaque tokens that identify a poll's creator and a respondent's browser.
    /// Shared so PollService and VotingService derive identical hashes from the same token.
    /// </summary>
    public static class TokenGenerator
    {
        /// <summary>Creates a 192-bit random token, URL-safe so it can live in a header or localStorage.</summary>
        public static string Create() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

        /// <summary>SHA-256, hex-encoded. Tokens are high-entropy random values, so no salt is needed.</summary>
        public static string Hash(string token) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        /// <summary>Constant-time comparison of a presented token against a stored hash.</summary>
        public static bool Matches(string? presentedToken, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(presentedToken) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Hash(presentedToken.Trim())),
                Encoding.UTF8.GetBytes(storedHash));
        }
    }
}
