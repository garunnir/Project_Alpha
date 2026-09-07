// ============================================================
// CharacterFactory — CharacterDefinition 프리팹 Instantiate + Apply
// ============================================================

using UnityEngine;

public static class CharacterFactory
{
    public static GameObject Instantiate(
        CharacterDefinition definition,
        Vector3 position,
        Transform parent = null,
        bool useGameplayDataOwner = false)
    {
        GameObject instance = InstantiateInactive(definition, position, parent, useGameplayDataOwner);
        if (instance == null)
            return null;

        instance.SetActive(true);
        return instance;
    }

    public static GameObject InstantiateInactive(
        CharacterDefinition definition,
        Vector3 position,
        Transform parent = null,
        bool useGameplayDataOwner = false)
    {
        if (definition == null)
        {
            Debug.LogError("[CharacterFactory] definition is null.");
            return null;
        }

        if (definition.Prefab == null)
        {
            Debug.LogError($"[CharacterFactory] Prefab missing on definition '{definition.name}'.");
            return null;
        }

        GameObject template = definition.Prefab;
        bool wasActive = template.activeSelf;
        if (wasActive)
            template.SetActive(false);

        GameObject instance = Object.Instantiate(template, position, Quaternion.identity, parent);

        if (wasActive)
            template.SetActive(true);

        if (instance == null)
            return null;

        if (useGameplayDataOwner)
            CharacterGameplayDataConfigurator.ConfigureAsGameplayDataOwner(instance);

        CharacterBodyRefs.EnsureResolved(instance);

        CharacterDefinitionBinder binder = instance.GetComponent<CharacterDefinitionBinder>();
        if (binder == null)
        {
            Debug.LogError(
                $"[CharacterFactory] '{definition.name}' prefab needs CharacterDefinitionBinder.",
                instance);
            Object.Destroy(instance);
            return null;
        }

        binder.Apply(definition);
        return instance;
    }
}
