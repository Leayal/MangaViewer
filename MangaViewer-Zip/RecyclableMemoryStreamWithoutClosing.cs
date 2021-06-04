using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IO;
using System.IO;
using System.Threading;

namespace MangaViewer_Zip
{
    class MemoryStreamWithoutClosing : Stream
    {
        private readonly MemoryStream _baseStream;

        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => _baseStream.CanSeek;

        public override bool CanWrite => false;

        public override long Length => _baseStream.Length;

        public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }

        public MemoryStreamWithoutClosing(MemoryStream stream)
        {
            this._baseStream = stream;
        }

        public override void Close() { }

        internal void ParentArchiveClose() => _baseStream.Dispose();

        internal ValueTask ParentArchiveCloseAsync() => _baseStream.DisposeAsync();

        protected override void Dispose(bool disposing) { }

        public override ValueTask DisposeAsync() { return ValueTask.CompletedTask; }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            _baseStream.CopyTo(destination, bufferSize);
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return _baseStream.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _baseStream.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            return _baseStream.Read(buffer);
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _baseStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return _baseStream.EndRead(asyncResult);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _baseStream.ReadAsync(buffer, cancellationToken);
        }

        public override int ReadByte()
        {
            return _baseStream.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _baseStream.Seek(offset, origin);
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
