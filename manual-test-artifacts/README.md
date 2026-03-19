# Manual test artifacts

This folder is the **default workspace** for [SqlTxt.ManualTests](../src/SqlTxt.ManualTests):

- Omitted `--db` → `run-<yyyyMMdd-HHmmss>/` under this directory (isolated per run).
- Omitted `--log` → `logs/ManualTests_<timestamp>.log` here.

With default **`--save-db` off**, run directories and known test DB trees under an explicit `--db` path are deleted after the run. Use **`--save-db`** to keep databases for inspection.

Contents are gitignored except this file.
