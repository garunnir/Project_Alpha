"""
Cataclysm-BN JSON → Project_Pictureless 정제 JSON 변환기

사용법:
    python convert.py --bn-path "Z:/Work/Project/Cataclysm-BN" --output "../../Assets/StreamingAssets/BNData"
    python convert.py --bn-path "Z:/Work/Project/Cataclysm-BN" --output "../../Assets/StreamingAssets/BNData" --locale-only
    python convert.py --bn-path "Z:/Work/Project/Cataclysm-BN" --output "../../Assets/StreamingAssets/BNData" --mapgen
    python export_mapgen.py --bn-path "Z:/Work/Project/Cataclysm-BN" --output "../../Assets/StreamingAssets/BNData/mapgen"

CC BY-SA 3.0 라이선스 준수:
    출력 JSON은 Cataclysm: Bright Nights 의 파생 저작물이며,
    동일한 CC BY-SA 3.0 라이선스가 적용됩니다.
    https://creativecommons.org/licenses/by-sa/3.0/
"""

import argparse
import copy
import json
import os
import sys
from pathlib import Path
from typing import Any


# ── Helpers ─────────────────────────────────────────────────────────

def load_all_json(directory: Path) -> list[dict]:
    """디렉토리 내 모든 .json 을 재귀적으로 읽어 리스트로 반환."""
    entries: list[dict] = []
    for path in sorted(directory.rglob("*.json")):
        try:
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)
        except (json.JSONDecodeError, UnicodeDecodeError) as e:
            print(f"  [WARN] skip {path}: {e}", file=sys.stderr)
            continue
        if isinstance(data, list):
            entries.extend(data)
        elif isinstance(data, dict):
            entries.append(data)
    return entries


def deep_merge(base: dict, override: dict) -> dict:
    """base 위에 override 를 얹는 deep merge. 배열은 override가 덮어씀."""
    result = copy.deepcopy(base)
    for key, val in override.items():
        if key in ("copy-from", "abstract", "looks_like"):
            continue
        if key == "extend":
            for ek, ev in val.items():
                if ek in result and isinstance(result[ek], list):
                    if isinstance(ev, list):
                        result[ek] = result[ek] + ev
                    else:
                        result[ek].append(ev)
                else:
                    result[ek] = ev if isinstance(ev, list) else [ev]
            continue
        if key == "proportional":
            for pk, pv in val.items():
                if pk in result:
                    result[pk] = _apply_scale(result[pk], pv)
            continue
        if key == "relative":
            for rk, rv in val.items():
                if rk in result:
                    result[rk] = _apply_add(result[rk], rv)
                elif isinstance(rv, dict) or _is_number(rv):
                    result[rk] = copy.deepcopy(rv)
            continue
        if key == "delete":
            for dk, dv in val.items():
                if dk in result and isinstance(result[dk], list):
                    result[dk] = [x for x in result[dk] if x not in dv]
            continue
        if (
            key in _DAMAGE_MERGE_KEYS
            and key in result
            and isinstance(result[key], dict)
            and isinstance(val, dict)
        ):
            merged = copy.deepcopy(result[key])
            merged.update(copy.deepcopy(val))
            result[key] = merged
            continue
        result[key] = copy.deepcopy(val)
    return result


# Dist WorldClockSettings.DefaultMinutesPerDay. Converter cannot import C#.
MINUTES_PER_DAY = 24 * 60

# Terrain/furniture flags Dist farming actually consumes. All other flags are dropped.
FARMING_FLAGS = frozenset({
    "PLANTABLE",
    "PLOWABLE",
    "DIGGABLE",
    "MINEABLE",
    "PLANT",
    "GROWTH_SEED",
    "GROWTH_SEEDLING",
    "GROWTH_MATURE",
    "GROWTH_HARVEST",
})

# Furniture/terrain craft environment flags Dist cooking consumes.
CRAFTING_FURNITURE_FLAGS = frozenset({
    "FIRE_CONTAINER",
    "FIRE",
    "LIT",
    "COOK",
    "SMOKE",
    "SMOKER",
})

# Recipe flags Dist cooking/light gate consumes.
RECIPE_FLAG_WHITELIST = frozenset({
    "DARK",
    "BLIND_EASY",
    "BLIND_HARD",
})


def parse_duration_to_minutes(t) -> float:
    """BN duration string → minutes. Example: '7 h 40 m' → 460.0. Does not accept ints."""
    if not isinstance(t, str):
        return 0.0
    t = t.strip()
    total = 0.0
    parts = t.replace(",", " ").split()
    i = 0
    while i < len(parts):
        try:
            num = float(parts[i])
        except ValueError:
            i += 1
            continue
        unit = parts[i + 1] if i + 1 < len(parts) else "m"
        if unit.startswith("s"):
            total += num / 60.0
        elif unit.startswith("m"):
            total += num
        elif unit.startswith("h"):
            total += num * 60.0
        elif unit.startswith("d"):
            total += num * MINUTES_PER_DAY
        else:
            total += num
        i += 2
    return round(total, 2) if total else 0.0


def parse_time_to_minutes(t) -> float:
    """Recipe/comestible time. Int = moves/100. Strings = parse_duration_to_minutes."""
    if isinstance(t, bool):
        return 0.0
    if isinstance(t, (int, float)):
        return t / 100.0  # movement points → minutes (rough)
    return parse_duration_to_minutes(t)


def parse_grow_to_minutes(t) -> float:
    """BN seed grow → minutes. Int = season-days × MINUTES_PER_DAY. Never moves/100."""
    if isinstance(t, bool):
        return 0.0
    if isinstance(t, (int, float)):
        return float(t) * MINUTES_PER_DAY
    return parse_duration_to_minutes(t)


def parse_weight_to_g(w) -> int:
    if isinstance(w, (int, float)):
        return int(w)
    if not isinstance(w, str):
        return 0
    w = w.strip()
    if w.endswith("kg"):
        return int(float(w[:-2].strip()) * 1000)
    if w.endswith("mg"):
        return max(1, int(float(w[:-2].strip()) / 1000))
    if w.endswith("g"):
        return int(float(w[:-1].strip()))
    # "200 g" style with space
    parts = w.split()
    if len(parts) == 2:
        try:
            num = float(parts[0])
            unit = parts[1].lower()
            if unit == "kg":
                return int(num * 1000)
            if unit == "mg":
                return max(1, int(num / 1000))
            if unit == "g":
                return int(num)
        except ValueError:
            pass
    try:
        return int(float(w))
    except ValueError:
        return 0


def parse_volume_to_ml(v) -> int:
    if isinstance(v, (int, float)):
        return int(v) * 250  # legacy: units of 250ml
    if not isinstance(v, str):
        return 0
    v = v.strip()
    if v.endswith("L"):
        return int(float(v[:-1].strip()) * 1000)
    if v.endswith("ml"):
        return int(float(v[:-2].strip()))
    try:
        return int(float(v)) * 250
    except ValueError:
        return 0


def get_item_name(entry: dict) -> str:
    name = entry.get("name", "")
    if isinstance(name, dict):
        return name.get("str", name.get("str_sp", name.get("str_pl", "")))
    return str(name)


# ── Phase: Items ────────────────────────────────────────────────────

def load_items(bn_path: Path) -> dict[str, dict]:
    """모든 아이템 정의를 읽고 copy-from 상속을 해소하여 id→data 맵 반환."""
    items_dir = bn_path / "data" / "json" / "items"
    obsoletion_dir = bn_path / "data" / "json" / "obsoletion"
    print(f"[items] Loading from {items_dir} ...")
    raw = load_all_json(items_dir)
    if obsoletion_dir.exists():
        print(f"[items] Loading from {obsoletion_dir} ...")
        raw.extend(load_all_json(obsoletion_dir))

    # 1차: type별 인덱스
    ITEM_TYPES = {
        "GENERIC", "COMESTIBLE", "ARMOR", "TOOL", "GUN", "AMMO",
        "BOOK", "MAGAZINE", "GUNMOD", "TOOL_ARMOR", "BIONIC_ITEM",
        "PET_ARMOR", "ENGINE",
        # containers/tools that are used as targets by recipes
        "CONTAINER",
        "TOOLMOD",
        # vehicle/obsolete entries that are still referenced by recipes/tools
        "WHEEL",
        "MIGRATION",
        "BATTERY",
    }
    by_id: dict[str, dict] = {}
    abstracts: dict[str, dict] = {}

    for entry in raw:
        t = entry.get("type", "")
        if t not in ITEM_TYPES:
            continue
        eid = entry.get("id") or entry.get("abstract")
        if not eid:
            continue
        if isinstance(eid, list):
            # obsoletion 등에서 id/abstract가 list로 들어오는 경우가 있어 방어
            eid = eid[0] if len(eid) > 0 and isinstance(eid[0], str) else None
        if not isinstance(eid, str):
            continue
        if "abstract" in entry:
            abstracts[eid] = entry
        else:
            by_id[eid] = entry

    # 2차: copy-from 해소
    resolved: dict[str, dict] = {}
    resolve_stack: set[str] = set()

    def resolve(eid: str) -> dict | None:
        if eid in resolved:
            return resolved[eid]
        if eid in resolve_stack:
            return None  # circular
        resolve_stack.add(eid)

        entry = by_id.get(eid) or abstracts.get(eid)
        if entry is None:
            resolve_stack.discard(eid)
            return None

        parent_id = entry.get("copy-from")
        if parent_id and parent_id != eid:
            parent = resolve(parent_id)
            if parent:
                entry = deep_merge(parent, entry)

        if eid in by_id:
            resolved[eid] = entry
        resolve_stack.discard(eid)
        return entry

    for eid in list(by_id.keys()):
        resolve(eid)

    # abstract 중에도 copy-from 체인 필요하므로 resolve하되 출력은 안 함
    for eid in list(abstracts.keys()):
        resolve(eid)

    print(f"[items] Resolved {len(resolved)} items ({len(abstracts)} abstracts skipped)")
    return resolved


