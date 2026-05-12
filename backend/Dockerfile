FROM data-watch-python314t:latest

ENV PYTHONUTF8=1
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

WORKDIR /app

COPY requirements.txt ./
RUN python3.14t -m pip install --no-cache-dir --upgrade pip \
  && sed '/^perspective-python/d' requirements.txt > requirements-docker.txt \
  && python3.14t -m pip install --no-cache-dir -r requirements-docker.txt

COPY . .

EXPOSE 8000

CMD ["python3.14t", "-m", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
