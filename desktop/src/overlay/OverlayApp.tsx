import { useState, useEffect, useRef, useCallback } from "react";

interface OverlaySettings {
  fontSize: number;
  lineHeight: number;
  theme: "Dark" | "Light";
  opacity: number;
  showTranslation: boolean;
}

interface TranscriptLine {
  id: string;
  text: string;
  translatedText?: string;
  timestamp: number;
}

declare global {
  interface Window {
    overlayAPI: {
      getSettings: () => Promise<OverlaySettings>;
      onTranscriptLine: (cb: (line: TranscriptLine) => void) => () => void;
      onPartialUpdate: (cb: (data: { text: string; translatedText?: string }) => void) => () => void;
      onSettingsUpdate: (cb: (settings: Partial<OverlaySettings>) => void) => () => void;
      onTranslationUpdate: (cb: (data: { id: string; translatedText: string }) => void) => () => void;
      onClear: (cb: () => void) => () => void;
      close: () => void;
    };
  }
}

const MAX_LINES = 50;

export function OverlayApp() {
  const [settings, setSettings] = useState<OverlaySettings>({
    fontSize: 26,
    lineHeight: 1.35,
    theme: "Dark",
    opacity: 0.88,
    showTranslation: true,
  });
  const [lines, setLines] = useState<TranscriptLine[]>([]);
  const [partial, setPartial] = useState<{ text: string; translatedText?: string } | null>(null);
  const [pendingTranslation, setPendingTranslation] = useState<Set<string>>(new Set());
  const [isAtBottom, setIsAtBottom] = useState(true);
  const scrollRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    window.overlayAPI.getSettings().then((s) => setSettings(s));
  }, []);

  useEffect(() => {
    const unsubs = [
      window.overlayAPI.onTranscriptLine((line) => {
        setLines((prev) => {
          const next = [...prev, line];
          return next.length > MAX_LINES ? next.slice(-MAX_LINES) : next;
        });
        setPartial(null);
        if (!line.translatedText) {
          setPendingTranslation((prev) => new Set(prev).add(line.id));
        }
      }),
      window.overlayAPI.onPartialUpdate((data) => {
        setPartial(data.text ? data : null);
      }),
      window.overlayAPI.onTranslationUpdate((data) => {
        setLines((prev) =>
          prev.map((line) =>
            line.id === data.id ? { ...line, translatedText: data.translatedText } : line,
          ),
        );
        setPendingTranslation((prev) => {
          const next = new Set(prev);
          next.delete(data.id);
          return next;
        });
      }),
      window.overlayAPI.onSettingsUpdate((s) => {
        setSettings((prev) => ({ ...prev, ...s }));
      }),
      window.overlayAPI.onClear(() => {
        setLines([]);
        setPartial(null);
        setPendingTranslation(new Set());
      }),
    ];
    return () => unsubs.forEach((fn) => fn());
  }, []);

  useEffect(() => {
    if (isAtBottom) {
      bottomRef.current?.scrollIntoView({ behavior: "smooth" });
    }
  }, [lines, partial, isAtBottom]);

  const handleScroll = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return;
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 40;
    setIsAtBottom(atBottom);
  }, []);

  const jumpToBottom = () => {
    setIsAtBottom(true);
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  const isDark = settings.theme === "Dark";
  const bgColor = isDark
    ? `rgba(14, 19, 28, ${settings.opacity})`
    : `rgba(245, 247, 250, ${settings.opacity})`;
  const textColor = isDark ? "text-white" : "text-gray-900";
  const mutedColor = isDark ? "text-white/60" : "text-gray-500";
  const borderColor = isDark ? "border-white/10" : "border-gray-200/60";

  const isEmpty = lines.length === 0 && !partial;

  return (
    <div
      className="h-screen w-screen flex flex-col rounded-lg overflow-hidden"
      style={{ backgroundColor: bgColor }}
    >
      {/* Drag handle */}
      <div
        className={`flex items-center justify-between px-3 h-7 shrink-0 ${borderColor} border-b`}
        style={{ WebkitAppRegion: "drag" } as React.CSSProperties}
      >
        <div className={`text-[10px] ${mutedColor} select-none`}>Sublingual Overlay</div>
        <button
          className={`${mutedColor} hover:${textColor} text-xs w-5 h-5 flex items-center justify-center rounded hover:bg-white/10 transition-colors`}
          style={{ WebkitAppRegion: "no-drag" } as React.CSSProperties}
          onClick={() => window.overlayAPI.close()}
        >
          ✕
        </button>
      </div>

      {/* Content */}
      <div
        ref={scrollRef}
        className="flex-1 overflow-y-auto px-4 py-3"
        onScroll={handleScroll}
      >
        {isEmpty && (
          <div className={`flex items-center justify-center h-full ${mutedColor} text-sm`}>
            Waiting for speech...
          </div>
        )}

        {lines.map((line) => (
          <div key={line.id} className={`mb-3 ${borderColor} border-b pb-3 last:border-b-0`}>
            <p
              className={`${textColor} font-medium`}
              style={{ fontSize: settings.fontSize, lineHeight: settings.lineHeight }}
            >
              {line.text}
            </p>
            {settings.showTranslation && line.translatedText ? (
              <p
                className={`${mutedColor} mt-0.5`}
                style={{
                  fontSize: Math.max(14, settings.fontSize - 4),
                  lineHeight: settings.lineHeight,
                }}
              >
                {line.translatedText}
              </p>
            ) : settings.showTranslation && pendingTranslation.has(line.id) ? (
              <p
                className={`${mutedColor} mt-0.5 animate-pulse`}
                style={{
                  fontSize: Math.max(14, settings.fontSize - 4),
                  lineHeight: settings.lineHeight,
                }}
              >
                ···
              </p>
            ) : null}
          </div>
        ))}

        {partial && (
          <div className="mb-3">
            <p
              className={`${textColor} font-medium italic opacity-70`}
              style={{ fontSize: settings.fontSize, lineHeight: settings.lineHeight }}
            >
              {partial.text}
            </p>
            {settings.showTranslation && partial.translatedText && (
              <p
                className={`${mutedColor} mt-0.5 italic opacity-70`}
                style={{
                  fontSize: Math.max(14, settings.fontSize - 4),
                  lineHeight: settings.lineHeight,
                }}
              >
                {partial.translatedText}
              </p>
            )}
          </div>
        )}

        <div ref={bottomRef} />
      </div>

      {/* Jump to bottom */}
      {!isAtBottom && (
        <button
          className={`absolute bottom-3 right-3 w-8 h-8 rounded-full flex items-center justify-center text-sm ${
            isDark ? "bg-white/20 text-white hover:bg-white/30" : "bg-gray-300/60 text-gray-700 hover:bg-gray-300/80"
          } transition-colors`}
          onClick={jumpToBottom}
        >
          ↓
        </button>
      )}
    </div>
  );
}
