using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RobotsTests
{
    /// <summary>
    /// Tests for loading robots.txt over HTTP through Load(Uri) and LoadAsync.
    /// </summary>
    [TestClass]
    public class RobotsLoadTest
    {
        private const string Content = "User-agent: *\nDisallow: /private";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Load_requests_robots_txt_at_the_root()
        {
            var handler = FakeHttpMessageHandler.Serving(Content);
            using (var client = new HttpClient(handler))
            {
                var robots = new Robots.Robots(client);

                robots.Load(new Uri("http://www.test.com/some/page.html"));

                Assert.AreEqual(new Uri("http://www.test.com/robots.txt"), handler.RequestedUris[0]);
                Assert.IsFalse(robots.Allowed("/private"));
                Assert.IsTrue(robots.Allowed("/public"));
            }
        }

        [TestMethod]
        public async Task LoadAsync_requests_robots_txt_at_the_root()
        {
            var handler = FakeHttpMessageHandler.Serving(Content);
            using (var client = new HttpClient(handler))
            {
                var robots = new Robots.Robots(client);

                await robots.LoadAsync(new Uri("http://www.test.com/some/page.html"), TestContext.CancellationToken);

                Assert.AreEqual(new Uri("http://www.test.com/robots.txt"), handler.RequestedUris[0]);
                Assert.IsFalse(robots.Allowed("/private"));
                Assert.IsTrue(robots.Allowed("/public"));
            }
        }

        [TestMethod]
        public void Load_uses_the_redirected_host_as_base()
        {
            var handler = FakeHttpMessageHandler.Serving(Content, new Uri("https://other.test.com/robots.txt"));
            using (var client = new HttpClient(handler))
            {
                var robots = new Robots.Robots(client);

                robots.Load(new Uri("http://www.test.com/"));

                Assert.AreEqual(new Uri("https://other.test.com"), robots.BaseUri);
            }
        }

        [TestMethod]
        public async Task LoadAsync_uses_the_redirected_host_as_base()
        {
            var handler = FakeHttpMessageHandler.Serving(Content, new Uri("https://other.test.com/robots.txt"));
            using (var client = new HttpClient(handler))
            {
                var robots = new Robots.Robots(client);

                await robots.LoadAsync(new Uri("http://www.test.com/"), TestContext.CancellationToken);

                Assert.AreEqual(new Uri("https://other.test.com"), robots.BaseUri);
            }
        }

        [TestMethod]
        public void Load_throws_on_a_non_success_status()
        {
            var handler = new FakeHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
            using (var client = new HttpClient(handler))
            {
                var robots = new Robots.Robots(client);

                Assert.ThrowsExactly<HttpRequestException>(() => robots.Load(new Uri("http://www.test.com/")));
            }
        }

        [TestMethod]
        public async Task LoadAsync_throws_on_a_non_success_status()
        {
            var handler = new FakeHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
            using (var client = new HttpClient(handler))
            {
                var robots = new Robots.Robots(client);

                await Assert.ThrowsExactlyAsync<HttpRequestException>(() => robots.LoadAsync(new Uri("http://www.test.com/"), TestContext.CancellationToken));
            }
        }

        [TestMethod]
        public async Task LoadAsync_honours_cancellation()
        {
            var handler = FakeHttpMessageHandler.Serving(Content);
            using (var client = new HttpClient(handler))
            using (var cancellation = new CancellationTokenSource())
            {
                var robots = new Robots.Robots(client);
                cancellation.Cancel();

                await Assert.ThrowsAsync<OperationCanceledException>(() => robots.LoadAsync(new Uri("http://www.test.com/"), cancellation.Token));
                Assert.IsEmpty(handler.RequestedUris);
            }
        }

        [TestMethod]
        public void Constructor_rejects_a_null_client()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new Robots.Robots(null));
        }

#if NET5_0_OR_GREATER
        [TestMethod]
        public void Load_sends_synchronously()
        {
            var handler = FakeHttpMessageHandler.Serving(Content);
            using (var client = new HttpClient(handler))
            {
                var robots = new Robots.Robots(client);

                robots.Load(new Uri("http://www.test.com/"));

                Assert.AreEqual(1, handler.SyncSends);
                Assert.AreEqual(0, handler.AsyncSends);
            }
        }
#endif
    }
}
