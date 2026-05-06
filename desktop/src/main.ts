import {
  app,
  BrowserWindow,
  desktopCapturer,
  dialog,
  ipcMain,
} from 'electron';
import path from 'node:path';
import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { createWriteStream, existsSync, mkdirSync } from 'node:fs';
import started from 'electron-squirrel-startup';
import { getBackendHealthUrl } from './config/backend-config.main';

let backendProcess: ChildProcessWithoutNullStreams | null = null;
let ownsBackendProcess = false;

const getPythonCommand = (
  backendCwd: string,
): { command: string; args: string[] } => {
  const venvPython =
    process.platform === 'win32'
      ? path.join(backendCwd, '.venv', 'Scripts', 'python.exe')
      : path.join(backendCwd, '.venv', 'bin', 'python');

  if (existsSync(venvPython)) {
    return { command: venvPython, args: ['main.py'] };
  }

  if (process.platform === 'win32') {
    // Windows Python launcher is often available even when `python` is not in PATH.
    return { command: 'py', args: ['-3', 'main.py'] };
  }

  return { command: 'python3', args: ['main.py'] };
};

const startBackend = async (): Promise<void> => {
  if (backendProcess) {
    return;
  }

  try {
    const backendAlreadyRunning = await isBackendHealthy();
    if (backendAlreadyRunning) {
      ownsBackendProcess = false;
      return;
    }

    const userDataDir = app.getPath('userData');
    const logsDir = path.join(userDataDir, 'logs');
    if (!existsSync(logsDir)) {
      mkdirSync(logsDir, { recursive: true });
    }
    const logFilePath = path.join(logsDir, 'backend.log');
    const logStream = createWriteStream(logFilePath, { flags: 'a' });

    const backendCwd =
      process.env.BACKEND_CWD ??
      path.resolve(__dirname, '..', '..', 'backend');
    const { command, args } = getPythonCommand(backendCwd);

    backendProcess = spawn(command, args, {
      cwd: backendCwd,
      env: {
        ...process.env,
        PYTHONUNBUFFERED: '1',
      },
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    ownsBackendProcess = true;

    backendProcess.stdout.on('data', (data: Buffer) => {
      logStream.write(`[STDOUT] ${data.toString()}`);
    });

    backendProcess.stderr.on('data', (data: Buffer) => {
      logStream.write(`[STDERR] ${data.toString()}`);
    });

    backendProcess.on('exit', (code, signal) => {
      logStream.write(
        `[EXIT] Backend process exited with code=${code} signal=${signal}\n`,
      );
      backendProcess = null;
    });

    const processError = new Promise<never>((_, reject) => {
      backendProcess?.once('error', (error) => {
        reject(error);
      });
      backendProcess?.once('exit', (code) => {
        if (code !== 0) {
          reject(new Error(`Backend exited before healthy (exit code: ${code})`));
        }
      });
    });

    await Promise.race([waitForBackendHealth(), processError]);
  } catch (error) {
    backendProcess = null;
    ownsBackendProcess = false;
    const message =
      error instanceof Error ? error.message : 'Unknown error starting backend';
    dialog.showErrorBox(
      'Backend Startup Failed',
      `The Python backend could not be started.\n\nDetails: ${message}`,
    );
    throw error;
  }
};

const isBackendHealthy = async (): Promise<boolean> => {
  try {
    await waitForBackendHealth(2_000);
    return true;
  } catch {
    return false;
  }
};

const waitForBackendHealth = async (timeoutMs = 15_000): Promise<void> => {
  const intervalMs = 500;
  const start = Date.now();

  // Using dynamic import to avoid bundler issues with node-fetch in main process.
  // eslint-disable-next-line @typescript-eslint/no-var-requires, global-require
  const fetch: typeof import('node-fetch')['default'] = require('node-fetch');

  const healthUrl = getBackendHealthUrl();

  // eslint-disable-next-line no-constant-condition
  while (true) {
    if (Date.now() - start > timeoutMs) {
      throw new Error('Backend health check timed out after 15 seconds');
    }

    try {
      const response = await fetch(healthUrl, { method: 'GET' });
      if (response.ok) {
        return;
      }
    } catch {
      // ignore and retry
    }

    await new Promise((resolve) => {
      setTimeout(resolve, intervalMs);
    });
  }
};

const stopBackend = (): void => {
  if (!backendProcess || !ownsBackendProcess) {
    return;
  }

  if (process.platform === 'win32') {
    backendProcess.kill();
  } else {
    backendProcess.kill('SIGTERM');
  }

  backendProcess = null;
  ownsBackendProcess = false;
};

const createWindow = async (): Promise<void> => {
  await startBackend();

  const mainWindow = new BrowserWindow({
    width: 800,
    height: 600,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
    },
  });

  if (MAIN_WINDOW_VITE_DEV_SERVER_URL) {
    mainWindow.loadURL(MAIN_WINDOW_VITE_DEV_SERVER_URL);
  } else {
    mainWindow.loadFile(
      path.join(__dirname, `../renderer/${MAIN_WINDOW_VITE_NAME}/index.html`),
    );
  }

  mainWindow.webContents.openDevTools();
};

if (started) {
  app.quit();
}

app.on('ready', () => {
  void createWindow();
});

app.on('before-quit', () => {
  stopBackend();
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    void createWindow();
  }
});

ipcMain.handle('get-desktop-audio-sources', async () => {
  const sources = await desktopCapturer.getSources({
    types: ['screen', 'window'],
    fetchWindowIcons: false,
    thumbnailSize: { width: 0, height: 0 },
  });

  return sources.map((source) => ({
    id: source.id,
    name: source.name,
  }));
});