def get_item_description(entry: dict) -> str:
    description = entry.get("description", "")
    if isinstance(description, dict):
        return str(description.get("str", "") or "")
    return str(description or "")


DURABLE_ITEM_TYPES = frozenset({
    "ARMOR", "TOOL", "GUN", "TOOL_ARMOR", "GUNMOD", "MAGAZINE",
    "ENGINE", "WHEEL", "PET_ARMOR", "BIONIC_ITEM",
})


def compute_has_durability(entry: dict, item_type: str) -> bool:
    if entry.get("armor_data") or entry.get("gun_data"):
        return True
    return item_type in DURABLE_ITEM_TYPES


def _int_or_zero(value) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return 0


def _float_or_zero(value) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return 0.0


def _is_number(value) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def _as_int(value) -> int:
    if not _is_number(value):
        try:
            return int(value)
        except (TypeError, ValueError):
            return 0
    return int(round(float(value)))


# copy-from 시 부분 덮어쓰기. 통째 치환하면 damage_type이 유실된다.
_DAMAGE_MERGE_KEYS = frozenset({"damage", "ranged_damage", "shot_damage"})


def _apply_scale(target, factor):
    """proportional: 숫자×배수, damage 객체는 필드별 또는 전 수치 필드."""
    if _is_number(target) and _is_number(factor):
        return target * factor
    if isinstance(target, dict) and isinstance(factor, dict):
        out = copy.deepcopy(target)
        for key, val in factor.items():
            if key in out:
                out[key] = _apply_scale(out[key], val)
        return out
    if isinstance(target, dict) and _is_number(factor):
        out = copy.deepcopy(target)
        for key, val in out.items():
            if _is_number(val):
                out[key] = val * factor
        return out
    return target


def _apply_add(target, delta):
    """relative: 숫자 가산, damage 객체는 필드별 또는 amount만."""
    if _is_number(target) and _is_number(delta):
        return target + delta
    if isinstance(target, dict) and isinstance(delta, dict):
        out = copy.deepcopy(target)
        for key, val in delta.items():
            if key in out:
                out[key] = _apply_add(out[key], val)
            elif _is_number(val):
                out[key] = val
        return out
    if isinstance(target, dict) and _is_number(delta):
        out = copy.deepcopy(target)
        if "amount" in out and _is_number(out["amount"]):
            out["amount"] = out["amount"] + delta
        return out
    return target


def _damage_units(value) -> list[dict]:
    """BN damage: int | unit dict | {values:[...]} | list → unit dict 목록."""
    if value is None or isinstance(value, bool):
        return []
    if _is_number(value):
        return [{"amount": value}]
    if isinstance(value, list):
        units: list[dict] = []
        for item in value:
            units.extend(_damage_units(item))
        return units
    if isinstance(value, dict):
        if "values" in value:
            return _damage_units(value.get("values"))
        return [value]
    return []


def flatten_damage(value) -> dict:
    """BN 피해 객체 → Dist 스칼라 {amount, pierce, damage_type}."""
    amount = 0.0
    pierce = 0.0
    damage_type = ""
    for unit in _damage_units(value):
        if "amount" in unit:
            amount += _float_or_zero(unit.get("amount"))
        ap = unit.get("armor_penetration", unit.get("pierce"))
        if ap is not None:
            pierce += _float_or_zero(ap)
        if not damage_type and unit.get("damage_type"):
            damage_type = str(unit.get("damage_type"))
    return {
        "amount": int(round(amount)),
        "pierce": int(round(pierce)),
        "damage_type": damage_type,
    }


# Dist BodyPartIds — BN plural / either covers → L/R parts.
_BN_COVER_EXPAND: dict[str, list[str]] = {
    "arms": ["arm_l", "arm_r"],
    "legs": ["leg_l", "leg_r"],
    "hands": ["hand_l", "hand_r"],
    "feet": ["foot_l", "foot_r"],
    "arm_either": ["arm_l", "arm_r"],
    "leg_either": ["leg_l", "leg_r"],
    "hand_either": ["hand_l", "hand_r"],
    "foot_either": ["foot_l", "foot_r"],
}

# Unilateral Dist parts → sided=true when covers is only these.
_BN_SIDED_PARTS = frozenset({
    "arm_l", "arm_r", "hand_l", "hand_r",
    "leg_l", "leg_r", "foot_l", "foot_r",
})

# BN layer is usually a flag (not `layer` field). Outer → under priority.
_BN_LAYER_FLAG_TO_DIST: tuple[tuple[str, str], ...] = (
    ("AURA", "AURA"),
    ("PERSONAL", "PERSONAL"),
    ("WAIST", "WAIST"),
    ("BELTED", "BELTED"),
    ("OUTER", "OUTER"),
    ("SKINTIGHT", "UNDER"),
)


def export_armor_detail(entry: dict, item_type: str) -> dict | None:
    """Armor whitelist bake. Intentional omissions (wear cost):
    docs/equipment/GEAR.md §BN Bake Omissions — keep in sync when changing keys below.
    Phase B: storage volume_ml + optional pockets(volume_ml, moves).
    Phase C: layer (str|flags) + sided (bool|inferred) + Dist covers expand.
    """
    source = entry.get("armor_data")
    if item_type in ("ARMOR", "TOOL_ARMOR", "PET_ARMOR"):
        source = entry if source is None else {**entry, **source}
    if not source:
        return None

    armor = {}
    covers = _export_armor_covers(source)
    if covers:
        armor["covers"] = covers
    for key in (
        "coverage", "encumbrance", "max_encumbrance", "warmth",
        "environmental_protection", "material_thickness",
    ):
        if key in source:
            armor[key] = _int_or_zero(source.get(key))
    if "power_armor" in source:
        armor["power_armor"] = bool(source.get("power_armor"))

    layer = _export_armor_layer(source)
    if layer:
        armor["layer"] = layer

    sided = _export_armor_sided(source, covers)
    if sided is not None:
        armor["sided"] = sided

    # BN legacy storage is volume string ("3 L" / "500 ml"); Dist stores ml.
    storage_ml = 0
    if "storage" in source:
        storage_ml = parse_volume_to_ml(source.get("storage"))
        if storage_ml > 0:
            armor["storage"] = storage_ml

    pockets = _export_armor_pockets(source)
    if pockets:
        armor["pockets"] = pockets
        pocket_vol = sum(int(p.get("volume_ml", 0) or 0) for p in pockets)
        if "storage" not in armor and pocket_vol > 0:
            armor["storage"] = pocket_vol

    return armor or None


def _export_armor_covers(source: dict) -> list[str]:
    """Expand BN plural/either covers to Dist BodyPartIds (hand_l/hand_r, …)."""
    raw = source.get("covers")
    if not raw:
        return []
    parts = raw if isinstance(raw, list) else [raw]
    out: list[str] = []
    seen: set[str] = set()
    for part in parts:
        if part is None:
            continue
        key = str(part).strip()
        if not key:
            continue
        expanded = _BN_COVER_EXPAND.get(key)
        if expanded:
            for e in expanded:
                if e not in seen:
                    seen.add(e)
                    out.append(e)
            continue
        if key not in seen:
            seen.add(key)
            out.append(key)
    return out


def _export_armor_layer(source: dict) -> str | None:
    """BN layer field (string/list) or flags SKINTIGHT/OUTER/BELTED/WAIST/… → Dist layer."""
    raw = source.get("layer")
    if raw is not None:
        if isinstance(raw, list):
            for entry in raw:
                if entry is None:
                    continue
                text = str(entry).strip()
                if text:
                    return text
        else:
            text = str(raw).strip()
            if text:
                return text

    flags = source.get("flags")
    if not isinstance(flags, list):
        return None
    flag_set = {str(f).strip().upper() for f in flags if f is not None}
    for flag, dist_layer in _BN_LAYER_FLAG_TO_DIST:
        if flag in flag_set:
            return dist_layer
    return None


def _export_armor_sided(source: dict, covers: list[str]) -> bool | None:
    """Explicit BN sided, or infer from either-covers / unilateral Dist parts."""
    if "sided" in source:
        return bool(source.get("sided"))

    raw = source.get("covers")
    parts = raw if isinstance(raw, list) else ([raw] if raw else [])
    for part in parts:
        if part is None:
            continue
        key = str(part).strip()
        if key.endswith("_either"):
            return True

    if covers and all(p in _BN_SIDED_PARTS for p in covers) and len(covers) == 1:
        return True
    return None


def _export_armor_pockets(source: dict) -> list[dict]:
    """BN/DDA pocket_data → [{volume_ml, moves}]. BN often has storage only (no pocket_data)."""
    raw = source.get("pocket_data")
    if not isinstance(raw, list):
        return []

    pockets: list[dict] = []
    for pocket in raw:
        if not isinstance(pocket, dict):
            continue
        vol_raw = (
            pocket.get("max_contains_volume")
            or pocket.get("volume_capacity")
            or pocket.get("max_volume")
            or 0
        )
        volume_ml = parse_volume_to_ml(vol_raw)
        moves = _int_or_zero(pocket.get("moves"))
        if volume_ml <= 0 and moves <= 0:
            continue
        entry = {"volume_ml": volume_ml}
        if moves > 0:
            entry["moves"] = moves
        pockets.append(entry)
    return pockets


