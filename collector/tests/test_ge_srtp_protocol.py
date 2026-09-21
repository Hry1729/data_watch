import os
import socket
import sys
from pathlib import Path

import pytest


GESRTP_ROOT = Path(__file__).resolve().parents[1] / "drivers" / "gesrtp-py-main"
sys.path.insert(0, os.fspath(GESRTP_ROOT))

from packages.ge_srtp.ge_srtp.transport import exceptions, protocol  # noqa: E402
from packages.ge_srtp.ge_srtp.transport.connection import SRTPConnection  # noqa: E402
from packages.ge_srtp.ge_srtp.transport.driver import GE_SRTP_Driver  # noqa: E402
from packages.ge_srtp.ge_srtp.transport.packet import SRTPPacket  # noqa: E402


class FakeSocket:
    def __init__(self, chunks):
        self._chunks = list(chunks)
        self.closed = False

    def recv(self, size):
        if not self._chunks:
            return b""
        chunk = self._chunks.pop(0)
        if isinstance(chunk, BaseException):
            raise chunk
        if len(chunk) <= size:
            return chunk
        self._chunks.insert(0, chunk[size:])
        return chunk[:size]


    def close(self):
        self.closed = True

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


class FakeConnection:
    def __init__(self, response):
        self.response = response
        self.disconnect_count = 0

    def send_request(self, _request):
        pass

    def receive_response(self):
        return self.response

    def disconnect(self):
        self.disconnect_count += 1


def _build_response(sequence, *, error=False):
    header = bytearray(protocol.HEADER_SIZE)
    header[0] = protocol.PacketType.RESPONSE
    header[2] = sequence
    header[31] = protocol.MessageType.ERROR if error else protocol.MessageType.ACK
    if error:
        header[42] = 0x07
        header[43] = 0x00
    return bytes(header)


def test_receive_timeout_invalidates_connection():
    conn = SRTPConnection("127.0.0.1")
    sock = FakeSocket([socket.timeout()])
    conn.sock = sock
    conn.is_connected = True
    conn.is_initialized = True

    with pytest.raises(exceptions.TimeoutError, match="Timeout receiving response"):
        conn._receive_data(1024)

    assert sock.closed is True
    assert conn.sock is None
    assert conn.is_connected is False
    assert conn.is_initialized is False


def test_sequence_mismatch_invalidates_connection():
    driver = GE_SRTP_Driver("127.0.0.1")
    conn = FakeConnection(_build_response(sequence=1))
    driver.connection = conn

    with pytest.raises(exceptions.SequenceNumberError):
        driver._send_request_and_receive(protocol.ServiceCode.READ_SYSTEM_MEMORY)

    assert conn.disconnect_count == 1


def test_fully_consumed_plc_nack_keeps_connection():
    driver = GE_SRTP_Driver("127.0.0.1")
    conn = FakeConnection(_build_response(sequence=0, error=True))
    driver.connection = conn

    with pytest.raises(exceptions.PLCError):
        driver._send_request_and_receive(protocol.ServiceCode.READ_SYSTEM_MEMORY)

    assert conn.disconnect_count == 0


@pytest.mark.parametrize("payload_length", [256, 500, 1000, 1024])
def test_receive_data_reads_16_bit_payload_length(payload_length):
    header = bytearray(protocol.HEADER_SIZE)
    header[0] = protocol.PacketType.RESPONSE
    header[4:6] = payload_length.to_bytes(2, "little")
    header[31] = protocol.MessageType.ACK_WITH_DATA
    payload = bytes(index % 256 for index in range(payload_length))

    conn = SRTPConnection("127.0.0.1")
    conn.sock = FakeSocket([bytes(header[:13]), bytes(header[13:]), payload[:17], payload[17:]])
    conn.is_connected = True
    conn.is_initialized = True

    data = conn._receive_data(1024)

    assert data == bytes(header) + payload


def test_driver_allows_word_count_derived_from_payload_limit():
    class WordResponse:
        @staticmethod
        def extract_word_values():
            return list(range(512))

    driver = GE_SRTP_Driver("127.0.0.1", max_payload_bytes=1024)
    captured = {}

    def fake_request(**kwargs):
        captured.update(kwargs)
        return WordResponse()

    driver._send_request_and_receive = fake_request
    values = driver.read_register(0, 512)

    assert len(values) == 512
    assert captured["data_length"] == 512
    with pytest.raises(exceptions.ValidationError, match="1-512"):
        driver.read_register(0, 513)


@pytest.mark.parametrize("payload_bytes", [7, 1025, "invalid"])
def test_driver_rejects_invalid_payload_limit(payload_bytes):
    with pytest.raises(exceptions.ValidationError, match="max_payload_bytes"):
        GE_SRTP_Driver("127.0.0.1", max_payload_bytes=payload_bytes)
