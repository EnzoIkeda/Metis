using System;
using System.Collections.Generic;
using UnityEngine;

// Som de um momento do jogo: variacoes sorteadas sem repetir a ultima, com volume e limites proprios.
[Serializable]
public class SoundEffectEntry
{
    [SerializeField] private SoundEffect _effect;
    [SerializeField] private AudioClip[] _clips;
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;

    [Tooltip("Corta o som depois desse tempo, com um fade curto. 0 toca o clipe inteiro.")]
    [SerializeField] private float _maxDuration;

    [Tooltip("Ignora novos pedidos do mesmo som dentro desse intervalo, em segundos.")]
    [SerializeField] private float _minInterval;

    public SoundEffect Effect => _effect;
    public IReadOnlyList<AudioClip> Clips => _clips;
    public float Volume => _volume;
    public float MaxDuration => _maxDuration;
    public float MinInterval => _minInterval;
}

// Mapa de momento do jogo para som; trocar um som e so trocar o clipe neste asset.
[CreateAssetMenu(fileName = "New Sound Effect Library", menuName = "Metis/Sound Effect Library")]
public class SoundEffectLibraryData : ScriptableObject
{
    [SerializeField] private SoundEffectEntry[] _entries;

    public IReadOnlyList<SoundEffectEntry> Entries => _entries;

    public SoundEffectEntry Find(SoundEffect effect)
    {
        if (_entries == null)
            return null;

        foreach (var entry in _entries)
        {
            if (entry != null && entry.Effect == effect)
                return entry;
        }
        return null;
    }
}
