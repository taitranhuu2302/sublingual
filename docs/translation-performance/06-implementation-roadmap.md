# Implementation Roadmap

## Ưu Tiên

Dựa trên **effort vs impact**, đề xuất thứ tự triển khai:

### P0 — Critical (làm ngay)
| # | Task | Phase | Effort | Lý do |
|---|---|---|---|---|
| 1.3 | Disable draft translation default | 1 | 5 phút | Ngay lập tức giảm 90% requests |
| 1.4 | VAD energy check | 1 | 2 giờ | Giảm 50% chunks vào Vosk |
| 1.1 | Tăng `MIN_REALTIME_CHARS` 8→15 | 1 | 5 phút | Giảm 40% draft requests |
| 1.5 | Stable queue cap | 1 | 30 phút | Memory safety |

### P1 — Important (triển khai sau P0)
| # | Task | Phase | Effort | Lý do |
|---|---|---|---|---|
| 1.2 | Tăng debounce 300→500ms | 1 | 5 phút | Giảm 40% draft |
| 3.3 | Accumulate before stable | 3 | 2 giờ | Quality cao hơn |
| 1.7 | C# min chars check | 1 | 5 phút | Skip sớm |
| 1.6 | Cache key normalization | 1 | 15 phút | Cache hit rate |

### P2 — Nice to Have
| # | Task | Phase | Effort | Lý do |
|---|---|---|---|---|
| 2.1 | Async Python endpoints | 2 | 4 giờ | Không block worker |
| 1.8 | Adaptive debounce | 1 | 1 giờ | Tự điều chỉnh |
| 2.3 | Batch accumulator | 2 | 4 giờ | Batch inference |
| 2.4 | Uvicorn workers=2 | 2 | 15 phút | Concurrent |

### P3 — Future
| # | Task | Phase | Effort | Lý do |
|---|---|---|---|---|
| 3.1 | Context window | 3 | 4 giờ | Quality |
| 3.2 | Two-tier model | 3 | 8-16 giờ | Speed + Quality |
| 3.5 | WebRTC VAD chunking | 3 | 4-8 giờ | Natural chunking |
| 3.6 | Batch draft | 3 | 2 giờ | Efficiency |
| 2.2 | WebSocket endpoint | 2 | 8-12 giờ | Overhead |
| 3.4 | Fine-tune model | 3 | 1-2 tuần | Quality |

---

## Dependency Graph

```
P0:
  1.3 (disable draft)
    └── 1.1 (MIN_REALTIME_CHARS) — independent
    └── 1.4 (VAD) — independent
    └── 1.5 (queue cap) — independent

P1:
  1.2 (debounce) — độc lập
  3.3 (accumulate) — cần 1.3 trước (nếu disable draft thì không cần accumulate stable?)
       → vẫn cần: stable accumulation độc lập với draft
  1.7 (C# min chars) — sync với 1.1
  1.6 (cache key) — độc lập

P2:
  2.1 (async Python) — độc lập
  1.8 (adaptive debounce) — cần 1.2 trước
  2.3 (batch accumulator) — có thể chạy song song
  2.4 (uvicorn workers) — cần 2.1? không, độc lập

P3:
  3.1 (context window) — cần Python endpoint cập nhật schema
  3.2 (two-tier) — cần convert model
  3.5 (VAD chunking) — có thể thay thế 1.4 (energy VAD)
  3.6 (batch draft) — depends on batch design
  2.2 (WebSocket) — độc lập
  3.4 (fine-tune) — độc lập, tốn data
```

---

## Effort Estimate (Tổng)

| Phase | Effort | Kết quả |
|---|---|---|
| Phase 1 (P0 + P1) | ~4-5 giờ | ↓90% requests, ↓50% Vosk load |
| Phase 2 (P2) | ~8-16 giờ | Non-blocking, batch optimization |
| Phase 3 (P3) | ~3-4 tuần | Quality improvement, context |

**Total: ~4-6 tuần full-time** (nếu làm hết)

---

## Metrics Tracking

Sau mỗi phase, theo dõi các metrics:

### Latency
- **Audio → STT result**: thời gian từ chunk capture đến Vosk result
- **STT → Translation complete**: thời gian từ Vosk final đến translation result
- **Draft latency**: từ Vosk partial → UI cập nhật draft translation
- **P50/P95/P99**: phân phối latency

### Throughput
- **Requests/second**: số translation requests gửi đến Python service
- **Skip rate**: % requests bị skip (too_short, too_similar, weak_boundary)
- **Cache hit rate**: % translation từ cache

### Quality
- **Coverage**: % source text được translate (không bị empty)
- **BLEU score**: so với reference translation
  
### Resource
- **CPU usage**: Vosk vs translation vs other
- **Stable queue depth**: backlog
- **Memory**: queue + cache size

---

## Rollback Plan

Mỗi change nên có flag/setting để rollback nhanh:

```csharp
// Settings
public bool EnableVad { get; set; } = true;
public int MinRealtimeChars { get; set; } = 15;
public int DraftDebounceMs { get; set; } = 500;
```

```python
# .env
TRANSLATION_QUALITY=fast  # fast | high
ENABLE_CONTEXT_WINDOW=true
```
