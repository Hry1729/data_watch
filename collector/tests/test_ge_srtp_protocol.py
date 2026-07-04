import os
import sys
from pathlib import Path

import pytest


GESRTP_ROOT = Path(__file__).resolve().parents[1] / "drivers" / "gesrtp-py-main"
sys.path.insert(0, os.fspath(GESRTP_ROOT))

from src import exceptions, protocol  # noqa: E402
from src.connection import SRTPConnection  # noqa: E402
from src.packet import SRTPPacket  # noqa: E402


class FakeSocket:
    def __init__(self, chunks):
        self._chunks = list(chunks)

    def recv(self, size):
        if not self._chunks:
            return b""
        chunk = self._chunks.pop(0)
        if len(chunk) <= size:
            return chunk
        self._chunks.insert(0, chunk[size:])
        return chunk[:size]


def test_nack_uses_header_status_bytes():
    data = bytearray(protocol.HEADER_SIZE)
    data[0] = protocol.PacketType.RESPONSE
    data[31] = protocol.MessageType.ERROR
    data[42] = 0x07
    data[43] = 0x02

    with pytest.raises(exceptions.PLCError) as exc_info:
        SRTPPacket.parse_response(bytes(data))

    message = str(exc_info.value)
    assert exc_info.value.error_code == 0x07
    assert "main_status=0x07" in message
    assert "sub_status=0x02" in message


def test_receive_data_reads_split_header_and_payload():
    header = bytearray(protocol.HEADER_SIZE)
    header[0] = protocol.PacketType.RESPONSE
    header[4] = 4
    header[31] = protocol.MessageType.ACK_WITH_DATA

    conn = SRTPConnection("127.0.0.1")
    conn.sock = FakeSocket([
        bytes(header[:7]),
        bytes(header[7:]),
        b"\x01",
        b"\x02\x03\x04",
    ])
    conn.is_connected = True
    conn.is_initialized = True

    data = conn._receive_data(1024)

    assert data == bytes(header) + b"\x01\x02\x03\x04"


def test_recv_exact_marks_disconnected_on_short_stream():
    conn = SRTPConnection("127.0.0.1")
    conn.sock = FakeSocket([b"ab", b""])
    conn.is_connected = True

    with pytest.raises(exceptions.ConnectionError, match="received 2/4"):
        conn._recv_exact(4)

    assert conn.is_connected is False