def export_gun_detail(entry: dict, item_type: str) -> dict | None:
    source = entry.get("gun_data")
    if item_type == "GUN":
        source = entry if source is None else {**entry, **source}
    if not source:
        return None

    gun = {}
    if source.get("skill"):
        gun["skill"] = str(source.get("skill"))
    ammo = source.get("ammo")
    if ammo:
        gun["ammo"] = ammo if isinstance(ammo, list) else [ammo]
    if "ranged_damage" in source:
        gun["ranged_damage"] = flatten_damage(source.get("ranged_damage"))["amount"]
    for key in (
        "range", "dispersion", "recoil", "durability",
        "clip_size", "reload", "burst",
        "sight_dispersion", "aim_speed", "handling",
    ):
        if key in source:
            gun[key] = _as_int(source.get(key))
    magazines = export_gun_magazines(source.get("magazines"))
    if magazines:
        gun["magazines"] = magazines
    return gun or None


def export_gun_magazines(raw) -> list[dict] | None:
    """BN magazines: [[ammo_type, [mag_id, ...]], ...] or {ammo_type: [mag_id, ...]}."""
    if not raw:
        return None

    groups: list[dict] = []
    if isinstance(raw, dict):
        for ammo_type, mags in raw.items():
            ids = mags if isinstance(mags, list) else [mags]
            cleaned = [str(mag_id) for mag_id in ids if mag_id]
            if not cleaned:
                continue
            groups.append({"ammo_type": str(ammo_type), "magazines": cleaned})
        return groups or None

    if not isinstance(raw, list):
        return None

    for row in raw:
        if not isinstance(row, list) or len(row) < 2:
            continue
        ammo_type = row[0]
        mag_list = row[1] if isinstance(row[1], list) else [row[1]]
        cleaned = [str(mag_id) for mag_id in mag_list if mag_id]
        if ammo_type is None or not cleaned:
            continue
        groups.append({"ammo_type": str(ammo_type), "magazines": cleaned})
    return groups or None


def export_tool_detail(entry: dict, item_type: str) -> dict | None:
    if item_type not in ("TOOL", "TOOL_ARMOR", "GUNMOD", "TOOLMOD", "BIONIC_ITEM", "ENGINE"):
        return None

    tool = {}
    for key in ("max_charges", "initial_charges", "charges_per_use", "turns_per_charge"):
        if key in entry:
            tool[key] = _int_or_zero(entry.get(key))
    ammo = entry.get("ammo")
    if ammo:
        tool["ammo"] = ammo if isinstance(ammo, list) else [ammo]
    if entry.get("revert_to"):
        tool["revert_to"] = str(entry.get("revert_to"))
    return tool or None


USE_ACTION_TYPE_HEAL = "heal"
USE_ACTION_TYPE_CONSUME_DRUG = "consume_drug"
USE_ACTION_TYPE_ANTIBIOTIC = "antibiotic"
USE_ACTION_TYPE_WEAK_ANTIBIOTIC = "weak_antibiotic"
USE_ACTION_TYPE_STRONG_ANTIBIOTIC = "strong_antibiotic"
USE_ACTION_TYPE_MULTICOOKER = "multicooker"
CONSUME_USE_ACTION_TYPES = frozenset(
    {
        USE_ACTION_TYPE_HEAL,
        USE_ACTION_TYPE_CONSUME_DRUG,
        USE_ACTION_TYPE_ANTIBIOTIC,
        USE_ACTION_TYPE_WEAK_ANTIBIOTIC,
        USE_ACTION_TYPE_STRONG_ANTIBIOTIC,
        USE_ACTION_TYPE_MULTICOOKER,
    }
)
ANTIBIOTIC_USE_ACTION_TYPES = frozenset(
    {
        USE_ACTION_TYPE_ANTIBIOTIC,
        USE_ACTION_TYPE_WEAK_ANTIBIOTIC,
        USE_ACTION_TYPE_STRONG_ANTIBIOTIC,
    }
)

_HEAL_POWER_KEYS = (
    "limb_power",
    "bandages_power",
    "head_power",
    "torso_power",
    "amount",
    "bleed",
)
_SCALAR_AMOUNT_KEYS = ("amount", "min", "limb_power")
_VITAMIN_ID_KEYS = ("id", "vitamin")
_VITAMIN_AMOUNT_KEYS = ("amount", "vitamins")
_EFFECT_ID_KEYS = ("id", "effect_id", "effect")


def _flatten_scalar_amount(value) -> int | None:
    """BN scalar that may be int, [min, max], or {amount:...}. None if unreadable."""
    if value is None or isinstance(value, bool):
        return None
    if _is_number(value):
        return int(round(value))
    if isinstance(value, list):
        for item in value:
            flat = _flatten_scalar_amount(item)
            if flat is not None:
                return flat
        return None
    if isinstance(value, dict):
        for key in _SCALAR_AMOUNT_KEYS:
            if key in value:
                return _flatten_scalar_amount(value.get(key))
        for nested in value.values():
            flat = _flatten_scalar_amount(nested)
            if flat is not None:
                return flat
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def flatten_vitamins(value) -> dict[str, int] | None:
    """BN vitamins pair-list or object → {id: amount}."""
    if not value:
        return None
    out: dict[str, int] = {}
    if isinstance(value, dict):
        for key, val in value.items():
            amount = _flatten_scalar_amount(val)
            if amount is not None:
                out[str(key)] = amount
        return out or None
    if isinstance(value, list):
        for item in value:
            if isinstance(item, (list, tuple)) and len(item) >= 2:
                amount = _flatten_scalar_amount(item[1])
                if amount is not None:
                    out[str(item[0])] = amount
            elif isinstance(item, dict):
                vitamin_id = next(
                    (item.get(key) for key in _VITAMIN_ID_KEYS if item.get(key)),
                    None,
                )
                if not vitamin_id:
                    continue
                raw_amount = next(
                    (item.get(key) for key in _VITAMIN_AMOUNT_KEYS if key in item),
                    None,
                )
                amount = _flatten_scalar_amount(raw_amount)
                if amount is not None:
                    out[str(vitamin_id)] = amount
        return out or None
    return None


def _use_action_type(action) -> str:
    if isinstance(action, str):
        return action.strip().lower()
    if isinstance(action, dict) and action.get("type"):
        return str(action.get("type")).strip().lower()
    return ""


def _iter_use_actions(value):
    if value is None:
        return
    if isinstance(value, list):
        for item in value:
            yield item
        return
    yield value


def _flatten_use_action_duration(value) -> int | None:
    if isinstance(value, str):
        minutes = parse_time_to_minutes(value)
        return int(round(minutes)) if minutes else None
    return _flatten_scalar_amount(value)


def _first_consume_effect(action: dict) -> dict | None:
    effects = action.get("effects")
    if isinstance(effects, dict):
        return effects
    if isinstance(effects, list):
        for item in effects:
            if isinstance(item, dict):
                return item
            if isinstance(item, str) and item:
                return {"id": item}
    effect_id = next((action.get(key) for key in _EFFECT_ID_KEYS if action.get(key)), None)
    if not effect_id:
        return None
    effect = {"id": effect_id}
    if "duration" in action:
        effect["duration"] = action.get("duration")
    return effect


def export_consume_use_action(entry: dict) -> dict | None:
    """Whitelist heal / consume_drug / antibiotic / multicooker. Unwrap nested values on the same BN key. Never drop/tick/countdown."""
    for action in _iter_use_actions(entry.get("use_action")):
        action_type = _use_action_type(action)
        if action_type not in CONSUME_USE_ACTION_TYPES:
            continue
        out = {"type": action_type}
        if action_type in ANTIBIOTIC_USE_ACTION_TYPES:
            return out
        if action_type == USE_ACTION_TYPE_MULTICOOKER:
            return out
        if isinstance(action, dict):
            if action_type == USE_ACTION_TYPE_HEAL:
                for key in _HEAL_POWER_KEYS:
                    if key not in action:
                        continue
                    amount = _flatten_scalar_amount(action.get(key))
                    if amount is not None:
                        out[key] = amount
            elif action_type == USE_ACTION_TYPE_CONSUME_DRUG:
                effect = _first_consume_effect(action)
                if effect:
                    effect_id = next(
                        (effect.get(key) for key in _EFFECT_ID_KEYS if effect.get(key)),
                        None,
                    )
                    if effect_id:
                        out["effect_id"] = str(effect_id)
                    if "duration" in effect:
                        duration = _flatten_use_action_duration(effect.get("duration"))
                        if duration is not None:
                            out["duration"] = duration
        return out
    return None


def export_comestible_detail(entry: dict, item_type: str) -> dict | None:
    if item_type != "COMESTIBLE":
        return None

    comestible = {}
    for key in ("calories", "quench", "fun", "healthy", "stim", "charges"):
        if key in entry:
            comestible[key] = _int_or_zero(entry.get(key))
    if entry.get("spoils_in") is not None:
        comestible["spoils_in_minutes"] = parse_time_to_minutes(entry.get("spoils_in"))
    if entry.get("addiction_type"):
        comestible["addiction_type"] = str(entry.get("addiction_type"))
    if "addiction_potential" in entry:
        potential = _flatten_scalar_amount(entry.get("addiction_potential"))
        if potential is not None:
            comestible["addiction_potential"] = potential
    vitamins = flatten_vitamins(entry.get("vitamins"))
    if vitamins:
        comestible["vitamins"] = vitamins
    if entry.get("cooks_like"):
        comestible["cooks_like"] = str(entry.get("cooks_like"))
    if entry.get("smoking_result"):
        comestible["smoking_result"] = str(entry.get("smoking_result"))
    return comestible or None


