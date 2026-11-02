using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

// Junta todos os textos do projeto e diz como cada um pode ser gravado, sem alterar nenhum asset.
public static class GameTextCollector
{
    public const string InterfacePath = "Assets/Scripts/Localization/UIStrings.cs";
    public const string TitleFontPath = "Assets/UI/Fonts/GROBOLD SDF.asset";

    private static readonly string[] ScenesAndPrefabs = { "Assets/Scenes", "Assets/Prefabs/UI" };

    public static List<GameTextItem> Collect()
    {
        var items = new List<GameTextItem>();
        AddPairs(items, GameTextOrigin.Card, "CardData", GameTextEditKind.Asset, ("_cardName", "_cardNameEn", "", ""), ("_description", "_descriptionEn", "", ""));
        AddPairs(items, GameTextOrigin.Event, "RandomEventData", GameTextEditKind.Asset, ("_title", "_titleEn", "", ""), ("_description", "_descriptionEn", "", ""));
        AddPairs(items, GameTextOrigin.Advantage, "PassiveAdvantageData", GameTextEditKind.Asset, ("_advantageName", "_advantageNameEn", "", ""), ("_description", "_descriptionEn", "", ""));
        AddPairs(items, GameTextOrigin.Preset, "CityParameterPresetData", GameTextEditKind.Asset, ("_presetName", "_presetNameEn", "", ""));
        AddPairs(items, GameTextOrigin.Knowledge, "KnowledgeEntryData", GameTextEditKind.Knowledge,
            ("_title", "_titleEn", "TitlePt", "TitleEn"),
            ("_summary", "_summaryEn", "SummaryPt", "SummaryEn"),
            ("_realWorld", "_realWorldEn", "RealWorldPt", "RealWorldEn"),
            ("_tradeOffs", "_tradeOffsEn", "TradeOffsPt", "TradeOffsEn"));
        AddPairs(items, GameTextOrigin.News, "NewsStoryData", GameTextEditKind.News, ("_headline", "_headlineEn", "HeadlinePt", "HeadlineEn"), ("_body", "_bodyEn", "BodyPt", "BodyEn"));
        AddStructures(items);
        AddKnowledgeSources(items);
        AddNestedPairs(items, GameTextOrigin.RelationNote, "InteractionNotesData", "_notes", "TextPt", "TextEn", "Reference");
        AddNestedPairs(items, GameTextOrigin.ParameterUnit, "ParameterDisplayData", "_entries", "UnitPt", "UnitEn", null);
        AddReport(items);

        items.AddRange(InterfaceTextParser.Parse(File.ReadAllText(InterfacePath), InterfacePath));

        var titleFont = AssetDatabase.AssetPathToGUID(TitleFontPath);
        foreach (var path in ScenePaths())
            items.AddRange(SceneTextParser.Parse(File.ReadAllText(path), path, titleFont));
        return items;
    }

