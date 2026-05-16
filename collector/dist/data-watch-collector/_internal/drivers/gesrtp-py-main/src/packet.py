#!/usr/bin/env python3
"""
gesrtp-py - GE-SRTP PLC Driver

Copyright (c) 2025 Jobe Eli Eastwood
Houston, TX

A Python driver for communicating with GE PLCs using the SRTP protocol.
Read-only driver for forensic memory acquisition and PLC monitoring.

Author: Jobe Eli Eastwood <jobeastwood@hotmail.com>
Project: https://github.com/jobeastwood/gesrtp-py
License: MIT

---

SRTP Packet Builder and Parser

This module provides the SRTPPacket class for building and parsing 56-byte
GE-SRTP protocol packets according to the specification.

Packet Structure (56 bytes):
    Byte 0:     Type (0x02=Request, 0x03=Response)
    Byte 1:     Reserved (0x00)
    Byte 2:     Sequence Number
    Byte 3:     Reserved (0x00)
    Byte 4:     Text Length (0x00)
    Bytes 5-8:  Reserved (0x00)
    Byte 9:     Reserved (0x01)
    Bytes 10-16: Reserved (0x00)
    Byte 17:    Reserved (0x01)
    Bytes 18-25: Reserved (0x00)
    Byte 26:    Time - seconds
    Byte 27:    Time - minutes
    Byte 28:    Time - hours
    Byte 29:    Reserved (0x00)
    Byte 30:    Sequence Number (duplicate)
    Byte 31:    Message Type (0xc0=Request, 0xd4=ACK, 0xd1=Error)
    Bytes 32-35: Mailbox Source (0x00 00 00 00)
    Bytes 36-39: Mailbox Destination (0x10 0e 00 00)
    Byte 40:    Packet Number (0x01)
    Byte 41:    Total Packets (0x01)
    Byte 42:    Service Request Code
    Byte 43:    Segment Selector
    Byte 44:    Data Offset LSB (little-endian)
    Byte 45:    Data Offset MSB
    Byte 46:    Data Length LSB (little-endian)
    Byte 47:    Data Length MSB
    Bytes 48-55: Reserved (0x00)
"""

import struct
import logging
from datetime import datetime
from typing import Optional, Tuple

from . import protocol
from . import exceptions


logger = logging.getLogger(__name__)


