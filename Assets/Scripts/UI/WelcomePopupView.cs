using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup de boas-vindas mostrado ao carregar a cena.
public class WelcomePopupView : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _startButton;

    private void Start()
    {
        if (_titleText != null)
            _titleText.text = UIStrings.WelcomeTitle;
        if (_messageText != null)
            _messageText.text = UIStrings.WelcomeMessage;
        if (_startButton != null)
            _startButton.onClick.AddListener(Hide);

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
