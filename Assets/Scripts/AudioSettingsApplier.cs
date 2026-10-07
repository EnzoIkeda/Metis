using UnityEngine;
using UnityEngine.Audio;

// Aplica os volumes salvos nos grupos do mixer, na inicializacao e a cada mudanca nas configuracoes.
public class AudioSettingsApplier : MonoBehaviour
{
    public const string MusicVolumeParameter = "MusicVolume";
    public const string SfxVolumeParameter = "SfxVolume";

    [SerializeField] private AudioMixer _mixer;

    // SetFloat chamado no Awake e ignorado pelo mixer, por isso a primeira aplicacao fica no Start.
    private void Start()
    {
        SoundSettings.OnChanged += Apply;
        Apply();
    }

    private void OnDestroy()
    {
        SoundSettings.OnChanged -= Apply;
    }

    private void Apply()
    {
        if (_mixer == null)
            return;

        _mixer.SetFloat(MusicVolumeParameter, VolumeConversion.LinearToDecibels(SoundSettings.MusicVolume));
        _mixer.SetFloat(SfxVolumeParameter, VolumeConversion.LinearToDecibels(SoundSettings.SfxVolume));
    }
}
