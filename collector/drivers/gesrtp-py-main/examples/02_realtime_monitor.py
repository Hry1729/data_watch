#!/usr/bin/env python3
"""
gesrtp-py - GE-SRTP PLC Driver
Copyright (c) 2025 Jobe Eli Eastwood
Houston, TX

Author: Jobe Eli Eastwood <jobeastwood@hotmail.com>
Project: https://github.com/jobeastwood/gesrtp-py
License: MIT

---

Continuous PLC Monitoring Example

This example demonstrates real-time monitoring of PLC memory with in-place updates.
Values are displayed and updated on the same lines (no scrolling).
Changes are highlighted when detected.
"""

import sys
import time
import os
from datetime import datetime

# Add parent directory to path so we can import src module
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

from src.driver import GE_SRTP_Driver


def clear_screen():
    """Clear the terminal screen."""
    os.system('cls' if os.name == 'nt' else 'clear')


def move_cursor_up(lines):
    """Move cursor up N lines."""
    sys.stdout.write(f'\033[{lines}F')
    sys.stdout.flush()


def clear_line():
    """Clear current line."""
    sys.stdout.write('\033[K')
    sys.stdout.flush()


def monitor_registers(plc, count=10, interval=1.0):
    """
    Monitor register memory with in-place updates.

    Args:
        plc: Connected GE_SRTP_Driver instance
        count: Number of registers to monitor
        interval: Polling interval in seconds
    """
    clear_screen()

    print("="*80)
    print(f"REGISTER MONITOR - %R1 to %R{count}")
    print("="*80)
    print(f"Polling interval: {interval}s | Press Ctrl+C to stop")
    print("-"*80)
    print()

    previous_values = None
    iteration = 0

    # Print initial header
    print("Addr | Register | Current Value | Status")
    print("-"*80)

    # Reserve space for data lines
    for i in range(count):
        print()

    try:
        while True:
            iteration += 1

            # Read current values
            current_values = plc.read_register(0, count=count)

            # Move cursor back up to data area
            move_cursor_up(count)

            # Update each line
            for i, value in enumerate(current_values):
                clear_line()

                status = "     "
                if previous_values is not None:
                    if value > previous_values[i]:
                        status = "  ↑  "  # Increased
                    elif value < previous_values[i]:
                        status = "  ↓  "  # Decreased
                    elif value == previous_values[i]:
                        status = "  =  "  # No change

                print(f" {i:3d} |   %R{i+1:3d}  |     {value:6d}    | {status}", flush=True)

            previous_values = current_values

            # Wait before next poll
            time.sleep(interval)

    except KeyboardInterrupt:
        print("\n")
        print("="*80)
        print(f"Monitoring stopped | Total iterations: {iteration}")
        print("="*80)


def monitor_analog_io(plc, ai_count=5, aq_count=5, interval=1.0):
    """
    Monitor analog I/O with in-place updates.

    Args:
        plc: Connected GE_SRTP_Driver instance
        ai_count: Number of analog inputs to monitor
        aq_count: Number of analog outputs to monitor
        interval: Polling interval in seconds
    """
    clear_screen()

    print("="*80)
    print(f"ANALOG I/O MONITOR - %AI1-%AI{ai_count} and %AQ1-%AQ{aq_count}")
    print("="*80)
    print(f"Polling interval: {interval}s | Press Ctrl+C to stop")
    print("-"*80)
    print()

    previous_ai = None
    previous_aq = None
    iteration = 0

    # Print headers for inputs
    print("ANALOG INPUTS:")
    print("Addr | Input  | Current Value | Status")
    print("-"*40)
    # Reserve space for AI data
    for i in range(ai_count):
        print()

    # Print blank line separator
    print()

    # Print headers for outputs
    print("ANALOG OUTPUTS:")
    print("Addr | Output | Current Value | Status")
    print("-"*40)
    # Reserve space for AQ data
    for i in range(aq_count):
        print()

    # Total lines to move cursor up = all data lines + 1 separator line + 3 header lines for outputs
    total_lines = ai_count + 1 + 3 + aq_count

    try:
        while True:
            iteration += 1

            # Read current values
            current_ai = plc.read_analog_input(0, count=ai_count)
            current_aq = plc.read_analog_output(0, count=aq_count)

            # Move cursor back up to start of data area
            move_cursor_up(total_lines)

            # Update analog inputs
            for i, value in enumerate(current_ai):
                clear_line()

                status = "     "
                if previous_ai is not None:
                    if value > previous_ai[i]:
                        status = "  ↑  "
                    elif value < previous_ai[i]:
                        status = "  ↓  "
                    elif value == previous_ai[i]:
                        status = "  =  "

                print(f" {i:3d} | %AI{i+1:3d} |     {value:6d}    | {status}", flush=True)

            # Move cursor down past separator and output headers (4 lines total)
            for _ in range(4):
                print()

            # Update analog outputs
            for i, value in enumerate(current_aq):
                clear_line()

                status = "     "
                if previous_aq is not None:
                    if value > previous_aq[i]:
                        status = "  ↑  "
                    elif value < previous_aq[i]:
                        status = "  ↓  "
                    elif value == previous_aq[i]:
                        status = "  =  "

                print(f" {i:3d} | %AQ{i+1:3d} |     {value:6d}    | {status}", flush=True)

            previous_ai = current_ai
            previous_aq = current_aq

            # Wait before next poll
            time.sleep(interval)

    except KeyboardInterrupt:
        print("\n")
        print("="*80)
        print(f"Monitoring stopped | Total iterations: {iteration}")
        print("="*80)


