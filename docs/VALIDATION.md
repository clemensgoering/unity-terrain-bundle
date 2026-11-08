# Validation

Source package tested in Unity 6000.3.9f1 on Windows, 2026-11-08. 13 preview integration checks and 9 corridor/material/rollback regression checks passed before packaging. Hallvik scene and terrain hashes stayed unchanged during preview tests. Those project-specific fixtures and scene files are not redistributed.

Measured example: an already-corrected corridor retained only about 0.01147 m planned effect; raising its cap did not increase that effect. A separate 6 m ridge preview changed the silhouette visibly. Neither finding is an aesthetic acceptance score.

The bundle adds a portable smoke test and structure validator. These do not replace the earlier end-to-end render/UI tests or prove compatibility with every Unity version/render pipeline. See README for limits.

Packaging verification: portable smoke test passed in Unity (ridge amplitude, pure planning, apply/stale rejection); structure/JSON/machine-path validator passed. Distributed C# package sources are byte-identical to the tested installed package.
