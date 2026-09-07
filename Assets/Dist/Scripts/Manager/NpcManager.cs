// ============================================================
// NpcManager — 씬 NPC를 원격 틱 (유닛별 FSM, 프리팹에 뇌 스크립트 없음)
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public enum NpcCombatState
{
    Idle = 0,
    Patrol = 1,
    Alert = 2,
    Chase = 3,
    Attack = 4,
    Return = 5,
    Dead = 6
}

public static class NpcAgentDefaults
{
    public const float AttackStandDistance = 1.1f;
    public const float AlertSeconds = 0.35f;
    public const float StoppingDistance = 0.1f;
}

[Serializable]
public sealed class NpcAgentEntry
{
    public Transform character;
    public Transform[] waypoints;
    public MovementStyle patrolStyle;
    public MovementStyle chaseStyle;
    public MovementStyle holdStyle;
    [Min(0f)] public float attackStandDistance = NpcAgentDefaults.AttackStandDistance;
    [Min(0f)] public float alertSeconds = NpcAgentDefaults.AlertSeconds;
    [Tooltip("무력화: 조준 다리.")]
    public bool suppressMode;
}

[DefaultExecutionOrder(-20)]
[DisallowMultipleComponent]
public sealed class NpcManager : MonoBehaviour
{
    [SerializeField] List<NpcAgentEntry> _agents = new();

    readonly List<NpcAgentRuntime> _runtimes = new(8);

    /// <summary>씬 활성 인스턴스 (기습 Vision lock).</summary>
    public static NpcManager Active { get; private set; }

    void OnEnable()
    {
        Active = this;
        BindRuntimes();
    }

    void OnDisable()
    {
        if (Active == this)
            Active = null;

        for (int i = 0; i < _runtimes.Count; i++)
            _runtimes[i].Release();
        _runtimes.Clear();
    }

    void Update()
    {
        // 할당 없음. 호스트는 CharacterBodyHost 레지스트리, 상태는 행 단위.
        float dt = TimeScaleService.Delta(TimeScaleChannel.World);
        if (dt <= 0f)
            return;

        for (int i = 0; i < _runtimes.Count; i++)
            _runtimes[i].Tick(dt);
    }

    public void Register(NpcAgentEntry entry)
    {
        if (entry == null || entry.character == null)
            return;

        if (_agents == null)
            _agents = new List<NpcAgentEntry>();
        _agents.Add(entry);

        if (!isActiveAndEnabled)
            return;

        var runtime = new NpcAgentRuntime();
        if (!runtime.TryBind(entry))
            return;

        _runtimes.Add(runtime);
    }

    /// <summary>observer NPC가 subject를 Vision 채널로 유지 중이면 true (기습 LoseRadius).</summary>
    public bool TryGetVisionLock(CharacterBodyHost observer, CharacterBodyHost subject)
    {
        if (observer == null || subject == null)
            return false;

        for (int i = 0; i < _runtimes.Count; i++)
        {
            if (_runtimes[i].TryGetVisionLock(observer, subject))
                return true;
        }

        return false;
    }

    /// <summary>NPC가 잡은 전투 타깃 (애니 1차 기습 대상).</summary>
    public bool TryGetCombatTarget(CharacterBodyHost observer, out CharacterBodyHost target)
    {
        target = null;
        if (observer == null)
            return false;

        for (int i = 0; i < _runtimes.Count; i++)
        {
            if (_runtimes[i].TryGetCombatTarget(observer, out target))
                return target != null;
        }

        return false;
    }

    void BindRuntimes()
    {
        for (int i = 0; i < _runtimes.Count; i++)
            _runtimes[i].Release();
        _runtimes.Clear();

        if (_agents == null)
            return;

        for (int i = 0; i < _agents.Count; i++)
        {
            NpcAgentEntry entry = _agents[i];
            if (entry == null || entry.character == null)
                continue;

            var runtime = new NpcAgentRuntime();
            if (!runtime.TryBind(entry))
                continue;

            _runtimes.Add(runtime);
        }
    }

    sealed class NpcAgentRuntime
    {
        NpcAgentEntry _entry;
        Transform _transform;
        CharacterMotor _motor;
        CharacterAttacker _attacker;
        CharacterSkillsHost _skillsHost;
        CharacterState _characterState;
        CharacterBodyHost _selfHost;
        CharacterPainHost _painHost;
        CharacterFactionHost _selfFactionHost;
        CharacterVision _vision;
        CharacterHearing _hearing;
        CharacterEmoteHost _emote;
        ICharacterDefeat _defeat;

