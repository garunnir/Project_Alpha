// ============================================================
// MessageLogPlayerCombatSink — 플레이어 피격·기습·패배만 메시지 로그에 남김
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MessageLogPlayerCombatSink : MonoBehaviour
{
    const string LocPlayerHit = "msg.combat.player_hit";
    const string LocSurpriseDealt = "msg.combat.surprise_dealt";
    const string LocSurpriseTaken = "msg.combat.surprise_taken";
    const string LocSurpriseNeck = "msg.combat.surprise_neck";
    const string LocSurpriseStun = "msg.combat.surprise_stun";
    const string LocDefeatBody = "msg.status.defeat_body";
    const string LocDefeatCollapse = "msg.status.defeat_collapse";
    const string PartLocKeyPrefix = "PlayerStatus.Part.";

    bool _subscribed;
    bool _wasDefeated;

    void OnEnable() => EnsureSubscribed();

    void Start() => EnsureSubscribed();

    void OnDisable() => Unsubscribe();

    void OnDestroy() => Unsubscribe();

    void EnsureSubscribed()
    {
        if (_subscribed)
            return;

        CharacterAttacker.AnyAttackJudged += OnAnyAttackJudged;
        ICharacterDefeat defeat = GameplayData.Defeat;
        if (defeat != null)
        {
            _wasDefeated = defeat.IsDefeated;
            defeat.Changed += OnDefeatChanged;
        }

        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed)
            return;

        CharacterAttacker.AnyAttackJudged -= OnAnyAttackJudged;
        ICharacterDefeat defeat = GameplayData.Defeat;
        if (defeat != null)
            defeat.Changed -= OnDefeatChanged;

        _subscribed = false;
    }

    void OnAnyAttackJudged(AttackOutcome outcome)
    {
        if (!outcome.DidHit)
            return;

        CharacterBodyHost target = outcome.Target;
        if (target == null)
            return;

        ICharacterBody body = target.Body;
        bool playerIsTarget = body != null && ReferenceEquals(body, PlayerPossessSession.SessionBody);
        bool playerIsAttacker = IsPossessedBody(outcome.Attacker);

        if (playerIsTarget)
        {
            string partLabel = ResolvePartLabel(outcome.AimedPartId);
            string text = Loc.Format(LocPlayerHit, partLabel, outcome.Damage);
            GameplayMessageLog.Append(
                MessageLogCategory.Combat,
                MessageLogImportance.Normal,
                text);

            if (outcome.IsSurprise)
            {
                GameplayMessageLog.Append(
                    MessageLogCategory.Combat,
                    MessageLogImportance.Critical,
                    Loc.Get(LocSurpriseTaken));
                AppendSurpriseMeleeLine(outcome);
            }

            return;
        }

        if (playerIsAttacker && outcome.IsSurprise)
        {
            GameplayMessageLog.Append(
                MessageLogCategory.Combat,
                MessageLogImportance.Critical,
                Loc.Get(LocSurpriseDealt));
            AppendSurpriseMeleeLine(outcome);
        }
    }

    static void AppendSurpriseMeleeLine(in AttackOutcome outcome)
    {
        if (outcome.SurpriseMelee == SurpriseMeleeKind.Neck)
        {
            GameplayMessageLog.Append(
                MessageLogCategory.Combat,
                MessageLogImportance.Critical,
                Loc.Get(LocSurpriseNeck));
        }
        else if (outcome.SurpriseMelee == SurpriseMeleeKind.Stun)
        {
            GameplayMessageLog.Append(
                MessageLogCategory.Combat,
                MessageLogImportance.Normal,
                Loc.Get(LocSurpriseStun));
        }
    }

    static bool IsPossessedBody(CharacterBodyHost host)
    {
        if (host == null || host.Body == null)
            return false;
        if (!ReferenceEquals(host.Body, PlayerPossessSession.SessionBody))
            return false;
        return CharacterBodyResolve.TryGetInBody(host, out CharacterMotor motor) && motor.IsPossessed;
    }

    void OnDefeatChanged()
    {
        ICharacterDefeat defeat = GameplayData.Defeat;
        if (defeat == null)
            return;

        bool isDefeated = defeat.IsDefeated;
        if (!isDefeated || _wasDefeated)
        {
            _wasDefeated = isDefeated;
            return;
        }

        _wasDefeated = true;
        string key = defeat.Cause == DefeatCause.StatCollapse
            ? LocDefeatCollapse
            : LocDefeatBody;
        GameplayMessageLog.Append(
            MessageLogCategory.Status,
            MessageLogImportance.Critical,
            Loc.Get(key));
    }

    static string ResolvePartLabel(string partId)
    {
        if (string.IsNullOrEmpty(partId))
            return Loc.Get(PartLocKeyPrefix + BodyPartIds.Torso);

        string key = PartLocKeyPrefix + partId;
        if (Loc.TryGet(key, out string label))
            return label;

        return partId;
    }
}
