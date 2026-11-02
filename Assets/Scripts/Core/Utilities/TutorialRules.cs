using System;
using System.Collections.Generic;

// Dicas do tutorial. So acrescentar no fim: a Unity grava enum pelo numero.
public enum TutorialTip
{
    None,
    Parameters,
    Cards,
    PassTurn,
    Event,
    Crisis,
    Collapse,
    Tier,
    Newspaper
}

// Retrato do que a dica precisa saber sobre a fase.
public struct TutorialSituation
{
    public bool IsActionPhase;
    public int Turn;
    public bool CanPass;
    public bool EventHappened;
    public bool AnyCrisis;
    public bool AnyCollapse;
    public bool TierUnlocked;
}

// Escolhe a proxima dica a mostrar. So leitura: nao muda nenhum numero do jogo.
public static class TutorialRules
{
    // Primeiro o basico do turno; depois o que acabou de acontecer, do mais grave pro mais leve.
    public static TutorialTip Next(TutorialSituation situation, Func<TutorialTip, bool> wasSeen)
    {
        if (situation.IsActionPhase == false || wasSeen == null)
            return TutorialTip.None;

        if (wasSeen(TutorialTip.Parameters) == false)
            return TutorialTip.Parameters;
        if (wasSeen(TutorialTip.Cards) == false)
            return TutorialTip.Cards;
        if (situation.CanPass && wasSeen(TutorialTip.PassTurn) == false)
            return TutorialTip.PassTurn;
        if (situation.AnyCollapse && wasSeen(TutorialTip.Collapse) == false)
            return TutorialTip.Collapse;
        if (situation.AnyCrisis && wasSeen(TutorialTip.Crisis) == false)
            return TutorialTip.Crisis;
        if (situation.TierUnlocked && wasSeen(TutorialTip.Tier) == false)
            return TutorialTip.Tier;
        if (situation.EventHappened && wasSeen(TutorialTip.Event) == false)
            return TutorialTip.Event;
        if (situation.Turn >= 2 && wasSeen(TutorialTip.Newspaper) == false)
            return TutorialTip.Newspaper;
        return TutorialTip.None;
    }

    // Preenche crise, Colapso e tier com os mesmos criterios que desbloqueiam as fichas de conceito.
    public static void ReadStats(ref TutorialSituation situation, CityStats stats, IReadOnlyDictionary<CardTier, float> tierGates)
    {
        if (stats == null)
            return;

        foreach (CityParameterType parameter in Enum.GetValues(typeof(CityParameterType)))
        {
            foreach (var trigger in AlmanacTriggers.ForParameter(stats, parameter, tierGates))
            {
                if (trigger == KnowledgeUnlockTrigger.FirstCrisis)
                    situation.AnyCrisis = true;
                else if (trigger == KnowledgeUnlockTrigger.FirstCollapse)
                    situation.AnyCollapse = true;
                else if (trigger == KnowledgeUnlockTrigger.TierCidadeDigital || trigger == KnowledgeUnlockTrigger.TierCidadeConectada || trigger == KnowledgeUnlockTrigger.TierSmartCity)
                    situation.TierUnlocked = true;
            }
        }
    }
}
