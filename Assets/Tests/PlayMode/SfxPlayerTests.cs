using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SfxPlayerTests
{
    private readonly List<GameObject> _hosts = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var host in _hosts)
            if (host != null)
                Object.DestroyImmediate(host);
        _hosts.Clear();
    }

    private SfxPlayer CreatePlayer(SoundEffectLibraryData library)
    {
        var host = new GameObject("SfxPlayer");
        _hosts.Add(host);
        host.SetActive(false);
        var player = host.AddComponent<SfxPlayer>();
        TestDataFactory.SetField(player, "_library", library);
        host.SetActive(true);
        return player;
    }

    // isPlaying nao e confiavel com clipe procedural nos testes; o sinal usado e o clipe atribuido.
    private static int SourcesWith(SfxPlayer player, AudioClip clip)
    {
        return player.GetComponentsInChildren<AudioSource>().Count(source => source.clip == clip);
    }

    [Test]
    public void Play_WithoutInstance_DoesNothing()
    {
        Assert.That(SfxPlayer.Instance, Is.Null);
        Assert.DoesNotThrow(() => SfxPlayer.Play(SoundEffect.ButtonClick));
    }

    [UnityTest]
    public IEnumerator Play_AssignsTheConfiguredClipToASource()
    {
        var click = TestDataFactory.CreateSilentClip("Click", 0.2f);
        var player = CreatePlayer(TestDataFactory.CreateSoundLibrary((SoundEffect.ButtonClick, click, 0f, 0f)));
        yield return null;

        SfxPlayer.Play(SoundEffect.ButtonClick);

        Assert.That(SourcesWith(player, click), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Play_EffectMissingFromLibrary_DoesNothing()
    {
        var click = TestDataFactory.CreateSilentClip("Click", 0.2f);
        var player = CreatePlayer(TestDataFactory.CreateSoundLibrary((SoundEffect.ButtonClick, click, 0f, 0f)));
        yield return null;

        Assert.DoesNotThrow(() => SfxPlayer.Play(SoundEffect.Defeat));
        Assert.That(player.GetComponentsInChildren<AudioSource>().All(source => source.clip == null), Is.True);
    }

    [UnityTest]
    public IEnumerator DuplicateInstance_DoesNotReplaceTheFirst()
    {
        var library = TestDataFactory.CreateSoundLibrary();
        var first = CreatePlayer(library);
        var second = CreatePlayer(library);
        yield return null;

        Assert.That(SfxPlayer.Instance, Is.SameAs(first));
        Assert.That(second, Is.Not.SameAs(first));
    }

    [UnityTest]
    public IEnumerator Play_WithinMinInterval_IsIgnored()
    {
        var alarm = TestDataFactory.CreateSilentClip("Alarm", 1f);
        var click = TestDataFactory.CreateSilentClip("Click", 0.2f);
        var player = CreatePlayer(TestDataFactory.CreateSoundLibrary(
            (SoundEffect.ParameterCollapse, alarm, 0f, 5f),
            (SoundEffect.ButtonClick, click, 0f, 0f)));
        yield return null;

        SfxPlayer.Play(SoundEffect.ParameterCollapse);
        SfxPlayer.Play(SoundEffect.ParameterCollapse);
        SfxPlayer.Play(SoundEffect.ButtonClick);
        SfxPlayer.Play(SoundEffect.ButtonClick);

        Assert.That(SourcesWith(player, alarm), Is.EqualTo(1), "o segundo alarme cai dentro do intervalo minimo");
        Assert.That(SourcesWith(player, click), Is.EqualTo(2), "sem intervalo os sons se sobrepoem em fontes diferentes");
    }

    // O corte usa tempo real: com o jogo pausado o som ainda termina no tempo configurado.
    [UnityTest]
    public IEnumerator Play_WithMaxDuration_IsCutEvenWhilePaused()
    {
        var defeat = TestDataFactory.CreateSilentClip("Defeat", 3f);
        var player = CreatePlayer(TestDataFactory.CreateSoundLibrary((SoundEffect.Defeat, defeat, 0.3f, 0f)));
        yield return null;

        Time.timeScale = 0f;
        try
        {
            SfxPlayer.Play(SoundEffect.Defeat);
            var cuts = (System.Collections.IDictionary)typeof(SfxPlayer)
                .GetField("_cutRoutines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(player);
            Assert.That(cuts.Count, Is.EqualTo(1));

            var start = Time.realtimeSinceStartup;
            while (cuts.Count > 0 && Time.realtimeSinceStartup - start < 2f)
                yield return null;

            Assert.That(cuts.Count, Is.EqualTo(0), "o corte terminou mesmo com timeScale 0");
            Assert.That(Time.realtimeSinceStartup - start, Is.LessThan(1f));
        }
        finally
        {
            Time.timeScale = 1f;
        }
    }

    // Os efeitos visuais depois de jogar uma carta tocam o som correspondente junto.
    [UnityTest]
    public IEnumerator ImpactEffect_PlaysTheCardImpactSound()
    {
        var impact = TestDataFactory.CreateSilentClip("Impact", 0.5f);
        var player = CreatePlayer(TestDataFactory.CreateSoundLibrary((SoundEffect.CardImpact, impact, 0f, 0f)));
        var effectsHost = new GameObject("Effects");
        _hosts.Add(effectsHost);
        var effects = effectsHost.AddComponent<CityEffectsController>();
        yield return null;

        effects.PlayImpactGlowing(null);

        Assert.That(SourcesWith(player, impact), Is.EqualTo(1));
    }
}
