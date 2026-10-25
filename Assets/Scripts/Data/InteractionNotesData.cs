using System;
using UnityEngine;

// Explicacao de uma relacao do mapa: por que a fonte influencia o alvo.
[Serializable]
public struct InteractionNote
{
    public CityParameterType Source;
    public CityParameterType Target;

    [TextArea(2, 5)] public string TextPt;
    [TextArea(2, 5)] public string TextEn;

    // Referencia de onde veio o lastro, vazia quando a relacao nao cita fonte.
    public string Reference;
}

// Textos de lastro das relacoes entre parametros, mostrados no mapa de relacoes.
[CreateAssetMenu(fileName = "InteractionNotes", menuName = "Metis/Interaction Notes")]
public class InteractionNotesData : ScriptableObject
{
    [SerializeField] private InteractionNote[] _notes = Array.Empty<InteractionNote>();

    public bool TryGetNote(CityParameterType source, CityParameterType target, out InteractionNote note)
    {
        foreach (var entry in _notes)
        {
            if (entry.Source == source && entry.Target == target)
            {
                note = entry;
                return true;
            }
        }
        note = default;
        return false;
    }

    public string GetText(CityParameterType source, CityParameterType target)
    {
        if (TryGetNote(source, target, out var note) == false)
            return string.Empty;
        return LocalizationManager.Current == Language.English && string.IsNullOrEmpty(note.TextEn) == false
            ? note.TextEn
            : note.TextPt;
    }
}
