// ============================================================
// CharacterBodyHost — 엔티티별 ICharacterBody 소유 (plain module)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public sealed class CharacterBodyHost
{
    const int DefaultSeedStrength = 8;

    CharacterBodyRefs _refs;
    bool _useGameplayDataBody;
    CombatHitStopSettings _hitStopSettings;
    ICharacterBody _body;
    CharacterHitStopState _hitStop;
    bool _enabled;

    public CharacterBodyRefs BodyRefs => _refs;
    public GameObject gameObject => _refs != null ? _refs.gameObject : null;
    public Transform transform => _refs != null ? _refs.transform : null;
    public bool isActiveAndEnabled => _enabled && _refs != null && _refs.isActiveAndEnabled;
    public string name => _refs != null ? _refs.name : string.Empty;

    public ICharacterBody Body
    {
        get
        {
            if (_body == null)
                EnsureBody();
            return _body;
        }
    }

    public CharacterHitStopState HitStop
    {
        get
        {
            EnsureHitStop();
            return _hitStop;
        }
    }

    public bool UseGameplayDataBody => _useGameplayDataBody;

    public void ConfigureUseGameplayDataBody(bool useGameplayDataBody)
    {
        _useGameplayDataBody = useGameplayDataBody;
    }

    public void ConfigureHitStopSettings(CombatHitStopSettings settings)
    {
        _hitStopSettings = settings;
        if (_hitStop != null)
            _hitStop.SetSettings(settings);
    }

    public static int ActiveCount => CharacterBodyRefs.ActiveCount;

    public static CharacterBodyHost GetActive(int index)
    {
        CharacterBodyRefs refs = CharacterBodyRefs.GetActive(index);
        return refs != null ? refs.BodyHost : null;
    }

    public void Bind(CharacterBodyRefs refs)
    {
        _refs = refs;
        if (refs != null && refs.HitStopSettings != null)
            _hitStopSettings = refs.HitStopSettings;
        if (_enabled)
            Enable();
    }

    public void Enable()
    {
        _enabled = true;
        EnsureBody();
        EnsureHitStop();
        HitStop?.Bind();
    }

    public void Disable()
    {
        _enabled = false;
        _hitStop?.Unbind();
    }

    public void Tick()
    {
        if (_hitStop == null)
            return;
        _hitStop.Tick(TimeScaleService.Delta(TimeScaleChannel.Realtime));
    }

    void EnsureHitStop()
    {
        if (_hitStop != null)
            return;

        if (_hitStopSettings == null && _refs != null)
            _hitStopSettings = _refs.HitStopSettings;

#if UNITY_EDITOR
        if (_hitStopSettings == null)
        {
            _hitStopSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatHitStopSettings>(
                CharacterHitStopState.DefaultSettingsPath);
        }
#endif
        _hitStop = new CharacterHitStopState(this, _hitStopSettings);
    }

    void EnsureBody()
    {
        if (_body != null)
            return;

        if (_useGameplayDataBody)
        {
            _body = GameplayData.Body;
            return;
        }

        _body = CharacterBody.CreateHumanDefault(DefaultSeedStrength, prototypeSeed: false);
    }

    public void BindBody(ICharacterBody body)
    {
        _body = body;
    }

    public void ApplyBodyDto(CharacterBodyDto dto)
    {
        Body.FromDto(dto);
    }

    public int GetInstanceID() => _refs != null ? _refs.GetInstanceID() : 0;
}
