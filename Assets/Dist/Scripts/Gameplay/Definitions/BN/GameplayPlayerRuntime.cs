// ============================================================
// GameplayPlayerRuntime — possessed 플레이어 런타임 SSOT
// ============================================================

using System;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public static class GameplayPlayerRuntime
{
    static IPlayerStats _stats;
    static ICharacterBody _body;
    static IPlayerVitals _vitals;
    static DefaultCharacterDefeat _defeat;
    static ICharacterProficiencies _proficiencies;
    static ICharacterRecipeMemory _recipeMemory;
    static ICharacterTraits _traits;
    static Func<ICharacterTraits> _possessedTraitsResolver;

    /// <summary>Player-facing stats / vitals host (possessed path; NPC uses ICharacterSkills).</summary>
    public static IPlayerStats Stats
    {
        get
        {
            if (_stats == null)
                _stats = new DefaultPlayerStats();
            return _stats;
        }
        set
        {
            _stats = value;
            InvalidateDefeat();
        }
    }

    /// <summary>Skill API. Stats is DefaultPlayerStats only path.</summary>
    public static ICharacterSkills CharacterSkills =>
        Stats is DefaultPlayerStats dps ? dps.Skills : null;

    /// <summary>Body graph SSOT.</summary>
    public static ICharacterBody Body
    {
        get
        {
            if (_body == null)
            {
                _body = CharacterBody.CreateHumanDefault(Stats.GetSkillLevel(AttributeIds.Str));
                InvalidateDefeat();
            }

            return _body;
        }
        set
        {
            _body = value;
            InvalidateDefeat();
        }
    }

    /// <summary>Player vitals SSOT.</summary>
    public static IPlayerVitals Vitals
    {
        get
        {
            if (_vitals == null)
                _vitals = new DefaultPlayerVitals();
            return _vitals;
        }
        set => _vitals = value;
    }

    /// <summary>Defeat Body ∨ Skills OR.</summary>
    public static ICharacterDefeat Defeat
    {
        get
        {
            if (_defeat == null)
                _defeat = new DefaultCharacterDefeat(Body, CharacterSkills);
            return _defeat;
        }
        set
        {
            InvalidateDefeat();
            if (value is DefaultCharacterDefeat concrete)
                _defeat = concrete;
            else if (value != null)
                Debug.LogWarning("[GameplayPlayerRuntime] Defeat setter expects DefaultCharacterDefeat; ignored.");
        }
    }

    /// <summary>Craft proficiency SSOT (BN recipe proficiencies).</summary>
    public static ICharacterProficiencies Proficiencies
    {
        get
        {
            if (_proficiencies == null)
                _proficiencies = new DefaultCharacterProficiencies();
            return _proficiencies;
        }
        set => _proficiencies = value;
    }

    /// <summary>Permanent recipe knowledge (decomp_learn etc). Map JSON playerProgress로 저장.</summary>
    public static ICharacterRecipeMemory RecipeMemory
    {
        get
        {
            if (_recipeMemory == null)
                _recipeMemory = new DefaultCharacterRecipeMemory();
            return _recipeMemory;
        }
        set => _recipeMemory = value;
    }

    /// <summary>Character traits (omniscience, omnivision, survival, …). Map JSON playerProgress로 저장.</summary>
    public static void RegisterPossessedTraitsResolver(Func<ICharacterTraits> resolver)
    {
        _possessedTraitsResolver = resolver;
    }

    /// <summary>Backing only — skips possessed resolver (host Ensure/Bind must not recurse).</summary>
    public static ICharacterTraits PeekTraitsBacking() => _traits;

    public static ICharacterTraits Traits
    {
        get
        {
            ICharacterTraits resolved = _possessedTraitsResolver?.Invoke();
            if (resolved != null)
                return resolved;

            if (_traits == null)
                _traits = new DefaultCharacterTraits();
            return _traits;
        }
        set => _traits = value;
    }

    static void InvalidateDefeat()
    {
        _defeat?.Dispose();
        _defeat = null;
    }
}
