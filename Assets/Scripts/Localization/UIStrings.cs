// Tabela central de textos fixos de UI, nos dois idiomas suportados.
public static class UIStrings
{
    private static bool IsPt => LocalizationManager.Current == Language.Portuguese;

    public static string WelcomeTitle => IsPt ? "Bem-vindo(a) a Metis!" : "Welcome to Metis!";

    public static string WelcomeMessage => IsPt
        ? "Você é o responsável por gerenciar esta cidade ao longo de 20 turnos.\n\n" +
          "A cada turno você pode jogar uma carta da sua mão. Cada carta altera parâmetros da " +
          "cidade — Renda, Energia, Segurança, População, Pesquisa, Sustentabilidade, Bem-Estar, " +
          "Saúde e Mobilidade — e algumas também constroem algo na cidade.\n\n" +
          "Fique de olho nos parâmetros: se algum deles zerar ou estourar o limite, é Game Over. " +
          "Sobreviva aos 20 turnos com a cidade equilibrada para vencer e construir uma verdadeira " +
          "Smart City!"
        : "You are responsible for managing this city over 20 turns.\n\n" +
          "Each turn you may play one card from your hand. Every card changes the city's " +
          "parameters — Income, Energy, Security, Population, Research, Sustainability, " +
          "Wellbeing, Health and Mobility — and some also build something in the city.\n\n" +
          "Keep an eye on the parameters: if any of them hits zero or maxes out, it's Game Over. " +
          "Survive all 20 turns with the city balanced to win and build a true Smart City!";

    public static string GameOverTitle => IsPt ? "Fim de Jogo" : "Game Over";

    public static string GameOverMessage => IsPt
        ? "A cidade entrou em crise, final da simulação!"
        : "The city has fallen into crisis — simulation over!";

    public static string VictoryTitle => IsPt ? "Vitória!" : "Victory!";

    public static string VictoryMessage => IsPt
        ? "Você governou com sucesso por 20 anos, parabéns! "
        : "You successfully governed for 20 years, congratulations!";

    public static string PlayButton => IsPt ? "Jogar" : "Play";
    public static string BackButton => IsPt ? "Voltar" : "Back";

    public static string MainMenuPlay => IsPt ? "Jogar" : "Play";
    public static string MainMenuSettings => IsPt ? "Configurações" : "Settings";
    public static string MainMenuQuit => IsPt ? "Sair" : "Quit";
    public static string MainMenuSettingsBack => IsPt ? "Voltar" : "Back";

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
