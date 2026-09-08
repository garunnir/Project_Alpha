// ============================================================
// ICombatPerformDriver — Leaf별 시전 payload (Layer2b)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;

/// <summary>
/// 플레이어 시전 라우트. 조작 모양은 <see cref="ICombatAttackInput"/>.
/// Layer3(<see cref="CharacterAttacker"/>)는 건드리지 않음.
/// </summary>
public interface ICombatPerformDriver
{
    bool MatchesLeaf(CombatLeaf leaf);

    /// <summary>LMB performed(click Input). 처리했으면 true.</summary>
    bool TryOnAttackPerformed(in CombatPerformContext ctx);

    /// <summary>매 프레임(hold Input 등).</summary>
    void Tick(in CombatPerformContext ctx);

    void Clear(in CombatPerformContext ctx);
}

/// <summary>Perform 드라이버가 공유하는 런타임 참조·게이트.</summary>
public readonly struct CombatPerformContext
{
    public readonly CharacterAttacker Attacker;
    public readonly CharacterState CharacterState;
    public readonly CharacterActionHost ActionHost;
    public readonly bool InputEnabled;
    public readonly PlayerCombatController Host;

    public CombatPerformContext(
        CharacterAttacker attacker,
        CharacterState characterState,
        CharacterActionHost actionHost,
        bool inputEnabled,
        PlayerCombatController host)
    {
        Attacker = attacker;
        CharacterState = characterState;
        ActionHost = actionHost;
        InputEnabled = inputEnabled;
        Host = host;
    }
}
