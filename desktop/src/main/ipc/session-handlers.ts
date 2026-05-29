import { ipcMain, BrowserWindow, dialog, shell } from "electron";
import { getSessionStorage } from "../sessions/session-storage";

export function registerSessionHandlers(mainWindow: BrowserWindow) {
  ipcMain.handle("sessions:list", async (_event, search?: string) =>
    getSessionStorage().listSessions(search),
  );
  ipcMain.handle("sessions:get-transcript", async (_event, sessionId: string) =>
    getSessionStorage().getTranscript(sessionId),
  );
  ipcMain.handle("sessions:delete", async (_event, sessionIds: string[]) =>
    getSessionStorage().deleteSessions(sessionIds),
  );
  ipcMain.handle("sessions:clear-all", async () => getSessionStorage().clearAll());

  ipcMain.handle("sessions:export-txt", async (_event, sessionId: string) => {
    const result = await dialog.showSaveDialog(mainWindow, {
      title: "Export Transcript as Text",
      defaultPath: `transcript-${sessionId}.txt`,
      filters: [{ name: "Text Files", extensions: ["txt"] }],
    });
    if (!result.canceled && result.filePath) {
      getSessionStorage().exportAsTxt(sessionId, result.filePath);
    }
  });

  ipcMain.handle("sessions:export-json", async (_event, sessionId: string) => {
    const result = await dialog.showSaveDialog(mainWindow, {
      title: "Export Transcript as JSON",
      defaultPath: `transcript-${sessionId}.json`,
      filters: [{ name: "JSON Files", extensions: ["json"] }],
    });
    if (!result.canceled && result.filePath) {
      getSessionStorage().exportAsJson(sessionId, result.filePath);
    }
  });

  ipcMain.handle("sessions:open-folder", async (_event, sessionId: string) => {
    const folder = getSessionStorage().getSessionFolder(sessionId);
    if (folder) await shell.openPath(folder);
  });
}
