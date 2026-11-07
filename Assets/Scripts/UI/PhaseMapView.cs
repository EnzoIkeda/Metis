using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Mapa entre fases: um botao por destino oferecido, sobre o mapa animado.
public class PhaseMapView : MonoBehaviour
{
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private PhaseMapNodeView _nodeTemplate;
    [SerializeField] private Transform _nodeContainer;

    [SerializeField] private ImageFrameAnimator _mapAnimator;
    [SerializeField] private Sprite[] _mapFrames = Array.Empty<Sprite>();
    [SerializeField] private float _mapFramesPerSecond = 12f;

    [SerializeField] private Sprite _unknownPin;
    [SerializeField] private Sprite _themedPin;
    [SerializeField] private Sprite _unavailablePin;

    private readonly List<PhaseMapNodeView> _nodes = new List<PhaseMapNodeView>();

    private void Start()
    {
        // Continuar a rodada a partir daqui volta pro mapa, nao pra fase ja vencida.
        MetaProgressionManager.EnterPhaseMap();

        if (_titleText != null)
            _titleText.text = UIStrings.PhaseMapTitle;
        if (_mapAnimator != null)
            _mapAnimator.Play(_mapFrames, _mapFramesPerSecond);

        ShowNodes(MetaProgressionManager.GetMapOffer(Array.Empty<string>(), new System.Random()));
    }

    public void ShowNodes(IReadOnlyList<PhaseMapNode> nodes)
    {
        _nodeTemplate.gameObject.SetActive(false);
        foreach (var existing in _nodes)
            Destroy(existing.gameObject);
        _nodes.Clear();

        foreach (var node in nodes)
        {
            var view = Instantiate(_nodeTemplate, _nodeContainer);
            view.gameObject.SetActive(true);
            var chosen = node;
            view.Bind(UIStrings.PhaseMapNodeTitle(node), UIStrings.PhaseMapNodeDescription(node), PinFor(node), node.IsAvailable, () => SelectNode(chosen));
            _nodes.Add(view);
        }
    }

    private Sprite PinFor(PhaseMapNode node)
    {
        if (node.IsAvailable == false)
            return _unavailablePin;
        return node.Type == PhaseMapNodeType.Themed ? _themedPin : _unknownPin;
    }

    private static void SelectNode(PhaseMapNode node)
    {
        MetaProgressionManager.ChooseMapNode(node);
        SceneManager.LoadScene(MetaProgressionManager.CitySceneName);
    }
}
