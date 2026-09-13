using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Botao de pause do HUD: pausa o tempo e abre acesso a Configuracoes (idioma) ou saida pro menu principal.
public class PausePopupView : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private GameObject _mainView;
    [SerializeField] private GameObject _settingsView;

    [SerializeField] private Button _pauseButton;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _languageButton;
    [SerializeField] private Button _settingsBackButton;

    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _resumeButtonText;
    [SerializeField] private TMP_Text _settingsButtonText;
    [SerializeField] private TMP_Text _mainMenuButtonText;
    [SerializeField] private TMP_Text _languageButtonText;
    [SerializeField] private TMP_Text _settingsBackButtonText;

    private void Start()
    {
        if (_pauseButton != null)
            _pauseButton.onClick.AddListener(OpenPause);
        if (_resumeButton != null)
            _resumeButton.onClick.AddListener(ResumeGame);
        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(OpenSettings);
        if (_mainMenuButton != null)
            _mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        if (_languageButton != null)
            _languageButton.onClick.AddListener(ToggleLanguage);
        if (_settingsBackButton != null)
            _settingsBackButton.onClick.AddListener(CloseSettings);

        RefreshTexts();
        Hide();
    }

    private void OnDisable()
    {
        // Nunca deixar o jogo travado em timeScale 0 se esse componente for desativado com o popup aberto.
        Time.timeScale = 1f;
    }

    private void RefreshTexts()
    {
        if (_titleText != null)
            _titleText.text = UIStrings.PauseTitle;
        if (_resumeButtonText != null)
            _resumeButtonText.text = UIStrings.PauseResumeButton;
        if (_settingsButtonText != null)
            _settingsButtonText.text = UIStrings.MainMenuSettings;
        if (_mainMenuButtonText != null)
            _mainMenuButtonText.text = UIStrings.PauseMainMenuButton;
        if (_languageButtonText != null)
            _languageButtonText.text = UIStrings.LanguageButtonLabel;
        if (_settingsBackButtonText != null)
            _settingsBackButtonText.text = UIStrings.MainMenuSettingsBack;
    }

    // Botao de pause do HUD.
    public void OpenPause()
    {
        ShowMainView();
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    // Botao 'Continuar'.
    public void ResumeGame()
    {
        Time.timeScale = 1f;
        Hide();
    }

    // Botao 'Configuracoes'.
    public void OpenSettings()
    {
        if (_mainView != null)
            _mainView.SetActive(false);
        if (_settingsView != null)
            _settingsView.SetActive(true);
    }

    // Botao 'Voltar' de dentro de Configuracoes.
    public void CloseSettings()
    {
        ShowMainView();
    }

    // Botao de idioma dentro de Configuracoes.
    public void ToggleLanguage()
    {
        LocalizationManager.Current = LocalizationManager.Current == Language.English
            ? Language.Portuguese
            : Language.English;
        RefreshTexts();
    }

    // Botao 'Menu Principal', desiste da rodada como um Game Over, ja que nao ha fase em andamento pra retomar.
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        MetaProgressionManager.ResetRun();
        SceneManager.LoadScene("MainMenu");
    }

    private void ShowMainView()
    {
        if (_mainView != null)
            _mainView.SetActive(true);
        if (_settingsView != null)
            _settingsView.SetActive(false);
    }

    private void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }
}
