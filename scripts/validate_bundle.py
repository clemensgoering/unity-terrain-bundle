from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
pkg=root/'Packages/com.ruinrelay.landscape-kernel'
manifest=json.loads((pkg/'package.json').read_text(encoding='utf-8-sig'))
assert manifest['version']=='0.3.0-preview.1'
for p in root.rglob('*'):
    if not p.is_file() or '.git' in p.parts: continue
    if p.suffix in {'.cs','.md','.ps1','.json','.uss'}:
        t=p.read_text(encoding='utf-8-sig')
        assert 'C:/Users/cleme' not in t and 'D:/Unity/Projects' not in t, str(p)
for p in (root/'examples').glob('*.json'):
    assert json.loads(p.read_text())['schemaVersion']==1
for name in ('Bridge.cs','LandscapeKernel.cs','TerrainPlan.cs','PreviewEngine.cs','LandscapePreviewWindow.cs'):
    assert (pkg/'Editor'/name).is_file()
assert (root/'skills/unity-landscape-kernel/scripts/Invoke-Landscape.ps1').is_file()
print('Bundle structure, JSON templates and machine-path checks passed.')
