#!/usr/bin/env python3
"""
Удаляет из 01_Beginning.unity старый данж (Ground_Mines_A + NodeSpawner + DungeonSpawnNodes + NavMeshSurface).
Оставляет DungeonItemSpawner; после запуска вручную проверь YAML ItemSpawner (nodeGenerator/nodesParent/proceduralDungeon).

Использование:
  python Tools/strip_old_mines_dungeon_from_scene.py
"""
from __future__ import annotations

import re
from collections import defaultdict
from pathlib import Path

SCENE = Path(__file__).resolve().parent.parent / "Assets" / "_Game" / "Scenes" / "01_Beginning.unity"

PREFAB_INSTANCE = 766981828
STRIPPED_ROOT_TRANSFORM = 766981829
STRIPPED_GROUND_GO = 392069608
NAVMESH_SURFACE = 766981834
NODE_SPAWNER_GO = 1876080150
NODE_SPAWNER_TRANSFORM = 1876080151
NODE_GEN = 1876080152
DUNGEON_SPAWN_NODES_GO = 1918065120
DUNGEON_SPAWN_NODES_XFORM = 1918065121

ITEM_SPAWNER_XFORM = 1761777821
DUNGEON_GEN_XFORM = 1006535596
PROCEDURAL_GEN = 1006535597

BLOCK_HEADER = re.compile(r"^--- !u!\d+ &(\d+)(?: stripped)?\r?\n", re.MULTILINE)
FATHER = re.compile(r"^\s*m_Father: \{fileID: (\d+)\}\s*$", re.MULTILINE)
M_COMPONENT = re.compile(r"^\s*- component: \{fileID: (\d+)\}\s*$", re.MULTILINE)
M_GAMEOBJECT = re.compile(r"^\s*m_GameObject: \{fileID: (\d+)\}\s*$", re.MULTILINE)


def split_blocks(text: str) -> tuple[str, list[tuple[int, str]]]:
    matches = list(BLOCK_HEADER.finditer(text))
    if not matches:
        return text, []
    preamble = text[: matches[0].start()]
    blocks: list[tuple[int, str]] = []
    for i, m in enumerate(matches):
        start = m.start()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        bid = int(m.group(1))
        blocks.append((bid, text[start:end]))
    return preamble, blocks


def collect_transform_fathers(blocks: list[tuple[int, str]]) -> dict[int, int]:
    fathers: dict[int, int] = {}
    for bid, blk in blocks:
        if not blk.startswith("--- !u!4 &"):
            continue
        mo = FATHER.search(blk)
        if mo:
            fathers[bid] = int(mo.group(1))
    return fathers


def subtree_transform_ids(fathers: dict[int, int], root: int) -> set[int]:
    children: dict[int, list[int]] = defaultdict(list)
    for tid, pa in fathers.items():
        children[pa].append(tid)
    out: set[int] = set()
    stack = [root]
    while stack:
        t = stack.pop()
        if t in out:
            continue
        out.add(t)
        stack.extend(children.get(t, ()))
    return out


def gameobject_components(blocks: list[tuple[int, str]]) -> dict[int, list[int]]:
    m: dict[int, list[int]] = {}
    for bid, blk in blocks:
        if not blk.startswith("--- !u!1 &"):
            continue
        comps = [int(x) for x in M_COMPONENT.findall(blk)]
        if comps:
            m[bid] = comps
    return m


def transform_gameobject(blocks: list[tuple[int, str]]) -> dict[int, int]:
    m: dict[int, int] = {}
    for bid, blk in blocks:
        if not blk.startswith("--- !u!4 &"):
            continue
        mo = M_GAMEOBJECT.search(blk)
        if mo:
            m[bid] = int(mo.group(1))
    return m