        NpcCombatState _state = NpcCombatState.Idle;
        int _patrolIndex;
        float _alertTimer;
        Vector3 _homePosition;
        CharacterBodyHost _target;
        float _distanceToTarget = float.MaxValue;
        SenseContactChannel _contact = SenseContactChannel.None;
        Vector3Int _heardCell;
        Vector3 _heardWorld;

        public bool TryBind(NpcAgentEntry entry)
        {
            _entry = entry;
            _transform = entry.character;
            GameObject go = _transform.gameObject;

            _motor = go.GetBodyComponent<CharacterMotor>();
            _attacker = go.GetBodyComponent<CharacterAttacker>();
            _skillsHost = go.GetBodyComponent<CharacterSkillsHost>();
            _characterState = go.GetBodyComponent<CharacterState>();
            _selfHost = go.GetBodyComponent<CharacterBodyHost>();
            _painHost = go.GetBodyComponent<CharacterPainHost>();
            _selfFactionHost = go.GetBodyComponent<CharacterFactionHost>();
            _vision = go.GetBodyComponent<CharacterVision>();
            _hearing = go.GetBodyComponent<CharacterHearing>();
            _emote = go.GetBodyComponent<CharacterEmoteHost>();

            if (_motor == null || _attacker == null || _selfHost == null)
            {
                Debug.LogError(
                    $"[NpcManager] '{go.name}' needs CharacterMotor, CharacterAttacker, CharacterBodyHost.",
                    go);
                return false;
            }

            _homePosition = _transform.position;
            ApplyAimPreference();

            _defeat = _skillsHost != null ? _skillsHost.Defeat : null;
            if (_defeat != null)
                _defeat.Changed += OnDefeatChanged;

            if (_defeat != null && _defeat.IsDefeated)
                EnterDead();
            else
                EnterPatrol();

            return true;
        }

        public void Release()
        {
            if (_defeat != null)
                _defeat.Changed -= OnDefeatChanged;
            _defeat = null;
            NpcSteer.Stop(_motor);
            ReleaseCombatAim();
            _emote?.ClearCombat();
        }

        public bool TryGetVisionLock(CharacterBodyHost observer, CharacterBodyHost subject)
        {
            if (_selfHost == null || observer == null || subject == null)
                return false;
            if (_selfHost != observer)
                return false;
            return _target == subject && _contact == SenseContactChannel.Vision;
        }

        public bool TryGetCombatTarget(CharacterBodyHost observer, out CharacterBodyHost target)
        {
            target = null;
            if (_selfHost == null || observer == null || _selfHost != observer)
                return false;
            target = _target;
            return target != null;
        }

        public void Tick(float dt)
        {
            if (_transform == null || _motor == null)
                return;
            if (!_transform.gameObject.activeInHierarchy)
                return;
            if (_motor.IsPossessed)
                return;
            if (_state == NpcCombatState.Dead)
                return;

            if (_defeat != null && _defeat.IsDefeated)
            {
                EnterDead();
                return;
            }

            if (_painHost != null && _painHost.IsPainShocked)
                return;

            RefreshTarget();

            switch (_state)
            {
                case NpcCombatState.Idle:
                case NpcCombatState.Patrol:
                    TickPatrol();
                    break;
                case NpcCombatState.Alert:
                    TickAlert(dt);
                    break;
                case NpcCombatState.Chase:
                    TickChase();
                    break;
                case NpcCombatState.Attack:
                    TickAttack();
                    break;
                case NpcCombatState.Return:
                    TickReturn();
                    break;
            }
        }

        void TickPatrol()
        {
            if (_target != null)
            {
                if (CharacterSenseContactResolver.AllowsAlert(_contact))
                    EnterAlert();
                else
                    EnterChase();
                return;
            }

            Transform[] waypoints = _entry.waypoints;
            if (waypoints == null || waypoints.Length == 0)
            {
                NpcSteer.Stop(_motor);
                _motor.SetActiveMovementStyle(_entry.holdStyle);
                return;
            }

            _motor.SetActiveMovementStyle(_entry.patrolStyle);
            Transform waypoint = waypoints[_patrolIndex];
            if (waypoint == null)
            {
                AdvancePatrolIndex();
                return;
            }

            if (NpcSteer.TryArriveOrSteer(
                    _motor,
                    _transform.position,
                    waypoint.position,
                    ResolveStoppingDistance(_entry.patrolStyle)))
                AdvancePatrolIndex();
        }

