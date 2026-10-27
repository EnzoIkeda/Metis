using System;
using System.Collections.Generic;
using UnityEngine;

// Relatorio tecnico do modelo da cidade: abas de planilha com o que o modelo mede, como calcula e o que deixa de fora.
// Gerado fora do Unity a partir dos dados calibrados e importado pelo menu de educacao.
[CreateAssetMenu(fileName = "ModelReport", menuName = "Metis/Model Report")]
public class ModelReportData : ScriptableObject
{
    [SerializeField] private ReportFile _file = new ReportFile();

    public IReadOnlyList<ReportSheet> Sheets => _file?.Sheets ?? Array.Empty<ReportSheet>();

    public string ModelVersion => _file?.ModelVersion ?? string.Empty;

    public string Fingerprint => _file?.Fingerprint ?? string.Empty;

    // Copia do conteudo como esta no asset, pra validacao e comparacao.
    public ReportFile File => _file;

#if UNITY_EDITOR
    // So o importador troca o conteudo.
    public void EditorReplace(ReportFile file)
    {
        _file = file;
    }
#endif
}
