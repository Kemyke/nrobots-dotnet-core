using System;
using System.Collections.Generic;

namespace Robots
{
    /// <summary>
    /// One group of a robots.txt file: its User-agent lines and the rules that follow them. Immutable.
    /// </summary>
    public sealed class RobotsGroup
    {
        internal RobotsGroup(IReadOnlyList<string> userAgents, IReadOnlyList<RobotsRule> rules, TimeSpan? crawlDelay)
        {
            UserAgents = userAgents;
            Rules = rules;
            CrawlDelay = crawlDelay;
        }

        /// <summary>The User-agent values of the group as written, such as "ExampleBot" or "*".</summary>
        public IReadOnlyList<string> UserAgents { get; }

        /// <summary>The group's Allow and Disallow rules, in file order.</summary>
        public IReadOnlyList<RobotsRule> Rules { get; }

        /// <summary>The group's first valid Crawl-delay, or null when it has none.</summary>
        public TimeSpan? CrawlDelay { get; }

        /// <summary>
        /// True when the group applies to the crawler: a "*" group applies to every crawler, and otherwise the
        /// product tokens must match case-insensitively (RFC 9309, section 2.2.1).
        /// </summary>
        /// <param name="userAgent">The crawler's product token, or a User-Agent string that starts with it.</param>
        public bool Matches(string userAgent)
        {
            if (userAgent == null)
                throw new ArgumentNullException(nameof(userAgent));

            return IsGlobal || IsFor(RobotsPattern.ProductToken(userAgent));
        }

        /// <summary>
        /// True when the group's rules allow a path and query: the longest matching rule wins, Allow wins a tie, and
        /// without a matching rule everything is allowed (RFC 9309, section 2.2.2). /robots.txt is always allowed.
        /// </summary>
        /// <param name="pathAndQuery">The path and query of a Url, such as "/products/1?page=2".</param>
        public bool IsAllowed(string pathAndQuery)
        {
            if (pathAndQuery == null)
                throw new ArgumentNullException(nameof(pathAndQuery));

            return IsAllowed(new[] { this }, RobotsPattern.NormalizePath(pathAndQuery));
        }

        /// <summary>The group's User-agent values, such as "User-agent: ExampleBot, OtherBot".</summary>
        public override string ToString()
        {
            return "User-agent: " + string.Join(", ", UserAgents);
        }

        /// <summary>True when a User-agent value of the group is "*".</summary>
        internal bool IsGlobal
        {
            get
            {
                foreach (var userAgent in UserAgents)
                {
                    if (RobotsPattern.IsGlobalAgent(userAgent))
                        return true;
                }

                return false;
            }
        }

        /// <summary>True when a User-agent value of the group has this product token.</summary>
        internal bool IsFor(string productToken)
        {
            if (productToken.Length == 0)
                return false;

            foreach (var userAgent in UserAgents)
            {
                if (!RobotsPattern.IsGlobalAgent(userAgent)
                    && string.Equals(RobotsPattern.ProductToken(userAgent), productToken, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Evaluates the rules of groups that apply together, as RFC 9309 combines them.
        /// </summary>
        internal static bool IsAllowed(IEnumerable<RobotsGroup> groups, string normalizedPath)
        {
            // RFC 9309, section 2.2.2: the /robots.txt URI is implicitly allowed.
            if (normalizedPath == "/robots.txt")
                return true;

            // The length of the longest matching Allow and Disallow pattern, or -1 when none matches.
            int longestAllow = -1;
            int longestDisallow = -1;
            foreach (var group in groups)
            {
                foreach (var rule in group.Rules)
                {
                    bool isAllow = rule.Kind == RobotsRuleKind.Allow;
                    if (rule.Pattern.Length <= (isAllow ? longestAllow : longestDisallow))
                        continue;

                    if (!rule.MatchesNormalized(normalizedPath))
                        continue;

                    if (isAllow)
                        longestAllow = rule.Pattern.Length;
                    else
                        longestDisallow = rule.Pattern.Length;
                }
            }

            return longestDisallow <= longestAllow;
        }
    }
}
