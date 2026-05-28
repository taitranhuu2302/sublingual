# Phase 3: Context-Aware Translation & Quality Improvement

Tập trung vào vấn đề **CTranslate2 không translate hết content** và cải thiện chất lượng dịch.

---

## 3.1 Sentence Boundary Detection + Context Window

### Problem
Mỗi câu được dịch độc lập → mất context → dịch sai pronoun, thiếu consistency.

### Proposed
Send kèm previous sentence context trong mỗi request:

```python
class TranslateRequest(BaseModel):
    text: str
    source_lang: str
    target_lang: str
    context_before: str = ""  # previous 1-2 sentences
    context_after: str = ""   # optional: next sentence (nếu có)
```

### How it works
- C# side accumulate `_previousStableText` (2-3 câu gần nhất)
- Gửi kèm context trong request
- Python service có thể:
  - **Option A**: Concatenate context + text → translate together → extract phần tương ứng
  - **Option B**: Chỉ dùng context để set prompt/prefix (nếu model hỗ trợ)

### Impact
- Dịch pronoun chính xác hơn (he/she/it → đúng ngữ cảnh)
- Consistent terminology trong cùng session
- Giảm "translation noise" cho câu ngắn

---

## 3.2 Two-Tier Translation Model

### Problem
Hiện tại chỉ có 1 model CTranslate2 cho cả draft (partial) và stable (final). Draft cần nhanh, stable cần chất lượng.

### Proposed
```
Draft:   Model nhỏ, nhanh (CTranslate2 int8, beam_size=1)
Stable:  Model lớn hơn, chất lượng cao (beam_size=4, hoặc model fine-tuned)
```

### Implementation

```python
# Model manager giữ 2 translator cho mỗi pair
self.fast_cache: dict[str, MarianCT2Translator] = {}   # int8, beam=1
self.quality_cache: dict[str, MarianCT2Translator] = {} # fp16, beam=4
```

- Draft: dùng `fast` model (có thể dùng model hiện tại)
- Stable: dùng `quality` model (cần convert model mới với beam support)

### C# Side
```
RealtimeTranslationContext:
  - Target: Draft → gửi kèm quality="fast"
  - Target: StableSegment → gửi kèm quality="high"
```

### Impact
- Draft vẫn nhanh (~5-10ms)
- Stable quality cao hơn (beam search, model lớn hơn)
- Tách biệt latency-sensitive vs quality-sensitive

---

## 3.3 Accumulate Text Trước Khi Stable Translate

### Problem
Vosk có thể emit nhiều final segments cho 1 câu:
```
"Hello" (final) → "Hello everyone" (final) → "Hello everyone welcome to today's meeting" (final)
```
Nếu translate ngay mỗi final, kết quả không hoàn chỉnh và tốn requests.

### Proposed
Add accumulation window (400ms) trước khi stable translate:

```csharp
// AudioCaptureDebugSession.cs
private string _stableAccumulator = "";
private CancellationTokenSource? _stableDelayCts;

private async Task ScheduleStableWithAccumulation(string finalText)
{
    _stableAccumulator = CombineText(_stableAccumulator, finalText);
    
    _stableDelayCts?.Cancel();
    _stableDelayCts = new CancellationTokenSource();
    
    await Task.Delay(400, _stableDelayCts.Token);
    
    // Nếu không có final mới trong 400ms → gửi translate
    _translationScheduler.EnqueueStable(new StableTranslationRequest(
        ...,
        _stableAccumulator, // gửi accumulated text
        ...
    ));
    _stableAccumulator = "";
}
```

### Edge cases
- Nếu có silence > 400ms → câu đã hoàn chỉnh → translate
- Nếu người nói liên tục → accumulate nhiều final → translate cả đoạn
- Vẫn emit `TranscriptTranslationChanged(Pending)` ngay để UI show loading

### Impact
- Câu translate hoàn chỉnh hơn
- Giảm số stable requests
- Chất lượng dịch cao hơn (vì context dài hơn)

