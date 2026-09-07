"""BN seed_data + terrain/furniture farming whitelist. Run: python test_seed_data.py"""

from convert import (
    FARMING_FLAGS,
    MINUTES_PER_DAY,
    export_furniture_entry,
    export_item_game_detail,
    export_seed_detail,
    export_terrain_entry,
    parse_grow_to_minutes,
    parse_time_to_minutes,
    resolve_copy_from_entries,
)


def _eq(actual, expected, label):
    if actual != expected:
        raise AssertionError(f"{label}: {actual!r} != {expected!r}")


def test_grow_string_to_minutes():
    _eq(parse_grow_to_minutes("14 days"), 14 * MINUTES_PER_DAY, "14 days")
    _eq(parse_grow_to_minutes("1 d"), MINUTES_PER_DAY, "1 d")
    seed = export_seed_detail(
        {
            "seed_data": {
                "fruit": "hops",
                "plant_name": "hops",
                "grow": "19 days",
            }
        }
    )
    _eq(seed["grow_minutes"], 19 * MINUTES_PER_DAY, "seed string grow")


def test_grow_int_is_season_days_not_moves():
    _eq(parse_time_to_minutes(91), 0.91, "moves/100 still for recipe time")
    _eq(parse_grow_to_minutes(91), 91 * MINUTES_PER_DAY, "int grow days")
    _eq(parse_grow_to_minutes(91) == parse_time_to_minutes(91), False, "not moves")
    seed = export_seed_detail(
        {
            "seed_data": {
                "fruit": "wheat",
                "plant_name": "wheat",
                "grow": 91,
            }
        }
    )
    _eq(seed["grow_minutes"], 91 * MINUTES_PER_DAY, "seed int grow")


def test_seed_whitelist_fields():
    seed = export_seed_detail(
        {
            "seed_data": {
                "fruit": "barley",
                "plant_name": "barley",
                "grow": "11 days",
                "seeds": False,
                "fruit_div": 2,
                "byproducts": ["straw_pile"],
                "required_terrain_flag": "PLOWABLE",
                "extra_ignored": True,
            }
        }
    )
    _eq(seed["fruit"], "barley", "fruit")
    _eq(seed["plant_name"], "barley", "plant_name")
    _eq(seed["grow_minutes"], 11 * MINUTES_PER_DAY, "grow_minutes")
    _eq(seed["seeds"], False, "seeds")
    _eq(seed["fruit_div"], 2, "fruit_div")
    _eq(seed["byproducts"], ["straw_pile"], "byproducts")
    _eq(seed["required_terrain_flag"], "PLOWABLE", "required_terrain_flag")
    _eq("extra_ignored" in seed, False, "no extra")
    _eq("seed_data" in seed, False, "no seed_data key")


def test_brewable_milling_fuel_not_leaked():
    detail = export_item_game_detail(
        {
            "type": "COMESTIBLE",
            "seed_data": {
                "fruit": "barley",
                "plant_name": "barley",
                "grow": "11 days",
                "byproducts": ["straw_pile"],
            },
            "brewable": {"time": 3600, "result": "beer"},
            "milling": {"into": "flour"},
            "fuel": {"energy": 1},
        },
        "COMESTIBLE",
    )
    _eq("seed" in detail, True, "seed present")
    _eq(detail["seed"]["fruit"], "barley", "seed fruit")
    for key in ("brewable", "milling", "fuel", "seed_data"):
        _eq(key in detail, False, key)


