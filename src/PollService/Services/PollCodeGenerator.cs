using System.Security.Cryptography;
using PollBuilder.Polls.Repo;
using PollBuilder.Polls.Services;

namespace PollBuilder.Polls.Services
{
    /// <summary>
    /// Generates 6-character poll codes from a 56-symbol alphabet (~30 billion combinations). Ambiguous
    /// glyphs (0/O, 1/l/I) are excluded so a code stays readable when someone reads it aloud or types it
    /// from a projector during a live session.
    /// </summary>
    public class PollCodeGenerator : IPollCodeGenerator
    {
        internal const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        internal const int CodeLength = 6;

        public string Next()
        {
            var buffer = new char[CodeLength];
            for (var i = 0; i < CodeLength; i++)
            {
                buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }

            return new string(buffer);
        }
    }
}
