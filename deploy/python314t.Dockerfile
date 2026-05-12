FROM debian:bookworm-slim AS builder

ARG PYTHON_VERSION=3.14.4
ARG PYTHON_TARBALL_URL=https://www.python.org/ftp/python/3.14.4/Python-3.14.4.tgz

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    ca-certificates \
    curl \
    wget \
    libbz2-dev \
    libffi-dev \
    libgdbm-dev \
    liblzma-dev \
    libncursesw5-dev \
    libreadline-dev \
    libsqlite3-dev \
    libssl-dev \
    tk-dev \
    uuid-dev \
    zlib1g-dev \
  && rm -rf /var/lib/apt/lists/*

WORKDIR /tmp/python-build

RUN wget -O python.tgz "${PYTHON_TARBALL_URL}" \
  && tar -xzf python.tgz --strip-components=1 \
  && ./configure \
    --prefix=/opt/python314t \
    --disable-gil \
    --enable-optimizations \
    --with-ensurepip=install \
  && make -j"$(nproc)" \
  && make install

FROM debian:bookworm-slim

ENV PYTHONUTF8=1
ENV PATH="/opt/python314t/bin:${PATH}"

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    libbz2-1.0 \
    libffi8 \
    libgdbm6 \
    liblzma5 \
    libncursesw6 \
    libreadline8 \
    libsqlite3-0 \
    libssl3 \
    tk \
    uuid-runtime \
    zlib1g \
  && rm -rf /var/lib/apt/lists/*

COPY --from=builder /opt/python314t /opt/python314t

RUN ln -s /opt/python314t/bin/python3.14 /usr/local/bin/python3.14t \
  && ln -s /opt/python314t/bin/pip3.14 /usr/local/bin/pip3.14t \
  && python3.14t --version \
  && python3.14t -c "import sys; print('GIL enabled:', sys._is_gil_enabled())"

CMD ["python3.14t", "--version"]