def test_terrain_furniture_farming_whitelist():
    terrain = export_terrain_entry(
        {
            "type": "terrain",
            "id": "t_dirtmound",
            "name": "mound of dirt",
            "symbol": "#",
            "color": "brown",
            "looks_like": "t_dirt",
            "flags": ["TRANSPARENT", "DIGGABLE", "MOUNTABLE", "NOCOLLIDE", "PLANTABLE"],
            "bash": {"str_min": 50, "str_max": 100},
        },
        "t_dirtmound",
    )
    _eq(terrain["id"], "t_dirtmound", "terrain id")
    _eq(terrain["name"], "mound of dirt", "terrain name")
    _eq(terrain["flags"], ["DIGGABLE", "PLANTABLE"], "terrain farming flags")
    for key in ("symbol", "color", "looks_like", "bash", "plant_data"):
        _eq(key in terrain, False, f"terrain {key}")

    mineable = export_terrain_entry(
        {
            "type": "terrain",
            "id": "t_rock_wall",
            "name": "stone wall",
            "flags": ["NOITEM", "SUPPORTS_ROOF", "WALL", "MINEABLE", "BLOCK_WIND"],
            "bash": {"str_min": 80, "str_max": 200},
        },
        "t_rock_wall",
    )
    _eq(mineable["flags"], ["MINEABLE"], "mineable terrain flag")
    _eq("bash" in mineable, False, "mineable terrain bash dropped")

    furniture = export_furniture_entry(
        {
            "type": "furniture",
            "id": "f_plant_harvest",
            "name": "harvestable plant",
            "symbol": "#",
            "color": "light_green",
            "looks_like": "f_plant_mature",
            "flags": [
                "PLANT", "SEALED", "TRANSPARENT", "CONTAINER", "NOITEM",
                "TINY", "DONT_REMOVE_ROTTEN", "GROWTH_HARVEST",
            ],
            "bash": {"str_min": 4, "str_max": 10},
            "plant_data": {
                "transform": "f_null",
                "base": "f_null",
                "growth_multiplier": 1.25,
                "harvest_multiplier": 2.5,
            },
        },
        "f_plant_harvest",
    )
    _eq(furniture["id"], "f_plant_harvest", "furn id")
    _eq(furniture["flags"], ["PLANT", "GROWTH_HARVEST"], "furn farming flags")
    _eq(
        furniture["plant_data"],
        {
            "transform": "f_null",
            "base": "f_null",
            "growth_multiplier": 1.25,
            "harvest_multiplier": 2.5,
        },
        "plant_data",
    )
    for key in ("symbol", "color", "looks_like", "bash"):
        _eq(key in furniture, False, f"furn {key}")
    for flag in furniture["flags"]:
        _eq(flag in FARMING_FLAGS, True, f"flag {flag}")


def test_furniture_copy_from_keeps_farming_drops_view():
    raw = [
        {
            "type": "furniture",
            "abstract": "f_plant_base",
            "name": "plant base",
            "symbol": "^",
            "color": "green",
            "looks_like": "f_null",
            "flags": ["PLANT", "TRANSPARENT", "GROWTH_SEED"],
            "bash": {"str_min": 1, "str_max": 5},
        },
        {
            "type": "furniture",
            "id": "f_plant_seed",
            "copy-from": "f_plant_base",
            "name": "seed",
            "plant_data": {"transform": "f_plant_seedling", "base": "f_null"},
        },
    ]
    resolved = resolve_copy_from_entries(raw, {"furniture"})
    out = export_furniture_entry(resolved["f_plant_seed"], "f_plant_seed")
    _eq(out["flags"], ["PLANT", "GROWTH_SEED"], "inherited farming flags")
    _eq(out["plant_data"]["transform"], "f_plant_seedling", "transform")
    _eq(out["plant_data"]["base"], "f_null", "base")
    for key in ("symbol", "color", "looks_like", "bash"):
        _eq(key in out, False, key)
    _eq("f_plant_base" in resolved, False, "abstract skipped")


if __name__ == "__main__":
    tests = [
        test_grow_string_to_minutes,
        test_grow_int_is_season_days_not_moves,
        test_seed_whitelist_fields,
        test_brewable_milling_fuel_not_leaked,
        test_terrain_furniture_farming_whitelist,
        test_furniture_copy_from_keeps_farming_drops_view,
    ]
    for fn in tests:
        fn()
        print(f"ok {fn.__name__}")
    print(f"{len(tests)} passed")
