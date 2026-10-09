// Tabela central de textos fixos de UI, nos dois idiomas suportados.
public static class UIStrings
{
    private static bool IsPt => LocalizationManager.Current == Language.Portuguese;

    public static string WelcomeTitle => IsPt ? "Bem-vindo(a) a Métis!" : "Welcome to Métis!";

    public static string WelcomeMessage => IsPt
        ? "Você administra esta cidade por 20 turnos.\n\n" +
          "A cada turno, jogue uma carta da mão: toque nela para ver os detalhes e arraste-a para " +
          "fora da mão para jogá-la. As cartas custam Renda e mudam os parâmetros da cidade, que " +
          "também se influenciam entre si. Depois de cada jogada, um evento pode mudar a situação. " +
          "Se nenhuma carta puder ser jogada, passe a vez.\n\n" +
          "Um parâmetro que chega ao nível crítico entra em colapso e derruba o Bem-estar (o " +
          "coração) a cada turno. Se o Bem-estar chegar ao nível crítico, a população perde a " +
          "confiança na gestão e é fim de jogo.\n\n" +
          "Invista em Pesquisa para liberar tecnologias mais avançadas e transformar a cidade numa " +
          "Smart City. Sobreviva aos 20 turnos para vencer!"
        : "You manage this city for 20 turns.\n\n" +
          "Each turn, play one card from your hand: tap it to see its details and drag it out of " +
          "your hand to play it. Cards cost Income and change the city's parameters, which also " +
          "affect one another. After each play, an event may change the situation. If no card can " +
          "be played, pass your turn.\n\n" +
          "A parameter that reaches its critical level collapses and drags Wellbeing (the heart) " +
          "down every turn. If Wellbeing reaches its critical level, the population loses " +
          "confidence in your administration and the game is over.\n\n" +
          "Invest in Research to unlock more advanced technologies and turn the city into a Smart " +
          "City. Survive all 20 turns to win!";

    public static string GameOverTitle => IsPt ? "Fim de Jogo" : "Game Over";

    public static string GameOverMessage => IsPt
        ? "A cidade entrou em crise, final da simulação!"
        : "The city has fallen into crisis, simulation over!";

    public static string VictoryTitle => IsPt ? "Vitória!" : "Victory!";

    public static string VictoryMessage => IsPt
        ? "Você governou com sucesso por 20 turnos, parabéns!"
        : "You successfully governed for 20 turns, congratulations!";

    public static string RewardTitle => IsPt ? "Recompensa" : "Reward";

    public static string PlayButton => IsPt ? "Jogar" : "Play";
    public static string BackButton => IsPt ? "Voltar" : "Back";

    public static string MainMenuPlay => IsPt ? "Jogar" : "Play";
    public static string MainMenuSettings => IsPt ? "Configurações" : "Settings";
    public static string MainMenuQuit => IsPt ? "Sair" : "Quit";
    public static string MainMenuSettingsBack => IsPt ? "Voltar" : "Back";
    public static string MainMenuContinue => IsPt ? "Continuar" : "Continue";

    public static string NewRunConfirmTitle => IsPt ? "Nova rodada" : "New run";
    public static string NewRunConfirmMessage => IsPt
        ? "Você tem uma rodada em andamento. Começar uma nova apaga o progresso salvo."
        : "You have a run in progress. Starting a new one erases your saved progress.";
    public static string NewRunConfirmButton => IsPt ? "Começar nova" : "Start new";
    public static string CancelButton => IsPt ? "Cancelar" : "Cancel";

    public static string SaveDiscardedTitle => IsPt ? "Progresso descartado" : "Progress discarded";
    public static string SaveDiscardedMessage => IsPt
        ? "O progresso salvo era de uma versão antiga do jogo e não pôde ser carregado."
        : "The saved progress was from an older version of the game and could not be loaded.";
    public static string OkButton => "OK";

    public static string SettingsMusicVolume => IsPt ? "Música" : "Music";
    public static string SettingsSfxVolume => IsPt ? "Efeitos" : "Effects";

