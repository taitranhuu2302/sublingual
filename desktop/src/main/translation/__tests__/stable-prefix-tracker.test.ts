import { describe, it, expect } from "vitest";
import { StablePrefixTracker } from "../stable-prefix-tracker";

describe("StablePrefixTracker", () => {
  describe("processPartial", () => {
    it("returns isReady false with insufficient history", () => {
      const tracker = new StablePrefixTracker({ stablePartialCount: 3 });

      const first = tracker.processPartial("hello world");
      expect(first.isReady).toBe(false);
      expect(first.committedWordCount).toBe(0);

      const second = tracker.processPartial("hello world test");
      expect(second.isReady).toBe(false);
      expect(second.committedWordCount).toBe(0);
    });

    it("returns isReady true once history is full and prefix is stable", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 2,
        holdBackWords: 1,
        minChunkWords: 1,
      });

      tracker.processPartial("hello world foo");
      const result = tracker.processPartial("hello world bar");

      expect(result.isReady).toBe(true);
      // Common prefix: ["hello", "world"] (length 2), minus holdBack=1 → 1
      expect(result.committedWordCount).toBe(1);
    });

    it("returns tokenized words array", () => {
      const tracker = new StablePrefixTracker({ stablePartialCount: 1 });

      const result = tracker.processPartial("hello world");
      expect(result.words).toEqual(["hello", "world"]);
    });

    it("detects longest common prefix across all history entries", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 3,
        holdBackWords: 1,
        minChunkWords: 2,
      });

      tracker.processPartial("the quick brown fox");
      tracker.processPartial("the quick brown rabbit");
      const result = tracker.processPartial("the quick brown bear");

      // Common prefix: ["the", "quick", "brown"] (length 3), minus holdBack=1 → 2
      expect(result.committedWordCount).toBe(2);
      expect(result.isReady).toBe(true);
    });

    it("respects minChunkWords threshold", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 2,
        holdBackWords: 2,
        minChunkWords: 5,
      });

      tracker.processPartial("one two three four five six");
      const result = tracker.processPartial("one two three four five seven");

      // Common prefix: ["one","two","three","four","five"] (length 5), minus holdBack=2 → 3
      // minChunkWords=5, so 3 < 5 → isReady false (unless timed out)
      expect(result.committedWordCount).toBe(3);
      expect(result.isReady).toBe(false);
    });

    it("forces ready when maxWaitMs elapsed", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 2,
        holdBackWords: 2,
        minChunkWords: 10,
        maxWaitMs: 100,
      });

      const now = 1000;
      tracker.markCommitted(500); // 500ms ago

      tracker.processPartial("a b c d e");
      const result = tracker.processPartial("a b c d f", now);

      // Common prefix: ["a","b","c","d"] (length 4), minus holdBack=2 → 2
      // minChunkWords=10 not met, but elapsed=500ms > maxWaitMs=100 → isReady true
      expect(result.committedWordCount).toBe(2);
      expect(result.isReady).toBe(true);
    });

    it("handles empty text", () => {
      const tracker = new StablePrefixTracker({ stablePartialCount: 1 });

      const result = tracker.processPartial("");

      expect(result.words).toEqual([]);
      expect(result.committedWordCount).toBe(0);
      expect(result.isReady).toBe(false);
    });

    it("handles holdBackWords >= stable prefix length", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 2,
        holdBackWords: 10,
        minChunkWords: 1,
      });

      tracker.processPartial("hello world");
      const result = tracker.processPartial("hello world");

      // Common prefix: ["hello", "world"] (length 2), minus holdBack=10 → 0
      expect(result.committedWordCount).toBe(0);
    });

    it("when stablePartialCount <= 1, always considers first array as stable", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 1,
        holdBackWords: 0,
        minChunkWords: 1,
      });

      const result = tracker.processPartial("hello world");
      expect(result.isReady).toBe(true);
      expect(result.committedWordCount).toBe(2);
    });

    it("uses defaults when no config provided", () => {
      const tracker = new StablePrefixTracker();

      // Defaults: stablePartialCount=2, holdBackWords=2, minChunkWords=3, maxWaitMs=600
      expect(tracker).toBeDefined();
    });

    it("uses Date.now() when now is not provided", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 1,
        holdBackWords: 0,
        minChunkWords: 1,
      });

      const result = tracker.processPartial("hello world");
      expect(result.isReady).toBe(true);
    });

    it("correctly reports latest words regardless of committed count", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 2,
        holdBackWords: 1,
        minChunkWords: 1,
      });

      tracker.processPartial("hello world test one");
      const result = tracker.processPartial("hello world test two");

      // Words are from last partial
      expect(result.words).toEqual(["hello", "world", "test", "two"]);
    });

    it("handles partials with trailing/leading whitespace", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 1,
        holdBackWords: 0,
        minChunkWords: 1,
      });

      const result = tracker.processPartial("  hello   world  ");
      expect(result.words).toEqual(["hello", "world"]);
    });

    it("tracks only last stablePartialCount entries in history", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 2,
        holdBackWords: 0,
        minChunkWords: 2,
      });

      tracker.processPartial("alpha beta gamma");
      tracker.processPartial("alpha beta delta");
      // Third call: history should only keep the last 2 entries, so
      // history[0] = ["alpha", "beta", "delta"], history[1] = ["alpha", "beta", "epsilon"]
      // Common prefix: ["alpha", "beta"] (length 2)
      const result = tracker.processPartial("alpha beta epsilon");

      expect(result.committedWordCount).toBe(2);
      expect(result.isReady).toBe(true);
    });
  });

  describe("reset", () => {
    it("clears history and resets lastCommitAt", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 2,
        holdBackWords: 0,
        minChunkWords: 1,
      });

      tracker.processPartial("hello world");
      tracker.processPartial("hello world");
      tracker.reset();

      // After reset, history is empty, so not enough history
      const result = tracker.processPartial("hello world");
      expect(result.isReady).toBe(false);
    });

    it("restores default state after reset", () => {
      const tracker = new StablePrefixTracker({
        holdBackWords: 0,
        minChunkWords: 1,
      });

      tracker.processPartial("first call");
      tracker.processPartial("second call");
      tracker.reset();

      // Start fresh
      const first = tracker.processPartial("hello world");
      expect(first.isReady).toBe(false);
      expect(first.committedWordCount).toBe(0);

      const second = tracker.processPartial("hello world");
      expect(second.isReady).toBe(true);
    });
  });

  describe("markCommitted", () => {
    it("updates lastCommitAt with provided timestamp", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 1,
        holdBackWords: 0,
        minChunkWords: 1,
        maxWaitMs: 500,
      });

      const now = 1000;
      tracker.markCommitted(now);

      // Immediately after markCommitted, time since commit is 0
      const result = tracker.processPartial("hello world", now);
      expect(result.isReady).toBe(true); // minChunkWords met
    });

    it("does not fire timeout when lastCommitAt is 0 (never committed)", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 2,
        holdBackWords: 0,
        minChunkWords: 10,
        maxWaitMs: 100,
      });

      // lastCommitAt = 0, so timeout check is skipped
      tracker.processPartial("a b c d e");
      const result = tracker.processPartial("a b c d f");
      expect(result.isReady).toBe(false); // not enough words, no timeout
    });

    it("affects maxWaitMs timeout calculation", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 2,
        holdBackWords: 2,
        minChunkWords: 10,
        maxWaitMs: 200,
      });

      // Mark committed at t=100 so timeout check fires starting at t=300
      tracker.markCommitted(100);

      tracker.processPartial("a b c d e f");
      // now=200, elapsed=100ms < maxWaitMs=200, minChunkWords not met
      const resultEarly = tracker.processPartial("a b c d e g", 200);
      expect(resultEarly.isReady).toBe(false);

      // now=400, elapsed=300ms > maxWaitMs=200 → force ready
      const resultLate = tracker.processPartial("a b c d e g", 400);
      expect(resultLate.isReady).toBe(true);
    });

    it("uses Date.now() when no argument provided", () => {
      const tracker = new StablePrefixTracker({
        stablePartialCount: 1,
        holdBackWords: 0,
        minChunkWords: 1,
      });

      // Should not throw
      expect(() => tracker.markCommitted()).not.toThrow();
    });
  });
});
