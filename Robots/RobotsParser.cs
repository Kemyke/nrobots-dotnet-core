using System;
using System.Collections.Generic;

namespace Robots
{
    /// <summary>
    /// Turns robots.txt lines into groups, sitemaps and parse errors, following RFC 9309, section 2. Lenient: a bad
    /// line is skipped and reported, never thrown.
    /// </summary>
    internal sealed class RobotsParser
    {
        private readonly List<RobotsGroup> _groups = new List<RobotsGroup>();
        private readonly List<string> _sitemaps = new List<string>();
        private readonly List<RobotsParseError> _errors = new List<RobotsParseError>();

        // The group being read: null before the first User-agent line. An empty list means only empty User-agent
        // lines so far, so the group's rules apply to no crawler.
        private List<string>? _agents;
        private List<RobotsRule>? _rules;
        private TimeSpan? _crawlDelay;

        // True once the group has a body line (Allow, Disallow or Crawl-delay): the next User-agent line starts a
        // new group. Blank lines, comments, Sitemap and unknown lines don't end a group (RFC 9309, section 2.2.4).
        private bool _hasBody;

        private int _lineNumber;

        public void AddLine(string text)
        {
            _lineNumber++;
            var line = RobotsLine.Parse(text);

            switch (line.Kind)
            {
                case RobotsLineKind.UserAgent:
                case RobotsLineKind.EmptyUserAgent:
                    if (_hasBody)
                        Flush();

                    _agents = _agents ?? new List<string>();
                    if (line.Kind == RobotsLineKind.UserAgent)
                        _agents.Add(line.Value);
                    else
                        AddError(text, RobotsParseErrorKind.EmptyUserAgent);
                    break;

                case RobotsLineKind.Allow:
                case RobotsLineKind.Disallow:
                case RobotsLineKind.CrawlDelay:
                case RobotsLineKind.InvalidCrawlDelay:
                    AddBodyLine(line, text);
                    break;

                case RobotsLineKind.Sitemap:
                    _sitemaps.Add(line.Value);
                    break;

                case RobotsLineKind.Malformed:
                    AddError(text, RobotsParseErrorKind.MalformedLine);
                    break;

                case RobotsLineKind.UnknownDirective:
                    AddError(text, RobotsParseErrorKind.UnknownDirective);
                    break;

                default:
                    // Blank and comment-only lines carry nothing.
                    break;
            }
        }

        /// <summary>
        /// Records the last line of a download that the size limit cut off part-way. It is not parsed.
        /// </summary>
        public void AddCutOffLine(string text)
        {
            _lineNumber++;
            AddError(text, RobotsParseErrorKind.LineCutOff);
        }

        public ParseResult Complete()
        {
            Flush();
            return new ParseResult(_groups, _sitemaps, _errors);
        }

        private void AddBodyLine(RobotsLine line, string text)
        {
            // RFC 9309, section 2.2.2: rules before the first User-agent line belong to no group and are ignored.
            if (_agents == null)
            {
                AddError(text, RobotsParseErrorKind.RuleOutsideGroup);
                return;
            }

            _hasBody = true;
            switch (line.Kind)
            {
                case RobotsLineKind.Allow:
                case RobotsLineKind.Disallow:
                    // An empty Allow or Disallow is part of the group's body but matches nothing.
                    if (line.Value.Length > 0)
                    {
                        _rules = _rules ?? new List<RobotsRule>();
                        _rules.Add(new RobotsRule(line.Kind == RobotsLineKind.Allow ? RobotsRuleKind.Allow : RobotsRuleKind.Disallow, line.Value));
                    }
                    break;

                case RobotsLineKind.CrawlDelay:
                    // The group's first valid Crawl-delay wins, as in 1.x.
                    _crawlDelay = _crawlDelay ?? line.CrawlDelay;
                    break;

                default:
                    AddError(text, RobotsParseErrorKind.InvalidCrawlDelay);
                    break;
            }
        }

        private void AddError(string text, RobotsParseErrorKind kind)
        {
            _errors.Add(new RobotsParseError(_lineNumber, text, kind));
        }

        private void Flush()
        {
            // A group of only empty User-agent lines applies to no crawler, so it is dropped.
            if (_agents != null && _agents.Count > 0)
            {
                _groups.Add(new RobotsGroup(
                    _agents.AsReadOnly(),
                    _rules == null ? (IReadOnlyList<RobotsRule>)Array.Empty<RobotsRule>() : _rules.AsReadOnly(),
                    _crawlDelay));
            }

            _agents = null;
            _rules = null;
            _crawlDelay = null;
            _hasBody = false;
        }
    }

    /// <summary>
    /// What one Load or Add call read.
    /// </summary>
    internal sealed class ParseResult
    {
        public ParseResult(List<RobotsGroup> groups, List<string> sitemaps, List<RobotsParseError> errors)
        {
            Groups = groups;
            Sitemaps = sitemaps;
            Errors = errors;
        }

        public List<RobotsGroup> Groups { get; }

        public List<string> Sitemaps { get; }

        public List<RobotsParseError> Errors { get; }
    }
}
