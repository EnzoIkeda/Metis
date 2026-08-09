using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Paineis da UI")]
    [SerializeField] private GameObject settingsPanel; 

    // Metodo para o botao 'Jogar'
    public void PlayGame()
    {
        // Carrega a cena 3D do jogo (Assets/Scenes/Game/Game.unity)
        SceneManager.LoadScene("Game");
    }

    // Metodos para o botao 'Configuracoes'
    public void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
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