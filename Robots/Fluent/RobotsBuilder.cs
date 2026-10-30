using System;
using Robots.Model;

namespace Robots.Fluent
{
    public class RobotsBuilder
    {
        private UserAgentEntry? _lastUserAgent;
        private readonly Uri _baseUri;
        private readonly Robots _robots;

        public Robots Robots
        {
            get { return _robots; }
        }

        private RobotsBuilder(Uri baseUri)
        {
            _baseUri = baseUri;
            _robots = new Robots {BaseUri = baseUri};
        }

        public static RobotsBuilder Create(Uri baseUri)
        {
            return new RobotsBuilder(baseUri);
        }

        private UserAgentEntry LastUserAgent
        {
            get
            {
                if (_lastUserAgent == null)
                    throw new InvalidOperationException("Call ForUserAgent before adding Allow or Disallow rules.");

                return _lastUserAgent;
            }
        }

        public RobotsBuilder ForUserAgent(string userAgent)
        {
            _lastUserAgent = new UserAgentEntry {UserAgent = userAgent};

            _robots.AddEntry(_lastUserAgent);

            return this;
        }

        public RobotsBuilder Disallow(string url)
        {
            return DisallowWithComment(url, string.Empty);
        }

        public RobotsBuilder DisallowWithComment(string url, string comment)
        {
            var disallowEntry = new DisallowEntry { Url = new Uri(_baseUri, url), Comment = comment };
            LastUserAgent.AddEntry(disallowEntry);

            return this;
        }

        public RobotsBuilder Disallow(Uri uri)
        {
            return DisallowWithComment(uri, string.Empty);
        }

        public RobotsBuilder DisallowWithComment(Uri uri, string comment)
        {
            var disallowEntry = new DisallowEntry { Url = uri, Comment = comment };
            LastUserAgent.AddEntry(disallowEntry);

            return this;
        }


        public RobotsBuilder Allow(string url)
        {
            return AllowWithComment(url, string.Empty);
        }

        public RobotsBuilder AllowWithComment(string url, string comment)
        {
            var allowEntry = new AllowEntry { Url = new Uri(_baseUri, url), Comment = comment };
            LastUserAgent.AddEntry(allowEntry);

            return this;
        }

        public RobotsBuilder Allow(Uri uri)
        {
            return AllowWithComment(uri, string.Empty);
        }

        public RobotsBuilder AllowWithComment(Uri uri, string comment)
        {
            var allowEntry = new AllowEntry { Url = uri, Comment = comment };
            LastUserAgent.AddEntry(allowEntry);

            return this;
        }
    }
}
