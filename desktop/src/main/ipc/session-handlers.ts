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

  // --- Folder management ---

  ipcMain.handle("sessions:list-folders", async () =>
    getSessionStorage().listFolders(),
  );

  ipcMain.handle("sessions:create-folder", async (_event, name: string) =>
    getSessionStorage().createFolder(name),
  );

  ipcMain.handle("sessions:rename-folder", async (_event, folderId: string, name: string) =>
    getSessionStorage().renameFolder(folderId, name),
  );

  ipcMain.handle("sessions:delete-folder", async (_event, folderId: string) =>
    getSessionStorage().deleteFolder(folderId),
  );

  ipcMain.handle("sessions:move-sessions", async (_event, sessionIds: string[], folderId: string) =>
    getSessionStorage().moveSessions(sessionIds, folderId),
  );
}
