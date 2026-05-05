from fastapi import APIRouter

from app.presentation.api.routes.health import router as health_router
from app.presentation.api.routes.websocket_audio import router as websocket_audio_router

api_router = APIRouter()
api_router.include_router(health_router)
api_router.include_router(websocket_audio_router)