def export_ammo_detail(entry: dict, item_type: str) -> dict | None:
    if item_type != "AMMO":
        return None

    ammo = {}
    if entry.get("ammo_type"):
        ammo["ammo_type"] = str(entry.get("ammo_type"))
    if "damage" in entry:
        flat = flatten_damage(entry.get("damage"))
        ammo["damage"] = flat["amount"]
        if flat["pierce"]:
            ammo["pierce"] = flat["pierce"]
        if flat["damage_type"]:
            ammo["damage_type"] = flat["damage_type"]
    if "pierce" in entry and "pierce" not in ammo:
        ammo["pierce"] = _as_int(entry.get("pierce"))
    for key in ("range", "dispersion", "recoil", "count"):
        if key in entry:
            ammo[key] = _as_int(entry.get(key))
    if "shot_damage" in entry:
        ammo["shot_damage"] = flatten_damage(entry.get("shot_damage"))["amount"]
    if "projectile_count" in entry:
        ammo["projectile_count"] = _as_int(entry.get("projectile_count"))
    if "shot_spread" in entry:
        ammo["shot_spread"] = _as_int(entry.get("shot_spread"))
    effects = entry.get("effects")
    if effects:
        ammo["effects"] = effects if isinstance(effects, list) else [str(effects)]
    if entry.get("casing"):
        ammo["casing"] = str(entry.get("casing"))
    if "loudness" in entry:
        ammo["loudness"] = _as_int(entry.get("loudness"))
    return ammo or None


def export_magazine_detail(entry: dict, item_type: str) -> dict | None:
    if item_type != "MAGAZINE":
        return None

    magazine = {}
    ammo_type = entry.get("ammo_type")
    if ammo_type:
        magazine["ammo_type"] = ammo_type if isinstance(ammo_type, list) else [ammo_type]
    for key in ("capacity", "reliability", "reload_time"):
        if key in entry:
            magazine[key] = _int_or_zero(entry.get(key))
    if entry.get("default_ammo"):
        magazine["default_ammo"] = str(entry.get("default_ammo"))
    return magazine or None


def export_book_detail(entry: dict, item_type: str) -> dict | None:
    source = entry.get("book_data")
    if item_type == "BOOK":
        source = entry if source is None else {**entry, **source}
    if not source:
        return None

    book = {}
    for key in ("intelligence", "fun", "chapters"):
        if key in source:
            book[key] = _int_or_zero(source.get(key))
    if source.get("time") is not None:
        book["read_time_minutes"] = parse_time_to_minutes(source.get("time"))
    return book or None


def export_container_detail(entry: dict, item_type: str) -> dict | None:
    source = entry.get("container_data")
    if item_type == "CONTAINER":
        source = entry if source is None else {**entry, **source}
    if not source:
        return None

    detail = {}
    for key in ("seals", "watertight", "preserves"):
        if key in source:
            detail[key] = bool(source.get(key))
    return detail or None


def export_item_game_detail(entry: dict, item_type: str) -> dict:
    detail: dict[str, Any] = {}

    description = get_item_description(entry)
    if description:
        detail["description"] = description

    subcategory = entry.get("subcategory", "")
    if subcategory:
        detail["subcategory"] = str(subcategory)

    stack_size = entry.get("stack_size", entry.get("count", 0))
    if stack_size:
        detail["max_stack"] = _int_or_zero(stack_size)

    detail["has_durability"] = compute_has_durability(entry, item_type)

    if entry.get("repairs_like"):
        detail["repairs_like"] = str(entry.get("repairs_like"))
    if entry.get("repair_difficulty") is not None:
        detail["repair_difficulty"] = _int_or_zero(entry.get("repair_difficulty"))

    for key in ("bashing", "cutting", "to_hit"):
        if key in entry:
            detail[key] = _int_or_zero(entry.get(key))

    weapon_category = entry.get("weapon_category")
    if weapon_category:
        detail["weapon_category"] = weapon_category if isinstance(weapon_category, list) else [weapon_category]

    techniques = entry.get("techniques")
    if techniques:
        detail["techniques"] = techniques if isinstance(techniques, list) else [techniques]

    armor = export_armor_detail(entry, item_type)
    if armor:
        detail["armor"] = armor
    gun = export_gun_detail(entry, item_type)
    if gun:
        detail["gun"] = gun
    tool = export_tool_detail(entry, item_type)
    if tool:
        detail["tool"] = tool
    comestible = export_comestible_detail(entry, item_type)
    if comestible:
        detail["comestible"] = comestible
    ammo = export_ammo_detail(entry, item_type)
    if ammo:
        detail["ammo"] = ammo
    magazine = export_magazine_detail(entry, item_type)
    if magazine:
        detail["magazine"] = magazine
    book = export_book_detail(entry, item_type)
    if book:
        detail["book"] = book
    container_detail = export_container_detail(entry, item_type)
    if container_detail:
        detail["container_detail"] = container_detail
    use_action = export_consume_use_action(entry)
    if use_action:
        detail["use_action"] = use_action
    seed = export_seed_detail(entry)
    if seed:
        detail["seed"] = seed

    return detail


def export_seed_detail(entry: dict) -> dict | None:
    """Whitelist seed_data when present. brewable/milling/fuel stay Parked."""
    source = entry.get("seed_data")
    if not isinstance(source, dict):
        return None

    seed: dict[str, Any] = {}
    if source.get("fruit"):
        seed["fruit"] = str(source.get("fruit"))

    plant_name = source.get("plant_name")
    if plant_name:
        if isinstance(plant_name, dict):
            plant_name = plant_name.get("str", plant_name.get("str_sp", ""))
        if plant_name:
            seed["plant_name"] = str(plant_name)

    if source.get("grow") is not None:
        seed["grow_minutes"] = parse_grow_to_minutes(source.get("grow"))

    seed["seeds"] = True if "seeds" not in source else bool(source.get("seeds"))

    if "fruit_div" in source:
        fruit_div = _int_or_zero(source.get("fruit_div"))
        seed["fruit_div"] = fruit_div if fruit_div > 0 else 1
    else:
        seed["fruit_div"] = 1

    byproducts = source.get("byproducts")
    if byproducts:
        seed["byproducts"] = (
            [str(item) for item in byproducts if item]
            if isinstance(byproducts, list)
            else [str(byproducts)]
        )

    if source.get("required_terrain_flag"):
        seed["required_terrain_flag"] = str(source.get("required_terrain_flag"))

    crop_kind = source.get("crop_kind")
    if crop_kind:
        seed["crop_kind"] = str(crop_kind)

    if source.get("fruit_regrow") is not None:
        seed["fruit_regrow_minutes"] = parse_grow_to_minutes(source.get("fruit_regrow"))
    elif source.get("fruit_regrow_minutes") is not None:
        seed["fruit_regrow_minutes"] = parse_grow_to_minutes(source.get("fruit_regrow_minutes"))

    chop_yields = source.get("chop_yields")
    if isinstance(chop_yields, list) and chop_yields:
        out_yields = []
        for entry in chop_yields:
            if not isinstance(entry, dict):
                continue
            stage = entry.get("stage")
            item_id = entry.get("item_id") or entry.get("item")
            count = _int_or_zero(entry.get("count"))
            if not stage or not item_id:
                continue
            out_yields.append(
                {
                    "stage": str(stage),
                    "item_id": str(item_id),
                    "count": count if count > 0 else 1,
                }
            )
        if out_yields:
            seed["chop_yields"] = out_yields

    return seed


def export_items_and_containers(resolved: dict[str, dict]) -> tuple[list[dict], list[dict]]:
    """정제된 아이템 리스트 + CONTAINER 정의 생성."""
    items_out = []
    containers_out = []
    for eid, entry in sorted(resolved.items()):
        materials = entry.get("material", [])
        if isinstance(materials, str):
            materials = [materials]

        flags = entry.get("flags", [])
        if isinstance(flags, str):
            flags = [flags]

        cat = entry.get("category", "other")
        if isinstance(cat, dict):
            cat = cat.get("id", "other")

        item_type = entry.get("type", "GENERIC")
        comestible_type = entry.get("comestible_type", "")
        qualities = entry.get("qualities", [])
        qual_out = []
        for q in qualities:
            if isinstance(q, list) and len(q) >= 2:
                qual_out.append({"id": q[0], "level": q[1]})
            elif isinstance(q, dict):
                qual_out.append({"id": q.get("id", ""), "level": q.get("level", 0)})

        item = {
            "id": eid,
            "name": get_item_name(entry),
            "type": item_type,
            "category": cat,
            "weight_g": parse_weight_to_g(entry.get("weight", 0)),
            "volume_ml": parse_volume_to_ml(entry.get("volume", 0)),
            "materials": materials,
            "flags": flags,
            "qualities": qual_out,
        }
        if comestible_type:
            item["comestible_type"] = comestible_type

        # CONTAINER: nested inventory 용량 정의까지 items.json root로 함께 출력
        if item_type == "CONTAINER":
            # BN: containers.json에서 contains 값으로 용량(부피)을 제공
            contains = entry.get("contains", entry.get("volume", 0))
            max_volume_ml = parse_volume_to_ml(contains)
            # BN 컨버터 스코프에서 max_weight는 근사한다(액체/일반 재료 1g/ml 가정).
            max_weight_g = float(max_volume_ml)

            item["is_container"] = True
            item["container_id"] = eid
            containers_out.append({
                "id": eid,
                "name": get_item_name(entry),
                "max_weight": max_weight_g,
                "max_volume": float(max_volume_ml),
            })

        # BOOK: 해당 책이 가리키는 skill/필요 레벨/상한 레벨 출력
        if item_type == "BOOK":
            item["book_skill"] = entry.get("skill", "")
            item["book_required_level"] = entry.get("required_level", 0)
            item["book_max_level"] = entry.get("max_level", 0)

        item.update(export_item_game_detail(entry, item_type))
        items_out.append(item)
    return items_out, containers_out


# ── Requirement LIST expansion helpers ─────────────────────────

MAX_LIST_EXPANSION_DEPTH = 6

def _scale_component_count(base_count, multiplier):
    try:
        return int(base_count) * int(multiplier)
    except Exception:
        return base_count * multiplier