        void TickAlert(float dt)
        {
            _alertTimer -= dt;
            if (_target == null)
            {
                EnterReturn();
                return;
            }

            if (_alertTimer > 0f)
                return;

            EnterChase();
        }

        void TickChase()
        {
            if (_target == null)
            {
                EnterReturn();
                return;
            }

            if (CharacterSenseContactResolver.AllowsAttack(_contact) &&
                IsSelectedActionInRange(_distanceToTarget))
            {
                EnterAttack();
                return;
            }

            _motor.SetActiveMovementStyle(_entry.chaseStyle);
            Vector3 steerGoal = CharacterSenseContactResolver.ResolveSteerGoal(
                _contact,
                _target.transform,
                _heardWorld);
            NpcSteer.TryArriveOrSteer(
                _motor,
                _transform.position,
                steerGoal,
                ResolveStoppingDistance(_entry.chaseStyle));
        }

        void TickAttack()
        {
            if (_target == null)
            {
                EnterReturn();
                return;
            }

            if (!CharacterSenseContactResolver.AllowsAttack(_contact))
            {
                EnterChase();
                return;
            }

            if (!IsSelectedActionInRange(_distanceToTarget))
            {
                EnterChase();
                return;
            }

            _motor.SetActiveMovementStyle(_entry.holdStyle);
            if (_distanceToTarget > _entry.attackStandDistance)
            {
                NpcSteer.TryArriveOrSteer(
                    _motor,
                    _transform.position,
                    _target.transform.position,
                    _entry.attackStandDistance);
            }
            else
            {
                NpcSteer.Stop(_motor);
            }

            AimAtTarget(_target);

            WeaponAction action = _attacker.SelectedAction;
            if (action == WeaponAction.Raise)
                return;

            AttackPerformResult result = _attacker.TryPerformSelected(_target);
            if (result == AttackPerformResult.OutOfRange)
                EnterChase();
        }

        void TickReturn()
        {
            if (_target != null)
            {
                if (CharacterSenseContactResolver.AllowsAlert(_contact))
                    EnterAlert();
                else
                    EnterChase();
                return;
            }

            _motor.SetActiveMovementStyle(_entry.patrolStyle);
            if (NpcSteer.TryArriveOrSteer(
                    _motor,
                    _transform.position,
                    _homePosition,
                    ResolveStoppingDistance(_entry.patrolStyle)))
                EnterPatrol();
        }

        void RefreshTarget()
        {
            Vector3 selfFeet = CharacterFeetPose.GetFeetWorld(_transform);
            Vector3 forward = CharacterSightForward.ResolveXZ(_characterState, _transform);

            if (_target != null)
            {
                if (!IsUsableTarget(_target))
                {
                    ClearTarget();
                    return;
                }

                Vector3 targetFeet = CharacterFeetPose.GetFeetWorld(_target.transform);
                _distanceToTarget = HorizontalDistance(_target.transform.position);
                CharacterBodyResolve.TryGetInBody(_target, out CharacterMotor targetMotor);

                bool visionKeep = EvaluateVisionKeep(selfFeet, forward, _target);
                bool hearingKeep = EvaluateHearingDetect(selfFeet, _target, targetMotor);
                _contact = CharacterSenseContactResolver.Resolve(visionKeep, hearingKeep);
                if (_contact == SenseContactChannel.None)
                {
                    ClearTarget();
                    return;
                }

                UpdateHeardLocation(targetFeet);
                return;
            }

            CharacterBodyHost best = null;
            float bestDist = float.MaxValue;
            SenseContactChannel bestContact = SenseContactChannel.None;
            int hostCount = CharacterBodyHost.ActiveCount;
            for (int i = 0; i < hostCount; i++)
            {
                CharacterBodyHost host = CharacterBodyHost.GetActive(i);
                if (!IsUsableTarget(host) || host == _selfHost)
                    continue;
                if (!IsPreferredHostile(host))
                    continue;

                Vector3 targetFeet = CharacterFeetPose.GetFeetWorld(host.transform);
                float dist = HorizontalDistance(host.transform.position);
                CharacterBodyResolve.TryGetInBody(host, out CharacterMotor targetMotor);

                bool visionDetect = EvaluateVisionDetect(selfFeet, forward, host);
                if (visionDetect && dist < bestDist)
                {
                    best = host;
                    bestDist = dist;
                    bestContact = SenseContactChannel.Vision;
                }
            }

            if (best == null)
            {
                for (int i = 0; i < hostCount; i++)
                {
                    CharacterBodyHost host = CharacterBodyHost.GetActive(i);
                    if (!IsUsableTarget(host) || host == _selfHost)
                        continue;
                    if (!IsPreferredHostile(host))
                        continue;

                    Vector3 targetFeet = CharacterFeetPose.GetFeetWorld(host.transform);
                    float dist = HorizontalDistance(host.transform.position);
                    CharacterBodyResolve.TryGetInBody(host, out CharacterMotor targetMotor);
                    if (!EvaluateHearingDetect(selfFeet, host, targetMotor) || dist >= bestDist)
                        continue;

                    best = host;
                    bestDist = dist;
                    bestContact = SenseContactChannel.Hearing;
                }
            }

            if (best == null)
            {
                ClearTarget();
                return;
            }

            BindTarget(best, bestDist, bestContact);
            UpdateHeardLocation(CharacterFeetPose.GetFeetWorld(best.transform));
        }

