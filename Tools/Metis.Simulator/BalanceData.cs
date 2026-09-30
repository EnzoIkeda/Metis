using System.Text.Json;

namespace Metis.Simulator;

// Carta lida do JSON exportado pelo Editor.
public sealed class SimCard : ICardDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public float Cost { get; init; }
    public CardTier Tier { get; init; }
    public CardArchetype Archetype { get; init; }
    public float RequiredPesquisa { get; init; }
    public bool PlacesStructure { get; init; }
    public IReadOnlyList<StatModifier> StatEffects { get; init; } = Array.Empty<StatModifier>();
}

public sealed class SimEvent : IRandomEventDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public IReadOnlyList<TriggerCondition> TriggerConditions { get; init; } = Array.Empty<TriggerCondition>();
    public IReadOnlyList<StatModifier> StatEffects { get; init; } = Array.Empty<StatModifier>();
    public int MinTurn { get; init; } = 1;
    public int MaxTurn { get; init; } = 999;
}

public sealed class SimAdvantage : IAdvantageDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<StatModifier> StatEffects { get; init; } = Array.Empty<StatModifier>();
}

public sealed class SimPreset
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyDictionary<CityParameterType, float> Overrides { get; init; } = new Dictionary<CityParameterType, float>();
}

public sealed class SimRules
{
    public int HandSize { get; init; } = 5;
    public int VictoryTurnCount { get; init; } = TurnMachine.DefaultVictoryTurnCount;
    public int RewardOptionCount { get; init; } = 3;
}

public sealed class SimInteraction
{
    public float ValorNeutro { get; init; } = 50f;
    public float EscalaGlobal { get; init; } = 1f;
    public float ZonaCrise { get; init; } = 25f;
    public float ZonaExcesso { get; init; } = 75f;
    public float MultiplicadorPressaoCrise { get; init; } = 1f;
    public float MultiplicadorApoioCrise { get; init; } = 1f;
    public float MultiplicadorApoioExcesso { get; init; } = 1f;
    public float DerivaCorrecaoExcesso { get; init; }
    public float PenalidadeColapso { get; init; }
}

// Todos os dados de uma configuracao do jogo, no formato que o simulador consome.
public sealed class BalanceData
{
    public SimRules Rules { get; init; } = new SimRules();
    public IReadOnlyList<CityParameterConfig> Parameters { get; init; } = Array.Empty<CityParameterConfig>();
    public SimInteraction Interaction { get; init; } = new SimInteraction();
    public IReadOnlyList<SimCard> Cards { get; init; } = Array.Empty<SimCard>();
    public IReadOnlyList<SimEvent> Events { get; init; } = Array.Empty<SimEvent>();
    public IReadOnlyList<SimAdvantage> Advantages { get; init; } = Array.Empty<SimAdvantage>();
    public IReadOnlyList<SimPreset> Presets { get; init; } = Array.Empty<SimPreset>();

    // Limiares de Pesquisa que liberam tiers novos, em ordem crescente.
    public IReadOnlyList<float> TierThresholds => Cards
        .Select(card => card.RequiredPesquisa)
        .Where(value => value > 0f)
        .Distinct()
        .OrderBy(value => value)
        .ToList();

    public CityParameterConfig GetParameterConfig(CityParameterType parameter)
    {
        return Parameters.First(config => config.Parameter == parameter);
    }

    // Valores iniciais da fase, com os overrides do preset aplicados por cima quando houver um.
    public CityParameterConfig[] BuildParameterConfigs(SimPreset preset)
    {
        var configs = Parameters.ToArray();
        if (preset == null)
            return configs;

        for (int i = 0; i < configs.Length; i++)
        {
            if (preset.Overrides.TryGetValue(configs[i].Parameter, out var initialValue))
                configs[i].InitialValue = initialValue;
        }
        return configs;
    }

    public InteractionMatrix BuildInteractionMatrix()
    {
        return new InteractionMatrix(
            Interaction.ValorNeutro,
            Interaction.EscalaGlobal,
            Interaction.ZonaCrise,
            Interaction.ZonaExcesso,
            Interaction.MultiplicadorPressaoCrise,
            Interaction.MultiplicadorApoioCrise,
            Interaction.MultiplicadorApoioExcesso,
            Interaction.DerivaCorrecaoExcesso);
    }

    public static BalanceData Load(string path)
    {
        return Parse(File.ReadAllText(path));
    }

