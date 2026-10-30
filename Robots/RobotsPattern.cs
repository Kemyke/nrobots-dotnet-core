using System;
using System.Text;

namespace Robots
{
    /// <summary>
    /// Path and user-agent matching as defined by RFC 9309, section 2.2.
    /// </summary>
    internal static class RobotsPattern
    {
        private const string HexDigits = "0123456789ABCDEF";

        /// <summary>
        /// Normalizes a rule's path pattern for matching. '*' and '$' keep their special meaning.
        /// </summary>
        public static string NormalizePattern(string pattern)
        {
            return Normalize(pattern, false);
        }

        /// <summary>
        /// Normalizes a URI's path and query for matching. A literal '*' or '$' is percent-encoded, so only a
        /// rule that encodes it, such as "/file-%2A.html", matches it (RFC 9309, section 2.2.3).
        /// </summary>
        public static string NormalizePath(string pathAndQuery)
        {
            return Normalize(pathAndQuery, true);
        }

        // RFC 9309, section 2.2.2: octets outside ASCII are percent-encoded, percent-encoded unreserved
        // characters are decoded, and any other percent-encoding is kept, with upper-case hex digits.
        private static string Normalize(string value, bool encodeSpecialCharacters)
        {
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '%' && i + 2 < value.Length && IsHexDigit(value[i + 1]) && IsHexDigit(value[i + 2]))
                {
                    int octet = HexValue(value[i + 1]) * 16 + HexValue(value[i + 2]);
                    if (IsUnreserved(octet))
                        builder.Append((char)octet);
                    else
                        AppendEncoded(builder, octet);
                    i += 2;
                }
                else if (c > 0x7F)
                {
                    int length = char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]) ? 2 : 1;
                    foreach (byte octet in Encoding.UTF8.GetBytes(value.Substring(i, length)))
                        AppendEncoded(builder, octet);
                    i += length - 1;
                }
                else if (encodeSpecialCharacters && (c == '*' || c == '$'))
                {
                    AppendEncoded(builder, c);
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// True when the normalized path matches the normalized pattern: the pattern is anchored at the start
        /// of the path, '*' matches any sequence of characters, and a '$' at the end anchors the end.
        /// </summary>
        public static bool Matches(string path, string pattern)
        {
            bool anchored = pattern.Length > 0 && pattern[pattern.Length - 1] == '$';
            int patternLength = anchored ? pattern.Length - 1 : pattern.Length;

            if (pattern.IndexOf('*') < 0)
            {
                return anchored
                    ? path.Length == patternLength && string.CompareOrdinal(path, 0, pattern, 0, patternLength) == 0
                    : path.Length >= patternLength && string.CompareOrdinal(path, 0, pattern, 0, patternLength) == 0;
            }

            // The positions in path that the pattern read so far can end at, in increasing order. This
            // follows the reference implementation (google/robotstxt), whose worst case stays
            // O(path length x pattern length) however the webmaster writes the pattern.
            var positions = new int[path.Length + 1];
            int count = 1;
            positions[0] = 0;

            for (int p = 0; p < patternLength; p++)
            {
                char c = pattern[p];
                if (c == '*')
                {
                    count = path.Length - positions[0] + 1;
                    for (int i = 1; i < count; i++)
                        positions[i] = positions[i - 1] + 1;
                }
                else
                {
                    int newCount = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (positions[i] < path.Length && path[positions[i]] == c)
                            positions[newCount++] = positions[i] + 1;
                    }

                    count = newCount;
                    if (count == 0)
                        return false;
                }
            }

            return !anchored || positions[count - 1] == path.Length;
        }

        /// <summary>
        /// True for the "*" group: a value of "*", or "*" followed by whitespace.
        /// </summary>
        public static bool IsGlobalAgent(string userAgent)
        {
            return userAgent.Length > 0 && userAgent[0] == '*'
                && (userAgent.Length == 1 || char.IsWhiteSpace(userAgent[1]));
        }

        /// <summary>
        /// The product token at the start of a user-agent value: letters, '_' and '-' (RFC 9309, section 2.2.1).
        /// "ExampleBot/2.1 (+https://example.com)" gives "ExampleBot".
        /// </summary>
        public static string ProductToken(string userAgent)
        {
            int length = 0;
            while (length < userAgent.Length && IsProductTokenCharacter(userAgent[length]))
                length++;

            return userAgent.Substring(0, length);
        }

        private static bool IsProductTokenCharacter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || c == '-';
        }

        // RFC 3986, section 2.3.
        private static bool IsUnreserved(int octet)
        {
            return (octet >= 'a' && octet <= 'z') || (octet >= 'A' && octet <= 'Z') || (octet >= '0' && octet <= '9')
                || octet == '-' || octet == '.' || octet == '_' || octet == '~';
        }

        private static void AppendEncoded(StringBuilder builder, int octet)
        {
            builder.Append('%').Append(HexDigits[octet >> 4]).Append(HexDigits[octet & 0xF]);
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static int HexValue(char c)
        {
            return c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;
        }
    }
}
