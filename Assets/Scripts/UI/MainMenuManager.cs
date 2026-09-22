using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

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

    private void Start()
    {
        RefreshTexts();
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
        // Vai pra selecao de baralho antes da primeira fase da rodada.
        SceneManager.LoadScene("DeckSelection");
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
