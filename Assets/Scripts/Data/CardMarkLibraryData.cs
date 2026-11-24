using System;
using System.Collections.Generic;
using UnityEngine;

// Marca de uma carta na cidade: o que deixa, onde prefere, quantos lugares e as pecas sorteadas.
[Serializable]
public struct CardMarkEntry
{
    public CardData Card;
    public CardMarkKind Kind;
    public CardMarkPlacement Placement;

    [Tooltip("Lote que vira verde (parque, horta): depois nao recebe peca no telhado.")]
    public bool ResultIsGreen;

    [Tooltip("Lote que vira predio de telhado plano: depois pode receber peca no telhado.")]
    public bool ResultHasFlatRoof;

    [Tooltip("Lugares vizinhos marcados de uma vez (1 a 4).")]
    [Range(1, 4)] public int Size;

    [Tooltip("Troca de lote: um predio so sobre 2x2 lotes, em escala dobrada, quando houver quadrado livre.")]
    public bool Merge;

    [Tooltip("Vezes que a carta marca na fase; 0 usa o padrao (3).")]
    public int Limit;

    [Tooltip("Pecas sorteadas para cada lugar da marca.")]
    public GameObject[] Pieces;

    public CardMarkRule Rule => new CardMarkRule
    {
        Kind = Kind,
        Placement = Placement,
        ResultIsGreen = ResultIsGreen,
        ResultHasFlatRoof = ResultHasFlatRoof,
        Size = Size,
        Merge = Merge,
        Limit = Limit,
    };
}

// Marca de cada carta na cidade, fora da definicao da carta (so apresentacao, nao muda numero).
[CreateAssetMenu(fileName = "CardMarkLibrary", menuName = "Metis/Card Mark Library")]
public class CardMarkLibraryData : ScriptableObject
{
    [SerializeField] private CardMarkEntry[] _entries;

    [Tooltip("Poeira que sobe onde uma peca pousa.")]
    [SerializeField] private GameObject _landingDust;

    public IReadOnlyList<CardMarkEntry> Entries => _entries ?? Array.Empty<CardMarkEntry>();
    public GameObject LandingDust => _landingDust;

    public bool TryGet(string cardId, out CardMarkEntry entry)
    {
        foreach (var candidate in Entries)
        {
            if (candidate.Card != null && candidate.Card.name == cardId && candidate.Kind != CardMarkKind.None && candidate.Pieces != null && candidate.Pieces.Length > 0)
            {
                entry = candidate;
                return true;
            }
        }
        entry = default;
        return false;
    }
}