class SRTPPacket:
    """
    GE-SRTP packet builder and parser.

    This class handles construction of request packets and parsing of response
    packets according to the 56-byte SRTP protocol specification.
    """

    def __init__(self):
        """Initialize an SRTP packet."""
        self.packet_type: int = protocol.PacketType.REQUEST
        self.sequence_number: int = 0
        self.message_type: int = protocol.MessageType.REQUEST
        self.service_code: int = 0
        self.segment_selector: int = 0
        self.data_offset: int = 0
        self.data_length: int = 0
        self.payload: bytes = b''
        self.timestamp: datetime = datetime.now()

    @staticmethod
    def build_request(
        sequence_number: int,
        service_code: int,
        segment_selector: int = 0,
        data_offset: int = 0,
        data_length: int = 0,
        payload: bytes = b'',
        slot: int = 1
    ) -> bytes:
        """
        Build a 56-byte SRTP request packet.

        Args:
            sequence_number: Packet sequence number (0-255)
            service_code: Service request code from protocol.ServiceCode
            segment_selector: Segment selector for memory access
            data_offset: Memory address offset (little-endian)
            data_length: Number of bytes/words to read/write (little-endian)
            payload: Optional payload data (for write operations)
            slot: CPU slot number (default 1)

        Returns:
            56-byte request packet + payload

        Raises:
            ValidationError: If parameters are out of range
        """
        # Validate inputs
        if not (0 <= sequence_number <= 255):
            raise exceptions.ValidationError(f"Sequence number must be 0-255, got {sequence_number}")
        if not (0 <= data_offset <= 65535):
            raise exceptions.ValidationError(f"Data offset must be 0-65535, got {data_offset}")
        if not (0 <= data_length <= 65535):
            raise exceptions.ValidationError(f"Data length must be 0-65535, got {data_length}")

        # Get current time
        now = datetime.now()

        # Build 56-byte header
        packet = bytearray(protocol.HEADER_SIZE)

        # Byte 0: Packet type
        packet[0] = protocol.PacketType.REQUEST

        # Byte 1: Reserved
        packet[1] = 0x00

        # Byte 2: Sequence number
        packet[2] = sequence_number & 0xFF

        # Byte 3: Reserved
        packet[3] = 0x00

        # Byte 4: Text length
        packet[4] = 0x00

        # Bytes 5-8: Reserved
        packet[5:9] = bytes([0x00] * 4)

        # Byte 9: Reserved (0x01)
        packet[9] = 0x01

        # Bytes 10-16: Reserved
        packet[10:17] = bytes([0x00] * 7)

        # Byte 17: Reserved (0x01)
        packet[17] = 0x01

        # Bytes 18-25: Reserved
        packet[18:26] = bytes([0x00] * 8)

        # Bytes 26-28: Timestamp (seconds, minutes, hours)
        packet[26] = now.second
        packet[27] = now.minute
        packet[28] = now.hour

        # Byte 29: Reserved
        packet[29] = 0x00

        # Byte 30: Sequence number (duplicate)
        packet[30] = sequence_number & 0xFF

        # Byte 31: Message type
        packet[31] = protocol.MessageType.REQUEST

        # Bytes 32-35: Mailbox source
        packet[32:36] = protocol.MAILBOX_SOURCE

        # Bytes 36-39: Mailbox destination (slot-specific)
        packet[36:40] = protocol.get_mailbox_destination(slot)

        # Byte 40: Packet number (1-indexed)
        packet[40] = 0x01

        # Byte 41: Total packets
        packet[41] = 0x01

        # Byte 42: Service request code
        packet[42] = service_code & 0xFF

        # Byte 43: Segment selector
        packet[43] = segment_selector & 0xFF

        # Bytes 44-45: Data offset (little-endian)
        packet[44] = data_offset & 0xFF        # LSB
        packet[45] = (data_offset >> 8) & 0xFF  # MSB

        # Bytes 46-47: Data length (little-endian)
        packet[46] = data_length & 0xFF        # LSB
        packet[47] = (data_length >> 8) & 0xFF  # MSB

        # Bytes 48-55: Reserved
        packet[48:56] = bytes([0x00] * 8)

        # Append payload if present
        full_packet = bytes(packet) + payload

        logger.debug(
            f"Built request packet: seq={sequence_number}, service=0x{service_code:02X}, "
            f"selector=0x{segment_selector:02X}, offset={data_offset}, length={data_length}"
        )

        return full_packet

    @staticmethod
    def parse_response(data: bytes) -> 'SRTPPacket':
        """
        Parse an SRTP response packet.

        Args:
            data: Raw packet data (minimum 56 bytes)

        Returns:
            SRTPPacket object with parsed fields

        Raises:
            InvalidPacketError: If packet is malformed
            InvalidResponseError: If response indicates an error
        """
        if len(data) < protocol.HEADER_SIZE:
            raise exceptions.InvalidPacketError(
                f"Packet too short: expected at least {protocol.HEADER_SIZE} bytes, "
                f"got {len(data)}"
            )

        packet = SRTPPacket()

        # Parse header fields
        packet.packet_type = data[0]
        packet.sequence_number = data[2]
        packet.message_type = data[31]
        packet.service_code = data[42]
        packet.segment_selector = data[43]

        # Parse data offset (little-endian)
        packet.data_offset = data[44] | (data[45] << 8)

        # Parse data length (little-endian)
        packet.data_length = data[46] | (data[47] << 8)

        # Validate packet type
        if packet.packet_type != protocol.PacketType.RESPONSE:
            raise exceptions.InvalidPacketError(
                f"Expected response packet (0x03), got 0x{packet.packet_type:02X}"
            )

        # Check message type
        if packet.message_type == protocol.MessageType.ERROR:
            # Extract error code if available in payload
            error_code = data[56] if len(data) > 56 else 0
            raise exceptions.error_code_to_exception(
                error_code,
                f"PLC returned error (NACK): 0x{error_code:02X}"
            )

        # Accept both ACK (0xD4) and ACK_WITH_DATA (0x94) as successful responses
        if packet.message_type not in (protocol.MessageType.ACK, protocol.MessageType.ACK_WITH_DATA):
            logger.warning(
                f"Unexpected message type: 0x{packet.message_type:02X} "
                f"(expected ACK 0xD4 or ACK_WITH_DATA 0x94)"
            )

        # Extract payload (data beyond 56-byte header)
        if len(data) > protocol.HEADER_SIZE:
            packet.payload = data[protocol.HEADER_SIZE:]
        else:
            packet.payload = b''

        logger.debug(
            f"Parsed response: seq={packet.sequence_number}, "
            f"msg_type=0x{packet.message_type:02X}, "
            f"payload_len={len(packet.payload)}"
        )

        return packet

    def extract_data_payload(self) -> bytes:
        """
        Extract the data payload from a response packet.

        Returns:
            Data payload bytes

        Raises:
            InvalidResponseError: If packet has no payload
        """
        if not self.payload:
            raise exceptions.InvalidResponseError("Response packet has no data payload")

        return self.payload

    def extract_word_values(self) -> list:
        """
        Extract 16-bit word values from payload (little-endian).

        Returns:
            List of 16-bit signed integers

        Raises:
            InvalidResponseError: If payload length is not even
        """
        if len(self.payload) % 2 != 0:
            raise exceptions.InvalidResponseError(
                f"Payload length {len(self.payload)} is not a multiple of 2 bytes"
            )

        # Unpack as little-endian 16-bit signed integers
        word_count = len(self.payload) // 2
        values = struct.unpack(f'<{word_count}h', self.payload)

        logger.debug(f"Extracted {word_count} word values: {values}")

        return list(values)

    def extract_byte_values(self) -> list:
        """
        Extract byte values from payload.

        Returns:
            List of byte values (0-255)
        """
        values = list(self.payload)
        logger.debug(f"Extracted {len(values)} byte values")
        return values

    def extract_bit_values(self, bit_count: int) -> list:
        """
        Extract individual bit values from payload.

        Args:
            bit_count: Number of bits to extract

        Returns:
            List of boolean values
        """
        bits = []
        byte_count = (bit_count + 7) // 8  # Round up

        for byte_idx in range(min(byte_count, len(self.payload))):
            byte_val = self.payload[byte_idx]
            for bit_idx in range(8):
                if len(bits) >= bit_count:
                    break
                bits.append(bool(byte_val & (1 << bit_idx)))

        logger.debug(f"Extracted {len(bits)} bit values")
        return bits

    def validate_sequence_number(self, expected_seq: int) -> bool:
        """
        Validate that packet sequence number matches expected value.

        Args:
            expected_seq: Expected sequence number

        Returns:
            True if sequence numbers match

        Raises:
            SequenceNumberError: If sequence numbers don't match
        """
        if self.sequence_number != expected_seq:
            raise exceptions.SequenceNumberError(
                f"Sequence number mismatch: expected {expected_seq}, "
                f"got {self.sequence_number}"
            )
        return True

    def __repr__(self) -> str:
        """String representation of packet."""
        return (
            f"SRTPPacket(type=0x{self.packet_type:02X}, seq={self.sequence_number}, "
            f"msg_type=0x{self.message_type:02X}, service=0x{self.service_code:02X}, "
            f"selector=0x{self.segment_selector:02X}, offset={self.data_offset}, "
            f"length={self.data_length}, payload_len={len(self.payload)})"
        )
