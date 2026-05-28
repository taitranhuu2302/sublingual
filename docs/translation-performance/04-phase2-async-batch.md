# Phase 2: Async Python + Batch Processing

Những thay đổi lớn hơn, cần refactor ở Python service.

---

## 2.1 Async Python Endpoints

### Problem
Hiện tại tất cả endpoint dùng `def` (sync), block uvicorn worker thread khi inference.

### Proposed
Chuyển sang `async def` + chạy CTranslate2 inference trong thread pool:

```python
import asyncio
from concurrent.futures import ThreadPoolExecutor

_executor = ThreadPoolExecutor(max_workers=2)

@app.post("/translate", response_model=TranslateResponse)
async def translate(request: TranslateRequest) -> TranslateResponse:
    started = time.perf_counter()
    source_text = _prepare_text(request.text)
    translator = model_manager.get_translator(request.source_lang, request.target_lang)
    
    # Chạy inference trong thread pool, không block event loop
    translated_text = await asyncio.get_event_loop().run_in_executor(
        _executor, translator.translate, source_text
    )
    
    latency_ms = (time.perf_counter() - started) * 1000
    ...
```

### Tương tự cho:
- `translate_realtime()`
- `translate_batch()`

### Lưu ý
- `RealtimeSessionCache` đang dùng `threading.Lock` — vẫn safe vì lock là thread-safe
- `TranslationModelManager` dùng `threading.Lock` — same
- Uvicorn worker config: tăng `--workers 2` để xử lý concurrent requests

### Impact
- Không block event loop khi inference
- Draft + stable request có thể xử lý đồng thời
- Hỗ trợ nhiều session hơn

---

## 2.2 WebSocket Streaming Endpoint

### Problem
HTTP POST cho mỗi partial text có overhead JSON serialization + connection setup.

### Proposed
Thêm `/translate/stream` WebSocket endpoint:

```
Client → Server: text partials (stream)
Server → Client: translation tokens (stream)
```

### Design

```python
@app.websocket("/translate/stream")
async def translate_stream(websocket: WebSocket):
    await websocket.accept()
    session = {}  # track context per connection
    
    while True:
        data = await websocket.receive_json()
        text = data["text"]
        is_final = data.get("is_final", False)
        
        # Accumulate text trong session
        session["buffer"] = session.get("buffer", "") + text
        
        if is_good_boundary(session["buffer"]) or is_final:
            translated = await translate_async(session["buffer"])
            await websocket.send_json({
                "translated_text": translated,
                "is_final": is_final,
                "should_display": True
            })
            session["buffer"] = ""
```

### C# Client Update

Thay thế `TranslateServiceLocalTranslationProvider` HTTP POST bằng WebSocket client:

```csharp
// Dùng ClientWebSocket để kết nối
using var ws = new ClientWebSocket();
await ws.ConnectAsync(new Uri("ws://127.0.0.1:3333/translate/stream"), ct);
```

### Impact
- Giảm overhead mỗi request
- Server có thể push translation tokens ngay khi có (streaming)
- Giảm latency cho draft translation

### Khi nào nên triển khai
Sau khi Phase 1 đã ổn định, và cần thêm tốc độ cho draft. Nếu Phase 1 đã đủ (disable draft), WebSocket là optional.

---

## 2.3 Batch Translation Accumulator

### Problem
Nhiều segment nhỏ riêng lẻ → nhiều HTTP request → CTranslate2 không tận dụng được batch processing.

### Proposed
C# side accumulate text segments trong 200-400ms window, gộp thành 1 batch request:

```csharp
// BatchAccumulator.cs
public sealed class BatchAccumulator : IDisposable
{
    private readonly List<string> _buffer = new();
    private readonly Timer _flushTimer;
    private readonly Func<string[], Task> _onFlush;
    
    public void Add(string text)
    {
        _buffer.Add(text);
        _flushTimer.Change(200, Timeout.Infinite); // reset timer
    }
    
    private async void Flush(object? state)
    {
        var batch = _buffer.ToArray();
        _buffer.Clear();
        await _onFlush(batch); // POST /translate/batch
    }
}
```

### Python Service - `/translate/batch` đã sẵn sàng
Endpoint `POST /translate/batch` đã có, nhận `list[str]` và trả về batch result. CTranslate2 có optimize cho batch processing.

### Impact
- Giảm số HTTP requests
- CTranslate2 batch inference nhanh hơn tổng nhiều single request
- Tốt cho stable segments gần nhau

---

## 2.4 Python Service Auto-scaling Workers

### Config Recommendation
```
UVICORN_WORKERS=2
UVICORN_BACKLOG=128
```

### Lý do
- 2 workers cho phép xử lý concurrent requests (draft + stable cùng lúc)
- Tránh thay đổi code, chỉ cần update docker-compose hoặc systemd

---

## Tổng Kết Phase 2

| # | Change | Effort | Impact |
|---|---|---|---|
| 2.1 | Async endpoints | 4 giờ | Medium (không block) |
| 2.2 | WebSocket endpoint | 8-12 giờ | Medium (giảm overhead) |
| 2.3 | Batch accumulator | 4 giờ | Medium (batch inference) |
| 2.4 | Uvicorn workers=2 | 15 phút | Low (concurrent) |

**Total effort: ~16-24 giờ**
