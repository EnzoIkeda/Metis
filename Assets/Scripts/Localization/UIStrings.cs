// Tabela central de textos fixos de UI, nos dois idiomas suportados.
public static class UIStrings
{
    private static bool IsPt => LocalizationManager.Current == Language.Portuguese;

    public static string WelcomeTitle => IsPt ? "Bem-vindo(a) a Métis!" : "Welcome to Métis!";

    public static string WelcomeMessage => IsPt
        ? "Você administra esta cidade por 20 turnos.\n\n" +
          "Sobreviva até o fim sem deixar o Bem-estar (o coração) chegar ao nível crítico: é ele " +
          "que mede a confiança da população na gestão.\n\n" +
          "As dicas aparecem aos poucos, conforme a cidade precisar. Boa gestão!"
        : "You manage this city for 20 turns.\n\n" +
          "Survive to the end without letting Wellbeing (the heart) reach its critical level: it " +
          "measures how much the population trusts your administration.\n\n" +
          "Tips will appear little by little, as the city needs them. Good luck!";

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

    public static string PhaseMapUnknownTitle => IsPt ? "Cidade desconhecida" : "Unknown city";

    public static string PhaseMapUnknownDescription => IsPt ? "Uma cidade sorteada, com problemas próprios." : "A random city with troubles of its own.";

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

    // Relatorio de gestao no fim da fase.
    public static string ReportTitle => IsPt ? "Relatório de gestão" : "Management report";

    public static string ReportSummary(GameOutcome outcome, int turnsPlayed, int totalTurns)
    {
        if (outcome == GameOutcome.Victory)
            return IsPt
                ? $"Vitória: a cidade atravessou os {totalTurns} turnos."
                : $"Victory: the city made it through all {totalTurns} turns.";
        return IsPt
            ? $"Derrota no turno {turnsPlayed} de {totalTurns}."
            : $"Defeat on turn {turnsPlayed} of {totalTurns}.";
    }

    public static string ReportChartHint => IsPt
        ? "Toque nos ícones para mostrar ou esconder cada parâmetro. Os números no eixo marcam as decisões abaixo."
        : "Tap the icons to show or hide each parameter. The numbers on the axis mark the decisions below.";

    public static string ReportDecisionsHeader => IsPt ? "Decisões de maior impacto" : "Most important decisions";

    public static string ReportNoDecisions => IsPt
        ? "Nenhuma carta foi jogada nesta fase."
        : "No card was played in this phase.";

    public static string ReportDecisionLine(int rank, int turnIndex, string cardName, bool helped) => IsPt
        ? $"{rank}. Turno {turnIndex}, <b>{cardName}</b>: {(helped ? "ajudou" : "atrapalhou")}"
        : $"{rank}. Turn {turnIndex}, <b>{cardName}</b>: {(helped ? "helped" : "hurt")}";

    public static string ReportCollapseHeader(GameOutcome outcome) => outcome == GameOutcome.GameOver
        ? (IsPt ? "O que levou ao fim" : "What led to the end")
        : (IsPt ? "Colapsos na fase" : "Collapses in this phase");

    public static string ReportNoCollapse => IsPt
        ? "Nenhum parâmetro entrou em Colapso."
        : "No parameter collapsed.";

    public static string ReportCollapseLine(CityParameterType parameter, int firstTurn, int turnsPenalized, string penalty)
    {
        var name = $"{ParameterIcon(parameter)} {parameter.GetFullName()}";
        if (turnsPenalized == 0)
            return IsPt
                ? $"{name} entrou em Colapso no turno {firstTurn}."
                : $"{name} collapsed on turn {firstTurn}.";
        return IsPt
            ? $"{name} entrou em Colapso no turno {firstTurn} e tirou {penalty} do {CityStats.AnchorParameter.GetFullName()} em {turnsPenalized} turno(s)."
            : $"{name} collapsed on turn {firstTurn} and took {penalty} from {CityStats.AnchorParameter.GetFullName()} over {turnsPenalized} turn(s).";
    }

