# US-010: LibreTranslate Integration in Backend

### US-010: LibreTranslate Integration in Backend

**Description:** As a developer, I need the backend to translate final English text to Vietnamese using LibreTranslate.

**Acceptance Criteria:**
- [ ] Add `requests` (or `httpx`) to `requirements.txt`
- [ ] Create `backend/engines/translator.py`
- [ ] Calls LibreTranslate API at configurable URL (default `http://localhost:5000/translate`)
- [ ] Sends `{"q": text, "source": "en", "target": "vi", "format": "text"}`
- [ ] Returns translated text string
- [ ] If LibreTranslate is unavailable, return original text with a warning flag `{"translated": text, "translation_failed": true}`
- [ ] Translation timeout: 5 seconds per request
- [ ] Document how to run LibreTranslate locally via Docker: `docker run -d -p 5000:5000 libretranslate/libretranslate`
- [ ] Typecheck/lint passes

---
