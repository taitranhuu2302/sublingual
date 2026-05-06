type PartialMessage = { type: 'partial'; text: string };
type FinalMessage = {
  type: 'final';
  original: string;
  translated: string;
  timestamp: string;
};
type ErrorMessage = { type: 'error'; message: string };
type IncomingMessage = PartialMessage | FinalMessage | ErrorMessage;

type WebSocketClientOptions = {
  url: string;
  onOpen?: () => void;
  onClose?: () => void;
  onPartial?: (text: string) => void;
  onFinal?: (message: FinalMessage) => void;
  onErrorMessage?: (message: string) => void;
  onConnectionError?: (message: string) => void;
};

export class RendererWebSocketClient {
  private readonly url: string;
  private readonly onOpen?: () => void;
  private readonly onClose?: () => void;
  private readonly onPartial?: (text: string) => void;
  private readonly onFinal?: (message: FinalMessage) => void;
  private readonly onErrorMessage?: (message: string) => void;
  private readonly onConnectionError?: (message: string) => void;

  private socket: WebSocket | null = null;
  private reconnectAttempts = 0;
  private reconnectTimers: number[] = [];
  private shouldReconnect = true;
  private isOpening = false;
  private pendingBinaryQueue: ArrayBuffer[] = [];

  constructor(options: WebSocketClientOptions) {
    this.url = options.url;
    this.onOpen = options.onOpen;
    this.onClose = options.onClose;
    this.onPartial = options.onPartial;
    this.onFinal = options.onFinal;
    this.onErrorMessage = options.onErrorMessage;
    this.onConnectionError = options.onConnectionError;
  }

  connect(): void {
    if (this.isOpening || this.socket?.readyState === WebSocket.OPEN) {
      return;
    }

    this.isOpening = true;
    this.socket = new WebSocket(this.url);

    this.socket.onopen = () => {
      this.isOpening = false;
      this.reconnectAttempts = 0;
      this.flushPendingQueue();
      this.onOpen?.();
    };

    this.socket.onmessage = (event) => {
      if (typeof event.data !== 'string') {
        return;
      }
      try {
        const parsed = JSON.parse(event.data) as IncomingMessage;
        if (parsed.type === 'partial') {
          this.onPartial?.(parsed.text);
          return;
        }
        if (parsed.type === 'final') {
          this.onFinal?.(parsed);
          return;
        }
        if (parsed.type === 'error') {
          this.onErrorMessage?.(parsed.message);
        }
      } catch {
        this.onConnectionError?.('Received malformed JSON from backend.');
      }
    };

    this.socket.onerror = () => {
      this.onConnectionError?.('WebSocket connection error.');
    };

    this.socket.onclose = () => {
      this.isOpening = false;
      this.onClose?.();
      if (this.shouldReconnect) {
        this.scheduleReconnect();
      }
    };
  }

  sendBinaryChunk(chunk: ArrayBuffer): void {
    if (this.socket?.readyState === WebSocket.OPEN) {
      this.socket.send(chunk);
      return;
    }

    this.pendingBinaryQueue.push(chunk);
  }

  stop(): void {
    this.shouldReconnect = false;
    this.clearReconnectTimers();
    if (this.socket?.readyState === WebSocket.OPEN) {
      this.socket.send(JSON.stringify({ type: 'end_session' }));
    }
    this.socket?.close();
    this.socket = null;
    this.pendingBinaryQueue = [];
  }

  private flushPendingQueue(): void {
    if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
      return;
    }
    for (const chunk of this.pendingBinaryQueue) {
      this.socket.send(chunk);
    }
    this.pendingBinaryQueue = [];
  }

  private scheduleReconnect(): void {
    if (this.reconnectAttempts >= 3) {
      this.onConnectionError?.(
        'WebSocket reconnect failed after 3 attempts (1s, 2s, 4s).',
      );
      return;
    }

    const delays = [1_000, 2_000, 4_000];
    const delay = delays[this.reconnectAttempts];
    this.reconnectAttempts += 1;

    const timer = window.setTimeout(() => {
      this.connect();
    }, delay);
    this.reconnectTimers.push(timer);
  }

  private clearReconnectTimers(): void {
    for (const timer of this.reconnectTimers) {
      window.clearTimeout(timer);
    }
    this.reconnectTimers = [];
  }
}

