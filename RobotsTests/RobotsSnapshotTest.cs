using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Robots;

// CA2234 (pass Uri instead of string) is disabled for this file on purpose: these tests call the string
// overloads, such as LoadContent(string, string) and Allowed(string), because they are the public API the README
// documents and callers use, so they need test coverage.
#pragma warning disable CA2234

namespace RobotsTests
{
    /// <summary>
    /// Tests for Snapshot, Sitemaps, the Encoding overloads and Robots.Parse.
    /// </summary>
    [TestClass]
    [SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
        Justification = "MSTest discovers and creates test classes through reflection. Internal test classes need [DiscoverInternals] and then trip CA1812 (never instantiated) instead, because the analyzer can't see MSTest create them.")]
    public class RobotsSnapshotTest
    {
        private const string FirstUrl = "https://first.example.com";
        private const string FirstContent = "User-agent: *\nDisallow: /first\nHost: first\nSitemap: https://first.example.com/s.xml";
        private const string SecondUrl = "https://second.example.com";
        private const string SecondContent = "User-agent: *\nDisallow: /second";

        public TestContext TestContext { get; set; }

        private static void AssertFirst(RobotsSnapshot snapshot)
        {
            Assert.AreEqual(new Uri(FirstUrl), snapshot.BaseUri);
            Assert.IsFalse(snapshot.Allowed("/first"));
            Assert.IsTrue(snapshot.Allowed("/second"));
            Assert.HasCount(1, snapshot.Groups);
            Assert.AreEqual("https://first.example.com/s.xml", string.Join(" | ", snapshot.Sitemaps));
            Assert.HasCount(1, snapshot.ParseErrors);
        }

        [TestMethod]
        public void A_snapshot_keeps_its_load_after_a_reload()
        {
            var robots = Robots.Robots.Parse(FirstContent, FirstUrl);
            var snapshot = robots.Snapshot;

            robots.LoadContent(SecondContent, SecondUrl);

            AssertFirst(snapshot);
            Assert.AreEqual(new Uri(SecondUrl), robots.Snapshot.BaseUri);
            Assert.IsFalse(robots.Allowed("/second"));
            Assert.IsEmpty(robots.ParseErrors);
            Assert.IsEmpty(robots.Sitemaps);
        }

        [TestMethod]
        public async Task During_a_load_the_snapshot_is_the_previous_complete_load()
        {
            var robots = Robots.Robots.Parse(FirstContent, FirstUrl);
            int checkedLines = 0;

            using (var reader = new InterceptingTextReader(SecondContent, line =>
            {
                AssertFirst(robots.Snapshot);
                checkedLines++;
            }))
            {
                await robots.LoadAsync(reader, new Uri(SecondUrl), TestContext.CancellationToken).ConfigureAwait(false);
            }

            Assert.AreEqual(3, checkedLines);
            Assert.IsFalse(robots.Allowed("/second"));
        }

        [TestMethod]
        public void The_members_of_Robots_answer_from_the_current_snapshot()
        {
            var robots = Robots.Robots.Parse(FirstContent, FirstUrl);
            var snapshot = robots.Snapshot;

            Assert.AreSame(snapshot.Groups, robots.Groups);
            Assert.AreSame(snapshot.Sitemaps, robots.Sitemaps);
            Assert.AreSame(snapshot.ParseErrors, robots.ParseErrors);
            Assert.AreEqual(snapshot.BaseUri, robots.BaseUri);
        }

        [TestMethod]
        public void An_add_publishes_a_snapshot_with_both_and_keeps_the_BaseUri()
        {
            var robots = Robots.Robots.Parse(FirstContent, FirstUrl);
            var before = robots.Snapshot;

            robots.AddContent("User-agent: *\nDisallow: /added\nSitemap: https://first.example.com/t.xml");

            AssertFirst(before);
            Assert.AreEqual(new Uri(FirstUrl), robots.BaseUri);
            Assert.HasCount(2, robots.Groups);
            Assert.HasCount(2, robots.Sitemaps);
            Assert.IsFalse(robots.Allowed("/first"));
            Assert.IsFalse(robots.Allowed("/added"));
        }

        [TestMethod]
        public void Sitemaps_is_read_only_and_GetSitemapUrls_is_a_copy()
        {
            var robots = Robots.Robots.Parse(FirstContent, FirstUrl);

            Assert.IsTrue(((IList<string>)robots.Sitemaps).IsReadOnly);
            var copy = robots.GetSitemapUrls();
            copy.Clear();
            Assert.HasCount(1, robots.Sitemaps);
        }

        [TestMethod]
        public void Parse_with_a_string_or_Uri_base()
        {
            Assert.AreEqual(new Uri(FirstUrl), Robots.Robots.Parse(FirstContent, new Uri(FirstUrl)).BaseUri);
            Assert.AreEqual(new Uri(FirstUrl), Robots.Robots.Parse(FirstContent, FirstUrl).BaseUri);
            Assert.ThrowsExactly<ArgumentException>(() => Robots.Robots.Parse(FirstContent, "not a url"));
            Assert.ThrowsExactly<ArgumentNullException>(() => Robots.Robots.Parse(null!, FirstUrl));
        }

        // "é" in ISO-8859-1 is the byte 0xE9, which isn't valid UTF-8. It sits mid-line on purpose: at the very end of
        // a stream, .NET Framework's UTF-8 decoder drops an incomplete sequence where .NET 10 gives U+FFFD.
        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
        private const string LatinContent = "User-agent: *\nDisallow: /café/menu";

        private static MemoryStream LatinStream(bool utf8ByteOrderMark = false)
        {
            var bytes = new List<byte>();
            if (utf8ByteOrderMark)
            {
                bytes.AddRange(new byte[] { 0xEF, 0xBB, 0xBF });
                bytes.AddRange(Encoding.UTF8.GetBytes(LatinContent));
            }
            else
            {
                bytes.AddRange(Latin1.GetBytes(LatinContent));
            }

            return new MemoryStream(bytes.ToArray());
        }

        [TestMethod]
        public void Load_reads_the_stream_in_the_given_encoding()
        {
            var robots = new Robots.Robots();
            using (var stream = LatinStream())
            {
                robots.Load(stream, FirstUrl, Latin1);
            }

            Assert.AreEqual("/café/menu", robots.Groups[0].Rules[0].Path);
            Assert.IsFalse(robots.Allowed("/café/menu"));
        }

        [TestMethod]
        public void Without_an_encoding_the_stream_is_read_as_UTF_8()
        {
            var robots = new Robots.Robots();
            using (var stream = LatinStream())
            {
                robots.Load(stream, new Uri(FirstUrl));
            }

            Assert.AreNotEqual("/café/menu", robots.Groups[0].Rules[0].Path);
            Assert.IsTrue(robots.Allowed("/café/menu"));
        }

        [TestMethod]
        public void A_byte_order_mark_takes_precedence_over_the_given_encoding()
        {
            var robots = new Robots.Robots();
            using (var stream = LatinStream(utf8ByteOrderMark: true))
            {
                robots.Load(stream, new Uri(FirstUrl), Latin1);
            }

            Assert.AreEqual("/café/menu", robots.Groups[0].Rules[0].Path);
        }

        [TestMethod]
        public async Task LoadAsync_and_AddAsync_read_the_stream_in_the_given_encoding()
        {
            var robots = new Robots.Robots();
            using (var stream = LatinStream())
            {
                await robots.LoadAsync(stream, FirstUrl, Latin1, TestContext.CancellationToken).ConfigureAwait(false);
            }
            using (var stream = LatinStream())
            {
                await robots.AddAsync(stream, Latin1, TestContext.CancellationToken).ConfigureAwait(false);
            }
            Assert.HasCount(2, robots.Groups);
            Assert.AreEqual("/café/menu", robots.Groups[1].Rules[0].Path);

            using (var stream = LatinStream())
            {
                await robots.LoadAsync(stream, new Uri(SecondUrl), Latin1, TestContext.CancellationToken).ConfigureAwait(false);
            }

            Assert.AreEqual("/café/menu", robots.Groups[0].Rules[0].Path);
            Assert.AreEqual(new Uri(SecondUrl), robots.BaseUri);
        }

        [TestMethod]
        public void Add_reads_the_stream_in_the_given_encoding()
        {
            var robots = Robots.Robots.Parse(FirstContent, FirstUrl);
            using (var stream = LatinStream())
            {
                robots.Add(stream, Latin1);
            }

            Assert.AreEqual("/café/menu", robots.Groups[1].Rules[0].Path);
            Assert.IsFalse(robots.Allowed("/café/menu"));
        }
    }
}