def expand_component_requirement(req_id: str, count_multiplier: int, requirements: dict[str, dict], depth: int = 0) -> list[dict]:
    """requirement가 {"item": <req_id>, "list": true}로 들어오는 경우를 alternatives로 전개."""
    if depth > MAX_LIST_EXPANSION_DEPTH:
        # circular/overly deep structures: stop expanding to keep converter stable
        print(f"[WARN] LIST component expansion depth exceeded: {req_id}")
        return [{"item": req_id, "count": count_multiplier}]

    req = requirements.get(req_id)
    if not req:
        return [{"item": req_id, "count": count_multiplier}]

    comps = req.get("components", []) or []
    if not comps or not isinstance(comps, list):
        return [{"item": req_id, "count": count_multiplier}]

    # 현재 출력 스키마가 component-slot 단위 OR만 표현하므로 첫 슬롯만 사용
    first_slot = comps[0] if isinstance(comps[0], list) else []
    out: list[dict] = []
    for alt in first_slot:
        if not isinstance(alt, list) or len(alt) < 2:
            continue
        target_id = alt[0]
        base_count = alt[1]
        nested_multiplier = _scale_component_count(base_count, count_multiplier)
        if len(alt) >= 3 and alt[2] == "LIST":
            out.extend(expand_component_requirement(target_id, nested_multiplier, requirements, depth + 1))
        else:
            out.append({"item": target_id, "count": nested_multiplier})
    return out


def _scale_tool_charges(base_charges, multiplier):
    # BN conventions: -1 means "unlimited/unknown" and should not be multiplied.
    if isinstance(base_charges, (int, float)) and base_charges > 0:
        return int(base_charges) * int(multiplier)
    return base_charges


def expand_tool_requirement(req_id: str, charges_multiplier: int, requirements: dict[str, dict], depth: int = 0) -> list[dict]:
    """requirement가 tools alternatives에서 list 참조로 들어오는 경우 전개."""
    if depth > MAX_LIST_EXPANSION_DEPTH:
        print(f"[WARN] LIST tool expansion depth exceeded: {req_id}")
        return [{"tool": req_id, "charges": charges_multiplier}]

    req = requirements.get(req_id)
    if not req:
        return [{"tool": req_id, "charges": charges_multiplier}]

    tools = req.get("tools", []) or []
    if not tools or not isinstance(tools, list):
        return [{"tool": req_id, "charges": charges_multiplier}]

    # 현재 출력 스키마가 tool-slot 단위 OR만 표현하므로 첫 슬롯만 사용
    first_slot = tools[0] if isinstance(tools[0], list) else []
    out: list[dict] = []
    for alt in first_slot:
        if not isinstance(alt, list) or len(alt) < 2:
            continue
        target_id = alt[0]
        base_charges = alt[1]
        if len(alt) >= 3 and alt[2] == "LIST":
            nested_multiplier = _scale_tool_charges(base_charges, charges_multiplier)
            out.extend(expand_tool_requirement(target_id, nested_multiplier, requirements, depth + 1))
        else:
            scaled = _scale_tool_charges(base_charges, charges_multiplier)
            out.append({"tool": target_id, "charges": scaled})
    return out


# ── Phase: Requirements ─────────────────────────────────────────────

def load_requirements(bn_path: Path) -> dict[str, dict]:
    """공유 요구사항(using 참조 대상)을 id→data 맵으로 반환."""
    req_dir = bn_path / "data" / "json" / "requirements"
    print(f"[requirements] Loading from {req_dir} ...")
    raw = load_all_json(req_dir)
    reqs: dict[str, dict] = {}
    for entry in raw:
        if entry.get("type") == "requirement":
            rid = entry.get("id", "")
            if rid:
                reqs[rid] = entry
    print(f"[requirements] Loaded {len(reqs)} requirements")
    return reqs


# ── Phase: Materials & Qualities ────────────────────────────────────

def load_materials(bn_path: Path) -> list[dict]:
    mat_file = bn_path / "data" / "json" / "materials.json"
    if not mat_file.exists():
        return []
    with open(mat_file, "r", encoding="utf-8") as f:
        data = json.load(f)
    mats = []
    for entry in data:
        if entry.get("type") == "material":
            name = entry.get("name", "")
            if isinstance(name, dict):
                name = name.get("str", "")
            mats.append({
                "id": entry.get("id", ""),
                "name": name,
                "bash_resist": _int_or_zero(entry.get("bash_resist")),
                "cut_resist": _int_or_zero(entry.get("cut_resist")),
                "bullet_resist": _int_or_zero(entry.get("bullet_resist")),
                "acid_resist": _int_or_zero(entry.get("acid_resist")),
                "fire_resist": _int_or_zero(entry.get("fire_resist")),
                "chip_resist": _int_or_zero(entry.get("chip_resist")),
                "density": _float_or_zero(entry.get("density")),
            })
    print(f"[materials] Loaded {len(mats)} materials")
    return mats


def load_qualities(bn_path: Path) -> list[dict]:
    q_file = bn_path / "data" / "json" / "tool_qualities.json"
    if not q_file.exists():
        return []
    with open(q_file, "r", encoding="utf-8") as f:
        data = json.load(f)
    quals = []
    for entry in data:
        if entry.get("type") == "tool_quality":
            name = entry.get("name", "")
            if isinstance(name, dict):
                name = name.get("str", "")
            quals.append({
                "id": entry.get("id", ""),
                "name": name,
            })
    print(f"[qualities] Loaded {len(quals)} tool qualities")
    return quals


def load_skills(bn_path: Path) -> list[dict]:
    skills_file = bn_path / "data" / "json" / "skills.json"
    if not skills_file.exists():
        return []
    with open(skills_file, "r", encoding="utf-8") as f:
        data = json.load(f)

    skills_out: list[dict] = []
    if not isinstance(data, list):
        return skills_out

    for entry in data:
        if not isinstance(entry, dict):
            continue
        if entry.get("type") != "skill":
            continue
        sid = entry.get("id", "")
        name = entry.get("name", "")
        if isinstance(name, dict):
            name = name.get("str", "") or name.get("str_pl", "") or ""
        skills_out.append({"id": sid, "name": str(name)})
    print(f"[skills] Loaded {len(skills_out)} skills")
    return skills_out


# ── Phase: Terrain & furniture (farming whitelist) ─────────────────

TERRAIN_TYPE = "terrain"
FURNITURE_TYPE = "furniture"


def _entry_id(entry: dict) -> str | None:
    eid = entry.get("id") or entry.get("abstract")
    if isinstance(eid, list):
        eid = eid[0] if len(eid) > 0 and isinstance(eid[0], str) else None
    if not isinstance(eid, str) or not eid:
        return None
    return eid


def resolve_copy_from_entries(raw: list[dict], allowed_types: set[str]) -> dict[str, dict]:
    """copy-from resolve for a type set. Abstracts are merged but not emitted."""
    by_id: dict[str, dict] = {}
    abstracts: dict[str, dict] = {}

    for entry in raw:
        if not isinstance(entry, dict):
            continue
        if entry.get("type") not in allowed_types:
            continue
        eid = _entry_id(entry)
        if not eid:
            continue
        if "abstract" in entry:
            abstracts[eid] = entry
        else:
            by_id[eid] = entry

    resolved: dict[str, dict] = {}
    resolve_stack: set[str] = set()

    def resolve(eid: str) -> dict | None:
        if eid in resolved:
            return resolved[eid]
        if eid in resolve_stack:
            return None
        resolve_stack.add(eid)

        entry = by_id.get(eid) or abstracts.get(eid)
        if entry is None:
            resolve_stack.discard(eid)
            return None

        parent_id = entry.get("copy-from")
        if parent_id and parent_id != eid:
            parent = resolve(parent_id)
            if parent:
                entry = deep_merge(parent, entry)

        if eid in by_id:
            resolved[eid] = entry
        resolve_stack.discard(eid)
        return entry

    for eid in list(by_id.keys()):
        resolve(eid)
    for eid in list(abstracts.keys()):
        resolve(eid)
    return resolved


def load_furniture_and_terrain(bn_path: Path) -> tuple[dict[str, dict], dict[str, dict]]:
    src_dir = bn_path / "data" / "json" / "furniture_and_terrain"
    print(f"[terrain/furniture] Loading from {src_dir} ...")
    raw = load_all_json(src_dir) if src_dir.exists() else []
    terrain = resolve_copy_from_entries(raw, {TERRAIN_TYPE})
    furniture = resolve_copy_from_entries(raw, {FURNITURE_TYPE})
    print(f"[terrain/furniture] Resolved {len(terrain)} terrain, {len(furniture)} furniture")
    return terrain, furniture


def export_farming_flags(entry: dict) -> list[str]:
    flags = entry.get("flags", [])
    if isinstance(flags, str):
        flags = [flags]
    if not isinstance(flags, list):
        return []
    out: list[str] = []
    seen: set[str] = set()
    for flag in flags:
        if flag is None:
            continue
        key = str(flag).strip()
        if key not in FARMING_FLAGS or key in seen:
            continue
        seen.add(key)
        out.append(key)
    return out


def export_crafting_furniture_flags(entry: dict) -> list[str]:
    """Farming flags stay separate. Craft whitelist + LIGHT_* numeric light flags."""
    flags = entry.get("flags", [])
    if isinstance(flags, str):
        flags = [flags]
    if not isinstance(flags, list):
        return []
    out: list[str] = []
    seen: set[str] = set()
    for flag in flags:
        if flag is None:
            continue
        key = str(flag).strip()
        if key in seen:
            continue
        keep = key in CRAFTING_FURNITURE_FLAGS or key.startswith("LIGHT_")
        if not keep:
            continue
        # Dist cooking SSOT: FIRE_CONTAINER → FIRE
        if key == "FIRE_CONTAINER":
            key = "FIRE"
        if key in seen:
            continue
        seen.add(key)
        out.append(key)
    return out


