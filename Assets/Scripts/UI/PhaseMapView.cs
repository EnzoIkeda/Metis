using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Mostra um botao por no do mapa de fase. No Beta e sempre 1 (avancar), mas ja suporta N nos.
public class PhaseMapView : MonoBehaviour
{
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Button _nodeButtonTemplate;
    [SerializeField] private Transform _nodeContainer;

    private void Start()
    {
        if (_titleText != null)
            _titleText.text = UIStrings.PhaseMapTitle;

        _nodeButtonTemplate.gameObject.SetActive(false);

        foreach (var node in PhaseMapProvider.GetNodes())
        {
            var button = Instantiate(_nodeButtonTemplate, _nodeContainer);
            button.gameObject.SetActive(true);
            button.interactable = node.IsAvailable;

            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = UIStrings.PhaseMapNodeLabel(node.Id);

            var nodeId = node.Id;
            button.onClick.AddListener(() => SelectNode(nodeId));
        }
    }

    private static void SelectNode(string nodeId)
    {
        MetaProgressionManager.IncrementPhasesCompleted();
        SceneManager.LoadScene("City_Scene");
    }
}
