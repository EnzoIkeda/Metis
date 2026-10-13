using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class ImageFrameAnimatorTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (var created in _created)
            if (created != null)
                Object.DestroyImmediate(created);
        _created.Clear();
    }

    private Sprite[] CreateFrames(int count)
    {
        var frames = new Sprite[count];
        for (var i = 0; i < count; i++)
        {
            var texture = new Texture2D(2, 2);
            frames[i] = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            frames[i].name = $"Frame{i}";
            _created.Add(texture);
            _created.Add(frames[i]);
        }
        return frames;
    }

    private (Image image, ImageFrameAnimator animator) CreateAnimator()
    {
        var host = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
        _created.Add(host);
        var animator = host.AddComponent<ImageFrameAnimator>();
        return (host.GetComponent<Image>(), animator);
    }

    // Espera em tempo real ate o sprite mudar, com limite folgado pra nao depender da taxa de quadros.
    private static IEnumerator WaitForSpriteChange(Image image, Sprite from, float timeout)
    {
        var start = Time.realtimeSinceStartup;
        while (image.sprite == from && Time.realtimeSinceStartup - start < timeout)
            yield return null;
    }

    [UnityTest]
    public IEnumerator Play_ShowsFirstFrameAndAdvances()
    {
        var frames = CreateFrames(3);
        var (image, animator) = CreateAnimator();
        yield return null;

        animator.Play(frames, 10f);
        Assert.That(image.sprite, Is.EqualTo(frames[0]));

        yield return WaitForSpriteChange(image, frames[0], 2f);
        Assert.That(image.sprite, Is.Not.EqualTo(frames[0]));
        Assert.That(frames, Has.Member(image.sprite));
    }

    [UnityTest]
    public IEnumerator Play_WhilePausedWithZeroTimeScale_StillAdvances()
    {
        var frames = CreateFrames(3);
        var (image, animator) = CreateAnimator();
        yield return null;

        Time.timeScale = 0f;
        animator.Play(frames, 10f);

        yield return WaitForSpriteChange(image, frames[0], 2f);
        Assert.That(image.sprite, Is.Not.EqualTo(frames[0]));
    }

    [UnityTest]
    public IEnumerator Play_WithoutFrames_KeepsCurrentSpriteAndStops()
    {
        var frames = CreateFrames(3);
        var stillArt = CreateFrames(1)[0];
        var (image, animator) = CreateAnimator();
        yield return null;

        animator.Play(frames, 10f);
        image.sprite = stillArt;
        animator.Play(null, 10f);

        var start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 0.5f)
            yield return null;
        Assert.That(image.sprite, Is.EqualTo(stillArt));
    }
}