def export_furniture_provides_qualities(entry: dict) -> list[dict]:
    qualities = entry.get("qualities")
    if not isinstance(qualities, list):
        return []
    out: list[dict] = []
    for q in qualities:
        if not isinstance(q, dict):
            continue
        qid = q.get("id") or ""
        if not qid:
            continue
        out.append({"id": str(qid), "level": _int_or_zero(q.get("level", 0))})
    return out


def export_furniture_plant_data(entry: dict) -> dict | None:
    source = entry.get("plant_data")
    if not isinstance(source, dict):
        return None
    plant: dict[str, Any] = {}
    if source.get("transform") is not None and source.get("transform") != "":
        plant["transform"] = str(source.get("transform"))
    if source.get("base") is not None and source.get("base") != "":
        plant["base"] = str(source.get("base"))
    if "growth_multiplier" in source:
        plant["growth_multiplier"] = _float_or_zero(source.get("growth_multiplier"))
    if "harvest_multiplier" in source:
        plant["harvest_multiplier"] = _float_or_zero(source.get("harvest_multiplier"))
    return plant or None


def export_terrain_entry(entry: dict, eid: str | None = None) -> dict:
    out: dict[str, Any] = {
        "id": eid or entry.get("id") or "",
        "name": get_item_name(entry),
    }
    flags = export_farming_flags(entry)
    if flags:
        out["flags"] = flags
    craft_flags = export_crafting_furniture_flags(entry)
    if craft_flags:
        out["crafting_flags"] = craft_flags
    return out


def export_furniture_entry(entry: dict, eid: str | None = None) -> dict:
    out = export_terrain_entry(entry, eid)
    plant = export_furniture_plant_data(entry)
    if plant:
        out["plant_data"] = plant
    provides = export_furniture_provides_qualities(entry)
    if provides:
        out["provides_qualities"] = provides
    pseudo = entry.get("crafting_pseudo_item")
    if pseudo:
        out["crafting_pseudo_item"] = str(pseudo)
    return out


def export_terrain_and_furniture(
    terrain: dict[str, dict],
    furniture: dict[str, dict],
) -> tuple[list[dict], list[dict]]:
    terrain_out = [export_terrain_entry(entry, eid) for eid, entry in sorted(terrain.items())]
    furniture_out = [export_furniture_entry(entry, eid) for eid, entry in sorted(furniture.items())]
    return terrain_out, furniture_out


# ── Phase: Recipes ──────────────────────────────────────────────────

def load_recipes(bn_path: Path, requirements: dict[str, dict]) -> list[dict]:
    """레시피를 로드하고, using 평탄화 + copy-from 해소 + 필터링."""
    recipes_dir = bn_path / "data" / "json" / "recipes"
    print(f"[recipes] Loading from {recipes_dir} ...")
    raw = load_all_json(recipes_dir)

    # 분류
    recipes_raw: list[dict] = []
    uncraft_raw: list[dict] = []
    recipe_by_key: dict[str, dict] = {}

    for entry in raw:
        t = entry.get("type", "")
        if t == "recipe":
            recipes_raw.append(entry)
            result = entry.get("result", "")
            suffix = entry.get("id_suffix", "")
            key = f"{result}:{suffix}" if suffix else result
            recipe_by_key[key] = entry
        elif t == "uncraft":
            uncraft_raw.append(entry)

    print(f"[recipes] Raw: {len(recipes_raw)} recipes, {len(uncraft_raw)} uncraft")

    # copy-from 해소 (레시피 간)
    resolved_recipes: list[dict] = []
    for entry in recipes_raw:
        parent_id = entry.get("copy-from")
        if parent_id and parent_id in recipe_by_key:
            parent = recipe_by_key[parent_id]
            entry = deep_merge(parent, entry)
        resolved_recipes.append(entry)

    # 필터링
    filtered: list[dict] = []
    skip_count = 0
    for entry in resolved_recipes:
        # basecamp 건설 레시피 제외
        if entry.get("construction_blueprint"):
            skip_count += 1
            continue
        cat = entry.get("category", "")
        if cat == "CC_BUILDING":
            skip_count += 1
            continue
        # obsolete 제외
        if entry.get("obsolete"):
            skip_count += 1
            continue
        # never_learn NPC 전용 제외
        if entry.get("never_learn"):
            skip_count += 1
            continue
        # result 없는 항목 제외
        if not entry.get("result"):
            skip_count += 1
            continue
        filtered.append(entry)

    print(f"[recipes] Filtered: {len(filtered)} kept, {skip_count} skipped")

    # using 평탄화 + 출력 포맷
    output: list[dict] = []
    for entry in filtered:
        output.append(flatten_recipe(entry, requirements))

    # uncraft도 별도 키로 포함
    uncraft_out: list[dict] = []
    for entry in uncraft_raw:
        if not entry.get("result"):
            continue
        uncraft_out.append(flatten_recipe(entry, requirements, is_uncraft=True))

    print(f"[recipes] Output: {len(output)} recipes, {len(uncraft_out)} uncraft")
    return output, uncraft_out


def flatten_recipe(entry: dict, requirements: dict[str, dict],
                   is_uncraft: bool = False) -> dict:
    """단일 레시피를 정제 포맷으로 변환. using 참조를 인라인 전개."""

    # components 정규화 (LIST requirement 전개 포함)
    components = normalize_components(entry.get("components", []), requirements)

    # tools 정규화 (LIST requirement 전개 포함)
    tools = normalize_tools(entry.get("tools", []), requirements)

    # qualities 정규화
    qualities = []
    for q in entry.get("qualities", []):
        if isinstance(q, dict):
            qualities.append({"id": q.get("id", ""), "level": q.get("level", 0)})

    # using 전개
    for using_ref in entry.get("using", []):
        if not isinstance(using_ref, list) or len(using_ref) < 2:
            continue
        req_id, multiplier = using_ref[0], using_ref[1]
        req = requirements.get(req_id)
        if not req:
            continue

        # requirement의 components를 multiplier 적용하여 추가
        for comp_slot in req.get("components", []):
            scaled_slot = []
            for alt in comp_slot:
                if isinstance(alt, list) and len(alt) >= 2:
                    item_id = alt[0]
                    count = alt[1] * multiplier
                    if len(alt) >= 3 and alt[2] == "LIST":
                        # LIST alt 는 requirement id 이므로 alternatives로 재귀 전개
                        scaled_slot.extend(expand_component_requirement(item_id, count, requirements))
                    else:
                        scaled_slot.append({"item": item_id, "count": count})
            if scaled_slot:
                components.append({"alternatives": scaled_slot})

        # requirement의 tools 추가
        for tool_slot in req.get("tools", []):
            scaled_slot = []
            for alt in tool_slot:
                if isinstance(alt, list) and len(alt) >= 2:
                    tool_id = alt[0]
                    charges = alt[1] * multiplier if alt[1] > 0 else alt[1]
                    if len(alt) >= 3 and alt[2] == "LIST":
                        scaled_slot.extend(expand_tool_requirement(tool_id, charges, requirements))
                    else:
                        scaled_slot.append({"tool": tool_id, "charges": charges})
            if scaled_slot:
                tools.append({"alternatives": scaled_slot})

        # requirement의 qualities 추가
        for q in req.get("qualities", []):
            if isinstance(q, dict):
                qualities.append({"id": q.get("id", ""), "level": q.get("level", 0)})

    # dedup qualities (같은 id면 높은 level만 유지)
    qual_map: dict[str, int] = {}
    for q in qualities:
        qid = q["id"]
        if qid not in qual_map or q["level"] > qual_map[qid]:
            qual_map[qid] = q["level"]
    qualities = [{"id": k, "level": v} for k, v in sorted(qual_map.items())]

    # autolearn 정규화
    autolearn = entry.get("autolearn", False)
    if isinstance(autolearn, list):
        autolearn_skills = autolearn
        autolearn = True
    else:
        autolearn_skills = []

    # book_learn 정규화
    book_learn = []
    for bl in entry.get("book_learn", []):
        if isinstance(bl, list) and len(bl) >= 2:
            book_learn.append({"book": bl[0], "level": bl[1]})

    # skills_required 정규화
    skills_req = entry.get("skills_required", [])
    skills_out = []
    if skills_req:
        if isinstance(skills_req[0], str):
            skills_out = [{"skill": skills_req[0], "level": skills_req[1] if len(skills_req) > 1 else 0}]
        elif isinstance(skills_req[0], list):
            for sr in skills_req:
                if len(sr) >= 2:
                    skills_out.append({"skill": sr[0], "level": sr[1]})

    result_id = entry.get("result", "")
    suffix = entry.get("id_suffix", "")
    recipe_id = f"{result_id}_{suffix}" if suffix else result_id

    rec = {
        "id": recipe_id,
        "result": result_id,
        "category": entry.get("category", ""),
        "subcategory": entry.get("subcategory", ""),
        "skill_used": entry.get("skill_used", ""),
        "skills_required": skills_out,
        "difficulty": entry.get("difficulty", 0),
        "time_minutes": parse_time_to_minutes(entry.get("time", 0)),
        "reversible": entry.get("reversible", False),
        "autolearn": autolearn,
        "result_count": entry.get("charges", entry.get("result_mult", 1)),
        "qualities_required": qualities,
        "tools": tools,
        "components": components,
    }

    if is_uncraft:
        rec["is_uncraft"] = True

    if book_learn:
        rec["book_learn"] = book_learn

    if autolearn_skills:
        parsed_autolearn = []
        for skill_entry in autolearn_skills:
            if isinstance(skill_entry, list) and len(skill_entry) >= 2:
                parsed_autolearn.append({"skill": skill_entry[0], "level": skill_entry[1]})
            elif isinstance(skill_entry, str):
                parsed_autolearn.append({"skill": skill_entry, "level": 0})
        if parsed_autolearn:
            rec["autolearn_skills"] = parsed_autolearn

    # decomp_learn: int → skill_used + level; array → [{skill, level}, ...]
    decomp_raw = entry.get("decomp_learn")
    if decomp_raw is not None:
        decomp_out = []
        if isinstance(decomp_raw, int):
            skill_used = entry.get("skill_used", "")
            if skill_used:
                decomp_out.append({"skill": skill_used, "level": decomp_raw})
        elif isinstance(decomp_raw, list):
            if decomp_raw and isinstance(decomp_raw[0], str):
                decomp_out.append({
                    "skill": decomp_raw[0],
                    "level": decomp_raw[1] if len(decomp_raw) > 1 else 0,
                })
            else:
                for sr in decomp_raw:
                    if isinstance(sr, list) and len(sr) >= 2:
                        decomp_out.append({"skill": sr[0], "level": sr[1]})
        if decomp_out:
            rec["decomp_learn"] = decomp_out

    proficiencies_out = []
    for prof in entry.get("proficiencies", []) or []:
        if isinstance(prof, dict):
            proficiencies_out.append({
                "proficiency": prof.get("proficiency", prof.get("id", "")),
                "required": bool(prof.get("required", False)),
                "time_multiplier": _float_or_zero(prof.get("time_multiplier", 1.0)),
            })
        elif isinstance(prof, str):
            proficiencies_out.append({
                "proficiency": prof,
                "required": False,
                "time_multiplier": 1.0,
            })
    if proficiencies_out:
        rec["proficiencies"] = proficiencies_out

    if entry.get("activity_level"):
        rec["activity_level"] = str(entry.get("activity_level"))
    if entry.get("morale_modifier") is not None:
        rec["morale_modifier"] = _int_or_zero(entry.get("morale_modifier"))
    if entry.get("hot_result"):
        rec["hot_result"] = True
    if entry.get("dehydrating"):
        rec["dehydrating"] = True

    recipe_flags = entry.get("flags", [])
    if isinstance(recipe_flags, str):
        recipe_flags = [recipe_flags]
    if isinstance(recipe_flags, list):
        kept: list[str] = []
        seen_flags: set[str] = set()
        for flag in recipe_flags:
            if flag is None:
                continue
            key = str(flag).strip()
            if key not in RECIPE_FLAG_WHITELIST or key in seen_flags:
                continue
            seen_flags.add(key)
            kept.append(key)
        if kept:
            rec["flags"] = kept

    if entry.get("byproducts"):
        rec["byproducts"] = [
            {"item": bp[0], "count": bp[1] if len(bp) > 1 else 1}
            for bp in entry["byproducts"] if isinstance(bp, list)
        ]

    return rec


