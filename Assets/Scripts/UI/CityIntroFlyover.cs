using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Sobrevoo da primeira fase de uma rodada nova, logo depois da abertura: a camera parte de perto e pousa
// no enquadramento da cena, com a interface escondida. Um toque pula para o fim; so entao vem as boas-vindas.
public class CityIntroFlyover : MonoBehaviour
{
    [SerializeField] private Camera _camera;

    [Tooltip("Zoom e arrasto da camera, desligados durante o sobrevoo.")]
    [SerializeField] private Behaviour[] _cameraControls = Array.Empty<Behaviour>();

    [Tooltip("HUD e mao de cartas: somem no sobrevoo e voltam com fusao no fim.")]
    [SerializeField] private CanvasGroup[] _hiddenGroups = Array.Empty<CanvasGroup>();

    [Tooltip("Painel transparente por cima de tudo: bloqueia a mao e aceita o toque para pular.")]
    [SerializeField] private GameObject _skipPanel;
    [SerializeField] private Button _skipButton;

    [SerializeField, Min(0f)] private float _duration = FlyoverPath.DefaultDuration;
    [SerializeField, Min(0.1f)] private float _startSize = FlyoverPath.DefaultStartSize;

    [Tooltip("Deslocamento inicial no chao, em unidades de mundo (direita e frente da camera).")]
    [SerializeField] private Vector2 _startPan = new Vector2(-3f, 2f);

    [SerializeField, Min(0f)] private float _revealDuration = 0.4f;

    private FlyoverPath _path;
    private float _elapsed;
    private bool[] _savedRaycasts = Array.Empty<bool>();
    private bool[] _savedInteractable = Array.Empty<bool>();

    public bool IsPlaying { get; private set; }

    // Pousou no enquadramento da cena, por tempo ou por toque.
    public event Action Finished;

    public CameraPose RestPose => _path != null ? _path.End : default;

    // No Awake, para quem decide no Start (as boas-vindas) ja saber se precisa esperar.
    private void Awake()
    {
        if (_skipPanel != null)
            _skipPanel.SetActive(false);
        if (RunIntroHandoff.ConsumeFlyover() == false || SessionMode.IsScenario || _camera == null)
            return;

        var cameraTransform = _camera.transform;
        var rest = cameraTransform.position;
        var right = Flat(cameraTransform.right);
        var forward = Flat(cameraTransform.forward);
        var start = rest + right * _startPan.x + forward * _startPan.y;
        _path = new FlyoverPath(
            new CameraPose(start.x, start.y, start.z, _startSize),
            new CameraPose(rest.x, rest.y, rest.z, _camera.orthographicSize),
            _duration);

        IsPlaying = true;
        foreach (var control in _cameraControls)
        {
            if (control != null)
                control.enabled = false;
        }
        HideInterface();
        if (_skipPanel != null)
            _skipPanel.SetActive(true);
        if (_skipButton != null)
            _skipButton.onClick.AddListener(Skip);
        Apply(_path.Start);
    }

    private void OnDestroy()
    {
        if (_skipButton != null)
            _skipButton.onClick.RemoveListener(Skip);
    }

    private void Update()
    {
        if (IsPlaying == false)
            return;

        // Tempo real: nada pausa o jogo aqui, mas o sobrevoo nao depende da escala de tempo.
        _elapsed += Time.unscaledDeltaTime;
        if (_path.IsFinished(_elapsed))
        {
            Finish();
            return;
        }
        Apply(_path.PoseAtTime(_elapsed));
    }

    public void Skip()
    {
        if (IsPlaying)
            Finish();
    }

    private void Finish()
    {
        IsPlaying = false;
        Apply(_path.End);
        foreach (var control in _cameraControls)
        {
            if (control != null)
                control.enabled = true;
        }
        if (_skipPanel != null)
            _skipPanel.SetActive(false);
        StartCoroutine(RevealInterface());
        Finished?.Invoke();
    }

    private void Apply(CameraPose pose)
    {
        _camera.transform.position = new Vector3(pose.X, pose.Y, pose.Z);
        _camera.orthographicSize = pose.Size;
    }

    private void HideInterface()
    {
        _savedRaycasts = new bool[_hiddenGroups.Length];
        _savedInteractable = new bool[_hiddenGroups.Length];
        for (var i = 0; i < _hiddenGroups.Length; i++)
        {
            var group = _hiddenGroups[i];
            if (group == null)
                continue;
            _savedRaycasts[i] = group.blocksRaycasts;
            _savedInteractable[i] = group.interactable;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }

    private IEnumerator RevealInterface()
    {
        // Os cliques voltam na hora; so a imagem entra aos poucos.
        for (var i = 0; i < _hiddenGroups.Length; i++)
        {
            var group = _hiddenGroups[i];
            if (group == null)
                continue;
            group.blocksRaycasts = _savedRaycasts[i];
            group.interactable = _savedInteractable[i];
        }

        for (var elapsed = 0f; elapsed < _revealDuration; elapsed += Time.unscaledDeltaTime)
        {
            SetAlpha(elapsed / _revealDuration);
            yield return null;
        }
        SetAlpha(1f);
    }

    private void SetAlpha(float alpha)
    {
        foreach (var group in _hiddenGroups)
        {
            if (group != null)
                group.alpha = alpha;
        }
    }

    private static Vector3 Flat(Vector3 direction)
    {
        return new Vector3(direction.x, 0f, direction.z).normalized;
    }
}
