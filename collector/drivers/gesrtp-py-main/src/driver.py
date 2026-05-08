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

GE-SRTP PLC Driver - Main Driver Class

This module provides the main driver class for communicating with GE PLCs
using the SRTP protocol. It implements read operations for all memory types
and PLC management functions.

WARNING: Write operations are intentionally NOT implemented for safety.
         This is a READ-ONLY driver focused on forensic acquisition.
"""

import logging
from typing import List, Dict, Any, Union, Optional
from datetime import datetime

from .connection import SRTPConnection
from .packet import SRTPPacket
from . import protocol
from . import exceptions


logger = logging.getLogger(__name__)


class GE_SRTP_Driver:
    """
    Main driver class for GE PLC communication via SRTP protocol.

    This class provides high-level methods for reading PLC memory and
    querying PLC status. All operations are READ-ONLY for safety.

    Example:
        ```python
        plc = GE_SRTP_Driver('172.16.12.127')
        plc.connect()
        value = plc.read_register(100)
        plc.disconnect()
        ```

    Or using context manager:
        ```python
        with GE_SRTP_Driver('172.16.12.127') as plc:
            value = plc.read_register(100)
        ```
    """

    def __init__(
        self,
        host: str,
        port: int = protocol.DEFAULT_PORT,
        timeout: int = protocol.DEFAULT_TIMEOUT,
        slot: int = 1
    ):
        """
        Initialize the PLC driver.

        Args:
            host: PLC IP address or hostname
            port: TCP port (default 18245)
            timeout: Socket timeout in seconds (default 5)
            slot: CPU slot number (default 1, use 2 if CPU is in slot 2)
        """
        self.host = host
        self.port = port
        self.timeout = timeout
        self.slot = slot
        self.connection = SRTPConnection(host, port, timeout)
        self.sequence_number = 0

        logger.info(f"Initialized GE-SRTP driver for {host}:{port} (CPU slot {slot})")

    def connect(self) -> None:
        """
        Connect to the PLC and perform initialization.

        Raises:
            ConnectionError: If connection fails
            InitializationError: If initialization handshake fails
        """
        self.connection.connect()
        self.sequence_number = 0
        logger.info("Driver connected and ready")

    def disconnect(self) -> None:
        """Disconnect from the PLC."""
        self.connection.disconnect()
        logger.info("Driver disconnected")

    def is_connected(self) -> bool:
        """
        Check if connected to PLC.

        Returns:
            True if connected and initialized
        """
        return self.connection.is_alive()

    def _get_next_sequence_number(self) -> int:
        """
        Get next sequence number and increment counter.

        Returns:
            Next sequence number (0-255)
        """
        seq = self.sequence_number
        self.sequence_number = (self.sequence_number + 1) % 256
        return seq

    def _send_request_and_receive(
        self,
        service_code: int,
        segment_selector: int = 0,
        data_offset: int = 0,
        data_length: int = 0
    ) -> SRTPPacket:
        """
        Send a service request and receive response.

        Args:
            service_code: Service request code
            segment_selector: Segment selector for memory access
            data_offset: Memory address offset
            data_length: Number of bytes/words to read

        Returns:
            Parsed response packet

        Raises:
            ConnectionError: If not connected
            ProtocolError: If request/response fails
        """
        # Get sequence number
        seq = self._get_next_sequence_number()

        # Build request packet
        request = SRTPPacket.build_request(
            sequence_number=seq,
            service_code=service_code,
            segment_selector=segment_selector,
            data_offset=data_offset,
            data_length=data_length,
            slot=self.slot
        )

        # Send request
        self.connection.send_request(request)

        # Receive response
        response_data = self.connection.receive_response()

        # Parse response
        response = SRTPPacket.parse_response(response_data)

        # Validate sequence number
        response.validate_sequence_number(seq)

        return response

    # ========================================================================
    # REGISTER MEMORY OPERATIONS (%R)
    # ========================================================================

    def read_register(self, address: int, count: int = 1) -> Union[int, List[int]]:
        """
        Read one or more register values (%R memory).

        Registers are 16-bit signed integers used for calculations,
        set points, and general data storage.

        Args:
            address: Starting register address (e.g., 100 for %R100)
            count: Number of consecutive registers to read (default 1)

        Returns:
            Single integer if count=1, list of integers otherwise

        Raises:
            ValidationError: If address or count is invalid
            MemoryError: If read operation fails

        Example:
            ```python
            value = plc.read_register(100)  # Read %R100
            values = plc.read_register(100, 10)  # Read %R100-R109
            ```
        """
        if count < 1 or count > 125:
            raise exceptions.ValidationError(f"Count must be 1-125, got {count}")

        logger.info(f"Reading register %R{address}, count={count}")

        # PLC requires minimum data_length of 4 words (8 bytes)
        # Request at least 4, but return only what was asked for
        request_length = max(count, 4)

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.READ_SYSTEM_MEMORY,
            segment_selector=protocol.SegmentSelector.REGISTERS_WORD,
            data_offset=address,
            data_length=request_length
        )

        values = response.extract_word_values()

        # Return only the requested number of values
        values = values[:count]

        logger.debug(f"Read registers %R{address}-{address+count-1}: {values}")

        return values[0] if count == 1 else values

    # ========================================================================
    # ANALOG I/O OPERATIONS (%AI, %AQ)
    # ========================================================================

    def read_analog_input(self, address: int, count: int = 1) -> Union[int, List[int]]:
        """
        Read analog input values (%AI memory).

        Analog inputs are 16-bit values representing sensor readings
        from field devices (temperature, pressure, flow, etc.).

        Args:
            address: Starting address (e.g., 0 for %AI1, note: 0-based addressing)
            count: Number of consecutive values to read

        Returns:
            Single integer if count=1, list of integers otherwise
        """
        if count < 1 or count > 125:
            raise exceptions.ValidationError(f"Count must be 1-125, got {count}")

        logger.info(f"Reading analog input %AI{address+1}, count={count}")

        # PLC requires minimum data_length of 4 words
        request_length = max(count, 4)

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.READ_SYSTEM_MEMORY,
            segment_selector=protocol.SegmentSelector.ANALOG_INPUTS_WORD,
            data_offset=address,
            data_length=request_length
        )

        values = response.extract_word_values()

        # Return only the requested number of values
        values = values[:count]

        return values[0] if count == 1 else values

    def read_analog_output(self, address: int, count: int = 1) -> Union[int, List[int]]:
        """
        Read analog output values (%AQ memory).

        Analog outputs are 16-bit values sent to control devices
        (valve positions, motor speeds, etc.).

        Args:
            address: Starting address (e.g., 0 for %AQ1, note: 0-based addressing)
            count: Number of consecutive values to read

        Returns:
            Single integer if count=1, list of integers otherwise
        """
        if count < 1 or count > 125:
            raise exceptions.ValidationError(f"Count must be 1-125, got {count}")

        logger.info(f"Reading analog output %AQ{address+1}, count={count}")

        # PLC requires minimum data_length of 4 words
        request_length = max(count, 4)

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.READ_SYSTEM_MEMORY,
            segment_selector=protocol.SegmentSelector.ANALOG_OUTPUTS_WORD,
            data_offset=address,
            data_length=request_length
        )

        values = response.extract_word_values()

        # Return only the requested number of values
        values = values[:count]

        return values[0] if count == 1 else values

    # ========================================================================
    # DISCRETE I/O OPERATIONS (%I, %Q)
    # ========================================================================

    def read_discrete_input(
        self,
        address: int,
        count: int = 1,
        mode: str = 'bit'
    ) -> Union[bool, List[bool], int, List[int]]:
        """
        Read discrete input values (%I memory).

        Discrete inputs are digital signals from sensors, switches,
        and other on/off devices.

        Args:
            address: Starting address (e.g., 1 for %I1)
            count: Number of values to read
            mode: 'bit' for boolean values, 'byte' for byte values

        Returns:
            Depends on mode and count:
            - mode='bit', count=1: single boolean
            - mode='bit', count>1: list of booleans
            - mode='byte', count=1: single integer (0-255)
            - mode='byte', count>1: list of integers
        """
        if mode == 'bit':
            selector = protocol.SegmentSelector.DISCRETE_INPUTS_BIT
            logger.info(f"Reading discrete input %I{address}, count={count}, mode=bit")
            # For bit mode, enforce minimum of 64 bits (8 bytes) - verified on RX3i
            request_length = max(count, 64)
        elif mode == 'byte':
            selector = protocol.SegmentSelector.DISCRETE_INPUTS_BYTE
            logger.info(f"Reading discrete input %I{address}, count={count}, mode=byte")
            # For byte mode, enforce minimum of 8 bytes - verified on RX3i
            request_length = max(count, 8)
        else:
            raise exceptions.ValidationError(f"Invalid mode: {mode}, must be 'bit' or 'byte'")

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.READ_SYSTEM_MEMORY,
            segment_selector=selector,
            data_offset=address,
            data_length=request_length
        )

        if mode == 'bit':
            values = response.extract_bit_values(count)
            return values[0] if count == 1 else values
        else:  # byte
            values = response.extract_byte_values()
            values = values[:count]  # Trim to requested count
            return values[0] if count == 1 else values

    def read_discrete_output(
        self,
        address: int,
        count: int = 1,
        mode: str = 'bit'
    ) -> Union[bool, List[bool], int, List[int]]:
        """
        Read discrete output values (%Q memory).

        Discrete outputs are digital signals to actuators, relays,
        and other on/off devices.

        Args:
            address: Starting address (e.g., 1 for %Q1)
            count: Number of values to read
            mode: 'bit' for boolean values, 'byte' for byte values

        Returns:
            Depends on mode and count (see read_discrete_input)
        """
        if mode == 'bit':
            selector = protocol.SegmentSelector.DISCRETE_OUTPUTS_BIT
            # For bit mode, enforce minimum of 64 bits (8 bytes) - verified on RX3i
            request_length = max(count, 64)
        elif mode == 'byte':
            selector = protocol.SegmentSelector.DISCRETE_OUTPUTS_BYTE
            # For byte mode, enforce minimum of 8 bytes - verified on RX3i
            request_length = max(count, 8)
        else:
            raise exceptions.ValidationError(f"Invalid mode: {mode}")

        logger.info(f"Reading discrete output %Q{address}, count={count}, mode={mode}")

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.READ_SYSTEM_MEMORY,
            segment_selector=selector,
            data_offset=address,
            data_length=request_length
        )

        if mode == 'bit':
            values = response.extract_bit_values(count)
            return values[0] if count == 1 else values
        else:
            values = response.extract_byte_values()
            values = values[:count]  # Trim to requested count
            return values[0] if count == 1 else values

    # ========================================================================
    # INTERNAL/TEMPORARY MEMORY OPERATIONS (%M, %T)
    # ========================================================================

    def read_discrete_internal(
        self,
        address: int,
        count: int = 1,
        mode: str = 'bit'
    ) -> Union[bool, List[bool], int, List[int]]:
        """
        Read discrete internal memory (%M).

        Internal memory consists of coils/flags used within PLC logic.

        Args:
            address: Starting address
            count: Number of values to read
            mode: 'bit' or 'byte'

        Returns:
            Depends on mode and count
        """
        if mode == 'bit':
            selector = protocol.SegmentSelector.DISCRETE_INTERNALS_BIT
            # For bit mode, enforce minimum of 64 bits (8 bytes) - verified on RX3i
            request_length = max(count, 64)
        elif mode == 'byte':
            selector = protocol.SegmentSelector.DISCRETE_INTERNALS_BYTE
            # For byte mode, enforce minimum of 8 bytes - verified on RX3i
            request_length = max(count, 8)
        else:
            raise exceptions.ValidationError(f"Invalid mode: {mode}")

        logger.info(f"Reading discrete internal %M{address}, count={count}, mode={mode}")

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.READ_SYSTEM_MEMORY,
            segment_selector=selector,
            data_offset=address,
            data_length=request_length
        )

        if mode == 'bit':
            values = response.extract_bit_values(count)
            return values[0] if count == 1 else values
        else:
            values = response.extract_byte_values()
            values = values[:count]  # Trim to requested count
            return values[0] if count == 1 else values

    def read_discrete_temp(
        self,
        address: int,
        count: int = 1,
        mode: str = 'bit'
    ) -> Union[bool, List[bool], int, List[int]]:
        """
        Read discrete temporary memory (%T).

        Temporary memory is volatile and lost on power cycle.
        Used for intermediate calculations.

        Args:
            address: Starting address
            count: Number of values to read
            mode: 'bit' or 'byte'

        Returns:
            Depends on mode and count
        """
        if mode == 'bit':
            selector = protocol.SegmentSelector.DISCRETE_TEMPS_BIT
            # For bit mode, enforce minimum of 64 bits (8 bytes) - verified on RX3i
            request_length = max(count, 64)
        elif mode == 'byte':
            selector = protocol.SegmentSelector.DISCRETE_TEMPS_BYTE
            # For byte mode, enforce minimum of 8 bytes - verified on RX3i
            request_length = max(count, 8)
        else:
            raise exceptions.ValidationError(f"Invalid mode: {mode}")

        logger.info(f"Reading discrete temp %T{address}, count={count}, mode={mode}")

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.READ_SYSTEM_MEMORY,
            segment_selector=selector,
            data_offset=address,
            data_length=request_length
        )

        if mode == 'bit':
            values = response.extract_bit_values(count)
            return values[0] if count == 1 else values
        else:
            values = response.extract_byte_values()
            values = values[:count]  # Trim to requested count
            return values[0] if count == 1 else values

    # ========================================================================
    # SYSTEM MEMORY OPERATIONS (%S, %SA, %SB, %SC)
    # ========================================================================

    def read_system_memory(
        self,
        mem_type: str,
        address: int,
        count: int = 1,
        mode: str = 'bit'
    ) -> Union[bool, List[bool], int, List[int]]:
        """
        Read system memory (%S, %SA, %SB, %SC).

        System memory contains timers, scan information, fault data, etc.

        Args:
            mem_type: 'S', 'SA', 'SB', or 'SC'
            address: Starting address
            count: Number of values to read
            mode: 'bit' or 'byte'

        Returns:
            Depends on mode and count
        """
        # Map memory type to selector
        selector_map_bit = {
            'S': protocol.SegmentSelector.SYSTEM_S_DISCRETE_BIT,
            'SA': protocol.SegmentSelector.SYSTEM_A_DISCRETE_BIT,
            'SB': protocol.SegmentSelector.SYSTEM_B_DISCRETE_BIT,
            'SC': protocol.SegmentSelector.SYSTEM_C_DISCRETE_BIT,
        }
        selector_map_byte = {
            'S': protocol.SegmentSelector.SYSTEM_S_DISCRETE_BYTE,
            'SA': protocol.SegmentSelector.SYSTEM_A_DISCRETE_BYTE,
            'SB': protocol.SegmentSelector.SYSTEM_B_DISCRETE_BYTE,
            'SC': protocol.SegmentSelector.SYSTEM_C_DISCRETE_BYTE,
        }

        if mode == 'bit':
            selector = selector_map_bit.get(mem_type.upper())
            # For bit mode, enforce minimum of 64 bits (8 bytes) - verified on RX3i
            request_length = max(count, 64)
        elif mode == 'byte':
            selector = selector_map_byte.get(mem_type.upper())
            # For byte mode, enforce minimum of 8 bytes - verified on RX3i
            request_length = max(count, 8)
        else:
            raise exceptions.ValidationError(f"Invalid mode: {mode}")

        if selector is None:
            raise exceptions.ValidationError(f"Invalid system memory type: {mem_type}")

        logger.info(f"Reading system memory %{mem_type}{address}, count={count}, mode={mode}")

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.READ_SYSTEM_MEMORY,
            segment_selector=selector,
            data_offset=address,
            data_length=request_length
        )

        if mode == 'bit':
            values = response.extract_bit_values(count)
            return values[0] if count == 1 else values
        else:
            values = response.extract_byte_values()
            values = values[:count]  # Trim to requested count
            return values[0] if count == 1 else values

    # ========================================================================
    # GENIUS GLOBAL DATA (%G)
    # ========================================================================

    def read_global_memory(
        self,
        address: int,
        count: int = 1,
        mode: str = 'bit'
    ) -> Union[bool, List[bool], int, List[int]]:
        """
        Read Genius global data (%G).

        Global data is shared between multiple PLCs in a Genius network.

        Args:
            address: Starting address
            count: Number of values to read
            mode: 'bit' or 'byte'

        Returns:
            Depends on mode and count
        """
        if mode == 'bit':
            selector = protocol.SegmentSelector.GENIUS_GLOBAL_DATA_BIT
            # For bit mode, enforce minimum of 64 bits (8 bytes) - verified on RX3i
            request_length = max(count, 64)
        elif mode == 'byte':
            selector = protocol.SegmentSelector.GENIUS_GLOBAL_DATA_BYTE
            # For byte mode, enforce minimum of 8 bytes - verified on RX3i
            request_length = max(count, 8)
        else:
            raise exceptions.ValidationError(f"Invalid mode: {mode}")

        logger.info(f"Reading global memory %G{address}, count={count}, mode={mode}")

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.READ_SYSTEM_MEMORY,
            segment_selector=selector,
            data_offset=address,
            data_length=request_length
        )

        if mode == 'bit':
            values = response.extract_bit_values(count)
            return values[0] if count == 1 else values
        else:
            values = response.extract_byte_values()
            values = values[:count]  # Trim to requested count
            return values[0] if count == 1 else values

    # ========================================================================
    # PLC STATUS AND DIAGNOSTIC OPERATIONS
    # ========================================================================

    def get_plc_status(self) -> Dict[str, Any]:
        """
        Get PLC short status information.

        Returns:
            Dictionary containing PLC status information

        Note:
            Response format varies by PLC model. Status may be in payload
            or embedded in header bytes 50-55 (PLC piggyback status).
        """
        logger.info("Requesting PLC short status")

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.PLC_SHORT_STATUS,
            segment_selector=0,
            data_offset=0,
            data_length=0
        )

        # Check if there's a payload, otherwise status is in header
        if len(response.payload) > 0:
            return {
                'raw_data': response.payload.hex(),
                'length': len(response.payload),
                'timestamp': datetime.now().isoformat()
            }
        else:
            # Status is typically in the header - return message type
            return {
                'message_type': f'0x{response.message_type:02X}',
                'service_code': response.service_code,
                'segment_selector': response.segment_selector,
                'timestamp': datetime.now().isoformat()
            }

    def get_controller_info(self) -> Dict[str, Any]:
        """
        Get controller type and ID information.

        Returns:
            Dictionary containing controller information
        """
        logger.info("Requesting controller info")

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.RETURN_CONTROLLER_TYPE_AND_ID,
            segment_selector=0,
            data_offset=0,
            data_length=0
        )

        if len(response.payload) > 0:
            return {
                'raw_data': response.payload.hex(),
                'length': len(response.payload),
                'timestamp': datetime.now().isoformat()
            }
        else:
            return {
                'message_type': f'0x{response.message_type:02X}',
                'timestamp': datetime.now().isoformat()
            }

    def get_program_names(self) -> Dict[str, Any]:
        """
        Get control program names from PLC.

        Returns:
            Dictionary containing program name information
        """
        logger.info("Requesting program names")

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.RETURN_CONTROL_PROGRAM_NAMES,
            segment_selector=0,
            data_offset=0,
            data_length=0
        )

        if len(response.payload) > 0:
            return {
                'raw_data': response.payload.hex(),
                'length': len(response.payload),
                'timestamp': datetime.now().isoformat()
            }
        else:
            return {
                'message_type': f'0x{response.message_type:02X}',
                'timestamp': datetime.now().isoformat()
            }

    def get_plc_datetime(self) -> Dict[str, Any]:
        """
        Get PLC current date and time.

        Returns:
            Dictionary containing PLC date/time information
        """
        logger.info("Requesting PLC date/time")

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.RETURN_PLC_DATETIME,
            segment_selector=0,
            data_offset=0,
            data_length=0
        )

        if len(response.payload) > 0:
            return {
                'raw_data': response.payload.hex(),
                'length': len(response.payload),
                'timestamp': datetime.now().isoformat()
            }
        else:
            return {
                'message_type': f'0x{response.message_type:02X}',
                'timestamp': datetime.now().isoformat()
            }

    def get_fault_table(self) -> Dict[str, Any]:
        """
        Get PLC fault table.

        Returns:
            Dictionary containing fault table information
        """
        logger.info("Requesting fault table")

        response = self._send_request_and_receive(
            service_code=protocol.ServiceCode.RETURN_FAULT_TABLE,
            segment_selector=0,
            data_offset=0,
            data_length=0
        )

        if len(response.payload) > 0:
            return {
                'raw_data': response.payload.hex(),
                'length': len(response.payload),
                'timestamp': datetime.now().isoformat()
            }
        else:
            return {
                'message_type': f'0x{response.message_type:02X}',
                'timestamp': datetime.now().isoformat()
            }

    # ========================================================================
    # CONTEXT MANAGER SUPPORT
    # ========================================================================

    def __enter__(self):
        """Context manager entry - connect to PLC."""
        self.connect()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit - disconnect from PLC."""
        self.disconnect()
        return False

    def __repr__(self) -> str:
        """String representation of driver."""
        status = "connected" if self.is_connected() else "disconnected"
        return f"GE_SRTP_Driver({self.host}:{self.port}, {status})"
