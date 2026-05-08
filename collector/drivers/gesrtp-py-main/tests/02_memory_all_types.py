#!/usr/bin/env python3
"""
gesrtp-py - GE-SRTP PLC Driver
Copyright (c) 2025 Jobe Eli Eastwood
Houston, TX

Author: Jobe Eli Eastwood <jobeastwood@hotmail.com>
Project: https://github.com/jobeastwood/gesrtp-py
License: MIT

---

Test all memory types against the GE Fanuc Series 90-30 PLC.
Tests analog I/O, discrete I/O, internal/temp memory, and system memory.
"""

import logging
import sys
import os

# Add parent directory to path so we can import src module
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

from src.driver import GE_SRTP_Driver

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


def test_analog_io(plc):
    """Test analog input and output operations."""
    print("\n" + "="*60)
    print("TESTING ANALOG I/O (%AI, %AQ)")
    print("="*60)

    # Test analog inputs
    print("\nTesting Analog Inputs (%AI):")
    try:
        ai1 = plc.read_analog_input(0)  # %AI1 (0-based addressing)
        print(f"  %AI1 (address 0) = {ai1}")

        # Batch read
        ai_batch = plc.read_analog_input(0, count=5)
        print(f"  %AI1-AI5 (addresses 0-4) = {ai_batch}")

        print("  ✓ Analog inputs working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("Analog input test failed")

    # Test analog outputs
    print("\nTesting Analog Outputs (%AQ):")
    try:
        aq1 = plc.read_analog_output(0)  # %AQ1 (0-based addressing)
        print(f"  %AQ1 (address 0) = {aq1}")

        # Batch read
        aq_batch = plc.read_analog_output(0, count=5)
        print(f"  %AQ1-AQ5 (addresses 0-4) = {aq_batch}")

        print("  ✓ Analog outputs working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("Analog output test failed")


def test_discrete_io(plc):
    """Test discrete input and output operations."""
    print("\n" + "="*60)
    print("TESTING DISCRETE I/O (%I, %Q)")
    print("="*60)

    # Test discrete inputs - BIT mode
    print("\nTesting Discrete Inputs (%I) - Bit Mode:")
    try:
        i1_bit = plc.read_discrete_input(0, mode='bit')  # %I1
        print(f"  %I1 (address 0, bit mode) = {i1_bit}")

        # Batch read
        i_batch_bit = plc.read_discrete_input(0, count=8, mode='bit')
        print(f"  %I1-I8 (addresses 0-7, bit mode) = {i_batch_bit}")

        print("  ✓ Discrete inputs (bit mode) working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("Discrete input (bit) test failed")

    # Test discrete inputs - BYTE mode
    print("\nTesting Discrete Inputs (%I) - Byte Mode:")
    try:
        i1_byte = plc.read_discrete_input(0, mode='byte')
        print(f"  %I1-I8 (address 0, byte mode) = 0x{i1_byte:02X}")

        # Batch read
        i_batch_byte = plc.read_discrete_input(0, count=4, mode='byte')
        print(f"  First 4 bytes = {[f'0x{b:02X}' for b in i_batch_byte]}")

        print("  ✓ Discrete inputs (byte mode) working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("Discrete input (byte) test failed")

    # Test discrete outputs - BIT mode
    print("\nTesting Discrete Outputs (%Q) - Bit Mode:")
    try:
        q1_bit = plc.read_discrete_output(0, mode='bit')  # %Q1
        print(f"  %Q1 (address 0, bit mode) = {q1_bit}")

        # Batch read
        q_batch_bit = plc.read_discrete_output(0, count=8, mode='bit')
        print(f"  %Q1-Q8 (addresses 0-7, bit mode) = {q_batch_bit}")

        print("  ✓ Discrete outputs (bit mode) working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("Discrete output (bit) test failed")

    # Test discrete outputs - BYTE mode
    print("\nTesting Discrete Outputs (%Q) - Byte Mode:")
    try:
        q1_byte = plc.read_discrete_output(0, mode='byte')
        print(f"  %Q1-Q8 (address 0, byte mode) = 0x{q1_byte:02X}")

        # Batch read
        q_batch_byte = plc.read_discrete_output(0, count=4, mode='byte')
        print(f"  First 4 bytes = {[f'0x{b:02X}' for b in q_batch_byte]}")

        print("  ✓ Discrete outputs (byte mode) working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("Discrete output (byte) test failed")


def test_internal_temp_memory(plc):
    """Test internal and temporary memory."""
    print("\n" + "="*60)
    print("TESTING INTERNAL/TEMP MEMORY (%M, %T)")
    print("="*60)

    # Test internal memory - BIT mode
    print("\nTesting Internal Memory (%M) - Bit Mode:")
    try:
        m1_bit = plc.read_discrete_internal(0, mode='bit')  # %M1
        print(f"  %M1 (address 0, bit mode) = {m1_bit}")

        # Batch read
        m_batch_bit = plc.read_discrete_internal(0, count=8, mode='bit')
        print(f"  %M1-M8 (addresses 0-7, bit mode) = {m_batch_bit}")

        print("  ✓ Internal memory (bit mode) working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("Internal memory (bit) test failed")

    # Test internal memory - BYTE mode
    print("\nTesting Internal Memory (%M) - Byte Mode:")
    try:
        m1_byte = plc.read_discrete_internal(0, mode='byte')
        print(f"  %M1-M8 (address 0, byte mode) = 0x{m1_byte:02X}")

        # Batch read
        m_batch_byte = plc.read_discrete_internal(0, count=4, mode='byte')
        print(f"  First 4 bytes = {[f'0x{b:02X}' for b in m_batch_byte]}")

        print("  ✓ Internal memory (byte mode) working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("Internal memory (byte) test failed")

    # Test temporary memory - BIT mode
    print("\nTesting Temporary Memory (%T) - Bit Mode:")
    try:
        t1_bit = plc.read_discrete_temp(0, mode='bit')  # %T1
        print(f"  %T1 (address 0, bit mode) = {t1_bit}")

        # Batch read
        t_batch_bit = plc.read_discrete_temp(0, count=8, mode='bit')
        print(f"  %T1-T8 (addresses 0-7, bit mode) = {t_batch_bit}")

        print("  ✓ Temporary memory (bit mode) working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("Temporary memory (bit) test failed")

    # Test temporary memory - BYTE mode
    print("\nTesting Temporary Memory (%T) - Byte Mode:")
    try:
        t1_byte = plc.read_discrete_temp(0, mode='byte')
        print(f"  %T1-T8 (address 0, byte mode) = 0x{t1_byte:02X}")

        # Batch read
        t_batch_byte = plc.read_discrete_temp(0, count=4, mode='byte')
        print(f"  First 4 bytes = {[f'0x{b:02X}' for b in t_batch_byte]}")

        print("  ✓ Temporary memory (byte mode) working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("Temporary memory (byte) test failed")


def test_system_memory(plc):
    """Test system memory."""
    print("\n" + "="*60)
    print("TESTING SYSTEM MEMORY (%S, %SA, %SB, %SC)")
    print("="*60)

    # Test system memory - BIT mode
    print("\nTesting System Memory (%S) - Bit Mode:")
    try:
        s1_bit = plc.read_system_memory(mem_type='S', address=0, mode='bit')  # %S1
        print(f"  %S1 (address 0, bit mode) = {s1_bit}")

        # Batch read
        s_batch_bit = plc.read_system_memory(mem_type='S', address=0, count=8, mode='bit')
        print(f"  %S1-S8 (addresses 0-7, bit mode) = {s_batch_bit}")

        print("  ✓ System memory %S (bit mode) working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("System memory %S (bit) test failed")

    # Test system memory - BYTE mode
    print("\nTesting System Memory (%S) - Byte Mode:")
    try:
        s1_byte = plc.read_system_memory(mem_type='S', address=0, mode='byte')
        print(f"  %S1-S8 (address 0, byte mode) = 0x{s1_byte:02X}")

        # Batch read
        s_batch_byte = plc.read_system_memory(mem_type='S', address=0, count=4, mode='byte')
        print(f"  First 4 bytes = {[f'0x{b:02X}' for b in s_batch_byte]}")

        print("  ✓ System memory %S (byte mode) working!")
    except Exception as e:
        print(f"  ✗ ERROR: {e}")
        logger.exception("System memory %S (byte) test failed")

    # Test system memory %SA, %SB, %SC
    for mem_type in ['SA', 'SB', 'SC']:
        print(f"\nTesting System Memory (%{mem_type}) - Byte Mode:")
        try:
            val = plc.read_system_memory(mem_type=mem_type, address=0, mode='byte')
            print(f"  %{mem_type}1-8 (address 0, byte mode) = 0x{val:02X}")
            print(f"  ✓ System memory %{mem_type} working!")
        except Exception as e:
            print(f"  ✗ ERROR: {e}")
            logger.exception(f"System memory %{mem_type} test failed")


def main():
    """Run all memory type tests."""
    plc_ip = "172.16.12.124"
    cpu_slot = 0  # EPXCPE210 in slot 0

    print("="*60)
    print("GE-SRTP MEMORY TYPE TESTING")
    print("="*60)
    print(f"PLC: {plc_ip}")
    print(f"CPU Slot: {cpu_slot}")
    print(f"Protocol: GE-SRTP")
    print("="*60)

    try:
        # Connect to PLC
        plc = GE_SRTP_Driver(plc_ip, slot=cpu_slot)
        plc.connect()
        print("\n✓ Connected to PLC")

        # Run all tests
        test_analog_io(plc)
        test_discrete_io(plc)
        test_internal_temp_memory(plc)
        test_system_memory(plc)

        # Disconnect
        plc.disconnect()
        print("\n" + "="*60)
        print("✓ All tests completed")
        print("="*60)

    except KeyboardInterrupt:
        print("\n\n⚠ Tests interrupted by user")
    except Exception as e:
        print(f"\n\n✗ FATAL ERROR: {e}")
        logger.exception("Test suite failed")


if __name__ == "__main__":
    main()
