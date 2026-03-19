using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class FileSystemAccessorTests
{
    [Fact]
    public async Task ReadLinesAsync_StreamsLines()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SqlTxtFs_" + Guid.NewGuid().ToString("N")[..8]);
        var file = Path.Combine(dir, "lines.txt");
        try
        {
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(file, "a\nb\nc\n");

            var fs = new FileSystemAccessor();
            var collected = new List<string>();
            await foreach (var line in fs.ReadLinesAsync(file))
            {
                collected.Add(line);
            }
            Assert.Equal(["a", "b", "c"], collected);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MoveFile_OverwritesDestination()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SqlTxtFs_" + Guid.NewGuid().ToString("N")[..8]);
        var src = Path.Combine(dir, "src.txt");
        var dest = Path.Combine(dir, "dest.txt");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(src, "new content");
            File.WriteAllText(dest, "old content");

            var fs = new FileSystemAccessor();
            fs.MoveFile(src, dest);

            Assert.False(File.Exists(src));
            Assert.Equal("new content", File.ReadAllText(dest));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DeleteFile_RemovesFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SqlTxtFs_" + Guid.NewGuid().ToString("N")[..8]);
        var file = Path.Combine(dir, "f.txt");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(file, "x");

            var fs = new FileSystemAccessor();
            Assert.True(fs.FileExists(file));
            fs.DeleteFile(file);
            Assert.False(fs.FileExists(file));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task OpenReadStreamAsync_ReturnsReadableStream()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SqlTxtFs_" + Guid.NewGuid().ToString("N")[..8]);
        var file = Path.Combine(dir, "read.bin");
        try
        {
            Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(file, [1, 2, 3, 4, 5]);

            var fs = new FileSystemAccessor();
            var stream = await fs.OpenReadStreamAsync(file);
            await using (stream)
            {
                var buf = new byte[5];
                var n = await stream.ReadAsync(buf);
                Assert.Equal(5, n);
                Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buf);
            }
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task OpenWriteStreamAsync_CreatesFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SqlTxtFs_" + Guid.NewGuid().ToString("N")[..8]);
        var file = Path.Combine(dir, "write.bin");
        try
        {
            Directory.CreateDirectory(dir);

            var fs = new FileSystemAccessor();
            var stream = await fs.OpenWriteStreamAsync(file);
            await using (stream)
            {
                await stream.WriteAsync(new byte[] { 10, 20, 30 });
            }
            var content = await File.ReadAllBytesAsync(file);
            Assert.Equal(new byte[] { 10, 20, 30 }, content);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
