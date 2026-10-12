# Aircraft Production Intake (RD-02)

This folder accepts only manifest-addressed `.fbx` or `.glb` aircraft candidates.

Every candidate must declare its source path, format, SHA-256, identity/quality state, camera sockets, LOD budgets, 6–16 hidden primitive colliders, Built-in Standard metallic materials, and 12 golden angles in `Resources/AircraftAssets/asset-manifest-v1.json`.

The Editor import/build gate rejects unaddressed, hash-mismatched, out-of-contract, or programmatic prototype files. There are currently no production aircraft in this folder; do not fabricate high-detail replacements to satisfy the gate.
