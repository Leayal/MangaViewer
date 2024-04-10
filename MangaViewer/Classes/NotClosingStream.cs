using System.Runtime.CompilerServices;
using System.IO;

namespace Leayal.MangaViewer.Classes
{
    sealed class NotClosingStream<T> : Stream where T : Stream
    {
        public readonly T BaseStream;

        public NotClosingStream(T baseStream)
        {
            this.BaseStream = baseStream;
        }

        public override bool CanRead { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BaseStream.CanRead; }
        public override bool CanSeek { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BaseStream.CanSeek; }
        public override bool CanWrite { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BaseStream.CanWrite; }
        public override long Length { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BaseStream.Length; }
        public override bool CanTimeout { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BaseStream.CanTimeout; }

        public override long Position { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BaseStream.Position; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => BaseStream.Position = value; }

        public override int ReadTimeout { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BaseStream.ReadTimeout; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => BaseStream.ReadTimeout = value; }
        public override int WriteTimeout { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BaseStream.WriteTimeout; [MethodImpl(MethodImplOptions.AggressiveInlining)] set => BaseStream.WriteTimeout = value; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void CopyTo(Stream destination, int bufferSize)
            => BaseStream.CopyTo(destination, bufferSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            => base.CopyToAsync(destination, bufferSize, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task FlushAsync(CancellationToken cancellationToken)
            => BaseStream.FlushAsync(cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush() => BaseStream.Flush();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Read(byte[] buffer, int offset, int count)
            => BaseStream.Read(buffer, offset, count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => BaseStream.BeginRead(buffer, offset, count, callback, state);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Read(Span<byte> buffer)
         => BaseStream.Read(buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => BaseStream.ReadAsync(buffer, offset, count, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => BaseStream.ReadAsync(buffer, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int ReadByte()
            => BaseStream.ReadByte();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int EndRead(IAsyncResult asyncResult)
            => BaseStream.EndRead(asyncResult);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override long Seek(long offset, SeekOrigin origin)
            => BaseStream.Seek(offset, origin);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SetLength(long value)
            => BaseStream.SetLength(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(byte[] buffer, int offset, int count)
            => BaseStream.Write(buffer, offset, count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => BaseStream.BeginWrite(buffer, offset, count, callback, state);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void EndWrite(IAsyncResult asyncResult)
            => BaseStream.EndWrite(asyncResult);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(ReadOnlySpan<byte> buffer)
            => BaseStream.Write(buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => BaseStream.WriteAsync(buffer, offset, count, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => BaseStream.WriteAsync(buffer, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteByte(byte value)
            => BaseStream.WriteByte(value);
    }
}
