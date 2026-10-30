using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Robots.Model
{
    public abstract class Entry
    {
        private const string USER_AGENT_KEYWORD = "user-agent";
        private const string DISALLOW_KEYWORD = "disallow";
        private const string ALLOW_KEYWORD = "allow";
        private const string CRAWL_DELAY_KEYWORD = "crawl-delay";
        private const string SITEMAP_KEYWORD = "sitemap";

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

        public static bool TryParse(Uri baseUri, string entryText, [NotNullWhen(true)] out Entry? entry)
        {
            if (baseUri == null)
                throw new ArgumentNullException(nameof(baseUri));

            entry = null;

            if (string.IsNullOrEmpty(entryText))
                return false;

            // RFC 9309, section 2.2.3: '#' starts a comment anywhere on the line.
            string comment = string.Empty;
            int commentPosition = entryText.IndexOf('#');
            if (commentPosition >= 0)
            {
                comment = entryText.Substring(commentPosition + 1);
                entryText = entryText.Substring(0, commentPosition);
            }

            // A UTF-8 byte order mark that reached the text, for example through LoadContent, is skipped.
            entryText = entryText.TrimStart('﻿').Trim();
            if (entryText.Length == 0)
            {
                if (commentPosition < 0)
                    return false;

                entry = CreateEntry(EntryType.Comment);
                entry.Comment = comment;
                return true;
            }

            // RFC 9309, section 2.2: <key> *WS ":" *WS <value>, with case-insensitive keys.
            int separator = entryText.IndexOf(':');
            if (separator < 0)
                return false;

            string key = entryText.Substring(0, separator).Trim();
            string value = entryText.Substring(separator + 1).Trim();

            try
            {
                if (string.Equals(key, USER_AGENT_KEYWORD, StringComparison.OrdinalIgnoreCase))
                {
                    string userAgent = value.TrimEnd('?');
                    if (userAgent.Length == 0)
                        return false;

                    entry = new UserAgentEntry { UserAgent = userAgent, Comment = comment };
                }
                else if (string.Equals(key, DISALLOW_KEYWORD, StringComparison.OrdinalIgnoreCase))
                {
                    // An empty Disallow disallows nothing.
                    if (value.Length == 0)
                        return false;

                    entry = CreateRule(new DisallowEntry(), baseUri, value, comment);
                }
                else if (string.Equals(key, ALLOW_KEYWORD, StringComparison.OrdinalIgnoreCase))
                {
                    // An empty Allow allows nothing beyond what is already allowed.
                    if (value.Length == 0)
                        return false;

                    entry = CreateRule(new AllowEntry(), baseUri, value, comment);
                }
                else if (string.Equals(key, CRAWL_DELAY_KEYWORD, StringComparison.OrdinalIgnoreCase))
                {
                    int crawlDelay;
                    entry = new CrawlDelayEntry
                    {
                        Comment = comment,
                        CrawlDelay = int.TryParse(value.TrimEnd('?'), NumberStyles.Integer, CultureInfo.InvariantCulture, out crawlDelay)
                            ? crawlDelay
                            : 0
                    };
                }
                else if (string.Equals(key, SITEMAP_KEYWORD, StringComparison.OrdinalIgnoreCase))
                {
                    entry = new SitemapEntry { Comment = comment, SitemapUrl = value.TrimEnd('?') };
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                entry = null;
                return false;
            }

            return true;
        }

        private static UrlEntry CreateRule(UrlEntry rule, Uri baseUri, string value, string comment)
        {
            Uri? url;
            Uri.TryCreate(baseUri, value, out url);

            rule.Comment = comment;
            rule.Url = url;
            rule.Inverted = EndsWithDollar(value);
            // Matching uses the pattern as written: Url would resolve "..", and '*' and '$' have no meaning in it.
            rule.PathPattern = RobotsPattern.NormalizePattern(value);
            return rule;
        }

        private static bool EndsWithDollar(string text)
        {
            return text.Length > 0 && text[text.Length - 1] == '$';
        }
    }
}