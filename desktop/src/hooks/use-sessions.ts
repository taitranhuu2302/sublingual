import { useState, useCallback, useEffect } from "react";
import type { SessionSummary, TranscriptLine } from "../types/electron-api";

export function useSessions() {
  const [sessions, setSessions] = useState<SessionSummary[]>([]);
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

  useEffect(() => {
    if (!window.electronAPI) return;
    loadSessions(search || undefined);
  }, [loadSessions, search]);

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
    await loadSessions(search || undefined);
  }, [selectedIds, activeSession, loadSessions, search]);

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
    await loadSessions(search || undefined);
  }, [activeSession, loadSessions, search]);

  const openFolder = useCallback(async (id: string) => {
    await window.electronAPI.sessions.openFolder(id);
  }, []);

  return {
    sessions,
    selectedIds,
    activeSession,
    search,
    loading,
    setSearch,
    selectSession,
    toggleSelect,
    selectAll,
    deselectAll,
    deleteSelected,
    deleteSession,
    exportTxt,
    exportJson,
    openFolder,
  };
}
