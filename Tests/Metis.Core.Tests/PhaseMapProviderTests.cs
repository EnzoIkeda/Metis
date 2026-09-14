namespace Metis.Core.Tests;

public class PhaseMapProviderTests
{
    [Test]
    public void GetNodes_Beta_ReturnsSingleAvailableAdvanceNode()
    {
        var nodes = PhaseMapProvider.GetNodes();

        Assert.That(nodes.Count, Is.EqualTo(1));
        Assert.That(nodes[0].Id, Is.EqualTo("next"));
        Assert.That(nodes[0].IsAvailable, Is.True);
    }
}
