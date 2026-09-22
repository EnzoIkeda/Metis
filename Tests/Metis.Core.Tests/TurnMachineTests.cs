namespace Metis.Core.Tests;

public class TurnMachineTests
{
    private static TurnMachine BuildMachine(CityStats stats = null)
    {
        return new TurnMachine(stats ?? CityStatsTestFactory.Build());
    }

    [Test]
    public void StartGame_BeginsAtTurnOneInActionPhase()
    {
        var machine = BuildMachine();

        machine.StartGame();

        Assert.That(machine.TurnIndex, Is.EqualTo(1));
        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
        Assert.That(machine.Outcome, Is.EqualTo(GameOutcome.None));
    }

    [Test]
    public void EndActionPhase_FromAction_MovesToEventAndResolvesStats()
    {
        var machine = BuildMachine();
        machine.StartGame();

        machine.EndActionPhase();

        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Event));
    }

    [Test]
    public void EndActionPhase_OutsideActionPhase_IsNoOp()
    {
        var machine = BuildMachine();
        machine.StartGame();
        machine.EndActionPhase(); // agora em Event

        machine.EndActionPhase(); // chamada invalida, fase atual nao e Action

        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Event));
    }

    [Test]
    public void AcknowledgeEvent_OutsideEventPhase_IsNoOp()
    {
        var machine = BuildMachine();
        machine.StartGame(); // fase Action

        machine.AcknowledgeEvent();

        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
        Assert.That(machine.TurnIndex, Is.EqualTo(1));
    }

    [Test]
    public void AcknowledgeEvent_NormalTurn_AdvancesTurnIndexAndReturnsToAction()
    {
        var machine = BuildMachine();
        var advancedTo = -1;
        machine.OnTurnAdvanced += turn => advancedTo = turn;
        machine.StartGame();
        machine.EndActionPhase();

        machine.AcknowledgeEvent();

        Assert.That(machine.TurnIndex, Is.EqualTo(2));
        Assert.That(advancedTo, Is.EqualTo(2));
        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
        Assert.That(machine.Outcome, Is.EqualTo(GameOutcome.None));
    }

    [Test]
    public void AcknowledgeEvent_AnchorCritical_EndsGameAsGameOver()
    {
        // A resolucao de turno recalcula BemEstar a partir dos 4 parametros positivos, entao e neles, nao no valor inicial de BemEstar, que precisa forcar o valor recalculado pra ficar abaixo do critico.
        var stats = CityStatsTestFactory.Build(customize: configs =>
        {
            var bemEstar = configs[CityParameterType.BemEstar];
            bemEstar.CriticalLevel = 20f;
            configs[CityParameterType.BemEstar] = bemEstar;

            foreach (var parameter in new[] { CityParameterType.Mobilidade, CityParameterType.Saude, CityParameterType.Seguranca, CityParameterType.Sustentabilidade })
            {
                var cfg = configs[parameter];
                cfg.InitialValue = 5f;
                configs[parameter] = cfg;
            }
        });
        var machine = BuildMachine(stats);
        GameOutcome? ended = null;
        machine.OnGameEnded += outcome => ended = outcome;
        machine.StartGame();
        machine.EndActionPhase();

        machine.AcknowledgeEvent();

        Assert.That(machine.Outcome, Is.EqualTo(GameOutcome.GameOver));
        Assert.That(ended, Is.EqualTo(GameOutcome.GameOver));
        Assert.That(machine.TurnIndex, Is.EqualTo(1), "nao deveria ter avancado de turno");
    }

    [Test]
    public void AcknowledgeEvent_ReachingTurnTwenty_EndsGameAsVictory()
    {
        var machine = BuildMachine();
        machine.StartGame();
        machine.DebugJumpToTurn(TurnMachine.VictoryTurnCount);
        machine.EndActionPhase();

        machine.AcknowledgeEvent();

        Assert.That(machine.Outcome, Is.EqualTo(GameOutcome.Victory));
    }

    [Test]
    public void MachineCalls_AfterGameEnded_AreNoOps()
    {
        var machine = BuildMachine();
        machine.StartGame();
        machine.DebugForceGameOver();

        machine.EndActionPhase();
        machine.AcknowledgeEvent();
        machine.DebugJumpToTurn(10);
        machine.DebugForceVictory();

        Assert.That(machine.Outcome, Is.EqualTo(GameOutcome.GameOver), "primeiro Outcome definido deveria ser final");
    }

    [Test]
    public void DebugJumpToTurn_SetsTurnIndexAndReturnsToActionPhase()
    {
        var machine = BuildMachine();
        machine.StartGame();
        machine.EndActionPhase();

        machine.DebugJumpToTurn(7);

        Assert.That(machine.TurnIndex, Is.EqualTo(7));
        Assert.That(machine.CurrentPhase, Is.EqualTo(TurnPhase.Action));
    }

    [Test]
    public void DebugJumpToTurn_ClampsBelowOneToOne()
    {
        var machine = BuildMachine();
        machine.StartGame();

        machine.DebugJumpToTurn(-5);

        Assert.That(machine.TurnIndex, Is.EqualTo(1));
    }
}
