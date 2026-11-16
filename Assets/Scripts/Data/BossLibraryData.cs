using System;
using System.Collections.Generic;
using UnityEngine;

// Todos os chefes de fim de rodada, no maximo um por baralho.
[CreateAssetMenu(fileName = "BossLibrary", menuName = "Metis/Boss Library")]
public class BossLibraryData : ScriptableObject
{
    [SerializeField] private BossData[] _bosses = Array.Empty<BossData>();

    public IReadOnlyList<BossData> Bosses => _bosses;

    public BossData Find(string bossName)
    {
        return BossRules.Find(_bosses, bossName);
    }

    public BossData ForArchetype(CardArchetype archetype)
    {
        return BossRules.ForArchetype(_bosses, archetype);
    }
}
