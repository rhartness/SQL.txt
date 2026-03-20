# Manual test failure / deficit report

Copy this template into an issue or Cursor chat. Replace angle-bracket placeholders. Attach files from **one run** (same timestamp).

## Command line

```text
dotnet run --project src/SqlTxt.ManualTests -- <test> [...]
```

## Environment

- **OS:**
- **.NET:** `dotnet --version`
- **Storage:** text | binary | all | localdb (via `--storage` / `--compare:`)

## Artifacts (same `ManualTests_<timestamp>` prefix)

- [ ] `manual-test-artifacts/logs/ManualTests_<ts>.log` — includes **RunId:** line when the driver starts
- [ ] `manual-test-artifacts/logs/ManualTests_<ts>.errors-and-comparison.md` — `#failures` / `#vs-localdb` / `#slower-than-localdb` / `#deficits`
- [ ] `manual-test-artifacts/logs/ManualTests_<ts>.diagnostics.jsonl` — when run with **`--diagnostics`**
- [ ] `manual-test-artifacts/logs/ManualTests_<ts>.failure-bundle.json` — when a test **fails** (optional recent events if diagnostics were on)
- [ ] Database folder (if `--save-db` or custom `--db`)

**RunId (correlation):** paste from the log header or a failing `TestResult` line so all artifacts from one run can be matched.

## Expected vs actual

**Expected:**

**Actual:**

## First bad stage / step (from log or jsonl)
