// ============================================================
// CharacterFactionHost — 본체가 속한 세력을 보관 (plain module)
// ============================================================

public sealed class CharacterFactionHost
{
    CharacterBodyRefs _refs;
    CharacterFaction _faction;

    public CharacterFaction Faction => _faction;
    public string name => _refs != null ? _refs.name : string.Empty;

    public void Bind(CharacterBodyRefs refs)
    {
        _refs = refs;
    }

    public int GetInstanceID() => _refs != null ? _refs.GetInstanceID() : 0;

    public UnityEngine.Object LogContext => _refs;

    public void ApplyFromDefinition(CharacterDefinition definition)
    {
        _faction = definition != null ? definition.Faction : null;
    }
}
