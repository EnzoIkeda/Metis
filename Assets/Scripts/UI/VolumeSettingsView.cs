using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Controles deslizantes de volume de Musica e Efeitos, usados no menu principal e na pausa.
public class VolumeSettingsView : MonoBehaviour
{
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private TMP_Text _musicLabel;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private TMP_Text _sfxLabel;

    private void Awake()
    {
        if (_musicSlider != null)
            _musicSlider.onValueChanged.AddListener(HandleMusicChanged);
        if (_sfxSlider != null)
            _sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += RefreshLabels;

        if (_musicSlider != null)
            _musicSlider.SetValueWithoutNotify(SoundSettings.MusicVolume);
        if (_sfxSlider != null)
            _sfxSlider.SetValueWithoutNotify(SoundSettings.SfxVolume);
        RefreshLabels();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshLabels;
    }

    private void HandleMusicChanged(float value)
    {
        SoundSettings.MusicVolume = value;
        RefreshLabels();
    }

    private void HandleSfxChanged(float value)
    {
        SoundSettings.SfxVolume = value;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (_musicLabel != null)
            _musicLabel.text = UIStrings.VolumeLabel(UIStrings.SettingsMusicVolume, SoundSettings.MusicVolume);
        if (_sfxLabel != null)
            _sfxLabel.text = UIStrings.VolumeLabel(UIStrings.SettingsSfxVolume, SoundSettings.SfxVolume);
    }
}
