using System;
using System.Collections.Generic;

namespace Robots.Fluent
{
    /// <summary>
    /// Builds robots.txt rules in code. Use it on one thread; share <see cref="Robots"/> once it is built.
    /// </summary>
    public class RobotsBuilder
    {
        private readonly Uri _baseUri;
        private readonly Robots _robots;

        // The groups so far, one per ForUserAgent call. Each change publishes a new snapshot built from them.
        private readonly List<KeyValuePair<string, List<RobotsRule>>> _groups = new List<KeyValuePair<string, List<RobotsRule>>>();

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

        public RobotsBuilder ForUserAgent(string userAgent)
        {
            _groups.Add(new KeyValuePair<string, List<RobotsRule>>(userAgent, new List<RobotsRule>()));
            Publish();

            return this;
        }

        public RobotsBuilder Disallow(string url)
        {
            return DisallowWithComment(new Uri(_baseUri, url), string.Empty);
        }

        public RobotsBuilder DisallowWithComment(string url, string comment)
        {
            return DisallowWithComment(new Uri(_baseUri, url), comment);
        }

        public RobotsBuilder Disallow(Uri uri)
        {
            return DisallowWithComment(uri, string.Empty);
        }

        /// <param name="uri">The Url to disallow; a relative Url resolves against the builder's base Url.</param>
        /// <param name="comment">Not stored: rules have no comments. Kept for 1.x compatibility.</param>
        public RobotsBuilder DisallowWithComment(Uri uri, string comment)
        {
            AddRule(RobotsRuleKind.Disallow, uri);

            return this;
        }


        public RobotsBuilder Allow(string url)
        {
            return AllowWithComment(new Uri(_baseUri, url), string.Empty);
        }

        public RobotsBuilder AllowWithComment(string url, string comment)
        {
            return AllowWithComment(new Uri(_baseUri, url), comment);
        }

        public RobotsBuilder Allow(Uri uri)
        {
            return AllowWithComment(uri, string.Empty);
        }

        /// <param name="uri">The Url to allow; a relative Url resolves against the builder's base Url.</param>
        /// <param name="comment">Not stored: rules have no comments. Kept for 1.x compatibility.</param>
        public RobotsBuilder AllowWithComment(Uri uri, string comment)
        {
            AddRule(RobotsRuleKind.Allow, uri);

            return this;
        }

        private void AddRule(RobotsRuleKind kind, Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (_groups.Count == 0)
                throw new InvalidOperationException("Call ForUserAgent before adding Allow or Disallow rules.");

            var absolute = uri.IsAbsoluteUri ? uri : new Uri(_baseUri, uri);
            _groups[_groups.Count - 1].Value.Add(new RobotsRule(kind, absolute.PathAndQuery));
            Publish();
        }

        private void Publish()
        {
            var groups = new List<RobotsGroup>(_groups.Count);
            foreach (var group in _groups)
                groups.Add(new RobotsGroup(new[] { group.Key }, group.Value.ToArray(), null));

            _robots.Publish(new RobotsSnapshot(_baseUri, groups.AsReadOnly(), Array.Empty<string>(), Array.Empty<RobotsParseError>()));
        }
    }
}
