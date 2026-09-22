using System;
using System.Reflection;
using UnityEngine;

// Constroi CardData/MusicPlaylistData de teste via reflection, ja que os campos sao [SerializeField] private sem setter publico. So pra PlayMode.
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
