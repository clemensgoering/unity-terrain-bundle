# Landscape Kernel request schema v1

Installed package: com.ruinrelay.landscape-kernel 0.3.0-preview.1. Entry: RuinRelay.LandscapeKernel.Bridge.RunFile(absoluteRequestPath). JSON object result contains success. Unknown fields are rejected. One operation per request, one Editor writer. Commands use metres and world XYZ/XZ.

Common required fields: schemaVersion=1; operation (inspect/preview/corridor/material/rollback); operationId (unique alphanumeric, underscore, hyphen, 1..64 chars); evidenceDirectory (absolute, new report destination). For inspect/corridor/material: scenePath (existing Assets/...unity). For mutation: expectedSceneHash from inspect; cameras array [{name,position:[x,y,z],target:[x,y,z]}]. Current scenes must be clean; Edit Mode required.

inspect: returns sceneHash, terrain component GlobalObjectIds, asset paths, positions/sizes, collider state and roots. For material renderer IDs use GlobalObjectId.GetGlobalObjectIdSlow(renderer) in explicit read-only scene inspection. Do not use a GameObject ID when a component is required.

corridor: objectId=Terrain component ID; start/end=[worldX,worldZ]; halfWidth,blend,maxChange positive metres. Single straight local profile, endpoint heights retained, eased ends and shoulders. Optional preserveRects=[[xmin,zmin,xmax,zmax]]. Preflight samples terrain holes, rectangular bounds and protected areas, conservatively rejects active collider envelopes. Existing full terrain asset is cloned to Assets/LandscapeKernelRuns/<operationId>/Terrain.asset; source data remains unchanged. Does not paint a new route or place decorations.

Optional retireInvisibleColliderIds: explicit MeshCollider IDs for obsolete hidden road surfaces. Every listed collider must be enabled, have a disabled same-object Renderer, and every mesh vertex must lie within 0.5 m of the selected terrain with terrain ray contact. Qualifying colliders are disabled as part of the backed-up operation. This is NOT a general ignore-collision switch and does not permit bridges or unsupported spans.

material: objectId=target Renderer ID; slot=index; donorObjectId=reference Renderer ID; donorSlot=index; optional donorScenePath for a different reference scene. Copies the effective material into a private asset and assigns one slot. Does not alter geometry or certify support.

rollback: rollbackManifest=absolute rollback.json path from a successful operation. Restores saved scene only if current scene hash still matches that operation's result and backup hash is valid; therefore later unrelated edits are not overwritten. Private generated assets remain as evidence. No recursive cleanup.

Evidence: before/after camera PNGs; Scene.before.unity; report.json; rollback.json after success. Failed mutations restore scene bytes and reopen the original; private partial outputs are retained and reported. Crash/power-loss recovery is manual from the saved backup; this is not a filesystem-atomic transaction.

Checks: copy paint after persistence; bind Terrain and TerrainCollider; ensure no changed height samples outside footprint; 21 terrain ray contacts; material slot persistence; compare scene hashes on rollback. artAccepted=false and traversalTested=false are intentional. Inspection does not prove mesh-foot support, water banks, whole-world scale, paths or vehicle handling. Holes/protection preflight is sampled, not a proof for arbitrarily thin features; scene revisions and explicit footprints remain necessary.

Unsupported in this release: multi-segment spline generation, mesh-terrain deformation, rotated/scaled terrain, changing props with terrain, automatic semantic support anchors, runtime road/navmesh generation. Reject or plan a separate explicit operation; do not silently replace the selected method.

Call through the local skill's Invoke-Landscape.ps1 or a thin Pipeline C# entrypoint returning Bridge.RunFile(path). No second server, no TerraLoom dependency, no runtime components.

inspect optional nameFilter: case-insensitive component-name substring, returns up to 100 renderer and collider component IDs with bounds/material slots or enabled state. Empty filter returns no renderer/collider rows.


## Preview (0.3)
operation="preview": requires scenePath, Terrain component objectId, start/end, cameras. Does not require expectedSceneHash; current scenes must be clean. No persistent terrain or collider mutation. Outputs before.png, preview.png, delta.png, preview.json per camera and report.json.
Preview-only fields: planMode="corridor" or "ridge"; intent; amplitude (signed metres, ridge only); influence (0..1); minimumUsefulChange (metres, context-specific warning threshold). Shared fields halfWidth, blend and maxChange use metres. preserveRects masks protected height samples. Ridge has a sin² longitudinal envelope and eased sides; this is a form primitive, not a natural-landscape guarantee.
maxChange is a cap, not intensity. If no samples reach the cap, increasing it cannot strengthen the result. Report includes planned max/mean, area, clipped/protected samples and actual temporary TerrainData readback max. Terrain storage quantization can cause small differences.
Use Tools > Landscape Kernel > Live Preview. Optional automatic refresh follows parameter changes after 500 ms; it does not monitor arbitrary external terrain brush operations. Views use fixed camera poses. Export rejects stale parameters and changed source heights in the planned window.
No apply button in this release. Existing corridor commit shares the planner; ridge previews have no Bridge commit operation. New preview-only fields do not modify the corridor commit recipe. Always evaluate appearance and traversal separately.
Portable request templates are in the repository examples/ directory. Replace all placeholders and inspect the actual terrain before constructing a footprint.
