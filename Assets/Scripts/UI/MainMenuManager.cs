using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Controla o menu principal, incluindo troca de idioma e navegacao entre paineis.
public class MainMenuManager : MonoBehaviour
{
    [Header("Paineis da UI")]
    [FormerlySerializedAs("settingsPanel")] [SerializeField] private GameObject _settingsPanel;

    [Header("Textos localizados")]
    [SerializeField] private TMP_Text _playButtonText;
    [SerializeField] private TMP_Text _settingsButtonText;
    [SerializeField] private TMP_Text _quitButtonText;
    [SerializeField] private TMP_Text _settingsBackButtonText;
    [SerializeField] private TMP_Text _languageButtonText;

    [Header("Rodada salva")]
    [SerializeField] private Button _continueButton;
    [SerializeField] private TMP_Text _continueButtonText;

    // Dialogo reaproveitado pra confirmar uma rodada nova e pra avisar que um save antigo foi descartado.
    [SerializeField] private GameObject _dialogPanel;
    [SerializeField] private TMP_Text _dialogTitleText;
    [SerializeField] private TMP_Text _dialogMessageText;
    [SerializeField] private Button _dialogConfirmButton;
    [SerializeField] private TMP_Text _dialogConfirmButtonText;
    [SerializeField] private Button _dialogCancelButton;
    [SerializeField] private TMP_Text _dialogCancelButtonText;

    private bool _dialogStartsNewRun;

    private void Start()
    {
        if (_continueButton != null)
            _continueButton.onClick.AddListener(ContinueGame);
        if (_dialogConfirmButton != null)
            _dialogConfirmButton.onClick.AddListener(ConfirmDialog);
        if (_dialogCancelButton != null)
            _dialogCancelButton.onClick.AddListener(CloseDialog);

        CloseDialog();
        RefreshTexts();

        if (_continueButton != null)
            _continueButton.gameObject.SetActive(MetaProgressionManager.HasRunInProgress);

        if (MetaProgressionManager.ConsumeDiscardedSaveNotice())
            OpenDialog(UIStrings.SaveDiscardedTitle, UIStrings.SaveDiscardedMessage, UIStrings.OkButton, showCancel: false, startsNewRun: false);
    }

    private void RefreshTexts()
    {
        if (_playButtonText != null)
            _playButtonText.text = UIStrings.MainMenuPlay;
        if (_settingsButtonText != null)
            _settingsButtonText.text = UIStrings.MainMenuSettings;
        if (_quitButtonText != null)
            _quitButtonText.text = UIStrings.MainMenuQuit;
        if (_settingsBackButtonText != null)
            _settingsBackButtonText.text = UIStrings.MainMenuSettingsBack;
        if (_languageButtonText != null)
            _languageButtonText.text = UIStrings.LanguageButtonLabel;
        if (_continueButtonText != null)
            _continueButtonText.text = UIStrings.MainMenuContinue;
        if (_dialogCancelButtonText != null)
            _dialogCancelButtonText.text = UIStrings.CancelButton;
    }

    // Metodo para o botao de idioma nas Configuracoes
    public void ToggleLanguage()
    {
        LocalizationManager.Current = LocalizationManager.Current == Language.English
            ? Language.Portuguese
            : Language.English;
        RefreshTexts();
    }

    // Metodo para o botao 'Jogar'
    public void PlayGame()
    {
        // Com uma rodada salva, pergunta antes de jogar o progresso fora.
        if (MetaProgressionManager.HasRunInProgress)
        {
            OpenDialog(UIStrings.NewRunConfirmTitle, UIStrings.NewRunConfirmMessage, UIStrings.NewRunConfirmButton, showCancel: true, startsNewRun: true);
            return;
        }

        StartNewRun();
    }

    // Vai pra selecao de baralho antes da primeira fase da rodada.
    private static void StartNewRun()
    {
        MetaProgressionManager.ResetRun();
        SceneManager.LoadScene("DeckSelection");
    }

    // Botao 'Continuar', visivel so com rodada salva.
    public void ContinueGame()
    {
        if (MetaProgressionManager.HasRunInProgress == false)
            return;

        SceneManager.LoadScene(MetaProgressionManager.ContinueSceneName);
    }

    private void OpenDialog(string title, string message, string confirmLabel, bool showCancel, bool startsNewRun)
    {
        _dialogStartsNewRun = startsNewRun;
        if (_dialogTitleText != null)
            _dialogTitleText.text = title;
        if (_dialogMessageText != null)
            _dialogMessageText.text = message;
        if (_dialogConfirmButtonText != null)
            _dialogConfirmButtonText.text = confirmLabel;
        if (_dialogCancelButton != null)
            _dialogCancelButton.gameObject.SetActive(showCancel);
        if (_dialogPanel != null)
            _dialogPanel.SetActive(true);
    }

    private void ConfirmDialog()
    {
        var startsNewRun = _dialogStartsNewRun;
        CloseDialog();
        if (startsNewRun)
            StartNewRun();
    }

    private void CloseDialog()
    {
        _dialogStartsNewRun = false;
        if (_dialogPanel != null)
            _dialogPanel.SetActive(false);
    }

    // Metodos para o botao 'Configuracoes'
    public void OpenSettings()
    {
        if (_settingsPanel != null)
            _settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);
    }

    // Metodo para o botao 'Sair'
    public void QuitGame()
    {
        Debug.Log("Saindo do jogo...");
        Application.Quit();

        #if UNITY_EDITOR
        // Para parar a execucao caso esteja rodando direto dentro do Editor da Unity
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
