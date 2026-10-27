using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Cada aba do relatorio real cabe inteira no papel na tela mais baixa (1080x1920), nos dois idiomas e com fichas abertas ou nao.
public class ModelReportPaperFitTests
{
    private const string TestKey = "Metis.Almanac.ReportFitTests";
    private const float MinReadableScale = 0.6f;

    private const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

    private GameObject _canvas;
    private object _language;

    [SetUp]
    public void SetUp()
    {
        // Idioma trocado direto no campo, sem gravar a preferencia real do Editor.
        _language = typeof(LocalizationManager).GetField("_current", Flags).GetValue(null);
        PlayerPrefs.DeleteKey(TestKey);
        SetStatic(typeof(AlmanacProgress), "_prefsKeyOverride", TestKey);
        SetStatic(typeof(AlmanacProgress), "_state", null);
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvas != null)
            Object.DestroyImmediate(_canvas);
        SetStatic(typeof(LocalizationManager), "_current", _language);
        PlayerPrefs.DeleteKey(TestKey);
        SetStatic(typeof(AlmanacProgress), "_prefsKeyOverride", null);
        SetStatic(typeof(AlmanacProgress), "_state", null);
    }

    private static void SetStatic(System.Type type, string name, object value) => type.GetField(name, Flags).SetValue(null, value);

    private ModelReportView BuildView()
    {
        _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        _canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1080f, 1920f);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Almanac.prefab");
        var almanac = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _canvas.transform);
        return almanac.GetComponentInChildren<ModelReportView>(true);
    }

    private static List<string> Overflowing(ModelReportView view)
    {
        var failures = new List<string>();
        view.Show();
        var count = AssetDatabase.LoadAssetAtPath<ModelReportData>(ModelReportImporter.ReportPath).Sheets.Count;
        for (var i = 0; i < count; i++)
        {
            view.SelectSheet(i);
            var layout = view.Layout;
            if (layout.Fits == false || layout.Scale < MinReadableScale)
                failures.Add($"{view.SheetId} ({LocalizationManager.Current}): cabe={layout.Fits}, letra={layout.Scale:0.00}");
        }
        return failures;
    }

    [TestCase(Language.Portuguese, false)]
    [TestCase(Language.English, false)]
    [TestCase(Language.Portuguese, true)]
    [TestCase(Language.English, true)]
    public void EverySheet_FitsThePaperWithReadableLetters(Language language, bool unlocked)
    {
        SetStatic(typeof(LocalizationManager), "_current", (Language?)language);
        if (unlocked)
        {
            var library = AssetDatabase.LoadAssetAtPath<KnowledgeLibraryData>(KnowledgeEntryImporter.LibraryPath);
            foreach (var entry in library.Entries)
            {
                if (entry != null)
                    AlmanacProgress.Unlock(entry);
            }
        }

        var failures = Overflowing(BuildView());

        Assert.That(failures, Is.Empty);
    }
}
