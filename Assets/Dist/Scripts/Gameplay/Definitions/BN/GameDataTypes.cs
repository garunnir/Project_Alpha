// ============================================================
// GameDataTypes — JSON 데이터 역직렬화용 POCO (아이템·레시피·컨테이너·지형·가구)
// ============================================================

using System;
using System.Collections.Generic;

namespace Garunnir.Runtime.Gameplay.Data
{
    // ── Root wrappers (Newtonsoft JSON) ─────────────────────────

    [Serializable]
    public sealed class ItemsFileRoot
    {
        public string _license;
        public string _source;
        public List<MaterialData> materials;
        public List<QualityData> qualities;
        public List<SkillData> skills;
        public List<ItemData> items;
        public List<ContainerData> containers;
    }

    [Serializable]
    public sealed class RecipesFileRoot
    {
        public string _license;
        public string _source;
        public List<RecipeData> recipes;
        public List<RecipeData> uncraft;
    }

    [Serializable]
    public sealed class TerrainFurnitureFileRoot
    {
        public string _license;
        public string _source;
        public List<TerrainData> terrain;
        public List<FurnitureData> furniture;
    }

    [Serializable]
    public sealed class TerrainData
    {
        public string id;
        public string name;
        /// <summary>Farming-flag whitelist only (PLANTABLE, PLOWABLE, DIGGABLE, MINEABLE, …).</summary>
        public List<string> flags;
        /// <summary>Cooking environment whitelist (FIRE, LIT, LIGHT_*, …).</summary>
        public List<string> crafting_flags;
    }

    [Serializable]
    public sealed class FurniturePlantData
    {
        public string transform;
        public string @base;
        public float growth_multiplier = 1f;
        public float harvest_multiplier = 1f;
    }

    [Serializable]
    public sealed class FurnitureData
    {
        public string id;
        public string name;
        /// <summary>Farming-flag whitelist only (PLANT, GROWTH_*, PLANTABLE, DIGGABLE, MINEABLE, …).</summary>
        public List<string> flags;
        /// <summary>Cooking environment whitelist (FIRE, LIT, SMOKE, LIGHT_*, …).</summary>
        public List<string> crafting_flags;
        /// <summary>BN crafting_pseudo_item — adjacent furniture supplies this tool id.</summary>
        public string crafting_pseudo_item;
        public List<QualityEntry> provides_qualities;
        public FurniturePlantData plant_data;
    }

    [Serializable]
    public sealed class ProficienciesFileRoot
    {
        public string _license;
        public string _source;
        public List<ProficiencyData> proficiencies;
    }

    [Serializable]
    public sealed class ProficiencyData
    {
        public string id;
        public string name;
    }

    // ── Item ───────────────────────────────────────────────────

    [Serializable]
    public sealed class ItemData
    {
        public string id;
        public string name;
        public string type;
        public string category;
        public int weight_g;
        public int volume_ml;
        public int max_stack = 1;
        public bool is_container;
        public string container_id;
        public List<string> materials;
        public List<string> flags;
        public List<QualityEntry> qualities;
        public string comestible_type;

        // BOOK gating data (required_level/max_level are used for recipe knowledge)
        public string book_skill;
        public int book_required_level;
        public int book_max_level;

        // ── Game detail (BN bake, 통합 ItemData) ─────────────────
        public string description;
        public string subcategory;
        public bool has_durability;
        public string repairs_like;
        public int repair_difficulty;
        public int bashing;
        public int cutting;
        public int to_hit;
        public List<string> weapon_category;
        public List<string> techniques;
        public ArmorDetailData armor;
        public GunDetailData gun;
        public ToolDetailData tool;
        public ComestibleDetailData comestible;
        /// <summary>Consume-only heal / consume_drug / antibiotic / multicooker. Other BN actions stay Parked.</summary>
        public UseActionData use_action;
        public AmmoDetailData ammo;
        public MagazineDetailData magazine;
        public BookDetailData book;
        public ContainerDetailData container_detail;
        /// <summary>BN seed_data whitelist. Null if the item is not a seed.</summary>
        public SeedDetailData seed;

        public float Weight => weight_g / 1000f;
        public float Volume => volume_ml / 1000f;

        [Obsolete("Max stack is no longer enforced at runtime.")]
        public int MaxStack => max_stack > 0 ? max_stack : 1;
    }

    [Serializable]
    public sealed class MaterialData
    {
        public string id;
        public string name;
        public int bash_resist;
        public int cut_resist;
        public int bullet_resist;
        public int acid_resist;
        public int fire_resist;
        public int chip_resist;
        public float density;
    }

    [Serializable]
    public sealed class QualityData
    {
        public string id;
        public string name;
    }

    [Serializable]
    public sealed class SkillData
    {
        public string id;
        public string name;
    }

    [Serializable]
    public sealed class QualityEntry
    {
        public string id;
        public int level;
    }

    [Serializable]
    public sealed class ContainerData
    {
        public string id;
        public string name;
        public float max_weight;
        public float max_volume;
        /// <summary>소스 컨테이너 인출 moves. &gt;0이면 CombatMath.MovesPerSecond로 초 변환 후 handling에 가산.</summary>
        public int draw_moves;

        public float MaxWeight => max_weight;
        public float MaxVolume => max_volume;
    }

    // ── Recipe ─────────────────────────────────────────────────

    [Serializable]
    public sealed class RecipeData
    {
        public string id;
        public string result;
        public string category;
        public string subcategory;
        public string skill_used;
        public List<SkillReq> skills_required;
        public int difficulty;
        public float time_minutes;
        public bool reversible;
        public bool autolearn;
        public List<SkillReq> autolearn_skills;
        /// <summary>BN decomp_learn — skill levels required to learn by disassembly.</summary>
        public List<SkillReq> decomp_learn;
        public int result_count;
        public List<QualityEntry> qualities_required;
        public List<ToolSlot> tools;
        public List<ComponentSlot> components;
        public bool is_uncraft;
        public List<BookLearn> book_learn;
        public List<Byproduct> byproducts;
        public List<ProficiencyReq> proficiencies;
        public string activity_level;
        public int morale_modifier;
        public bool hot_result;
        public bool dehydrating;
        /// <summary>Cooking/light whitelist (DARK, BLIND_*).</summary>
        public List<string> flags;
    }

    [Serializable]
    public sealed class SkillReq
    {
        public string skill;
        public int level;
    }

    [Serializable]
    public sealed class ComponentSlot
    {
        public List<ComponentAlt> alternatives;
    }

    [Serializable]
    public sealed class ComponentAlt
    {
        public string item;
        public int count;
        public bool list;
        public bool container;
        public bool filthy;
        public bool liquid;
    }

    [Serializable]
    public sealed class ToolSlot
    {
        public List<ToolAlt> alternatives;
    }

    [Serializable]
    public sealed class ToolAlt
    {
        public string tool;
        public int charges;
        public bool list;
    }

    [Serializable]
    public sealed class BookLearn
    {
        public string book;
        public int level;
    }

    [Serializable]
    public sealed class Byproduct
    {
        public string item;
        public int count;
    }
}
