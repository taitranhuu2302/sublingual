# US-001: Python Backend Scaffold with FastAPI + WebSocket

### US-001: Python Backend Scaffold with FastAPI + WebSocket

**Description:** As a developer, I need a Python backend service that runs a FastAPI server with WebSocket support so that the Electron app can send audio and receive transcription results.

**Acceptance Criteria:**
- [x] Create `backend/` directory at project root with `main.py`, `requirements.txt`
- [x] `requirements.txt` includes `fastapi`, `uvicorn[standard]`, `websockets`
- [x] `main.py` starts a FastAPI app on `localhost:8765`
- [x] WebSocket endpoint at `/ws/audio` accepts binary messages and echoes back a JSON acknowledgment `{"type": "ack"}`
- [x] REST endpoint `GET /health` returns `{"status": "ok"}`
- [x] Server can be started with `python main.py` or `uvicorn main:app`
- [x] Typecheck/lint passes

---
