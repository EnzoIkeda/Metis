using UnityEngine;

[CreateAssetMenu(fileName = "NovaCarta", menuName = "Metis/Carta de Acao")]
public class CardData : ScriptableObject
{
    [Header("Identificacao da Carta")]
    public string cardName;
    [TextArea(2, 4)] public string description;

    [Header("Impactos nas Metricas Urbanas")]
    public int impactoSustentabilidade; // Ex: -10 ou +15
    public int impactoSatisfacao;       // Ex: +5
    public int impactoMobilidade;       // Ex: +10
    public int custoEconomia;           // Ex: -10 (Custos financeiros/orcamento)
}