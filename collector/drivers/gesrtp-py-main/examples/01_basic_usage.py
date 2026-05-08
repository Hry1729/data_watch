#!/usr/bin/env python3
"""
gesrtp-py - GE-SRTP PLC Driver
Copyright (c) 2025 Jobe Eli Eastwood
Houston, TX

Author: Jobe Eli Eastwood <jobeastwood@hotmail.com>
Project: https://github.com/jobeastwood/gesrtp-py
License: MIT

---

Basic GE-SRTP Driver Usage Example

Demonstrates simple register reading from a GE Fanuc PLC.
"""

import sys
import os

# Add parent directory to path to import src module
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from src.driver import GE_SRTP_Driver


def main():
    # PLC connection details
    PLC_IP = "172.16.12.124"
    CPU_SLOT = 0  # CPU is in slot 0 for EPXCPE210

    print("="*60)
    print("GE-SRTP Driver - Basic Usage Example")
    print("="*60)
    print(f"PLC: {PLC_IP}")
    print(f"CPU Slot: {CPU_SLOT}")
    print()

    # Method 1: Manual connect/disconnect
    print("[Method 1] Manual Connection")
    print("-" * 60)

    plc = GE_SRTP_Driver(PLC_IP, slot=CPU_SLOT)
    plc.connect()
    print("✓ Connected to PLC")

    # Read a single register
    # IMPORTANT: Protocol uses 0-based addressing!
    # To read %R1 on the PLC, use address 0
    r1 = plc.read_register(0)  # Reads %R1
    print(f"  %R1 (address 0) = {r1}")

    r2 = plc.read_register(1)  # Reads %R2
    print(f"  %R2 (address 1) = {r2}")

    # Read multiple registers at once
    values = plc.read_register(0, count=10)  # Reads %R1 through %R10
    print(f"  %R1-%R10 (addresses 0-4) = {values}")

    plc.disconnect()
    print("✓ Disconnected")
    print()

    # Method 2: Context manager (recommended - auto-disconnect)
    print("[Method 2] Context Manager (Recommended)")
    print("-" * 60)

    with GE_SRTP_Driver(PLC_IP, slot=CPU_SLOT) as plc:
        print("✓ Connected to PLC")

        # Read register at address 0 (%R1 on PLC)
        value = plc.read_register(0)  # Reads %R1
        print(f"  %R1 (address 0) = {value}")

        # Read batch of registers
        batch = plc.read_register(0, count=10)
        print(f"  %R1-%R10 (addresses 0-9) = {batch}")

    print("✓ Auto-disconnected")
    print()

    # Method 3: Reading analog I/O
    print("[Method 3] Analog I/O")
    print("-" * 60)

    with GE_SRTP_Driver(PLC_IP, slot=CPU_SLOT) as plc:
        # Read analog inputs (0-based addressing)
        ai1 = plc.read_analog_input(0)  # %AI1 on PLC
        print(f"  %AI1 (address 0) = {ai1}")

        ai_batch = plc.read_analog_input(0, count=5)  # %AI1-%AI5
        print(f"  %AI1-%AI5 (addresses 0-4) = {ai_batch}")

        # Read analog outputs
        aq1 = plc.read_analog_output(0)  # %AQ1 on PLC
        print(f"  %AQ1 (address 0) = {aq1}")

    print()
    print("="*60)
    print("Example complete!")
    print("="*60)


if __name__ == "__main__":
    main()
