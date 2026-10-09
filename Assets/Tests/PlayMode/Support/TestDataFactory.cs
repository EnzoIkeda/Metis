using System;
using System.Reflection;
using UnityEngine;

// Monta assets de teste via reflection, ja que os campos sao privados e sem setter. So pra PlayMode.
public static class TestDataFactory
{
    public static CardData CreateCard(
        string name = "TestCard",
        CardTier tier = CardTier.Basica,
        CardArchetype archetype = CardArchetype.Geral,
        float requiredPesquisa = 0f,
        float cost = 0f,
        StatModifier[] statEffects = null,
        StructureData structureToPlace = null,
        CardAbility ability = CardAbility.None,
        int copies = 1)
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.name = name;
        SetField(card, "_tier", tier);
        SetField(card, "_archetype", archetype);
        SetField(card, "_requiredPesquisa", requiredPesquisa);
        SetField(card, "_cost", cost);
        SetField(card, "_statEffects", statEffects ?? new StatModifier[0]);
        SetField(card, "_structureToPlace", structureToPlace);
        SetField(card, "_ability", ability);
        SetField(card, "_copies", copies);
        return card;
    }

    public static RandomEventData CreateEvent(string name = "TestEvent", StatModifier[] statEffects = null)
    {
        var eventData = ScriptableObject.CreateInstance<RandomEventData>();
        eventData.name = name;
        SetField(eventData, "_title", name);
        SetField(eventData, "_minTurn", 1);
        SetField(eventData, "_maxTurn", 999);
        SetField(eventData, "_triggerConditions", new TriggerCondition[0]);
        SetField(eventData, "_statEffects", statEffects ?? new StatModifier[0]);
        return eventData;
    }

    public static void SetStaticField(Type type, string fieldName, object value)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
            throw new MissingFieldException(type.Name, fieldName);
        field.SetValue(null, value);
    }

    public static MusicPlaylistData CreatePlaylist(params AudioClip[] tracks)
    {
        var playlist = ScriptableObject.CreateInstance<MusicPlaylistData>();
        SetField(playlist, "_tracks", tracks);
        return playlist;
    }

    // Clipe silencioso curto, so pra exercitar o ciclo de vida real do AudioSource sem depender de asset de audio.
    public static AudioClip CreateSilentClip(string name, float lengthSeconds)
    {
        const int frequency = 8000;
        var clip = AudioClip.Create(name, Mathf.Max(1, Mathf.RoundToInt(frequency * lengthSeconds)), 1, frequency, false);
        return clip;
    }

    // Biblioteca de sons com uma entrada por efeito, campos privados preenchidos por reflection.
    public static SoundEffectLibraryData CreateSoundLibrary(params (SoundEffect effect, AudioClip clip, float maxDuration, float minInterval)[] entries)
    {
        var library = ScriptableObject.CreateInstance<SoundEffectLibraryData>();
        var built = new SoundEffectEntry[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = new SoundEffectEntry();
            SetObjectField(entry, "_effect", entries[i].effect);
            SetObjectField(entry, "_clips", new[] { entries[i].clip });
            SetObjectField(entry, "_volume", 1f);
            SetObjectField(entry, "_maxDuration", entries[i].maxDuration);
            SetObjectField(entry, "_minInterval", entries[i].minInterval);
            built[i] = entry;
        }
        SetField(library, "_entries", built);
        return library;
    }

    private static void SetObjectField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    public static void SetField<T>(UnityEngine.Object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    public static void InvokePrivateMethod(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        method.Invoke(target, null);
    }
}
