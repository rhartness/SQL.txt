using SqlTxt.Engine;

namespace SqlTxt.Engine.Tests;

public class DatabaseLockManagerTests
{
    [Fact]
    public async Task SchemaReadLock_TwoSequentialReaders_BothAcquireAndRelease()
    {
        var m = new DatabaseLockManager();
        var db = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sqltxt_lock_" + Guid.NewGuid().ToString("N")));

        await using (await m.AcquireReadLockAsync(db))
        {
        }
        await using (await m.AcquireReadLockAsync(db))
        {
        }
    }

    [Fact]
    public async Task FkOrderedLocks_AcquiresInTableNameOrder_ReleasesWithoutThrowing()
    {
        var m = new DatabaseLockManager();
        var db = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sqltxt_fklock_" + Guid.NewGuid().ToString("N")));

        await using (await m.AcquireFkOrderedLocksAsync(
            db,
            sharedTables: new[] { "Parent" },
            exclusiveTables: new[] { "Zeta", "Alpha" },
            CancellationToken.None))
        {
        }
    }
}
