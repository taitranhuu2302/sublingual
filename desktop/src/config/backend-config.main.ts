const DEFAULT_BACKEND_HOST = '127.0.0.1';
const DEFAULT_BACKEND_PORT = 8765;
const BACKEND_HEALTH_PATH = '/health';

const toValidPort = (value: string | undefined): number => {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : DEFAULT_BACKEND_PORT;
};

export const BACKEND_HOST =
  process.env.BACKEND_HOST ?? DEFAULT_BACKEND_HOST;
export const BACKEND_PORT = toValidPort(process.env.BACKEND_PORT);

export const getBackendHealthUrl = (): string =>
  `http://${BACKEND_HOST}:${BACKEND_PORT}${BACKEND_HEALTH_PATH}`;

