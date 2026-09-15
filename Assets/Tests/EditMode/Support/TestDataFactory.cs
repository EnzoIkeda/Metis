using System;
using System.Reflection;
using UnityEngine;

// Constroi CardData/RandomEventData/StructureData pra teste via reflection, ja que os campos sao
// [SerializeField] private sem setter publico (por padrao de codigo do projeto). So pra EditMode.
public static class TestDataFactory
{
    public static CardData CreateCard(
        string name = "TestCard",
        CardTier tier = CardTier.Basica,
        CardArchetype archetype = CardArchetype.Geral,
        float requiredPesquisa = 0f,
        float cost = 0f,
        StatModifier[] statEffects = null,
        StructureData structureToPlace = null)
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.name = name;
        SetField(card, "_tier", tier);
        SetField(card, "_archetype", archetype);
        SetField(card, "_requiredPesquisa", requiredPesquisa);
        SetField(card, "_cost", cost);
        SetField(card, "_statEffects", statEffects ?? new StatModifier[0]);
        SetField(card, "_structureToPlace", structureToPlace);
        return card;
    }

    public static RandomEventData CreateEvent(
        string name = "TestEvent",
        int minTurn = 1,
        int maxTurn = 999,
        TriggerCondition[] triggerConditions = null,
        StatModifier[] statEffects = null)
    {
        var eventData = ScriptableObject.CreateInstance<RandomEventData>();
        eventData.name = name;
        SetField(eventData, "_minTurn", minTurn);
        SetField(eventData, "_maxTurn", maxTurn);
        SetField(eventData, "_triggerConditions", triggerConditions);
        SetField(eventData, "_statEffects", statEffects ?? new StatModifier[0]);
        return eventData;
    }

    public static StructureData CreateStructure(string name = "TestStructure")
    {
        var structure = ScriptableObject.CreateInstance<StructureData>();
        structure.name = name;
        return structure;
    }

    private static void SetField<T>(UnityEngine.Object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }
}
