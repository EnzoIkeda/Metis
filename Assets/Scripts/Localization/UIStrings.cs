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

    public static string RunVictoryTitle => IsPt ? "Rodada vencida!" : "Run complete!";

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

    public static string BloomButtonLabel => IsPt
        ? (BloomSettings.Enabled ? "Brilho (bloom): ligado" : "Brilho (bloom): desligado")
        : (BloomSettings.Enabled ? "Glow (bloom): on" : "Glow (bloom): off");

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

    // Efeitos imediatos de uma opcao de dilema, em indice, numa linha so.
    public static string EventChoiceEffects(System.Collections.Generic.IReadOnlyList<StatModifier> effects)
    {
        if (effects == null || effects.Count == 0)
            return IsPt ? "Sem efeito direto" : "No direct effect";

        var parts = new System.Collections.Generic.List<string>();
        foreach (var effect in effects)
            parts.Add($"{ParameterIcon(effect.Parameter)} {effect.Amount:+0;-0;0}");
        return string.Join("   ", parts);
    }

    public static string EventFollowUpOrigin(int turnIndex, string choiceLabel) => IsPt
        ? $"Consequência da sua decisão no turno {turnIndex}: {choiceLabel}"
        : $"A result of your decision on turn {turnIndex}: {choiceLabel}";

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
            case ContributionSource.Boss: return IsPt ? "Ataque do desafio final" : "Final challenge attack";
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
    public static string RelationZoneNote(CityParameterType source, InteractionZone zone, float factor, bool takes)
    {
        var percent = (int)System.Math.Round(factor * 100f);
        var name = source.GetFullName();
        if (IsPt)
        {
            var kind = takes ? "o que tira dos outros" : "o que dá aos outros";
            var change = factor > 1f ? $"fica {percent - 100}% mais forte" : $"cai para {percent}% do normal";
            return $"{name} está em {ZoneName(zone)}: {kind} {change}.";
        }
        var kindEn = takes ? "what it takes from others" : "what it gives to others";
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
            case CardArchetype.Comunitario:
                return IsPt ? "Comunitário" : "Community";
            default:
                return IsPt ? "Geral" : "General";
        }
    }

    public static string PhaseMapTitle => IsPt ? "Escolha o próximo destino" : "Choose your next destination";

    public static string PhaseMapUnknownTitle => IsPt ? "Cidade desconhecida" : "Unknown city";

    public static string PhaseMapUnknownDescription => IsPt ? "Uma cidade sorteada, com problemas próprios." : "A random city with troubles of its own.";

    public static string PhaseMapBossTitle(string bossName) => IsPt ? $"Desafio final: {bossName}" : $"Final challenge: {bossName}";

    // Chefe de fim de rodada, no HUD e no popup da jogada dele.
    public static string BossMomentumLabel => IsPt ? "Ímpeto" : "Momentum";

    public static string BossMomentumValue(int momentum, int cost) => $"{momentum}/{cost}";

    public static string BossNextCard(string cardName) => IsPt ? $"Próxima: {cardName}" : $"Next: {cardName}";

    public static string BossPlaysThisTurn => IsPt ? "Ataca neste turno" : "Strikes this turn";

    public static string BossCharging(int turns) => IsPt
        ? (turns == 1 ? "Ataca no próximo turno" : $"Ataca em {turns} turnos")
        : (turns == 1 ? "Strikes next turn" : $"Strikes in {turns} turns");

    public static string BossPlayedTitle(string bossName) => IsPt ? $"{bossName} ataca" : $"{bossName} strikes";

    public static string BossGrowingDamage(float factor) => IsPt
        ? $"Dano aumentado: ×{factor:0.0#}"
        : $"Increased damage: ×{factor:0.0#}";

    // Linha da regra especial no HUD.
    public static string BossMechanicStatus(BossMechanic mechanic, float factor, int extraCards, int costOffset)
    {
        switch (mechanic)
        {
            case BossMechanic.GrowingDamage:
                return IsPt ? $"Dano ×{factor:0.0#}" : $"Damage ×{factor:0.0#}";
            case BossMechanic.CascadeSpread:
                return IsPt ? "Apagão se espalha" : "Blackout spreads";
            case BossMechanic.ExtraCards:
                return IsPt ? $"Termos no baralho: {extraCards}" : $"Terms in deck: {extraCards}";
            case BossMechanic.CostIncrease:
                return IsPt ? $"Custo das cartas +{costOffset}" : $"Card cost +{costOffset}";
            default:
                return string.Empty;
        }
    }

    // Regra especial explicada no popup da carta do chefe.
    public static string BossMechanicRule(BossMechanic mechanic, int extraCards, string extraCardName)
    {
        switch (mechanic)
        {
            case BossMechanic.GrowingDamage:
                return IsPt ? "Cada ataque deixa o próximo mais forte." : "Each strike makes the next one stronger.";
            case BossMechanic.CascadeSpread:
                return IsPt
                    ? "A falta de Energia se espalha na hora para quem depende dela: Renda, População, Pesquisa e Mobilidade."
                    : "The Energy loss spreads at once to what depends on it: Income, Population, Research and Mobility.";
            case BossMechanic.ExtraCards:
                return IsPt
                    ? $"Cada ataque põe {extraCards} cartas {extraCardName} no seu baralho."
                    : $"Each strike puts {extraCards} {extraCardName} cards in your deck.";
            case BossMechanic.CostIncrease:
                return IsPt
                    ? "Os ataques deixam suas cartas mais caras até o fim da fase."
                    : "The strikes make your cards more expensive until the end of the phase.";
            default:
                return string.Empty;
        }
    }

    public static string BossExtraCardsAdded(int count, string cardName) => IsPt
        ? $"+{count} {cardName} no seu baralho"
        : $"+{count} {cardName} in your deck";

    public static string BossCostIncreased(int offset) => IsPt
        ? $"Suas cartas custam +{offset} até o fim da fase"
        : $"Your cards cost +{offset} until the end of the phase";

    public static string BossOkButton => IsPt ? "Aguentar" : "Hold on";

    public static string BossLearnMoreButton => IsPt ? "Saiba mais sobre esta crise" : "Learn more about this crisis";

    // Tela da oferta de troca, na parada do mapa antes dos destinos.
    public static string TradeHeader => IsPt ? "Proposta" : "Proposal";
    public static string TradeBenefitHeader => IsPt ? "A cidade ganha" : "The city gets";
    public static string TradeDrawbackHeader => IsPt ? "A cidade perde" : "The city gives up";
    public static string TradeDuration => IsPt ? "Vale até o fim da rodada." : "Lasts until the end of the run.";
    public static string TradeAccept => IsPt ? "Aceitar" : "Accept";
    public static string TradeDecline => IsPt ? "Recusar" : "Decline";

    // Um lado da troca em texto; cardName e o nome da carta acrescentada, quando houver.
    public static string TradeTerm(TradeTerm term, string cardName)
    {
        switch (term.Kind)
        {
            case TradeTermKind.PhaseStartEffect:
                return IsPt
                    ? $"{EventChoiceEffects(term.EffectsOrEmpty())} no início de cada fase"
                    : $"{EventChoiceEffects(term.EffectsOrEmpty())} at the start of each phase";
            case TradeTermKind.CardCostOffset:
                var cost = System.Math.Abs(term.Amount);
                return IsPt
                    ? $"Toda carta custa {cost} {ParameterIcon(CityParameterType.Renda)} {(term.Amount > 0 ? "a mais" : "a menos")}"
                    : $"Every card costs {cost} {ParameterIcon(CityParameterType.Renda)} {(term.Amount > 0 ? "more" : "less")}";
            case TradeTermKind.RewardOptionOffset:
                var options = System.Math.Abs(term.Amount);
                if (IsPt)
                    return options == 1
                        ? $"1 opção de recompensa {(term.Amount > 0 ? "a mais" : "a menos")} ao vencer uma fase"
                        : $"{options} opções de recompensa {(term.Amount > 0 ? "a mais" : "a menos")} ao vencer uma fase";
                return options == 1
                    ? $"1 {(term.Amount > 0 ? "more" : "fewer")} reward option when you win a phase"
                    : $"{options} {(term.Amount > 0 ? "more" : "fewer")} reward options when you win a phase";
            case TradeTermKind.ExtraCard:
                return IsPt
                    ? $"A carta {cardName} entra no seu baralho e ocupa lugar na mão"
                    : $"The card {cardName} joins your deck and takes up room in your hand";
            default:
                return string.Empty;
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
            case CardArchetype.Comunitario:
                return IsPt
                    ? "Aposta em comunidade e meio ambiente com cartas baratas, mas abre mão da Pesquisa."
                    : "Bets on community and the environment with cheap cards, but gives up Research.";
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

    public static string ReportChoicesHeader => IsPt ? "Escolhas" : "Choices";

    public static string ReportChoiceLine(int turnIndex, string eventTitle, string choiceLabel) => IsPt
        ? $"Turno {turnIndex}, <b>{eventTitle}</b>: {choiceLabel}"
        : $"Turn {turnIndex}, <b>{eventTitle}</b>: {choiceLabel}";

    public static string ReportChoiceConsequence(int turnIndex, string eventTitle) => IsPt
        ? $"Consequência no turno {turnIndex}: {eventTitle}"
        : $"Result on turn {turnIndex}: {eventTitle}";

    public static string ReportChoiceNoConsequence => IsPt
        ? "A fase acabou antes da consequência."
        : "The phase ended before the result.";

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
            case ContributionSource.Boss:
                return IsPt ? "A crise que a cidade enfrenta cobrou mais um pedaço." : "The crisis the city is facing took another bite.";
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
            case TutorialTip.Trade: return IsPt ? "Propostas" : "Proposals";
            case TutorialTip.Boss: return IsPt ? "Desafio final" : "Final challenge";
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
            case TutorialTip.Trade:
                return IsPt
                    ? "Cada parada do mapa traz uma proposta. Aceitar dá uma vantagem e uma desvantagem até o fim da rodada; recusar não muda nada."
                    : "Every map stop brings a proposal. Accepting gives an advantage and a drawback until the end of the run; declining changes nothing.";
            case TutorialTip.Boss:
                return IsPt
                    ? "Uma crise ataca a cidade. A cada turno ela acumula Ímpeto e, quando ele basta, joga a carta mostrada aqui, depois da sua. Aguente os 20 turnos para vencer a rodada."
                    : "A crisis is hitting the city. Every turn it builds Momentum and, once there is enough, plays the card shown here, right after yours. Hold out for 20 turns to win the run.";
            default:
                return string.Empty;
        }
    }

    public static string SkipButton => IsPt ? "Pular" : "Skip";

    // Passeio a pe pela cidade.
    public static string WalkBackButton => IsPt ? "Voltar" : "Back";

    public static string WalkHint => IsPt
        ? "Arraste à esquerda para andar e à direita para olhar."
        : "Drag on the left to walk and on the right to look around.";

    // Chave da legenda de uma tela do fim de rodada; a imagem conta a partir de 0.
    public static string RunEndCaptionKey(RunEnding ending, int imageIndex) => $"{(ending == RunEnding.Defeat ? "defeat" : "victory")}.{imageIndex + 1}";

    public static string RunDefeatTitle => IsPt ? "Fim do mandato" : "End of term";

    public static string RunSummaryHeader => IsPt ? "Resumo da rodada" : "Run summary";

    public static string RunSummaryMenuButton => IsPt ? "Voltar ao menu" : "Back to menu";

    // Uma linha do resumo da rodada; names ja vem traduzidos e separados por virgula.
    public static string RunSummaryLine(RunSummaryLineKind kind, int number, CardArchetype archetype, string names)
    {
        switch (kind)
        {
            case RunSummaryLineKind.Archetype:
                return IsPt ? $"Baralho: {ArchetypeName(archetype)}" : $"Deck: {ArchetypeName(archetype)}";
            case RunSummaryLineKind.PhasesWon:
                return IsPt ? $"Fases vencidas: {number}" : $"Phases won: {number}";
            case RunSummaryLineKind.DefeatPhase:
                return IsPt ? $"Derrota na fase {number}" : $"Defeated in phase {number}";
            case RunSummaryLineKind.DefeatAtBoss:
                return IsPt ? $"Derrota para {names} na fase {number}" : $"Defeated by {names} in phase {number}";
            case RunSummaryLineKind.BossDefeated:
                return IsPt ? $"Chefe vencido: {names}" : $"Boss defeated: {names}";
            case RunSummaryLineKind.Themes:
                return IsPt ? $"Cidades temáticas: {names}" : $"Themed cities: {names}";
            case RunSummaryLineKind.RewardCards:
                return IsPt ? $"Cartas conquistadas: {names}" : $"Cards earned: {names}";
            case RunSummaryLineKind.RewardAdvantages:
                return IsPt ? $"Vantagens conquistadas: {names}" : $"Advantages earned: {names}";
            case RunSummaryLineKind.Trades:
                return IsPt ? $"Propostas aceitas: {names}" : $"Proposals accepted: {names}";
            default:
                return string.Empty;
        }
    }

    // Chave da legenda de cada tela da abertura, na ordem das imagens.
    public static string IntroCaptionKey(int index) => $"intro.{index + 1}";

    // Legendas das telas de abertura e de fim de rodada; a tela da posse cita o baralho escolhido.
    public static string SlideCaption(string key, CardArchetype archetype)
    {
        switch (key)
        {
            case "intro.1":
                return IsPt
                    ? "A cidade chegou ao limite: ruas quebradas, céu fechado e contas no vermelho."
                    : "The city has reached its limit: broken streets, gray skies and empty coffers.";
            case "intro.2":
                return IsPt
                    ? "A gestão anterior não deu conta da crise."
                    : "The previous administration could not handle the crisis.";
            case "intro.3":
                return IsPt
                    ? "Agora é a sua vez de propor outro caminho."
                    : "Now it is your turn to propose another way.";
            case "intro.4":
                return IsPt
                    ? "A prefeitura muda de mãos."
                    : "City Hall changes hands.";
            case "intro.5":
                return IsPt
                    ? "O gabinete espera por você."
                    : "The office is waiting for you.";
            case "intro.6":
                return IsPt
                    ? $"Você toma posse com uma proposta de {(archetype == CardArchetype.Comunitario ? "cidade comunitária" : ArchetypeName(archetype))}."
                    : $"You take office with a {ArchetypeName(archetype)} plan.";
            case "intro.7":
                return IsPt
                    ? "Você sonha com moradia digna para todos..."
                    : "You dream of decent housing for everyone...";
            case "intro.8":
                return IsPt
                    ? "...com parques e ar limpo..."
                    : "...of parks and clean air...";
            case "intro.9":
                return IsPt
                    ? "...com uma cidade conectada ao mundo..."
                    : "...of a city connected to the world...";
            case "intro.10":
                return IsPt
                    ? "...e com gente feliz nas ruas. Cada cidade dura 20 turnos. Boa gestão!"
                    : "...and of happy people in the streets. Each city lasts 20 turns. Good luck!";
            case "defeat.1":
                return IsPt
                    ? "A confiança da população acabou, e a cidade voltou ao limite."
                    : "The population's trust is gone, and the city is back at its limit.";
            case "defeat.2":
                return IsPt
                    ? "Agora quem está em pânico é você. A próxima gestão recebe a chave."
                    : "Now you are the one in a panic. The next administration gets the keys.";
            case "victory.6":
                return IsPt
                    ? $"A cidade atravessou a crise, e a sua proposta de {(archetype == CardArchetype.Comunitario ? "cidade comunitária" : ArchetypeName(archetype))} saiu do papel."
                    : $"The city made it through the crisis, and your {ArchetypeName(archetype)} plan came true.";
            case "victory.7":
                return IsPt
                    ? "Hoje há moradia digna para mais gente..."
                    : "Today more people have decent housing...";
            case "victory.8":
                return IsPt
                    ? "...parques e ar mais limpo..."
                    : "...parks and cleaner air...";
            case "victory.9":
                return IsPt
                    ? "...uma cidade conectada ao mundo..."
                    : "...a city connected to the world...";
            case "victory.10":
                return IsPt
                    ? "...e gente feliz nas ruas. Missão cumprida!"
                    : "...and happy people in the streets. Mission accomplished!";
            default:
                return string.Empty;
        }
    }
}
