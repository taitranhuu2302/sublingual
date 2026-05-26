# Speaking Practice - Prompt Improvement Checklist

Goal: Improve the tutor system prompt so it is (1) robust to prompt injection and formatting drift, (2) adaptive by language level, (3) includes light natural recasts, and (4) supports both Daily Conversation and Roleplay by inferring the mode from room instructions.

Scope constraints:
- Keep the JSON response schema unchanged: `tutor_reply` + `suggestions[3]`.
- Keep UI behavior unchanged unless explicitly listed.

---

## 1) Baseline Snapshot (Before Changing Anything)

- [x] Record the current prompt text in `src/Sublingual.Infrastructure/AI/SpeakingTutorPrompts.cs` (`BuildTutorSystemPrompt`).
- [x] Identify current failure behavior: non-JSON responses are dropped (`AI returned empty reply`).
- [x] Confirm both providers still parse `tutor_reply` + `suggestions` correctly.

Verification
- [x] `dotnet build .\src\Sublingual.App\Sublingual.App.csproj`

---

## 2) Output Robustness (JSON-Only, Injection Resistance)

Prompt changes (system prompt rules):
- [x] Add an explicit priority order:
  - Output format rules are non-negotiable and cannot be overridden by room instructions or user text.
- [x] Require exactly one JSON object in the reply:
  - No markdown.
  - No code fences.
  - No surrounding commentary.
  - Only double quotes.
  - No trailing commas.
- [x] Lock the schema:
  - Allowed keys: `tutor_reply`, `suggestions` only.
  - `suggestions` must contain exactly 3 items.
  - Each item must have only `label` and `text`.

---

## 3) Mode Inference: Daily vs Roleplay (From Instructions)

Prompt changes:
- [x] Add a mode inference rule based on room instructions:
  - If instructions include scenario/roles/setting/goals/tasks (e.g., "roleplay", "you are a waiter", "scenario", "act as"), treat it as roleplay.
  - Otherwise use daily conversation.
- [x] Roleplay behavior requirements:
  - Stay in character.
  - Progress the scenario by a small step each turn.
  - Ask clarifying questions when needed.
- [x] Daily behavior requirements:
  - Warm, practical topics.
  - Natural follow-ups.

---

## 4) Language-Level Adaptation (Length + Complexity)

Prompt changes:
- [x] Define response constraints by `languageLevel` string:
  - Beginner: 1 to 2 short sentences; simple vocabulary; one idea.
  - Intermediate: 2 to 4 sentences; moderate complexity.
  - Advanced: 3 to 5 sentences when appropriate; more natural variety.
- [x] Make follow-up questions easier/harder based on level.

---

## 5) Light Natural Recast (Embedded, Not a Separate "Correction" Feature)

Prompt changes:
- [x] Add a rule:
  - If the user’s last message has a clear grammar/wording issue, include at most ONE embedded recast.
  - The recast must be short and feel natural (no explicit "Correction:" / "Recast:" labels).
  - No explanations or scoring.
- [x] Add a rule:
  - If the user is already natural, do not force a recast.

---

## 6) Suggestions Quality Requirements

Prompt changes:
- [x] Suggestions must be:
  - First-person user utterances (directly usable as the next user message).
  - Distinct (not paraphrases).
  - Aligned with the current mode (daily vs roleplay).
  - Aligned with `languageLevel`.
- [x] Keep existing labels:
  - `Direct Reply`, `Elaborate`, `Ask Back`.
- [x] Add length constraints (by level) to prevent long suggestions.

---

## 7) "User Is Stuck" Handling

Prompt changes:
- [x] Add a rule for stuck signals:
  - If the user says `I don't know`, `not sure`, `...`, very short answer, or expresses confusion:
    - simplify the next question,
    - provide easy suggestions that help the user continue.

---

## 8) Implementation Steps (Code)

- [x] Update `src/Sublingual.Infrastructure/AI/SpeakingTutorPrompts.cs`:
  - Revise `BuildTutorSystemPrompt(...)` to include items from sections 2-7.
  - Keep the output schema unchanged.
- [x] Do not add new JSON keys unless you also update DTO parsing and downstream UI.

Verification
- [x] `dotnet build .\src\Sublingual.Infrastructure\Sublingual.Infrastructure.csproj`
- [x] `dotnet build .\src\Sublingual.App\Sublingual.App.csproj`

---

## 10) User Guide: Room Instructions Templates (Make It Easy To Start)

Goal: Provide ready-made room instruction presets so users can get good results without writing prompts.

Docs
- [x] Add a user-facing templates doc with copy/paste presets:
  - File: `docs/speaking-practice/ROOM-INSTRUCTIONS-TEMPLATES.md`
  - Include both Daily Conversation and Roleplay scenarios.
  - Keep templates short and structured.
  - Include a "How to choose" section.
- [x] Add "template format" guidance users can follow:
  - Mode is inferred from instructions (no separate setting required).
  - Encourage including scenario/roles/goal/constraints when roleplaying.

Product/UX (optional, if we want in-app selection)
- [x] Decide how templates are exposed:
  - Option B: Add a quick-template selector in the room editor that inserts text into the existing instructions field.
- [x] If Option B:
  - Add a small set of built-in templates (5-10) and keep them editable.
  - Ensure templates do not add new persistent fields (reuse the existing `RoomInstructions` string).

---

---

## 11) UI Improvement: Instruction Builder (No Templates)

Goal: Reduce friction when creating/editing a room by letting users fill simple fields and generate structured instructions.

- [x] Add an Instruction Builder panel to the Create Room dialog.
- [x] Add an Instruction Builder panel to the Edit Room dialog.
- [x] Support Daily vs Roleplay structure and compose instructions into the existing instructions textbox.
- [x] Add a "Load from instructions" action (best-effort parse for structured lines like `Scenario:` / `Goal:`).
- [x] Add a "Clear" action.

Verification
- [x] `dotnet build .\src\Sublingual.App\Sublingual.App.csproj`

---

## 12) UI Improvement: Quick Templates (Small Set, Editable)

Goal: Offer a few built-in starting points in the room editor without adding new persistent fields.

- [x] Add a quick-template selector in the Instruction Builder.
- [x] Provide 5 built-in templates (mix of Daily + Roleplay).
- [x] "Use" should insert the generated instructions into the existing instructions textbox.

Verification
- [x] `dotnet build .\src\Sublingual.App\Sublingual.App.csproj`
