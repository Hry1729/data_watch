#!/usr/bin/env python3
"""
gesrtp-py - GE-SRTP PLC Driver
Copyright (c) 2025 Jobe Eli Eastwood
Houston, TX

Author: Jobe Eli Eastwood <jobeastwood@hotmail.com>
Project: https://github.com/jobeastwood/gesrtp-py
License: MIT

---

PLC Memory Dump Example

Performs a comprehensive memory dump of a GE Fanuc PLC, saving the results to JSON.
Useful for forensic analysis, backup, and troubleshooting.
"""

import sys
import os
import json
from datetime import datetime
from typing import Dict, Any

# Add parent directory to path
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from src.driver import GE_SRTP_Driver


def dump_registers(plc: GE_SRTP_Driver, start: int, end: int) -> Dict[str, Any]:
    """
    Dump a range of registers.

    Args:
        plc: Connected driver instance
        start: Starting register address (0-based)
        end: Ending register address (0-based, inclusive)

    Returns:
        Dictionary containing register data
    """
    print(f"  Dumping %R{start+1}-%R{end+1} (addresses {start}-{end})...", end=" ")

    count = end - start + 1
    batch_size = 125  # Max registers per request

    all_values = []

    for offset in range(0, count, batch_size):
        remaining = count - offset
        read_count = min(batch_size, remaining)
        values = plc.read_register(start + offset, count=read_count)

        if isinstance(values, int):
            all_values.append(values)
        else:
            all_values.extend(values)

    # Create register map
    register_map = {}
    for i, value in enumerate(all_values):
        addr = start + i
        reg_num = addr + 1  # 1-based for display
        register_map[f"R{reg_num}"] = {
            "address": addr,
            "value": value
        }

    print(f"✓ {len(all_values)} registers")
    return register_map


def dump_analog_io(plc: GE_SRTP_Driver, count: int) -> Dict[str, Any]:
    """
    Dump analog I/O.

    Args:
        plc: Connected driver instance
        count: Number of analog I/O points to dump

    Returns:
        Dictionary containing analog I/O data
    """
    print(f"  Dumping %AI1-%AI{count} and %AQ1-%AQ{count}...", end=" ")

    ai_values = plc.read_analog_input(0, count=count)
    aq_values = plc.read_analog_output(0, count=count)

    # Create analog I/O map
    analog_inputs = {}
    analog_outputs = {}

    for i, value in enumerate(ai_values):
        analog_inputs[f"AI{i+1}"] = {
            "address": i,
            "value": value
        }

    for i, value in enumerate(aq_values):
        analog_outputs[f"AQ{i+1}"] = {
            "address": i,
            "value": value
        }

    print(f"✓ {len(ai_values)} inputs, {len(aq_values)} outputs")

    return {
        "analog_inputs": analog_inputs,
        "analog_outputs": analog_outputs
    }


def get_plc_diagnostics(plc: GE_SRTP_Driver) -> Dict[str, Any]:
    """
    Get PLC diagnostic information.

    Args:
        plc: Connected driver instance

    Returns:
        Dictionary containing diagnostic data
    """
    print("  Gathering PLC diagnostics...", end=" ")

    diagnostics = {}

    try:
        diagnostics["status"] = plc.get_plc_status()
    except Exception as e:
        diagnostics["status"] = {"error": str(e)}

    try:
        diagnostics["controller_info"] = plc.get_controller_info()
    except Exception as e:
        diagnostics["controller_info"] = {"error": str(e)}

    try:
        diagnostics["program_names"] = plc.get_program_names()
    except Exception as e:
        diagnostics["program_names"] = {"error": str(e)}

    try:
        diagnostics["datetime"] = plc.get_plc_datetime()
    except Exception as e:
        diagnostics["datetime"] = {"error": str(e)}

    try:
        diagnostics["fault_table"] = plc.get_fault_table()
    except Exception as e:
        diagnostics["fault_table"] = {"error": str(e)}

    print("✓")
    return diagnostics


def perform_memory_dump(plc_ip: str, cpu_slot: int, output_file: str = None):
    """
    Perform complete memory dump.

    Args:
        plc_ip: PLC IP address
        cpu_slot: CPU slot number
        output_file: Output JSON file path (optional)
    """
    print("="*80)
    print("GE-SRTP Driver - Memory Dump")
    print("="*80)
    print(f"PLC: {plc_ip}")
    print(f"CPU Slot: {cpu_slot}")
    print()

    # Connect to PLC
    plc = GE_SRTP_Driver(plc_ip, slot=cpu_slot)
    plc.connect()
    print("✓ Connected to PLC\n")

    # Create dump data structure
    dump_data = {
        "metadata": {
            "plc_ip": plc_ip,
            "cpu_slot": cpu_slot,
            "dump_timestamp": datetime.now().isoformat(),
            "driver_version": "0.1.0"
        },
        "data": {}
    }

    try:
        # Dump diagnostics
        print("[1/3] Collecting diagnostics")
        dump_data["data"]["diagnostics"] = get_plc_diagnostics(plc)
        print()

        # Dump registers
        print("[2/3] Dumping register memory")
        dump_data["data"]["registers"] = dump_registers(plc, start=0, end=99)  # %R1-%R100
        print()

        # Dump analog I/O
        print("[3/3] Dumping analog I/O")
        dump_data["data"]["analog_io"] = dump_analog_io(plc, count=10)
        print()

        # Calculate statistics
        stats = {
            "registers_dumped": len(dump_data["data"]["registers"]),
            "analog_inputs_dumped": len(dump_data["data"]["analog_io"]["analog_inputs"]),
            "analog_outputs_dumped": len(dump_data["data"]["analog_io"]["analog_outputs"]),
        }
        dump_data["metadata"]["statistics"] = stats

        print("✓ Memory dump complete!\n")

        # Save to file
        if output_file is None:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            output_file = f"plc_dump_{plc_ip}_{timestamp}.json"

        with open(output_file, 'w') as f:
            json.dump(dump_data, f, indent=2)

        print(f"✓ Dump saved to: {output_file}")

        # Display summary
        print("\n" + "-"*80)
        print("Dump Summary:")
        print(f"  Registers:      {stats['registers_dumped']}")
        print(f"  Analog Inputs:  {stats['analog_inputs_dumped']}")
        print(f"  Analog Outputs: {stats['analog_outputs_dumped']}")
        print(f"  File size:      {os.path.getsize(output_file):,} bytes")
        print("-"*80)

    finally:
        plc.disconnect()
        print("\n✓ Disconnected from PLC")

    print("\n" + "="*80)
    print("Memory dump operation complete!")
    print("="*80)


def main():
    PLC_IP = "172.16.12.124"
    CPU_SLOT = 0  # EPXCPE210 in slot 0

    # Optional: customize output file
    OUTPUT_FILE = None  # None = auto-generate filename

    perform_memory_dump(PLC_IP, CPU_SLOT, OUTPUT_FILE)


if __name__ == "__main__":
    main()
