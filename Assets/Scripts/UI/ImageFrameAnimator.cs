using UnityEngine;
using UnityEngine.UI;

// Anima uma imagem de UI trocando o sprite por quadros, em tempo real pra seguir rodando com o jogo pausado.
[RequireComponent(typeof(Image))]
public class ImageFrameAnimator : MonoBehaviour
{
    [SerializeField] private Image _image;

    private Sprite[] _frames;
    private float _framesPerSecond;
    private float _startTime;

    private void Awake()
    {
        if (_image == null)
            _image = GetComponent<Image>();
    }

    // Sem quadros, para a animacao e deixa o sprite atual.
    public void Play(Sprite[] frames, float framesPerSecond)
    {
        if (frames == null || frames.Length == 0)
        {
            Stop();
            return;
        }

        _frames = frames;
        _framesPerSecond = framesPerSecond;
        _startTime = Time.unscaledTime;
        ShowFrame(0);
    }

    public void Stop()
    {
        _frames = null;
    }

    private void Update()
    {
        if (_frames == null)
            return;

        ShowFrame(SpriteFrameTiming.FrameIndex(Time.unscaledTime - _startTime, _framesPerSecond, _frames.Length));
    }

    private void ShowFrame(int index)
    {
        if (_image != null && index >= 0)
            _image.sprite = _frames[index];
    }
}
