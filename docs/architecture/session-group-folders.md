# Session Group Folders Implementation Spec

## Goal

Refactor session management from nested tree-path folders into flat group folders.

The new model separates two concepts clearly:

- `SessionFolder`: a user-managed group that contains many capture records
- `CaptureRecord`: a saved capture entry with audio, transcript, and metadata

The UI must not expose filesystem paths as a primary concept.

## Product Rules

### Folder model

- Folders are single-level only. No nested folders.
- A default folder `Global` always exists.
- `Global` cannot be deleted.
- `Global` should not be renamed.
- Folder names are unique, case-insensitive.
- Folder names must be filesystem-safe after sanitization.
- The UI should treat folder names as labels, not as raw paths.

### Capture model

- Every capture record belongs to exactly one folder.
- New captures are saved into the folder currently selected in the capture screen.
- If no folder is selected, the app falls back to `Global`.
- Users can move a capture record from one folder to another using a picker UI.
- Users can delete capture records from the folder detail/list view.

### Folder deletion rule

- If a non-default folder is empty, it can be deleted directly.
- If a non-default folder contains capture records, deleting it should move all captures to `Global` and then delete the folder.
- The UI must state this clearly in the confirmation dialog.

## Scope

### In scope

- Replace nested folder logic with flat folder groups
- Add folder CRUD in the Sessions page
- Add record move flow between folders
- Update capture flow to choose a folder from a simple browser/list
- Persist folder membership in capture metadata
- Move physical capture directories when a record changes folder

### Out of scope

- Nested folder trees
- Manual path entry by users
- Arbitrary filesystem browsing to assign record destination folders
- Multi-root session storage systems

## Data Model Changes

### Current problem

Current code uses `TreePath` as both UI concept and storage concept. That couples the UX to filesystem path semantics.

### Target model

#### SessionFolder

- `Id`
- `Name`
- `IsDefault`
- `CreatedAt`
- `UpdatedAt`

#### CaptureRecord

- `Id`
- `FolderId`
- `FolderNameSnapshot` optional if needed for migration/debugging
- `Title`
- `AudioPath`
- `TranscriptPath`
- `MetadataPath`
- `CreatedAt`
- `DurationSeconds`
- `DeviceName`
- `Language`
- `ModelName`

### Recommended implementation shortcut for MVP

For the current file-based architecture, a full relational folder store is not required yet.

Use a lightweight file-based representation:

- `sessions/folders.json` stores folder entities
- each capture record still lives as a directory on disk
- each capture metadata file stores `FolderId`

This avoids using folder name as the only stable key.

## Storage Layout

### Disk structure

Use a flat folder structure under the sessions root:

```text
sessions/
  folders.json
  global/
    session-20260521-.../
      audio.wav
      session.json
      transcript.json
  meeting-a/
    session-20260521-.../
      audio.wav
      session.json
      transcript.json
```

### Folder storage semantics

- Each `SessionFolder` has a display name and a storage slug.
- `Global` uses fixed slug `global`.
- User-created folders use sanitized unique slugs derived from the name.
- Folder rename updates:
  - `folders.json`
  - on-disk folder path
  - all `session.json` records under that folder if they store folder name snapshots or slugs

### Capture move semantics

Moving a capture record between folders must:

1. create target folder directory if missing
2. move the capture record directory to the target folder
3. update record metadata so history reload remains correct
4. refresh any in-memory list/filter state

## File-Level Implementation Plan

### 1. Models

#### Add

- `src/Sublingual.App/Models/SessionFolderRecord.cs`
- `src/Sublingual.App/Models/SessionFolderCollection.cs` if needed

#### Update

- `src/Sublingual.App/Models/CaptureSessionMetadata.cs`

Replace or supplement `TreePath` with:

- `FolderId`
- `FolderName`
- `FolderSlug`

Recommended minimum:

- keep legacy `TreePath` only during migration
- new code reads/writes `FolderId` and `FolderSlug`

### 2. Storage Service

#### Refactor

- `src/Sublingual.App/Services/CaptureSessionStorage.cs`

#### Responsibilities after refactor

- load folders from `folders.json`
- ensure default `Global` folder exists
- create folder
- rename folder
- delete folder with move-to-Global behavior
- list capture records by folder
- move capture records between folders
- delete single capture record
- delete multiple capture records
- create output path for a specific folder id or slug

#### New methods

- `GetFolders()`
- `EnsureDefaultFolder()`
- `CreateFolder(string name)`
- `RenameFolder(string folderId, string newName)`
- `DeleteFolder(string folderId)`
- `GetCaptureRecords(string folderId)`
- `MoveCaptureRecord(string captureRecordId, string targetFolderId)`
- `MoveCaptureRecords(IEnumerable<string> captureRecordIds, string targetFolderId)`
- `DeleteCaptureRecord(string captureRecordId)`

#### Migration behavior

On first load after the refactor:

