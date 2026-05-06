# US-018: History Page — Display Real Session Data

### US-018: History Page — Display Real Session Data

**Description:** As a user, I want the History page to show my actual past sessions so I can review what was said and translated.

**Acceptance Criteria:**
- [ ] On page load, fetch sessions from backend via `GET /api/sessions` (through IPC → main process → HTTP request)
- [ ] Display real session data instead of hardcoded mock data
- [ ] Each session card shows: title (auto-generated from the first usable final transcript snippet, with timestamp fallback), language pair, duration, timestamp
- [ ] Clicking "Replay" on a session opens a detail view showing the full transcript (original + translated side by side)
- [ ] Search input filters sessions by title text (client-side filtering)
- [ ] Empty state when no sessions exist: "No sessions yet. Start your first session from the Dashboard."
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---
