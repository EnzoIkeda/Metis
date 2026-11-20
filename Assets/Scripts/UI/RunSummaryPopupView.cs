using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Resumo do fim da rodada: como terminou, fases vencidas, cidades tematicas, chefe, recompensas e propostas.
// O botao devolve a trilha de fundo e leva ao menu principal; a rodada ja foi apagada no fim da fase.
public class RunSummaryPopupView : MonoBehaviour
{
    private const string MainMenuScene = "MainMenu";

    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private TMP_Text _headerText;
    [SerializeField] private TMP_Text _linesText;
    [SerializeField] private Button _menuButton;

    // Nomes no idioma atual a partir dos ids guardados no save.
    [SerializeField] private ThemedPhaseLibraryData _themes;
    [SerializeField] private BossLibraryData _bosses;
    [SerializeField] private TradeOfferLibraryData _trades;

    // Desligado nos testes, que nao trocam de cena.
    [SerializeField] private bool _loadMenu = true;

    private bool _shown;

    public RunSummary Summary { get; private set; }
    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;
    public bool ReturnedToMenu { get; private set; }
    public string LinesText => _linesText != null ? _linesText.text : string.Empty;

    private void Start()
    {
        if (_menuButton != null)
            _menuButton.onClick.AddListener(BackToMenu);
        // Pode ter sido aberto antes do proprio Start.
        if (_panelRoot != null && _shown == false)
            _panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_menuButton != null)
            _menuButton.onClick.RemoveListener(BackToMenu);
    }

    public void Show(RunSummary summary)
    {
        if (summary == null)
            return;
        Summary = summary;
        _shown = true;
        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        var victory = summary.Ending == RunEnding.BossVictory;
        var boss = _turnManager != null ? _turnManager.BossDefinition : null;
        if (_titleText != null)
            _titleText.text = victory ? UIStrings.RunVictoryTitle : UIStrings.RunDefeatTitle;
        if (_messageText != null)
            _messageText.text = victory && boss != null ? boss.Victory : victory ? UIStrings.VictoryMessage : UIStrings.GameOverMessage;
        if (_headerText != null)
            _headerText.text = UIStrings.RunSummaryHeader;
        if (_linesText != null)
            _linesText.text = string.Join("\n", summary.Lines.Select(line => UIStrings.RunSummaryLine(line.Kind, line.Number, summary.Archetype, Names(line))));
        if (_menuButton != null)
        {
            var label = _menuButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = UIStrings.RunSummaryMenuButton;
        }
    }

    private string Names(RunSummaryLine line)
    {
        var names = new List<string>();
        foreach (var id in line.Ids)
            names.Add(NameOf(line.Kind, id));
        return string.Join(", ", names);
    }

    // Item que nao existe mais aparece pelo id, para o resumo nunca perder uma linha.
    private string NameOf(RunSummaryLineKind kind, string id)
    {
        switch (kind)
        {
            case RunSummaryLineKind.DefeatAtBoss:
            case RunSummaryLineKind.BossDefeated:
                var boss = _bosses != null ? _bosses.Find(id) : null;
                return boss != null ? boss.BossName : id;
            case RunSummaryLineKind.Themes:
                var theme = _themes != null ? _themes.Find(id) : null;
                return theme != null ? theme.ThemeName : id;
            case RunSummaryLineKind.RewardCards:
                var card = _turnManager != null && _turnManager.CardPool != null ? _turnManager.CardPool.FirstOrDefault(candidate => candidate != null && candidate.name == id) : null;
                return card != null ? card.CardName : id;
            case RunSummaryLineKind.RewardAdvantages:
                var advantage = _turnManager != null && _turnManager.AdvantagePool != null
                    ? _turnManager.AdvantagePool.FirstOrDefault(candidate => candidate != null && candidate.name == id)
                    : null;
                return advantage != null ? advantage.AdvantageName : id;
            case RunSummaryLineKind.Trades:
                var offer = _trades != null ? _trades.Find(id) : null;
                return offer != null ? offer.OfferName : id;
            default:
                return id;
        }
    }

    private void BackToMenu()
    {
        ReturnedToMenu = true;
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        var player = MusicPlayer.Instance;
        if (player != null)
            player.EndSequence();
        if (_loadMenu)
            SceneManager.LoadScene(MainMenuScene);
    }
}