- legacy nested folder sessions should be mapped into flat folder groups
- any pathless or unknown sessions go to `Global`
- nested paths like `project/client-a` should be flattened by rule

Recommended migration rule:

- use only the first segment as the folder candidate if old nested data exists
- if that is ambiguous, move old records into `Global`

If you want less risky behavior, route all legacy non-global sessions into `Global` and let users reorganize manually later.

## ViewModel Refactor

### MainWindowViewModel

#### Remove or replace

- `SessionTreePath`
- tree-folder validation logic
- tree node selection state

#### Add

- `ObservableCollection<SessionFolderItemViewModel> SessionFolders`
- `SessionFolderItemViewModel? SelectedSessionFolder`
- `ObservableCollection<CaptureSessionItemViewModel> FolderCaptureRecords`
- `bool HasSelectedSessionFolder`
- `string SelectedSessionFolderName`

#### Folder commands

- `CreateSessionFolderCommand`
- `RenameSessionFolderCommand`
- `DeleteSessionFolderCommand`
- `SelectSessionFolderCommand`

#### Record commands

- `MoveSelectedCaptureRecordsCommand`
- `DeleteSelectedCaptureRecordsCommand`
- `DeleteCaptureRecordCommand`

#### Capture tab state

- `SelectedCaptureFolder`
- last selected folder persistence in settings by `FolderId`, not by path string

### App settings

#### Update

- `src/Sublingual.App/Models/AppSettings.cs`

Replace:

- `LastSessionTreePath`

With:

- `LastSessionFolderId`

Migration fallback:

- if `LastSessionFolderId` missing, use `Global`

## UI Specification

### Capture tab

#### Goal

Capture tab only chooses an existing destination folder.

#### UI

- folder picker dropdown or compact folder browser
- default selection is `Global`
- no folder creation controls
- no path textbox
- no tree browser

#### Behavior

- starting capture uses selected folder
- if folder no longer exists, fallback to `Global`

### Sessions tab

Split into two panes.

#### Left pane: folder browser

- flat list of folders
- each row shows:
  - folder name
  - capture count
  - default badge for `Global`
- actions in header:
  - create folder
  - rename folder
  - delete folder

#### Right pane: capture records in selected folder

- list or table of records in current folder
- columns:
  - title
  - created at
  - duration
  - audio file
- row actions:
  - open detail
  - move
  - delete
- bulk actions:
  - move selected
  - delete selected

### Dialogs

#### Create folder dialog

- single field: `Folder name`
- realtime validation
- create button disabled when invalid

Validation rules:

- required
- not equal to `Global`
- unique ignoring case
- no `/` or `\`
- no filesystem-invalid chars

#### Rename folder dialog

- same validation as create
- disabled for `Global`

#### Delete folder dialog

If folder empty:

- message: delete folder permanently

If folder has captures:

- message: all captures will be moved to `Global` before deletion

#### Move capture dialog

- no path input
- dropdown/list of target folders
- exclude current folder from targets when possible
- support single and bulk move

## Recommended Refactor Sequence

### Phase 1: storage and migration

1. add folder store (`folders.json`)
2. add default `Global` folder bootstrap
3. move metadata from `TreePath`-based to `FolderId`-based
4. add migration logic for legacy sessions

Verify:

- old data still loads
- new captures land in `Global`

### Phase 2: capture flow

1. remove folder creation/path input from capture tab
2. bind capture tab to existing folders only
3. persist last selected folder id

Verify:

- capture starts with selected folder
- missing folder falls back to `Global`

### Phase 3: sessions page folder CRUD

1. build flat folder browser
2. add create dialog
3. add rename dialog
4. add delete dialog with move-to-Global rule

Verify:

- folder CRUD works without path input
- `Global` remains protected

### Phase 4: capture record management

1. filter records by selected folder
2. add single delete
3. add bulk delete
4. add single move
5. add bulk move

Verify:

- record moves update UI and on-disk location correctly

### Phase 5: cleanup

1. remove old tree-path code
2. remove obsolete view models and dialogs
3. remove legacy settings keys when safe

## Acceptance Criteria

### Folder management

- user can create same-level folders only
- user cannot create nested folders
- user can rename non-default folders
- user can delete non-default folders
- deleting a non-empty folder moves records to `Global`
- `Global` always exists and cannot be deleted

### Capture management

- user can select an existing folder before capture
- if nothing valid is selected, capture goes to `Global`
- user can delete records inside a folder
- user can move records between folders without path input
- user can bulk move and bulk delete records

### UX

- no raw path input in folder management flow
- sessions page is folder-first, then record list
- modal dialogs remain centered and use full-window overlays

## Known Code Areas To Replace

- `TreePath` usage in `CaptureSessionStorage`
- `SessionTreePath` usage in `MainWindowViewModel`
- tree browser UI in `MainWindow.axaml`
- `LastSessionTreePath` in settings
- any metadata reads that assume nested path ownership
