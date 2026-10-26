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

    private void Start()
    {
        if (_titleText != null)
            _titleText.text = UIStrings.WelcomeTitle;
        if (_messageText != null)
            _messageText.text = UIStrings.WelcomeMessage;
        if (_startButton != null)
            _startButton.onClick.AddListener(Hide);
        if (_reportButton != null)
        {
            var label = _reportButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = UIStrings.ModelReportWelcomeButton;
            _reportButton.gameObject.SetActive(_reportView != null);
            if (_reportView != null)
                _reportButton.onClick.AddListener(_reportView.Show);
        }

        // Ao continuar uma fase salva o jogador ja passou por aqui.
        var resumed = _turnManager != null && _turnManager.ResumedFromSave;
        if (_panelRoot != null)
            _panelRoot.SetActive(resumed == false);
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
