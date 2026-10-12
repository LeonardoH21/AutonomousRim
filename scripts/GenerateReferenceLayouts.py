"""Generate the compiled reference coordinates without reading or changing any gameplay save."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
d=json.loads((root/'outputs/ReferenceModules/reference-modules.json').read_text(encoding='utf-8'))
assert d['sourceSha256']=='D5B5BFEDE9E265261EDB8DB0DD6A53A15D6600EB72EE140C28ABB638CE46D81A'
lines=['// Generated from the user\'s unmodified 13X13 reference save; see docs/MODULOS-REFERENCIA-DO-USUARIO.md.','namespace AutonomousRim.Planning {','internal static class ReferenceLayoutData {','internal struct Entry { public string Def; public int X,Z,Rotation; public Entry(string d,int x,int z,int r){Def=d;X=x;Z=z;Rotation=r;} }','internal static Entry[] Get(string id) { switch(id) {']
for m in d['modules']:
 if m['id'] not in ['kitchen_butcher_freezer','energy_hospital','research_two_bedrooms','dining_recreation']:continue
 lines.append('case "'+m['id']+'": return new Entry[] {')
 for b in m['structures']:
  if 'Conduit' in b['def']:continue
  lines.append('new Entry("%s",%d,%d,%d),'%(b['def'],*b['position'],b['rotation']))
 lines.append('};')
lines+=['default: throw new System.ArgumentException(id);','} } } }']
(root/'Source/AutonomousRim/Planning/ReferenceLayoutData.cs').write_text('\n'.join(lines),encoding='utf-8')