    public static string ReportAnchorCritical(int turnIndex) => IsPt
        ? $"{ParameterIcon(CityStats.AnchorParameter)} {CityStats.AnchorParameter.GetFullName()} chegou ao nível crítico no turno {turnIndex}."
        : $"{ParameterIcon(CityStats.AnchorParameter)} {CityStats.AnchorParameter.GetFullName()} reached its critical level on turn {turnIndex}.";

    public static string ReportAlternativeHeader => IsPt ? "Outra jogada possível" : "Another possible play";

    public static string ReportAlternative(int turnIndex, string betterCard, string playedCard, bool avoidsGameOver)
    {
        if (avoidsGameOver)
            return IsPt
                ? $"No turno {turnIndex}, <b>{betterCard}</b> teria evitado que {playedCard} levasse o {CityStats.AnchorParameter.GetFullName()} ao nível crítico."
                : $"On turn {turnIndex}, <b>{betterCard}</b> would have kept {playedCard} from pushing {CityStats.AnchorParameter.GetFullName()} to its critical level.";
        return IsPt
            ? $"No turno {turnIndex}, <b>{betterCard}</b> deixaria a cidade em situação melhor que {playedCard}."
            : $"On turn {turnIndex}, <b>{betterCard}</b> would have left the city better off than {playedCard}.";
    }

    public static string ReportNoAlternative => IsPt
        ? "Em nenhum turno outra carta da mão faria uma diferença clara."
        : "On no turn would another card in hand have made a clear difference.";

    public static string ReportExplanation => IsPt
        ? "O impacto de uma jogada compara a cidade no fim do turno com a carta e sem ela, a partir do mesmo estado, " +
          "contando as relações entre os parâmetros e os Colapsos, mas não o evento. A outra jogada usa a mesma conta com as outras cartas que estavam na mão."
        : "A play's impact compares the city at the end of the turn with and without the card, from the same state, " +
          "counting the relationships between parameters and Collapses, but not the event. The other play uses the same reckoning with the other cards in hand.";

    public static string ReportContinueButton => IsPt ? "Continuar" : "Continue";

    // Jornal "A Tribuna do Turno".
    public static string NewsMasthead => IsPt ? "A Tribuna do Turno" : "The Turn Tribune";

    public static string NewsSlogan => IsPt
        ? "Sai a cada turno, faça chuva ou faça Colapso"
        : "Out every turn, come rain or Collapse";

    public static string NewsEditionLabel(int turnIndex) => turnIndex <= 0
        ? (IsPt ? "Edição de posse" : "Inauguration issue")
        : (IsPt ? $"Edição do turno {turnIndex}" : $"Turn {turnIndex} edition");

    public static string NewsMood(float wellbeing)
    {
        string mood;
        switch (NewsRules.MoodLevel(wellbeing))
        {
            case 4:
                mood = IsPt ? "eufórica" : "ecstatic";
                break;
            case 3:
                mood = IsPt ? "satisfeita" : "pleased";
                break;
            case 2:
                mood = IsPt ? "desconfiada" : "suspicious";
                break;
            case 1:
                mood = IsPt ? "irritada" : "grumpy";
                break;
            default:
                mood = IsPt ? "em pé de guerra" : "up in arms";
                break;
        }
        return IsPt
            ? $"{ParameterIcon(CityStats.AnchorParameter)} Humor da população: <b>{mood}</b>"
            : $"{ParameterIcon(CityStats.AnchorParameter)} Public mood: <b>{mood}</b>";
    }