        void BindTarget(CharacterBodyHost host, float distance, SenseContactChannel contact)
        {
            bool acquired = host != null && _target != host;
            _target = host;
            _distanceToTarget = distance;
            _contact = contact;
            if (acquired)
                SyncCombatEmoteForContact();
        }

        void SyncCombatEmoteForContact()
        {
            if (_emote == null || _target == null || _contact == SenseContactChannel.None)
                return;

            if (_contact == SenseContactChannel.Vision)
                _emote.SetAlertSpotted();
            else if (_contact == SenseContactChannel.Hearing)
                _emote.SetAlertSuspicious();
        }

        void ClearTarget()
        {
            BindTarget(null, float.MaxValue, SenseContactChannel.None);
            _heardCell = default;
            _heardWorld = default;
            _emote?.ClearCombat();
        }

        void UpdateHeardLocation(Vector3 targetFeet)
        {
            if (_contact != SenseContactChannel.Hearing)
                return;

            float cellSize = _hearing != null ? _hearing.TopologyCellSize : 1f;
            _heardCell = IsoTilemap.TileHelper.ConvertWorldToGrid(targetFeet, cellSize);
            _heardWorld = IsoTilemap.TileHelper.ConvertGridToWorldPos(_heardCell, cellSize);
        }

        bool EvaluateVisionDetect(Vector3 selfFeet, Vector3 forward, CharacterBodyHost targetHost)
        {
            if (targetHost == null)
                return false;

            Vector3 targetFeet = CharacterFeetPose.GetFeetWorld(targetHost.transform);
            float visibility = CharacterPresenceHost.ResolveVisibility01(targetHost);
            if (visibility <= 0f)
                return false;

            return CharacterSightForward.IsWithinDetectCone(
                _vision,
                selfFeet,
                forward,
                targetFeet,
                visibility);
        }

        bool EvaluateVisionKeep(Vector3 selfFeet, Vector3 forward, CharacterBodyHost targetHost)
        {
            if (targetHost == null)
                return false;

            Vector3 targetFeet = CharacterFeetPose.GetFeetWorld(targetHost.transform);
            float visibility = CharacterPresenceHost.ResolveVisibility01(targetHost);
            if (visibility <= 0f)
                return false;

            return CharacterSightForward.IsWithinKeepCone(
                _vision,
                selfFeet,
                forward,
                targetFeet,
                visibility);
        }

        bool EvaluateHearingDetect(
            Vector3 selfFeet,
            CharacterBodyHost targetHost,
            CharacterMotor targetMotor)
        {
            if (_hearing == null || targetHost == null)
                return false;

            Vector3 targetFeet = CharacterFeetPose.GetFeetWorld(targetHost.transform);
            float noise = CharacterPresenceHost.ResolveNoise01(targetHost);
            return _hearing.CanDetect(selfFeet, targetFeet, targetMotor, noise);
        }

        bool IsPreferredHostile(CharacterBodyHost host)
        {
            if (host == null)
                return false;
            if (!CharacterBodyResolve.TryGetInBody(host, out CharacterFactionHost otherFaction))
                return false;
            return CharacterHostility.IsHostile(_selfFactionHost, otherFaction);
        }

