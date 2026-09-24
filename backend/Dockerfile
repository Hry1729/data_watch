FROM tdengine/tsdb-amd64:3.4.0.9 AS tdengine-client

FROM data-watch-python314t:latest

ENV PYTHONUTF8=1
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
ENV LD_LIBRARY_PATH=/usr/local/lib:${LD_LIBRARY_PATH}

WORKDIR /app

COPY --from=tdengine-client /usr/local/lib/libtaos.so* /usr/local/lib/

COPY requirements.txt ./
RUN python3.14t -m pip install --no-cache-dir --upgrade pip \
  && sed '/^perspective-python/d;/^taos-ws-py/d' requirements.txt > requirements-docker.txt \
  && python3.14t -m pip install --no-cache-dir -r requirements-docker.txt

COPY . .

# Abort the build if a dependency silently switches Python 3.14t back to the GIL.
RUN python3.14t -c "import sys; import main; assert not sys._is_gil_enabled(), 'Backend import enabled the GIL'"

EXPOSE 8000

CMD ["python3.14t", "-m", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
