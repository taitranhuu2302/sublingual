import fs from "fs";
import path from "path";
import { getSettings } from "../settings/settings-store";

export interface SessionSummary {
  id: string;
  date: string;
  duration: number;
  segmentCount: number;
  preview: string;
  folderPath: string;
  folderId: string;
}

export interface SessionFolder {
  id: string;
  name: string;
  createdAt: string;
  sessionCount: number;
}

interface FolderRegistry {
  folders: SessionFolder[];
  sessionFolders: Record<string, string>;
}

export interface TranscriptLine {
  id: string;
  text: string;
  translatedText?: string;
  timestamp: number;
  isFinal: boolean;
}

interface SessionMeta {
  startedAt: string;
  duration: number;
}

// Active recording session
interface ActiveSession {
  id: string;
  folderPath: string;
  startedAt: Date;
  lines: TranscriptLine[];
}

class SessionStorage {
  private activeSession: ActiveSession | null = null;

  private getSessionsRoot(): string {
    return getSettings().storage.sessionsRoot;
  }

  // --- Folder Registry ---

  private getRegistryPath(): string {
    return path.join(this.getSessionsRoot(), "folders.json");
  }

  private ensureRoot(): void {
    const root = this.getSessionsRoot();
    if (!fs.existsSync(root)) {
      fs.mkdirSync(root, { recursive: true });
    }
  }

  private loadRegistry(): FolderRegistry {
    this.ensureRoot();
    const regPath = this.getRegistryPath();
    if (!fs.existsSync(regPath)) {
      const defaultRegistry: FolderRegistry = {
        folders: [{ id: "global", name: "Global", createdAt: new Date().toISOString() }],
        sessionFolders: {},
      };
      fs.writeFileSync(regPath, JSON.stringify(defaultRegistry, null, 2), "utf-8");
      return defaultRegistry;
    }
    try {
      const data = JSON.parse(fs.readFileSync(regPath, "utf-8"));
      if (!data.sessionFolders) data.sessionFolders = {};
      if (!data.folders) data.folders = [];
      return data;
    } catch {
      return { folders: [], sessionFolders: {} };
    }
  }

  private saveRegistry(registry: FolderRegistry): void {
    this.ensureRoot();
    fs.writeFileSync(this.getRegistryPath(), JSON.stringify(registry, null, 2), "utf-8");
  }

  listFolders(): SessionFolder[] {
    const registry = this.loadRegistry();
    const allSessions = this.listSessions();
    return registry.folders.map((f) => ({
      ...f,
      sessionCount: allSessions.filter((s) => s.folderId === f.id).length,
    }));
  }

  createFolder(name: string): SessionFolder {
    const registry = this.loadRegistry();
    const id = name.toLowerCase().replace(/\s+/g, "-").replace(/[^a-z0-9-]/g, "");
    if (registry.folders.some((f) => f.id === id)) {
      throw new Error(`Folder "${name}" already exists`);
    }
    const folder: SessionFolder = {
      id,
      name,
      createdAt: new Date().toISOString(),
    };
    registry.folders.push(folder);
    this.saveRegistry(registry);
    return folder;
  }

  renameFolder(folderId: string, name: string): void {
    const registry = this.loadRegistry();
    const folder = registry.folders.find((f) => f.id === folderId);
    if (!folder) throw new Error(`Folder "${folderId}" not found`);
    if (folder.id === "global") throw new Error("Cannot rename the Global folder");
    folder.name = name;
    this.saveRegistry(registry);
  }

  deleteFolder(folderId: string): void {
    const registry = this.loadRegistry();
    const folder = registry.folders.find((f) => f.id === folderId);
    if (!folder) throw new Error(`Folder "${folderId}" not found`);
    if (folder.id === "global") throw new Error("Cannot delete the Global folder");
    registry.folders = registry.folders.filter((f) => f.id !== folderId);
    // Move orphaned sessions to Global
    for (const [sessionId, fid] of Object.entries(registry.sessionFolders)) {
      if (fid === folderId) {
        registry.sessionFolders[sessionId] = "global";
      }
    }
    this.saveRegistry(registry);
  }

  moveSessions(sessionIds: string[], folderId: string): void {
    const registry = this.loadRegistry();
    if (!registry.folders.some((f) => f.id === folderId)) {
      throw new Error(`Folder "${folderId}" not found`);
    }
    for (const id of sessionIds) {
      registry.sessionFolders[id] = folderId;
    }
    this.saveRegistry(registry);
  }

  // --- Live recording ---

  startSession(): string {
    const now = new Date();
    const id = now.toISOString().replace(/[:.]/g, "-");
    const root = this.getSessionsRoot();
    const folderPath = path.join(root, id);

    this.ensureRoot();
    fs.mkdirSync(folderPath, { recursive: true });

    this.activeSession = { id, folderPath, startedAt: now, lines: [] };

    // Assign to Global folder
    const registry = this.loadRegistry();
    registry.sessionFolders[id] = "global";
    this.saveRegistry(registry);

    // Write meta.json immediately so session is valid even before stop
    const meta: SessionMeta = {
      startedAt: now.toISOString(),
      duration: 0,
    };
    fs.writeFileSync(path.join(folderPath, "meta.json"), JSON.stringify(meta, null, 2), "utf-8");
    // Write empty transcript
    fs.writeFileSync(path.join(folderPath, "transcript.json"), "[]", "utf-8");

    console.log("[session] started:", id);
    return id;
  }

