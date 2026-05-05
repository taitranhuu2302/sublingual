# US-002: Electron Main Process Spawns Python Backend

### US-002: Electron Main Process Spawns Python Backend

**Description:** As a user, I want the Python backend to start automatically when I launch the app so I don't need to run separate processes.

**Acceptance Criteria:**
- [ ] Main process spawns Python backend as a child process on app `ready` event
- [ ] Backend process is killed on app `before-quit` event
- [ ] If backend fails to start (Python not found, port in use), show an error dialog to the user
- [ ] Backend stdout/stderr is logged to a file in the app's userData directory
- [ ] Health check: main process polls `GET /health` until backend is ready (timeout 15 seconds)
- [ ] Typecheck/lint passes

---
