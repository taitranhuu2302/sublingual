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
  speakerLabel?: string;
  speakerColor?: string;
}

const MAX_LINES = 50;
const PARTIAL_ID = "__partial__";

export function OverlayApp() {
  const [settings, setSettings] = useState<OverlaySettings>({
    fontSize: 26,
    lineHeight: 1.35,
    theme: "Dark",
    opacity: 0.88,
    showTranslation: true,
  });
  const [lines, setLines] = useState<TranscriptLine[]>([]);
  const [partialTranslation, setPartialTranslation] = useState<string | null>(null);
  const [pendingTranslation, setPendingTranslation] = useState<Set<string>>(new Set());
  const [isAtBottom, setIsAtBottom] = useState(true);
  const scrollRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  const addCommittedLine = useCallback((line: TranscriptLine) => {
    setLines((prev) => {
      const next = prev.filter((l) => l.id !== PARTIAL_ID);
      next.push(line);
      return next.length > MAX_LINES ? next.slice(-MAX_LINES) : next;
    });
    if (!line.translatedText) {
      setPendingTranslation((prev) => new Set(prev).add(line.id));
    }
  }, []);

  const updatePartial = useCallback((text: string) => {
    setLines((prev) => {
      const next = prev.filter((l) => l.id !== PARTIAL_ID);
      next.push({ id: PARTIAL_ID, text, timestamp: Date.now() });
      return next.length > MAX_LINES ? next.slice(-MAX_LINES) : next;
    });
  }, []);

  useEffect(() => {
    window.overlayAPI.getSettings().then((s) => setSettings(s));
  }, []);

  useEffect(() => {
    const unsubs = [
      window.overlayAPI.onTranscriptLine((line) => {
        addCommittedLine(line);
        setPartialTranslation(null);
      }),
      window.overlayAPI.onPartialUpdate((data) => {
        if (data.text) updatePartial(data.text);
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
      window.overlayAPI.onTranslationCommitted((data) => {
        setPartialTranslation(data.text || null);
      }),
      window.overlayAPI.onSettingsUpdate((s) => {
        setSettings((prev) => ({ ...prev, ...s }));
      }),
      window.overlayAPI.onClear(() => {
        setLines([]);
        setPartialTranslation(null);
        setPendingTranslation(new Set());
      }),
    ];
    return () => unsubs.forEach((fn) => fn());
  }, [addCommittedLine, updatePartial]);

  useEffect(() => {
    if (isAtBottom) {
      bottomRef.current?.scrollIntoView({ behavior: "auto" });
    }
  }, [lines, isAtBottom]);

  const handleScroll = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return;
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 40;
    setIsAtBottom(atBottom);
  }, []);

  const jumpToBottom = () => {
    setIsAtBottom(true);
    bottomRef.current?.scrollIntoView({ behavior: "auto" });
  };

  const bgOpacity = settings.opacity;
  const isEmpty = lines.length === 0;

  return (
    <div
      className="h-screen w-screen flex flex-col rounded-xl overflow-hidden"
      style={{
        background: `hsla(232, 23%, 18%, ${bgOpacity})`,
        backdropFilter: "blur(24px)",
        WebkitBackdropFilter: "blur(24px)",
        border: "1px solid hsl(234, 19%, 26% / 0.6)",
        boxShadow: "0 4px 16px hsla(0, 0%, 0%, 0.16)",
      }}
    >
      {/* Drag handle */}
      <div
        className="flex items-center justify-between px-3 h-7 shrink-0 border-b border-[hsl(234,19%,26%)]/40"
        style={{ WebkitAppRegion: "drag" } as React.CSSProperties}
      >
        <div className="text-[10px] text-foreground/30 select-none">NERIS Sublingual</div>
        <button
          className="text-foreground/30 hover:text-foreground text-xs w-5 h-5 flex items-center justify-center rounded hover:bg-white/10 transition-colors"
          style={{ WebkitAppRegion: "no-drag" } as React.CSSProperties}
          onClick={() => window.overlayAPI.close()}
        >
          &#x2715;
        </button>
      </div>

      {/* Content */}
      <div
        ref={scrollRef}
        className="flex-1 overflow-y-auto px-4 py-3"
        onScroll={handleScroll}
      >
        {isEmpty && (
          <div className="flex items-center justify-center h-full text-muted-foreground/60 text-sm">
            Waiting for speech...
          </div>
        )}

        {lines.map((line) => {
          const isPartial = line.id === PARTIAL_ID;
          return (
            <div
              key={line.id}
              className={`mb-3 ${!isPartial ? "border-b border-border/30 pb-3 last:border-b-0" : ""}`}
            >
              <p
                className="text-foreground font-medium"
                style={{ fontSize: settings.fontSize, lineHeight: settings.lineHeight }}
              >
                {line.speakerLabel && (
                  <span
                    className="inline-flex items-center gap-1 mr-2 text-xs font-semibold rounded px-1.5 py-0.5 align-middle"
                    style={{
                      backgroundColor: `${line.speakerColor}22`,
                      color: line.speakerColor,
                      border: `1px solid ${line.speakerColor}44`,
                    }}
                  >
                    {line.speakerLabel}
                  </span>
                )}
                {line.text}
              </p>
              {settings.showTranslation && !isPartial && line.translatedText ? (
                <p
                  className="text-muted-foreground mt-0.5"
                  style={{
                    fontSize: Math.max(14, settings.fontSize - 4),
                    lineHeight: settings.lineHeight,
                  }}
                >
                  {line.translatedText}
                </p>
              ) : settings.showTranslation && !isPartial && pendingTranslation.has(line.id) ? (
                <p
                  className="text-muted-foreground/60 mt-0.5 animate-pulse"
                  style={{
                    fontSize: Math.max(14, settings.fontSize - 4),
                    lineHeight: settings.lineHeight,
                  }}
                >
                  ···
                </p>
              ) : null}
              {settings.showTranslation && isPartial && partialTranslation ? (
                <p
                  className="text-muted-foreground mt-0.5"
                  style={{
                    fontSize: Math.max(14, settings.fontSize - 4),
                    lineHeight: settings.lineHeight,
                  }}
                >
                  {partialTranslation}
                </p>
              ) : settings.showTranslation && isPartial ? (
                <p
                  className="text-muted-foreground/60 mt-0.5 animate-pulse"
                  style={{
                    fontSize: Math.max(14, settings.fontSize - 4),
                    lineHeight: settings.lineHeight,
                  }}
                >
                  ···
                </p>
              ) : null}
            </div>
          );
        })}

        <div ref={bottomRef} />
      </div>

      {/* Jump to bottom */}
      {!isAtBottom && (
        <button
          className="absolute bottom-3 right-3 w-8 h-8 rounded-full flex items-center justify-center text-sm bg-foreground/10 text-foreground/70 hover:bg-foreground/20 hover:text-foreground transition-colors"
          onClick={jumpToBottom}
        >
          &#x2193;
        </button>
      )}
    </div>
  );
}
