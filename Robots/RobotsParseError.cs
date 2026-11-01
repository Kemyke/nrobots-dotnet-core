using System;
using System.Globalization;

namespace Robots
{
    /// <summary>
    /// A robots.txt line that was skipped. Parsing never throws on bad content; it reports the line here instead.
    /// Immutable.
    /// </summary>
    public sealed class RobotsParseError
    {
        /// <summary>
        /// Creates a parse error.
        /// </summary>
        /// <param name="lineNumber">The 1-based line number.</param>
        /// <param name="line">The line as read, before comments and whitespace are removed.</param>
        /// <param name="kind">Why the line was skipped.</param>
        public RobotsParseError(int lineNumber, string line, RobotsParseErrorKind kind)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));

            LineNumber = lineNumber;
            Line = line;
            Kind = kind;
        }

        /// <summary>
        /// The 1-based line number, counted from the start of the text passed to the Load or Add call that read it.
        /// </summary>
        public int LineNumber { get; }

        /// <summary>The line as read, before comments and whitespace are removed.</summary>
        public string Line { get; }

        /// <summary>Why the line was skipped.</summary>
        public RobotsParseErrorKind Kind { get; }

        /// <summary>The error as text, such as "Line 3 (UnknownDirective): Host: example.com".</summary>
        public override string ToString()
        {
            return string.Concat("Line ", LineNumber.ToString(CultureInfo.InvariantCulture), " (", Kind.ToString(), "): ", Line);
        }
    }
}