    public static BalanceData Parse(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dto = JsonSerializer.Deserialize<BalanceDataDto>(json, options)
            ?? throw new InvalidDataException("JSON de balanceamento vazio.");

        return new BalanceData
        {
            Rules = dto.Rules ?? new SimRules(),
            Interaction = dto.Interaction ?? new SimInteraction(),
            Parameters = dto.Parameters.Select(parameter => new CityParameterConfig
            {
                Parameter = ParseEnum<CityParameterType>(parameter.Parameter),
                InitialValue = parameter.InitialValue,
                MinValue = parameter.MinValue,
                MaxValue = parameter.MaxValue,
                CriticalLevel = parameter.CriticalLevel,
                Deriva = parameter.Deriva,
            }).ToList(),
            Cards = dto.Cards.Select(card => new SimCard
            {
                Id = card.Id,
                Name = card.Name,
                Cost = card.Cost,
                Tier = ParseEnum<CardTier>(card.Tier),
                Archetype = ParseEnum<CardArchetype>(card.Archetype),
                RequiredPesquisa = card.RequiredPesquisa,
                PlacesStructure = card.PlacesStructure,
                StatEffects = ToModifiers(card.Effects),
            }).ToList(),
            Events = dto.Events.Select(randomEvent => new SimEvent
            {
                Id = randomEvent.Id,
                Title = randomEvent.Title,
                MinTurn = randomEvent.MinTurn,
                MaxTurn = randomEvent.MaxTurn,
                StatEffects = ToModifiers(randomEvent.Effects),
                TriggerConditions = randomEvent.Conditions.Select(condition => new TriggerCondition
                {
                    Parameter = ParseEnum<CityParameterType>(condition.Parameter),
                    Comparison = ParseEnum<ComparisonType>(condition.Comparison),
                    Threshold = condition.Threshold,
                }).ToList(),
            }).ToList(),
            Advantages = dto.Advantages.Select(advantage => new SimAdvantage
            {
                Id = advantage.Id,
                Name = advantage.Name,
                StatEffects = ToModifiers(advantage.Effects),
            }).ToList(),
            Presets = dto.Presets.Select(preset => new SimPreset
            {
                Id = preset.Id,
                Name = preset.Name,
                Overrides = preset.Overrides.ToDictionary(
                    overrideValue => ParseEnum<CityParameterType>(overrideValue.Parameter),
                    overrideValue => overrideValue.InitialValue),
            }).ToList(),
        };
    }

    private static List<StatModifier> ToModifiers(List<ModifierDto> modifiers)
    {
        return modifiers.Select(modifier => new StatModifier
        {
            Parameter = ParseEnum<CityParameterType>(modifier.Parameter),
            Amount = modifier.Amount,
        }).ToList();
    }

    // Por nome e nao por numero, pra um enum reordenado no jogo falhar alto aqui em vez de trocar valor em silencio.
    private static T ParseEnum<T>(string value) where T : struct, Enum
    {
        if (Enum.TryParse<T>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed))
            return parsed;
        throw new InvalidDataException($"Valor '{value}' desconhecido pra {typeof(T).Name}.");
    }

    private sealed class BalanceDataDto
    {
        public SimRules Rules { get; set; }
        public List<ParameterDto> Parameters { get; set; } = new();
        public SimInteraction Interaction { get; set; }
        public List<CardDto> Cards { get; set; } = new();
        public List<EventDto> Events { get; set; } = new();
        public List<AdvantageDto> Advantages { get; set; } = new();
        public List<PresetDto> Presets { get; set; } = new();
    }

    private sealed class ModifierDto
    {
        public string Parameter { get; set; } = "";
        public float Amount { get; set; }
    }

    private sealed class ConditionDto
    {
        public string Parameter { get; set; } = "";
        public string Comparison { get; set; } = "";
        public float Threshold { get; set; }
    }

    private sealed class ParameterDto
    {
        public string Parameter { get; set; } = "";
        public float InitialValue { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public float CriticalLevel { get; set; }
        public float Deriva { get; set; }
    }

    private sealed class CardDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Tier { get; set; } = "";
        public string Archetype { get; set; } = "";
        public float Cost { get; set; }
        public float RequiredPesquisa { get; set; }
        public bool PlacesStructure { get; set; }
        public List<ModifierDto> Effects { get; set; } = new();
    }

    private sealed class EventDto
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public int MinTurn { get; set; }
        public int MaxTurn { get; set; }
        public List<ConditionDto> Conditions { get; set; } = new();
        public List<ModifierDto> Effects { get; set; } = new();
    }

    private sealed class AdvantageDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<ModifierDto> Effects { get; set; } = new();
    }

    private sealed class OverrideDto
    {
        public string Parameter { get; set; } = "";
        public float InitialValue { get; set; }
    }

    private sealed class PresetDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<OverrideDto> Overrides { get; set; } = new();
    }
}
