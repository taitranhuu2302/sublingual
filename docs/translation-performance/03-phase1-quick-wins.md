# Phase 1: Quick Wins

Những thay đổi nhỏ, ít rủi ro, impact lớn. Có thể implement trong 1-2 ngày.

---

## 1.1 Tăng `MIN_REALTIME_CHARS` (8 → 15-20)

- **File:** `translate-service/app/config.py:26`
- **Current:** `min_realtime_chars: int = 8`
- **Proposed:** `min_realtime_chars: int = 15`
- **Lý do:** Partial text < 15 chars (VD: "hello", "how", "I am") thường không đủ context để translate có ý nghĩa. Skip sớm giảm request.
- **Impact:** Giảm ~40-50% draft translation requests.

### Cập nhật đồng bộ
- `translate-service/.env`: `MIN_REALTIME_CHARS=15`
- Check C# scheduler: nếu có min-delta check ở C# side, update theo

---

## 1.2 Tăng Draft Debounce (300ms → 500ms)

- **File:** `src/Sublingual.App/Services/RealtimeTranslationScheduler.cs:12`
- **Current:** `private static readonly TimeSpan DraftDebounce = TimeSpan.FromMilliseconds(300);`
- **Proposed:** `private static readonly TimeSpan DraftDebounce = TimeSpan.FromMilliseconds(500);`
- **Lý do:** 300ms quá ngắn so với thời gian gõ/phát âm một từ. 500ms giúp accumulate thêm text trước khi gửi translate.
- **Impact:** Giảm ~40% draft requests, cải thiện chất lượng draft translation (vì text dài hơn).

---

## 1.3 Disable Draft Translation Mặc Định

- **File:** Models/AppSettings.cs (tìm `TranslatePartials` default)
- **Current:** `TranslatePartials` có thể đang default `true`
- **Proposed:** Default `false`
- **Lý do:** Nếu người dùng không cần partial preview, chỉ translate khi Vosk final → giảm 80-90% translation requests.
- **Impact:** Lớn nhất trong quick wins. Translation chỉ xảy ra cho stable segments.

### UX Note
Vẫn có thể bật lại trong Settings cho người dùng muốn realtime preview.

---

## 1.4 Thêm VAD Check Trước Khi Gọi Vosk

- **File:** `src/Sublingual.Infrastructure/Audio/Processing/FixedWindowAudioChunkProcessor.cs`
- **Proposed:** Add energy-based VAD check:
  ```csharp
  if (!HasVoiceActivity(chunk.Data, threshold: 0.01))
      continue; // skip silence
  ```
- **Implement:** Energy-based (RMS) threshold — đơn giản, chỉ cần 20 dòng code.
- **Impact:** Giảm ~50% audio chunks vào Vosk cho conversation thông thường.

### Reference implementation
```csharp
private static bool HasVoiceActivity(byte[] audioData, float threshold)
{
    int sampleCount = audioData.Length / 2; // 16-bit PCM
    long sumSquares = 0;
    for (int i = 0; i < sampleCount; i++)
    {
        short sample = BitConverter.ToInt16(audioData, i * 2);
        sumSquares += sample * sample;
    }
    float rms = (float)Math.Sqrt(sumSquares / (double)sampleCount);
    return rms > threshold * short.MaxValue;
}
```

---

## 1.5 Thêm Capacity Limit Cho Stable Queue

- **File:** `src/Sublingual.App/Services/RealtimeTranslationScheduler.cs:15-16`
- **Current:**
  ```csharp
  private readonly ConcurrentQueue<StableTranslationRequest> _stableQueue = new();
  ```
- **Proposed:**
  ```csharp
  private const int MaxStableQueue = 100;
  private readonly ConcurrentQueue<StableTranslationRequest> _stableQueue = new();
  // In EnqueueStable: if queue.Count >= MaxStableQueue → drop oldest or skip
  ```
- **Lý do:** Tránh memory leak nếu translation chậm hơn STT.
- **Impact:** Ngăn unbounded growth, predictable memory.

---

## 1.6 Cải Thiện Cache Key

- **File:** `src/Sublingual.App/Services/Translation/ConfigurableTranslationService.cs:216-225`
- **Proposed:** Normalize thêm lowercase + strip punctuation:
  ```csharp
  private static string NormalizeCacheToken(string? value)
  {
      if (string.IsNullOrWhiteSpace(value)) return string.Empty;
      var normalized = string.Join(' ', value
          .ToLowerInvariant()
          .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
      return normalized.TrimEnd('.', ',', '!', '?', ';', ':');
  }
  ```
- **Impact:** Cache hit rate cao hơn cho partial text tương tự nhau (VD: "Hello" vs "hello,").

---

## 1.7 Thêm `MIN_REALTIME_CHARS` Check Ở C# Side

- **File:** `src/Sublingual.App/Services/RealtimeTranslationScheduler.cs`
- **Proposed:** Check text length trước khi enqueue draft, tránh HTTP round-trip vô ích:
  ```csharp
  // Trong EnqueueDraft
  if (request.SourceText.Length < 15) return; // skip short partials
  ```
- **Impact:** Giảm HTTP requests, tận dụng skip logic sớm.
- **Note:** Giá trị 15 nên sync với `MIN_REALTIME_CHARS` ở Python side.

---

## 1.8 Chuyển Draft Debounce thành Adaptive

- **File:** `src/Sublingual.App/Services/RealtimeTranslationScheduler.cs`
- **Proposed:** Điều chỉnh debounce dựa trên translation latency gần đây:
  ```csharp
  // Estimate từ recent translation latency
  // Nếu latency avg > 200ms → tăng debounce
  var dynamicDebounce = Math.Min(500, Math.Max(200, _recentAvgLatencyMs));
  await Task.Delay(dynamicDebounce, cancellationToken);
  ```
- **Impact:** Tự động điều chỉnh theo tốc độ translation hiện tại.

---

## Tổng Kết Impact Phase 1

| # | Change | Effort | Impact |
|---|---|---|---|
| 1.1 | Tăng `MIN_REALTIME_CHARS` 8→15 | 5 phút | Medium (↓40% draft) |
| 1.2 | Tăng debounce 300→500ms | 5 phút | Medium (↓40% draft) |
| 1.3 | Disable draft default | 5 phút | High (↓90% requests) |
| 1.4 | VAD energy check | 2 giờ | High (↓50% chunks) |
| 1.5 | Stable queue cap | 30 phút | Medium (memory safety) |
| 1.6 | Cache key normalization | 15 phút | Low-Medium (cache hit) |
| 1.7 | C# min chars check | 5 phút | Low (skip sớm) |
| 1.8 | Adaptive debounce | 1 giờ | Medium (tự điều chỉnh) |

**Total effort: ~4-5 giờ**
