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

Custom exceptions for the GE-SRTP PLC Driver

This module defines a hierarchy of exception classes for handling errors
that may occur during PLC communication and operations.
"""


class SRTPException(Exception):
    """Base exception class for all GE-SRTP driver errors."""
    pass


class ConnectionError(SRTPException):
    """Raised when connection to PLC fails or is lost."""
    pass


class InitializationError(ConnectionError):
    """Raised when the initialization handshake fails."""
    pass


class TimeoutError(ConnectionError):
    """Raised when a request times out waiting for response."""
    pass


class ProtocolError(SRTPException):
    """Base class for protocol-level errors."""
    pass


class InvalidPacketError(ProtocolError):
    """Raised when a packet is malformed or invalid."""
    pass


class InvalidResponseError(ProtocolError):
    """Raised when response from PLC is unexpected or malformed."""
    pass


class SequenceNumberError(ProtocolError):
    """Raised when sequence number mismatch is detected."""
    pass


class MemoryError(SRTPException):
    """Base class for memory access errors."""
    pass


class InvalidAddressError(MemoryError):
    """Raised when attempting to access an invalid memory address."""
    pass


class InvalidMemoryTypeError(MemoryError):
    """Raised when attempting to access unsupported memory type."""
    pass


class AccessModeError(MemoryError):
    """Raised when using wrong access mode for memory type."""
    pass


class MemoryRangeError(MemoryError):
    """Raised when requested memory range is out of bounds."""
    pass


class PLCError(SRTPException):
    """Base class for PLC-reported errors."""

    def __init__(self, message: str, error_code: int = 0):
        super().__init__(message)
        self.error_code = error_code


class ServiceCodeError(PLCError):
    """Raised when PLC rejects service code."""
    pass


class SegmentSelectorError(PLCError):
    """Raised when PLC rejects segment selector."""
    pass


class InsufficientPrivilegeError(PLCError):
    """Raised when operation requires higher privilege level."""
    pass


class PLCInRunModeError(PLCError):
    """Raised when operation requires PLC to be stopped."""
    pass


class MemoryProtectError(PLCError):
    """Raised when attempting to write to protected memory."""
    pass


class WriteOperationError(SRTPException):
    """Raised for errors during write operations (if implemented)."""

    def __init__(self, message: str = "Write operations are dangerous and disabled by default"):
        super().__init__(message)


class ForensicError(SRTPException):
    """Base class for forensic acquisition errors."""
    pass


class DumpError(ForensicError):
    """Raised when memory dump operation fails."""
    pass


class ExportError(ForensicError):
    """Raised when export operation fails."""
    pass


class ComparisonError(ForensicError):
    """Raised when snapshot comparison fails."""
    pass


class ValidationError(SRTPException):
    """Raised when input validation fails."""
    pass


def error_code_to_exception(error_code: int, message: str = "") -> PLCError:
    """
    Convert a PLC error code to the appropriate exception class.

    Args:
        error_code: Error code from PLC response
        message: Additional error message

    Returns:
        Appropriate exception instance
    """
    error_mapping = {
        0x01: ServiceCodeError,
        0x02: SegmentSelectorError,
        0x03: InvalidAddressError,
        0x04: MemoryRangeError,
        0x05: InsufficientPrivilegeError,
        0x06: PLCInRunModeError,
        0x07: MemoryProtectError,
        0x08: TimeoutError,
    }

    exception_class = error_mapping.get(error_code, PLCError)

    if not message:
        message = f"PLC error code: 0x{error_code:02X}"

    return exception_class(message, error_code)