def main() -> None:
    raw = SCENE.read_text(encoding="utf-8")
    preamble, blocks = split_blocks(raw)
    fathers = collect_transform_fathers(blocks)
    tf_sub = subtree_transform_ids(fathers, NODE_SPAWNER_TRANSFORM)
    go_components = gameobject_components(blocks)
    tf_go = transform_gameobject(blocks)

    delete_ids: set[int] = {
        PREFAB_INSTANCE,
        STRIPPED_ROOT_TRANSFORM,
        STRIPPED_GROUND_GO,
        NAVMESH_SURFACE,
        NODE_SPAWNER_GO,
        NODE_GEN,
        DUNGEON_SPAWN_NODES_GO,
    }
    delete_ids |= tf_sub
    for tid in tf_sub:
        go = tf_go.get(tid)
        if go:
            delete_ids.add(go)
            delete_ids.update(go_components.get(go, ()))

    new_blocks = [blk for bid, blk in blocks if bid not in delete_ids]
    text = preamble + "".join(new_blocks)

    # ItemSpawner → под DungeonGen
    text, n1 = re.subn(
        rf"(--- !u!4 &{ITEM_SPAWNER_XFORM}\r?\nTransform:[\s\S]*?m_LocalPosition: )[^\n]+\n"
        rf"([\s\S]*?m_LocalScale: )[^\n]+\n"
        rf"([\s\S]*?m_Children: \[\]\r?\n)"
        rf"\s*m_Father: \{{fileID: \d+\}}",
        rf"\1{{x: 0, y: 0, z: 0}}\n\2{{x: 1, y: 1, z: 1}}\n\3  m_Father: {{fileID: {DUNGEON_GEN_XFORM}}}",
        text,
        count=1,
    )

    # Родитель DungeonGen: добавить ребёнка ItemSpawner (если ещё пустой список)
    text = text.replace(
        "m_GameObject: {fileID: 1006535594}\n  serializedVersion: 2\n"
        "  m_LocalRotation: {x: -0, y: -0, z: -0, w: 1}\n"
        "  m_LocalPosition: {x: -0.2, y: -68.96, z: 3.96}\n"
        "  m_LocalScale: {x: 2, y: 2, z: 2}\n"
        "  m_ConstrainProportionsScale: 0\n"
        "  m_Children: []",
        "m_GameObject: {fileID: 1006535594}\n  serializedVersion: 2\n"
        "  m_LocalRotation: {x: -0, y: -0, z: -0, w: 1}\n"
        "  m_LocalPosition: {x: -0.2, y: -68.96, z: 3.96}\n"
        "  m_LocalScale: {x: 2, y: 2, z: 2}\n"
        "  m_ConstrainProportionsScale: 0\n"
        "  m_Children:\n"
        f"  - {{fileID: {ITEM_SPAWNER_XFORM}}}",
        1,
    )

    # ItemSpawner: только процедурный данж (фиксированная замена полей)
    text = re.sub(
        r"(spawnMode: 0\r?\n)"
        r"nodeGenerator: \{fileID: \d+\}\r?\n"
        r"nodesParent: \{fileID: \d+\}\r?\n"
        r"(proceduralDungeon: \{fileID: \d+\}\r?\n)?",
        rf"\1nodeGenerator: {{fileID: 0}}\n"
        rf"nodesParent: {{fileID: 0}}\n"
        rf"proceduralDungeon: {{fileID: {PROCEDURAL_GEN}}}\n",
        text,
        count=1,
    )

    text = re.sub(
        r"(\s*dungeonCenter: )\{fileID: \d+\}",
        rf"\1{{fileID: {DUNGEON_GEN_XFORM}}}",
        text,
        count=1,
    )

    text = re.sub(
        rf"^  - \{{fileID: {PREFAB_INSTANCE}\}}\r?\n",
        "",
        text,
        flags=re.MULTILINE,
    )

    SCENE.write_text(text, encoding="utf-8")
    removed = len(blocks) - len(new_blocks)
    print(f"Updated {SCENE}; removed {removed} blocks. ItemSpawner reparent regex applied: {n1}.")


if __name__ == "__main__":
    main()
