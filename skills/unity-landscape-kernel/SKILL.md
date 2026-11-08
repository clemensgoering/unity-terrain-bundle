---
name: unity-landscape-kernel
description: Inspect and edit existing Unity terrain with the installed Landscape Kernel, including bounded corridors, material changes, and rollback. Use for preserving and adjusting a chosen landscape, not creating a replacement world.
---

# Unity Landscape Kernel

This is a local Codex skill for the installed `RuinRelay.LandscapeKernel` package, not a marketplace plugin. The Unity CLI and project Pipeline connection must already be available.

Read `../../Packages/com.ruinrelay.landscape-kernel/Documentation~/REQUEST.md` in this bundle, or resolve the installed package through Unity Package Manager. Never assume a user's filesystem layout. Use schemaVersion 1 and explicit scene/object identifiers.

Use `inspect` first to obtain actual object IDs and terrain state. Then choose `corridor`, `material`, or `rollback` within the requested scope. Preserve the user's chosen scene and its positive terrain forms. The kernel edits the saved scene in place with backups and stages its own terrain data copy; inspect the returned backup and evidence paths before relying on rollback.

Before reaching Unity, honor the project's active editor slot and scene ownership rules. Do not run another Unity operation concurrently. Resolve unsaved scene changes through the existing workflow before mutation. A timeout or false result requires inspecting the captured evidence; do not blindly retry a mutating request.

Save the request as a UTF-8 JSON file and invoke the bundled helper:

```powershell
& ./scripts/Invoke-Landscape.ps1 -ProjectPath '<Unity project>' -RequestPath '<request.json>' -OutputDirectory '<reports directory>'
```

The helper creates a unique external C# entrypoint, calls `Bridge.RunFile` through `unity command script run`, captures CLI stdout/stderr, and requires both CLI and kernel success. It never embeds request content in executable code. The kernel writes operation evidence under the request's `evidenceDirectory`; helper logs live under `OutputDirectory`.

Report changed scene/asset paths, checks, backups, and unresolved limitations. A successful operation proves execution only: separately inspect terrain appearance, preservation of the original landscape, and requested playability. It does not imply art approval or vehicle-driving approval.

For effect evaluation use `preview` before mutation. Compare actual change against the stated visible goal; increasing maxChange alone may have no effect. The UI menu is Tools > Landscape Kernel > Live Preview. Preview exports do not prove art quality, asset support or playability. See the installed request schema for preview-only fields.
