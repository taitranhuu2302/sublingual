import { describe, it, expect, vi, beforeEach } from "vitest";
import { IncrementalTranslationManager } from "../incremental-translation-manager";
import type { CommittedChunkEvent, FinalizedEvent } from "../incremental-translation-manager";

import * as settingsStore from "../../settings/settings-store";

vi.mock("../../settings/settings-store");

const mockTranslateFn = vi.fn<(...args: unknown[]) => Promise<{ translatedText: string; providerName: string; durationMs: number }>>();

vi.mock("../translation-service", () => ({
  getTranslationService: () => ({
    translate: mockTranslateFn,
  }),
}));

function mockSettings(overrides: Partial<{
  enabled: boolean;
  srcLang: string;
  tgtLang: string;
}> = {}) {
  const { enabled = true, srcLang = "en", tgtLang = "vi" } = overrides;
  vi.mocked(settingsStore.getSettings).mockReturnValue({
    speechToText: { sourceLanguage: srcLang, selectedModel: "" },
    translation: {
      enabled,
      provider: "google-free",
      targetLanguage: tgtLang,
      google: { endpoint: "" },
      local: { baseUrl: "" },
    },
    storage: { sessionsRoot: "", speechToTextModelsRoot: "" },
    overlay: {
      fontSize: 26,
      lineHeight: 1.35,
      width: 720,
      height: 200,
      theme: "Dark",
      opacity: 0.88,
      showTranslation: true,
      positionX: null,
      positionY: null,
    },
  } as ReturnType<typeof settingsStore.getSettings>);
}

