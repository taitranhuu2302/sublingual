# TODO: Local Storage (SQLite Migration)

Goal: migrate larger local data (starting with Speaking Practice) from JSON rewrite-on-save to SQLite for incremental writes, searchability, and better data integrity.

Scope note: no unit tests.

## Phase 1: Speaking Practice rooms/messages -> SQLite

- [x] Choose DB file name and path
- [x] Default: `~/.sublingual/sublingual.db`
- [x] Add a helper to resolve DB path under `AppPathHelper.GetDefaultAppRoot()`
  - `AppPathHelper.GetDefaultDatabasePath()` added to `src/Sublingual.App/Services/AppPathHelper.cs`

- [x] Add SQLite dependency
- [x] Use `Microsoft.Data.Sqlite`
- [x] Add package reference to `Sublingual.App.csproj`

- [x] Add schema + migrations bootstrap
- [x] Add `schema_migrations(version INTEGER PRIMARY KEY)`
- [x] Enable foreign keys on each connection: `PRAGMA foreign_keys = ON;`
- [x] Migration v1 DDL
- [x] `practice_rooms(id TEXT PRIMARY KEY, name TEXT NOT NULL, instructions TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL)`
- [x] `practice_messages(id TEXT PRIMARY KEY, room_id TEXT NOT NULL, sender TEXT NOT NULL, text TEXT NOT NULL, timestamp TEXT NOT NULL, is_spoken INTEGER NOT NULL, enhancement_advice TEXT NULL, FOREIGN KEY(room_id) REFERENCES practice_rooms(id) ON DELETE CASCADE)`
- [x] `practice_suggestions(message_id TEXT NOT NULL, label TEXT NOT NULL, text TEXT NOT NULL, FOREIGN KEY(message_id) REFERENCES practice_messages(id) ON DELETE CASCADE)`
- [x] Indexes
- [x] `practice_rooms(updated_at)`
- [x] `practice_messages(room_id, timestamp)`
- [x] `practice_suggestions(message_id)`
  - Implemented in `src/Sublingual.App/Services/LocalSqliteDatabase.cs`

- [x] Implement SQLite-backed room store
- [x] `SpeakingPracticeRoomStore` internals replaced with SQLite (same public API, no ViewModel changes)
- [x] Rooms sort: `updated_at desc, created_at desc`
- [x] Messages sort (load): `timestamp asc`
- [x] `BuildRoomName(...)` logic identical
- [x] Transactions used for create/update/delete/replace

- [x] One-time migration from JSON
- [x] If DB has 0 rooms and `speaking-practice-rooms.json` exists: read + import in one transaction
- [x] Rename JSON to `speaking-practice-rooms.json.bak` (keep backup)
- [x] Never deletes `.bak` automatically
  - Implemented in `SpeakingPracticeRoomStore.EnsureMigratedFromJsonIfNeeded()`

- [x] Wire store into app
- [x] `LocalSqliteDatabase` registered as singleton in `AppBootstrapper`
- [x] `SpeakingPracticeRoomStore` injected with `LocalSqliteDatabase` via DI
- [x] App works when DB does not exist (auto-create + migrate on first use)

- [x] Build passes (0 errors, 0 warnings)

- [ ] Manual verification (no tests)
- [ ] Create room -> restart app -> room persists
- [ ] Send typed message -> restart -> message persists
- [ ] Suggestions persist on restart
- [ ] Delete room(s) -> restart -> removed
- [ ] Upgrade path: start with existing JSON -> open app -> data appears -> `.bak` created

## Phase 2: Sessions/transcripts index -> SQLite (audio stays as files)

- [x] Keep audio `.wav` as files in sessions folder (source of truth unchanged)
- [x] Add SQLite tables for session metadata and transcript entries (migration v2)
  - `session_folders(id, name, slug, is_default, created_at, updated_at)`
  - `capture_sessions(session_id, folder_id, title, directory_path, audio_path, transcript_path, metadata_path, model_name, device_name, language, duration_seconds, created_at)`
  - `transcript_entries(segment_id, session_id, original_text, translated_text, is_final, updated_at)`
  - Implemented in `LocalSqliteDatabase.ApplyMigrationV2()`
- [x] Implement `SessionIndexStore` — dual-write SQLite index; all writes are best-effort (never blocks capture pipeline)
  - Upsert/delete for folders, sessions, transcript entries
  - `IndexExistingSessionsFromFilesystem(...)` helper for one-time seeding
  - Implemented in `src/Sublingual.App/Services/SessionIndexStore.cs`
- [x] Wire dual-writes into `CaptureSessionStorage`
  - `SaveSessionMetadata` → `_index.UpsertSession(...)`
  - `SaveTranscriptEntry` → `_index.UpsertTranscriptEntry(...)`
  - `DeleteTranscriptEntry` → `_index.DeleteTranscriptEntry(...)`
  - `DeleteSessions` → `_index.DeleteTranscriptEntriesForSession(...)` + `_index.DeleteSession(...)`
  - `CreateFolder` / `EnsureDefaultFolder` / `RenameFolder` → `_index.UpsertFolder(...)`
  - `DeleteFolder` → `_index.DeleteFolder(...)`
- [x] Register `SessionIndexStore` in `AppBootstrapper`
- [x] Build passes (0 errors, 0 warnings)

- [ ] Seed existing sessions on startup (call `IndexExistingSessionsFromFilesystem` once)
- [ ] Update `MainWindowViewModel.Sessions.cs` to query sessions from DB instead of filesystem scan
- [ ] Manual verification (no tests)

