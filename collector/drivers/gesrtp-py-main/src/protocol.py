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

GE-SRTP Protocol Constants and Definitions

This module contains all protocol-level constants for the GE Service Request
Transport Protocol (SRTP), including service codes, segment selectors, message
types, and other protocol-specific values.

Reference: DFRWS 2017 paper - "Leveraging the SRTP protocol for over-the-network
memory acquisition of a GE Fanuc Series 90-30"
"""

from enum import IntEnum
from typing import Final


# Protocol Configuration
DEFAULT_PORT: Final[int] = 18245
HEADER_SIZE: Final[int] = 56
DEFAULT_TIMEOUT: Final[int] = 5  # seconds


# Packet Type Codes (Byte 0)
class PacketType(IntEnum):
    """Packet type identifier"""
    REQUEST = 0x02
    RESPONSE = 0x03


# Message Type Codes (Byte 31)
class MessageType(IntEnum):
    """Message type in packet header"""
    REQUEST = 0xC0
    ACK = 0xD4
    ACK_WITH_DATA = 0x94  # Response with data payload
    ERROR = 0xD1


# Service Request Codes (Byte 42)
class ServiceCode(IntEnum):
    """Service request codes for PLC operations"""
    # Status and Information
    PLC_SHORT_STATUS = 0x00
    RETURN_CONTROL_PROGRAM_NAMES = 0x03

    # Memory Read Operations
    READ_SYSTEM_MEMORY = 0x04
    READ_TASK_MEMORY = 0x05
    READ_PROGRAM_MEMORY = 0x06

    # Memory Write Operations (DANGEROUS - Use with extreme caution)
    WRITE_SYSTEM_MEMORY = 0x07
    WRITE_TASK_MEMORY = 0x08
    WRITE_PROGRAM_BLOCK_MEMORY = 0x09

    # PLC Management
    PROGRAMMER_LOGON = 0x20
    CHANGE_PLC_CPU_PRIVILEGE_LEVEL = 0x21
    SET_CONTROL_ID = 0x22
    SET_PLC_STATE = 0x23  # RUN/STOP
    SET_PLC_DATETIME = 0x24
    RETURN_PLC_DATETIME = 0x25

    # Fault Management
    RETURN_FAULT_TABLE = 0x38
    CLEAR_FAULT_TABLE = 0x39

    # Program Management
    PROGRAM_STORE = 0x3F  # Upload from PLC
    PROGRAM_LOAD = 0x40   # Download to PLC

    # System Information
    RETURN_CONTROLLER_TYPE_AND_ID = 0x43
    TOGGLE_FORCE_SYSTEM_MEMORY = 0x44


# Segment Selectors (Byte 43) - Memory Access Types
class SegmentSelector(IntEnum):
    """Segment selectors for different memory types and access modes"""

    # WORD ACCESS (Register/Analog Memory) - 16-bit values
    REGISTERS_WORD = 0x08          # %R - Register memory
    ANALOG_INPUTS_WORD = 0x0A      # %AI - Analog input memory
    ANALOG_OUTPUTS_WORD = 0x0C     # %AQ - Analog output memory

    # BYTE ACCESS (Discrete Memory as bytes)
    DISCRETE_INPUTS_BYTE = 0x10    # %I - Discrete inputs
    DISCRETE_TEMPS_BYTE = 0x14     # %T - Discrete temporaries
    DISCRETE_OUTPUTS_BYTE = 0x12   # %Q - Discrete outputs
    DISCRETE_INTERNALS_BYTE = 0x16 # %M - Discrete internals
    SYSTEM_A_DISCRETE_BYTE = 0x18  # %SA - System A discrete
    SYSTEM_B_DISCRETE_BYTE = 0x1A  # %SB - System B discrete
    SYSTEM_C_DISCRETE_BYTE = 0x1C  # %SC - System C discrete
    SYSTEM_S_DISCRETE_BYTE = 0x1E  # %S - System S discrete
    GENIUS_GLOBAL_DATA_BYTE = 0x38 # %G - Genius global data

    # BIT ACCESS (Discrete Memory as individual bits)
    DISCRETE_INPUTS_BIT = 0x46     # %I - Discrete inputs
    DISCRETE_OUTPUTS_BIT = 0x48    # %Q - Discrete outputs
    DISCRETE_TEMPS_BIT = 0x4A      # %T - Discrete temporaries
    DISCRETE_INTERNALS_BIT = 0x4C  # %M - Discrete internals
    SYSTEM_A_DISCRETE_BIT = 0x4E   # %SA - System A discrete
    SYSTEM_B_DISCRETE_BIT = 0x50   # %SB - System B discrete
    SYSTEM_C_DISCRETE_BIT = 0x52   # %SC - System C discrete
    SYSTEM_S_DISCRETE_BIT = 0x54   # %S - System S discrete
    GENIUS_GLOBAL_DATA_BIT = 0x56  # %G - Genius global data


# Memory Type Descriptions
class MemoryType(IntEnum):
    """PLC memory type categories"""
    REGISTER = 0        # %R - 16-bit signed integers
    ANALOG_INPUT = 1    # %AI - 16-bit analog input values
    ANALOG_OUTPUT = 2   # %AQ - 16-bit analog output values
    DISCRETE_INPUT = 3  # %I - Digital inputs
    DISCRETE_OUTPUT = 4 # %Q - Digital outputs
    DISCRETE_TEMP = 5   # %T - Temporary discrete (volatile)
    DISCRETE_INTERNAL = 6  # %M - Internal coils/flags
    SYSTEM_A = 7        # %SA - System A memory
    SYSTEM_B = 8        # %SB - System B memory
    SYSTEM_C = 9        # %SC - System C memory
    SYSTEM_S = 10       # %S - System S memory
    GENIUS_GLOBAL = 11  # %G - Genius global data


# Mailbox Addresses (Bytes 32-39)
MAILBOX_SOURCE: Final[bytes] = bytes([0x00, 0x00, 0x00, 0x00])
MAILBOX_DESTINATION: Final[bytes] = bytes([0x10, 0x0E, 0x00, 0x00])  # Default (slot 1)

def get_mailbox_destination(slot: int = 1) -> bytes:
    """
    Generate mailbox destination address for a specific PLC slot.

    Args:
        slot: CPU slot number (typically 1-8)

    Returns:
        4-byte mailbox destination address

    Note:
        Format is [rack, slot, port, reserved]
        For most GE PLCs: rack=0, port=0
    """
    # GE SRTP mailbox format: rack=0x00, slot=(slot-1)*2+0x0E, port=0x00, reserved=0x00
    # Slot 1: 0x10 0x0E 0x00 0x00 (default)
    # Slot 2: 0x20 0x0E 0x00 0x00
    return bytes([slot * 0x10, 0x0E, 0x00, 0x00])


# Initialization Sequences
# First initialization packet - critical for establishing connection
# Per DFRWS 2017 paper (page 3): "initialize bit streams of all zeros were exchanged"
# between the master device (HMI) and the PLC slave to start communication
INIT_PACKET_1: Final[bytes] = bytes(56)  # All zeros - 56 bytes of 0x00


# PLC State Codes (for SET_PLC_STATE service)
class PLCState(IntEnum):
    """PLC operational states"""
    STOP = 0x00
    RUN = 0x01
    PAUSE = 0x02  # If supported by model


# Privilege Levels (for CHANGE_PLC_CPU_PRIVILEGE_LEVEL)
class PrivilegeLevel(IntEnum):
    """PLC access privilege levels"""
    LEVEL_1 = 0x01  # Read system/task/program memory
    LEVEL_2 = 0x02  # + Write, toggle force, clear faults, set time, change state
    LEVEL_3 = 0x03  # + Set controller ID, upload programs
    LEVEL_4 = 0x04  # Firmware dependent features


# Error Codes (commonly encountered in responses)
class ErrorCode(IntEnum):
    """Common error codes in SRTP responses"""
    SUCCESS = 0x00
    INVALID_SERVICE_CODE = 0x01
    INVALID_SEGMENT_SELECTOR = 0x02
    INVALID_ADDRESS = 0x03
    INVALID_LENGTH = 0x04
    INSUFFICIENT_PRIVILEGE = 0x05
    PLC_IN_RUN_MODE = 0x06
    MEMORY_PROTECT = 0x07
    TIMEOUT = 0x08


# Byte Ordering
BYTE_ORDER: Final[str] = 'little'  # GE-SRTP uses little-endian


def get_memory_type_name(mem_type: MemoryType) -> str:
    """
    Get the PLC memory type name string.

    Args:
        mem_type: Memory type enumeration value

    Returns:
        String representation (e.g., "%R", "%I", "%Q")
    """
    mapping = {
        MemoryType.REGISTER: "%R",
        MemoryType.ANALOG_INPUT: "%AI",
        MemoryType.ANALOG_OUTPUT: "%AQ",
        MemoryType.DISCRETE_INPUT: "%I",
        MemoryType.DISCRETE_OUTPUT: "%Q",
        MemoryType.DISCRETE_TEMP: "%T",
        MemoryType.DISCRETE_INTERNAL: "%M",
        MemoryType.SYSTEM_A: "%SA",
        MemoryType.SYSTEM_B: "%SB",
        MemoryType.SYSTEM_C: "%SC",
        MemoryType.SYSTEM_S: "%S",
        MemoryType.GENIUS_GLOBAL: "%G",
    }
    return mapping.get(mem_type, "UNKNOWN")


def get_segment_selector_for_memory_type(
    mem_type: MemoryType,
    access_mode: str = 'word'
) -> SegmentSelector:
    """
    Get the appropriate segment selector for a memory type and access mode.

    Args:
        mem_type: Memory type to access
        access_mode: 'word', 'byte', or 'bit'

    Returns:
        Appropriate segment selector

    Raises:
        ValueError: If invalid combination of memory type and access mode
    """
    if access_mode == 'word':
        mapping = {
            MemoryType.REGISTER: SegmentSelector.REGISTERS_WORD,
            MemoryType.ANALOG_INPUT: SegmentSelector.ANALOG_INPUTS_WORD,
            MemoryType.ANALOG_OUTPUT: SegmentSelector.ANALOG_OUTPUTS_WORD,
        }
        if mem_type not in mapping:
            raise ValueError(
                f"Memory type {get_memory_type_name(mem_type)} does not support word access"
            )
        return mapping[mem_type]

    elif access_mode == 'byte':
        mapping = {
            MemoryType.DISCRETE_INPUT: SegmentSelector.DISCRETE_INPUTS_BYTE,
            MemoryType.DISCRETE_OUTPUT: SegmentSelector.DISCRETE_OUTPUTS_BYTE,
            MemoryType.DISCRETE_TEMP: SegmentSelector.DISCRETE_TEMPS_BYTE,
            MemoryType.DISCRETE_INTERNAL: SegmentSelector.DISCRETE_INTERNALS_BYTE,
            MemoryType.SYSTEM_A: SegmentSelector.SYSTEM_A_DISCRETE_BYTE,
            MemoryType.SYSTEM_B: SegmentSelector.SYSTEM_B_DISCRETE_BYTE,
            MemoryType.SYSTEM_C: SegmentSelector.SYSTEM_C_DISCRETE_BYTE,
            MemoryType.SYSTEM_S: SegmentSelector.SYSTEM_S_DISCRETE_BYTE,
            MemoryType.GENIUS_GLOBAL: SegmentSelector.GENIUS_GLOBAL_DATA_BYTE,
        }
        if mem_type not in mapping:
            raise ValueError(
                f"Memory type {get_memory_type_name(mem_type)} does not support byte access"
            )
        return mapping[mem_type]

    elif access_mode == 'bit':
        mapping = {
            MemoryType.DISCRETE_INPUT: SegmentSelector.DISCRETE_INPUTS_BIT,
            MemoryType.DISCRETE_OUTPUT: SegmentSelector.DISCRETE_OUTPUTS_BIT,
            MemoryType.DISCRETE_TEMP: SegmentSelector.DISCRETE_TEMPS_BIT,
            MemoryType.DISCRETE_INTERNAL: SegmentSelector.DISCRETE_INTERNALS_BIT,
            MemoryType.SYSTEM_A: SegmentSelector.SYSTEM_A_DISCRETE_BIT,
            MemoryType.SYSTEM_B: SegmentSelector.SYSTEM_B_DISCRETE_BIT,
            MemoryType.SYSTEM_C: SegmentSelector.SYSTEM_C_DISCRETE_BIT,
            MemoryType.SYSTEM_S: SegmentSelector.SYSTEM_S_DISCRETE_BIT,
            MemoryType.GENIUS_GLOBAL: SegmentSelector.GENIUS_GLOBAL_DATA_BIT,
        }
        if mem_type not in mapping:
            raise ValueError(
                f"Memory type {get_memory_type_name(mem_type)} does not support bit access"
            )
        return mapping[mem_type]

    else:
        raise ValueError(f"Invalid access mode: {access_mode}. Must be 'word', 'byte', or 'bit'")
