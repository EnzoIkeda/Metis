using System;
using System.Collections.Generic;

// Uma tela da sequencia: qual imagem, qual legenda e quanto tempo fica.
public readonly struct SlideStep
{
    public SlideStep(int imageIndex, string captionKey, float duration)
    {
        ImageIndex = imageIndex;
        CaptionKey = captionKey ?? string.Empty;
        Duration = duration;
    }

    public int ImageIndex { get; }
    public string CaptionKey { get; }
    public float Duration { get; }
}

// Sequencia de telas com fusao entre elas. O tempo entra de fora (tempo real), entao pausa nao afeta.
// Toque no meio da fusao completa a fusao; toque com a tela parada avanca; pular encerra.
public class SlideSequence
{
    public const float FadeDuration = 0.4f;
    public const float ZoomStart = 1f;
    public const float ZoomEnd = 1.05f;

    private readonly List<SlideStep> _steps;

    public SlideSequence(IEnumerable<SlideStep> steps)
    {
        _steps = new List<SlideStep>();
        if (steps != null)
        {
            // A tela precisa durar pelo menos a fusao, senao nunca seria vista inteira.
            foreach (var step in steps)
                _steps.Add(new SlideStep(step.ImageIndex, step.CaptionKey, Math.Max(step.Duration, FadeDuration)));
        }
        IsFinished = _steps.Count == 0;
    }

    public IReadOnlyList<SlideStep> Steps => _steps;
    public int CurrentIndex { get; private set; }
    public float ElapsedInStep { get; private set; }
    public bool IsFinished { get; private set; }
    public bool WasSkipped { get; private set; }

    public SlideStep Current => _steps.Count == 0 ? default : _steps[Math.Min(CurrentIndex, _steps.Count - 1)];

    // 0 no comeco da tela, 1 com a fusao completa; a tela anterior some na mesma proporcao.
    public float FadeProgress => IsFinished ? 1f : Math.Clamp(ElapsedInStep / FadeDuration, 0f, 1f);

    public bool IsFading => IsFinished == false && ElapsedInStep < FadeDuration;

    // Leve aproximacao ao longo da tela.
    public float Zoom
    {
        get
        {
            if (_steps.Count == 0)
                return ZoomStart;
            var t = IsFinished ? 1f : Math.Clamp(ElapsedInStep / Current.Duration, 0f, 1f);
            return ZoomStart + (ZoomEnd - ZoomStart) * t;
        }
    }

    public void Tick(float deltaTime)
    {
        if (IsFinished || deltaTime <= 0f || float.IsNaN(deltaTime))
            return;

        ElapsedInStep += deltaTime;
        while (IsFinished == false && ElapsedInStep >= Current.Duration)
        {
            var leftover = ElapsedInStep - Current.Duration;
            Advance();
            ElapsedInStep = IsFinished ? 0f : leftover;
        }
    }

    public void Tap()
    {
        if (IsFinished)
            return;
        if (IsFading)
            ElapsedInStep = FadeDuration;
        else
            Advance();
    }

    public void Skip()
    {
        if (IsFinished)
            return;
        WasSkipped = true;
        IsFinished = true;
        ElapsedInStep = 0f;
    }

    private void Advance()
    {
        if (CurrentIndex + 1 >= _steps.Count)
        {
            IsFinished = true;
            ElapsedInStep = 0f;
            return;
        }
        CurrentIndex++;
        ElapsedInStep = 0f;
    }
}