---

## 3.4 Fine-tune Marian Model

### Problem
Marian model base không tối ưu cho conversation/subtitle domain.

### Proposed fine-tune pipeline

```
1. Thu thập data:
   - Conversation corpus (EN-VI parallel)
   - Subtitle data (OpenSubtitles EN-VI)
   - Custom domain data nếu có

2. Fine-tune Marian:
   - Dùng HuggingFace MarianMTModel
   - Train trên conversation domain
   - Convert → CTranslate2 format

3. Evaluate:
   - BLEU score so với baseline
   - Latency benchmark
   - Subjective quality test
```

### CTranslate2 Model Conversion
```bash
ct2-transformers-converter \
    --model path/to/fine-tuned-marian \
    --output_dir models/ct2/en-vi-quality \
    --quantization int8_float16 \
    --force
```

### Threshold
- **Min acceptable:** không chậm hơn 2x so với model hiện tại
- **Target:** quality improvement +20% BLEU

---

## 3.5 Sentence-Level VAD cho Chunking Tự Nhiên

### Problem
`FixedWindowAudioChunkProcessor` cắt audio theo time window (350ms), không theo câu nói.

### Proposed
Thêm VAD (Voice Activity Detection) để chunk theo speech segments:

```
Audio → VAD → Speech Segment 1 (2.3s) → Vosk → "Hello everyone"
            → Speech Segment 2 (1.5s) → Vosk → "welcome to today's meeting"
            → Silence → không gửi vào Vosk
```

### Implementation options
1. **Energy-based** (đơn giản): RMS threshold → phát hiện speech/silence
2. **WebRTC VAD** (recommended): `webrtcvad` Python library hoặc C# binding
3. **Silero VAD** (chất lượng cao): model-based, phát hiện chính xác

### Impact
- Chunk tự nhiên theo câu nói
- Không gửi silence vào Vosk
- STT quality cao hơn (Vosk không bị noise)
- Giảm số chunk không cần thiết

### Recommended: WebRTC VAD
```csharp
// C# binding cho WebRTC VAD
public class WebRtcVad : IDisposable
{
    public bool HasVoiceActivity(byte[] audioFrame, int sampleRate = 16000)
    {
        // WebRTC VAD yêu cầu frame 10ms, 20ms, hoặc 30ms
        // Trả về true nếu có voice activity
    }
}
```

---

## 3.6 Gửi Nhiều Segment Trong 1 Request (Batch cho Draft)

### Problem
Trong 1 giây, Vosk có thể emit 2-3 partial texts. Mỗi partial là 1 HTTP request riêng.

### Proposed
C# side accumulate multiple partials trong 200ms window, gửi batch:

```json
// POST /translate/batch
{
    "texts": ["hello everyone", "hello everyone welcome", "hello everyone welcome to"],
    "source_lang": "en",
    "target_lang": "vi"
}
```

### C# Implementation
Dùng `BatchAccumulator` từ Phase 2 nhưng apply cho draft:
- Accumulate partial texts theo thời gian
- Flush mỗi 200ms hoặc khi có 5 partials
- Gửi batch → lấy kết quả → chỉ dùng latest result

### Impact
- Giảm HTTP requests
- CTranslate2 batch inference nhanh hơn
- Vẫn giữ latest-only semantics

---

## Tổng Kết Phase 3

| # | Change | Effort | Impact |
|---|---|---|---|
| 3.1 | Context window | 4 giờ | High (quality) |
| 3.2 | Two-tier model | 8-16 giờ | High (speed + quality) |
| 3.3 | Accumulate before stable | 2 giờ | High (quality) |
| 3.4 | Fine-tune model | 1-2 tuần | High (quality) |
| 3.5 | VAD chunking | 4-8 giờ | High (speed + quality) |
| 3.6 | Batch draft | 2 giờ | Medium (efficiency) |

**Total effort: ~3-4 tuần**
