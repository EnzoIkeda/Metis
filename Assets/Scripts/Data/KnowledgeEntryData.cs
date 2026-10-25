using System;
using System.Collections.Generic;
using UnityEngine;

// Fonte citada numa ficha, aberta no navegador pelo botao da ficha.
[Serializable]
public struct KnowledgeSource
{
    public string Label;
    public string Url;
}

// Ficha "Saiba mais": o que e, onde existe de verdade, custos e riscos e as fontes.
// Uma ficha pode ser compartilhada por cartas e eventos sobre o mesmo tema.
[CreateAssetMenu(fileName = "New Knowledge Entry", menuName = "Metis/Knowledge Entry")]
public class KnowledgeEntryData : ScriptableObject
{
    [SerializeField] private string _title;
    [SerializeField] private string _titleEn;
    [SerializeField] private KnowledgeCategory _category;

    // Momento que desbloqueia a ficha no almanaque; as de carta e evento ficam em None.
    [SerializeField] private KnowledgeUnlockTrigger _unlockTrigger;

    [SerializeField, TextArea(3, 8)] private string _summary;
    [SerializeField, TextArea(3, 8)] private string _summaryEn;
    [SerializeField, TextArea(3, 8)] private string _realWorld;
    [SerializeField, TextArea(3, 8)] private string _realWorldEn;
    [SerializeField, TextArea(3, 8)] private string _tradeOffs;
    [SerializeField, TextArea(3, 8)] private string _tradeOffsEn;
    [SerializeField] private KnowledgeSource[] _sources = Array.Empty<KnowledgeSource>();
    [SerializeField] private KnowledgeEntryData[] _relatedEntries = Array.Empty<KnowledgeEntryData>();

    // Arte da carta ou icone do parametro, preenchidos pela importacao; vazio nas outras fichas.
    [SerializeField] private Sprite _image;

    public string Id => name;

    public KnowledgeCategory Category => _category;

    public KnowledgeUnlockTrigger UnlockTrigger => _unlockTrigger;

    public string Title => Localized(_title, _titleEn);

    public string Summary => Localized(_summary, _summaryEn);

    public string RealWorld => Localized(_realWorld, _realWorldEn);

    public string TradeOffs => Localized(_tradeOffs, _tradeOffsEn);

    public IReadOnlyList<KnowledgeSource> Sources => _sources;

    public IReadOnlyList<KnowledgeEntryData> RelatedEntries => _relatedEntries;

    public Sprite Image => _image;

    // Texto no idioma atual, com fallback pro outro idioma se a traducao estiver vazia.
    private static string Localized(string pt, string en)
    {
        if (LocalizationManager.Current == Language.English)
            return string.IsNullOrEmpty(en) ? pt : en;
        return string.IsNullOrEmpty(pt) ? en : pt;
    }
}
