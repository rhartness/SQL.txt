using System.Buffers;
using System.Runtime.CompilerServices;

namespace SqlTxt.Storage;

/// <summary>
/// Streams fixed-length binary records from a Stream using chunked reads and ArrayPool.
/// Caller must consume each yielded record before the next MoveNextAsync; the buffer is reused.
/// </summary>
internal static class BinaryRecordStreamHelper
{
    private const int DefaultBufferSize = 65536;

    /// <summary>
    /// Yields fixed-length records from the stream. Uses ArrayPool for buffer reuse.
    /// Lifetime: caller must consume each record before the next MoveNextAsync.
    /// </summary>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadRecordsAsync(
        Stream stream,
        int recordSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (recordSize <= 0)
            yield break;

        var bufferSize = Math.Max(DefaultBufferSize, recordSize * 2);
        bufferSize = (bufferSize / recordSize) * recordSize;

        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            var carryOver = 0;

            while (true)
            {
                var toRead = buffer.Length - carryOver;
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(carryOver, toRead), cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    if (carryOver > 0 && carryOver < recordSize)
                        throw new InvalidOperationException($"Truncated record at end of stream: expected {recordSize} bytes, got {carryOver}.");
                    yield break;
                }

                var total = carryOver + bytesRead;
                var offset = 0;

                while (offset + recordSize <= total)
                {
                    yield return new ReadOnlyMemory<byte>(buffer, offset, recordSize);
                    offset += recordSize;
                }

                carryOver = total - offset;
                if (carryOver > 0)
                    Buffer.BlockCopy(buffer, offset, buffer, 0, carryOver);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
