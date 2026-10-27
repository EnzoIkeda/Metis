using System.Linq;
using NUnit.Framework;
using UnityEditor;

// Confere o relatorio tecnico real: conteudo valido, links que existem e numeros iguais aos do jogo.
public class ModelReportAssetTests
{
    private static ModelReportData Report => AssetDatabase.LoadAssetAtPath<ModelReportData>(ModelReportImporter.ReportPath);

    private static KnowledgeLibraryData Library => AssetDatabase.LoadAssetAtPath<KnowledgeLibraryData>(KnowledgeEntryImporter.LibraryPath);

    [Test]
    public void Report_IsValidAndPointsOnlyToExistingEntries()
    {
        Assert.That(Report, Is.Not.Null, "relatorio nao importado");
        var known = Library.Entries.Where(e => e != null).Select(e => e.Id).ToHashSet();

        Assert.That(ModelReportImportFormat.Validate(Report.File, known), Is.Empty);
    }

    [Test]
    public void Report_NumbersMatchTheCurrentModel()
    {
        Assert.That(Report.Fingerprint, Is.EqualTo(ModelReportLiveData.Fingerprint()),
            "o modelo mudou e o relatorio tecnico ficou desatualizado: apontar o gerador pro export novo, gerar de novo e reimportar");
    }
}
