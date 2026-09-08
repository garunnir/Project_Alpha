# Item catalog locale (names, descriptions, recipe categories, qualities)

Canonical for BN/custom **catalog display strings** and how they tie to Data Definitions + fonts.

## Decision

- **Data Definitions** (`Tools/Data Definitions`) is the hub for serialized editable game data. Prefer editing here (or entering from here); do not scatter parallel edit UIs.
- **Catalog strings are locale-only.** Consumers go through `UITextPresenter` / `ItemNameTable` with `ItemLocaleKind` (`Name`, `Description`, `RecipeCategory`, `Quality`). Do **not** use `ItemData.name` / `ItemData.description` / `QualityData.name` for display, edit, or fallback.
- Lookup key is **id** plus kind discriminator. Slots **`en` / `ko` / `ja`** in one file.
- Editing **Name** or **Description** in Definitions writes `GameData/item_names.json` for the **active** language. It does **not** change `items.json` `name` / `description`.
- If Definitions is the better place to edit a setting, **do not add a separate SO** just for that UX. Exception: one `LocalizationBundle` SO holds TMP font asset references and active language. Language/font change is **rare** — edit the bundle in Inspector (Definitions toolbar `Loc Bundle` pings it). Do **not** put language/font fields inline on the Definitions hub.
- **Fonts + language are one bundle** (`LocalizationBundle`). `DistUiFont` reads the active language’s font from that bundle.
- Cataclysm-BN `lang/po/*.po` is **convert import only**. Runtime never looks up by English msgid.
- Converter field whitelist / not-baked catalog: [`docs/equipment/BN_BAKE.md`](../equipment/BN_BAKE.md).

## Data

| Path | Role |
|------|------|
| `Assets/StreamingAssets/BNData/item_names.json` | BN bake: `names` / `descriptions` / `recipe_categories` / `qualities` → `{ en, ko?, ja? }` |
| `Assets/StreamingAssets/GameData/item_names.json` | Project overlay (Definitions Name/Description edits + custom seeds) |
| `Assets/Dist/Resources/Localization/LocalizationBundle.asset` | Active language + per-lang TMP fonts |

Regenerate BN catalog locale (full bake writes items/recipes too):

```text
python Tools/bn_converter/convert.py --bn-path <Cataclysm-BN> --output Assets/StreamingAssets/BNData
```

Locale sections only (keep existing `items.json` / `recipes.json`):

```text
python Tools/bn_converter/convert.py --bn-path <Cataclysm-BN> --output Assets/StreamingAssets/BNData --locale-only
```

## Lookup

Same miss rule for every kind:

1. Optional Dist force (names only): `Loc` key `Item.{id}` (`UI_ko`)
2. `item_names[kind][id][activeLang]` (GameData overlay wins over BNData)
3. If active ≠ `en` and miss → `en`
4. Else `[Missing: names|descriptions|recipe_categories|qualities.{id}]` (UI + console once; JSON 슬롯)

Default active language: **`ko`**.

`recipe_categories` holds both `CC_*` and `CSC_*`. UI chrome (`Crafting.All`, `Crafting.Favourites`) stays `Loc`. Tool quality display names (`CUT` → cutting) live in `qualities`; crafting cards use `UITextPresenter.GetQuality`.

## Caution

- Do not display via `ItemData.name` / `stack.Item.name` / `item.description` / `QualityData.name`.
- Do not mass-edit `item_names.json` by hand when Definitions/convert can do it.
- Do not use English msgid as runtime key; do not copy `ko.po`/`ja.po` into StreamingAssets.
- Do not merge BN catalog strings into `UI_ko.asset`.
- Do not use Liberation Sans or unset TMP fonts; set fonts on `LocalizationBundle` (ko/en default Galmuri7). JA unset falls back to ko with a warning (possible tofu).
- Do not keep a second SSOT for language (EditorPrefs-only, etc.) parallel to the bundle.
- Do not put Korean (or other) display strings into custom `items.json` `name` / `description` — use locale slots.

## Related

- Hub window: `Assets/Dist/Scripts/Editor/BN/DataDefinitionsWindow.cs` (`OdinMenuEditorWindow`). Menu tree:
  - **Catalog/** — Items Reference·Custom, Recipes Reference·Custom, Item Names (locale). Save Changes / Loc Bundle toolbar는 Catalog 선택 시에만.
  - **Characters/** — `+ Create Definition` · `+ Create Faction` · Definitions · Factions · Faction/Emote Catalog
  - **World/** — Clock · Weather · Needs · Mood (기존 에셋만; Ensure 없음)
  - **Combat/** — `+ Create Presentation` · `+ Create Attack` · Catalog · Presentations · Attacks · Fallbacks
  - **Locomotion/** — MovementStyle (읽기 전용; `+ Create` 없음)
  - **Map/** — `+ Create Tile` · Tiles · Prefab DB · Farming · Fishing
- Editor MenuItem/Ensure/Patch 신규 추가: `.cursor/rules/editor-automation-hygiene.mdc`
- Characters are Dist SO under `SOData/Gameplay/Character/`; Tiles are Dist `TileDefinition` under `SOData/Tile/`. Not BN JSON.
- Runtime: `UITextPresenter`, `ItemNameTable`, `LocalizationBundle`, `DistUiFont`
- UI chrome keys remain `UI_ko` / `Loc` (separate from catalog locale)
