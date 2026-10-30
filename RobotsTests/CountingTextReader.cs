using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RobotsTests
{
    /// <summary>
    /// Reads from a string and counts the synchronous and asynchronous line reads.
    /// </summary>
    internal sealed class CountingTextReader : TextReader
    {
        private readonly StringReader _inner;

        public CountingTextReader(string content)
        {
            _inner = new StringReader(content);
        }

        public int SyncReads { get; private set; }

        public int AsyncReads { get; private set; }

        public override string? ReadLine()
        {
            SyncReads++;
            return _inner.ReadLine();
        }

        public override Task<string?> ReadLineAsync()
        {
            AsyncReads++;
            return Task.FromResult<string?>(_inner.ReadLine());
        }

#if NET7_0_OR_GREATER
        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            AsyncReads++;
            return new ValueTask<string?>(_inner.ReadLine());
        }
#endif

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();

            base.Dispose(disposing);
        }
    }
}
