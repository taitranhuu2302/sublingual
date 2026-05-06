import uvicorn
import os
from app.main import create_app

app = create_app()


if __name__ == "__main__":
    host = os.getenv("BACKEND_HOST", "127.0.0.1")
    port = int(os.getenv("BACKEND_PORT", "8765"))
    uvicorn.run("main:app", host=host, port=port, reload=False)