    // Por que o parametro da noticia mudou, no tom do jornal.
    public static string NewsCause(ContributionSource source, CityParameterType from, bool positive)
    {
        switch (source)
        {
            case ContributionSource.Interaction:
                return IsPt
                    ? $"Segundo nossos analistas, {(positive ? "mérito" : "culpa")} de {ParameterIcon(from)} {from.GetFullName()}."
                    : $"According to our analysts, {(positive ? "credit goes to" : "blame goes to")} {ParameterIcon(from)} {from.GetFullName()}.";
            case ContributionSource.Card:
                return IsPt
                    ? (positive ? "O gabinete do prefeito jura que foi tudo planejado." : "Decisão da prefeitura, para desgosto de muita gente.")
                    : (positive ? "The mayor's office swears it was all part of the plan." : "A City Hall decision, much to everyone's dismay.");
            case ContributionSource.CardCost:
                return IsPt ? "A conta da última decisão chegou ao caixa." : "The bill for the latest decision has hit the coffers.";
            case ContributionSource.Event:
                return IsPt
                    ? (positive ? "Sorte grande: obra do acaso do turno." : "Culpa do imprevisto do turno, ninguém viu chegando.")
                    : (positive ? "Pure luck: this turn's surprise did it." : "Blame this turn's surprise, nobody saw it coming.");
            case ContributionSource.Deriva:
                return IsPt ? "Desgaste natural, explicam os técnicos, pela vigésima vez." : "Natural wear and tear, the experts explain, for the twentieth time.";
            case ContributionSource.ExcessCorrection:
                return IsPt ? "O excesso está se acomodando sozinho, como sempre acontece." : "The excess is settling down on its own, as it always does.";
            default:
                return string.Empty;
        }
    }

    public static string NewsEmpty => IsPt
        ? "A redação ainda está tomando café. A primeira edição sai em breve."
        : "The newsroom is still having coffee. The first issue is coming soon.";

    public static string NewsPreviousButton => IsPt ? "Anterior" : "Previous";
    public static string NewsNextButton => IsPt ? "Próxima" : "Next";
    public static string NewsCloseButton => IsPt ? "Fechar" : "Close";
    public static string NewsWhyButton => IsPt ? "Por que mudou?" : "Why did it change?";
    public static string NewsLearnMoreButton => IsPt ? "Saiba mais" : "Learn more";

    public static string NewsCitizenQuote(NewsCitizen citizen, string quote)
    {
        string name;
        switch (citizen)
        {
            case NewsCitizen.DonaCida:
                name = IsPt ? "Dona Cida, da padaria" : "Mrs. Cida, from the bakery";
                break;
            case NewsCitizen.SeuArnaldo:
                name = IsPt ? "Seu Arnaldo, taxista" : "Arnaldo, cab driver";
                break;
            default:
                name = IsPt ? "Júlia, da escola técnica" : "Júlia, from the technical school";
                break;
        }
        return $"<b>{name}:</b> {quote}";
    }

    public static string NewsColumnTitle(NewsColumnKind kind)
    {
        switch (kind)
        {
            case NewsColumnKind.Classified: return IsPt ? "Classificados" : "Classifieds";
            case NewsColumnKind.Horoscope: return IsPt ? "Horóscopo da gestão" : "Management horoscope";
            case NewsColumnKind.PollutionForecast: return IsPt ? "Previsão da poluição" : "Pollution forecast";
            default: return string.Empty;
        }
    }

    public static string ModelReportTitle => IsPt ? "RELATÓRIO TÉCNICO 01" : "TECHNICAL REPORT 01";
    public static string ModelReportButton => IsPt ? "Relatório técnico" : "Technical report";
    public static string ModelReportWelcomeButton => IsPt ? "Ler o relatório técnico" : "Read the technical report";

    public static string MainMenuScenarios => IsPt ? "Cenários" : "Scenarios";

    public static string ScenarioSelectionTitle => IsPt ? "Cenários" : "Scenarios";

    public static string ScenarioSelectionHint => IsPt
        ? "Cidades inspiradas em dados reais. Sobreviva e cumpra os objetivos para ganhar estrelas."
        : "Cities inspired by real data. Survive and meet the objectives to earn stars.";

    public static string ScenarioPlayButton => IsPt ? "Jogar" : "Play";

    public static string ScenarioRestartButton => IsPt ? "Recomeçar" : "Restart";

