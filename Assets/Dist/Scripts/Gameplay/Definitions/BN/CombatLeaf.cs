// ============================================================
// CombatLeaf — 시전·선택 슬롯 (Family 묶음은 Util). 동작은 Attack.logicId.
// ============================================================

using System;

/// <summary>
/// UI 묶음. Melee=근접 타격, Trigger=발사 모드,
/// Etc=그 외(Excavate/Chop/Raise 등 — 미분류 묶음, Melee 쓰레기통 방지).
/// </summary>
public enum CombatLeafFamily
{
    Melee = 0,
    Trigger = 1,
    Etc = 2
}

/// <summary>팔 애니·Pipeline thin·슬롯 파일 접두. Leaf와 1:1이 아님(Semi/Burst/Auto→Trigger).</summary>
public enum AnimVerb
{
    Swing = 0,
    Thrust = 1,
    Dig = 2,
    Trigger = 3,
    Raise = 4
}

[Flags]
public enum CombatLeafMask
{
    None = 0,
    Strike = 1,
    /// <summary>구 Trigger 비트. Normalize 후 Semi로 접힘.</summary>
    Trigger = 4,
    Pierce = 8,
    Raise = 16,
    Semi = 32,
    Burst = 64,
    Auto = 128,
    Excavate = 256,
    Chop = 512
}

/// <summary>
/// Presentation 줄·플레이어 선택·영속. 값 고정 —
/// 0=Strike, 2=Trigger(구·Normalize→Semi), 3=Pierce, 4=Raise, 5=Semi, 6=Burst, 7=Auto, 8=Excavate, 9=Chop.
/// 1은 구 Cutting — Normalize가 Strike로 접음.
/// 실제 동작 SSOT = <see cref="WeaponAttack"/> logicId → IActionHandler.
/// 연출 슬롯 = <see cref="CombatLeafUtil.ToAnimVerb"/>.
/// </summary>
public enum CombatLeaf
{
    Strike = 0,
    Trigger = 2,
    Pierce = 3,
    Raise = 4,
    Semi = 5,
    Burst = 6,
    Auto = 7,
    Excavate = 8,
    Chop = 9
}
