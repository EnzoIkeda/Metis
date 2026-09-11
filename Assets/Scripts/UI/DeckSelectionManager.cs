using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tela de escolha do arquetipo de baralho, uma vez por rodada, antes da primeira fase.
public class DeckSelectionManager : MonoBehaviour
{
    [SerializeField] private TMP_Text _titleText;

    [SerializeField] private Button _sustentabilidadeButton;
    [SerializeField] private TMP_Text _sustentabilidadeText;
    [SerializeField] private TMP_Text _sustentabilidadeDescription;

    [SerializeField] private Button _industriaButton;
    [SerializeField] private TMP_Text _industriaText;
    [SerializeField] private TMP_Text _industriaDescription;

    [SerializeField] private Button _automacaoButton;
    [SerializeField] private TMP_Text _automacaoText;
    [SerializeField] private TMP_Text _automacaoDescription;

    private void Start()
    {
        if (_titleText != null)
            _titleText.text = UIStrings.DeckSelectionTitle;

        SetupOption(_sustentabilidadeButton, _sustentabilidadeText, _sustentabilidadeDescription, CardArchetype.Sustentabilidade);
        SetupOption(_industriaButton, _industriaText, _industriaDescription, CardArchetype.Industria);
        SetupOption(_automacaoButton, _automacaoText, _automacaoDescription, CardArchetype.Automacao);
    }

    private void SetupOption(Button button, TMP_Text label, TMP_Text description, CardArchetype archetype)
    {
        if (label != null)
            label.text = UIStrings.ArchetypeName(archetype);
        if (description != null)
            description.text = UIStrings.ArchetypeDescription(archetype);
        if (button != null)
            button.onClick.AddListener(() => SelectArchetype(archetype));
    }

    private static void SelectArchetype(CardArchetype archetype)
    {
        MetaProgressionManager.SetArchetype(archetype);
        SceneManager.LoadScene("City_Scene");
    }
}