def monitor_discrete_io(plc, count=16, interval=1.0):
    """
    Monitor discrete I/O with in-place updates (bit mode).

    Args:
        plc: Connected GE_SRTP_Driver instance
        count: Number of discrete points to monitor
        interval: Polling interval in seconds
    """
    clear_screen()

    print("="*80)
    print(f"DISCRETE I/O MONITOR - Bits 1-{count}")
    print("="*80)
    print(f"Polling interval: {interval}s | Press Ctrl+C to stop")
    print("-"*80)
    print()

    previous_i = None
    previous_q = None
    iteration = 0

    # Print headers for inputs
    print("DISCRETE INPUTS (%I):")
    print("Addr | Bit    | State | Status")
    print("-"*40)
    # Reserve space for input data
    for i in range(count):
        print()

    # Print blank line separator
    print()

    # Print headers for outputs
    print("DISCRETE OUTPUTS (%Q):")
    print("Addr | Bit    | State | Status")
    print("-"*40)
    # Reserve space for output data
    for i in range(count):
        print()

    # Total lines to move cursor up = all data lines + 1 separator line + 3 header lines for outputs
    total_lines = count + 1 + 3 + count

    try:
        while True:
            iteration += 1

            # Read current values
            current_i = plc.read_discrete_input(0, count=count, mode='bit')
            current_q = plc.read_discrete_output(0, count=count, mode='bit')

            # Move cursor back up to start of data area
            move_cursor_up(total_lines)

            # Update discrete inputs
            for i, value in enumerate(current_i):
                clear_line()

                state = "ON " if value else "OFF"
                status = "     "
                if previous_i is not None:
                    if value != previous_i[i]:
                        status = " CHG "
                    else:
                        status = "  =  "

                print(f" {i:3d} |  %I{i+1:3d} |  {state}  | {status}", flush=True)

            # Move cursor down past separator and output headers (4 lines total)
            for _ in range(4):
                print()

            # Update discrete outputs
            for i, value in enumerate(current_q):
                clear_line()

                state = "ON " if value else "OFF"
                status = "     "
                if previous_q is not None:
                    if value != previous_q[i]:
                        status = " CHG "
                    else:
                        status = "  =  "

                print(f" {i:3d} |  %Q{i+1:3d} |  {state}  | {status}", flush=True)

            previous_i = current_i
            previous_q = current_q

            # Wait before next poll
            time.sleep(interval)

    except KeyboardInterrupt:
        print("\n")
        print("="*80)
        print(f"Monitoring stopped | Total iterations: {iteration}")
        print("="*80)


def main():
    PLC_IP = "172.16.12.124"
    CPU_SLOT = 0  # EPXCPE210 in slot 0

    print("="*80)
    print("GE-SRTP Driver - Continuous Monitoring")
    print("="*80)
    print(f"PLC: {PLC_IP}")
    print(f"CPU Slot: {CPU_SLOT}")
    print("="*80)
    print()
    print("Select monitoring mode:")
    print("  1. Registers (%R)")
    print("  2. Analog I/O (%AI, %AQ)")
    print("  3. Discrete I/O (%I, %Q)")
    print()

    choice = input("Enter choice (1-3): ").strip()

    if choice not in ['1', '2', '3']:
        print("Invalid choice. Exiting.")
        return

    # Get polling interval
    try:
        interval = float(input("Polling interval in seconds (default 1.0): ").strip() or "1.0")
        if interval < 0.1:
            print("Interval too small, using 0.1s")
            interval = 0.1
    except ValueError:
        print("Invalid interval, using 1.0s")
        interval = 1.0

    # Connect to PLC
    try:
        plc = GE_SRTP_Driver(PLC_IP, slot=CPU_SLOT)
        plc.connect()
        print(f"\n✓ Connected to PLC at {PLC_IP}")
        time.sleep(1)  # Brief pause before starting monitor

        # Start appropriate monitor
        if choice == '1':
            count = int(input("Number of registers to monitor (default 10): ").strip() or "10")
            monitor_registers(plc, count=count, interval=interval)
        elif choice == '2':
            ai_count = int(input("Number of analog inputs to monitor (default 5): ").strip() or "5")
            aq_count = int(input("Number of analog outputs to monitor (default 5): ").strip() or "5")
            monitor_analog_io(plc, ai_count=ai_count, aq_count=aq_count, interval=interval)
        elif choice == '3':
            count = int(input("Number of discrete bits to monitor (default 16): ").strip() or "16")
            monitor_discrete_io(plc, count=count, interval=interval)

        # Disconnect
        plc.disconnect()
        print("\n✓ Disconnected from PLC")

    except Exception as e:
        print(f"\n✗ Error: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    main()
