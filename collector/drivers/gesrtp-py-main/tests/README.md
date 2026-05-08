# gesrtp-py Test Suite

Comprehensive test scripts for the GE-SRTP PLC driver.

## Test Environment

**Current Test PLC:**
- **Model**: GE PACSystems EPXCPE210
- **IP Address**: 172.16.12.124
- **Port**: 18245
- **CPU Slot**: 0
- **Firmware**: 10.30 [EJTT]
- **Program Status**: NO PROGRAM LOADED (clean slate)

See `../docs/hardware.md` for complete hardware configuration.

---

## Available Tests

### 01_connection_basic.py
**Purpose**: Basic connectivity and communication test

**What it tests:**
- TCP connection to PLC
- Driver initialization
- Basic register reads (single and batch)
- PLC status queries
- Context manager functionality

**When to use:**
- First test to run on a new PLC
- Verify basic connectivity
- Confirm slot number is correct
- Quick smoke test

**Usage:**
```bash
python tests/01_connection_basic.py
```

**Expected output:**
```
GE-SRTP Driver Connection Test
========================================
Starting connection test to PLC at 172.16.12.124 (CPU slot 0)
Connection successful!
SUCCESS: Read %R1 = <value>
SUCCESS: Read %R1-R5 = [<values>]
ALL TESTS PASSED!
```

---

### 02_memory_all_types.py
**Purpose**: Test all 9 memory types with their access modes

**What it tests:**
- Register memory (%R) - word mode
- Analog I/O (%AI, %AQ) - word mode
- Discrete I/O (%I, %Q) - bit and byte modes
- Internal memory (%M) - bit and byte modes
- Temporary memory (%T) - bit and byte modes
- System memory (%S, %SA, %SB, %SC) - bit and byte modes
- Global memory (%G) - bit and byte modes
- PLC diagnostics (status, info, programs, datetime, faults)

**When to use:**
- Verify all memory types are accessible
- Test after PLC configuration changes
- Comprehensive functionality check
- Baseline testing

**Usage:**
```bash
python tests/02_memory_all_types.py
```

**Expected output:**
```
GE-SRTP MEMORY TYPE TESTING
========================================
TESTING REGISTERS (%R)
✓ Registers working!
TESTING ANALOG I/O
✓ Analog inputs working!
...
```

---

### 03_memory_comprehensive_0_64.py ⭐ NEW
**Purpose**: Comprehensive test of addresses 0-64 for all memory types

**What it tests:**
- **Registers (%R)**: Addresses 0-64 (65 registers)
- **Analog Input (%AI)**: Addresses 0-64 (65 inputs)
- **Analog Output (%AQ)**: Addresses 0-64 (65 outputs)
- **Discrete Input (%I)**: Addresses 0-64 in bit and byte modes
- **Discrete Output (%Q)**: Addresses 0-64 in bit and byte modes
- **Internal Memory (%M)**: Addresses 0-64 in bit and byte modes
- **PLC Diagnostics**: Status, controller info, programs, datetime

**When to use:**
- Thorough testing of address range
- Testing clean PLC with no program
- Establishing baseline values
- Finding working/non-working address ranges

**Usage:**
```bash
python tests/03_memory_comprehensive_0_64.py
```

**Expected output:**
```
COMPREHENSIVE MEMORY TEST - Addresses 0-64
========================================
PLC: 172.16.12.124
CPU Slot: 0

TESTING REGISTERS (%R) - Addresses 0 to 64
  %R1 (addr 0): 0
  %R2 (addr 1): 0
  ...
  %R65 (addr 64): 0
✓ Success: 65/65

TEST SUMMARY
========================================
✓ registers: 65/65 successful
✓ analog_input: 65/65 successful
...
TOTAL: XXX/XXX tests passed
Success Rate: XX.X%
```

**Features:**
- Tests every address individually
- Shows success/error count for each memory type
- Provides detailed summary at end
- Stores results for later analysis

---

## Test Naming Convention

Tests are numbered for recommended execution order:

- `01_*` - Basic connectivity and smoke tests
- `02_*` - Feature/functionality tests
- `03_*` - Comprehensive/stress tests
- `04_*` - Advanced/specialized tests (future)

---

## Running All Tests

To run all tests sequentially:

```bash
# Windows
for %f in (tests\*.py) do python %f

# Linux/Mac
for test in tests/*.py; do python "$test"; done
```

---

## Test Configuration

### Changing PLC IP Address

Edit each test file and modify:
```python
PLC_IP = "172.16.12.124"  # Change to your PLC's IP
CPU_SLOT = 0               # Change to your CPU's slot number
```

### Adjusting Test Ranges

In `03_memory_comprehensive_0_64.py`, modify the test ranges:
```python
# Change address range
tester.test_registers(0, 64)  # Start address, end address
tester.test_analog_input(0, 64)
# etc.
```

---

## Understanding Test Results

### Success Indicators
- ✓ - Test passed
- ⚠ - Partial success (some addresses failed)
- ✗ - Test failed completely

### Common Errors

**"Connection timeout"**
- Check PLC IP address
- Verify network connectivity
- Ensure PLC is powered on

**"Empty payload"**
- Wrong CPU slot number
- Try slot 0, 1, or 2

**"Address not available"**
- Address may not be configured in PLC
- Hardware module may be missing
- Expected on clean PLC with no program

**"Minimum length error"**
- Protocol requires minimum read sizes
- This is normal, driver handles automatically

---

## Test Development Guidelines

When creating new tests:

1. **Use descriptive names**: `04_performance_batch_reads.py`
2. **Follow numbering**: Next test = `04_*`
3. **Include docstring**: Explain purpose and usage
4. **Use logging**: Info level for results, debug for details
5. **Handle errors gracefully**: Try/except with clear messages
6. **Provide summary**: Success/failure counts at end

Example template:
```python
#!/usr/bin/env python3
"""
Test description here.
"""

import logging
from src.driver import GE_SRTP_Driver

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

def main():
    PLC_IP = "172.16.12.124"
    CPU_SLOT = 0

    logger.info("Starting test...")

    try:
        plc = GE_SRTP_Driver(PLC_IP, slot=CPU_SLOT)
        plc.connect()

        # Test code here

        plc.disconnect()
        logger.info("Test passed!")

    except Exception as e:
        logger.error(f"Test failed: {e}", exc_info=True)

if __name__ == "__main__":
    main()
```

---

## Troubleshooting

### Tests fail with "Connection refused"
1. Ping the PLC: `ping 172.16.12.124`
2. Check port: `telnet 172.16.12.124 18245` (should connect)
3. Verify PLC is running
4. Check firewall settings

### Tests pass but return all zeros
1. This is expected on clean PLC with no program
2. Use continuous monitor to verify changes when PLC is running
3. Check that hardware modules are installed and configured

### Tests are slow
1. Reduce address range in comprehensive tests
2. Increase timeout values if needed
3. Check network latency

---

## Contributing

When adding tests:
1. Test on real hardware before committing
2. Update this README with test description
3. Follow naming conventions
4. Include error handling
5. Document expected results

---

**Last Updated**: 2025-10-17
**Test PLC**: GE PACSystems EPXCPE210 at 172.16.12.124:18245 (slot 0)
**Status**: ✅ All tests updated for new hardware
