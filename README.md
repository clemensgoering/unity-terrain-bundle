# Unity Terrain Bundle

A reusable Unity Editor package for terrain previews, measured height changes, and bounded terrain edits, with an optional Codex skill and JSON adapter.

**Version: 0.3.0-preview.1.** Supports local operations rather than full landscape generation.

## Features

- Fixed-camera before/after previews and height-change maps.
- Height-change measurements, affected-area estimates, and configurable effect thresholds.
- Corridor and material operations with scene backups and rollback records.
- A JSON interface, PowerShell adapter, request templates, and portable smoke test.

## Installation

Requires Unity 6. Tested with Unity 6000.3.9f1 on Windows. Other versions and render pipelines have not been comprehensively tested. Newtonsoft.Json 3.2.2 is resolved as a package dependency.

In Unity Package Manager, choose **Install package from git URL**:

```text
https://github.com/clemensgoering/unity-terrain-bundle.git?path=/Packages/com.ruinrelay.landscape-kernel#v0.3.0-preview.1
```

Alternatively, download the repository and install from `Packages/com.ruinrelay.landscape-kernel/package.json`. Git must be available for installation by Git URL.

## Editor workflow

1. Save the scene and exit Play Mode.
2. Open **Tools → Landscape Kernel → Live Preview** and assign a terrain.
3. Set start/end positions in world X/Z, width, and transition distance. Keep the entire footprint inside the terrain.
4. Select `ridge` for a raised or lowered form, or `corridor` for an endpoint-based connection.
5. Set a camera position and target, or copy the Scene view camera.
6. Generate a preview, compare views, and export images and parameters. Optional automatic refresh follows parameter changes.

Some Editor labels currently appear in German. **maxChange is a cap, not intensity.** Ridge amplitude and influence determine the requested effect. The effect threshold is task-specific, not a quality score.

The window does not save terrain changes. Ridge operations are preview-only through the window and JSON bridge. Persistent corridor and material operations use the bridge.

## Optional Codex and CLI integration

Copy `skills/unity-landscape-kernel` into your Codex skills directory, typically `$CODEX_HOME/skills` or `~/.codex/skills`. Compare existing files before replacing them and reload skill discovery if needed.

The adapter requires a Unity CLI supporting `unity command ... script run`, a compatible Unity Pipeline connection in the open project, and `unity` on PATH. These external components are not bundled or required for the Editor window. The standard Unity Editor executable alone does not provide this interface.

```powershell
& ./skills/unity-landscape-kernel/scripts/Invoke-Landscape.ps1 -ProjectPath 'E:/Projects/MyGame' -RequestPath 'E:/Requests/inspect.json' -OutputDirectory 'E:/Reports'
```

Start with `examples/inspect.json`, replacing placeholders. Use the returned terrain component ID to adapt `examples/preview.json`. Adjust coordinates to the actual terrain. Mutations require the current scene hash. Run only one Editor write operation at a time.

See the [request specification](Packages/com.ruinrelay.landscape-kernel/Documentation~/REQUEST.md).

## Verification

With the optional Pipeline integration installed:

```powershell
unity command --project-path 'E:/Projects/MyGame' script run --file ./scripts/SmokeTest.cs --entry TerrainBundleSmokeTest.Run --timeout 180 --format json
```

The test uses temporary terrain objects, cleans them up in a finally block, and saves no scene. Run `python scripts/validate_bundle.py` to check repository structure and request templates.

See [validation notes](docs/VALIDATION.md).

## Limitations

- Single axis-aligned, unscaled Unity Terrain; no mesh deformation or spline paths.
- No automatic asset ground-contact adjustment, vehicle or water-transition validation, or aesthetic scoring.
- Live refresh follows panel parameters, not arbitrary terrain brushes.
- Protected-area and hole checks are sampled; thin features need separate inspection.
- Preview temporarily copies TerrainData; memory use scales with terrain size. Animation and wind are not frozen between images.
- Rollback checks hashes to preserve subsequent changes. Crash recovery uses backups rather than an atomic filesystem transaction.

Successful execution and measurable change do not constitute visual or gameplay approval.

## Licensing

No open-source license has been selected. Public availability alone does not grant general redistribution rights. Third-party art assets are not included.
