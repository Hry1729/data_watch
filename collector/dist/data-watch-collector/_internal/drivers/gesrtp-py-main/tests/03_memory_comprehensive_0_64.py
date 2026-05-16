#!/usr/bin/env python3
"""
gesrtp-py - GE-SRTP PLC Driver
Copyright (c) 2025 Jobe Eli Eastwood
Houston, TX

Author: Jobe Eli Eastwood <jobeastwood@hotmail.com>
Project: https://github.com/jobeastwood/gesrtp-py
License: MIT

---

Comprehensive memory test for addresses 0-64.
Tests all memory types with extended address range on clean PLC.
"""

import logging
import sys
import os
from datetime import datetime

# Add parent directory to path so we can import src module
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

from src.driver import GE_SRTP_Driver

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


class MemoryTester:
    """Comprehensive memory testing class."""

    def __init__(self, plc_ip: str, cpu_slot: int):
        self.plc_ip = plc_ip
        self.cpu_slot = cpu_slot
        self.plc = None
        self.results = {
            'registers': {},
            'analog_input': {},
            'analog_output': {},
            'discrete_input_bit': {},
            'discrete_input_byte': {},
            'discrete_output_bit': {},
            'discrete_output_byte': {},
            'internal_bit': {},
            'internal_byte': {},
            'temp_bit': {},
            'temp_byte': {},
            'system_s_bit': {},
            'system_s_byte': {},
            'global_bit': {},
            'global_byte': {}
        }

    def connect(self):
        """Connect to PLC."""
        logger.info(f"Connecting to PLC at {self.plc_ip} (slot {self.cpu_slot})")
        self.plc = GE_SRTP_Driver(self.plc_ip, slot=self.cpu_slot)
        self.plc.connect()
        logger.info("Connected successfully")

    def disconnect(self):
        """Disconnect from PLC."""
        if self.plc:
            self.plc.disconnect()
            logger.info("Disconnected")

    def test_registers(self, start=0, end=64):
        """Test register memory (%R) addresses."""
        print("\n" + "="*80)
        print(f"TESTING REGISTERS (%R) - Addresses {start} to {end}")
        print("="*80)

        success_count = 0
        error_count = 0

        for addr in range(start, end + 1):
            try:
                value = self.plc.read_register(addr)
                self.results['registers'][addr] = value
                print(f"  %R{addr+1} (addr {addr}): {value}")
                success_count += 1
            except Exception as e:
                self.results['registers'][addr] = f"ERROR: {str(e)}"
                print(f"  %R{addr+1} (addr {addr}): ERROR - {e}")
                error_count += 1

        print(f"\n✓ Success: {success_count}/{end-start+1}")
        if error_count > 0:
            print(f"✗ Errors: {error_count}")
        return success_count, error_count

    def test_analog_input(self, start=0, end=64):
        """Test analog input memory (%AI) addresses."""
        print("\n" + "="*80)
        print(f"TESTING ANALOG INPUT (%AI) - Addresses {start} to {end}")
        print("="*80)

        success_count = 0
        error_count = 0

        for addr in range(start, end + 1):
            try:
                value = self.plc.read_analog_input(addr)
                self.results['analog_input'][addr] = value
                print(f"  %AI{addr+1} (addr {addr}): {value}")
                success_count += 1
            except Exception as e:
                self.results['analog_input'][addr] = f"ERROR: {str(e)}"
                print(f"  %AI{addr+1} (addr {addr}): ERROR - {e}")
                error_count += 1

        print(f"\n✓ Success: {success_count}/{end-start+1}")
        if error_count > 0:
            print(f"✗ Errors: {error_count}")
        return success_count, error_count

    def test_analog_output(self, start=0, end=64):
        """Test analog output memory (%AQ) addresses."""
        print("\n" + "="*80)
        print(f"TESTING ANALOG OUTPUT (%AQ) - Addresses {start} to {end}")
        print("="*80)

        success_count = 0
        error_count = 0

        for addr in range(start, end + 1):
            try:
                value = self.plc.read_analog_output(addr)
                self.results['analog_output'][addr] = value
                print(f"  %AQ{addr+1} (addr {addr}): {value}")
                success_count += 1
            except Exception as e:
                self.results['analog_output'][addr] = f"ERROR: {str(e)}"
                print(f"  %AQ{addr+1} (addr {addr}): ERROR - {e}")
                error_count += 1

        print(f"\n✓ Success: {success_count}/{end-start+1}")
        if error_count > 0:
            print(f"✗ Errors: {error_count}")
        return success_count, error_count

    def test_discrete_input(self, start=0, end=64):
        """Test discrete input memory (%I) in both bit and byte modes."""
        print("\n" + "="*80)
        print(f"TESTING DISCRETE INPUT (%I) - Addresses {start} to {end}")
        print("="*80)

        # Test bit mode
        print("\n--- Bit Mode ---")
        success_bit = 0
        error_bit = 0

        for addr in range(start, end + 1):
            try:
                value = self.plc.read_discrete_input(addr, mode='bit')
                self.results['discrete_input_bit'][addr] = value
                print(f"  %I{addr+1} (addr {addr}): {value}")
                success_bit += 1
            except Exception as e:
                self.results['discrete_input_bit'][addr] = f"ERROR: {str(e)}"
                print(f"  %I{addr+1} (addr {addr}): ERROR - {e}")
                error_bit += 1

        # Test byte mode
        print("\n--- Byte Mode ---")
        success_byte = 0
        error_byte = 0

        for addr in range(start, min(end + 1, 16)):  # Limit byte test to first 16
            try:
                value = self.plc.read_discrete_input(addr, count=8, mode='byte')
                self.results['discrete_input_byte'][addr] = value
                print(f"  %I bytes starting at {addr}: {[hex(v) for v in value]}")
                success_byte += 1
            except Exception as e:
                self.results['discrete_input_byte'][addr] = f"ERROR: {str(e)}"
                print(f"  %I bytes starting at {addr}: ERROR - {e}")
                error_byte += 1

        print(f"\n✓ Bit mode success: {success_bit}/{end-start+1}")
        print(f"✓ Byte mode success: {success_byte}/16")
        if error_bit > 0 or error_byte > 0:
            print(f"✗ Total errors: {error_bit + error_byte}")
        return success_bit + success_byte, error_bit + error_byte

    def test_discrete_output(self, start=0, end=64):
        """Test discrete output memory (%Q) in both bit and byte modes."""
        print("\n" + "="*80)
        print(f"TESTING DISCRETE OUTPUT (%Q) - Addresses {start} to {end}")
        print("="*80)

        # Test bit mode
        print("\n--- Bit Mode ---")
        success_bit = 0
        error_bit = 0

        for addr in range(start, end + 1):
            try:
                value = self.plc.read_discrete_output(addr, mode='bit')
                self.results['discrete_output_bit'][addr] = value
                print(f"  %Q{addr+1} (addr {addr}): {value}")
                success_bit += 1
            except Exception as e:
                self.results['discrete_output_bit'][addr] = f"ERROR: {str(e)}"
                print(f"  %Q{addr+1} (addr {addr}): ERROR - {e}")
                error_bit += 1

        # Test byte mode
        print("\n--- Byte Mode ---")
        success_byte = 0
        error_byte = 0

        for addr in range(start, min(end + 1, 16)):  # Limit byte test to first 16
            try:
                value = self.plc.read_discrete_output(addr, count=8, mode='byte')
                self.results['discrete_output_byte'][addr] = value
                print(f"  %Q bytes starting at {addr}: {[hex(v) for v in value]}")
                success_byte += 1
            except Exception as e:
                self.results['discrete_output_byte'][addr] = f"ERROR: {str(e)}"
                print(f"  %Q bytes starting at {addr}: ERROR - {e}")
                error_byte += 1

        print(f"\n✓ Bit mode success: {success_bit}/{end-start+1}")
        print(f"✓ Byte mode success: {success_byte}/16")
        if error_bit > 0 or error_byte > 0:
            print(f"✗ Total errors: {error_bit + error_byte}")
        return success_bit + success_byte, error_bit + error_byte

    def test_internal_memory(self, start=0, end=64):
        """Test internal memory (%M) in both bit and byte modes."""
        print("\n" + "="*80)
        print(f"TESTING INTERNAL MEMORY (%M) - Addresses {start} to {end}")
        print("="*80)

        # Test bit mode
        print("\n--- Bit Mode ---")
        success_bit = 0
        error_bit = 0

        for addr in range(start, end + 1):
            try:
                value = self.plc.read_discrete_internal(addr, mode='bit')
                self.results['internal_bit'][addr] = value
                print(f"  %M{addr+1} (addr {addr}): {value}")
                success_bit += 1
            except Exception as e:
                self.results['internal_bit'][addr] = f"ERROR: {str(e)}"
                print(f"  %M{addr+1} (addr {addr}): ERROR - {e}")
                error_bit += 1

        # Test byte mode
        print("\n--- Byte Mode ---")
        success_byte = 0
        error_byte = 0

        for addr in range(start, min(end + 1, 16)):  # Limit byte test to first 16
            try:
                value = self.plc.read_discrete_internal(addr, count=8, mode='byte')
                self.results['internal_byte'][addr] = value
                print(f"  %M bytes starting at {addr}: {[hex(v) for v in value]}")
                success_byte += 1
            except Exception as e:
                self.results['internal_byte'][addr] = f"ERROR: {str(e)}"
                print(f"  %M bytes starting at {addr}: ERROR - {e}")
                error_byte += 1

        print(f"\n✓ Bit mode success: {success_bit}/{end-start+1}")
        print(f"✓ Byte mode success: {success_byte}/16")
        if error_bit > 0 or error_byte > 0:
            print(f"✗ Total errors: {error_bit + error_byte}")
        return success_bit + success_byte, error_bit + error_byte

    def test_plc_diagnostics(self):
        """Test PLC diagnostic functions."""
        print("\n" + "="*80)
        print("TESTING PLC DIAGNOSTICS")
        print("="*80)

        try:
            print("\n--- PLC Status ---")
            status = self.plc.get_plc_status()
            print(f"Status: {status}")

            print("\n--- Controller Info ---")
            info = self.plc.get_controller_info()
            print(f"Controller: {info}")

            print("\n--- Program Names ---")
            programs = self.plc.get_program_names()
            print(f"Programs: {programs}")

            print("\n--- PLC Date/Time ---")
            datetime_info = self.plc.get_plc_datetime()
            print(f"Date/Time: {datetime_info}")

            print("\n✓ All diagnostic functions working")
            return True
        except Exception as e:
            print(f"\n✗ Diagnostic test failed: {e}")
            logger.exception("Diagnostic test error")
            return False

    def print_summary(self):
        """Print test summary."""
        print("\n" + "="*80)
        print("TEST SUMMARY")
        print("="*80)

        total_tests = 0
        total_success = 0

        for memory_type, data in self.results.items():
            if data:
                success = len([v for v in data.values() if not isinstance(v, str) or not v.startswith('ERROR')])
                total = len(data)
                total_tests += total
                total_success += success
                status = "✓" if success == total else "⚠"
                print(f"{status} {memory_type}: {success}/{total} successful")

        print(f"\n{'='*80}")
        print(f"TOTAL: {total_success}/{total_tests} tests passed")
        print(f"Success Rate: {(total_success/total_tests*100):.1f}%")
        print(f"{'='*80}")


def main():
    """Run comprehensive memory tests."""
    PLC_IP = "172.16.12.124"
    CPU_SLOT = 0  # EPXCPE210 in slot 0

    print("="*80)
    print("COMPREHENSIVE MEMORY TEST - Addresses 0-64")
    print("="*80)
    print(f"PLC: {PLC_IP}")
    print(f"CPU Slot: {CPU_SLOT}")
    print(f"Test Start: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print("="*80)

    tester = MemoryTester(PLC_IP, CPU_SLOT)

    try:
        # Connect
        tester.connect()

        # Run diagnostic tests first
        tester.test_plc_diagnostics()

        # Test all memory types
        tester.test_registers(0, 64)
        tester.test_analog_input(0, 64)
        tester.test_analog_output(0, 64)
        tester.test_discrete_input(0, 64)
        tester.test_discrete_output(0, 64)
        tester.test_internal_memory(0, 64)

        # Print summary
        tester.print_summary()

        print(f"\nTest Complete: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")

    except KeyboardInterrupt:
        print("\n\nTest interrupted by user")
    except Exception as e:
        logger.error(f"Test failed: {e}", exc_info=True)
        sys.exit(1)
    finally:
        tester.disconnect()


if __name__ == "__main__":
    main()
