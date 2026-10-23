using TMPro;
using UnityEngine.UI;

// Botao "Saiba mais" dos popups de carta e evento: so aparece quando ha ficha e popup pra mostra-la.
public static class KnowledgeButtonRules
{
    public static void Refresh(Button button, KnowledgePopupView popup, KnowledgeEntryData entry)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(entry != null && popup != null);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = UIStrings.KnowledgeButton;
    }
}
