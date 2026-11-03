using System.Collections.Generic;

namespace Metis.Core.Tests;

public class TutorialRulesTests
{
    private static readonly TutorialTip[] Basics = { TutorialTip.Parameters, TutorialTip.Cards };

    private static TutorialTip Next(TutorialSituation situation, params TutorialTip[] seen)
    {
        var set = new HashSet<TutorialTip>(seen);
        return TutorialRules.Next(situation, set.Contains);
    }

    private static TutorialSituation Action(int turn = 1)
    {
        return new TutorialSituation { IsActionPhase = true, Turn = turn };
    }

    [Test]
    public void Next_OutsideTheActionPhase_ShowsNothing()
    {
        Assert.That(Next(new TutorialSituation { Turn = 1 }), Is.EqualTo(TutorialTip.None));
    }

    [Test]
    public void Next_FirstTurn_ShowsParametersThenCards()
    {
        Assert.That(Next(Action()), Is.EqualTo(TutorialTip.Parameters));
        Assert.That(Next(Action(), TutorialTip.Parameters), Is.EqualTo(TutorialTip.Cards));
        Assert.That(Next(Action(), Basics), Is.EqualTo(TutorialTip.None));
    }

    [Test]
    public void Next_EachTrigger_ShowsItsTip()
    {
        var pass = Action();
        pass.CanPass = true;
        var happened = Action();
        happened.EventHappened = true;
        var crisis = Action();
        crisis.AnyCrisis = true;
        var collapse = Action();
        collapse.AnyCollapse = true;
        var tier = Action();
        tier.TierUnlocked = true;

        Assert.That(Next(pass, Basics), Is.EqualTo(TutorialTip.PassTurn));
        Assert.That(Next(happened, Basics), Is.EqualTo(TutorialTip.Event));
        Assert.That(Next(crisis, Basics), Is.EqualTo(TutorialTip.Crisis));
        Assert.That(Next(collapse, Basics), Is.EqualTo(TutorialTip.Collapse));
        Assert.That(Next(tier, Basics), Is.EqualTo(TutorialTip.Tier));
        Assert.That(Next(Action(turn: 2), Basics), Is.EqualTo(TutorialTip.Newspaper));
    }

    [Test]
    public void Next_SeenTip_IsNotRepeated()
    {
        var crisis = Action();
        crisis.AnyCrisis = true;

        Assert.That(Next(crisis, TutorialTip.Parameters, TutorialTip.Cards, TutorialTip.Crisis), Is.EqualTo(TutorialTip.None));
    }

    [Test]
    public void Next_CollapseComesBeforeCrisis()
    {
        var both = Action();
        both.AnyCollapse = true;
        both.AnyCrisis = true;

        Assert.That(Next(both, Basics), Is.EqualTo(TutorialTip.Collapse));
    }

    [Test]
    public void ReadStats_NullStats_LeavesTheSituationAlone()
    {
        var situation = Action();

        TutorialRules.ReadStats(ref situation, null, null);

        Assert.That(situation.AnyCrisis, Is.False);
        Assert.That(situation.AnyCollapse, Is.False);
        Assert.That(situation.TierUnlocked, Is.False);
    }
}
