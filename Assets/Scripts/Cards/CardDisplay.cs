using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardDisplay : MonoBehaviour
{
    [Header("Dados da Carta")]
    public CardData cardData;

    [Header("Referencias Visuais do Prefab")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI positiveEffectsText;
    [SerializeField] private TextMeshProUGUI negativeEffectsText;

    public void SetupCard(CardData data)
    {
        cardData = data;
        if (cardData == null) return;

        if (titleText != null) titleText.text = cardData.cardName;
        if (descriptionText != null) descriptionText.text = cardData.description;

        string positivos = "";
        string negativos = "";

        // Sustentabilidade
        if (cardData.impactoSustentabilidade > 0) positivos += $"+{cardData.impactoSustentabilidade} Sustentabilidade\n";
        else if (cardData.impactoSustentabilidade < 0) negativos += $"{cardData.impactoSustentabilidade} Sustentabilidade\n";

        // Satisfacao
        if (cardData.impactoSatisfacao > 0) positivos += $"+{cardData.impactoSatisfacao} Satisfacao\n";
        else if (cardData.impactoSatisfacao < 0) negativos += $"{cardData.impactoSatisfacao} Satisfacao\n";

        // Mobilidade
        if (cardData.impactoMobilidade > 0) positivos += $"+{cardData.impactoMobilidade} Mobilidade\n";
        else if (cardData.impactoMobilidade < 0) negativos += $"{cardData.impactoMobilidade} Mobilidade\n";

        // Economia
        if (cardData.custoEconomia > 0) positivos += $"+{cardData.custoEconomia} Economia\n";
        else if (cardData.custoEconomia < 0) negativos += $"{cardData.custoEconomia} Economia\n";

        if (positiveEffectsText != null) positiveEffectsText.text = positivos.TrimEnd();
        if (negativeEffectsText != null) negativeEffectsText.text = negativos.TrimEnd();
    }
}