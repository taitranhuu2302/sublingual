import { BrowserWindow } from "electron";
import path from "path";
import { getSettings, setSettings } from "../settings/settings-store";

declare const MAIN_WINDOW_VITE_DEV_SERVER_URL: string;
declare const MAIN_WINDOW_VITE_NAME: string;

class OverlayManager {
  private window: BrowserWindow | null = null;
  private parentWindow: BrowserWindow | null = null;

  show(parentWindow: BrowserWindow): void {
    this.parentWindow = parentWindow;

    if (this.window && !this.window.isDestroyed()) {
      this.window.show();
      this.notifyVisibility(true);
      return;
    }

    const settings = getSettings().overlay;

    this.window = new BrowserWindow({
      width: settings.width,
      height: settings.height,
      x: settings.positionX ?? undefined,
      y: settings.positionY ?? undefined,
      transparent: true,
      frame: false,
      alwaysOnTop: true,
      skipTaskbar: true,
      hasShadow: false,
      resizable: true,
      webPreferences: {
        preload: path.join(__dirname, "overlay-preload.js"),
        contextIsolation: true,
        nodeIntegration: false,
        sandbox: false,
      },
    });

    // Load the overlay page
    if (MAIN_WINDOW_VITE_DEV_SERVER_URL) {
      this.window.loadURL(`${MAIN_WINDOW_VITE_DEV_SERVER_URL}/overlay.html`);
    } else {
      this.window.loadFile(
        path.join(__dirname, `../renderer/${MAIN_WINDOW_VITE_NAME}/overlay.html`)
      );
    }

    this.window.on("moved", () => this.savePosition());
    this.window.on("resized", () => this.savePosition());

    this.window.on("close", (e) => {
      e.preventDefault();
      this.window?.hide();
    });

    this.window.on("closed", () => {
      this.window = null;
    });
  }

  hide(): void {
    if (this.window && !this.window.isDestroyed()) {
      this.window.hide();
    }
    this.notifyVisibility(false);
  }

  isVisible(): boolean {
    return this.window !== null && !this.window.isDestroyed() && this.window.isVisible();
  }

  sendToOverlay(channel: string, ...args: unknown[]): void {
    if (this.window && !this.window.isDestroyed()) {
      this.window.webContents.send(channel, ...args);
    }
  }

  destroy(): void {
    if (this.window && !this.window.isDestroyed()) {
      this.window.removeAllListeners("close");
      this.window.close();
      this.window = null;
    }
  }

  private notifyVisibility(visible: boolean): void {
    if (this.parentWindow && !this.parentWindow.isDestroyed()) {
      this.parentWindow.webContents.send("overlay:visibility-changed", visible);
    }
  }

  private savePosition(): void {
    if (!this.window || this.window.isDestroyed()) return;

    const bounds = this.window.getBounds();
    setSettings({
      overlay: {
        ...getSettings().overlay,
        width: bounds.width,
        height: bounds.height,
        positionX: bounds.x,
        positionY: bounds.y,
      },
    });
  }
}

let instance: OverlayManager | null = null;
export function getOverlayManager(): OverlayManager {
  if (!instance) instance = new OverlayManager();
  return instance;
}
