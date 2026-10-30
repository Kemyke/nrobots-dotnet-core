using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Robots
{
    /// <summary>
    /// Reads at most a given number of bytes from another stream, then reports the end of the stream.
    /// </summary>
    internal sealed class LengthLimitedStream : Stream
    {
        private readonly Stream _inner;
        private readonly byte[] _probe = new byte[1];
        private long _remaining;
        private bool _probed;

        public LengthLimitedStream(Stream inner, long maxBytes)
        {
            _inner = inner;
            _remaining = maxBytes;
        }

        /// <summary>
        /// True when the inner stream had more bytes than the limit allowed. Known once a read has returned 0.
        /// </summary>
        public bool LimitReached { get; private set; }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining == 0)
            {
                // One more byte tells a cut-off stream apart from one that is exactly the limit long.
                if (!_probed)
                {
                    _probed = true;
                    LimitReached = _inner.Read(_probe, 0, 1) > 0;
                }
                return 0;
            }

            int read = _inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
            _remaining -= read;
            return read;
        }

#if NETCOREAPP
        public override int Read(Span<byte> buffer)
        {
            if (_remaining == 0)
            {
                if (!_probed)
                {
                    _probed = true;
                    LimitReached = _inner.Read(_probe, 0, 1) > 0;
                }
                return 0;
            }

            int read = _inner.Read(buffer.Slice(0, (int)Math.Min(buffer.Length, _remaining)));
            _remaining -= read;
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining == 0)
            {
                if (!_probed)
                {
                    _probed = true;
                    LimitReached = await _inner.ReadAsync(_probe.AsMemory(), cancellationToken).ConfigureAwait(false) > 0;
                }
                return 0;
            }

            int read = await _inner.ReadAsync(buffer.Slice(0, (int)Math.Min(buffer.Length, _remaining)), cancellationToken).ConfigureAwait(false);
            _remaining -= read;
            return read;
        }
#else
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_remaining == 0)
            {
                if (!_probed)
                {
                    _probed = true;
                    LimitReached = await _inner.ReadAsync(_probe, 0, 1, cancellationToken).ConfigureAwait(false) > 0;
                }
                return 0;
            }

            int read = await _inner.ReadAsync(buffer, offset, (int)Math.Min(count, _remaining), cancellationToken).ConfigureAwait(false);
            _remaining -= read;
            return read;
        }
#endif

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
