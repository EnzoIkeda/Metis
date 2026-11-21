using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Mostra uma sequencia de telas ilustradas com fusao, leve aproximacao e legenda numa faixa embaixo.
// Toque em qualquer lugar completa a fusao ou avanca; o botao de pular encerra. Anda em tempo real.
public class SlideSequenceView : MonoBehaviour, IPointerClickHandler
{
    private const float SlideWidth = 1080f;
    private const float SlideHeight = 1920f;
    private const float MinCaptionBand = 300f;
    private const float OverlapBandAlpha = 0.85f;
    private const float BandOverlap = 6f;

    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private RectTransform _slideArea;
    [SerializeField] private Image _backImage;
    [SerializeField] private Image _frontImage;
    [SerializeField] private Image _captionBand;
    [SerializeField] private TMP_Text _captionText;
    [SerializeField] private Button _skipButton;
    [SerializeField] private TMP_Text _skipLabel;

    // Uma imagem por tela e a cor da borda de cada uma, que pinta a faixa da legenda.
    [SerializeField] private Sprite[] _slides = Array.Empty<Sprite>();
    [SerializeField] private Color[] _borderColors = Array.Empty<Color>();

    private SlideSequence _sequence;
    private Func<string, string> _captionOf;
    private int _shownIndex = -1;
    private float _lastCanvasHeight = -1f;

    public event Action Finished;

    public SlideSequence Sequence => _sequence;
    public IReadOnlyList<Sprite> Slides => _slides;
    public bool IsPlaying => _sequence != null && _sequence.IsFinished == false;
    public string CurrentCaption => _captionText != null ? _captionText.text : string.Empty;

    private void Awake()
    {
        if (_skipButton != null)
            _skipButton.onClick.AddListener(Skip);
    }

    public void Play(IEnumerable<SlideStep> steps, Func<string, string> captionOf)
    {
        _sequence = new SlideSequence(steps);
        _captionOf = captionOf;
        _shownIndex = -1;
        if (_skipLabel != null)
            _skipLabel.text = UIStrings.SkipButton;
        if (_backImage != null)
            _backImage.enabled = false;
        LayoutForCanvas();
        Refresh();
        if (_sequence.IsFinished)
            Finish();
    }

    public void Tap()
    {
        if (IsPlaying == false)
            return;
        _sequence.Tap();
        AfterChange();
    }

    public void Skip()
    {
        if (IsPlaying == false)
            return;
        _sequence.Skip();
        AfterChange();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Tap();
    }

    private void Update()
    {
        if (IsPlaying == false)
            return;
        if (_canvasRect != null && Mathf.Approximately(_canvasRect.rect.height, _lastCanvasHeight) == false)
            LayoutForCanvas();
        _sequence.Tick(Time.unscaledDeltaTime);
        AfterChange();
    }

    private void AfterChange()
    {
        if (_sequence.IsFinished)
        {
            Finish();
            return;
        }
        Refresh();
    }

    private void Finish()
    {
        enabled = false;
        Finished?.Invoke();
    }

    private void Refresh()
    {
        if (_sequence == null || _sequence.IsFinished)
            return;

        var step = _sequence.Current;
        if (_sequence.CurrentIndex != _shownIndex)
        {
            // A tela que estava na frente passa para tras e a nova entra por cima, ainda transparente.
            if (_shownIndex >= 0 && _backImage != null && _frontImage != null)
            {
                _backImage.sprite = _frontImage.sprite;
                _backImage.rectTransform.localScale = _frontImage.rectTransform.localScale;
                _backImage.enabled = _backImage.sprite != null;
            }
            if (_frontImage != null)
                _frontImage.sprite = SlideAt(step.ImageIndex);
            if (_captionText != null)
                _captionText.text = _captionOf != null ? _captionOf(step.CaptionKey) : step.CaptionKey;
            PaintBand(step.ImageIndex);
            _shownIndex = _sequence.CurrentIndex;
        }

        var fade = _sequence.FadeProgress;
        if (_frontImage != null)
        {
            _frontImage.color = new Color(1f, 1f, 1f, fade);
            _frontImage.rectTransform.localScale = Vector3.one * _sequence.Zoom;
        }
        if (_captionText != null)
            _captionText.alpha = fade;
    }

    private Sprite SlideAt(int index)
    {
        return index >= 0 && index < _slides.Length ? _slides[index] : null;
    }

    // A tela fica inteira; o que sobra embaixo vira a faixa da legenda. Sem sobra, a faixa fica por cima da tela.
    private void LayoutForCanvas()
    {
        if (_canvasRect == null || _slideArea == null)
            return;
        var height = _canvasRect.rect.height;
        var width = _canvasRect.rect.width;
        _lastCanvasHeight = height;
        var scale = Mathf.Min(1f, height / SlideHeight, width / SlideWidth);
        _slideArea.sizeDelta = new Vector2(SlideWidth * scale, SlideHeight * scale);

        if (_captionBand != null)
        {
            // A faixa sobe um pouco por cima da tela para esconder a emenda da borda da imagem.
            var spare = height - SlideHeight * scale + BandOverlap;
            var band = _captionBand.rectTransform;
            band.sizeDelta = new Vector2(band.sizeDelta.x, Mathf.Max(MinCaptionBand, spare));
        }
        if (_shownIndex >= 0)
            PaintBand(_sequence.Current.ImageIndex);
    }

    private bool BandOverlapsSlide => _canvasRect != null && _canvasRect.rect.height - SlideHeight < MinCaptionBand;

    private void PaintBand(int imageIndex)
    {
        if (_captionBand == null)
            return;
        var color = imageIndex >= 0 && imageIndex < _borderColors.Length ? _borderColors[imageIndex] : Color.black;
        color.a = BandOverlapsSlide ? OverlapBandAlpha : 1f;
        _captionBand.color = color;
        if (_captionText != null)
        {
            // Letra escura em faixa clara e clara em faixa escura.
            var luminance = 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
            var textColor = luminance > 0.6f ? new Color(0.12f, 0.12f, 0.16f) : Color.white;
            textColor.a = _captionText.alpha;
            _captionText.color = textColor;
        }
    }
}
