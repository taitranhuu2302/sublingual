# Translation Performance Optimization

Tài liệu này phân tích và đề xuất cải thiện hiệu năng cho pipeline STT → Translation của Sublingual.

## Mục lục

| File | Nội dung |
|---|---|
| [`01-current-architecture.md`](01-current-architecture.md) | Kiến trúc hiện tại: flow, component, data flow |
| [`02-bottlenecks.md`](02-bottlenecks.md) | Các bottleneck đã xác định (kèm file:line) |
| [`03-phase1-quick-wins.md`](03-phase1-quick-wins.md) | Phase 1: thay đổi nhỏ, impact lớn |
| [`04-phase2-async-batch.md`](04-phase2-async-batch.md) | Phase 2: async Python, WebSocket, batch |
| [`05-phase3-context-quality.md`](05-phase3-context-quality.md) | Phase 3: context-aware, two-tier model, quality |
| [`06-implementation-roadmap.md`](06-implementation-roadmap.md) | Roadmap ưu tiên, effort, dependencies |

## Problem Statement

- **STT Vosk + translation quá chậm**, không liên tục
- **CTranslate2 không translate hết content**, mất ý nghĩa câu
- Draft translation gây request flood
- CPU-bound inference trên Python microservice

## TL;DR — Giải pháp chính

1. **Quick wins**: tăng threshold, thêm VAD, disable draft default
2. **Async + Batch**: async Python endpoints, batch accumulation, WebSocket
3. **Context + Quality**: context window, two-tier model, sentence boundary detection
