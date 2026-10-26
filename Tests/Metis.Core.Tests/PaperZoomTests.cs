namespace Metis.Core.Tests;

public class PaperZoomTests
{
    private static PaperZoom Zoom()
    {
        var zoom = new PaperZoom(1f, 3f);
        zoom.Resize(900f, 1500f);
        return zoom;
    }

    [Test]
    public void StartsWholeAndStill()
    {
        var zoom = Zoom();

        Assert.That(zoom.Scale, Is.EqualTo(1f));
        Assert.That(zoom.IsZoomed, Is.False);
        zoom.Pan(100f, -100f);
        Assert.That(zoom.OffsetX, Is.EqualTo(0f), "sem zoom nao ha o que arrastar");
        Assert.That(zoom.OffsetY, Is.EqualTo(0f));
    }

    [Test]
    public void ZoomKeepsThePointUnderTheFingerInPlace()
    {
        var zoom = Zoom();
        zoom.ZoomAround(450f, -750f, 2f);

        Assert.That(zoom.Scale, Is.EqualTo(2f));
        // O meio da folha continua no meio da janela.
        Assert.That(zoom.OffsetX + 450f * zoom.Scale, Is.EqualTo(450f).Within(1e-3));
        Assert.That(zoom.OffsetY - 750f * zoom.Scale, Is.EqualTo(-750f).Within(1e-3));
    }

    [Test]
    public void ZoomStaysWithinLimits()
    {
        var zoom = Zoom();
        zoom.ZoomAround(0f, 0f, 10f);
        Assert.That(zoom.Scale, Is.EqualTo(3f));
        zoom.ZoomAround(0f, 0f, 0.01f);
        Assert.That(zoom.Scale, Is.EqualTo(1f));
        zoom.ZoomAround(0f, 0f, float.NaN);
        zoom.ZoomAround(0f, 0f, -2f);
        Assert.That(zoom.Scale, Is.EqualTo(1f));
    }

    [Test]
    public void PanNeverShowsEmptySpace()
    {
        var zoom = Zoom();
        zoom.ZoomAround(0f, 0f, 2f);

        zoom.Pan(500f, -500f);
        Assert.That(zoom.OffsetX, Is.EqualTo(0f), "a borda esquerda nao entra na janela");
        Assert.That(zoom.OffsetY, Is.EqualTo(0f), "o topo nao desce");

        zoom.Pan(-5000f, 5000f);
        Assert.That(zoom.OffsetX, Is.EqualTo(900f - 1800f), "a borda direita encosta na da janela");
        Assert.That(zoom.OffsetY, Is.EqualTo(3000f - 1500f), "o rodape encosta no da janela");
    }

    [Test]
    public void ZoomingOutPullsThePaperBackIntoTheWindow()
    {
        var zoom = Zoom();
        zoom.ZoomAround(900f, -1500f, 3f);
        zoom.ZoomAround(0f, 0f, 0.5f);

        Assert.That(zoom.Scale, Is.EqualTo(1.5f).Within(1e-4));
        Assert.That(zoom.OffsetX, Is.InRange(900f - 900f * 1.5f, 0f));
        Assert.That(zoom.OffsetY, Is.InRange(0f, 1500f * 1.5f - 1500f));
    }

    [Test]
    public void ResetGoesBackToTheWholePage()
    {
        var zoom = Zoom();
        zoom.ZoomAround(300f, -300f, 2.5f);
        zoom.Pan(-100f, 100f);

        zoom.Reset();

        Assert.That(zoom.Scale, Is.EqualTo(1f));
        Assert.That(zoom.OffsetX, Is.EqualTo(0f));
        Assert.That(zoom.OffsetY, Is.EqualTo(0f));
    }
}
