using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class BinaryRecordStreamHelperTests
{
    [Fact]
    public async Task ReadRecordsAsync_YieldsCorrectNumberOfRecords()
    {
        const int recordSize = 10;
        var data = new byte[recordSize * 5];
        for (var i = 0; i < 5; i++)
            data[i * recordSize] = (byte)(i + 1);

        await using var stream = new MemoryStream(data);
        var count = 0;
        await foreach (var record in BinaryRecordStreamHelper.ReadRecordsAsync(stream, recordSize))
        {
            Assert.Equal(recordSize, record.Length);
            count++;
        }
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task ReadRecordsAsync_EmptyStream_YieldsNothing()
    {
        await using var stream = new MemoryStream(Array.Empty<byte>());
        var count = 0;
        await foreach (var _ in BinaryRecordStreamHelper.ReadRecordsAsync(stream, 10))
            count++;
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ReadRecordsAsync_RecordSizeZero_YieldsNothing()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var count = 0;
        await foreach (var _ in BinaryRecordStreamHelper.ReadRecordsAsync(stream, 0))
            count++;
        Assert.Equal(0, count);
    }
}
