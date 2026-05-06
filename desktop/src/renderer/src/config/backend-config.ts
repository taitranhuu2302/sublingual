const DEFAULT_BACKEND_HOST = '127.0.0.1';
const DEFAULT_BACKEND_PORT = 8765;
const BACKEND_WS_AUDIO_PATH = '/ws/audio';

const toValidPort = (value: string | undefined): number => {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : DEFAULT_BACKEND_PORT;
};

export const BACKEND_HOST =
  import.meta.env.VITE_BACKEND_HOST ?? DEFAULT_BACKEND_HOST;
export const BACKEND_PORT = toValidPort(import.meta.env.VITE_BACKEND_PORT);

export const getBackendWsAudioUrl = (): string =>
  `ws://${BACKEND_HOST}:${BACKEND_PORT}${BACKEND_WS_AUDIO_PATH}`;

