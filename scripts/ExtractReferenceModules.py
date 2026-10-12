"""Read the user's reference save; export portable 13x13 templates without modifying it."""
import argparse
import collections
import hashlib
import html
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path


def cell(text):
    values = [int(n) for n in re.findall(r"-?\d+", text or "")]
    return [values[0], values[2]] if len(values) == 3 else None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("save", type=Path)
    parser.add_argument("game", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    raw = args.save.read_bytes()
    root = ET.fromstring(raw)
    map_node = root.find("game/maps/li")
    definitions, parents = {}, {}
    for path in args.game.joinpath("Data").glob("*/Defs/**/*.xml"):
        try:
            tree = ET.parse(path)
        except ET.ParseError:
            continue
        for node in tree.getroot():
            if node.tag != "ThingDef":
                continue
            if node.get("Name"):
                parents[node.get("Name")] = node
            if node.findtext("defName"):
                definitions[node.findtext("defName")] = node

    def inherited(node, key, seen=None):
        if node is None:
            return None
        found = node.find(key)
        if found is not None:
            return found
        seen = (seen or set()) | {id(node)}
        parent = parents.get(node.get("ParentName"))
        return inherited(parent, key, seen) if parent is not None and id(parent) not in seen else None

    things = map_node.findall("things/thing")
    faction = collections.Counter(t.findtext("faction") for t in things if t.findtext("def") == "Wall" and t.findtext("faction")).most_common(1)[0][0]
    buildings = []
    for t in things:
        node = definitions.get(t.findtext("def"))
        category = inherited(node, "category")
        if t.findtext("faction") != faction or t.findtext("def") == "Human":
            continue
        if category is not None and category.text != "Building" and not t.get("Class", "").startswith("Building"):
            continue
        position = cell(t.findtext("pos"))
        if position is None:
            continue
        size = inherited(node, "size")
        dimensions = [int(v) for v in re.findall(r"\d+", size.text)] if size is not None else [1, 1]
        research = inherited(node, "researchPrerequisites")
        interaction = inherited(node, "interactionCellOffset")
        buildings.append({"def": t.findtext("def"), "position": position,
                          "rotation": int(t.findtext("rot", "0")), "stuff": t.findtext("stuff"),
                          "size": dimensions, "research": [r.text for r in research] if research is not None else [],
                          "interactionOffset": cell(interaction.text) if interaction is not None else None,
                          "temperature": float(t.findtext("targetTemperature")) if t.findtext("targetTemperature") is not None else None,
                          "medical": t.findtext("medical") == "True", "storagePriority": t.findtext("settings/priority"),
                          "storageAllowedDefs": [d.text for d in t.findall("settings/filter/allowedDefs/li")],
                          "storageDisallowedSpecialFilters": [d.text for d in t.findall("settings/filter/disallowedSpecialFilters/li")]})

    barriers = {tuple(t["position"]) for t in buildings if t["def"] in ("Wall", "Door", "Autodoor", "Vent", "Cooler")}
    origins = []
    for x, z in sorted(barriers):
        perimeter = {(a, b) for a in range(x, x + 13) for b in range(z, z + 13)
                     if a in (x, x + 12) or b in (z, z + 12)}
        if perimeter <= barriers:
            origins.append((x, z))

    names = {
        (125, 137): ("dining_recreation", "Refeitório e recreação"),
        (141, 137): ("kitchen_butcher_freezer", "Cozinha, abate e freezer"),
        (125, 153): ("workshop_storage_a", "Estoque e oficina A"),
        (141, 153): ("workshop_storage_b", "Estoque e oficina B"),
        (125, 169): ("four_bedrooms", "Quatro quartos e aquecimento"),
        (141, 169): ("energy_hospital", "Energia e hospital"),
        (157, 137): ("research_two_bedrooms", "Pesquisa e dois quartos"),
    }
    assert set(origins) == set(names), f"Unexpected module boundaries: {origins}. Recheck the reference before reusing."
    modules = []
    for origin in origins:
        key, title = names[origin]
        ox, oz = origin
        selected = []
        for t in buildings:
            if ox <= t["position"][0] <= ox + 12 and oz <= t["position"][1] <= oz + 12:
                selected.append(dict(t, position=[t["position"][0] - ox, t["position"][1] - oz]))
        modules.append({"id": key, "title": title, "referenceOrigin": list(origin), "width": 13, "height": 13,
                        "structures": selected, "counts": dict(collections.Counter(t["def"] for t in selected))})

    # Portable stages retain original positions. Replacements are proposals for
    # native construction, never free swaps or an assertion that the AI already imports them.
    replacements = {"HospitalBed": "Bed", "DoubleBed": "Bed", "ElectricStove": "FueledStove",
                    "ElectricSmithy": "FueledSmithy", "ElectricTailoringBench": "HandTailoringBench",
                    "Autodoor": "Door", "Vent": "Wall", "Cooler": "Wall"}
    optional = {"VitalsMonitor", "FlatscreenTelevision", "Couch", "BilliardsTable", "PokerTable", "ToolCabinet",
                "PlantPot_Bonsai", "ElectricCrematorium", "Bookcase", "MarriageSpot", "PartySpot"}
    for module in modules:
        stages = []
        for t in module["structures"]:
            stages.append({"def": t["def"], "position": t["position"], "rotation": t["rotation"],
                           "requiresResearch": t["research"], "initialReplacement": replacements.get(t["def"]),
                           "medical": module["id"] == "energy_hospital" and t["def"] == "HospitalBed",
                           "optionalAfterBasics": t["def"] in optional,
                           "storageRole": "medicine" if t["storageAllowedDefs"] == ["MedicineHerbal", "MedicineIndustrial", "MedicineUltratech"]
                           else "food" if t["def"] == "Shelf" and module["id"] == "kitchen_butcher_freezer"
                           else "general" if t["def"] == "Shelf" else None})
        module["progressionRules"] = stages
        module["constructionPolicy"] = "Normal jobs, native costs, available research, demand and materials; keep the saved geometry."

    fields = []
    for zone in map_node.findall("zoneManager/allZones/li"):
        if zone.get("Class") != "Zone_Growing":
            continue
        cells = [cell(c.text) for c in zone.findall("cells/li")]
        ox, oz = min(c[0] for c in cells), min(c[1] for c in cells)
        fields.append({"crop": zone.findtext("plantDefToGrow"), "count": len(cells), "origin": [ox, oz],
                       "cells": [[c[0] - ox, c[1] - oz] for c in cells]})

    result = {"sourceSave": str(args.save), "sourceSha256": hashlib.sha256(raw).hexdigest().upper(),
              "schemaVersion": 1, "sourceModified": False, "modules": modules, "fields": fields,
              "notes": ["Reference only: no free research or resources in gameplay.",
                        "Ventilation transfers heat; the reference does not prove climate performance in every biome.",
                        "The medicine shelf uses Preferred in the reference; use Critical in automated gameplay."]}
    args.output.mkdir(parents=True, exist_ok=True)
    args.output.joinpath("reference-modules.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    args.output.joinpath("reference-plan.html").write_text(render(result), encoding="utf-8")
    for module in modules:
        summary = {name: count for name, count in module["counts"].items() if name not in ("Wall", "HiddenConduit", "PowerConduit")}
        print(module["title"], json.dumps(summary, ensure_ascii=False))
    print("FIELDS", [(f["crop"], f["count"]) for f in fields])
    print("SOURCE SHA256", result["sourceSha256"])


def render(result):
    cards = []
    colors = {"Wall": "#39434d", "Door": "#b56932", "Autodoor": "#b56932", "Vent": "#44bdb2",
              "Heater": "#e98643", "Cooler": "#538ecf", "HospitalBed": "#eeece6", "DoubleBed": "#90b798",
              "Shelf": "#cead76", "Battery": "#d8c657", "HiddenConduit": "#90765c", "PowerConduit": "#90765c"}
    short = {"Vent": "AR", "Heater": "AQ", "Cooler": "FR", "WallLamp": "LUZ", "Shelf": "EST",
             "HospitalBed": "MED", "DoubleBed": "CAMA", "Battery": "BAT", "WoodFiredGenerator": "GER",
             "HiTechResearchBench": "PES+", "SimpleResearchBench": "PES", "TableButcher": "AB",
             "ElectricStove": "FOG", "DrugLab": "LAB", "FabricationBench": "FAB"}
    for module in result["modules"]:
        content = [f'<svg viewBox="0 0 390 390" aria-label="{html.escape(module["title"])}">', '<rect width="390" height="390" fill="#c9b395"/>']
        for i in range(14):
            content.append(f'<path d="M{i*30},0 V390 M0,{i*30} H390" stroke="#ac9478" stroke-width=".6"/>')
        for t in module["structures"]:
            if t["def"].endswith("Conduit"):
                continue
            x, z = t["position"]
            w, h = t["size"]
            if t["rotation"] in (1, 3):
                w, h = h, w
            # Native occupied rectangles center even sizes toward the rotation's anchor.
            # Glyphs are anchored to saved positions; exact coordinates are retained in JSON.
            sx, sy = x * 30, (12 - z) * 30
            label = short.get(t["def"], t["def"][:5])
            tooltip = html.escape(f'{t["def"]} ({x},{z}) | rotação {t["rotation"]} | pesquisa: {", ".join(t["research"]) or "inicial"}')
            color = colors.get(t["def"], "#7e93a8")
            content.append(f'<g><title>{tooltip}</title><rect x="{sx+2}" y="{sy+2}" width="26" height="26" rx="3" fill="{color}"/>'
                           + (f'<text x="{sx+15}" y="{sy+18}" text-anchor="middle" font-size="7" fill="#10212b">{html.escape(label)}</text>' if t["def"] != "Wall" else "") + '</g>')
        content.append('</svg>')
        cards.append(f'<article><h2>{html.escape(module["title"])}</h2>{"".join(content)}<p>13×13 · origem no save {module["referenceOrigin"]}</p></article>')
    return '''<!doctype html><html lang="pt-br"><meta charset="utf-8"><title>Módulos do seu save</title>
<style>body{background:#152129;color:#e9e5db;font:16px system-ui;margin:32px}h1{font-size:28px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:24px}article{background:#23333e;padding:18px;border-radius:12px}h2{font-size:18px}svg{width:100%;max-height:390px}p{color:#c0cbc8;font-size:14px}</style>
<h1>Referência extraída: 13X13 Todas as construções</h1><p>Posições reais dos objetos no save. Passe o cursor para ver o objeto e a pesquisa. Cada símbolo marca a posição de origem do objeto; móveis grandes ocupam mais células, registradas no catálogo.</p>
<p>AR = saída de ar · AQ = aquecedor · FR = resfriador · EST = prateleira · PES = pesquisa · GER = gerador. Versão completa de referência: a IA deverá construir as etapas permitidas pela pesquisa.</p>
<main>''' + ''.join(cards) + '</main></html>'


if __name__ == "__main__":
    main()
