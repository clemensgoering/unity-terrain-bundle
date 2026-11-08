# Unity Terrain Bundle

Wiederverwendbares Unity-Editorpaket mit optionalem Codex-Skill und JSON-Adapter. **Version 0.3.0-preview.1**: Vorschauen und begrenzte Terrainoperationen, kein vollständiger Landschaftsgenerator.

## Enthalten
- UPM-Paket mit Terrainplaner, Vorschaufenster, Höhenänderungskarte und Wirkungsmessung.
- Korridor- und Materialoperationen mit Szenenbackup, eigener Terrainkopie und Rücknahmebeleg.
- JSON-Schnittstelle und PowerShell-Adapter für Codex/Unity Pipeline.
- Neutrale Auftragsvorlagen und ein isolierter Smoke-Test ohne Fremdassets.

Keine Hallvik-Welt, Polygon-/Synty-Assets oder TerraLoom-Abhängigkeit enthalten. Der Paketname und C#-Namespace bleiben aus Kompatibilitätsgründen `com.ruinrelay.landscape-kernel` bzw. `RuinRelay.LandscapeKernel`; ein Ruin-Relay-Projekt wird nicht benötigt.

## Installation in Unity
Voraussetzung: Unity 6; geprüft mit **6000.3.9f1 unter Windows**. Andere Editorversionen und Renderpipelines sind nicht umfassend geprüft. Newtonsoft.Json 3.2.2 wird als Paketabhängigkeit aufgelöst.

Im Package Manager **Install package from git URL** verwenden:

```text
https://github.com/clemensgoering/unity-terrain-bundle.git?path=/Packages/com.ruinrelay.landscape-kernel#v0.3.0-preview.1
```

Alternativ Repository herunterladen und **Install package from disk** mit `Packages/com.ruinrelay.landscape-kernel/package.json` auswählen. Git muss für die Git-Installation lokal verfügbar sein.

## Ohne Codex benutzen
1. Bestehende Szene speichern und Play Mode verlassen.
2. **Tools → Landscape Kernel → Live Preview** öffnen und Terrain zuweisen.
3. Start/Ende in Welt-X/Z, Breite und Übergang innerhalb des Terrains wählen.
4. `ridge` für Rücken/Senke oder `corridor` für einen Anschluss wählen. Kamera und Blickziel setzen oder Scene-Kamera übernehmen.
5. Vorschau berechnen; Vorher, Vorschau und Höhenänderung vergleichen. Optional nach Parameteränderungen automatisch aktualisieren lassen.
6. Bilder und Parameter exportieren.

**maxChange ist eine Grenze, keine Intensität.** Für einen Rücken bestimmen amplitude und influence die gewünschte Wirkung. Die Wirkungsschwelle ist zielabhängig, kein universeller Qualitätsmaßstab.

Das Fenster speichert keine Terrainänderung. `ridge` ist derzeit Vorschau-only. Dauerhafte Korridor-/Materialoperationen erfolgen über die dokumentierte Bridge. Keine Behauptung eines allgemeinen Preview-Commit-Workflows.

## Optional: Codex und CLI
Den Ordner `skills/unity-landscape-kernel` in das persönliche Codex-Skillverzeichnis kopieren (üblicherweise `$CODEX_HOME/skills`, sonst `~/.codex/skills`). Bestehende Installation zuerst vergleichen; nicht blind überschreiben. Codex-Skills bei Bedarf neu laden. Den Request-Vertrag aus diesem Repository oder dem installierten UPM-Paket verwenden.

Der Adapter benötigt zusätzlich eine installierte **Unity CLI mit dem Befehl `unity command ... script run`**, eine kompatible **Unity Pipeline** im geöffneten Projekt und `unity` im PATH. Diese externe Integration ist nicht im Bundle enthalten und für das Editorfenster nicht erforderlich. Die normale Unity-Editor-Executable allein ersetzt diese CLI nicht.

```powershell
& ./skills/unity-landscape-kernel/scripts/Invoke-Landscape.ps1 `
  -ProjectPath 'E:/Projects/MyGame' `
  -RequestPath 'E:/TerrainRequests/inspect.json' `
  -OutputDirectory 'E:/TerrainReports'
```

Zuerst `examples/inspect.json` kopieren und Platzhalter ersetzen. Die Antwort liefert echte Terrain-Komponenten-IDs. Danach `examples/preview.json` an Terrainmaß, Ursprung und Kamera anpassen. Die Beispielkoordinaten sind keine universell passenden Werte. Schreiboperationen benötigen den aktuellen Szenenhash. Nie parallele Editor-Schreiboperationen ausführen.

[Alle Felder und Operationen](Packages/com.ruinrelay.landscape-kernel/Documentation~/REQUEST.md).

## Prüfung nach Installation
Mit der optionalen Pipeline lässt sich der isolierte Test ausführen:

```powershell
unity command --project-path 'E:/Projects/MyGame' script run --file ./scripts/SmokeTest.cs --entry TerrainBundleSmokeTest.Run --timeout 180 --format json
```

Der Test verwendet ausschließlich temporäre eigene Terrainobjekte und räumt diese im finally-Block auf. Er speichert keine Szene. Die Repository-Struktur wird mit `python scripts/validate_bundle.py` geprüft.

## Grenzen
- Einzelnes achsenparalleles, unskaliertes Unity Terrain; keine Mesh-Terrain-Deformation oder Spline-Wege.
- Keine automatische Asset-Auflagekorrektur, Fahrzeugabnahme, Wasseranschlussprüfung oder Bewertung der Natürlichkeit.
- Live-Aktualisierung reagiert auf Panelparameter; kein allgemeiner Beobachter aller Terrainbrushes.
- Schutz-/Lochprüfungen sind abgetastet. Dünne Sonderfälle benötigen gesonderte Prüfung.
- Vorschau kopiert TerrainData temporär; Speicherbedarf steigt mit Terraingröße. Animation/Wind wird zwischen Bildern nicht eingefroren.
- Rücknahme prüft Hashes und überschreibt keine späteren fremden Änderungen. Absturz-/Stromausfall-Recovery erfolgt aus dem Backup, nicht als atomare Dateitransaktion.

[Prüfstand und Herkunft](docs/VALIDATION.md). Ein erfolgreicher Test beweist keine gestalterische Qualität.

## Nutzungsrechte
Dieses Repository legt noch keine Open-Source-Lizenz fest. Eine öffentliche Ablage allein erteilt keine allgemeine Weiterverbreitungslizenz. Fremdassets sind nicht enthalten.
