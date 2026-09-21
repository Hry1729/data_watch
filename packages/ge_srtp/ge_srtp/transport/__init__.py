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

GE-SRTP PLC Driver Package

A Python driver for communicating with GE Programmable Logic Controllers using
the proprietary GE-SRTP (Service Request Transport Protocol).

This package provides:
- Direct network-based communication with GE Fanuc PLCs
- Read operations for all memory types (%R, %I, %Q, %AI, %AQ, %M, %T, %S, %G)
- PLC status and diagnostic queries
- Forensic memory acquisition capabilities
- Non-invasive monitoring tools

Supported PLC Models:
- GE Fanuc Series 90-30
- GE Fanuc Series 90-70
- GE RX3i / RX7i
- Most GE PLCs with Ethernet and SRTP support

Example:
    ```python
    from src.driver import GE_SRTP_Driver

    # Connect to PLC
    plc = GE_SRTP_Driver('172.16.12.127', slot=2)
    plc.connect()

    # Read register (0-based addressing: %R1 = address 0)
    value = plc.read_register(0)
    print(f"%R1 = {value}")

    plc.disconnect()
    ```

WARNING:
    This protocol has minimal security by default. PLCs in default configuration
    have no authentication. Use only on isolated networks. Read operations are
    generally safe, but write operations (if implemented) can cause physical
    damage to equipment or endanger workers.
"""

__version__ = "1.1.0"
__author__ = "Jobe Eli Eastwood"
__email__ = "jobeastwood@hotmail.com"
__license__ = "MIT"

from . import protocol
from . import exceptions
from . import packet
from . import connection
from .driver import GE_SRTP_Driver

__all__ = [
    'protocol',
    'exceptions',
    'packet',
    'connection',
    'GE_SRTP_Driver',
]
