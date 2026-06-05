import { useState, useCallback, useEffect } from "react";
import type { SessionSummary, TranscriptLine, SessionFolder } from "../types/electron-api";

export function useSessions() {
  const [sessions, setSessions] = useState<SessionSummary[]>([]);
  const [folders, setFolders] = useState<SessionFolder[]>([]);
  const [activeFolder, setActiveFolder] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [activeSession, setActiveSession] = useState<{
    info: SessionSummary;
    transcript: TranscriptLine[];
  } | null>(null);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(false);

  const loadSessions = useCallback(async (query?: string) => {
    setLoading(true);
    try {
      const list = await window.electronAPI.sessions.list(query);
      setSessions(list);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadFolders = useCallback(async () => {
    try {
      const list = await window.electronAPI.sessions.listFolders();
      setFolders(list);
    } catch {
      // ignore
    }
  }, []);

  const refreshAll = useCallback(async () => {
    await loadSessions(search || undefined);
    await loadFolders();
  }, [loadSessions, loadFolders, search]);

  useEffect(() => {
    if (!window.electronAPI) return;
    refreshAll();
  }, [refreshAll]);

  const selectSession = useCallback(async (session: SessionSummary) => {
    const transcript = await window.electronAPI.sessions.getTranscript(session.id);
    setActiveSession({ info: session, transcript });
  }, []);

  const toggleSelect = useCallback((id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const selectAll = useCallback(() => {
    setSelectedIds(new Set(sessions.map((s) => s.id)));
  }, [sessions]);

  const deselectAll = useCallback(() => {
    setSelectedIds(new Set());
  }, []);

  const deleteSelected = useCallback(async () => {
    if (selectedIds.size === 0) return;
    await window.electronAPI.sessions.delete(Array.from(selectedIds));
    if (activeSession && selectedIds.has(activeSession.info.id)) {
      setActiveSession(null);
    }
    setSelectedIds(new Set());
    await refreshAll();
  }, [selectedIds, activeSession, refreshAll]);

  const exportTxt = useCallback(async (id: string) => {
    await window.electronAPI.sessions.exportTxt(id);
  }, []);

  const exportJson = useCallback(async (id: string) => {
    await window.electronAPI.sessions.exportJson(id);
  }, []);

  const deleteSession = useCallback(async (id: string) => {
    await window.electronAPI.sessions.delete([id]);
    if (activeSession?.info.id === id) {
      setActiveSession(null);
    }
    setSelectedIds((prev) => {
      const next = new Set(prev);
      next.delete(id);
      return next;
    });
    await refreshAll();
  }, [activeSession, refreshAll]);

  const openFolder = useCallback(async (id: string) => {
    await window.electronAPI.sessions.openFolder(id);
  }, []);

  const createFolder = useCallback(async (name: string) => {
    const folder = await window.electronAPI.sessions.createFolder(name);
    await loadFolders();
    return folder;
  }, [loadFolders]);

  const renameFolder = useCallback(async (folderId: string, name: string) => {
    await window.electronAPI.sessions.renameFolder(folderId, name);
    await loadFolders();
  }, [loadFolders]);

  const deleteFolder = useCallback(async (folderId: string) => {
    await window.electronAPI.sessions.deleteFolder(folderId);
    if (activeFolder === folderId) setActiveFolder("global");
    await refreshAll();
  }, [activeFolder, refreshAll]);

  const moveSessions = useCallback(async (sessionIds: string[], folderId: string) => {
    await window.electronAPI.sessions.moveSessions(sessionIds, folderId);
    await refreshAll();
  }, [refreshAll]);

  const filteredSessions = activeFolder
    ? sessions.filter((s) => s.folderId === activeFolder)
    : sessions;

  return {
    sessions: filteredSessions,
    allSessions: sessions,
    folders,
    activeFolder,
    selectedIds,
    activeSession,
    search,
    loading,
    setSearch,
    setActiveFolder,
    selectSession,
    toggleSelect,
    selectAll,
    deselectAll,
    deleteSelected,
    deleteSession,
    exportTxt,
    exportJson,
    openFolder,
    createFolder,
    renameFolder,
    deleteFolder,
    moveSessions,
  };
}
