import { app, BrowserWindow } from 'electron';
import path from 'path';
import { registerIpcHandlers } from './main/ipc-handlers';
import { stopAudioCapture } from './main/audio/audio-capture';
import { stopVosk } from './main/asr/vosk-process';
import { getSessionStorage } from './main/sessions/session-storage';
import { getOverlayManager } from './main/overlay/overlay-window';

declare const MAIN_WINDOW_VITE_DEV_SERVER_URL: string;
declare const MAIN_WINDOW_VITE_NAME: string;

let mainWindow: BrowserWindow | null = null;

const createWindow = () => {
  mainWindow = new BrowserWindow({
    width: 1024,
    height: 768,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  if (MAIN_WINDOW_VITE_DEV_SERVER_URL) {
    mainWindow.loadURL(MAIN_WINDOW_VITE_DEV_SERVER_URL);
    mainWindow.webContents.openDevTools();
  } else {
    mainWindow.loadFile(
      path.join(__dirname, `../renderer/${MAIN_WINDOW_VITE_NAME}/index.html`)
    );
  }

  registerIpcHandlers(mainWindow);
};

app.on('ready', createWindow);

app.on('window-all-closed', () => {
  app.quit();
});

app.on('before-quit', () => {
  stopAudioCapture();
  stopVosk();
  getSessionStorage().stopSession();
  getOverlayManager().destroy();
});

app.on('activate', () => {
  if (mainWindow === null) {
    createWindow();
  }
});
