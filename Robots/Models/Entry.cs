using System;
using System.Diagnostics.CodeAnalysis;

namespace Robots.Model
{
    public abstract class Entry
    {
        protected Entry(EntryType type)
        {
            Type = type;
        }

        public EntryType Type { get; private set; }

        public string? Comment { get; set; }

        public bool HasComment { get { return !string.IsNullOrEmpty(Comment); } }

        public static Entry CreateEntry(EntryType type)
        {
            switch (type)
            {
                case EntryType.Allow:
                    return new AllowEntry();
                case EntryType.Comment:
                    return new CommentEntry();
                case EntryType.Disallow:
                    return new DisallowEntry();
                case EntryType.UserAgent:
                    return new UserAgentEntry();
                case EntryType.CrawlDelay:
                    return new CrawlDelayEntry();
                case EntryType.Sitemap:
                    return new SitemapEntry();
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Parses one robots.txt line. Returns false for lines that hold no entry: blank lines, malformed or unknown
        /// lines, an empty User-agent, Allow or Disallow, and an invalid Crawl-delay.
        /// </summary>
        public static bool TryParse(Uri baseUri, string entryText, [NotNullWhen(true)] out Entry? entry)
        {
            if (baseUri == null)
                throw new ArgumentNullException(nameof(baseUri));

            entry = null;

            if (string.IsNullOrEmpty(entryText))
                return false;

            var line = RobotsLine.Parse(entryText);
            switch (line.Kind)
            {
                case RobotsLineKind.Comment:
                    entry = new CommentEntry { Comment = line.Comment };
                    return true;

                case RobotsLineKind.UserAgent:
                    entry = new UserAgentEntry { UserAgent = line.Value, Comment = line.Comment };
                    return true;

                case RobotsLineKind.Disallow:
                case RobotsLineKind.Allow:
                    // An empty Allow or Disallow matches nothing, so there is no rule to return.
                    if (line.Value.Length == 0)
                        return false;

                    entry = CreateRule(line.Kind == RobotsLineKind.Allow ? new AllowEntry() : (UrlEntry)new DisallowEntry(), baseUri, line.Value, line.Comment);
                    return true;

                case RobotsLineKind.CrawlDelay:
                    entry = new CrawlDelayEntry { Delay = line.CrawlDelay, Comment = line.Comment };
                    return true;

                case RobotsLineKind.Sitemap:
                    entry = new SitemapEntry { SitemapUrl = line.Value, Comment = line.Comment };
                    return true;

                default:
                    return false;
            }
        }

        [SuppressMessage("Usage", "CA2234:Pass system uri objects instead of strings",
            Justification = "value is a robots.txt path pattern that may contain '*' and '$', not a URI; this TryCreate is what turns it into the informational Url.")]
        private static UrlEntry CreateRule(UrlEntry rule, Uri baseUri, string value, string comment)
        {
            Uri? url;
            Uri.TryCreate(baseUri, value, out url);

            rule.Comment = comment;
            rule.Url = url;
            rule.Inverted = value.Length > 0 && value[value.Length - 1] == '$';
            return rule;
        }
    }
}
