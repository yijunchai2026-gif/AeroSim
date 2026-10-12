# RD-02 Aircraft Asset Identity and Production Gate

## Runtime identity truth

Manifest `4.0.0-rd02.1` is the source of truth for the six runtime aircraft assets.

- `mini4`, `air3s`, `mavic3p`, `avata2`: **Programmatic Prototype / Unverified**. UI labels them as AeroSim prototypes with model references; they cannot claim verified model identity.
- `racer`, `micro`: **AeroSim Generic / Programmatic Prototype**.
- No fake high-detail or production model is supplied by RD-02.

## State machines

Identity progresses `Unverified → ReferenceLocked → GeometryVerified → ProductionApproved`, with `AeroSimGeneric` as the explicit generic identity and `Retired` terminal. Quality progresses `ProgrammaticPrototype → DccCandidate → ImportValidated → ProductionReady`, with `Rejected` and `Retired` terminal paths. Model identity may be claimed only at `ProductionApproved + ProductionReady`.

## Versioned manifest contract

`Assets/AeroSim/Resources/AircraftAssets/asset-manifest-v1.json` declares stable asset/runtime/catalog IDs, disclosure labels, coordinate convention (`Y-up`, `+Z forward`, `+X right`, meters), DCC source/hash fields, `flight-camera` and `preview-focus` sockets, LOD0/1/2/3/Cull budgets, 6–16 hidden primitive collision proxies, Built-in Standard metallic material/color-space rules, and 12 named golden angles.

## Formal DCC gate

Production candidates are restricted to `Assets/AeroSim/Art/AircraftProduction/`, FBX/GLB, explicit manifest addressing, and SHA-256 verification. Import and build validation enforce identity root TRS, sockets, LODs and triangle budgets, primitive-only collision, and Standard materials. Programmatic prototypes cannot pass through the DCC production path.

The gate does not modify cameras, scenes, physics profiles, or the Built-in render pipeline. Runtime validation and `PreviewCapture` preserve the six-aircraft v3 flight/replay regression while asserting RD-02 identity and socket contracts.
