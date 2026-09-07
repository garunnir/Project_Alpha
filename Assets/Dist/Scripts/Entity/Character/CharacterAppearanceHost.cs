// ============================================================
// CharacterAppearanceHost — Definition에서 복사한 외형·체형 런타임 저장
// ============================================================

using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterAppearanceHost : MonoBehaviour
{
    [SerializeField] string _id;
    [SerializeField] string _displayNameOverride;
    [SerializeField] Sprite _portraitSprite;
    [SerializeField] Vector2 _alignment;
    [SerializeField] float _bodyMassKg;
    [SerializeField] float _bustCm;
    [SerializeField] float _waistCm;
    [SerializeField] float _hipCm;
    [SerializeField] List<CharacterPartMassEntry> _partMasses = new();

    public string Id => _id;
    public string DisplayNameOverride => _displayNameOverride;
    public Sprite PortraitSprite => _portraitSprite;
    public Vector2 Alignment => _alignment;
    public float BodyMassKg => _bodyMassKg;
    public float BustCm => _bustCm;
    public float WaistCm => _waistCm;
    public float HipCm => _hipCm;
    public IReadOnlyList<CharacterPartMassEntry> PartMasses => _partMasses;

    CharacterBodyHost _bodyHost;
    float _remainingMassKg;

    /// <summary>절단 후 남은 체중(kg). partMasses에서 없는 부위를 뺀 값. 과적은 미연동.</summary>
    public float RemainingMassKg
    {
        get
        {
            RecalcRemainingMass();
            return _remainingMassKg;
        }
    }

    public string ResolveDisplayName()
    {
        if (!string.IsNullOrEmpty(_displayNameOverride))
            return _displayNameOverride;

        if (string.IsNullOrEmpty(_id))
            return string.Empty;

        return Loc.Get(_id);
    }

    void Awake() =>
        _bodyHost = CharacterBodyResolve.GetInBody<CharacterBodyHost>(this);

    void RecalcRemainingMass()
    {
        if (_bodyHost == null)
            _bodyHost = CharacterBodyResolve.GetInBody<CharacterBodyHost>(this);

        float remaining = _bodyMassKg;
        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        if (body != null)
        {
            for (int i = 0; i < _partMasses.Count; i++)
            {
                CharacterPartMassEntry entry = _partMasses[i];
                if (string.IsNullOrEmpty(entry.partId) || body.Has(entry.partId))
                    continue;
                remaining -= entry.kg;
            }
        }

        _remainingMassKg = remaining < 0f ? 0f : remaining;
    }

    public void ApplyFromDefinition(CharacterDefinition definition)
    {
        if (definition == null)
            return;

        _id = definition.Id;
        _displayNameOverride = definition.DisplayNameOverride;
        _portraitSprite = definition.PortraitSprite;
        _alignment = definition.Alignment;
        _bodyMassKg = definition.BodyMassKg;
        _bustCm = definition.BustCm;
        _waistCm = definition.WaistCm;
        _hipCm = definition.HipCm;

        _partMasses.Clear();
        IReadOnlyList<CharacterPartMassEntry> source = definition.PartMasses;
        for (int i = 0; i < source.Count; i++)
            _partMasses.Add(source[i]);
    }
}
