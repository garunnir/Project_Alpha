// ============================================================
// CharacterTraitsHost — 상시 패시브 특성 보유 (plain module)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;

public sealed class CharacterTraitsHost
{
    bool _useGameplayDataTraits;
    DefaultCharacterTraits _ownedTraits;
    ICharacterTraits _traits;

    public ICharacterTraits Traits
    {
        get
        {
            EnsureTraits();
            return _traits;
        }
    }

    /// <summary>Already-bound instance. Does not Ensure — possessed resolver must not call Traits.</summary>
    public ICharacterTraits BoundTraits => _traits;

    public bool UseGameplayDataTraits => _useGameplayDataTraits;

    public void ConfigureUseGameplayDataTraits(bool useGameplayDataTraits)
    {
        _useGameplayDataTraits = useGameplayDataTraits;
    }

    public void Bind(CharacterBodyRefs refs)
    {
    }

    void EnsureTraits()
    {
        if (_traits != null)
            return;

        if (_useGameplayDataTraits)
        {
            ICharacterTraits backing = GameplayPlayerRuntime.PeekTraitsBacking();
            if (backing == null)
            {
                backing = new DefaultCharacterTraits();
                GameplayData.Traits = backing;
            }

            _traits = backing;
            return;
        }

        _ownedTraits = new DefaultCharacterTraits();
        _traits = _ownedTraits;
    }

    public void BindTraits(DefaultCharacterTraits traits)
    {
        if (traits == null)
            return;

        if (_useGameplayDataTraits)
        {
            GameplayData.Traits = traits;
            _traits = traits;
            return;
        }

        _ownedTraits = traits;
        _traits = traits;
    }
}
