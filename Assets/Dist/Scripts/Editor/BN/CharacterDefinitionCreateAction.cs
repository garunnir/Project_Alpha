// ============================================================
// CharacterDefinitionCreateAction — Characters/+ Create (Odin)
// ============================================================

using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

[HideReferenceObjectPicker]
public sealed class CharacterDefinitionCreateAction
{
    const string DraftKey = "CharacterDefinition";

    bool AlwaysShow => true;

    [Title("Create Character Definition", "Characters / Definitions", TitleAlignments.Split)]
    [GUIColor(0.55f, 1f, 0.65f)]
    [InfoBox(
        "에셋은 Assets/Dist/SOData/Gameplay/Character/ 에 생성됩니다.\n저장은 Unity Ctrl+S.",
        SdfIconType.PersonPlusFill,
        nameof(AlwaysShow))]
    [ShowInInspector, HideLabel, DisplayAsString(EnableRichText = true)]
    [PropertyOrder(-5)]
    string Badge =>
        "<color=#77ff99><b>NEW</b></color>  ·  CharacterDefinition SO";

    [LabelText("Base name")]
    [PropertyOrder(-1)]
    [OnValueChanged(nameof(SaveDraft))]
    public string BaseName;

    [OnInspectorInit]
    void InitBaseName()
    {
        BaseName = DataDefinitionsCreateNameDraft.GetOrRecommend(
            DraftKey,
            DataDefinitionsSoCatalogs.RecommendCharacterDefinitionBaseName);
    }

    [Button(SdfIconType.PlusCircleFill, "Create Character Definition")]
    [GUIColor(0.4f, 0.95f, 0.55f)]
    [PropertyOrder(0)]
    void Create()
    {
        DataDefinitionsSoCatalogs.FinishCreate(
            DataDefinitionsSoCatalogs.CreateCharacterDefinition(BaseName));
    }

    void SaveDraft() => DataDefinitionsCreateNameDraft.Set(DraftKey, BaseName);
}
