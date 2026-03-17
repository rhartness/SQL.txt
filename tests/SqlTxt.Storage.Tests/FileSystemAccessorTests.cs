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
}
