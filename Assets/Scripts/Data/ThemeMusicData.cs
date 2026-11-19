using UnityEngine;

// Faixas proprias das sequencias de abertura e de fim de rodada e da crise. Campo vazio mantem a trilha de fundo.
[CreateAssetMenu(fileName = "ThemeMusic", menuName = "Metis/Theme Music")]
public class ThemeMusicData : ScriptableObject
{
    [SerializeField] private AudioClip _opening;
    [SerializeField] private AudioClip _defeat;
    [SerializeField] private AudioClip _victory;
    [SerializeField] private AudioClip _crisis;

    public AudioClip Opening => _opening;
    public AudioClip Defeat => _defeat;
    public AudioClip Victory => _victory;
    public AudioClip Crisis => _crisis;
}
