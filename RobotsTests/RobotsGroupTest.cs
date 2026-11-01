using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Robots;
using Robots.Fluent;

// CA2234 (pass Uri instead of string) is disabled for this file on purpose: these tests call the string
// overloads, such as LoadContent(string, string) and Allowed(string, string), because they are the public API
// the README documents and callers use, so they need test coverage.
#pragma warning disable CA2234

namespace RobotsTests
{
    /// <summary>
    /// Tests for Groups, GetGroup, RobotsGroup and RobotsRule.
    /// </summary>
    [TestClass]
    [SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
        Justification = "MSTest discovers and creates test classes through reflection. Internal test classes need [DiscoverInternals] and then trip CA1812 (never instantiated) instead, because the analyzer can't see MSTest create them.")]
    public class RobotsGroupTest
    {
        private const string SiteUrl = "https://www.example.com";

        private const string Content =
            "User-agent: *\n" +
            "Disallow: /private/\n" +
            "Allow: /private/public/\n" +
            "Crawl-delay: 1.5\n" +
            "\n" +
            "User-agent: ExampleBot\n" +
            "User-agent: OtherBot\n" +
            "\n" +
            "Disallow: /example/\n" +
            "\n" +
            "User-agent: examplebot/1.0\n" +
            "Allow: /example/open\n" +
            "Crawl-delay: 9\n";

        private static Robots.Robots Load(string robotsTxt)
        {
            var robots = new Robots.Robots();
            robots.LoadContent(robotsTxt, SiteUrl);
            return robots;
        }

        private static string[] Describe(RobotsGroup group)
        {
            return group.Rules.Select(rule => rule.ToString()).ToArray();
        }

        [TestMethod]
        public void Groups_are_listed_as_written()
        {
            var groups = Load(Content).Groups;

            Assert.HasCount(3, groups);
            Assert.AreEqual("*", string.Join(" | ", groups[0].UserAgents));
            Assert.AreEqual("Disallow: /private/ | Allow: /private/public/", string.Join(" | ", Describe(groups[0])));
            Assert.AreEqual(TimeSpan.FromSeconds(1.5), groups[0].CrawlDelay);

            // A blank line doesn't end a group (RFC 9309): both agents share the Disallow after it.
            Assert.AreEqual("ExampleBot | OtherBot", string.Join(" | ", groups[1].UserAgents));
            Assert.AreEqual("Disallow: /example/", string.Join(" | ", Describe(groups[1])));
            Assert.IsNull(groups[1].CrawlDelay);

            Assert.AreEqual("examplebot/1.0", string.Join(" | ", groups[2].UserAgents));
            Assert.AreEqual(TimeSpan.FromSeconds(9), groups[2].CrawlDelay);
        }

        [TestMethod]
        public void An_empty_Disallow_ends_the_group_body()
        {
            // RFC 9309 and Google count an empty Disallow as a rule line, so b starts a new group. 1.x and earlier
            // 2.0 builds let b join a's group, which disallowed /x for a too.
            var robots = Load("User-agent: a\nDisallow:\nUser-agent: b\nDisallow: /x");

            Assert.HasCount(2, robots.Groups);
            Assert.IsTrue(robots.Allowed("/x", "a"));
            Assert.IsFalse(robots.Allowed("/x", "b"));
        }

        [TestMethod]
        public void GetGroup_combines_every_group_for_the_product_token()
        {
            var group = Load(Content).GetGroup("ExampleBot/2.1 (+https://example.com/bot)");

            Assert.IsNotNull(group);
            Assert.AreEqual("ExampleBot | OtherBot | examplebot/1.0", string.Join(" | ", group.UserAgents));
            Assert.AreEqual("Disallow: /example/ | Allow: /example/open", string.Join(" | ", Describe(group)));
            Assert.AreEqual(TimeSpan.FromSeconds(9), group.CrawlDelay);
        }

        [TestMethod]
        public void GetGroup_returns_the_parsed_instance_when_one_group_applies()
        {
            var robots = Load(Content);

            Assert.AreSame(robots.Groups[0], robots.GetGroup("UnknownBot"));
        }

        [TestMethod]
        public void GetGroup_returns_null_when_no_group_applies()
        {
            var robots = Load("User-agent: ExampleBot\nDisallow: /");

            Assert.IsNull(robots.GetGroup("OtherBot"));
            Assert.IsNull(new Robots.Robots().GetGroup("OtherBot"));
        }

        [TestMethod]
        public void GetGroup_agrees_with_Allowed()
        {
            var robots = Load(Content);
            string[] agents = { "*", "ExampleBot", "examplebot/1.0", "OtherBot", "UnknownBot" };
            string[] paths = { "/", "/private/", "/private/public/x", "/example/", "/example/open", "/robots.txt" };

            foreach (var agent in agents)
            {
                var group = robots.GetGroup(agent);
                Assert.IsNotNull(group);
                foreach (var path in paths)
                    Assert.AreEqual(robots.Allowed(path, agent), group.IsAllowed(path), agent + " " + path);
            }
        }

        [TestMethod]
        public void RobotsGroup_Matches_by_product_token_and_star()
        {
            var groups = Load(Content).Groups;

            Assert.IsTrue(groups[0].Matches("AnyBot"));
            Assert.IsTrue(groups[1].Matches("otherbot/3.0"));
            Assert.IsFalse(groups[1].Matches("AnyBot"));
        }

        [TestMethod]
        public void RobotsGroup_IsAllowed_always_allows_robots_txt()
        {
            var group = Load("User-agent: *\nDisallow: /").Groups[0];

            Assert.IsFalse(group.IsAllowed("/page"));
            Assert.IsTrue(group.IsAllowed("/robots.txt"));
        }

        [TestMethod]
        public void RobotsRule_matches_as_RFC_9309_defines()
        {
            var rule = new RobotsRule(RobotsRuleKind.Disallow, "/*.pdf$");

            Assert.IsTrue(rule.Matches("/files/a.pdf"));
            Assert.IsFalse(rule.Matches("/files/a.pdf?download"));
            Assert.IsTrue(new RobotsRule(RobotsRuleKind.Allow, "/%7Euser").Matches("/~user/page"));
            Assert.AreEqual("Disallow: /*.pdf$", rule.ToString());
            Assert.AreEqual(RobotsRuleKind.Disallow, rule.Kind);
            Assert.AreEqual("/*.pdf$", rule.Path);
        }

        [TestMethod]
        public void Arguments_are_checked()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new RobotsRule(RobotsRuleKind.Allow, null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => new Robots.Robots().GetGroup(null!));
            var group = Load(Content).Groups[0];
            Assert.ThrowsExactly<ArgumentNullException>(() => group.Matches(null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => group.IsAllowed(null!));
        }

        [TestMethod]
        public void Groups_and_their_lists_are_read_only()
        {
            var robots = Load(Content);

            Assert.IsTrue(((IList<RobotsGroup>)robots.Groups).IsReadOnly);
            Assert.IsTrue(((IList<RobotsRule>)robots.Groups[0].Rules).IsReadOnly);
            Assert.IsTrue(((IList<string>)robots.Groups[0].UserAgents).IsReadOnly);
        }

        [TestMethod]
        public void RobotsBuilder_rules_show_up_as_groups()
        {
            var robots = RobotsBuilder.Create(new Uri(SiteUrl))
                .ForUserAgent("bot1").Disallow("/web").Allow("/web/page1.aspx")
                .ForUserAgent("*").Disallow(new Uri("/blocked", UriKind.Relative))
                .Robots;

            Assert.HasCount(2, robots.Groups);
            Assert.AreEqual("Disallow: /web | Allow: /web/page1.aspx", string.Join(" | ", Describe(robots.Groups[0])));
            Assert.AreEqual("Disallow: /blocked", string.Join(" | ", Describe(robots.Groups[1])));
            Assert.IsFalse(robots.Allowed("/blocked", "other"));
        }

        [TestMethod]
        public void ToString_lists_the_user_agents()
        {
            Assert.AreEqual("User-agent: ExampleBot, OtherBot", Load(Content).Groups[1].ToString());
        }
    }
}
