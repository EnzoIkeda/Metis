using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Passeio a pe pela cidade, so de contemplacao: troca a camera isometrica por uma em primeira pessoa,
// esconde o HUD e a mao e devolve tudo exatamente como estava ao voltar. Nao muda nenhum numero do jogo.
public class CityWalkController : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private PlacementManager _placementManager;
    [SerializeField] private CityIntroFlyover _flyover;

    [Header("Cameras")]
    [SerializeField] private Camera _mainCamera;
    [Tooltip("Camera em perspectiva do passeio; fica desligada fora dele.")]
    [SerializeField] private Camera _walkCamera;
    [Tooltip("Zoom e arrasto da camera isometrica, desligados no passeio.")]
    [SerializeField] private Behaviour[] _cameraControls = Array.Empty<Behaviour>();

    [Header("Interface")]
    [SerializeField] private Button _walkButton;
    [Tooltip("HUD e mao: somem no passeio e voltam como estavam.")]
    [SerializeField] private CanvasGroup[] _hiddenGroups = Array.Empty<CanvasGroup>();
    [Tooltip("Camada do passeio: joystick, area de olhar, voltar e dica.")]
    [SerializeField] private GameObject _overlay;
    [SerializeField] private WalkStickView _stick;
    [SerializeField] private WalkLookArea _lookArea;
    [SerializeField] private Button _backButton;
    [SerializeField] private TMP_Text _backButtonText;
    [SerializeField] private GameObject _hint;
    [SerializeField] private TMP_Text _hintText;

    [Tooltip("Enquanto um destes estiver aberto o botao do passeio some (a mesma lista do tutorial).")]
    [SerializeField] private GameObject[] _blockingPanels = Array.Empty<GameObject>();

    [Header("Carros")]
    [SerializeField] private RoadTrafficController _carTraffic;
    [SerializeField, Min(0f)] private float _carRadius = 0.22f;

    [Header("Movimento")]
    [Tooltip("Altura dos olhos acima do chao, em unidades do mundo.")]
    [SerializeField, Min(0.01f)] private float _eyeHeight = 0.2f;
    [SerializeField, Min(0.01f)] private float _bodyRadius = StreetWalk.DefaultRadius;
    [Tooltip("Celulas por segundo com o joystick no maximo.")]
    [SerializeField, Min(0f)] private float _walkSpeed = 1.3f;
    [Tooltip("Graus por pixel de arraste numa tela de 1080 de largura.")]
    [SerializeField, Min(0f)] private float _lookSensitivity = 0.18f;
    [Tooltip("Graus por segundo nas setas do teclado.")]
    [SerializeField, Min(0f)] private float _keyboardTurnSpeed = 90f;

    [Header("Ceu e nevoa")]
    [SerializeField] private Color _skyColor = new Color(0.66f, 0.8f, 0.93f);
    [SerializeField, Min(0f)] private float _fogStart = 4f;
    [SerializeField, Min(0.1f)] private float _fogEnd = 11f;
    [Tooltip("Luz ambiente do passeio: de perto, as faces na sombra ficariam pretas.")]
    [SerializeField] private Color _ambientColor = new Color(0.55f, 0.58f, 0.64f);
    [Tooltip("Segundos (reais) ate a dica sumir sozinha.")]
    [SerializeField, Min(0f)] private float _hintSeconds = 6f;

    [Tooltip("Intervalo (real) entre as consultas que mostram ou escondem o botao.")]
    [SerializeField, Min(0.01f)] private float _pollSeconds = 0.25f;

    private StreetWalk _walk;
    private WalkPose _pose;
    private bool _hasPose;
    private float _nextPoll;
    private float _hintTimer;
    private SavedState _saved;
    private readonly List<(float X, float Z, float Radius)> _obstacles = new List<(float X, float Z, float Radius)>();

    private static bool s_hintShown;

    public bool IsWalking { get; private set; }
    public WalkPose Pose => _pose;

    public event Action Entered;
    public event Action Exited;

    private struct SavedState
    {
        public bool MainCameraEnabled;
        public bool[] ControlsEnabled;
        public float[] Alpha;
        public bool[] Raycasts;
        public bool[] Interactable;
        public bool Fog;
        public FogMode FogMode;
        public Color FogColor;
        public float FogStart;
        public float FogEnd;
        public UnityEngine.Rendering.AmbientMode AmbientMode;
        public Color AmbientLight;
        public UnityEngine.Rendering.SphericalHarmonicsL2 AmbientProbe;
    }

    private void Awake()
    {
        if (_overlay != null)
            _overlay.SetActive(false);
        if (_walkCamera != null)
            _walkCamera.enabled = false;
    }

    private void Start()
    {
        if (_walkButton != null)
            _walkButton.onClick.AddListener(Enter);
        if (_backButton != null)
            _backButton.onClick.AddListener(Exit);
        RefreshButton();
    }

    private void OnDestroy()
    {
        if (_walkButton != null)
            _walkButton.onClick.RemoveListener(Enter);
        if (_backButton != null)
            _backButton.onClick.RemoveListener(Exit);
        // Nevoa e luz ambiente sao da cena; sair dela no meio do passeio nao pode deixar a proxima com elas.
        if (IsWalking)
            RestoreRenderSettings();
    }

    private void Update()
    {
        if (IsWalking == false)
        {
            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + _pollSeconds;
                RefreshButton();
            }
            return;
        }

        // Tempo real: o passeio nao depende da escala de tempo do jogo.
        var dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        var move = ReadMove();
        var look = ReadLook(dt);

        _pose = StreetWalk.Look(_pose, look.x, look.y);
        if (move.sqrMagnitude > 0f)
            _pose = _walk.Walk(_pose, move.y, move.x, _walkSpeed * dt);
        _pose = _walk.PushOut(_pose, CollectCars());
        ApplyPose();

        if (_hint != null && _hint.activeSelf)
        {
            _hintTimer -= Time.unscaledDeltaTime;
            if (_hintTimer <= 0f || move.sqrMagnitude > 0f || look.sqrMagnitude > 0f)
                _hint.SetActive(false);
        }
    }

    public bool CanWalk()
    {
        if (IsWalking || _placementManager == null || _placementManager.Plan == null || _walkCamera == null || _mainCamera == null)
            return false;
        var machine = _turnManager != null ? _turnManager.Machine : null;
        if (machine == null || machine.CurrentPhase != TurnPhase.Action || machine.Outcome != GameOutcome.None)
            return false;
        if (_flyover != null && _flyover.IsPlaying)
            return false;
        foreach (var panel in _blockingPanels)
        {
            if (panel != null && panel.activeInHierarchy)
                return false;
        }
        return true;
    }

    private void RefreshButton()
    {
        if (_walkButton == null)
            return;
        var show = CanWalk();
        if (_walkButton.gameObject.activeSelf != show)
            _walkButton.gameObject.SetActive(show);
    }

    public void Enter()
    {
        if (CanWalk() == false)
            return;

        var plan = _placementManager.Plan;
        if (_walk == null)
            _walk = new StreetWalk(plan, _bodyRadius / Mathf.Max(0.0001f, _placementManager.CellSize));
        // Voltar ao passeio na mesma fase continua de onde parou.
        if (_hasPose == false)
        {
            _pose = StreetWalk.StartPose(plan);
            _hasPose = true;
        }

        SaveState();
        IsWalking = true;

        foreach (var control in _cameraControls)
        {
            if (control != null)
                control.enabled = false;
        }
        foreach (var group in _hiddenGroups)
        {
            if (group == null)
                continue;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = _skyColor;
        RenderSettings.fogStartDistance = _fogStart;
        RenderSettings.fogEndDistance = _fogEnd;
        var probe = new UnityEngine.Rendering.SphericalHarmonicsL2();
        probe.AddAmbientLight(_ambientColor);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = _ambientColor;
        RenderSettings.ambientProbe = probe;

        _walkCamera.clearFlags = CameraClearFlags.SolidColor;
        _walkCamera.backgroundColor = _skyColor;
        _walkCamera.farClipPlane = _fogEnd + 1f;
        // O brilho opcional vale tambem aqui: segue o que a camera principal estiver usando.
        _walkCamera.GetUniversalAdditionalCameraData().renderPostProcessing = _mainCamera.GetUniversalAdditionalCameraData().renderPostProcessing;
        ApplyPose();
        _walkCamera.enabled = true;
        _mainCamera.enabled = false;

        if (_overlay != null)
            _overlay.SetActive(true);
        if (_backButtonText != null)
            _backButtonText.text = UIStrings.WalkBackButton;
        if (_hint != null)
        {
            _hint.SetActive(s_hintShown == false);
            _hintTimer = _hintSeconds;
            s_hintShown = true;
        }
        if (_hintText != null)
            _hintText.text = UIStrings.WalkHint;
        if (_walkButton != null)
            _walkButton.gameObject.SetActive(false);

        Entered?.Invoke();
    }

    public void Exit()
    {
        if (IsWalking == false)
            return;
        IsWalking = false;

        if (_stick != null)
            _stick.Release();
        if (_lookArea != null)
            _lookArea.ConsumeDelta();
        if (_overlay != null)
            _overlay.SetActive(false);

        _walkCamera.enabled = false;
        _mainCamera.enabled = _saved.MainCameraEnabled;
        for (var i = 0; i < _cameraControls.Length; i++)
        {
            if (_cameraControls[i] != null)
                _cameraControls[i].enabled = _saved.ControlsEnabled[i];
        }
        for (var i = 0; i < _hiddenGroups.Length; i++)
        {
            var group = _hiddenGroups[i];
            if (group == null)
                continue;
            group.alpha = _saved.Alpha[i];
            group.blocksRaycasts = _saved.Raycasts[i];
            group.interactable = _saved.Interactable[i];
        }
        RestoreRenderSettings();

        _nextPoll = 0f;
        RefreshButton();
        Exited?.Invoke();
    }

    private void SaveState()
    {
        _saved.MainCameraEnabled = _mainCamera.enabled;
        _saved.ControlsEnabled = new bool[_cameraControls.Length];
        for (var i = 0; i < _cameraControls.Length; i++)
            _saved.ControlsEnabled[i] = _cameraControls[i] != null && _cameraControls[i].enabled;
        _saved.Alpha = new float[_hiddenGroups.Length];
        _saved.Raycasts = new bool[_hiddenGroups.Length];
        _saved.Interactable = new bool[_hiddenGroups.Length];
        for (var i = 0; i < _hiddenGroups.Length; i++)
        {
            var group = _hiddenGroups[i];
            if (group == null)
                continue;
            _saved.Alpha[i] = group.alpha;
            _saved.Raycasts[i] = group.blocksRaycasts;
            _saved.Interactable[i] = group.interactable;
        }
        _saved.Fog = RenderSettings.fog;
        _saved.FogMode = RenderSettings.fogMode;
        _saved.FogColor = RenderSettings.fogColor;
        _saved.FogStart = RenderSettings.fogStartDistance;
        _saved.FogEnd = RenderSettings.fogEndDistance;
        _saved.AmbientMode = RenderSettings.ambientMode;
        _saved.AmbientLight = RenderSettings.ambientLight;
        _saved.AmbientProbe = RenderSettings.ambientProbe;
    }

    private void RestoreRenderSettings()
    {
        RenderSettings.fog = _saved.Fog;
        RenderSettings.fogMode = _saved.FogMode;
        RenderSettings.fogColor = _saved.FogColor;
        RenderSettings.fogStartDistance = _saved.FogStart;
        RenderSettings.fogEndDistance = _saved.FogEnd;
        RenderSettings.ambientMode = _saved.AmbientMode;
        RenderSettings.ambientLight = _saved.AmbientLight;
        RenderSettings.ambientProbe = _saved.AmbientProbe;
    }

    private Vector2 ReadMove()
    {
        var move = _stick != null ? _stick.Value : Vector2.zero;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            var keys = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                keys.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                keys.y -= 1f;
            if (keyboard.dKey.isPressed)
                keys.x += 1f;
            if (keyboard.aKey.isPressed)
                keys.x -= 1f;
            if (keys.sqrMagnitude > 0f)
                move = Vector2.ClampMagnitude(move + keys, 1f);
        }
        return move;
    }

    // Graus de giro (x) e de inclinacao (y, positivo olha para baixo) neste quadro.
    private Vector2 ReadLook(float dt)
    {
        var look = Vector2.zero;
        if (_lookArea != null)
        {
            var delta = _lookArea.ConsumeDelta();
            var scale = _lookSensitivity * 1080f / Mathf.Max(1, Screen.width);
            // Arrastar para a direita gira para a direita; para cima, olha para cima.
            look += new Vector2(delta.x * scale, -delta.y * scale);
        }
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rightArrowKey.isPressed || keyboard.eKey.isPressed)
                look.x += _keyboardTurnSpeed * dt;
            if (keyboard.leftArrowKey.isPressed || keyboard.qKey.isPressed)
                look.x -= _keyboardTurnSpeed * dt;
        }
        return look;
    }

    private IReadOnlyList<(float X, float Z, float Radius)> CollectCars()
    {
        _obstacles.Clear();
        if (_carTraffic == null)
            return _obstacles;
        var cell = Mathf.Max(0.0001f, _placementManager.CellSize);
        var grid = _placementManager.transform;
        foreach (var car in _carTraffic.ActiveActors)
        {
            if (car == null || car.gameObject.activeInHierarchy == false)
                continue;
            var local = grid.InverseTransformPoint(car.transform.position);
            _obstacles.Add((local.x / cell, local.z / cell, _carRadius / cell));
        }
        return _obstacles;
    }

    private void ApplyPose()
    {
        if (_walkCamera == null || _placementManager == null)
            return;
        var cell = _placementManager.CellSize;
        var grid = _placementManager.transform;
        _walkCamera.transform.SetPositionAndRotation(
            grid.TransformPoint(new Vector3(_pose.X * cell, 0f, _pose.Z * cell)) + Vector3.up * _eyeHeight,
            grid.rotation * Quaternion.Euler(_pose.Pitch, _pose.Yaw, 0f));
    }
}
