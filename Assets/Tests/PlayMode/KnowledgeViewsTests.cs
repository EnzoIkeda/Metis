using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Popup "Saiba mais" e almanaque montados de verdade, com fichas criadas no teste.
public class KnowledgeViewsTests
{
    private const string TestKey = "Metis.Almanac.PlayModeTests";

    private readonly List<Object> _created = new List<Object>();
    private GameObject _canvas;

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(TestKey);
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_prefsKeyOverride", TestKey);
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_state", null);
        _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvas != null)
            Object.DestroyImmediate(_canvas);
        foreach (var created in _created)
        {
            if (created != null)
                Object.DestroyImmediate(created);
        }
        _created.Clear();
        PlayerPrefs.DeleteKey(TestKey);
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_prefsKeyOverride", null);
        TestDataFactory.SetStaticField(typeof(AlmanacProgress), "_state", null);
    }

    private KnowledgeEntryData Entry(string id, KnowledgeCategory category, params KnowledgeEntryData[] related)
    {
        var entry = ScriptableObject.CreateInstance<KnowledgeEntryData>();
        entry.name = id;
        TestDataFactory.SetField(entry, "_title", id);
        TestDataFactory.SetField(entry, "_titleEn", id);
        TestDataFactory.SetField(entry, "_summary", "Resumo " + id);
        TestDataFactory.SetField(entry, "_summaryEn", "Summary " + id);
        TestDataFactory.SetField(entry, "_category", category);
        TestDataFactory.SetField(entry, "_sources", new[] { new KnowledgeSource { Label = "Fonte", Url = "https://example.org/" + id } });
        TestDataFactory.SetField(entry, "_relatedEntries", related);
        _created.Add(entry);
        return entry;
    }

    private GameObject Ui(string name, Transform parent, params System.Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (var component in components)
            go.AddComponent(component);
        return go;
    }

    private Button LinkTemplate(Transform parent)
    {
        var button = Ui("Template", parent, typeof(Image), typeof(Button)).GetComponent<Button>();
        Ui("Label", button.transform, typeof(TextMeshProUGUI));
        return button;
    }

    private IEnumerator CreatePopup(System.Action<string> openUrl)
    {
        var host = Ui("KnowledgeController", _canvas.transform);
        host.SetActive(false);
        var view = host.AddComponent<KnowledgePopupView>();
        var panel = Ui("Panel", _canvas.transform);
        TestDataFactory.SetField(view, "_panelRoot", panel);
        TestDataFactory.SetField<TMP_Text>(view, "_titleText", Ui("Title", panel.transform, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>());
        TestDataFactory.SetField<TMP_Text>(view, "_bodyText", Ui("Body", panel.transform, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>());
        TestDataFactory.SetField<TMP_Text>(view, "_relatedHeaderText", Ui("RelatedHeader", panel.transform, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>());
        TestDataFactory.SetField(view, "_sourcesContainer", (RectTransform)Ui("Sources", panel.transform).transform);
        TestDataFactory.SetField(view, "_relatedContainer", (RectTransform)Ui("Related", panel.transform).transform);
        TestDataFactory.SetField(view, "_linkTemplate", LinkTemplate(panel.transform));
        typeof(KnowledgePopupView).GetField("_openUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(view, openUrl);
        host.SetActive(true);
        var start = Time.realtimeSinceStartup;
        while (panel.activeSelf && Time.realtimeSinceStartup - start < 5f)
            yield return null;
        _popup = view;
        _popupPanel = panel;
    }

    private KnowledgePopupView _popup;
    private GameObject _popupPanel;

    private List<Button> ActiveLinks(string container)
    {
        return _popupPanel.transform.Find(container).GetComponentsInChildren<Button>().ToList();
    }

    [UnityTest]
    public IEnumerator Popup_StartsHidden_AndShowFillsTitleBodyAndSources()
    {
        yield return CreatePopup(_ => { });
        Assert.That(_popupPanel.activeSelf, Is.False);

        var entry = Entry("Carta_VLT", KnowledgeCategory.Card);
        _popup.Show(entry);

        Assert.That(_popupPanel.activeSelf, Is.True);
        Assert.That(_popupPanel.transform.Find("Title").GetComponent<TMP_Text>().text, Is.EqualTo("Carta_VLT"));
        StringAssert.Contains("Carta_VLT", _popupPanel.transform.Find("Body").GetComponent<TMP_Text>().text);
        Assert.That(ActiveLinks("Sources").Count, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Popup_SourceButton_OpensTheUrl()
    {
        var opened = new List<string>();
        yield return CreatePopup(opened.Add);

        _popup.Show(Entry("Conceito_Colapso", KnowledgeCategory.Concept));
        ActiveLinks("Sources")[0].onClick.Invoke();

        Assert.That(opened, Is.EqualTo(new[] { "https://example.org/Conceito_Colapso" }));
    }

    [UnityTest]
    public IEnumerator Popup_ShowsOnlyUnlockedRelatedEntries_AndOpensThem()
    {
        yield return CreatePopup(_ => { });
        var unlocked = Entry("Evento_Enchente", KnowledgeCategory.Event);
        var locked = Entry("Evento_Apagao", KnowledgeCategory.Event);
        var entry = Entry("Carta_Parque", KnowledgeCategory.Card, unlocked, locked);
        AlmanacProgress.Unlock(unlocked);

        _popup.Show(entry);
        var related = ActiveLinks("Related");

        Assert.That(related.Count, Is.EqualTo(1), "a ficha bloqueada nao aparece");
        related[0].onClick.Invoke();
        Assert.That(_popup.Current, Is.SameAs(unlocked));
    }

    [UnityTest]
    public IEnumerator Popup_ShowingAgain_ReplacesTheOldLinks()
    {
        yield return CreatePopup(_ => { });
        _popup.Show(Entry("A", KnowledgeCategory.Concept));
        _popup.Show(Entry("B", KnowledgeCategory.Concept));
        yield return null;

        Assert.That(ActiveLinks("Sources").Count, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Almanac_ShowsCounterLockedEntriesAndTabs()
    {
        yield return CreatePopup(_ => { });
        var card = Entry("Carta_VLT", KnowledgeCategory.Card);
        var lockedCard = Entry("Carta_Horta", KnowledgeCategory.Card);
        var concept = Entry("Conceito_Colapso", KnowledgeCategory.Concept);
        var parameter = Entry("Parametro_Renda", KnowledgeCategory.Parameter);
        var library = ScriptableObject.CreateInstance<KnowledgeLibraryData>();
        TestDataFactory.SetField(library, "_entries", new[] { card, lockedCard, concept, parameter });
        _created.Add(library);
        AlmanacProgress.Unlock(card);
        AlmanacProgress.Unlock(parameter);

        var host = Ui("AlmanacController", _canvas.transform);
        host.SetActive(false);
        var view = host.AddComponent<AlmanacView>();
        var panel = Ui("AlmanacPanel", _canvas.transform);
        var grid = (RectTransform)Ui("Grid", panel.transform).transform;
        var counter = Ui("Counter", panel.transform, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var conceptsTab = Ui("ConceptsTab", panel.transform, typeof(Image), typeof(Button)).GetComponent<Button>();
        TestDataFactory.SetField(view, "_library", library);
        TestDataFactory.SetField(view, "_knowledgePopup", _popup);
        TestDataFactory.SetField(view, "_panelRoot", panel);
        TestDataFactory.SetField<TMP_Text>(view, "_counterText", counter);
        TestDataFactory.SetField(view, "_gridContainer", grid);
        TestDataFactory.SetField(view, "_entryTemplate", LinkTemplate(panel.transform));
        TestDataFactory.SetField(view, "_conceptsTabButton", conceptsTab);
        host.SetActive(true);
        var start = Time.realtimeSinceStartup;
        while (panel.activeSelf && Time.realtimeSinceStartup - start < 5f)
            yield return null;
        Assert.That(panel.activeSelf, Is.False);

        view.Show();
        StringAssert.Contains("2/4", counter.text);
        var cards = grid.GetComponentsInChildren<Button>();
        Assert.That(cards.Length, Is.EqualTo(2), "aba de cartas");
        Assert.That(cards.Count(button => button.interactable), Is.EqualTo(1));
        Assert.That(cards.Any(button => button.GetComponentInChildren<TMP_Text>().text == UIStrings.KnowledgeLockedEntry), Is.True);

        conceptsTab.onClick.Invoke();
        yield return null;
        var concepts = grid.GetComponentsInChildren<Button>();
        Assert.That(concepts.Length, Is.EqualTo(2), "conceitos e parametros juntos");

        concepts.First(button => button.interactable).onClick.Invoke();
        Assert.That(_popup.Current, Is.SameAs(parameter));
    }

    private Sprite TestSprite()
    {
        var texture = new Texture2D(4, 4);
        _created.Add(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        _created.Add(sprite);
        return sprite;
    }

    [UnityTest]
    public IEnumerator Popup_ShowsTheImageOnlyWhenTheEntryHasOne()
    {
        yield return CreatePopup(_ => { });
        var image = Ui("Image", _popupPanel.transform, typeof(Image)).GetComponent<Image>();
        TestDataFactory.SetField(_popup, "_image", image);
        var withImage = Entry("Parametro_Renda", KnowledgeCategory.Parameter);
        var sprite = TestSprite();
        TestDataFactory.SetField(withImage, "_image", sprite);

        _popup.Show(withImage);
        Assert.That(image.gameObject.activeSelf, Is.True);
        Assert.That(image.sprite, Is.SameAs(sprite));

        _popup.Show(Entry("Conceito_Colapso", KnowledgeCategory.Concept));
        Assert.That(image.gameObject.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator EntryView_ShowsTheIconAndMovesTheLabel_OrHidesIt()
    {
        var tile = Ui("Tile", _canvas.transform);
        var icon = Ui("Icon", tile.transform, typeof(Image)).GetComponent<Image>();
        var label = Ui("Label", tile.transform, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var view = tile.AddComponent<AlmanacEntryView>();
        TestDataFactory.SetField(view, "_icon", icon);
        TestDataFactory.SetField<TMP_Text>(view, "_label", label);
        yield return null;

        var sprite = TestSprite();
        view.Bind("Renda", sprite);
        Assert.That(icon.gameObject.activeSelf, Is.True);
        Assert.That(icon.sprite, Is.SameAs(sprite));
        Assert.That(label.text, Is.EqualTo("Renda"));
        Assert.That(label.rectTransform.offsetMin.x, Is.EqualTo(104f));

        view.Bind("???", null);
        Assert.That(icon.gameObject.activeSelf, Is.False, "ficha bloqueada nao mostra a imagem");
        Assert.That(label.rectTransform.offsetMin.x, Is.EqualTo(24f));
    }

    private (AlmanacSettingsView view, Button reveal, Button reset, KnowledgeEntryData[] entries) CreateSettings(float messageSeconds = 3f)
    {
        var entries = new[] { Entry("Carta_A", KnowledgeCategory.Card), Entry("Evento_B", KnowledgeCategory.Event) };
        var library = ScriptableObject.CreateInstance<KnowledgeLibraryData>();
        TestDataFactory.SetField(library, "_entries", entries);
        _created.Add(library);

        var host = Ui("AlmanacSettings", _canvas.transform);
        host.SetActive(false);
        var view = host.AddComponent<AlmanacSettingsView>();
        var reveal = LinkTemplate(host.transform);
        var reset = LinkTemplate(host.transform);
        TestDataFactory.SetField(view, "_library", library);
        TestDataFactory.SetField(view, "_revealButton", reveal);
        TestDataFactory.SetField(view, "_resetButton", reset);
        TestDataFactory.SetField(view, "_messageSeconds", messageSeconds);
        host.SetActive(true);
        return (view, reveal, reset, entries);
    }

    private static string Label(Button button) => button.GetComponentInChildren<TMP_Text>().text;

    private static IEnumerator WaitForLabel(Button button, string expected)
    {
        var start = Time.realtimeSinceStartup;
        while (Label(button) != expected && Time.realtimeSinceStartup - start < 5f)
            yield return null;
    }

    [UnityTest]
    public IEnumerator Settings_Reveal_NeedsASecondTap()
    {
        var (_, reveal, _, entries) = CreateSettings();
        yield return WaitForLabel(reveal, UIStrings.AlmanacRevealSetting);

        reveal.onClick.Invoke();
        Assert.That(AlmanacProgress.UnlockedCount, Is.Zero, "primeiro toque so pede confirmacao");
        Assert.That(Label(reveal), Is.EqualTo(UIStrings.AlmanacConfirmSetting));

        reveal.onClick.Invoke();
        Assert.That(entries.All(AlmanacProgress.IsUnlocked), Is.True);
        Assert.That(Label(reveal), Is.EqualTo(UIStrings.AlmanacRevealedSetting));
    }

    [UnityTest]
    public IEnumerator Settings_Reset_NeedsASecondTap()
    {
        var (_, _, reset, entries) = CreateSettings();
        yield return WaitForLabel(reset, UIStrings.AlmanacResetSetting);
        AlmanacProgress.Unlock(entries[0]);

        reset.onClick.Invoke();
        Assert.That(AlmanacProgress.UnlockedCount, Is.EqualTo(1));

        reset.onClick.Invoke();
        Assert.That(AlmanacProgress.UnlockedCount, Is.Zero);
        Assert.That(Label(reset), Is.EqualTo(UIStrings.AlmanacResetDoneSetting));
    }

    [UnityTest]
    public IEnumerator Settings_ConfirmationExpires_AndArmingTheOtherButtonCancelsIt()
    {
        var (_, reveal, reset, _) = CreateSettings(messageSeconds: 0.2f);
        yield return WaitForLabel(reveal, UIStrings.AlmanacRevealSetting);

        reveal.onClick.Invoke();
        reset.onClick.Invoke();
        Assert.That(Label(reveal), Is.EqualTo(UIStrings.AlmanacRevealSetting), "so um botao armado por vez");
        Assert.That(AlmanacProgress.UnlockedCount, Is.Zero);

        yield return WaitForLabel(reset, UIStrings.AlmanacResetSetting);
        Assert.That(Label(reset), Is.EqualTo(UIStrings.AlmanacResetSetting), "o pedido expira sozinho");

        reveal.onClick.Invoke();
        Assert.That(AlmanacProgress.UnlockedCount, Is.Zero, "depois de expirar, volta a pedir confirmacao");
    }
}
