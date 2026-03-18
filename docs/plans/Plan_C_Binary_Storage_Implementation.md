# Plan C: Binary Storage Implementation

**Status:** Done  
**Parent:** Enterprise SQL:2023 Meta-Plan  
**Prerequisites:** Plan B complete

**Reference:** [docs/architecture/02-storage-format.md](../architecture/02-storage-format.md), [docs/architecture/10-performance-and-efficiency.md](../architecture/10-performance-and-efficiency.md)

---

## Scope

Implement full binary backend. When `storageBackend=binary`, use `.bin` for data, `.idx` for indexes, binary serialization for rows. Both backends support same SQL features.

---

## Wave 1: IFileSystemAccessor Extensions

### 1.1 Add Byte APIs

- In [src/SqlTxt.Contracts/IFileSystemAccessor.cs](../../src/SqlTxt.Contracts/IFileSystemAccessor.cs):
  - `Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default)`
  - `Task WriteAllBytesAsync(string path, byte[] content, CancellationToken ct = default)`
  - `Task AppendAllBytesAsync(string path, byte[] content, CancellationToken ct = default)`
  - `IAsyncEnumerable<byte[]> ReadRecordsAsync(string path, int recordSize, CancellationToken ct = default)` — or equivalent for fixed-length binary records

### 1.2 Implement in FileSystemAccessor

- [src/SqlTxt.Storage/FileSystemAccessor.cs](../../src/SqlTxt.Storage/FileSystemAccessor.cs): Implement new methods using FileStream/File.ReadAllBytesAsync/WriteAllBytesAsync

**Acceptance:** IFileSystemAccessor supports byte I/O; FileSystemAccessor implements it.

---

## Wave 2: Row Serialization Abstraction

### 2.1 Generalize or Add Binary Interfaces

**Option A:** Generalize IRowSerializer/IRowDeserializer to support both:
- Add overloads: `byte[] SerializeToBytes(...)`, `RowData DeserializeFromBytes(ReadOnlySpan<byte> data, ...)`

**Option B:** Add IBinaryRowSerializer, IBinaryRowDeserializer:
- `byte[] Serialize(RowData row, TableDefinition table, bool isActive, ...)`
- `RowData Deserialize(ReadOnlySpan<byte> data, TableDefinition table, out bool isActive)`

### 2.2 Implement BinaryRowSerializer

- Fixed-width binary format: 1-byte flag (A/D), 8-byte _RowId, then fixed-width column values (binary encoding)
- CHAR: fixed bytes (pad with spaces); INT/BIGINT: little-endian; DECIMAL: encoded per schema

### 2.3 Implement BinaryRowDeserializer

- Parse binary record into RowData
- Handle schema column order and widths

**Acceptance:** BinaryRowSerializer and BinaryRowDeserializer produce/consume correct RowData.

---

## Wave 3: Binary Store Implementations

### 3.1 BinarySchemaStore

- Write schema as binary (e.g., MessagePack, or custom fixed format)
- File extension: .bin
- Or: keep schema as JSON/binary JSON for simplicity

### 3.2 BinaryIndexStore

- Sorted binary format: fixed-length records (key + ShardId + RowId)
- Use binary search for LookupByValueAsync
- File extension: .idx

### 3.3 BinaryStocStore

- Fixed-length records: ShardId, MinRowId, MaxRowId, FilePath (fixed width), RowCount
- File extension: .bin

### 3.4 BinaryMetadataStore, BinaryRowIdSequenceStore

- Binary format for table.meta and rowid sequence
- File extensions: .bin

**Acceptance:** All stores have binary implementations.

---

## Wave 4: TableDataStore Backend Integration

### 4.1 TableDataStore Uses IStorageBackend

- Inject IStorageBackend (or resolve from database path)
- GetDataFilePath: use backend's DataFileExtension (.txt vs .bin)
- When binary: use ReadRecordsAsync/AppendAllBytesAsync; BinaryRowSerializer/Deserializer
- When text: existing behavior (ReadLinesAsync, FixedWidthRowSerializer)

### 4.2 Shard Split, StreamTransformRowsAsync

- Both paths must work for binary backend
- Use backend-specific serialization and I/O

**Acceptance:** TableDataStore supports both backends; CRUD works for binary databases.

---

## Wave 5: Engine and DatabaseCreator

### 5.1 CreateDatabase with storageBackend

- DatabaseCreator already writes storageBackend (Plan B)
- Ensure engine resolves backend when opening; passes to TableDataStore, SchemaStore, IndexStore, StocStore

### 5.2 Composition

- DatabaseEngine: when opening DB, read manifest storageBackend; create appropriate store instances (text vs binary)
- TableDataStore, SchemaStore, etc. receive backend and use correct implementation

**Acceptance:** Opening binary database uses binary stores; opening text database uses text stores.

---

## Wave 6: Tests and Documentation

### 6.1 Unit Tests

- BinaryRowSerializer/Deserializer round-trip
- Binary stores read/write
- TableDataStore with binary backend: append, read, update, delete

### 6.2 Integration Tests

- Create database with --storage:binary; create table; insert; select; update; delete
- Verify .bin and .idx files created

### 6.3 Documentation

- [docs/architecture/02-storage-format.md](../architecture/02-storage-format.md): Binary format specification
- [docs/cli-reference.md](../cli-reference.md): --storage:binary usage
- README: Storage Abstraction row = Done
- Migration notes: text and binary databases are incompatible; no in-place conversion

**Acceptance:** Tests pass; docs updated.

---

## Plan Execution Rules (Apply on Completion)

1. Update README Roadmap table
2. Update 02-storage-format.md
3. Update CLI reference, Getting Started
4. Provide examples for CLI, WASM, Embedding

---

## Deliverable

Full binary backend. `sqltxt create-db MyDb --storage:binary` creates a database that uses .bin and .idx files with binary serialization. All CRUD operations work for both text and binary backends.
