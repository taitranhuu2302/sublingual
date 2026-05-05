from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from app.bootstrap.container import Container
from app.infrastructure.serialization.json_encoder import to_json

router = APIRouter()
container = Container()


@router.websocket("/ws/audio")
async def websocket_audio(websocket: WebSocket) -> None:
    await websocket.accept()
    try:
        while True:
            message = await websocket.receive()
            if message.get("bytes") is not None or message.get("text") is not None:
                ack = container.process_incoming_audio_chunk.execute()
                await websocket.send_text(to_json(ack))
    except WebSocketDisconnect:
        return
