using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

public class AudioSettingsApplierTests
{
    private const string PrefsKey = "Metis.Sound";
    private const string MixerPath = "Assets/Audio/MetisMixer.mixer";

    private GameObject _host;
    private string _savedPrefs;

    private static void ResetSoundSettingsCache()
    {
        typeof(SoundSettings).GetField("_state", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
    }

    [SetUp]
    public void SetUp()
    {
        // Preferencias de verdade do jogador ficam guardadas e voltam no fim do teste.
        _savedPrefs = PlayerPrefs.GetString(PrefsKey, null);
        PlayerPrefs.DeleteKey(PrefsKey);
        ResetSoundSettingsCache();
    }

    [TearDown]
    public void TearDown()
    {
        if (_host != null)
            Object.DestroyImmediate(_host);

        if (string.IsNullOrEmpty(_savedPrefs))
            PlayerPrefs.DeleteKey(PrefsKey);
        else
            PlayerPrefs.SetString(PrefsKey, _savedPrefs);
        ResetSoundSettingsCache();
    }

    private static AudioMixer LoadMixer()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
#else
        return null;
#endif
    }

    [UnityTest]
    public IEnumerator Start_AppliesSavedVolumes_AndFollowsLaterChanges()
    {
        var mixer = LoadMixer();
        if (mixer == null)
            Assert.Ignore("Mixer so e carregavel pelo AssetDatabase dentro do Editor.");

        SoundSettings.MusicVolume = 0.5f;
        SoundSettings.SfxVolume = 0f;

        _host = new GameObject("AudioSettingsApplier");
        _host.SetActive(false);
        var applier = _host.AddComponent<AudioSettingsApplier>();
        TestDataFactory.SetField(applier, "_mixer", mixer);
        _host.SetActive(true);

        // Start so roda no proximo ciclo de Update, entao espera ate o volume chegar no mixer.
        var frames = 0;
        float music;
        while ((mixer.GetFloat(AudioSettingsApplier.MusicVolumeParameter, out music) == false || Mathf.Abs(music - VolumeConversion.LinearToDecibels(0.5f)) > 0.01f) && frames < 10)
        {
            frames++;
            yield return null;
        }

        Assert.That(music, Is.EqualTo(VolumeConversion.LinearToDecibels(0.5f)).Within(0.01f));
        mixer.GetFloat(AudioSettingsApplier.SfxVolumeParameter, out var sfx);
        Assert.That(sfx, Is.EqualTo(VolumeConversion.SilenceDecibels).Within(0.01f));

        SoundSettings.SfxVolume = 1f;

        mixer.GetFloat(AudioSettingsApplier.SfxVolumeParameter, out sfx);
        Assert.That(sfx, Is.EqualTo(0f).Within(0.01f));
    }
}
