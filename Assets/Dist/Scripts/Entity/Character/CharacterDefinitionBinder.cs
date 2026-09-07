// ============================================================
// CharacterDefinitionBinder — CharacterDefinition → 호스트/GameplayData Apply
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

[DefaultExecutionOrder(-80)]
[DisallowMultipleComponent]
public sealed class CharacterDefinitionBinder : MonoBehaviour
{
    [SerializeField] CharacterDefinition _definition;

    CharacterBodyHost _bodyHost;
    CharacterSkillsHost _skillsHost;
    CharacterTraitsHost _traitsHost;
    CharacterAppearanceHost _appearanceHost;
    CharacterFactionHost _factionHost;
    CharacterFootprintHost _footprintHost;
    CharacterState _characterState;
    CharacterVision _vision;
    CharacterHearing _hearing;
    CharacterMotor _motor;

    public CharacterDefinition Definition => _definition;

    void Awake()
    {
        if (_definition == null)
            return;

        Apply(_definition);
    }

    public void Apply(CharacterDefinition definition)
    {
        if (definition == null)
            return;

        _definition = definition;
        EnsureHosts();
        if (_appearanceHost != null)
            _appearanceHost.ApplyFromDefinition(definition);
        if (_factionHost != null)
            _factionHost.ApplyFromDefinition(definition);
        if (_vision != null)
            _vision.ApplyFromDefinition(definition);
        else
        {
            Debug.LogError(
                $"[CharacterDefinitionBinder] '{name}' needs CharacterVision on the prefab.",
                this);
        }

        if (_hearing != null)
            _hearing.ApplyFromDefinition(definition);
        else
        {
            Debug.LogError(
                $"[CharacterDefinitionBinder] '{name}' needs CharacterHearing on the prefab.",
                this);
        }

        if (_motor != null)
            _motor.ApplyWalkSpeedFromDefinition(definition);
        else
        {
            Debug.LogError(
                $"[CharacterDefinitionBinder] '{name}' needs CharacterMotor on the prefab.",
                this);
        }

        if (_footprintHost != null)
            _footprintHost.ApplyFromDefinition(definition);
        else
        {
            Debug.LogError(
                $"[CharacterDefinitionBinder] '{name}' needs CharacterFootprintHost on the prefab.",
                this);
        }

        _characterState?.BindFootprint(_footprintHost);

        DefaultCharacterSkills skills = definition.CreateSkills();
        CharacterBody body = definition.CreateBody();
        DefaultCharacterTraits traits = definition.CreateTraits();

        if (UsesGameplayData())
        {
            GameplayData.Stats = new DefaultPlayerStats(skills);
            GameplayData.Body = body;
            GameplayData.RecipeMemory = new DefaultCharacterRecipeMemory();
            GameplayData.Traits = traits;
            return;
        }

        if (_bodyHost == null || _skillsHost == null || _traitsHost == null)
        {
            Debug.LogError(
                $"[CharacterDefinitionBinder] '{name}' needs CharacterBodyHost, CharacterSkillsHost, and CharacterTraitsHost, or UseGameplayData on those hosts.",
                this);
            return;
        }

        _bodyHost.BindBody(body);
        _skillsHost.BindSkills(skills);
        _traitsHost.BindTraits(traits);
    }

    bool UsesGameplayData()
    {
        if (_bodyHost != null && _bodyHost.UseGameplayDataBody)
            return true;
        if (_skillsHost != null && _skillsHost.UseGameplayDataSkills)
            return true;
        if (_traitsHost != null && _traitsHost.UseGameplayDataTraits)
            return true;
        return _bodyHost == null && _skillsHost == null && _traitsHost == null;
    }

    void EnsureHosts()
    {
        CharacterBodyRefs refs = CharacterBodyResolve.GetRefs(this);
        if (refs != null)
        {
            _bodyHost = refs.BodyHost;
            _skillsHost = refs.SkillsHost;
            _traitsHost = refs.TraitsHost;
            _appearanceHost = refs.Appearance;
            _factionHost = refs.FactionHost;
            _footprintHost = refs.FootprintHost;
            _characterState = refs.State;
            _vision = refs.Vision;
            _hearing = refs.Hearing;
            _motor = refs.Motor;
        }
        else
        {
            _bodyHost = CharacterBodyResolve.GetInBody<CharacterBodyHost>(this);
            _skillsHost = CharacterBodyResolve.GetInBody<CharacterSkillsHost>(this);
            _traitsHost = CharacterBodyResolve.GetInBody<CharacterTraitsHost>(this);
            _appearanceHost ??= CharacterBodyResolve.GetInBody<CharacterAppearanceHost>(this);
            _factionHost ??= CharacterBodyResolve.GetInBody<CharacterFactionHost>(this);
            _footprintHost ??= CharacterBodyResolve.GetInBody<CharacterFootprintHost>(this);
            _characterState ??= CharacterBodyResolve.GetInBody<CharacterState>(this);
            _vision ??= CharacterBodyResolve.GetInBody<CharacterVision>(this);
            _hearing ??= CharacterBodyResolve.GetInBody<CharacterHearing>(this);
            _motor ??= CharacterBodyResolve.GetInBody<CharacterMotor>(this);
        }

        if (_appearanceHost == null)
        {
            Debug.LogError(
                $"[CharacterDefinitionBinder] '{name}' needs CharacterAppearanceHost on the prefab.",
                this);
        }
        if (_factionHost == null)
        {
            Debug.LogError(
                $"[CharacterDefinitionBinder] '{name}' needs CharacterFactionHost on the prefab.",
                this);
        }
        if (_footprintHost == null)
        {
            Debug.LogError(
                $"[CharacterDefinitionBinder] '{name}' needs CharacterFootprintHost on the prefab.",
                this);
        }
        if (_hearing == null)
        {
            Debug.LogError(
                $"[CharacterDefinitionBinder] '{name}' needs CharacterHearing on the prefab.",
                this);
        }
    }
}
