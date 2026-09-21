# Unified driver packages (P2/P3)

## Runtime and installation

All five official protocols (`modbus_tcp`, `s7`, `ge_srtp`, `opcua`, `opcda_center`) are independent packages. S7/GE planning and decoding live inside their packages. Collector discovers only `<DATAWATCH_DATA_ROOT>/drivers/<id>/driver.toml`; it never imports packages directly from the offline catalog. Read-only status requests reuse the initialized registry snapshot.

PyInstaller builds `driver-catalog/catalog.json` and reproducible ZIP archives beside each Collector EXE. Agent installs these once, records `preinstall_completed`, and preserves existing versions and explicit removals across restarts/software upgrades. An uninstalled official package remains visible and can be explicitly restored from the offline catalog.

`state.json` is the node-local installation/transaction record. `install_status` distinguishes `installed`, `not_installed`, `missing`, `invalid`, and `unknown`; `runtime_status` distinguishes `loaded`, `load_failed`, `not_loaded`, `collector_stopped`, and `unknown`. A distributed center does not represent edge-node installation state. The application labels the current target as the local collection node.

## Package contract and trust

Agent and Collector share `data_watch_driver_manifest.py`: IDs, aliases, publisher, API/ABI, OS/architecture, minimum Collector version, capabilities, dependencies and resource paths. OPC DA declares `managed_components = { opcda_proxy = "1.0" }`; Agent's existing fixed proxy manager remains responsible for the x86 proxy. Packages cannot specify executable installation scripts or arbitrary service commands.

Official identities and aliases are reserved to DataWatch. Their complete package-tree digest must match the catalog delivered with the software, or an existing verified installation record when retaining an older version. Treat this catalog as trusted installation material and deliver it using the release distribution trust mechanism. Checksums alone are **not digital signatures**. Independently signed ZIPs use Ed25519 and a complete per-file inventory in `signature.json`; verification happens in Agent before any package import.

Third-party uploads require a nonempty publisher and either a valid signature from a trusted publisher or an exact internally reviewed entry in the trusted catalog's `approved_packages` list, with `id`, `publisher`, `version`, and `tree_sha256`. The upload endpoint cannot add or alter this approval or trusted keys. A signed package that has an invalid/unknown signature is always rejected, even if its digest was previously approved. ZIP entries containing `.pyc` or `__pycache__` are rejected. Unreviewed packages are rejected before code import. Frozen Collectors likewise require a reviewed digest or an approved installation record. For the one-shot signature preflight, Agent passes the exact verified tree digest to the target subprocess; this grant is not used by normal Collector startup. Direct source-mode registry scans remain available for driver development/unit tests.

Python dependencies ship under each package's `vendor` directory, pinned by `vendor_meta.toml` and (for S7/OPC UA) `requirements-vendor.txt`. Dependency ownership includes transitive top-level modules. Identical content may be reused; conflicting content is rejected without removing another package's imported modules.

## Signing a reviewed package

Use an existing base64-encoded raw Ed25519 private key (the format produced by `packaging/release-tool.py generate-key`). Keep private keys out of ZIPs and source control.

```powershell
& agent/.venv-pack/Scripts/python.exe packaging/sign-driver.py --package packages/my_driver --private-key packaging/keys/driver-release.key
```

The tool writes `signature.json` and prints only the public trust entry. A release administrator adds that entry (`publisher`, `key_id`, `public_key`) to the trusted installation catalog's `trusted_publishers` list through the controlled release process. Uploads cannot trust their own embedded keys. Sign after all code/resources/dependencies are finalized, then archive the package. Changing any signed file or adding a file invalidates the signature. The official offline catalog can continue pinning unsigned bundled archives because it is itself trusted release material.

## Mutation workflow

1. Admin uploads a ZIP or chooses an official restore. Agent checks safe extraction, manifest, provenance and content.
2. The target Collector runtime runs `--driver-preflight`, imports dependencies and constructs the factory without connecting to a PLC. Native extensions that re-enable the GIL are rejected.
3. The application shows the verified name/version and explains that restarting collection affects **all devices on this node**.
4. After confirmation, installation repeats validation, stops Collector and verifies exit before moving the old/new directories.
5. If collection was running, Agent starts a new process and verifies its PID, boot identity, data root, version, digest and loaded state. If it was stopped, it stays stopped.
6. Failures restore the old package and original running/stopped state; unresolved recovery blocks further mutations. Transaction replay follows the same rule.

Uninstall checks device references and reports device codes. Edge Agent also checks its durable local configuration. Removing a local package does not delete the global protocol catalog, devices, point mappings or history. The persistent removal marker is written before transaction completion.

## Rebuilding and verification

From repository root in PowerShell:

```powershell
python -c "from pathlib import Path; from data_watch_driver_catalog import write_catalog; write_catalog(Path('packages'), Path('packages/driver-catalog/catalog.json'), '0.1.0')"
& collector/.venv-pack/Scripts/pyinstaller.exe collector/data-watch-collector.spec --noconfirm
& collector/.venv-pack/Scripts/pyinstaller.exe edge/data-watch-edge-collector.spec --noconfirm
& agent/.venv-pack/Scripts/pyinstaller.exe agent/data-watch-agent.spec --noconfirm
```

Refresh the catalog after any package/manifest/vendor change. Vendored native extensions must be Windows x64 CPython 3.14 free-threaded wheels. Keep licenses and dependency metadata with the packages.

`agent/tests/test_driver_lifecycle.py` uses real target-runtime subprocesses. Set `DATAWATCH_TEST_COLLECTOR_EXE` to a built central or edge EXE to run it against frozen binaries. It covers first install, preflight without activation, explicit repair, stop-before-switch, source rejection, wrong-instance/digest rejection, upgrade rollback and stopped crash recovery.

`scripts/test_driver_frozen_lifecycle.py --agent <Agent.exe> --collector <Collector.exe>` starts an isolated real Agent on an ephemeral loopback port and exercises authenticated HTTP preview/uninstall/restore, including an Agent restart between uninstall and restore. It writes `report.json` and logs under a fresh `outputs/driver-lifecycle-build/smoke-*` directory and stops only the test process it created. No PLC or live installation is touched.

Protocol migration golden cases are in `collector/tests/fixtures/protocol_migration.json`. They preserve original S7/GE read plans, decoded values and missing-data semantics. Real PLC/OPC server acceptance remains a deployment-site check.
