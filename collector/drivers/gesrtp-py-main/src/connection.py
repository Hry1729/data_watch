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

TCP Connection Management for GE-SRTP Protocol

This module handles TCP/IP socket connections to GE PLCs, including the critical
two-packet initialization handshake sequence that must occur before any service
requests can be made.

The initialization sequence was discovered through reverse engineering and is
NOT documented in the academic paper.
"""

import socket
import logging
from typing import Optional

from . import protocol
from . import exceptions


logger = logging.getLogger(__name__)


class SRTPConnection:
    """
    Manages TCP/IP connection to a GE PLC using the SRTP protocol.

    Handles:
    - TCP socket connection
    - Two-packet initialization handshake
    - Send/receive operations with timeout
    - Connection lifecycle management
    """

    def __init__(
        self,
        host: str,
        port: int = protocol.DEFAULT_PORT,
        timeout: int = protocol.DEFAULT_TIMEOUT
    ):
        """
        Initialize connection parameters.

        Args:
            host: PLC IP address or hostname
            port: TCP port (default 18245)
            timeout: Socket timeout in seconds (default 5)
        """
        self.host = host
        self.port = port
        self.timeout = timeout
        self.sock: Optional[socket.socket] = None
        self.is_connected = False
        self.is_initialized = False

        logger.info(f"Initialized SRTP connection for {host}:{port}")

    def connect(self) -> None:
        """
        Establish TCP connection to PLC and perform initialization handshake.

        Raises:
            ConnectionError: If connection fails
            InitializationError: If initialization handshake fails
        """
        if self.is_connected:
            logger.warning("Already connected")
            return

        try:
            # Create TCP socket
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.sock.settimeout(self.timeout)
            # 关 Nagle：SRTP 是小报文请求/响应型协议，Nagle + 延迟 ACK 可能给每次往返
            # 叠加几十毫秒停顿，高频采集下放大严重。关掉让小包立即发出。
            self.sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)

            # Connect to PLC
            logger.info(f"Connecting to PLC at {self.host}:{self.port}...")
            self.sock.connect((self.host, self.port))
            self.is_connected = True
            logger.info("TCP connection established")

            # Perform initialization handshake
            self._perform_initialization()

        except socket.timeout as e:
            raise exceptions.TimeoutError(
                f"Connection timeout to {self.host}:{self.port}"
            ) from e
        except socket.error as e:
            raise exceptions.ConnectionError(
                f"Failed to connect to {self.host}:{self.port}: {e}"
            ) from e
        except Exception as e:
            self.disconnect()
            raise exceptions.ConnectionError(
                f"Connection error: {e}"
            ) from e

    def _perform_initialization(self) -> None:
        """
        Perform the critical two-packet initialization handshake.

        This sequence is REQUIRED before any service requests can be sent.
        It was discovered through reverse engineering and Wireshark analysis.

        Raises:
            InitializationError: If initialization fails
        """
        logger.info("Starting initialization handshake...")

        try:
            # INITIALIZATION PACKET 1
            # This exact sequence is critical - discovered through reverse engineering
            logger.debug("Sending initialization packet 1...")
            self.sock.sendall(protocol.INIT_PACKET_1)

            # Wait for response to init packet 1
            response1 = self._receive_data(56)
            if len(response1) < 1:
                raise exceptions.InitializationError(
                    "No response to initialization packet 1"
                )

            # Validate response - byte[0] should be 0x01
            if response1[0] != 0x01:
                raise exceptions.InitializationError(
                    f"Invalid response to init packet 1: expected 0x01, got 0x{response1[0]:02X}"
                )

            logger.debug(f"Init packet 1 response received: {response1[:8].hex()}")

            # INITIALIZATION PACKET 2
            # Note: The second initialization packet pattern needs to be determined
            # from reference implementations. For now, we'll assume init is complete
            # after packet 1. This should be updated based on actual PLC behavior.

            # TODO: Implement second initialization packet if required by PLC
            # This may vary by PLC model. Monitor Wireshark captures to determine
            # if a second init packet is needed.

            self.is_initialized = True
            logger.info("Initialization handshake complete")

        except socket.timeout as e:
            raise exceptions.InitializationError(
                "Timeout during initialization handshake"
            ) from e
        except socket.error as e:
            raise exceptions.InitializationError(
                f"Socket error during initialization: {e}"
            ) from e
        except Exception as e:
            raise exceptions.InitializationError(
                f"Initialization failed: {e}"
            ) from e

    def send_request(self, request_data: bytes) -> None:
        """
        Send a request packet to the PLC.

        Args:
            request_data: Complete packet data to send

        Raises:
            ConnectionError: If not connected or send fails
        """
        if not self.is_connected or self.sock is None:
            raise exceptions.ConnectionError("Not connected to PLC")

        if not self.is_initialized:
            raise exceptions.ConnectionError("Connection not initialized")

        try:
            logger.debug(f"Sending {len(request_data)} bytes to PLC")
            self.sock.sendall(request_data)
            # .hex() 是热点：实参恒被求值，故用 isEnabledFor 守卫，DEBUG 关时不计算
            if logger.isEnabledFor(logging.DEBUG):
                logger.debug(f"Sent packet: {request_data[:56].hex()}")

        except socket.timeout as e:
            raise exceptions.TimeoutError("Timeout sending request") from e
        except socket.error as e:
            self.is_connected = False
            raise exceptions.ConnectionError(f"Error sending request: {e}") from e

    def receive_response(self, expected_size: int = 1024) -> bytes:
        """
        Receive a response packet from the PLC.

        Args:
            expected_size: Maximum number of bytes to receive

        Returns:
            Response data

        Raises:
            ConnectionError: If not connected or receive fails
            TimeoutError: If receive times out
        """
        if not self.is_connected or self.sock is None:
            raise exceptions.ConnectionError("Not connected to PLC")

        return self._receive_data(expected_size)

    def _recv_exact(self, n: int) -> bytes:
        """循环读取直到收满 n 字节。

        TCP 是字节流，单次 recv 可能返回少于请求的字节数（高并发/跨网络时尤甚）。
        SRTP 报文头固定 56 字节、数据段长度由头部 byte4 指明，长度均已知，
        因此必须读满；否则半包会导致下游解析错位或失败。

        recv 返回空 bytes 表示对端已关闭连接，抛 ConnectionError。
        socket.timeout / socket.error 不在此捕获，交由调用方 _receive_data 统一转换。
        """
        chunks = []
        remaining = n
        while remaining > 0:
            chunk = self.sock.recv(remaining)
            if not chunk:
                self.is_connected = False
                raise exceptions.ConnectionError(
                    f"Connection closed by PLC (received {n - remaining}/{n} bytes)"
                )
            chunks.append(chunk)
            remaining -= len(chunk)
        return b"".join(chunks)

    def _receive_data(self, max_bytes: int) -> bytes:
        """
        Internal method to receive data from socket.

        The PLC sends responses in multiple TCP packets:
        - First packet: 56-byte header
        - Second packet: payload data (if any)

        Byte 4 of the header indicates the payload length.

        Args:
            max_bytes: Maximum bytes to receive

        Returns:
            Received data (header + payload)

        Raises:
            TimeoutError: If receive times out
            ConnectionError: If receive fails
        """
        try:
            # First, receive the 56-byte header (循环读满，防 TCP 半包)
            header = self._recv_exact(56)

            if not header:
                self.is_connected = False
                raise exceptions.ConnectionError("Connection closed by PLC")

            logger.debug(f"Received header: {len(header)} bytes")
            # .hex() 是热点：实参恒被求值，故用 isEnabledFor 守卫，DEBUG 关时不计算
            if logger.isEnabledFor(logging.DEBUG):
                logger.debug(f"Response header: {header.hex()}")

            # Check byte 4 for payload length indicator
            payload_length = header[4] if len(header) > 4 else 0

            if payload_length > 0:
                # Receive the payload in a second packet (循环读满，防 TCP 半包)
                logger.debug(f"Expecting {payload_length} bytes of payload")
                payload = self._recv_exact(payload_length)
                logger.debug(f"Received payload: {len(payload)} bytes")

                # Combine header and payload
                data = header + payload
                logger.debug(f"Total data: {len(data)} bytes")
                return data
            else:
                # No payload, just return header
                return header

        except socket.timeout as e:
            raise exceptions.TimeoutError("Timeout receiving response") from e
        except socket.error as e:
            self.is_connected = False
            raise exceptions.ConnectionError(f"Error receiving response: {e}") from e

    def disconnect(self) -> None:
        """
        Close the TCP connection to the PLC.
        """
        if self.sock:
            try:
                self.sock.close()
                logger.info(f"Disconnected from {self.host}:{self.port}")
            except Exception as e:
                logger.error(f"Error closing socket: {e}")
            finally:
                self.sock = None
                self.is_connected = False
                self.is_initialized = False

    def is_alive(self) -> bool:
        """
        Check if connection is still alive.

        Returns:
            True if connected and initialized
        """
        return self.is_connected and self.is_initialized

    def set_timeout(self, timeout: int) -> None:
        """
        Update socket timeout.

        Args:
            timeout: New timeout in seconds
        """
        self.timeout = timeout
        if self.sock:
            self.sock.settimeout(timeout)
            logger.debug(f"Socket timeout updated to {timeout}s")

    def __enter__(self):
        """Context manager entry - connect to PLC."""
        self.connect()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit - disconnect from PLC."""
        self.disconnect()
        return False

    def __repr__(self) -> str:
        """String representation of connection."""
        status = "connected" if self.is_connected else "disconnected"
        init_status = "initialized" if self.is_initialized else "not initialized"
        return f"SRTPConnection({self.host}:{self.port}, {status}, {init_status})"
