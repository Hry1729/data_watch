# GE-SRTP Driver Examples

This directory contains example scripts demonstrating how to use the GE-SRTP PLC driver.

## Available Examples

Examples are numbered by complexity (01 = simplest, 03 = most advanced).

### 01_basic_usage.py
**Simple register reading demonstration** 🌟 **START HERE**

Perfect introduction to the driver showing three methods:
- Manual connect/disconnect
- Context manager (recommended)
- Reading analog I/O

**Usage:**
```bash
python examples/01_basic_usage.py
```

**What you'll learn:**
- 0-based addressing (read %R1 using address 0)
- Single vs batch reads
- Context manager auto-disconnect
- PLC status queries

**Difficulty**: ⭐ Beginner

---

### 02_realtime_monitor.py
**Real-time monitoring with in-place updates** 🔥 **NEW & IMPROVED**

Monitor PLC memory with live updates on the same screen (no scrolling!).
Values update in-place with change indicators (↑ ↓ = CHG).

**Usage:**
```bash
python examples/02_realtime_monitor.py
```

**Features:**
- **In-place updates**: Values update on same lines
- **Change indicators**: ↑ (increased), ↓ (decreased), = (no change), CHG (changed)
- **3 monitoring modes**: Registers, Analog I/O, Discrete I/O
- **Configurable**: Choose count and polling interval
- **Clean display**: Professional real-time monitoring

**Monitor modes:**
1. **Registers (%R)**: Watch register values change in real-time
2. **Analog I/O (%AI, %AQ)**: Monitor analog inputs and outputs
3. **Discrete I/O (%I, %Q)**: Track discrete bit states (ON/OFF)

**Use Cases:**
- Debugging PLC logic
- Understanding process behavior
- Verifying PLC programming changes
- Live process monitoring
- Testing without HMI

**Difficulty**: ⭐⭐ Intermediate

---

### 03_forensic_dump.py
**Forensic memory acquisition and export**

Performs comprehensive memory dump to JSON file.

**Usage:**
```bash
python examples/03_forensic_dump.py
```

**Captures:**
- Register memory (%R1-%R100)
- Analog I/O (%AI, %AQ)
- PLC diagnostics (status, controller info, program names, datetime, faults)
- Metadata (timestamp, PLC IP, slot number)

**Output:**
JSON file named `plc_dump_<IP>_<timestamp>.json`

**Use Cases:**
- Forensic analysis
- PLC backup before changes
- Incident response
- Baseline documentation
- Change tracking

**Difficulty**: ⭐⭐⭐ Advanced

---

## Quick Start Guide

**New users**: Start with `01_basic_usage.py`

```bash
# 1. Start with basics
python examples/01_basic_usage.py

# 2. Try real-time monitoring
python examples/02_realtime_monitor.py

# 3. Perform forensic dump (optional)
python examples/03_forensic_dump.py
```

---

## Important Notes

### Addressing

The GE-SRTP protocol uses **0-based addressing**, while PLC programming software uses 1-based:

| PLC Display | Protocol Address | Driver Call |
|-------------|------------------|-------------|
| %R1 | 0 | `plc.read_register(0)` |
| %R2 | 1 | `plc.read_register(1)` |
| %R100 | 99 | `plc.read_register(99)` |
| %AI1 | 0 | `plc.read_analog_input(0)` |

**Formula**: `protocol_address = plc_register_number - 1`

See `../ADDRESSING_SCHEME.md` for detailed explanation.

### CPU Slot Configuration

**CRITICAL**: You must specify the correct CPU slot number!

```python
# If CPU is in slot 1 (default):
plc = GE_SRTP_Driver('192.168.1.100', slot=1)

# If CPU is in slot 0 (like the current test PLC - EPXCPE210):
plc = GE_SRTP_Driver('172.16.12.124', slot=0)
```

Wrong slot number = PLC acknowledges but returns no data!

See `../SLOT_ADDRESSING_FINDINGS.md` for details.

### Hardware Dependencies

**Working on all PLCs:**
- %R (Registers) - Always available in CPU memory
- %M, %T (Internal/Temp Memory) - Always available in CPU memory
- %S, %SA, %SB, %SC (System Memory) - Always available in CPU memory
- PLC diagnostics (status, info, datetime) - Always available

**Requires hardware modules:**
- %AI/%AQ (Analog I/O) - Requires analog I/O modules
- %I/%Q (Discrete I/O) - Requires discrete I/O modules

The current test PLC (172.16.12.124 - EPXCPE210) configuration:
- ✅ Slot 0: EPXCPE210 CPU with integrated Ethernet
- ✅ Slot 1: EP-12F4 I/O module
- ✅ Slot 2: EP-2714 I/O module
- ⚠️ No program loaded - clean slate for testing

**Result**: ALL memory types working! ✅

See `../docs/hardware.md` for complete hardware configuration.

---

## Modifying Examples

### Change PLC IP Address

Edit the script and modify:
```python
PLC_IP = "172.16.12.124"  # Change to your PLC's IP
CPU_SLOT = 2              # Change to your CPU's slot number
```

### Change Register Range

For `memory_dump.py`, modify the dump range:
```python
# Default: dumps %R1-%R100
dump_data["data"]["registers"] = dump_registers(plc, start=0, end=99)

# To dump %R1-%R1000:
dump_data["data"]["registers"] = dump_registers(plc, start=0, end=999)
```

### Change Monitoring Interval

For `continuous_monitor.py`:
```python
# Default: 0.5 second polling
monitor_registers(plc, start_address=0, count=10, interval=0.5)

# Faster: 0.1 second (10 Hz)
monitor_registers(plc, start_address=0, count=10, interval=0.1)

# Slower: 2 seconds
monitor_registers(plc, start_address=0, count=10, interval=2.0)
```

---

## Security Warnings

⚠️ **IMPORTANT**:
- These scripts are **READ-ONLY** (safe to run)
- No write operations are implemented
- Use on **isolated networks** only
- GE-SRTP has **minimal security** by default
- Unauthorized PLC access may be **illegal**

---

## Troubleshooting

### "Connection refused" or timeout
- Check PLC IP address
- Verify network connectivity: `ping <PLC_IP>`
- Ensure PLC is powered on
- Check firewall rules (port 18245)

### "Empty payload" or "IndexError"
- Verify CPU slot number is correct
- Check if memory type is available on your PLC model
- For discrete I/O, verify hardware modules are installed

### Values seem wrong (off by 1)
- Remember: protocol uses 0-based addressing!
- To read %R1 from PLC, use address 0
- To read %R100 from PLC, use address 99

---

## Further Reading

- `../README.md` - Main driver documentation
- `../ADDRESSING_SCHEME.md` - Addressing explained in detail
- `../SLOT_ADDRESSING_FINDINGS.md` - CPU slot configuration
- `../MULTI_PACKET_DISCOVERY.md` - Protocol internals
- `../SESSION_SUMMARY.md` - Complete development log

---

**Last Updated**: 2025-10-16
**Driver Version**: 1.0.0 (Production Ready)
**Current Test PLC**: GE PACSystems EPXCPE210 (Firmware 10.30) at 172.16.12.124:18245 (slot 0)
**Previously Tested**: GE RX3i IC695CPE330 (Firmware 10.85) at 172.16.12.127:18245 (slot 2)
**Status**: ✅ All example scripts tested and working

