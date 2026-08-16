using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup de boas-vindas
/// </summary>
public class WelcomePopupView : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _startButton;

    [SerializeField] private string _title = "Bem-vindo(a) a Metis!";

    [SerializeField, TextArea(4, 12)] private string _message =
        "Você administra essa cidade por 20 turnos.\n\n" +
        "A cada turno, jogue uma carta da mão para alterar os parâmetros da cidade (Renda, " +
        "Energia, Segurança, População, Pesquisa, Sustentabilidade, Bem-Estar, Saúde, " +
        "Mobilidade). Algumas cartas também constroem algo no mapa.\n\n" +
        "Se algum parâmetro zerar ou estourar o limite, é Game Over. Sobreviva aos 20 turnos " +
        "com tudo equilibrado para vencer.";

    private void Start()
    {
        if (_titleText != null)
            _titleText.text = _title;
        if (_messageText != null)
            _messageText.text = _message;
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