describe("IncrementalTranslationManager", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSettings();
    mockTranslateFn.mockResolvedValue({
      translatedText: "",
      providerName: "test",
      durationMs: 0,
    });
  });

  describe("constructor", () => {
    it("sets utteranceId", () => {
      const m = new IncrementalTranslationManager("utt-1");
      expect(m.utteranceId).toBe("utt-1");
    });
  });

  describe("handlePartial", () => {
    it("does nothing when isReady is false (not enough history)", () => {
      const m = new IncrementalTranslationManager("test");
      m.onCommit = vi.fn();

      m.handlePartial("hello world");

      expect(mockTranslateFn).not.toHaveBeenCalled();
    });

    it("does nothing when isReady is false (not enough stable words)", () => {
      const m = new IncrementalTranslationManager("test");
      m.onCommit = vi.fn();

      m.handlePartial("hello");
      m.handlePartial("hello");

      // 1 word, committedWordCount = max(0, 1-2) = 0 < minChunkWords=3 → isReady=false
      expect(mockTranslateFn).not.toHaveBeenCalled();
    });

    it("translates new stable chunk and fires onCommit", async () => {
      mockTranslateFn.mockResolvedValue({
        translatedText: "mot hai ba",
        providerName: "test",
        durationMs: 0,
      });

      const m = new IncrementalTranslationManager("test");
      const commitEvent = new Promise<void>((resolve) => {
        m.onCommit = (e: CommittedChunkEvent) => {
          expect(e.text).toBe("mot hai ba");
          expect(e.fullSource).toBe("one two three four five");
          expect(e.revision).toBe(1);
          resolve();
        };
      });

      m.handlePartial("one two three four five");
      m.handlePartial("one two three four five");

      await commitEvent;

      expect(mockTranslateFn).toHaveBeenCalledWith(
        "one two three",
        "en",
        "vi",
        { previousTarget: "" },
      );
    });

    it("does nothing when translation is disabled", () => {
      mockSettings({ enabled: false });

      const m = new IncrementalTranslationManager("test");
      m.onCommit = vi.fn();

      m.handlePartial("one two three four five");
      m.handlePartial("one two three four five");

      expect(mockTranslateFn).not.toHaveBeenCalled();
      expect(m.onCommit).not.toHaveBeenCalled();
    });

    it("discards translation result when revision is outdated", async () => {
      const { promise: hangPromise, resolve: hangResolve } = createControlledPromise();
      mockTranslateFn.mockImplementationOnce(() => hangPromise);
      mockTranslateFn.mockResolvedValue({
        translatedText: "sau",
        providerName: "test",
        durationMs: 0,
      });

      const events: CommittedChunkEvent[] = [];
      const m = new IncrementalTranslationManager("test");
      m.onCommit = (e) => {
        events.push(e);
      };

      // First commit: 3 words, revision=1, translate hangs
      m.handlePartial("one two three four five");
      m.handlePartial("one two three four five");
      await waitForMicrotasks();

      // Second commit: pushes to committedWordCount=4, revision=2
      m.handlePartial("one two three four five six");
      m.handlePartial("one two three four five six");
      await waitForMicrotasks();

      // Resolve the first (stale) translation
      hangResolve({
        translatedText: "stale",
        providerName: "test",
        durationMs: 0,
      });
      await waitForMicrotasks();

      expect(events).toHaveLength(1);
      expect(events[0].text).toBe("sau");
    });

    it("logs error when translate promise rejects", async () => {
      mockTranslateFn.mockRejectedValue(new Error("network failure"));
      const spy = vi.spyOn(console, "error").mockImplementation(() => {});

      const m = new IncrementalTranslationManager("test");
      m.onCommit = vi.fn();

      m.handlePartial("one two three four five");
      m.handlePartial("one two three four five");
      await waitForMicrotasks();

      expect(spy).toHaveBeenCalled();
      spy.mockRestore();
    });
  });

  describe("handleFinal", () => {
    it("translates remaining words and fires onFinalize", async () => {
      mockTranslateFn.mockResolvedValueOnce({
        translatedText: "mot hai ba",
        providerName: "test",
        durationMs: 0,
      });

      const m = new IncrementalTranslationManager("test");

      // Commit 3 words first
      m.handlePartial("one two three four five");
      m.handlePartial("one two three four five");
      await waitForMicrotasks();

      mockTranslateFn.mockResolvedValueOnce({
        translatedText: "bon nam",
        providerName: "test",
        durationMs: 0,
      });

      const finalEvent = new Promise<void>((resolve) => {
        m.onFinalize = (e: FinalizedEvent) => {
          expect(e.fullSource).toBe("one two three four five");
          expect(e.fullTranslation).toBe("mot hai ba bon nam");
          expect(e.revision).toBe(2);
          resolve();
        };
      });

      await m.handleFinal("one two three four five");
      await finalEvent;

      expect(mockTranslateFn).toHaveBeenCalledTimes(2);
      expect(mockTranslateFn).toHaveBeenLastCalledWith("four five", "en", "vi");
    });

    it("fires onFinalize with current committedTranslation when no remaining words", async () => {
      mockTranslateFn.mockResolvedValue({
        translatedText: "mot hai ba",
        providerName: "test",
        durationMs: 0,
      });

      const m = new IncrementalTranslationManager("test");

      m.handlePartial("one two three four five");
      m.handlePartial("one two three four five");
      await waitForMicrotasks();

      const finalEvent = new Promise<void>((resolve) => {
        m.onFinalize = (e: FinalizedEvent) => {
          expect(e.fullTranslation).toBe("mot hai ba");
          expect(e.fullSource).toBe("one two three");
          resolve();
        };
      });

      await m.handleFinal("one two three");
      await finalEvent;
    });

    it("fires onFinalize with empty translation when translation disabled", async () => {
      mockSettings({ enabled: false });

      const m = new IncrementalTranslationManager("test");

      const finalEvent = new Promise<void>((resolve) => {
        m.onFinalize = (e: FinalizedEvent) => {
          expect(e.fullTranslation).toBe("");
          expect(e.fullSource).toBe("hello world");
          resolve();
        };
      });

      await m.handleFinal("hello world");
      await finalEvent;

      expect(mockTranslateFn).not.toHaveBeenCalled();
    });

    it("calls reset after finalizing", async () => {
      mockTranslateFn.mockResolvedValue({
        translatedText: "x",
        providerName: "test",
        durationMs: 0,
      });

      const m = new IncrementalTranslationManager("test");

      m.handlePartial("one two three four five");
      m.handlePartial("one two three four five");
      await waitForMicrotasks();

      await m.handleFinal("one two three four five");

      expect((m as unknown as Record<string, unknown>).committedWordCount).toBe(0);
      expect((m as unknown as Record<string, unknown>).committedTranslation).toBe("");
    });
  });

  describe("resetUtteranceId", () => {
    it("updates utteranceId", () => {
      const m = new IncrementalTranslationManager("old");
      m.resetUtteranceId("new");
      expect(m.utteranceId).toBe("new");
    });
  });

  describe("reset", () => {
    it("resets tracker, committedWordCount, committedTranslation but not revision", async () => {
      mockTranslateFn.mockResolvedValue({
        translatedText: "x",
        providerName: "test",
        durationMs: 0,
      });

      const m = new IncrementalTranslationManager("test");

      // Trigger a commit so revision becomes 1
      m.handlePartial("one two three four five");
      m.handlePartial("one two three four five");
      await waitForMicrotasks();

      m.reset();

      expect((m as unknown as Record<string, unknown>).revision).toBe(1);
      expect((m as unknown as Record<string, unknown>).committedWordCount).toBe(0);
      expect((m as unknown as Record<string, unknown>).committedTranslation).toBe("");
    });
  });
});

function createControlledPromise<T = void>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function waitForMicrotasks() {
  return new Promise<void>((resolve) => {
    queueMicrotask(resolve);
  });
}
