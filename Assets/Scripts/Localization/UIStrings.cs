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

    // Formato curto, usado no espaco reduzido da carta recolhida: icone de Pesquisa com a exigencia, icone de Renda com o custo.
    public static string CardRequirementsShort(ParameterDisplayData display, float requiredResearch, float cost) =>
        $"{ParameterIcon(CityParameterType.Pesquisa)} {ResearchText(display, requiredResearch, withUnit: false)} · {ParameterIcon(CityParameterType.Renda)} {CostText(display, cost)}";

    // Formato completo, usado no popup de detalhe da carta.
    public static string CardRequirementsFull(ParameterDisplayData display, float requiredResearch, float cost) => IsPt
        ? $"{ParameterIcon(CityParameterType.Pesquisa)} mín.: {ResearchText(display, requiredResearch)} · {ParameterIcon(CityParameterType.Renda)} {CostText(display, cost)}"
        : $"{ParameterIcon(CityParameterType.Pesquisa)} min.: {ResearchText(display, requiredResearch)} · {ParameterIcon(CityParameterType.Renda)} {CostText(display, cost)}";

    // Icone do parametro dentro do texto, do atlas de sprites ligado ao texto da carta.
    public static string ParameterIcon(CityParameterType parameter) => $"<sprite name=\"{parameter}\">";

    // Exigencia e custo na mesma unidade que a barra superior mostra.
    private static string ResearchText(ParameterDisplayData display, float requiredResearch, bool withUnit = true) => display != null
        ? display.FormatValue(CityParameterType.Pesquisa, requiredResearch, smallUnit: false, withUnit)
        : requiredResearch.ToString("0");

    private static string CostText(ParameterDisplayData display, float cost) => display != null
        ? display.FormatAmount(CityParameterType.Renda, cost, smallUnit: false)
        : cost.ToString("0");

    public static string UnitsButtonLabel => IsPt
        ? (UnitDisplaySettings.ShowRealUnits ? "Valores: unidades reais" : "Valores: índice")
        : (UnitDisplaySettings.ShowRealUnits ? "Values: real units" : "Values: index");

    // Efeitos exatos da carta, um parametro por linha, mostrados quando a mao foi revelada.
    public static string CardEffects(CardData card)
    {
        if (card.StatEffects == null || card.StatEffects.Count == 0)
            return IsPt ? "Sem efeito direto" : "No direct effect";

        var lines = new System.Collections.Generic.List<string>();
        foreach (var effect in card.StatEffects)
            lines.Add($"{ParameterIcon(effect.Parameter)} {effect.Amount:+0;-0;0}");
        return string.Join("\n", lines);
    }

    // Popup "Por que mudou?" de um parametro.
    public static string BreakdownTitle(CityParameterType parameter) => IsPt
        ? $"Por que mudou: {parameter.GetFullName()}?"
        : $"Why did {parameter.GetFullName()} change?";

    public static string BreakdownTurnHeader(int turnIndex, string from, string to) => IsPt
        ? $"No turno {turnIndex}, de {from} a {to}"
        : $"On turn {turnIndex}, from {from} to {to}";

    public static string BreakdownTotalChange => IsPt ? "Variação no turno" : "Change this turn";

    public static string BreakdownNoData => IsPt
        ? "A explicação aparece depois que um turno termina."
        : "The breakdown appears once a turn has ended.";

    public static string BreakdownNoChange => IsPt ? "Nada mudou neste turno." : "Nothing changed this turn.";

    public static string ContributionLabel(ContributionSource source, CityParameterType from, int collapsedCount)
    {
        switch (source)
        {
            case ContributionSource.Card: return IsPt ? "Efeito das cartas" : "Card effects";
            case ContributionSource.CardCost: return IsPt ? "Custo das cartas" : "Card costs";
            case ContributionSource.Event: return IsPt ? "Evento" : "Event";
            case ContributionSource.Interaction: return IsPt
                ? $"Efeito de {ParameterIcon(from)} {from.GetFullName()}"
                : $"Effect of {ParameterIcon(from)} {from.GetFullName()}";
            case ContributionSource.Deriva: return IsPt ? "Manutenção (desgaste do turno)" : "Upkeep (wear each turn)";
            case ContributionSource.ExcessCorrection: return IsPt ? "Correção do excesso" : "Excess correction";
            case ContributionSource.CollapsePenalty: return IsPt
                ? $"Colapso de {collapsedCount} parâmetro(s)"
                : $"Collapse of {collapsedCount} parameter(s)";
            case ContributionSource.Clamp: return IsPt ? "Limite da escala" : "Scale limit";
            default: return source.ToString();
        }
    }

    public static string BreakdownOthers => IsPt ? "Outras influências" : "Other influences";

    // Composicao do Bem-estar, que e recalculado do zero a cada turno.
    public static string WellbeingAverage => IsPt
        ? $"Média de {ParameterIcon(CityParameterType.Mobilidade)}{ParameterIcon(CityParameterType.Saude)}{ParameterIcon(CityParameterType.Seguranca)}{ParameterIcon(CityParameterType.Sustentabilidade)}"
        : $"Average of {ParameterIcon(CityParameterType.Mobilidade)}{ParameterIcon(CityParameterType.Saude)}{ParameterIcon(CityParameterType.Seguranca)}{ParameterIcon(CityParameterType.Sustentabilidade)}";

    public static string WellbeingCrowding => IsPt
        ? $"Adensamento ({ParameterIcon(CityParameterType.Populacao)} acima da metade)"
        : $"Crowding ({ParameterIcon(CityParameterType.Populacao)} above half)";

    public static string WellbeingBonus => IsPt ? "Cartas e eventos acumulados" : "Cards and events so far";

    public static string WellbeingAfterResolution => IsPt ? "Depois da resolução" : "After resolution";

    public static string WellbeingEventThisTurn => IsPt ? "Evento deste turno" : "Event this turn";

    public static string WellbeingExplanation => IsPt
        ? "O Bem-estar é recalculado a cada turno a partir dos outros parâmetros."
        : "Well-being is recalculated every turn from the other parameters.";

    public static string BreakdownMapButton => IsPt ? "Ver no mapa" : "See on map";

    public static string RelationMapTitle => IsPt ? "Mapa de relações" : "Relationship map";

    public static string RelationMapIntro => IsPt
        ? "Cada seta é um parâmetro influenciando outro a cada turno. Verde apoia, vermelho pressiona; " +
          "a espessura mostra a força agora. Toque num parâmetro para ver o que o afeta.\n\n" +
          "<size=80%>Uma relação de apoio puxa o alvo para cima quando a fonte está acima da metade da " +
          "escala, e para baixo quando está abaixo; a pressão faz o contrário. O anel de cada parâmetro " +
          "fica vermelho na crise e laranja no excesso, zonas que mudam a força do que ele exerce. " +
          "O tracejado dourado mostra o que forma o Bem-estar.</size>"
        : "Each arrow is a parameter influencing another every turn. Green supports, red pressures; " +
          "thickness shows the strength right now. Tap a parameter to see what affects it.\n\n" +
          "<size=80%>A supporting link pulls its target up when the source is above half of its scale, " +
          "and down when it is below; pressure does the opposite. Each parameter's ring turns red in " +
          "crisis and orange in excess, zones that change the strength of what it exerts. " +
          "The dashed gold lines show what makes up Well-being.</size>";

    public static string RelationModeIncoming => IsPt ? "O que afeta" : "What affects it";
    public static string RelationModeOutgoing => IsPt ? "O que influencia" : "What it affects";

    public static string RelationIncomingHeader(CityParameterType parameter) => IsPt
        ? $"O que afeta {parameter.GetFullName()} a cada turno:"
        : $"What affects {parameter.GetFullName()} each turn:";

    public static string RelationOutgoingHeader(CityParameterType parameter) => IsPt
        ? $"O que {parameter.GetFullName()} influencia a cada turno:"
        : $"What {parameter.GetFullName()} affects each turn:";

    public static string RelationWellbeingHeader => IsPt
        ? "O Bem-estar é recalculado a cada turno a partir de:"
        : "Well-being is recalculated every turn from:";

    public static string RelationNoLinks => IsPt ? "Nenhuma relação nesta direção." : "No links in this direction.";

    public static string RelationReversedHint => IsPt
        ? "Apoio negativo ou pressão positiva: a fonte está abaixo da metade da escala."
        : "Negative support or positive pressure: the source is below half of its scale.";

    public static string RelationReversedNote(CityParameterType source, bool support) => IsPt
        ? $"{source.GetFullName()} está abaixo da metade da escala, então {(support ? "este apoio está puxando para baixo" : "esta pressão está aliviando")}."
        : $"{source.GetFullName()} is below half of its scale, so {(support ? "this support is pulling down" : "this pressure is easing off")}.";

    public static string RelationPairHint => IsPt
        ? "Toque em outro parâmetro para ver por que eles se afetam."
        : "Tap another parameter to see why they affect each other.";

    public static string RelationLabel(bool support, RelationStrength strength)
    {
        if (IsPt)
        {
            var word = support ? "apoio" : "pressão";
            switch (strength)
            {
                case RelationStrength.Fraca: return $"{word} fraco{(support ? "" : "a")}";
                case RelationStrength.Media: return $"{word} médi{(support ? "o" : "a")}";
                default: return $"{word} forte";
            }
        }
        var kind = support ? "support" : "pressure";
        switch (strength)
        {
            case RelationStrength.Fraca: return $"weak {kind}";
            case RelationStrength.Media: return $"moderate {kind}";
            default: return $"strong {kind}";
        }
    }

    public static string WellbeingPartAverage => IsPt ? "¼ da média" : "¼ of the average";
    public static string WellbeingPartCrowding => IsPt ? "adensamento" : "crowding";

    public static string ZoneName(InteractionZone zone)
    {
        switch (zone)
        {
            case InteractionZone.Crise: return IsPt ? "crise" : "crisis";
            case InteractionZone.Excesso: return IsPt ? "excesso" : "excess";
            default: return IsPt ? "saudável" : "healthy";
        }
    }

    public static string RelationNodeHeader(CityParameterType parameter, string level, InteractionZone zone) => IsPt
        ? $"{ParameterIcon(parameter)} <b>{parameter.GetFullName()}</b>: {level}, zona {ZoneName(zone)}"
        : $"{ParameterIcon(parameter)} <b>{parameter.GetFullName()}</b>: {level}, {ZoneName(zone)} zone";

    public static string RelationPairTitle(CityParameterType from, CityParameterType to) =>
        $"{ParameterIcon(from)} <b>{from.GetFullName()}</b> » {ParameterIcon(to)} <b>{to.GetFullName()}</b>";

    public static string RelationNone(CityParameterType a, CityParameterType b) => IsPt
        ? $"{a.GetFullName()} e {b.GetFullName()} não se afetam diretamente. Toque de novo para voltar."
        : $"{a.GetFullName()} and {b.GetFullName()} do not affect each other directly. Tap again to go back.";

    public static string RelationNow(string amount, CityParameterType target) => IsPt
        ? $"Agora: {amount} por turno em {target.GetFullName()}"
        : $"Now: {amount} per turn on {target.GetFullName()}";

    public static string RelationPartNow(string amount) => IsPt
        ? $"Agora: {amount} no Bem-estar"
        : $"Now: {amount} on Well-being";

    public static string RelationReference(string reference) => IsPt ? $"Fonte: {reference}" : $"Source: {reference}";

    // Como a zona da fonte muda a forca da relacao, com o fator em porcentagem.
    public static string RelationZoneNote(CityParameterType source, InteractionZone zone, float factor, bool support)
    {
        var percent = (int)System.Math.Round(factor * 100f);
        var name = source.GetFullName();
        if (IsPt)
        {
            var kind = support ? "o apoio que exerce" : "a pressão que exerce";
            var change = factor > 1f ? $"fica {percent - 100}% mais forte" : $"cai para {percent}% do normal";
            return $"{name} está em {ZoneName(zone)}: {kind} {change}.";
        }
        var kindEn = support ? "the support it gives" : "the pressure it exerts";
        var changeEn = factor > 1f ? $"is {percent - 100}% stronger" : $"drops to {percent}% of normal";
        return $"{name} is in {ZoneName(zone)}: {kindEn} {changeEn}.";
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

    public static string KnowledgeButton => IsPt ? "Saiba mais" : "Learn more";
    public static string KnowledgeSummaryHeader => IsPt ? "O que é" : "What it is";
    public static string KnowledgeRealWorldHeader => IsPt ? "Onde existe de verdade" : "In the real world";
    public static string KnowledgeTradeOffsHeader => IsPt ? "Custos e riscos" : "Costs and risks";
    public static string KnowledgeSourcesHeader => IsPt ? "Fontes" : "Sources";
    public static string KnowledgeRelatedHeader => IsPt ? "Veja também" : "See also";
    public static string KnowledgeLockedEntry => "???";

    public static string KnowledgeCategoryName(KnowledgeCategory category)
    {
        switch (category)
        {
            case KnowledgeCategory.Card:
                return IsPt ? "Carta" : "Card";
            case KnowledgeCategory.Event:
                return IsPt ? "Evento" : "Event";
            case KnowledgeCategory.Parameter:
                return IsPt ? "Parâmetro da cidade" : "City parameter";
            default:
                return IsPt ? "Conceito" : "Concept";
        }
    }

    public static string AlmanacTitle => IsPt ? "Almanaque" : "Almanac";
    public static string AlmanacButton => IsPt ? "Almanaque" : "Almanac";
    public static string AlmanacTabCards => IsPt ? "Cartas" : "Cards";
    public static string AlmanacTabEvents => IsPt ? "Eventos" : "Events";
    public static string AlmanacTabConcepts => IsPt ? "Conceitos" : "Concepts";

    public static string AlmanacCounter(int unlocked, int total) => IsPt
        ? $"{unlocked}/{total} fichas descobertas"
        : $"{unlocked}/{total} entries found";

    public static string AlmanacRevealSetting => IsPt ? "Revelar almanaque" : "Reveal almanac";
    public static string AlmanacResetSetting => IsPt ? "Zerar almanaque" : "Reset almanac";
    public static string AlmanacConfirmSetting => IsPt ? "Toque de novo para confirmar" : "Tap again to confirm";
    public static string AlmanacRevealedSetting => IsPt ? "Almanaque revelado" : "Almanac revealed";
    public static string AlmanacResetDoneSetting => IsPt ? "Almanaque zerado" : "Almanac reset";

    public static string AlmanacHint => IsPt
        ? "Jogue cartas, enfrente eventos e explore a cidade para descobrir novas fichas."
        : "Play cards, face events and explore the city to find new entries.";

    public static string AlmanacNewEntry(string title) => IsPt
        ? $"Nova ficha no almanaque: {title}"
        : $"New almanac entry: {title}";

    public static string AlmanacNewEntries(int count) => IsPt
        ? $"{count} novas fichas no almanaque"
        : $"{count} new almanac entries";

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
