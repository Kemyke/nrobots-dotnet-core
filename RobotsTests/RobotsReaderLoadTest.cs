using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RobotsTests
{
    /// <summary>
    /// Tests for loading robots.txt from a Stream or TextReader through Load and LoadAsync.
    /// </summary>
    [TestClass]
    public class RobotsReaderLoadTest
    {
        private const string BaseUrl = "http://www.test.com";

        private const string Content =
@"User-agent: *
Disallow: /private
Crawl-delay: 5
Sitemap: http://www.test.com/sitemap.xml";

        public TestContext TestContext { get; set; }

        private static MemoryStream ContentStream()
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(Content));
        }

        private static void AssertLoaded(Robots.IRobots robots)
        {
            Assert.AreEqual(new Uri(BaseUrl), robots.BaseUri);
            Assert.IsFalse(robots.Allowed("/private"));
            Assert.IsTrue(robots.Allowed("/public"));
            Assert.AreEqual(5, robots.GetCrawlDelay());
            Assert.AreEqual("http://www.test.com/sitemap.xml", robots.GetSitemapUrls()[0]);
        }

        [TestMethod]
        public void Load_stream_with_base_url()
        {
            var robots = new Robots.Robots();
            robots.Load(ContentStream(), BaseUrl);
            AssertLoaded(robots);
        }

        [TestMethod]
        public async Task LoadAsync_stream_with_base_url()
        {
            var robots = new Robots.Robots();
            await robots.LoadAsync(ContentStream(), BaseUrl, TestContext.CancellationToken);
            AssertLoaded(robots);
        }

        [TestMethod]
        public void Load_stream_with_base_uri()
        {
            var robots = new Robots.Robots();
            robots.Load(ContentStream(), new Uri(BaseUrl));
            AssertLoaded(robots);
        }

        [TestMethod]
        public async Task LoadAsync_stream_with_base_uri()
        {
            var robots = new Robots.Robots();
            await robots.LoadAsync(ContentStream(), new Uri(BaseUrl), TestContext.CancellationToken);
            AssertLoaded(robots);
        }

        [TestMethod]
        public void Load_reader()
        {
            var robots = new Robots.Robots();
            robots.Load(new StringReader(Content), new Uri(BaseUrl));
            AssertLoaded(robots);
        }

        [TestMethod]
        public async Task LoadAsync_reader()
        {
            var robots = new Robots.Robots();
            await robots.LoadAsync(new StringReader(Content), new Uri(BaseUrl), TestContext.CancellationToken);
            AssertLoaded(robots);
        }

        [TestMethod]
        public void Load_reader_reads_synchronously()
        {
            var reader = new CountingTextReader(Content);
            new Robots.Robots().Load(reader, new Uri(BaseUrl));

            Assert.AreEqual(5, reader.SyncReads);
            Assert.AreEqual(0, reader.AsyncReads);
        }

        [TestMethod]
        public async Task LoadAsync_reader_reads_asynchronously()
        {
            var reader = new CountingTextReader(Content);
            await new Robots.Robots().LoadAsync(reader, new Uri(BaseUrl), TestContext.CancellationToken);

            Assert.AreEqual(0, reader.SyncReads);
            Assert.AreEqual(5, reader.AsyncReads);
        }

        [TestMethod]
        public async Task LoadAsync_stream_rejects_an_invalid_base_url()
        {
            var robots = new Robots.Robots();
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => robots.LoadAsync(ContentStream(), "not a url", TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task LoadAsync_reader_rejects_a_null_reader()
        {
            var robots = new Robots.Robots();
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => robots.LoadAsync((TextReader)null, new Uri(BaseUrl), TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task LoadAsync_reader_honours_cancellation()
        {
            var reader = new CountingTextReader(Content);
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => new Robots.Robots().LoadAsync(reader, new Uri(BaseUrl), cancellation.Token));
            Assert.AreEqual(0, reader.AsyncReads);
        }
    }
}
