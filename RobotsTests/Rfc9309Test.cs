using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RobotsTests
{
    /// <summary>
    /// RFC 9309 (https://www.rfc-editor.org/rfc/rfc9309.html) conformance. Most cases are ported from the "ID_"
    /// tests of Google's reference parser, https://github.com/google/robotstxt/blob/master/robots_test.cc
    /// (Copyright 2019 Google LLC, Apache License 2.0), which test the RFC rather than Google's extensions.
    /// Where this library deliberately differs from that file, a comment says why.
    /// </summary>
    [TestClass]
    public class Rfc9309Test
    {
        private static bool IsUserAgentAllowed(string robotsTxt, string userAgent, string url)
        {
            var robots = new Robots.Robots();
            robots.LoadContent(robotsTxt, "http://foo.bar");
            return robots.Allowed(new Uri(url), userAgent);
        }

        // RFC 9309, section 2.1. Google's version also accepts a missing colon ("disallow /"), a Google-only
        // leniency this library doesn't implement.
        [TestMethod]
        public void ID_LineSyntax_Line()
        {
            const string correct = "user-agent: FooBot\ndisallow: /\n";
            const string incorrect = "foo: FooBot\nbar: /\n";
            const string url = "http://foo.bar/x/y";

            Assert.IsFalse(IsUserAgentAllowed(correct, "FooBot", url));
            Assert.IsTrue(IsUserAgentAllowed(incorrect, "FooBot", url));
        }

        // RFC 9309, section 2.1: groups for the same agent combine, and rules outside groups are ignored.
        [TestMethod]
        public void ID_LineSyntax_Groups()
        {
            const string robotsTxt =
                "allow: /foo/bar/\n" +
                "\n" +
                "user-agent: FooBot\n" +
                "disallow: /\n" +
                "allow: /x/\n" +
                "user-agent: BarBot\n" +
                "disallow: /\n" +
                "allow: /y/\n" +
                "\n" +
                "\n" +
                "allow: /w/\n" +
                "user-agent: BazBot\n" +
                "\n" +
                "user-agent: FooBot\n" +
                "allow: /z/\n" +
                "disallow: /\n";

            Assert.IsTrue(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/x/b"));
            Assert.IsTrue(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/z/d"));
            Assert.IsFalse(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/y/c"));
            Assert.IsTrue(IsUserAgentAllowed(robotsTxt, "BarBot", "http://foo.bar/y/c"));
            Assert.IsTrue(IsUserAgentAllowed(robotsTxt, "BarBot", "http://foo.bar/w/a"));
            Assert.IsFalse(IsUserAgentAllowed(robotsTxt, "BarBot", "http://foo.bar/z/d"));
            Assert.IsTrue(IsUserAgentAllowed(robotsTxt, "BazBot", "http://foo.bar/z/d"));

            // Lines with rules outside groups are ignored.
            Assert.IsFalse(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/foo/bar/"));
            Assert.IsFalse(IsUserAgentAllowed(robotsTxt, "BarBot", "http://foo.bar/foo/bar/"));
            Assert.IsFalse(IsUserAgentAllowed(robotsTxt, "BazBot", "http://foo.bar/foo/bar/"));
        }

        // RFC 9309, section 2.2.4: other records, such as Sitemap, and unknown lines don't end a group.
        [TestMethod]
        public void ID_LineSyntax_Groups_OtherRules()
        {
            const string withSitemap = "User-agent: BarBot\nSitemap: https://foo.bar/sitemap\nUser-agent: *\nDisallow: /\n";
            Assert.IsFalse(IsUserAgentAllowed(withSitemap, "FooBot", "http://foo.bar/"));
            Assert.IsFalse(IsUserAgentAllowed(withSitemap, "BarBot", "http://foo.bar/"));

            const string withUnknown = "User-agent: FooBot\nInvalid-Unknown-Line: unknown\nUser-agent: *\nDisallow: /\n";
            Assert.IsFalse(IsUserAgentAllowed(withUnknown, "FooBot", "http://foo.bar/"));
            Assert.IsFalse(IsUserAgentAllowed(withUnknown, "BarBot", "http://foo.bar/"));
        }

        // RFC 9309, section 2.1: record names are case-insensitive.
        [TestMethod]
        [DataRow("USER-AGENT: FooBot\nALLOW: /x/\nDISALLOW: /\n")]
        [DataRow("user-agent: FooBot\nallow: /x/\ndisallow: /\n")]
        [DataRow("uSeR-aGeNt: FooBot\nAlLoW: /x/\ndIsAlLoW: /\n")]
        public void ID_REPLineNamesCaseInsensitive(string robotsTxt)
        {
            Assert.IsTrue(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/x/y"));
            Assert.IsFalse(IsUserAgentAllowed(robotsTxt, "FooBot", "http://foo.bar/a/b"));
        }

        // RFC 9309, section 2.1: whitespace is allowed around the colon.
        [TestMethod]
        public void Whitespace_around_the_colon()
        {
            Assert.IsFalse(IsUserAgentAllowed("user-agent :  FooBot\ndisallow\t:\t/x\n", "FooBot", "http://foo.bar/x"));
        }

        // RFC 9309, section 2.2.1: the product token is letters, '_' and '-'. Google tests this on its
        // IsValidUserAgentToObey helper; this library applies it when matching, to both the group's value and
        // the agent passed to Allowed, so a full User-Agent string finds its group.
        [TestMethod]
        [DataRow("Foobot")]
        [DataRow("foobot")]
        [DataRow("Foobot/2.1")]
        [DataRow("Foobot/2.1 (+https://foo.bar/bot)")]
        [DataRow("Foobot Bar")]
        public void ID_ProductToken_finds_the_group(string userAgent)
        {
            const string robotsTxt = "User-agent: *\nDisallow: /\nUser-agent: Foobot\nAllow: /\n";
            Assert.IsTrue(IsUserAgentAllowed(robotsTxt, userAgent, "http://foo.bar/x"));
        }

        [TestMethod]
        [DataRow("Foo-Bot")]
        [DataRow("Foo_Bot")]
        public void ID_ProductToken_allows_hyphen_and_underscore(string userAgent)
        {
            string robotsTxt = "User-agent: " + userAgent + "\nDisallow: /\n";
            Assert.IsFalse(IsUserAgentAllowed(robotsTxt, userAgent, "http://foo.bar/x"));
            Assert.IsTrue(IsUserAgentAllowed(robotsTxt, "Foo", "http://foo.bar/x"));
        }

        // RFC 9309, section 2.2.1: user-agent values are case-insensitive; the value counts up to the first
        // character that can't be in a product token.
        [TestMethod]
        [DataRow("User-Agent: FOO BAR\nAllow: /x/\nDisallow: /\n")]
        [DataRow("User-Agent: foo bar\nAllow: /x/\nDisallow: /\n")]
        [DataRow("User-Agent: FoO bAr\nAllow: /x/\nDisallow: /\n")]
        public void ID_UserAgentValueCaseInsensitive(string robotsTxt)
        {
            Assert.IsTrue(IsUserAgentAllowed(robotsTxt, "Foo", "http://foo.bar/x/y"));
            Assert.IsFalse(IsUserAgentAllowed(robotsTxt, "Foo", "http://foo.bar/a/b"));
            Assert.IsTrue(IsUserAgentAllowed(robotsTxt, "foo", "http://foo.bar/x/y"));
            Assert.IsFalse(IsUserAgentAllowed(robotsTxt, "foo", "http://foo.bar/a/b"));
        }

        // RFC 9309, section 2.2.1: without a matching group the "*" group applies; without either, no rules do.
        [TestMethod]
        public void ID_GlobalGroups_Secondary()
        {
            const string global = "user-agent: *\nallow: /\nuser-agent: FooBot\ndisallow: /\n";
            const string onlySpecific = "user-agent: FooBot\nallow: /\nuser-agent: BarBot\ndisallow: /\nuser-agent: BazBot\ndisallow: /\n";
            const string url = "http://foo.bar/x/y";

            Assert.IsTrue(IsUserAgentAllowed(string.Empty, "FooBot", url));
            Assert.IsFalse(IsUserAgentAllowed(global, "FooBot", url));
            Assert.IsTrue(IsUserAgentAllowed(global, "BarBot", url));
            Assert.IsTrue(IsUserAgentAllowed(onlySpecific, "QuxBot", url));
        }

        // RFC 9309, section 2.2.2: matching rules against URIs is case-sensitive.
        [TestMethod]
        public void ID_AllowDisallow_Value_CaseSensitive()
        {
            Assert.IsFalse(IsUserAgentAllowed("user-agent: FooBot\ndisallow: /x/\n", "FooBot", "http://foo.bar/x/y"));
            Assert.IsTrue(IsUserAgentAllowed("user-agent: FooBot\ndisallow: /X/\n", "FooBot", "http://foo.bar/x/y"));
        }

        // RFC 9309, section 2.2.2: the most specific (longest) match wins; Allow wins a tie.
        [TestMethod]
        public void ID_LongestMatch()
        {
            const string url = "http://foo.bar/x/page.html";

            Assert.IsFalse(IsUserAgentAllowed("user-agent: FooBot\ndisallow: /x/page.html\nallow: /x/\n", "FooBot", url));

            const string allowLonger = "user-agent: FooBot\nallow: /x/page.html\ndisallow: /x/\n";
            Assert.IsTrue(IsUserAgentAllowed(allowLonger, "FooBot", url));
            Assert.IsFalse(IsUserAgentAllowed(allowLonger, "FooBot", "http://foo.bar/x/"));

            Assert.IsTrue(IsUserAgentAllowed("user-agent: FooBot\ndisallow: \nallow: \n", "FooBot", url));
            Assert.IsTrue(IsUserAgentAllowed("user-agent: FooBot\ndisallow: /\nallow: /\n", "FooBot", url));

            const string folder = "user-agent: FooBot\ndisallow: /x\nallow: /x/\n";
            Assert.IsFalse(IsUserAgentAllowed(folder, "FooBot", "http://foo.bar/x"));
            Assert.IsTrue(IsUserAgentAllowed(folder, "FooBot", "http://foo.bar/x/"));

            Assert.IsTrue(IsUserAgentAllowed("user-agent: FooBot\ndisallow: /x/page.html\nallow: /x/page.html\n", "FooBot", url));

            const string wildcard = "user-agent: FooBot\nallow: /page\ndisallow: /*.html\n";
            Assert.IsFalse(IsUserAgentAllowed(wildcard, "FooBot", "http://foo.bar/page.html"));
            Assert.IsTrue(IsUserAgentAllowed(wildcard, "FooBot", "http://foo.bar/page"));

            const string longerAllow = "user-agent: FooBot\nallow: /x/page.\ndisallow: /*.html\n";
            Assert.IsTrue(IsUserAgentAllowed(longerAllow, "FooBot", url));
            Assert.IsFalse(IsUserAgentAllowed(longerAllow, "FooBot", "http://foo.bar/x/y.html"));

            // The specific group applies on its own: the "*" group's rules don't add to it.
            const string specificGroup = "User-agent: *\nDisallow: /x/\nUser-agent: FooBot\nDisallow: /y/\n";
            Assert.IsTrue(IsUserAgentAllowed(specificGroup, "FooBot", "http://foo.bar/x/page"));
            Assert.IsFalse(IsUserAgentAllowed(specificGroup, "FooBot", "http://foo.bar/y/page"));
        }

        // RFC 9309, section 2.2.2: octets outside ASCII are percent-encoded before comparison, and
        // percent-encoded unreserved characters are decoded (Figure 4).
        [TestMethod]
        public void ID_Encoding()
        {
            Assert.IsTrue(IsUserAgentAllowed(
                "User-agent: FooBot\nDisallow: /\nAllow: /foo/bar?qux=taz&baz=http://foo.bar?tar&par\n",
                "FooBot", "http://foo.bar/foo/bar?qux=taz&baz=http://foo.bar?tar&par"));

            // A 3-byte character in a rule is percent-encoded. Google's test expects the unencoded Url
            // ".../ツ" not to match, because its API requires callers to encode Urls; System.Uri encodes it,
            // which is what the RFC asks for, so here it matches.
            const string character = "User-agent: FooBot\nDisallow: /\nAllow: /foo/bar/ツ\n";
            Assert.IsTrue(IsUserAgentAllowed(character, "FooBot", "http://foo.bar/foo/bar/%E3%83%84"));
            Assert.IsTrue(IsUserAgentAllowed(character, "FooBot", "http://foo.bar/foo/bar/ツ"));

            const string encoded = "User-agent: FooBot\nDisallow: /\nAllow: /foo/bar/%E3%83%84\n";
            Assert.IsTrue(IsUserAgentAllowed(encoded, "FooBot", "http://foo.bar/foo/bar/%E3%83%84"));
            Assert.IsTrue(IsUserAgentAllowed(encoded, "FooBot", "http://foo.bar/foo/bar/ツ"));

            // Percent-encoded unreserved ASCII is decoded before comparison: RFC 9309 Figure 4 matches
            // "/foo/bar/%62%61%7A" with "/foo/bar/baz". Google's test expects no match here.
            const string unreserved = "User-agent: FooBot\nDisallow: /\nAllow: /foo/bar/%62%61%7A\n";
            Assert.IsTrue(IsUserAgentAllowed(unreserved, "FooBot", "http://foo.bar/foo/bar/baz"));
            Assert.IsTrue(IsUserAgentAllowed(unreserved, "FooBot", "http://foo.bar/foo/bar/%62%61%7A"));

            // Hex digits compare case-insensitively ("%aa" is "%AA").
            Assert.IsFalse(IsUserAgentAllowed("User-agent: *\nDisallow: /%aa\n", "FooBot", "http://foo.bar/%AA"));
        }

        // RFC 9309, section 2.2.3: '#' comments, '$' anchors the end, '*' matches any characters.
        [TestMethod]
        public void ID_SpecialCharacters()
        {
            const string star = "User-agent: FooBot\nDisallow: /foo/bar/quz\nAllow: /foo/*/qux\n";
            Assert.IsFalse(IsUserAgentAllowed(star, "FooBot", "http://foo.bar/foo/bar/quz"));
            Assert.IsTrue(IsUserAgentAllowed(star, "FooBot", "http://foo.bar/foo/quz"));
            Assert.IsTrue(IsUserAgentAllowed(star, "FooBot", "http://foo.bar/foo//quz"));
            Assert.IsTrue(IsUserAgentAllowed(star, "FooBot", "http://foo.bar/foo/bax/quz"));

            const string dollar = "User-agent: FooBot\nDisallow: /foo/bar$\nAllow: /foo/bar/qux\n";
            Assert.IsFalse(IsUserAgentAllowed(dollar, "FooBot", "http://foo.bar/foo/bar"));
            Assert.IsTrue(IsUserAgentAllowed(dollar, "FooBot", "http://foo.bar/foo/bar/qux"));
            Assert.IsTrue(IsUserAgentAllowed(dollar, "FooBot", "http://foo.bar/foo/bar/"));
            Assert.IsTrue(IsUserAgentAllowed(dollar, "FooBot", "http://foo.bar/foo/bar/baz"));

            const string comment = "User-agent: FooBot\n# Disallow: /\nDisallow: /foo/quz#qux\nAllow: /\n";
            Assert.IsTrue(IsUserAgentAllowed(comment, "FooBot", "http://foo.bar/foo/bar"));
            Assert.IsFalse(IsUserAgentAllowed(comment, "FooBot", "http://foo.bar/foo/quz"));
        }

        // RFC 9309, section 2.2.3, Figure 6: a literal '*' or '$' in a Url is matched by its percent-encoding.
        [TestMethod]
        public void Literal_special_characters_match_their_encoding()
        {
            Assert.IsFalse(IsUserAgentAllowed("User-agent: *\nDisallow: /path/file-with-a-%2A.html\n", "FooBot", "https://www.example.com/path/file-with-a-*.html"));
            Assert.IsFalse(IsUserAgentAllowed("User-agent: *\nDisallow: /path/foo-%24\n", "FooBot", "https://www.example.com/path/foo-$"));
        }

        // RFC 9309, section 2.2.2: "The /robots.txt URI is implicitly allowed."
        [TestMethod]
        public void Robots_txt_is_implicitly_allowed()
        {
            Assert.IsTrue(IsUserAgentAllowed("User-agent: *\nDisallow: /\n", "FooBot", "http://foo.bar/robots.txt"));
        }

        // The query is matched, the fragment isn't (Google's TestGetPathParamsQuery).
        [TestMethod]
        public void Query_is_matched_and_fragment_is_not()
        {
            Assert.IsFalse(IsUserAgentAllowed("User-agent: *\nDisallow: /a/b?c=d&e=f$\n", "FooBot", "http://www.example.com/a/b?c=d&e=f#fragment"));
            Assert.IsFalse(IsUserAgentAllowed("User-agent: *\nDisallow: /a;b$\n", "FooBot", "http://example.com/a;b#c"));
            Assert.IsTrue(IsUserAgentAllowed("User-agent: *\nDisallow: /a/b$\n", "FooBot", "http://example.com/a/b?c"));
        }

        // RFC 9309, section 2.2: lines end with CR, LF or CR LF, and the last line needs no line break.
        [TestMethod]
        [DataRow("User-Agent: foo\nAllow: /some/path\nUser-Agent: bar\n\n\nDisallow: /\n")]
        [DataRow("User-Agent: foo\r\nAllow: /some/path\r\nUser-Agent: bar\r\n\r\n\r\nDisallow: /\r\n")]
        [DataRow("User-Agent: foo\rAllow: /some/path\rUser-Agent: bar\r\r\rDisallow: /\r")]
        [DataRow("User-Agent: foo\nAllow: /some/path\nUser-Agent: bar\n\n\nDisallow: /")]
        [DataRow("User-Agent: foo\nAllow: /some/path\r\nUser-Agent: bar\n\r\n\nDisallow: /")]
        public void ID_LineEndings(string robotsTxt)
        {
            Assert.IsFalse(IsUserAgentAllowed(robotsTxt, "bar", "http://foo.bar/x"));
            Assert.IsTrue(IsUserAgentAllowed(robotsTxt, "foo", "http://foo.bar/x"));
        }

        // A UTF-8 byte order mark at the start of the file is skipped.
        [TestMethod]
        public void ID_UTF8ByteOrderMarkIsSkipped()
        {
            Assert.IsFalse(IsUserAgentAllowed("\uFEFFUser-Agent: foo\nDisallow: /\n", "foo", "http://foo.bar/x"));

            var robots = new Robots.Robots();
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("User-Agent: foo\nDisallow: /\n")).ToArray();
            using (var stream = new MemoryStream(bytes))
            {
                robots.Load(stream, "http://foo.bar");
            }
            Assert.IsFalse(robots.Allowed("/x", "foo"));
        }

        // A Sitemap line may appear anywhere (RFC 9309, section 2.2.4).
        [TestMethod]
        public void ID_NonStandardLineExample_Sitemap()
        {
            const string sitemap = "http://foo.bar/sitemap.xml";
            const string groups = "User-Agent: foo\nAllow: /some/path\nUser-Agent: bar\n\n\n";

            var atEnd = new Robots.Robots();
            atEnd.LoadContent(groups + "Sitemap: " + sitemap + "\n", "http://foo.bar");
            Assert.AreEqual(sitemap, atEnd.GetSitemapUrls()[0]);

            var atStart = new Robots.Robots();
            atStart.LoadContent("Sitemap: " + sitemap + "\n" + groups, "http://foo.bar");
            Assert.AreEqual(sitemap, atStart.GetSitemapUrls()[0]);
        }
    }
}
