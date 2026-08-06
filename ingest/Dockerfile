FROM python:3.14-slim

ENV PYTHONUTF8=1
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

WORKDIR /app
COPY ingest/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

COPY data_watch_shared ./data_watch_shared
COPY ingest ./ingest

EXPOSE 8010
CMD ["python", "-m", "uvicorn", "ingest.main:app", "--host", "0.0.0.0", "--port", "8010"]
