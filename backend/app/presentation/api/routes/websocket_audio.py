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
            chunk = message.get("bytes")
            if chunk is not None:
                responses = container.process_incoming_audio_chunk.execute(chunk)
                for response in responses:
                    await websocket.send_text(to_json(response))
    except WebSocketDisconnect:
        return
