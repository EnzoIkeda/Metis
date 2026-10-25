using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Popup "Saiba mais": mostra uma ficha com as secoes, os botoes das fontes e as fichas relacionadas.
public class KnowledgePopupView : MonoBehaviour
{
    // Tom claro pros titulos das secoes, legivel sobre o fundo azul do popup.
    private const string HeaderColor = "#FFD54A";

    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _categoryText;
    [SerializeField] private TMP_Text _bodyText;

    // Arte da carta ou icone do parametro; some nas fichas sem imagem.
    [SerializeField] private Image _image;
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private Button _closeButton;

    // Fundo escurecido de tela inteira; tocar nele tambem fecha.
    [SerializeField] private Button _backdropButton;

    [Header("Links")]
    [SerializeField] private TMP_Text _sourcesHeaderText;
    [SerializeField] private TMP_Text _relatedHeaderText;
    [SerializeField] private RectTransform _sourcesContainer;
    [SerializeField] private RectTransform _relatedContainer;
    [SerializeField] private Button _linkTemplate;

    private readonly List<Button> _spawnedLinks = new List<Button>();

    // Abrir o link e trocado nos testes, pra nao abrir o navegador.
    private Action<string> _openUrl = Application.OpenURL;

    public KnowledgeEntryData Current { get; private set; }

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    private void Start()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Hide);
            var closeText = _closeButton.GetComponentInChildren<TMP_Text>();
            if (closeText != null)
                closeText.text = UIStrings.OkButton;
        }
        if (_backdropButton != null)
            _backdropButton.onClick.AddListener(Hide);
        if (_linkTemplate != null)
            _linkTemplate.gameObject.SetActive(false);

        if (Current == null)
            Hide();
    }

    public void Show(KnowledgeEntryData entry)
    {
        if (entry == null)
            return;

        Current = entry;
        if (_titleText != null)
            _titleText.text = entry.Title;
        if (_categoryText != null)
            _categoryText.text = UIStrings.KnowledgeCategoryName(entry.Category);
        if (_bodyText != null)
            _bodyText.text = BuildBody(entry);
        if (_image != null)
        {
            _image.sprite = entry.Image;
            _image.gameObject.SetActive(entry.Image != null);
        }

        ClearLinks();
        BuildSourceLinks(entry);
        BuildRelatedLinks(entry);

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        if (_scroll != null)
            _scroll.verticalNormalizedPosition = 1f;
    }

    public void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private static string BuildBody(KnowledgeEntryData entry)
    {
        var builder = new StringBuilder();
        AppendSection(builder, UIStrings.KnowledgeSummaryHeader, entry.Summary);
        AppendSection(builder, UIStrings.KnowledgeRealWorldHeader, entry.RealWorld);
        AppendSection(builder, UIStrings.KnowledgeTradeOffsHeader, entry.TradeOffs);
        return builder.ToString().TrimEnd();
    }

    private static void AppendSection(StringBuilder builder, string header, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        if (builder.Length > 0)
            builder.Append("\n\n");
        builder.Append($"<b><color={HeaderColor}>{header}</color></b>\n");
        builder.Append(text.Trim());
    }

    private void BuildSourceLinks(KnowledgeEntryData entry)
    {
        var count = 0;
        foreach (var source in entry.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.Url))
                continue;
            var url = source.Url.Trim();
            var label = string.IsNullOrWhiteSpace(source.Label) ? url : source.Label.Trim();
            SpawnLink(_sourcesContainer, $"{label} »", () => _openUrl(url));
            count++;
        }
        SetHeader(_sourcesHeaderText, UIStrings.KnowledgeSourcesHeader, count > 0);
    }

    // So as fichas relacionadas ja desbloqueadas aparecem, pra o almanaque nao revelar o que falta.
    private void BuildRelatedLinks(KnowledgeEntryData entry)
    {
        var count = 0;
        foreach (var related in entry.RelatedEntries)
        {
            if (related == null || related == entry || AlmanacProgress.IsUnlocked(related) == false)
                continue;
            var target = related;
            SpawnLink(_relatedContainer, target.Title, () => Show(target));
            count++;
        }
        SetHeader(_relatedHeaderText, UIStrings.KnowledgeRelatedHeader, count > 0);
    }

    private static void SetHeader(TMP_Text header, string text, bool visible)
    {
        if (header == null)
            return;
        header.text = text;
        header.gameObject.SetActive(visible);
    }

    private void SpawnLink(RectTransform container, string label, Action onClick)
    {
        if (_linkTemplate == null || container == null)
            return;

        var link = Instantiate(_linkTemplate, container);
        link.gameObject.SetActive(true);
        var text = link.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = label;
        link.onClick.AddListener(() => onClick());
        _spawnedLinks.Add(link);
    }

    private void ClearLinks()
    {
        foreach (var link in _spawnedLinks)
        {
            if (link != null)
                Destroy(link.gameObject);
        }
        _spawnedLinks.Clear();
    }
}
