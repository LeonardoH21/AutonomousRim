"""Check modular geometry in a real RimWorld save; never changes the save."""
import argparse
import re
import xml.etree.ElementTree as ET


def cell(text):
    x, _, z = map(int, re.findall(r"-?\d+", text))
    return x, z


def check(path):
    root = ET.parse(path).getroot()
    ai = next(e for e in root.iter() if e.get("Class") == "AutonomousRim.Core.AutonomousRimMapComponent")
    projects = list(ai.find("baseProjects"))
    marker = next(p for p in projects if p.findtext("layoutSlot") == "modular-root")
    ax, az = cell(marker.findtext("layoutAnchor"))
    modules, walls, interiors, bedrooms, farms = {}, {}, set(), 0, 0
    for p in projects:
        slot = p.findtext("layoutSlot", "")
        if not slot.startswith("mod:"):
            continue
        _, gx, gz, mask = slot.split(":")
        gx, gz, mask = int(gx), int(gz), int(mask)
        assert mask in (1, 2, 4, 8, 3, 12, 15), (slot, "unsupported room partition")
        assert not modules.get((gx, gz), 0) & mask, (slot, "overlapping reservation")
        modules[gx, gz] = modules.get((gx, gz), 0) | mask
        ox, oz = ax + gx * 16, az + gz * 16
        if p.findtext("crop") not in (None, "null"):
            cells = {cell(c.text) for c in p.find("storageCells")}
            assert cells == {(x, z) for x in range(ox, ox + 13) for z in range(oz, oz + 13)}, "Growing zone must occupy a full 13x13 module"
            assert p.findtext("requiresRoof") == "False", "Growing module cannot require a roof"
            farms += 1
            continue
        bits = [i for i in range(4) if mask & (1 << i)]
        minx, maxx = min(i % 2 for i in bits), max(i % 2 for i in bits)
        minz, maxz = min(i // 2 for i in bits), max(i // 2 for i in bits)
        x, z = cell(p.findtext("origin"))
        width = int(p.findtext("interiorSize"))
        height = int(p.findtext("interiorHeight", "0")) or width
        assert (x, z) == (ox + minx * 6, oz + minz * 6), (slot, "room outside grid")
        assert (width, height) == (5 + (maxx - minx) * 6, 5 + (maxz - minz) * 6), (slot, "wrong merged-room dimensions")
        inside = {(cx, cz) for cx in range(x + 1, x + width + 1) for cz in range(z + 1, z + height + 1)}
        assert not inside & interiors, (slot, "overlapping interiors")
        interiors |= inside
        floors = {cell(t.findtext("position")) for t in p.find("furniture") if t.findtext("floorDef") == "WoodPlankFloor"}
        assert inside <= floors, (slot, "missing wooden floor plan")
        for t in p.find("shell"):
            pos, definition = cell(t.findtext("position")), t.findtext("def")
            assert pos not in inside, (slot, "partition inside merged room")
            assert pos not in walls or walls[pos] == definition, (slot, "conflicting shared wall")
            walls[pos] = definition
        if p.findtext("kind") == "Quarto":
            assert (width, height) == (5, 5), "Bedroom must be 5x5 internally"
            assert any(t.findtext("def") == "Bed" for t in p.find("furniture")), "Bedroom missing bed"
            bedrooms += 1
    assert not set(walls) & interiors, "A wall crosses another room's interior"
    connected = {(0, 0)}
    while True:
        more = {p for p in modules if any(abs(p[0] - q[0]) + abs(p[1] - q[1]) == 1 for q in connected)}
        if more <= connected:
            break
        connected |= more
    assert set(modules) <= connected, "Isolated module"
    print(f"PASS: {len(modules)} connected modules; {bedrooms} bedrooms; {farms} 13x13 growing zones; shared walls and complete wooden floor plans.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("save")
    check(parser.parse_args().save)
