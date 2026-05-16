#!/usr/bin/env python3
"""
gesrtp-py - GE-SRTP PLC Driver
Copyright (c) 2025 Jobe Eli Eastwood
Houston, TX

Author: Jobe Eli Eastwood <jobeastwood@hotmail.com>
Project: https://github.com/jobeastwood/gesrtp-py
License: MIT

---

Basic connection test script for GE-SRTP driver.

This script tests the basic connection and register reading functionality
with the PLC at 172.16.12.124.
"""

import logging
import sys
import os

# Add parent directory to path so we can import src module
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

from src.driver import GE_SRTP_Driver

# Configure logging
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)

logger = logging.getLogger(__name__)


def test_connection():
    """Test basic connection to PLC."""
    plc_ip = "172.16.12.124"
    cpu_slot = 0  # CPU is in slot 0 (EPXCPE210)

    logger.info(f"Starting connection test to PLC at {plc_ip} (CPU slot {cpu_slot})")

    try:
        # Create driver instance with slot 2
        plc = GE_SRTP_Driver(plc_ip, slot=cpu_slot)
        logger.info("Driver instance created")

        # Connect to PLC
        logger.info("Attempting to connect...")
        plc.connect()
        logger.info("Connection successful!")

        # Test reading a single register
        logger.info("Testing read_register() for %R1...")
        value = plc.read_register(1)
        logger.info(f"SUCCESS: Read %R1 = {value}")

        # Test reading multiple registers
        logger.info("Testing batch read for %R1-R5...")
        values = plc.read_register(1, count=5)
        logger.info(f"SUCCESS: Read %R1-R5 = {values}")

        # Test PLC status
        logger.info("Testing get_plc_status()...")
        status = plc.get_plc_status()
        logger.info(f"PLC Status: {status}")

        # Disconnect
        plc.disconnect()
        logger.info("Disconnected successfully")

        logger.info("="*60)
        logger.info("ALL TESTS PASSED!")
        logger.info("="*60)

        return True

    except Exception as e:
        logger.error(f"Test failed: {e}", exc_info=True)
        return False


def test_context_manager():
    """Test using driver as context manager."""
    plc_ip = "172.16.12.124"
    cpu_slot = 0  # CPU is in slot 0 (EPXCPE210)

    logger.info(f"\nTesting context manager mode with PLC at {plc_ip} (CPU slot {cpu_slot})")

    try:
        with GE_SRTP_Driver(plc_ip, slot=cpu_slot) as plc:
            logger.info("Entered context manager - connected")

            value = plc.read_register(1)
            logger.info(f"Read %R1 = {value}")

        logger.info("Exited context manager - disconnected")
        logger.info("Context manager test PASSED!")
        return True

    except Exception as e:
        logger.error(f"Context manager test failed: {e}", exc_info=True)
        return False


if __name__ == "__main__":
    logger.info("="*60)
    logger.info("GE-SRTP Driver Connection Test")
    logger.info("="*60)

    # Run basic connection test
    result1 = test_connection()

    # Run context manager test
    result2 = test_context_manager()

    # Exit with appropriate code
    if result1 and result2:
        logger.info("\nAll tests completed successfully!")
        sys.exit(0)
    else:
        logger.error("\nSome tests failed!")
        sys.exit(1)
