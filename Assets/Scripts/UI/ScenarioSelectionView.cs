using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tela de escolha do cenario, aberta pelo menu e para onde o jogador volta quando a fase termina.
public class ScenarioSelectionView : MonoBehaviour
{
    private const string MainMenuScene = "MainMenu";
    private const string DeckSelectionScene = "DeckSelection";

    [SerializeField] private ScenarioLibraryData _library;
    [SerializeField] private ParameterDisplayData _displayData;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _hintText;
    [SerializeField] private ScenarioCardView _cardTemplate;
    [SerializeField] private RectTransform _cardRoot;
    [SerializeField] private Button _backButton;
    [SerializeField] private TMP_Text _backButtonText;

    private void Start()
    {
        // Nenhum cenario fica selecionado ao chegar aqui; o save de cada um so e lido ao montar o cartao.
        SessionMode.EnterRoguelike();

        if (_titleText != null)
            _titleText.text = UIStrings.ScenarioSelectionTitle;
        if (_hintText != null)
            _hintText.text = UIStrings.ScenarioSelectionHint;
        if (_backButtonText != null)
            _backButtonText.text = UIStrings.ScenarioBackButton;
        if (_backButton != null)
            _backButton.onClick.AddListener(() => SceneManager.LoadScene(MainMenuScene));

        _cardTemplate.gameObject.SetActive(false);
        foreach (var scenario in _library.Scenarios)
        {
            if (scenario == null || scenario.Theme == null)
                continue;

            var card = Instantiate(_cardTemplate, _cardRoot);
            card.name = "Card_" + scenario.name;
            card.gameObject.SetActive(true);

            var captured = scenario;
            card.Bind(scenario, _displayData, ScenarioProgress.BestStars(scenario.name), HasSavedGame(scenario),
                () => Play(captured), () => StartFresh(captured));
        }
    }

    private static bool HasSavedGame(ScenarioData scenario)
    {
        SessionMode.EnterScenario(scenario.name, scenario.Theme.name);
        var saved = MetaProgressionManager.HasRunInProgress;
        SessionMode.EnterRoguelike();
        return saved;
    }

    private static void Play(ScenarioData scenario)
    {
        SessionMode.EnterScenario(scenario.name, scenario.Theme.name);
        if (MetaProgressionManager.HasRunInProgress)
            SceneManager.LoadScene(MetaProgressionManager.CitySceneName);
        else
            StartFresh(scenario);
    }

    private static void StartFresh(ScenarioData scenario)
    {
        SessionMode.EnterScenario(scenario.name, scenario.Theme.name);
        if (scenario.FreeArchetype)
        {
            SceneManager.LoadScene(DeckSelectionScene);
            return;
        }
        MetaProgressionManager.BeginScenario(scenario.name, scenario.Theme.name, scenario.Archetype);
        SceneManager.LoadScene(MetaProgressionManager.CitySceneName);
    }
}
