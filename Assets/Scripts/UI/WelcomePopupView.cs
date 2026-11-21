using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup de boas-vindas mostrado ao carregar a cena.
public class WelcomePopupView : MonoBehaviour
{
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _startButton;

    // Abre o relatorio tecnico do modelo por cima das boas-vindas.
    [SerializeField] private Button _reportButton;
    [SerializeField] private ModelReportView _reportView;

    // Abre a ficha da cidade tematica no lugar do relatorio.
    [SerializeField] private KnowledgePopupView _knowledgePopup;

    // Sobrevoo da abertura; enquanto ele roda as boas-vindas esperam. Sem referencia, abrem na hora.
    [SerializeField] private CityIntroFlyover _flyover;

    // O jogador fechou as boas-vindas.
    public event Action Closed;

    private void Start()
    {
        var theme = _turnManager != null ? _turnManager.Theme : null;
        var boss = _turnManager != null ? _turnManager.BossDefinition : null;
        if (_titleText != null)
            _titleText.text = boss != null ? boss.BossName : theme != null ? theme.ThemeName : UIStrings.WelcomeTitle;
        if (_messageText != null)
            _messageText.text = boss != null ? boss.Intro : theme != null ? $"{theme.Opening}\n\n<i>{theme.Inspiration}</i>" : UIStrings.WelcomeMessage;
        if (_startButton != null)
            _startButton.onClick.AddListener(Close);
        if (_reportButton != null)
        {
            var entry = _knowledgePopup == null ? null : boss != null ? boss.KnowledgeEntry : theme != null ? theme.KnowledgeEntry : null;
            var label = _reportButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = entry == null ? UIStrings.ModelReportWelcomeButton : boss != null ? UIStrings.BossLearnMoreButton : UIStrings.ThemeLearnMoreButton;
            _reportButton.gameObject.SetActive(entry != null || _reportView != null);
            if (entry != null)
                _reportButton.onClick.AddListener(() => _knowledgePopup.Show(entry));
            else if (_reportView != null)
                _reportButton.onClick.AddListener(_reportView.Show);
        }

        // Ao continuar uma fase salva o jogador ja passou por aqui.
        var resumed = _turnManager != null && _turnManager.ResumedFromSave;
        var waitFlyover = resumed == false && _flyover != null && _flyover.IsPlaying;
        if (_panelRoot != null)
            _panelRoot.SetActive(resumed == false && waitFlyover == false);
        if (waitFlyover)
            _flyover.Finished += Open;
    }

    private void OnDestroy()
    {
        if (_flyover != null)
            _flyover.Finished -= Open;
    }

    private void Open()
    {
        _flyover.Finished -= Open;
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    private void Close()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        // A musica da abertura acompanha ate aqui e devolve a vez a trilha de fundo.
        if (MusicPlayer.Instance != null)
            MusicPlayer.Instance.EndSequence();
        Closed?.Invoke();
    }
}
