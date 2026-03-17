using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class MemoryFileSystemAccessorTests
{
    private readonly MemoryFileSystemAccessor _fs = new();

    [Fact]
    public void CreateDirectory_And_DirectoryExists()
    {
        _fs.CreateDirectory("a/b/c");
        Assert.True(_fs.DirectoryExists("a"));
        Assert.True(_fs.DirectoryExists("a/b"));
        Assert.True(_fs.DirectoryExists("a/b/c"));
        Assert.False(_fs.DirectoryExists("a/b/c/d"));
    }

    [Fact]
    public async Task WriteAndReadFile()
    {
        _fs.CreateDirectory("data");
        await _fs.WriteAllTextAsync("data/file.txt", "hello");
        Assert.True(_fs.FileExists("data/file.txt"));
        var content = await _fs.ReadAllTextAsync("data/file.txt");
        Assert.Equal("hello", content);
    }

    [Fact]
    public async Task AppendAllText_Accumulates()
    {
        _fs.CreateDirectory("data");
        await _fs.WriteAllTextAsync("data/log.txt", "line1\n");
        await _fs.AppendAllTextAsync("data/log.txt", "line2\n");
        var content = await _fs.ReadAllTextAsync("data/log.txt");
        Assert.Equal("line1\nline2\n", content);
    }

    [Fact]
    public async Task ReadAllLines_ReturnsLines()
    {
        _fs.CreateDirectory("data");
        await _fs.WriteAllTextAsync("data/lines.txt", "a\nb\nc");
        var lines = await _fs.ReadAllLinesAsync("data/lines.txt");
        Assert.Equal(3, lines.Count);
        Assert.Equal("a", lines[0]);
        Assert.Equal("b", lines[1]);
        Assert.Equal("c", lines[2]);
    }

    [Fact]
    public async Task ReadLinesAsync_StreamsLines()
    {
        _fs.CreateDirectory("data");
        await _fs.WriteAllTextAsync("data/stream.txt", "x\ny\nz");
        var collected = new List<string>();
        await foreach (var line in _fs.ReadLinesAsync("data/stream.txt"))
        {
            collected.Add(line);
        }
        Assert.Equal(["x", "y", "z"], collected);
    }

    [Fact]
    public async Task MoveFile_OverwritesDestination()
    {
        _fs.CreateDirectory("src");
        _fs.CreateDirectory("dest");
        await _fs.WriteAllTextAsync("src/a.txt", "original");
        await _fs.WriteAllTextAsync("dest/a.txt", "old");
        _fs.MoveFile("src/a.txt", "dest/a.txt");
        Assert.False(_fs.FileExists("src/a.txt"));
        Assert.Equal("original", await _fs.ReadAllTextAsync("dest/a.txt"));
    }

    [Fact]
    public async Task DeleteFile_RemovesFile()
    {
        _fs.CreateDirectory("x");
        await _fs.WriteAllTextAsync("x/f.txt", "content");
        Assert.True(_fs.FileExists("x/f.txt"));
        _fs.DeleteFile("x/f.txt");
        Assert.False(_fs.FileExists("x/f.txt"));
    }

    [Fact]
    public void DeleteFile_Nonexistent_DoesNotThrow()
    {
        _fs.DeleteFile("nonexistent/path.txt");
    }

    [Fact]
    public void GetFullPath_Normalizes()
    {
        var p = _fs.GetFullPath("a/b/c");
        Assert.True(p.Contains("a") && p.Contains("b") && p.Contains("c"));
    }

    [Fact]
    public void Combine_JoinsPaths()
    {
        var p = _fs.Combine("db", "Tables", "User");
        Assert.Equal("db/Tables/User", p);
    }

    [Fact]
    public void GetDirectories_ReturnsImmediateChildren()
    {
        _fs.CreateDirectory("db/Tables/User");
        _fs.CreateDirectory("db/Tables/Page");
        _fs.CreateDirectory("db/db");
        var dirs = _fs.GetDirectories("db");
        Assert.Contains("db/Tables", dirs);
        Assert.Contains("db/db", dirs);
    }

    [Fact]
    public async Task GetFiles_ReturnsImmediateFiles()
    {
        _fs.CreateDirectory("db/db");
        await _fs.WriteAllTextAsync("db/db/manifest.json", "{}");
        _fs.CreateDirectory("db/Tables/User");
        await _fs.WriteAllTextAsync("db/Tables/User/User.txt", "data");
        var files = _fs.GetFiles("db/db");
        Assert.Single(files);
        Assert.Contains("manifest.json", files[0]);
    }

    [Fact]
    public async Task GetFileLength_ReturnsContentLength()
    {
        _fs.CreateDirectory("x");
        await _fs.WriteAllTextAsync("x/f.txt", "hello");
        var len = _fs.GetFileLength("x/f.txt");
        Assert.Equal(5, len);
    }

    [Fact]
    public async Task DeleteDirectory_RemovesRecursively()
    {
        _fs.CreateDirectory("toDelete/a/b");
        await _fs.WriteAllTextAsync("toDelete/a/b/f.txt", "x");
        _fs.DeleteDirectory("toDelete");
        Assert.False(_fs.DirectoryExists("toDelete"));
        Assert.False(_fs.FileExists("toDelete/a/b/f.txt"));
    }

    [Fact]
    public void NormalizesBackslashes()
    {
        _fs.CreateDirectory("a\\b\\c");
        Assert.True(_fs.DirectoryExists("a/b/c"));
    }
}
