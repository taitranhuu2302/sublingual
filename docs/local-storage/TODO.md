# TODO: Local Storage (SQLite Migration)

Goal: migrate larger local data from JSON rewrite-on-save to SQLite for incremental writes, searchability, and better data integrity.

Scope note: no unit tests.

## Phase 1: Sessions/transcripts index -> SQLite (audio stays as files)

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
