using System;

namespace Robots
{
    /// <summary>
    /// One Allow or Disallow rule of a robots.txt group. Immutable.
    /// </summary>
    public sealed class RobotsRule
    {
        /// <summary>
        /// Creates a rule.
        /// </summary>
        /// <param name="kind">Whether the rule allows or disallows.</param>
        /// <param name="path">The path pattern as written in robots.txt, such as "/private/" or "/*.pdf$".</param>
        public RobotsRule(RobotsRuleKind kind, string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            Kind = kind;
            Path = path;
            Pattern = RobotsPattern.NormalizePattern(path);
        }

        /// <summary>Whether the rule allows or disallows.</summary>
        public RobotsRuleKind Kind { get; }

        /// <summary>The path pattern as written in robots.txt.</summary>
        public string Path { get; }

        /// <summary>The pattern normalized for matching (RFC 9309 percent-encoding).</summary>
        internal string Pattern { get; }

        /// <summary>
        /// True when the rule matches a path and query, following RFC 9309: the pattern matches from the start, is
        /// case-sensitive, '*' matches any characters and a final '$' anchors the end.
        /// </summary>
        /// <param name="pathAndQuery">The path and query of a Url, such as "/products/1?page=2".</param>
        public bool Matches(string pathAndQuery)
        {
            if (pathAndQuery == null)
                throw new ArgumentNullException(nameof(pathAndQuery));

            return MatchesNormalized(RobotsPattern.NormalizePath(pathAndQuery));
        }

        internal bool MatchesNormalized(string normalizedPath)
        {
            return RobotsPattern.Matches(normalizedPath, Pattern);
        }

        /// <summary>The rule as a robots.txt line, such as "Disallow: /private/".</summary>
        public override string ToString()
        {
            return string.Concat(Kind == RobotsRuleKind.Allow ? "Allow: " : "Disallow: ", Path);
        }
    }
}
