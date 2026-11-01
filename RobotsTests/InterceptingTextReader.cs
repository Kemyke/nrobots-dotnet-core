using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RobotsTests
{
    /// <summary>
    /// Reads from a string and calls back before each line, so a test can act in the middle of a load.
    /// </summary>
    internal sealed class InterceptingTextReader : TextReader
    {
        private readonly StringReader _inner;
        private readonly Action<int> _beforeLine;
        private int _linesRead;

        public InterceptingTextReader(string content, Action<int> beforeLine)
        {
            _inner = new StringReader(content);
            _beforeLine = beforeLine;
        }

        public override string? ReadLine()
        {
            _beforeLine(_linesRead++);
            return _inner.ReadLine();
        }

        public override Task<string?> ReadLineAsync()
        {
            return Task.FromResult(ReadLine());
        }

#if NET7_0_OR_GREATER
        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            return new ValueTask<string?>(ReadLine());
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
