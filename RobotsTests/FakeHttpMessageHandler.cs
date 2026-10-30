using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RobotsTests
{
    /// <summary>
    /// Answers HttpClient requests in memory, so tests never reach the network.
    /// </summary>
    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public List<Uri> RequestedUris { get; } = new List<Uri>();

        /// <summary>
        /// The User-Agent header of each request, empty when none was sent.
        /// </summary>
        public List<string> UserAgents { get; } = new List<string>();

        public int AsyncSends { get; private set; }

        public int SyncSends { get; private set; }

        /// <summary>
        /// Returns <paramref name="content"/> for every request, as if served from <paramref name="finalUri"/>
        /// (the requested address when null).
        /// </summary>
        public static FakeHttpMessageHandler Serving(string content, Uri finalUri = null)
        {
            return new FakeHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/plain"),
                RequestMessage = new HttpRequestMessage(request.Method, finalUri ?? request.RequestUri)
            });
        }

        /// <summary>
        /// Returns an empty response with <paramref name="status"/> for every request.
        /// </summary>
        public static FakeHttpMessageHandler Returning(HttpStatusCode status)
        {
            return new FakeHttpMessageHandler(request => new HttpResponseMessage(status) { RequestMessage = request });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AsyncSends++;
            return Task.FromResult(Respond(request, cancellationToken));
        }

#if NET5_0_OR_GREATER
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SyncSends++;
            return Respond(request, cancellationToken);
        }
#endif

        private HttpResponseMessage Respond(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Real handlers stop on a cancelled token; HttpClient leaves that check to them.
            cancellationToken.ThrowIfCancellationRequested();
            RequestedUris.Add(request.RequestUri);
            UserAgents.Add(request.Headers.UserAgent.ToString());
            return _respond(request);
        }
    }
}
