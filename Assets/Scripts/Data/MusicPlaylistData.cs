using System.Collections.Generic;
using UnityEngine;

// Lista de faixas de musica de fundo, trocavel via asset sem precisar editar codigo.
[CreateAssetMenu(fileName = "New Music Playlist", menuName = "Metis/Music Playlist")]
public class MusicPlaylistData : ScriptableObject
{
    [SerializeField] private AudioClip[] _tracks;

    public IReadOnlyList<AudioClip> Tracks => _tracks;
}
