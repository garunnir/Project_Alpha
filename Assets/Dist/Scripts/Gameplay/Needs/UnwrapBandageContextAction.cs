// ============================================================
// UnwrapBandageContextAction — 부위 붕대 수동 벗기
// ============================================================

using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;

public sealed class UnwrapBandageContextAction : IContextMenuAction
{
    static readonly List<BodyPartEffect> EffectScratch = new(8);

    readonly string _partId;

    public UnwrapBandageContextAction(string partId)
    {
        _partId = partId;
    }

    public string GetDisabledReason()
    {
        ICharacterBody body = PlayerPossessSession.SessionBody;
        if (body == null || string.IsNullOrEmpty(_partId))
            return ItemContextMenuLabels.ConsumeBlocked;
        if (!BodyHealApply.HasBandagedUnder(body, _partId, EffectScratch))
            return ItemContextMenuLabels.ConsumeBlocked;
        return null;
    }

    public void Execute()
    {
        BodyHealApply.TryUnwrap(PlayerPossessSession.SessionBody, _partId);
    }
}