        static bool IsUsableTarget(CharacterBodyHost host)
        {
            if (host == null || !host.isActiveAndEnabled)
                return false;
            ICharacterBody body = host.Body;
            if (body == null || body.IsDeadState)
                return false;
            if (CharacterBodyResolve.TryGetInBody(host, out CharacterPainHost painHost) && painHost.IsPainShocked)
                return false;
            if (CharacterBodyResolve.TryGetInBody(host, out CharacterSkillsHost skillsHost) &&
                skillsHost.Defeat != null &&
                skillsHost.Defeat.IsDefeated)
                return false;
            return true;
        }

        float HorizontalDistance(Vector3 world)
        {
            Vector3 offset = world - _transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        bool IsSelectedActionInRange(float distance)
        {
            if (_attacker == null)
                return false;
            WeaponAction action = _attacker.SelectedAction;
            if (!_attacker.CanPerform(action))
                return false;
            ItemData item = _attacker.ItemFor(_attacker.ItemId);
            ItemData ammo = WeaponChamber.ResolveAmmo(
                _attacker.WieldedStack,
                _attacker.WieldedInstance);
            return distance <= CombatMath.RangeMeters(item, action, ammo);
        }

        static float ResolveStoppingDistance(MovementStyle style) =>
            style != null ? style.StoppingDistance : NpcAgentDefaults.StoppingDistance;

        void SetAimHeld(bool held) => _attacker?.SetAimHeld(held);

        void ReleaseCombatAim()
        {
            SetAimHeld(false);
            _characterState?.ClearAim();
        }

        void AimAtTarget(CharacterBodyHost target)
        {
            if (_characterState == null || target == null)
                return;

            Vector3 toTarget = target.transform.position - _transform.position;
            toTarget.y = 0f;
            float reach = toTarget.magnitude;
            if (reach < CharacterAttacker.MinRayDistance)
                return;

            _characterState.SetAimDir(toTarget / reach, target.transform.position, reach);
        }

        void EnterIdle()
        {
            _state = NpcCombatState.Idle;
            ReleaseCombatAim();
            NpcSteer.Stop(_motor);
            _motor.SetActiveMovementStyle(_entry.holdStyle);
        }

        void EnterPatrol()
        {
            _state = NpcCombatState.Patrol;
            ReleaseCombatAim();
            _motor.SetActiveMovementStyle(_entry.patrolStyle);
            if (_entry.waypoints == null || _entry.waypoints.Length == 0)
                EnterIdle();
        }

        void EnterAlert()
        {
            _state = NpcCombatState.Alert;
            ReleaseCombatAim();
            _alertTimer = _entry.alertSeconds;
            NpcSteer.Stop(_motor);
            _motor.SetActiveMovementStyle(_entry.holdStyle);
            _emote?.SetAlertSpotted();
        }

        void EnterChase()
        {
            _state = NpcCombatState.Chase;
            ReleaseCombatAim();
            _motor.SetActiveMovementStyle(_entry.chaseStyle);
            if (_contact == SenseContactChannel.Hearing)
                _emote?.SetAlertSuspicious();
            else
                _emote?.ClearCombat();
        }

        void EnterAttack()
        {
            _state = NpcCombatState.Attack;
            SetAimHeld(true);
            AimAtTarget(_target);
            _motor.SetActiveMovementStyle(_entry.holdStyle);
        }

        void EnterReturn()
        {
            _state = NpcCombatState.Return;
            ReleaseCombatAim();
            _motor.SetActiveMovementStyle(_entry.patrolStyle);
            NpcSteer.TryArriveOrSteer(
                _motor,
                _transform.position,
                _homePosition,
                ResolveStoppingDistance(_entry.patrolStyle));
        }

        void EnterDead()
        {
            _state = NpcCombatState.Dead;
            ReleaseCombatAim();
            NpcSteer.Stop(_motor);
            _motor.SetActiveMovementStyle(_entry.holdStyle);
            _motor.SetDesiredWorldDir(Vector3.zero);
            _emote?.ClearCombat();
        }

        void OnDefeatChanged()
        {
            if (_defeat != null && _defeat.IsDefeated)
                EnterDead();
        }

        void AdvancePatrolIndex()
        {
            Transform[] waypoints = _entry.waypoints;
            if (waypoints == null || waypoints.Length == 0)
                return;
            _patrolIndex = (_patrolIndex + 1) % waypoints.Length;
        }

        void ApplyAimPreference()
        {
            if (_attacker == null)
                return;

            _attacker.SetPreferredPart(
                _entry.suppressMode ? BodyPartIds.LegL : BodyPartIds.Torso);
        }
    }
}