    public static string ScenarioBackButton => IsPt ? "Voltar" : "Back";

    public static string ScenarioFreeDeck => IsPt ? "Baralho à sua escolha" : "Deck of your choice";

    public static string ScenarioFixedDeck(CardArchetype archetype) => IsPt
        ? $"Baralho: {ArchetypeName(archetype)}"
        : $"Deck: {ArchetypeName(archetype)}";

    public static string ScenarioBestStars(int stars, int maxStars) => IsPt
        ? $"Melhor resultado: {stars} de {maxStars} estrelas"
        : $"Best result: {stars} of {maxStars} stars";

    public static string ScenarioObjectivesTitle => IsPt ? "Objetivos" : "Objectives";

    public static string ScenarioObjectivesHint => IsPt
        ? "Sobreviver aos 20 turnos vale 1 estrela, e cada objetivo cumprido vale mais 1."
        : "Surviving the 20 turns is worth 1 star, and each objective met is worth 1 more.";

    public static string ScenarioCurrentValue(string value) => IsPt ? $"Agora: {value}" : $"Now: {value}";

    public static string ScenarioStateLabel(ObjectiveState state)
    {
        switch (state)
        {
            case ObjectiveState.Achieved: return IsPt ? "Cumprido" : "Met";
            case ObjectiveState.Failed: return IsPt ? "Não cumprido" : "Missed";
            default: return IsPt ? "Em andamento" : "In progress";
        }
    }

    public static string ScenarioObjectiveLine(ParameterDisplayData display, ScenarioObjective objective)
    {
        var name = $"{ParameterIcon(objective.Parameter)} {objective.Parameter.GetFullName()}";
        var value = display != null ? display.FormatValue(objective.Parameter, objective.Threshold, smallUnit: false) : objective.Threshold.ToString("0");
        var atLeast = objective.Comparison == ObjectiveComparison.AtLeast;
        if (objective.MustHold)
            return IsPt
                ? $"{name}: manter em {value} ou {(atLeast ? "mais" : "menos")} do turno {objective.ByTurn} até o fim"
                : $"{name}: stay at {value} or {(atLeast ? "more" : "less")} from turn {objective.ByTurn} to the end";
        return IsPt
            ? $"{name}: {(atLeast ? "chegar a" : "baixar para")} {value} ou {(atLeast ? "mais" : "menos")} até o turno {objective.ByTurn}"
            : $"{name}: {(atLeast ? "reach" : "bring down to")} {value} or {(atLeast ? "more" : "less")} by turn {objective.ByTurn}";
    }

    public static string ReportObjectivesHeader => IsPt ? "Objetivos do cenário" : "Scenario objectives";

    public static string ReportScenarioStars(GameOutcome outcome, int stars, int maxStars)
    {
        if (outcome != GameOutcome.Victory)
            return IsPt ? "Sem estrelas: elas só contam quando a cidade sobrevive à fase." : "No stars: they only count when the city survives the phase.";
        return IsPt ? $"Resultado: {stars} de {maxStars} estrelas." : $"Result: {stars} of {maxStars} stars.";
    }

    public static string ThemeLearnMoreButton => IsPt ? "Saiba mais sobre esta cidade" : "Learn more about this city";

    public static string ModelReportFile(string modelVersion) => IsPt
        ? $"Modelo da cidade, versão {modelVersion}"
        : $"City model, version {modelVersion}";

    public static string ModelReportPage(int page, int total) => IsPt ? $"Folha {page} de {total}" : $"Sheet {page} of {total}";

    public static string ModelReportNotes => IsPt ? "Observações" : "Notes";

    // Formato com {0} pro numero da observacao.
    public static string ModelReportNoteFormat => IsPt ? "Obs. {0}" : "Note {0}";
    public static string ModelReportSeeAlso => IsPt ? "Ver também" : "See also";
    public static string ModelReportAutoTag => IsPt ? "Tabela montada com os dados do modelo" : "Table built from the model's data";
    public static string ModelReportMapLink => IsPt ? "Mapa de relações" : "Relation map";
    public static string ModelReportLockedLink => IsPt ? "??? (ficha não descoberta)" : "??? (entry not found yet)";