    private static IEnumerable<string> AssetPaths(string type)
    {
        return AssetDatabase.FindAssets("t:" + type, new[] { "Assets/Data" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, System.StringComparer.Ordinal);
    }

    private static IEnumerable<string> ScenePaths()
    {
        return AssetDatabase.FindAssets("t:Scene t:Prefab", ScenesAndPrefabs).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(path => path, System.StringComparer.Ordinal);
    }

    private static SerializedObject Open(string path)
    {
        return new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
    }

    private static void AddPairs(List<GameTextItem> items, GameTextOrigin origin, string type, GameTextEditKind editKind, params (string Pt, string En, string JsonPt, string JsonEn)[] fields)
    {
        foreach (var path in AssetPaths(type))
        {
            var so = Open(path);
            foreach (var field in fields)
            {
                items.Add(new GameTextItem
                {
                    Origin = origin,
                    Id = Path.GetFileNameWithoutExtension(path),
                    Field = field.Pt,
                    Pt = so.FindProperty(field.Pt).stringValue,
                    En = so.FindProperty(field.En).stringValue,
                    Location = path,
                    EditKind = editKind,
                    PtField = field.Pt,
                    EnField = field.En,
                    JsonPtKey = field.JsonPt,
                    JsonEnKey = field.JsonEn,
                });
            }
        }
    }

    private static void AddStructures(List<GameTextItem> items)
    {
        foreach (var path in AssetPaths("StructureData"))
        {
            AddSingle(items, GameTextOrigin.Structure, Path.GetFileNameWithoutExtension(path), "_structureName", Open(path).FindProperty("_structureName").stringValue, path, string.Empty);
            var last = items[items.Count - 1];
            if (last.Origin == GameTextOrigin.Structure && last.Location == path)
            {
                last.EditKind = GameTextEditKind.Asset;
                last.PtField = "_structureName";
            }
        }
    }

    private static void AddKnowledgeSources(List<GameTextItem> items)
    {
        foreach (var path in AssetPaths("KnowledgeEntryData"))
        {
            var sources = Open(path).FindProperty("_sources");
            for (var i = 0; i < sources.arraySize; i++)
                AddSingle(items, GameTextOrigin.Knowledge, Path.GetFileNameWithoutExtension(path), $"_sources[{i}]", sources.GetArrayElementAtIndex(i).FindPropertyRelative("Label").stringValue, path, "Nome de fonte: edite no fichas.json e reimporte.");
        }
    }

    private static void AddNestedPairs(List<GameTextItem> items, GameTextOrigin origin, string type, string arrayField, string ptField, string enField, string singleField)
    {
        foreach (var path in AssetPaths(type))
        {
            var array = Open(path).FindProperty(arrayField);
            for (var i = 0; i < array.arraySize; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                var id = NestedId(element, i);
                var isNote = origin == GameTextOrigin.RelationNote;
                items.Add(new GameTextItem
                {
                    Origin = origin,
                    Id = id,
                    Field = ptField,
                    Pt = element.FindPropertyRelative(ptField).stringValue,
                    En = element.FindPropertyRelative(enField).stringValue,
                    Location = path,
                    EditKind = isNote ? GameTextEditKind.RelationNote : GameTextEditKind.ReadOnly,
                    ReadOnlyReason = isNote ? string.Empty : "O relatório técnico lê as unidades deste asset: mudar aqui exige rodar o gerador dele.",
                    PtField = ptField,
                    EnField = enField,
                    NoteSource = isNote ? EnumName(element.FindPropertyRelative("Source")) : string.Empty,
                    NoteTarget = isNote ? EnumName(element.FindPropertyRelative("Target")) : string.Empty,
                    ArrayIndex = i,
                });
                if (singleField != null)
                    AddSingle(items, origin, id, singleField, element.FindPropertyRelative(singleField).stringValue, path, "Referência do mapa: edite no mapa_relacoes.json e no asset.");
            }
        }
    }

    private static string NestedId(SerializedProperty element, int index)
    {
        var source = element.FindPropertyRelative("Source");
        var target = element.FindPropertyRelative("Target");
        if (source != null && target != null)
            return $"{EnumName(source)} > {EnumName(target)}";
        return System.Enum.GetName(typeof(CityParameterType), index) ?? index.ToString();
    }

    private static string EnumName(SerializedProperty property)
    {
        return property.enumNames[property.enumValueIndex];
    }

    private static void AddSingle(List<GameTextItem> items, GameTextOrigin origin, string id, string field, string text, string path, string readOnlyReason)
    {
        if (GameTextRules.HasLetter(text) == false)
            return;
        items.Add(new GameTextItem { Origin = origin, Id = id, Field = field, Pt = text, IsPaired = false, Location = path, ReadOnlyReason = readOnlyReason });
    }

    private static void AddReport(List<GameTextItem> items)
    {
        foreach (var path in AssetPaths("ModelReportData"))
        {
            var file = AssetDatabase.LoadAssetAtPath<ModelReportData>(path).File;
            foreach (var sheet in file.Sheets)
            {
                AddReportText(items, sheet, "Tab", sheet.Tab, path);
                AddReportText(items, sheet, "Title", sheet.Title, path);
                AddReportText(items, sheet, "Intro", sheet.Intro, path);
                for (var i = 0; i < sheet.Header.Length; i++)
                {
                    AddReportText(items, sheet, $"Header[{i}].Field", sheet.Header[i].Field, path);
                    AddReportText(items, sheet, $"Header[{i}].Value", sheet.Header[i].Value, path);
                }
                for (var i = 0; i < sheet.Columns.Length; i++)
                    AddReportText(items, sheet, $"Columns[{i}]", sheet.Columns[i], path);
                for (var r = 0; r < sheet.Rows.Length; r++)
                {
                    for (var c = 0; c < sheet.Rows[r].Cells.Length; c++)
                        AddReportText(items, sheet, $"Rows[{r}][{c}]", sheet.Rows[r].Cells[c], path);
                }
                for (var i = 0; i < sheet.Notes.Length; i++)
                    AddReportText(items, sheet, $"Notes[{i}]", sheet.Notes[i], path);
            }
        }
    }

    private static void AddReportText(List<GameTextItem> items, ReportSheet sheet, string field, ReportText text, string path)
    {
        if (text == null || (GameTextRules.HasLetter(text.Pt) == false && GameTextRules.HasLetter(text.En) == false))
            return;
        items.Add(new GameTextItem { Origin = GameTextOrigin.Report, Id = sheet.Id, Field = field, Pt = text.Pt, En = text.En, Location = path, ReadOnlyReason = "Relatório técnico: corrija no build.py do gerador dele e reimporte." });
    }
}
