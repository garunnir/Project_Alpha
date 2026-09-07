// ============================================================
// CharacterBodyRefs — 본체 스폰 시 1회 트리 resolve·캐시 (GetInBody SSOT)
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterBodyRoot))]
public sealed class CharacterBodyRefs : MonoBehaviour
{
    readonly Dictionary<Type, Component> _byExactType = new(32);

    bool _resolved;

    public bool IsResolved => _resolved;

    void Awake() => ResolveFromHierarchy();

    /// <summary>스폰(inactive) 직후·SetActive 전에 Factory가 호출. 이후 GetInBody는 캐시만 조회.</summary>
    public void ResolveFromHierarchy()
    {
        if (_resolved)
            return;

        _byExactType.Clear();
        Component[] components = GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            Type type = component.GetType();
            if (!_byExactType.ContainsKey(type))
                _byExactType[type] = component;
        }

        _resolved = true;
    }

    public void Invalidate() => _resolved = false;

    /// <summary>inactive 스폰 직후 SetActive 전. Awake 이전에 캐시를 채운다. 프리팹 루트에 미리 있어야 함.</summary>
    public static CharacterBodyRefs EnsureResolved(GameObject instance)
    {
        if (instance == null)
            return null;

        CharacterBodyRefs refs = instance.GetComponentInParent<CharacterBodyRefs>();
        if (refs == null)
            instance.TryGetComponent(out refs);

        if (refs == null)
        {
            Debug.LogError(
                $"[CharacterBodyRefs] '{instance.name}' needs CharacterBodyRefs on the prefab root.",
                instance);
            return null;
        }

        refs.ResolveFromHierarchy();
        return refs;
    }

    public bool TryGet<T>(out T component) where T : Component
    {
        if (!_resolved)
            ResolveFromHierarchy();

        component = null;
        if (!_byExactType.TryGetValue(typeof(T), out Component cached))
            return false;

        component = cached as T;
        return component != null;
    }

    public T Get<T>() where T : Component
    {
        TryGet(out T component);
        return component;
    }

    public CharacterState State => Get<CharacterState>();
    public CharacterMotor Motor => Get<CharacterMotor>();
    public Collider BodyCollider => Get<CapsuleCollider>() as Collider ?? Get<Collider>();
    public CharacterDefinitionBinder DefinitionBinder => Get<CharacterDefinitionBinder>();
    public CharacterBodyHost BodyHost => Get<CharacterBodyHost>();
    public CharacterSkillsHost SkillsHost => Get<CharacterSkillsHost>();
    public CharacterTraitsHost TraitsHost => Get<CharacterTraitsHost>();
    public CharacterAttacker Attacker => Get<CharacterAttacker>();
    public CharacterActionHost ActionHost => Get<CharacterActionHost>();
    public CharacterPainHost PainHost => Get<CharacterPainHost>();
    public CharacterVision Vision => Get<CharacterVision>();
    public CharacterHearing Hearing => Get<CharacterHearing>();
    public CharacterAppearanceHost Appearance => Get<CharacterAppearanceHost>();
    public CharacterSightFadeHost SightFade => Get<CharacterSightFadeHost>();
    public PlayerGearHost GearHost => Get<PlayerGearHost>();
    public CharacterFactionHost FactionHost => Get<CharacterFactionHost>();
    public CharacterFootprintHost FootprintHost => Get<CharacterFootprintHost>();
    public CharacterLocomotionAnim LocomotionAnim => Get<CharacterLocomotionAnim>();
    public CharacterVaultHost VaultHost => Get<CharacterVaultHost>();
    public CharacterSessionHub SessionHub => Get<CharacterSessionHub>();
}
