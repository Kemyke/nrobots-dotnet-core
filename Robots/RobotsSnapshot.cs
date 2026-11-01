using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Robots
{
    /// <summary>
    /// One loaded robots.txt: everything a <see cref="Robots"/> instance knows at one moment. Immutable, so every
    /// member answers from the same load even while another thread reloads the <see cref="Robots"/> instance.
    /// </summary>
    public sealed class RobotsSnapshot
    {
        internal static readonly RobotsSnapshot Empty =
            new RobotsSnapshot(null, Array.Empty<RobotsGroup>(), Array.Empty<string>(), Array.Empty<RobotsParseError>());

        internal RobotsSnapshot(Uri? baseUri, IReadOnlyList<RobotsGroup> groups, IReadOnlyList<string> sitemaps, IReadOnlyList<RobotsParseError> parseErrors)
        {
            BaseUri = baseUri;
            Groups = groups;
            Sitemaps = sitemaps;
            ParseErrors = parseErrors;
        }

        internal RobotsSnapshot(Uri? baseUri, ParseResult result)
            : this(baseUri, result.Groups.AsReadOnly(), result.Sitemaps.AsReadOnly(), result.Errors.AsReadOnly())
        {
        }

        /// <summary>The Url relative Urls are resolved against, or null when none is known.</summary>
        public Uri? BaseUri { get; }

        /// <summary>The groups as written, in file order. Groups added with Add follow the loaded ones.</summary>
        public IReadOnlyList<RobotsGroup> Groups { get; }

        /// <summary>The Sitemap values, in file order.</summary>
        public IReadOnlyList<string> Sitemaps { get; }

        /// <summary>The lines that were skipped, in the order they were read. Empty for a clean file.</summary>
        public IReadOnlyList<RobotsParseError> ParseErrors { get; }

        /// <summary>Checks a Url for the "*" group. See <see cref="Allowed(Uri, string)"/>.</summary>
        public bool Allowed(string url)
        {
            return Allowed(ToUri(url), UserAgents.AllAgents);
        }

        /// <summary>Checks a Url for a crawler. See <see cref="Allowed(Uri, string)"/>.</summary>
        public bool Allowed(string url, string userAgent)
        {
            return Allowed(ToUri(url), userAgent);
        }

        /// <summary>Checks a Url for the "*" group. See <see cref="Allowed(Uri, string)"/>.</summary>
        public bool Allowed(Uri uri)
        {
            return Allowed(uri, UserAgents.AllAgents);
        }

        /// <summary>
        /// Checks whether a crawler may access a Url, following RFC 9309: the groups for the crawler's product token
        /// apply (or the "*" groups when none match), the longest matching rule wins, and Allow wins a tie.
        /// </summary>
        /// <param name="uri">The Url to check. A relative Url resolves against <see cref="BaseUri"/>.</param>
        /// <param name="userAgent">
        /// The crawler's product token, such as "ExampleBot", or a User-Agent string that starts with it, such as
        /// "ExampleBot/2.1 (+https://example.com/bot)". Matching is case-insensitive.
        /// </param>
        /// <exception cref="InvalidOperationException">The Url is relative, there are rules, and no BaseUri is known.</exception>
        public bool Allowed(Uri uri, string userAgent)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (userAgent == null)
                throw new ArgumentNullException(nameof(userAgent));

            if (Groups.Count == 0)
                return true;

            if (!uri.IsAbsoluteUri)
            {
                if (BaseUri == null)
                    throw new InvalidOperationException("Set BaseUri before checking a relative Url.");

                uri = new Uri(BaseUri, uri);
            }

            return RobotsGroup.IsAllowed(FindGroups(userAgent), RobotsPattern.NormalizePath(uri.PathAndQuery));
        }

        /// <summary>
        /// The group that applies to a crawler, or null when none does: the groups for its product token, or the "*"
        /// groups when none match. Several groups that apply are combined into one, as RFC 9309, section 2.2.1,
        /// requires: their User-agent values, their rules in file order, and the first Crawl-delay.
        /// </summary>
        /// <param name="userAgent">The crawler's product token, or a User-Agent string that starts with it.</param>
        public RobotsGroup? GetGroup(string userAgent)
        {
            if (userAgent == null)
                throw new ArgumentNullException(nameof(userAgent));

            var groups = FindGroups(userAgent);
            if (groups.Count <= 1)
                return groups.Count == 0 ? null : groups[0];

            var userAgents = new List<string>();
            var rules = new List<RobotsRule>();
            TimeSpan? crawlDelay = null;
            foreach (var group in groups)
            {
                foreach (var agent in group.UserAgents)
                {
                    if (!userAgents.Exists(existing => string.Equals(existing, agent, StringComparison.OrdinalIgnoreCase)))
                        userAgents.Add(agent);
                }

                rules.AddRange(group.Rules);
                crawlDelay = crawlDelay ?? group.CrawlDelay;
            }

            return new RobotsGroup(userAgents.AsReadOnly(), rules.AsReadOnly(), crawlDelay);
        }

        /// <summary>The Crawl-delay for the "*" group in whole seconds. See <see cref="GetCrawlDelay(string)"/>.</summary>
        public int GetCrawlDelay()
        {
            return GetCrawlDelay(UserAgents.AllAgents);
        }

        /// <summary>
        /// The Crawl-delay for a crawler in whole seconds, rounded up so it never waits less than the site asked:
        /// "Crawl-delay: 1.5" gives 2. 0 when none applies. <see cref="TryGetCrawlDelay(string, out TimeSpan)"/> gives
        /// the exact value.
        /// </summary>
        /// <param name="userAgent">The crawler's product token, or a User-Agent string that starts with it.</param>
        public int GetCrawlDelay(string userAgent)
        {
            TimeSpan crawlDelay;
            return TryGetCrawlDelay(userAgent, out crawlDelay) ? RobotsLine.ToWholeSecondsRoundedUp(crawlDelay) : 0;
        }

        /// <summary>The exact Crawl-delay for the "*" group. See <see cref="TryGetCrawlDelay(string, out TimeSpan)"/>.</summary>
        public bool TryGetCrawlDelay(out TimeSpan crawlDelay)
        {
            return TryGetCrawlDelay(UserAgents.AllAgents, out crawlDelay);
        }

        /// <summary>
        /// The exact Crawl-delay for a crawler, fractions included: the first valid one of the groups that apply.
        /// </summary>
        /// <param name="userAgent">The crawler's product token, or a User-Agent string that starts with it.</param>
        /// <param name="crawlDelay">The delay, or TimeSpan.Zero when the method returns false.</param>
        /// <returns>False when no group that applies has a valid Crawl-delay.</returns>
        public bool TryGetCrawlDelay(string userAgent, out TimeSpan crawlDelay)
        {
            if (userAgent == null)
                throw new ArgumentNullException(nameof(userAgent));

            foreach (var group in FindGroups(userAgent))
            {
                if (group.CrawlDelay.HasValue)
                {
                    crawlDelay = group.CrawlDelay.Value;
                    return true;
                }
            }

            crawlDelay = TimeSpan.Zero;
            return false;
        }

        internal RobotsSnapshot WithBaseUri(Uri? baseUri)
        {
            return new RobotsSnapshot(baseUri, Groups, Sitemaps, ParseErrors);
        }

        /// <summary>
        /// This snapshot with another parse appended: the way 1.x combined two loads into one instance.
        /// </summary>
        internal RobotsSnapshot Append(ParseResult added)
        {
            return new RobotsSnapshot(BaseUri, Concat(Groups, added.Groups), Concat(Sitemaps, added.Sitemaps), Concat(ParseErrors, added.Errors));
        }

        // RFC 9309, section 2.2.1: every group whose product token matches the crawler's, case-insensitively; when
        // none does, every "*" group.
        private List<RobotsGroup> FindGroups(string userAgent)
        {
            string token = RobotsPattern.IsGlobalAgent(userAgent) ? string.Empty : RobotsPattern.ProductToken(userAgent);

            var specific = new List<RobotsGroup>();
            var global = new List<RobotsGroup>();
            foreach (var group in Groups)
            {
                if (group.IsFor(token))
                    specific.Add(group);
                if (group.IsGlobal)
                    global.Add(group);
            }

            return specific.Count > 0 ? specific : global;
        }

        private static ReadOnlyCollection<T> Concat<T>(IReadOnlyList<T> first, List<T> second)
        {
            var all = new List<T>(first.Count + second.Count);
            all.AddRange(first);
            all.AddRange(second);
            return all.AsReadOnly();
        }

        internal static Uri ToUri(string url)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));

            return new Uri(url, UriKind.RelativeOrAbsolute);
        }
    }
}