  appendLine(line: TranscriptLine): void {
    if (!this.activeSession) return;
    this.activeSession.lines.push(line);
    this.saveActive();
  }

  stopSession(): string | null {
    if (!this.activeSession) return null;

    const duration = Math.floor((Date.now() - this.activeSession.startedAt.getTime()) / 1000);
    const meta: SessionMeta = {
      startedAt: this.activeSession.startedAt.toISOString(),
      duration,
    };

    const metaPath = path.join(this.activeSession.folderPath, "meta.json");
    fs.writeFileSync(metaPath, JSON.stringify(meta, null, 2), "utf-8");
    this.saveActive();

    const id = this.activeSession.id;
    console.log("[session] stopped:", id, `${this.activeSession.lines.length} lines, ${duration}s`);
    this.activeSession = null;
    return id;
  }

  getActiveSessionId(): string | null {
    return this.activeSession?.id ?? null;
  }

  private saveActive(): void {
    if (!this.activeSession) return;
    const transcriptPath = path.join(this.activeSession.folderPath, "transcript.json");
    fs.writeFileSync(transcriptPath, JSON.stringify(this.activeSession.lines, null, 2), "utf-8");
  }

  // --- Querying ---

  listSessions(search?: string): SessionSummary[] {
    const root = this.getSessionsRoot();
    if (!fs.existsSync(root)) return [];

    const registry = this.loadRegistry();
    const entries = fs.readdirSync(root, { withFileTypes: true });
    const sessions: SessionSummary[] = [];

    for (const entry of entries) {
      if (!entry.isDirectory()) continue;
      if (entry.name === "folders.json") continue;

      const folderPath = path.join(root, entry.name);
      const metaPath = path.join(folderPath, "meta.json");
      const transcriptPath = path.join(folderPath, "transcript.json");

      if (!fs.existsSync(transcriptPath)) continue;

      let meta: SessionMeta = { startedAt: "", duration: 0 };
      try {
        if (fs.existsSync(metaPath)) {
          meta = JSON.parse(fs.readFileSync(metaPath, "utf-8"));
        }
      } catch {
        // ignore
      }

      if (!meta.startedAt || isNaN(new Date(meta.startedAt).getTime())) {
        const parsed = entry.name
          .replace(/^(\d{4}-\d{2}-\d{2}T\d{2})-(\d{2})-(\d{2})-(\d{3})Z$/, "$1:$2:$3.$4Z");
        meta.startedAt = new Date(parsed).toISOString();
      }

      let lines: TranscriptLine[] = [];
      let preview = "";
      try {
        lines = JSON.parse(fs.readFileSync(transcriptPath, "utf-8"));
        preview = lines[0]?.text?.slice(0, 80) ?? "";
      } catch {
        // ignore
      }

      const summary: SessionSummary = {
        id: entry.name,
        date: meta.startedAt,
        duration: meta.duration,
        segmentCount: lines.length,
        preview,
        folderPath,
        folderId: registry.sessionFolders[entry.name] || "global",
      };

      if (search) {
        const q = search.toLowerCase();
        if (
          !summary.id.toLowerCase().includes(q) &&
          !summary.preview.toLowerCase().includes(q) &&
          !summary.date.toLowerCase().includes(q)
        ) {
          continue;
        }
      }

      sessions.push(summary);
    }

    return sessions.sort((a, b) => b.date.localeCompare(a.date));
  }

  getTranscript(sessionId: string): TranscriptLine[] {
    const transcriptPath = path.join(
      this.getSessionsRoot(),
      sessionId,
      "transcript.json"
    );
    if (!fs.existsSync(transcriptPath)) return [];
    try {
      return JSON.parse(fs.readFileSync(transcriptPath, "utf-8"));
    } catch {
      return [];
    }
  }

  deleteSessions(sessionIds: string[]): number {
    let count = 0;
    const root = this.getSessionsRoot();
    const registry = this.loadRegistry();
    for (const id of sessionIds) {
      const folderPath = path.join(root, id);
      if (fs.existsSync(folderPath)) {
        fs.rmSync(folderPath, { recursive: true, force: true });
        count++;
      }
      delete registry.sessionFolders[id];
    }
    this.saveRegistry(registry);
    return count;
  }

  clearAll(): number {
    const sessions = this.listSessions();
    return this.deleteSessions(sessions.map((s) => s.id));
  }

  exportAsTxt(sessionId: string, destPath: string): void {
    const lines = this.getTranscript(sessionId);
    const text = lines
      .filter((l) => l.isFinal)
      .map((l) => {
        const ts = new Date(l.timestamp).toLocaleTimeString();
        const speaker = (l as any).speakerLabel ? `[${(l as any).speakerLabel}] ` : "";
        let line = `[${ts}] ${speaker}${l.text}`;
        if (l.translatedText) line += `\n         ${l.translatedText}`;
        return line;
      })
      .join("\n\n");
    fs.writeFileSync(destPath, text, "utf-8");
  }

  exportAsJson(sessionId: string, destPath: string): void {
    const lines = this.getTranscript(sessionId);
    fs.writeFileSync(destPath, JSON.stringify(lines, null, 2), "utf-8");
  }

  getSessionFolder(sessionId: string): string | null {
    const folderPath = path.join(this.getSessionsRoot(), sessionId);
    return fs.existsSync(folderPath) ? folderPath : null;
  }
}

let instance: SessionStorage | null = null;
export function getSessionStorage(): SessionStorage {
  if (!instance) instance = new SessionStorage();
  return instance;
}
