# US-017: SQLite Session Persistence in Backend

### US-017: SQLite Session Persistence in Backend

**Description:** As a developer, I need the backend to save completed session transcripts to SQLite so they can be displayed in the History page.

**Acceptance Criteria:**
- [ ] Add `aiosqlite` to `requirements.txt`
- [ ] Create `backend/database.py` with schema:
  ```sql
  CREATE TABLE sessions (
    id TEXT PRIMARY KEY,
    title TEXT,
    started_at TEXT NOT NULL,
    ended_at TEXT,
    stt_engine TEXT NOT NULL,
    language_pair TEXT NOT NULL DEFAULT 'en-vi'
  );

  CREATE TABLE transcripts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL REFERENCES sessions(id),
    original TEXT NOT NULL,
    translated TEXT,
    timestamp TEXT NOT NULL
  );
  ```
- [ ] Database file stored at `~/.lingostream/sessions.db`
- [ ] When a WebSocket session starts, insert a row into `sessions` with `started_at = now()`
- [ ] When a final subtitle is produced, insert a row into `transcripts`
- [ ] When WebSocket disconnects or receives `end_session`, update `sessions.ended_at`
- [ ] REST endpoint `GET /api/sessions` returns all sessions ordered by `started_at DESC`
- [ ] REST endpoint `GET /api/sessions/{id}/transcripts` returns all transcripts for a session
- [ ] Typecheck/lint passes

---