    public static string TutorialOkButton => IsPt ? "Entendi" : "Got it";
    public static string TutorialReplay => IsPt ? "Rever tutorial" : "Replay tutorial";
    public static string TutorialReplayDone => IsPt ? "As dicas vão aparecer de novo" : "Tips will appear again";

    public static string TutorialTitle(TutorialTip tip)
    {
        switch (tip)
        {
            case TutorialTip.Parameters: return IsPt ? "Os parâmetros da cidade" : "The city's parameters";
            case TutorialTip.Cards: return IsPt ? "Uma carta por turno" : "One card per turn";
            case TutorialTip.PassTurn: return IsPt ? "Sem carta para jogar" : "No card to play";
            case TutorialTip.Event: return IsPt ? "Eventos" : "Events";
            case TutorialTip.Crisis: return IsPt ? "Parâmetro em crise" : "Parameter in crisis";
            case TutorialTip.Collapse: return IsPt ? "Colapso" : "Collapse";
            case TutorialTip.Tier: return IsPt ? "Novas tecnologias" : "New technologies";
            case TutorialTip.Newspaper: return IsPt ? "A Tribuna do Turno" : "The Turn Tribune";
            default: return string.Empty;
        }
    }

    public static string TutorialMessage(TutorialTip tip)
    {
        switch (tip)
        {
            case TutorialTip.Parameters:
                return IsPt
                    ? "Estes são os parâmetros da cidade. Eles influenciam uns aos outros, então mexer em um também mexe nos demais."
                    : "These are the city's parameters. They affect one another, so changing one also changes the others.";
            case TutorialTip.Cards:
                return IsPt
                    ? "Toque em uma carta para ver os detalhes e arraste-a para fora da mão para jogá-la. As cartas custam Renda."
                    : "Tap a card to see its details and drag it out of your hand to play it. Cards cost Income.";
            case TutorialTip.PassTurn:
                return IsPt
                    ? "Nenhuma carta da mão pode ser jogada agora. Use este botão para passar a vez."
                    : "No card in your hand can be played right now. Use this button to pass your turn.";
            case TutorialTip.Event:
                return IsPt
                    ? "Depois de cada jogada, um evento pode mudar a situação da cidade. Alguns só acontecem em certos momentos ou condições."
                    : "After each play, an event may change the city's situation. Some only happen at certain moments or under certain conditions.";
            case TutorialTip.Crisis:
                return IsPt
                    ? "Um parâmetro entrou na zona de crise. Nessa faixa, as influências dele sobre os outros mudam de força. Vale agir antes que piore."
                    : "A parameter has entered the crisis zone. In this range, its influence on the others changes in strength. Act before it gets worse.";
            case TutorialTip.Collapse:
                return IsPt
                    ? "Um parâmetro chegou ao nível crítico e entrou em Colapso: enquanto ele não se recuperar, o Bem-estar cai a cada turno. Se o Bem-estar chegar ao crítico, a fase termina."
                    : "A parameter has reached its critical level and collapsed: until it recovers, Wellbeing drops every turn. If Wellbeing reaches its critical level, the phase ends.";
            case TutorialTip.Tier:
                return IsPt
                    ? "O investimento em Pesquisa liberou um novo nível de tecnologia. Cartas mais avançadas já podem aparecer na sua mão."
                    : "Investing in Research has unlocked a new technology level. More advanced cards can now appear in your hand.";
            case TutorialTip.Newspaper:
                return IsPt
                    ? "O jornal da cidade comenta cada turno. O selo aceso no botão avisa que há uma edição nova."
                    : "The city's newspaper covers every turn. The lit badge on the button means there is a new edition.";
            default:
                return string.Empty;
        }
    }
}
