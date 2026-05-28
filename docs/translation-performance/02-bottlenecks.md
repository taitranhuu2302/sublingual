# Bottlenecks

Dưới đây là các bottleneck đã xác định, sắp xếp theo mức độ ảnh hưởng.

---

## B1. `_pipelineGate` Serializes Audio + STT

- **File:** `src/Sublingual.App/Services/AudioCaptureDebugSession.cs:25`
- **Code:** `private readonly SemaphoreSlim _pipelineGate = new(1, 1);`
- **Vấn đề:** Mọi chunk audio phải chờ nhau qua STT (Vosk) vì semaphore chỉ cho 1 luồng.
- **Impact:** Nếu Vosk chậm (VD: model lớn, CPU weak), toàn bộ capture pipeline bị block.
- **Note:** Translation **đã được** decoupled ra khỏi gate qua `RealtimeTranslationScheduler`, nhưng STT vẫn ở trong gate.

### Root cause
`ProcessChunkAsync` (line 218-251) giữ `_pipelineGate` trong suốt vòng lặp:
1. `_processAudioChunkUseCase.Execute(inputChunk)` — process audio
2. `ProcessTranscriptAsync(chunk)` — normalize → Vosk → publish events → schedule translation

Translation scheduling (`ScheduleTranslation`) chỉ enqueue vào `RealtimeTranslationScheduler`, không await, nên không phải bottleneck ở đây. Nhưng Vosk `TranscribeAsync` là blocking.

---

## B2. Thiếu Voice Activity Detection (VAD)

- **File:** `src/Sublingual.Infrastructure/Audio/Processing/FixedWindowAudioChunkProcessor.cs`
- **Vấn đề:** Fixed-window chunker gửi mọi audio (kể cả silence) vào Vosk.
- **Impact:**
  - Wasted CPU: silence vẫn được normalize + gửi vào Vosk
  - Wasted translation: Vosk sinh partial text từ noise
  - Tăng số chunk không cần thiết

### Ước lượng
Trong conversation thông thường, silence chiếm 40-60% thời gian. Nếu cắt silence, giảm được ~50% chunk vào Vosk.

---

## B3. Sync Python Endpoints Block Worker Threads

- **File:** `translate-service/app/main.py:232`, `265`, `305`
- **Code pattern:**
  ```python
  def translate(request: TranslateRequest) -> TranslateResponse:  # sync def
      translator = model_manager.get_translator(...)
      translated_text = translator.translate(source_text)  # blocking I/O
  ```
- **Vấn đề:** FastAPI endpoints dùng `def` (sync), không `async def`. Khi CTranslate2 inference, worker thread bị block.
- **Impact:** Dưới tải nhiều request, uvicorn workers bị saturated.
- **Note:** Với single user (1 session), impact thấp. Nhưng stable + draft request có thể xảy ra đồng thời.

---

## B4. HTTP Overhead Cho Mỗi Request

- **File:** `src/Sublingual.App/Services/Translation/TranslateServiceLocalTranslationProvider.cs:49-110`
- **Vấn đề:** Mỗi partial/final text là một HTTP POST riêng với JSON serialization.
- **Impact:**
  - Connection overhead (dù keep-alive, vẫn tốn ~1ms mỗi request)
  - JSON serialize/deserialize (~0.5-1ms)
  - Python request parsing + validation (~1ms)
- **Tổng overhead:** ~2-5ms mỗi request, chưa kể inference.

### Frequency estimate
Với chunk 350ms, TranslatePartials=true:
- Draft: ~2-3 requests/second
- Stable: ~1 request mỗi 2-3 giây
→ ~3-6 requests/second. Overhead ~10-30ms/s CPU.

---

## B5. Stable Queue Tuần Tự (Single Worker)

- **File:** `src/Sublingual.App/Services/RealtimeTranslationScheduler.cs:161-192`
- **Code:**
  ```csharp
  private async Task ProcessStableQueueAsync()
  {
      while (!_disposeCts.IsCancellationRequested)
      {
          await _stableSignal.WaitAsync(_disposeCts.Token);
          while (_stableQueue.TryDequeue(out var request))
          {
              var result = await TranslateAsync(...);  // tuần tự
          }
      }
  }
  ```
- **Vấn đề:** Một stable translation chậm sẽ block tất cả stable khác.
- **Impact:** Nếu CTranslate2 inference chậm (VD: câu dài 50 tokens), queue tích tụ. Overlay UI thấy stable text bị delay.
- **Không có capacity limit** → queue có thể grow infinite.

---

## B6. Draft Debounce Cố Định (300ms)

- **File:** `src/Sublingual.App/Services/RealtimeTranslationScheduler.cs:12`
- **Code:** `private static readonly TimeSpan DraftDebounce = TimeSpan.FromMilliseconds(300);`
- **Vấn đề:** 300ms là hardcode, không adaptive theo network/translation latency.
- **Impact:**
  - Nếu translation latency > 300ms, draft request chồng lên nhau
  - Nếu speech chậm, vẫn phải chờ 300ms

---

## B7. Tokenizer Overhead Trên Text Nhỏ

- **File:** `translate-service/app/translator/marian_ct2.py:45-50`
- **Vấn đề:** `MarianTokenizer` được gọi cho mỗi request, kể cả text rất ngắn (2-3 từ).
- **Impact:** Tokenizer overhead có thể chiếm 30-50% thời gian xử lý cho partial text ngắn.

### Ví dụ
```
Input: "hello" (5 chars)
Tokenizer: ~2ms
CTranslate2: ~3ms
→ Overhead tokenizer = 40%
```

---

## B8. Thiếu Context → Quality Thấp

- **File:** `translate-service/app/translator/marian_ct2.py:32-34`
- **Vấn đề:** Mỗi câu được dịch độc lập, không biết câu trước/sau.
- **Impact:**
  - Dịch "It" → "Nó" thay vì "Cái này" (tùy context)
  - Mất consistency: cùng thuật ngữ dịch khác nhau giữa các câu
  - Model không đủ mạnh (Marian nhỏ, beam_size=1)

### Root causes của "không translate hết content"
1. **Beam size = 1**: greedy decoding, dễ miss optimal translation
2. **Không context**: mất tham chiếu (pronoun, discourse)
3. **Model size**: Marian base model (khoảng 300-400M params) khá nhỏ so với SOTA
4. **Không fine-tune**: model pre-trained trên general text, không tối ưu cho conversation/subtitle

---

## B9. Cache Key Không Chuẩn Hóa

- **File:** `src/Sublingual.App/Services/Translation/ConfigurableTranslationService.cs:216-225`
- **Code:**
  ```csharp
  private static string BuildCacheKey(TranslationRequest request)
  {
      return string.Concat(
          NormalizeCacheToken(request.SourceLanguage), '|',
          NormalizeCacheToken(request.TargetLanguage), '|',
          NormalizeCacheToken(request.SourceText)
      );
  }
  ```
- **Vấn đề:** Cache key normalize whitespace nhưng không normalize case, punctuation khác nhau → miss cache cho partial text gần giống nhau.
- **Impact:** Với Vosk partial text (thường thay đổi 1-2 từ), cache hit rate thấp.
