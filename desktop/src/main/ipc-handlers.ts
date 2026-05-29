import { BrowserWindow } from "electron";
import { registerAudioHandlers } from "./ipc/audio-handlers";
import { registerAsrHandlers } from "./ipc/asr-handlers";
import { registerSettingsHandlers } from "./ipc/settings-handlers";
import { registerTranslationHandlers } from "./ipc/translation-handlers";
import { registerModelHandlers } from "./ipc/model-handlers";
import { registerOverlayHandlers } from "./ipc/overlay-handlers";
import { registerSessionHandlers } from "./ipc/session-handlers";

export function registerIpcHandlers(mainWindow: BrowserWindow) {
  registerAudioHandlers(mainWindow);
  registerAsrHandlers(mainWindow);
  registerSettingsHandlers(mainWindow);
  registerTranslationHandlers();
  registerModelHandlers(mainWindow);
  registerOverlayHandlers(mainWindow);
  registerSessionHandlers(mainWindow);
}
