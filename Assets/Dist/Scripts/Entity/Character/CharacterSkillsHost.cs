// ============================================================
// CharacterSkillsHost ù skills + Defeat host (plain module, pairs with BodyHost)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;

public sealed class CharacterSkillsHost
{
    CharacterBodyRefs _refs;
    CharacterBodyHost _bodyHost;
    DefaultCharacterSkills _ownedSkills;
    ICharacterSkills _skills;
    BodySkillModifierAggregator _bodyAggregator;
    DefaultCharacterDefeat _ownedDefeat;
    ICharacterDefeat _defeat;
    DefaultCharacterRecipeMemory _ownedRecipeMemory;
    ICharacterRecipeMemory _recipeMemory;
    bool _useGameplayDataSkills;
    bool _bodySubscribed;

    public ICharacterSkills Skills
    {
        get
        {
            EnsureSkills();
            return _skills;
        }
    }

    public ICharacterDefeat Defeat
    {
        get
        {
            EnsureDefeat();
            return _defeat;
        }
    }

    public ICharacterRecipeMemory RecipeMemory
    {
        get
        {
            EnsureRecipeMemory();
            return _recipeMemory;
        }
    }

    public bool UseGameplayDataSkills => _useGameplayDataSkills;

    public void ConfigureUseGameplayDataSkills(bool useGameplayDataSkills)
    {
        _useGameplayDataSkills = useGameplayDataSkills;
    }

    public void Bind(CharacterBodyRefs refs)
    {
        UnsubscribeBody();
        _refs = refs;
        _bodyHost = refs != null ? refs.BodyHost : null;
    }

    public void Enable()
    {
        _bodyHost = _refs != null ? _refs.BodyHost : _bodyHost;
        EnsureSkills();
        BindBodyToSkills();
        EnsureDefeat();
        EnsureRecipeMemory();
        SubscribeBody();
    }

    public void Disable()
    {
        UnsubscribeBody();
    }

    public void Dispose()
    {
        UnsubscribeBody();
        if (_bodyAggregator != null && _skills != null)
            _skills.RemoveModifierSource(_bodyAggregator);
        _bodyAggregator = null;
        _ownedDefeat?.Dispose();
        _ownedDefeat = null;
        _defeat = null;
    }

    void EnsureSkills()
    {
        if (_skills != null)
            return;

        if (_useGameplayDataSkills)
        {
            _skills = GameplayData.CharacterSkills;
            return;
        }

        _ownedSkills = SkillCatalog.CreateSeededSkills();
        _skills = _ownedSkills;
    }

    void EnsureRecipeMemory()
    {
        if (_recipeMemory != null)
            return;

        if (_useGameplayDataSkills)
        {
            _recipeMemory = GameplayData.RecipeMemory;
            return;
        }

        _ownedRecipeMemory = new DefaultCharacterRecipeMemory();
        _recipeMemory = _ownedRecipeMemory;
    }

    /// <summary>Definition Apply ù replace owned skills instance.</summary>
    public void BindSkills(DefaultCharacterSkills skills)
    {
        _bodyHost = _refs != null ? _refs.BodyHost : _bodyHost;

        if (_bodyAggregator != null && _skills != null)
        {
            _skills.RemoveModifierSource(_bodyAggregator);
            _bodyAggregator = null;
        }

        _ownedDefeat?.Dispose();
        _ownedDefeat = null;
        _defeat = null;

        _ownedSkills = skills;
        _skills = skills;

        BindBodyToSkills();
        EnsureDefeat();
    }

    void BindBodyToSkills()
    {
        if (_skills == null || _bodyHost == null)
            return;

        ICharacterBody body = _bodyHost.Body;
        if (body == null)
            return;

        if (_bodyAggregator != null)
        {
            _skills.RemoveModifierSource(_bodyAggregator);
            _bodyAggregator = null;
        }

        _bodyAggregator = new BodySkillModifierAggregator(body, _skills);
        _skills.AddModifierSource(_bodyAggregator);
        _skills.Refresh();
    }

    void EnsureDefeat()
    {
        if (_defeat != null)
            return;

        if (_useGameplayDataSkills)
        {
            _defeat = GameplayData.Defeat;
            return;
        }

        _ownedDefeat = new DefaultCharacterDefeat(_bodyHost != null ? _bodyHost.Body : null, Skills);
        _defeat = _ownedDefeat;
    }

    void SubscribeBody()
    {
        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        if (body == null || _bodySubscribed)
            return;

        body.Changed += OnBodyChanged;
        _bodySubscribed = true;
    }

    void UnsubscribeBody()
    {
        if (!_bodySubscribed)
            return;

        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        if (body != null)
            body.Changed -= OnBodyChanged;
        _bodySubscribed = false;
    }

    void OnBodyChanged()
    {
        _skills?.Refresh();
    }
}