def parse_component_alt(item_id: str, count, flags: list | None = None) -> dict:
    alt = {"item": item_id, "count": count}
    for flag in flags or []:
        if flag == "CONTAINER":
            alt["container"] = True
        elif flag == "FILTHY":
            alt["filthy"] = True
        elif flag == "LIQUID":
            alt["liquid"] = True
    return alt


def normalize_components(comps: list, requirements: dict[str, dict]) -> list[dict]:
    """components 배열을 JsonUtility 호환 형태로 정규화.
    각 슬롯을 {"alternatives": [...]} 오브젝트로 감싼다."""
    result = []
    for slot in comps:
        if not isinstance(slot, list):
            continue
        alternatives = []
        for alt in slot:
            if isinstance(alt, list) and len(alt) >= 2:
                item_id = alt[0]
                count = alt[1]
                extra_flags = list(alt[2:]) if len(alt) > 2 else []
                if extra_flags and extra_flags[0] == "LIST":
                    alternatives.extend(expand_component_requirement(item_id, count, requirements))
                else:
                    alternatives.append(parse_component_alt(item_id, count, extra_flags))
        if alternatives:
            result.append({"alternatives": alternatives})
    return result


def normalize_tools(tools: list, requirements: dict[str, dict]) -> list[dict]:
    """tools 배열을 JsonUtility 호환 형태로 정규화.
    각 슬롯을 {"alternatives": [...]} 오브젝트로 감싼다."""
    result = []
    for slot in tools:
        if not isinstance(slot, list):
            continue
        alternatives = []
        for alt in slot:
            if isinstance(alt, list) and len(alt) >= 2:
                tool_id = alt[0]
                charges = alt[1]
                if len(alt) >= 3 and alt[2] == "LIST":
                    alternatives.extend(expand_tool_requirement(tool_id, charges, requirements))
                else:
                    alternatives.append({"tool": tool_id, "charges": charges})
        if alternatives:
            result.append({"alternatives": alternatives})
    return result


# ── Localization (gettext → item id names) ───────────────────────────

LICENSE = "CC BY-SA 3.0 — derived from Cataclysm: Bright Nights"
SOURCE = "https://github.com/cataclysmbnteam/Cataclysm-BN"


def _po_unescape(s: str) -> str:
    return (
        s.replace(r"\\", "\0")
        .replace(r"\n", "\n")
        .replace(r"\t", "\t")
        .replace(r"\"", '"')
        .replace("\0", "\\")
    )


def parse_po_translations(po_path: Path) -> dict[str, str]:
    """msgid (singular) → msgstr / msgstr[0]. Empty msgstr skipped."""
    if not po_path.is_file():
        return {}

    text = po_path.read_text(encoding="utf-8", errors="replace")
    translations: dict[str, str] = {}
    msgid: list[str] = []
    msgstr: list[str] = []
    msgstr0: list[str] = []
    mode = None  # None | msgid | msgstr | msgstr0

    def flush():
        nonlocal msgid, msgstr, msgstr0, mode
        if not msgid:
            msgid, msgstr, msgstr0, mode = [], [], [], None
            return
        key = _po_unescape("".join(msgid))
        if key:
            val = _po_unescape("".join(msgstr0 if msgstr0 else msgstr))
            if val:
                translations[key] = val
        msgid, msgstr, msgstr0, mode = [], [], [], None

    for raw in text.splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("msgid_plural"):
            mode = None
            continue
        if line.startswith("msgid "):
            flush()
            mode = "msgid"
            msgid.append(line[6:].strip().strip('"'))
            continue
        if line.startswith("msgstr["):
            # plural forms — use [0] only
            if line.startswith("msgstr[0]"):
                mode = "msgstr0"
                msgstr0.append(line.split("]", 1)[1].strip().strip('"'))
            else:
                mode = None
            continue
        if line.startswith("msgstr "):
            mode = "msgstr"
            msgstr.append(line[7:].strip().strip('"'))
            continue
        if line.startswith('"') and mode == "msgid":
            msgid.append(line.strip('"'))
        elif line.startswith('"') and mode == "msgstr":
            msgstr.append(line.strip('"'))
        elif line.startswith('"') and mode == "msgstr0":
            msgstr0.append(line.strip('"'))

    flush()
    return translations


def apply_po_slots(
    entry: dict[str, str],
    msgid: str,
    ko_map: dict[str, str],
    ja_map: dict[str, str],
) -> None:
    if not msgid:
        return
    ko = ko_map.get(msgid)
    if ko:
        entry["ko"] = ko
    ja = ja_map.get(msgid)
    if ja:
        entry["ja"] = ja


def export_item_names(
    items_out: list[dict],
    ko_map: dict[str, str],
    ja_map: dict[str, str],
) -> dict[str, dict[str, str]]:
    names: dict[str, dict[str, str]] = {}
    for item in items_out:
        item_id = item.get("id")
        en = item.get("name") or ""
        if not item_id or not en:
            continue
        entry: dict[str, str] = {"en": en}
        apply_po_slots(entry, en, ko_map, ja_map)
        names[item_id] = entry
    return names


def export_item_descriptions(
    items_out: list[dict],
    ko_map: dict[str, str],
    ja_map: dict[str, str],
) -> dict[str, dict[str, str]]:
    descriptions: dict[str, dict[str, str]] = {}
    for item in items_out:
        item_id = item.get("id")
        en = item.get("description") or ""
        if not item_id or not en:
            continue
        entry: dict[str, str] = {"en": en}
        apply_po_slots(entry, en, ko_map, ja_map)
        descriptions[item_id] = entry
    return descriptions


def humanize_recipe_category_id(cat_id: str) -> str:
    s = cat_id or ""
    if s.startswith("CSC_"):
        s = s[4:]
    elif s.startswith("CC_"):
        s = s[3:]
    return s.replace("_", " ").title()


# Dist UI_ko leftovers → bake ko when po misses. CC_MISC is Dist id; BN uses CC_OTHER.
RECIPE_CATEGORY_KO_SEED = {
    "CC_FOOD": "음식",
    "CC_DRINK": "음료",
    "CC_CHEM": "화학",
    "CC_AMMO": "탄약",
    "CC_WEAPON": "무기",
    "CC_ARMOR": "방어구",
    "CC_ELECTRONIC": "전자",
    "CC_OTHER": "기타",
    "CSC_FOOD_MEAT": "육류",
    "CSC_FOOD_VEGGI": "채소",
    "CSC_FOOD_OTHER": "기타 음식",
}


def export_qualities(
    qualities: list[dict],
    ko_map: dict[str, str],
    ja_map: dict[str, str],
) -> dict[str, dict[str, str]]:
    """tool_quality id → en/ko/ja. msgid is English name (cutting), not CUT."""
    out: dict[str, dict[str, str]] = {}
    for entry in qualities:
        if not isinstance(entry, dict):
            continue
        quality_id = entry.get("id") or ""
        en = entry.get("name") or ""
        if not quality_id or not en:
            continue
        slot: dict[str, str] = {"en": en}
        apply_po_slots(slot, en, ko_map, ja_map)
        out[quality_id] = slot
    return out


