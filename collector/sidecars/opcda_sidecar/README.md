# OPC DA Sidecar

This folder is the baseline for the AGV OPC DA x86 sidecar path.

Current status:

- `OpcDaProbe.cs` is a read-only x86 .NET Framework probe.
- The probe verifies OPC DA COM activation, ItemID format, `AddItems`, and `SyncIO.Read`.
- The next implementation step is to upgrade this probe into a resident sidecar service that pushes values to collector HTTP ingest.

Default local assumptions:

- OPC DA ProgID: `ZPMC.OPCServer`
- OPC channel/driver: `OPD_Server`
- OPC device: `AGV801`
- OPC group: `ViewMain`
- CSV: `data/AGV_OPC/agv/ViewMain.csv`
- ItemID format: `{channel}.{device}.{group}.{tag}`
- Example ItemID: `OPD_Server.AGV801.ViewMain.Tag30041800`

## Build

```powershell
cd D:\python\device_alarm\data_watch\collector\sidecars\opcda_sidecar
.\build.ps1
```

The build target must stay x86 because the ZPMC OPC DA COM server is 32-bit.

## Probe

AGV801:

```powershell
.\bin\OpcDaProbe.exe --server ZPMC.OPCServer --channel OPD_Server --device AGV801 --group ViewMain --csv ..\..\..\data\AGV_OPC\agv\ViewMain.csv --limit 20 --probe-count 10 --source cache --read-delay-ms 2000
```

AGV802:

```powershell
.\bin\OpcDaProbe.exe --server ZPMC.OPCServer --channel OPD_Server --device AGV802 --group ViewMain --csv ..\..\..\data\AGV_OPC\agv\ViewMain.csv --skip 96 --limit 10 --probe-count 10 --source cache --read-delay-ms 2000
```

Tag300201 smoke run:

```powershell
.\run_probe_tag300201.bat
```

Equivalent full command:

```powershell
.\bin\OpcDaProbe.exe --server ZPMC.OPCServer --channel OPD_Server --device AGV801 --group ViewMain --csv ..\..\..\data\AGV_OPC\agv\ViewMain.csv --skip 310 --limit 10 --probe-count 10 --source cache --read-delay-ms 500
```

The probe prints detailed `AddItem` and `Read` fields, including client/server handles, canonical OPC variant type, access rights, value type, quality decimal/hex, Good-quality flag, OPC timestamp, and item HRESULT.

If the auto-detected ItemID format is wrong, force one explicitly:

```powershell
.\bin\OpcDaProbe.exe --item-format "{channel}.{device}.{group}.{tag}" --limit 20
```

Common ItemID formats the probe tries:

- `{channel}.{device}.{group}.{tag}`
- `{channel}.{device}.{group}.{address}`
- `{device}.{group}.{tag}`
- `{device}.{group}.{address}`
- `{tag}`
- `{address}`

## Safety Boundary

The probe is read-only. It only calls OPC DA `AddItems` and `SyncIO.Read`.

The future resident sidecar must keep the same safety boundary:

- Do not write OPC DA Items.
- Do not send AGV control commands.
- Do not load OPC DA COM from the 64-bit Python collector process.
- Do not write TDengine directly; push data to collector HTTP ingest instead.

## Verified Local Result

Verified on 2026-04-26:

- ProgID `ZPMC.OPCServer` can be activated from the x86 probe when run outside the sandbox.
- Auto-detected ItemID format: `{channel}.{device}.{group}.{tag}`.
- Example ItemID: `OPD_Server.AGV801.ViewMain.Tag30041800`.
- `AddItems` succeeds for ViewMain points.
- `SyncIO.Read` returns success.
- AGV801 and AGV802 currently return `<null>` values with quality `0x0000`; this points to OPC server simulation/value-quality state rather than an ItemID or client connection problem.

## Planned Service Mode

The resident sidecar should:

- Load enabled AGV devices and point mappings from MySQL or an exported mapping file.
- Register OPC DA Items in batches by AGV device and group.
- Read periodically or through OPC DA subscription callbacks.
- Convert values into collector HTTP ingest payloads.
- Include `device_id`, `variable_id`, `value`, and `timestamp`.
- Use collector-side TDengine target mapping; the sidecar should not derive or write TDengine table names.
- Log OPC connection state, Item quality, read failures, push failures, and retry decisions.
