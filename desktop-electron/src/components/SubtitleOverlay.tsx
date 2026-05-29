import type { TranscriptEntry } from "../hooks/use-transcription";

interface Props {
  segments: TranscriptEntry[];
}

export function SubtitleOverlay({ segments }: Props) {
  const recent = segments.slice(-5); // show last 5 segments

  return (
    <div className="fixed bottom-8 left-1/2 -translate-x-1/2 w-[80%] max-w-2xl">
      <div className="bg-black/80 rounded-lg px-6 py-4 space-y-1">
        {recent.length === 0 && (
          <p className="text-white/50 text-center text-sm">Waiting for speech...</p>
        )}
        {recent.map((seg, i) => (
          <p
            key={i}
            className={`text-white text-lg text-center ${!seg.isFinal ? "opacity-60 italic" : ""}`}
          >
            {seg.text}
          </p>
        ))}
      </div>
    </div>
  );
}
