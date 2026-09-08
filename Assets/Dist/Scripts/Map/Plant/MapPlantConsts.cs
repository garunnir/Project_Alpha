// ============================================================
// MapPlantConsts — 맵 식물 오버레이 타겟팅·농사 플래그·단계 외형 SSOT
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    public static class MapPlantConsts
    {
        public const float OverlayScale = 0.35f;
        public const float OverlayYOffset = 0.18f;
        public const float OverlayHeight = 0.4f;

        public const string DigQualityId = "DIG";
        public const int MinDigQualityLevel = 1;
        public const string AxeQualityId = "AXE";
        public const int MinAxeQualityLevel = 1;
        /// <summary>나무 OccupiedCell breakDurability 미지정 폴백.</summary>
        public const int DefaultChopDurability = 8;
        public const string FertilizerFlag = "FERTILIZER";
        public const int NoFruitHarvestMinute = -1;
        public const string GreenhouseFlag = "GREENHOUSE";

        /// <summary>
        /// Dist.Map clock bridge fallback. Matches <c>WorldClockSettings.DefaultMinutesPerDay</c>
        /// when DistScript has not bound <see cref="MapClockSnapshot.GetMinutesPerDay"/>.
        /// </summary>
        public const int FallbackMinutesPerDay = 24 * 60;

        public const string OverlayShaderUrpUnlit = "Universal Render Pipeline/Unlit";
        public const string OverlayShaderUnlitColor = "Unlit/Color";

        public static readonly Vector3 OverlayScaleSeed = new Vector3(0.16f, 0.12f, 0.16f);
        public static readonly Vector3 OverlayScaleSeedling = new Vector3(0.22f, 0.32f, 0.22f);
        public static readonly Vector3 OverlayScaleMature = new Vector3(0.28f, 0.52f, 0.28f);
        public static readonly Vector3 OverlayScaleHarvestable = new Vector3(0.34f, 0.68f, 0.34f);
        public static readonly Vector3 OverlayScaleWithered = new Vector3(0.26f, 0.18f, 0.26f);

        public static readonly Color OverlayColorSeed = new Color(0.45f, 0.32f, 0.12f, 1f);
        public static readonly Color OverlayColorSeedling = new Color(0.42f, 0.72f, 0.28f, 1f);
        public static readonly Color OverlayColorMature = new Color(0.22f, 0.55f, 0.18f, 1f);
        public static readonly Color OverlayColorHarvestable = new Color(0.72f, 0.78f, 0.18f, 1f);
        public static readonly Color OverlayColorWithered = new Color(0.38f, 0.30f, 0.22f, 1f);

        /// <summary>BN 32px@100ppu 기준 월드 스케일 (Catalog/BN 스프라이트 경로).</summary>
        public const float SpriteWorldScaleSeed = 1.6f;
        public const float SpriteWorldScaleSeedling = 2.2f;
        public const float SpriteWorldScaleMature = 2.8f;
        public const float SpriteWorldScaleHarvestable = 3.2f;
        public const float SpriteWorldScaleWithered = 2.4f;

        public const float OverlayColliderHeight = 0.5f;

        /// <summary>SSOT: <see cref="ConstructionConsts"/> (Farm·건설 셀 타겟 프리뷰 공유).</summary>
        public const float TargetPreviewAlpha = ConstructionConsts.TargetPreviewAlpha;
        public static readonly Color TargetPreviewValid = ConstructionConsts.TargetPreviewValid;
        public static readonly Color TargetPreviewInvalid = ConstructionConsts.TargetPreviewInvalid;

        public const string MeshVisualChildName = ConstructionConsts.MeshVisualChildName;
        public const string SpriteVisualChildName = ConstructionConsts.SpriteVisualChildName;
        public const string TargetPreviewResourcesName = ConstructionConsts.TargetPreviewResourcesName;

        /// <summary>심기 Work/적용 가능 XZ Chebyshev 셀 반경 (목표 셀 포함).</summary>
        public const int PlantActionRangeCells = 1;

        /// <summary>비-Plant 농사 Arrive 월드 stopping = CellSize × 이 배율.</summary>
        public const float CellArriveStoppingCellFraction = 0.55f;

        /// <summary>심기 Work 대기초 (Catalog/_clips null 폴백).</summary>
        public const float PlantWorkDurationSeconds = 1f;

        /// <summary>경작 Work 대기초 (Catalog/_clips null 폴백).</summary>
        public const float TillWorkDurationSeconds = 2f;

        /// <summary>벌목 Work 대기초 (Catalog/_clips null 폴백).</summary>
        public const float ChopWorkDurationSeconds = 2.5f;
    }
}
