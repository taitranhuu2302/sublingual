FROM python:3.11-slim

ARG UVICORN_WORKERS=1

WORKDIR /app

COPY requirements.txt ./requirements.txt

RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r requirements.txt \
    && pip install --no-cache-dir torch

COPY . .

EXPOSE 3333

CMD ["sh", "-c", "uvicorn app.main:app --host 0.0.0.0 --port 3333 --workers ${UVICORN_WORKERS}"]