def export_recipe_categories(
    recipes_out: list[dict],
    uncraft_out: list[dict],
    ko_map: dict[str, str],
    ja_map: dict[str, str],
) -> dict[str, dict[str, str]]:
    ids: set[str] = set()
    for rec in recipes_out + uncraft_out:
        category = rec.get("category") or ""
        subcategory = rec.get("subcategory") or ""
        if category:
            ids.add(category)
        if subcategory:
            ids.add(subcategory)

    categories: dict[str, dict[str, str]] = {}
    for cat_id in sorted(ids):
        en = humanize_recipe_category_id(cat_id)
        if not en:
            continue
        entry: dict[str, str] = {"en": en}
        apply_po_slots(entry, en, ko_map, ja_map)
        if "ko" not in entry:
            apply_po_slots(entry, cat_id, ko_map, ja_map)
        if "ko" not in entry and cat_id in RECIPE_CATEGORY_KO_SEED:
            entry["ko"] = RECIPE_CATEGORY_KO_SEED[cat_id]
        categories[cat_id] = entry
    return categories


def write_item_names_file(
    path: Path,
    names: dict[str, dict[str, str]],
    descriptions: dict[str, dict[str, str]],
    recipe_categories: dict[str, dict[str, str]],
    qualities: dict[str, dict[str, str]],
) -> None:
    payload = {
        "_license": LICENSE,
        "_source": SOURCE,
        "names": names,
        "descriptions": descriptions,
        "recipe_categories": recipe_categories,
        "qualities": qualities,
    }
    with open(path, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)
    name_ko = sum(1 for v in names.values() if "ko" in v)
    name_ja = sum(1 for v in names.values() if "ja" in v)
    desc_ko = sum(1 for v in descriptions.values() if "ko" in v)
    desc_ja = sum(1 for v in descriptions.values() if "ja" in v)
    cat_ko = sum(1 for v in recipe_categories.values() if "ko" in v)
    cat_ja = sum(1 for v in recipe_categories.values() if "ja" in v)
    qual_ko = sum(1 for v in qualities.values() if "ko" in v)
    qual_ja = sum(1 for v in qualities.values() if "ja" in v)
    print(
        f"[output] {path}  names={len(names)} (ko={name_ko}, ja={name_ja})  "
        f"descriptions={len(descriptions)} (ko={desc_ko}, ja={desc_ja})  "
        f"recipe_categories={len(recipe_categories)} (ko={cat_ko}, ja={cat_ja})  "
        f"qualities={len(qualities)} (ko={qual_ko}, ja={qual_ja})"
    )


def load_po_maps(bn_path: Path) -> tuple[dict[str, str], dict[str, str]]:
    ko_po = bn_path / "lang" / "po" / "ko.po"
    ja_po = bn_path / "lang" / "po" / "ja.po"
    ko_map = parse_po_translations(ko_po)
    ja_map = parse_po_translations(ja_po)
    if not ko_map:
        print(f"[WARN] no ko translations from {ko_po}")
    if not ja_map:
        print(f"[WARN] no ja translations from {ja_po}")
    return ko_map, ja_map


def merge_locale_into_existing(bn_path: Path, out_dir: Path) -> None:
    items_file = out_dir / "items.json"
    recipes_file = out_dir / "recipes.json"
    names_file = out_dir / "item_names.json"
    if not items_file.is_file() or not recipes_file.is_file() or not names_file.is_file():
        print("[ERROR] --locale-only needs existing items.json, recipes.json, item_names.json")
        sys.exit(1)

    with open(items_file, encoding="utf-8") as f:
        items_root = json.load(f)
    with open(recipes_file, encoding="utf-8") as f:
        recipes_root = json.load(f)
    with open(names_file, encoding="utf-8") as f:
        names_root = json.load(f)

    items_out = items_root.get("items") or []
    recipes_out = recipes_root.get("recipes") or []
    uncraft_out = recipes_root.get("uncraft") or []
    existing_names = names_root.get("names") or {}

    ko_map, ja_map = load_po_maps(bn_path)
    descriptions = export_item_descriptions(items_out, ko_map, ja_map)
    recipe_categories = export_recipe_categories(recipes_out, uncraft_out, ko_map, ja_map)
    qualities = export_qualities(items_root.get("qualities") or [], ko_map, ja_map)
    write_item_names_file(names_file, existing_names, descriptions, recipe_categories, qualities)


# ── Main ────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="BN → Project JSON converter")
    parser.add_argument("--bn-path", required=True, help="Cataclysm-BN 프로젝트 루트")
    parser.add_argument("--output", required=True, help="출력 디렉토리")
    parser.add_argument(
        "--locale-only",
        action="store_true",
        help="Keep items/recipes; rewrite item_names.json names+descriptions+recipe_categories+qualities",
    )
    parser.add_argument(
        "--mapgen",
        action="store_true",
        help="Also bake house mapgen → output/mapgen (MapSaveJsonDto). Off by default.",
    )
    args = parser.parse_args()

    bn_path = Path(args.bn_path)
    out_dir = Path(args.output)
    out_dir.mkdir(parents=True, exist_ok=True)

    if args.locale_only:
        merge_locale_into_existing(bn_path, out_dir)
        return

    if not (bn_path / "data" / "json").exists():
        print(f"[ERROR] BN data path not found: {bn_path / 'data' / 'json'}")
        sys.exit(1)

    # 1) Items
    resolved_items = load_items(bn_path)
    items_out, containers_out = export_items_and_containers(resolved_items)

    # 2) Materials & Qualities
    materials = load_materials(bn_path)
    qualities = load_qualities(bn_path)

    # 2.5) Skills
    skills_out = load_skills(bn_path)

    # 3) Requirements
    requirements = load_requirements(bn_path)

    # 4) Recipes
    recipes_out, uncraft_out = load_recipes(bn_path, requirements)

    # 4.5) Terrain / furniture (farming whitelist)
    terrain_resolved, furniture_resolved = load_furniture_and_terrain(bn_path)
    terrain_out, furniture_out = export_terrain_and_furniture(
        terrain_resolved, furniture_resolved,
    )

    # 5) Catalog locale (id → en/ko/ja); po is import-only
    ko_map, ja_map = load_po_maps(bn_path)
    item_names = export_item_names(items_out, ko_map, ja_map)
    item_descriptions = export_item_descriptions(items_out, ko_map, ja_map)
    recipe_categories = export_recipe_categories(recipes_out, uncraft_out, ko_map, ja_map)
    quality_names = export_qualities(qualities, ko_map, ja_map)

    # ── Write output ────────────────────────────────────────────────

    items_file = out_dir / "items.json"
    with open(items_file, "w", encoding="utf-8") as f:
        json.dump({
            "_license": LICENSE,
            "_source": SOURCE,
            "materials": materials,
            "qualities": qualities,
            "skills": skills_out,
            "items": items_out,
            "containers": containers_out,
        }, f, ensure_ascii=False, indent=2)
    print(f"\n[output] {items_file}  ({len(items_out)} items, {len(containers_out)} containers)")

    recipes_file = out_dir / "recipes.json"
    with open(recipes_file, "w", encoding="utf-8") as f:
        json.dump({
            "_license": LICENSE,
            "_source": SOURCE,
            "recipes": recipes_out,
            "uncraft": uncraft_out,
        }, f, ensure_ascii=False, indent=2)
    print(f"[output] {recipes_file}  ({len(recipes_out)} recipes, {len(uncraft_out)} uncraft)")

    names_file = out_dir / "item_names.json"
    write_item_names_file(names_file, item_names, item_descriptions, recipe_categories, quality_names)

    terrain_furniture_file = out_dir / "terrain_furniture.json"
    with open(terrain_furniture_file, "w", encoding="utf-8") as f:
        json.dump({
            "_license": LICENSE,
            "_source": SOURCE,
            "terrain": terrain_out,
            "furniture": furniture_out,
        }, f, ensure_ascii=False, indent=2)
    print(
        f"[output] {terrain_furniture_file}  "
        f"({len(terrain_out)} terrain, {len(furniture_out)} furniture)"
    )

    # BN Bright Nights currently has no recipe proficiency catalog; emit empty for Dist schema.
    proficiencies_out: list[dict] = []
    proficiencies_file = out_dir / "proficiencies.json"
    with open(proficiencies_file, "w", encoding="utf-8") as f:
        json.dump({
            "_license": LICENSE,
            "_source": SOURCE,
            "proficiencies": proficiencies_out,
        }, f, ensure_ascii=False, indent=2)
    print(f"[output] {proficiencies_file}  ({len(proficiencies_out)} proficiencies)")

    # 통계 요약
    categories = set()
    skills = set()
    for r in recipes_out:
        if r["category"]:
            categories.add(r["category"])
        if r["skill_used"]:
            skills.add(r["skill_used"])
    print(f"\n── Summary ──")
    print(f"  Items:      {len(items_out)}")
    print(f"  Materials:  {len(materials)}")
    print(f"  Qualities:  {len(qualities)}")
    print(f"  Recipes:    {len(recipes_out)}")
    print(f"  Uncraft:    {len(uncraft_out)}")
    print(f"  Terrain:    {len(terrain_out)}")
    print(f"  Furniture:  {len(furniture_out)}")
    print(f"  ItemNames:  {len(item_names)}")
    print(f"  ItemDescs:  {len(item_descriptions)}")
    print(f"  RecipeCats: {len(recipe_categories)}")
    print(f"  Categories: {sorted(categories)}")
    print(f"  Skills:     {sorted(skills)}")

    if args.mapgen:
        from export_mapgen import export_house_mapgen
        export_house_mapgen(bn_path, out_dir / "mapgen")


if __name__ == "__main__":
    main()
