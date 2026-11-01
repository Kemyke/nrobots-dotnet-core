using System;
using System.Globalization;

namespace Robots
{
    /// <summary>
    /// What a single robots.txt line contains.
    /// </summary>
    internal enum RobotsLineKind
    {
        Empty,
        Comment,
        UserAgent,
        EmptyUserAgent,
        Allow,
        Disallow,
        CrawlDelay,
        InvalidCrawlDelay,
        Sitemap,
        Malformed,
        UnknownDirective,
    }

    /// <summary>
    /// One robots.txt line split into its parts. The only place that knows the line syntax: Entry.TryParse and the
    /// loader both use it.
    /// </summary>
    internal readonly struct RobotsLine
    {
        // TimeSpan.MaxValue is a little over 922,337,203,685.47 seconds. Whole seconds below it keep the tick
        // count inside a long.
        private const double MaxCrawlDelaySeconds = 922_337_203_685d;

        private RobotsLine(RobotsLineKind kind, string value, string comment, TimeSpan crawlDelay)
        {
            Kind = kind;
            Value = value;
            Comment = comment;
            CrawlDelay = crawlDelay;
        }

        public RobotsLineKind Kind { get; }

        /// <summary>The value after the colon, trimmed; empty when the line has none.</summary>
        public string Value { get; }

        /// <summary>The text after '#', or empty when the line has no comment.</summary>
        public string Comment { get; }

        /// <summary>The parsed delay when <see cref="Kind"/> is CrawlDelay.</summary>
        public TimeSpan CrawlDelay { get; }

        /// <summary>
        /// True for lines that belong to a group's body: Allow, Disallow and Crawl-delay, valid or not. A
        /// User-agent line after one of these starts a new group.
        /// </summary>
        public bool IsGroupBody
        {
            get
            {
                return Kind == RobotsLineKind.Allow || Kind == RobotsLineKind.Disallow
                    || Kind == RobotsLineKind.CrawlDelay || Kind == RobotsLineKind.InvalidCrawlDelay;
            }
        }

        public static RobotsLine Parse(string text)
        {
            // RFC 9309, section 2.2.3: '#' starts a comment anywhere on the line.
            string comment = string.Empty;
            int commentPosition = RobotsPattern.IndexOfOrdinal(text, '#');
            if (commentPosition >= 0)
            {
                comment = text.Substring(commentPosition + 1);
                text = text.Substring(0, commentPosition);
            }

            // A UTF-8 byte order mark that reached the text, for example through LoadContent, is skipped.
            text = text.TrimStart('﻿').Trim();
            if (text.Length == 0)
                return new RobotsLine(commentPosition >= 0 ? RobotsLineKind.Comment : RobotsLineKind.Empty, string.Empty, comment, TimeSpan.Zero);

            // RFC 9309, section 2.2: <key> *WS ":" *WS <value>, with case-insensitive keys.
            int separator = RobotsPattern.IndexOfOrdinal(text, ':');
            if (separator < 0)
                return new RobotsLine(RobotsLineKind.Malformed, string.Empty, comment, TimeSpan.Zero);

            string key = text.Substring(0, separator).Trim();
            string value = text.Substring(separator + 1).Trim();

            if (IsKey(key, "user-agent"))
            {
                // 1.x dropped trailing '?' from these values; kept for compatibility.
                string userAgent = value.TrimEnd('?');
                return new RobotsLine(userAgent.Length == 0 ? RobotsLineKind.EmptyUserAgent : RobotsLineKind.UserAgent, userAgent, comment, TimeSpan.Zero);
            }

            if (IsKey(key, "disallow"))
                return new RobotsLine(RobotsLineKind.Disallow, value, comment, TimeSpan.Zero);

            if (IsKey(key, "allow"))
                return new RobotsLine(RobotsLineKind.Allow, value, comment, TimeSpan.Zero);

            if (IsKey(key, "crawl-delay"))
            {
                TimeSpan crawlDelay;
                return TryParseCrawlDelay(value.TrimEnd('?'), out crawlDelay)
                    ? new RobotsLine(RobotsLineKind.CrawlDelay, value, comment, crawlDelay)
                    : new RobotsLine(RobotsLineKind.InvalidCrawlDelay, value, comment, TimeSpan.Zero);
            }

            if (IsKey(key, "sitemap"))
                return new RobotsLine(RobotsLineKind.Sitemap, value.TrimEnd('?'), comment, TimeSpan.Zero);

            return new RobotsLine(RobotsLineKind.UnknownDirective, value, comment, TimeSpan.Zero);
        }

        /// <summary>
        /// Parses a Crawl-delay value in seconds, fractions included: "1.5" is one and a half seconds. Negative values,
        /// NaN, infinity and values a TimeSpan can't hold are invalid.
        /// </summary>
        internal static bool TryParseCrawlDelay(string value, out TimeSpan crawlDelay)
        {
            // NumberStyles.Float: a delay has no thousands separator, so "1,5" is invalid rather than 15 seconds. The
            // range check also rejects NaN, which fails every comparison.
            double seconds;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)
                && seconds >= 0
                && seconds <= MaxCrawlDelaySeconds)
            {
                // From ticks rather than TimeSpan.FromSeconds, which rounds to whole milliseconds on .NET Framework.
                crawlDelay = TimeSpan.FromTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));
                return true;
            }

            crawlDelay = TimeSpan.Zero;
            return false;
        }

        /// <summary>
        /// A delay in whole seconds, rounded up so a crawler never waits less than the site asked for:
        /// 1.5 s gives 2, 0.1 s gives 1. Capped at int.MaxValue.
        /// </summary>
        internal static int ToWholeSecondsRoundedUp(TimeSpan delay)
        {
            if (delay <= TimeSpan.Zero)
                return 0;

            // Division and remainder rather than adding TicksPerSecond - 1 first, which overflows near TimeSpan.MaxValue.
            long seconds = delay.Ticks / TimeSpan.TicksPerSecond + (delay.Ticks % TimeSpan.TicksPerSecond == 0 ? 0 : 1);
            return seconds > int.MaxValue ? int.MaxValue : (int)seconds;
        }

        private static bool IsKey(string key, string name)
        {
            return string.Equals(key, name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