    // Nome do controle seguido do volume em porcentagem, ex. "Música: 80%".
    public static string VolumeLabel(string name, float volume) => $"{name}: {UnityEngine.Mathf.RoundToInt(volume * 100f)}%";

    public static string PauseTitle => IsPt ? "Pausado" : "Paused";
    public static string PauseResumeButton => IsPt ? "Continuar" : "Resume";
    public static string PauseMainMenuButton => IsPt ? "Menu Principal" : "Main Menu";

    public static string LanguageName(Language language) =>
        language == Language.Portuguese ? "Português" : "English";

    public static string LanguageButtonLabel =>
        (IsPt ? "Idioma: " : "Language: ") + LanguageName(LocalizationManager.Current);

    // Formato curto, usado no espaco reduzido da carta recolhida.
    public static string CardRequirementsShort(float requiredResearch, float cost) => IsPt
        ? $"Pesq {requiredResearch:0} · Custo {cost:0}"
        : $"Res {requiredResearch:0} · Cost {cost:0}";

    // Formato completo, usado no popup de detalhe da carta.
    public static string CardRequirementsFull(float requiredResearch, float cost) => IsPt
        ? $"Pesquisa mín.: {requiredResearch:0} · Custo: {cost:0}"
        : $"Research min.: {requiredResearch:0} · Cost: {cost:0}";

    // Efeitos exatos da carta, um parametro por linha, mostrados quando a mao foi revelada.
    public static string CardEffects(CardData card)
    {
        if (card.StatEffects == null || card.StatEffects.Count == 0)
            return IsPt ? "Sem efeito direto" : "No direct effect";

        var lines = new System.Collections.Generic.List<string>();
        foreach (var effect in card.StatEffects)
            lines.Add($"{effect.Parameter.GetDisplayName()} {effect.Amount:+0;-0;0}");
        return string.Join("\n", lines);
    }

    public static string SearchTitle => IsPt ? "Buscar no baralho" : "Search the deck";

    public static string PassTurnButton => IsPt ? "Passar a vez" : "Pass turn";

    public static string TurnCounter(int turnIndex, int totalTurns) => IsPt
        ? $"Turno {turnIndex}/{totalTurns}"
        : $"Turn {turnIndex}/{totalTurns}";

    public static string DeckSelectionTitle => IsPt ? "Escolha seu baralho" : "Choose your deck";

    public static string ArchetypeName(CardArchetype archetype)
    {
        switch (archetype)
        {
            case CardArchetype.Sustentabilidade:
                return IsPt ? "Sustentabilidade" : "Sustainability";
            case CardArchetype.Industria:
                return IsPt ? "Indústria" : "Industry";
            case CardArchetype.Automacao:
                return IsPt ? "Automação" : "Automation";
            default:
                return IsPt ? "Geral" : "General";
        }
    }

    public static string PhaseMapTitle => IsPt ? "Escolha o próximo destino" : "Choose your next destination";

    public static string PhaseMapNodeLabel(string nodeId)
    {
        switch (nodeId)
        {
            case "next":
                return IsPt ? "Avançar" : "Advance";
            default:
                return nodeId;
        }
    }

    public static string ArchetypeDescription(CardArchetype archetype)
    {
        switch (archetype)
        {
            case CardArchetype.Sustentabilidade:
                return IsPt
                    ? "Acelera Sustentabilidade e Bem-estar, mas é fraco em Renda e Energia."
                    : "Boosts Sustainability and Wellbeing, but weak in Income and Energy.";
            case CardArchetype.Industria:
                return IsPt
                    ? "Acelera Renda, mas pressiona Sustentabilidade e Saúde."
                    : "Boosts Income, but pressures Sustainability and Health.";
            case CardArchetype.Automacao:
                return IsPt
                    ? "Acelera Pesquisa e Mobilidade, mas pressiona Segurança e População."
                    : "Boosts Research and Mobility, but pressures Security and Population.";
            default:
                return string.Empty;
        }
    }
}
