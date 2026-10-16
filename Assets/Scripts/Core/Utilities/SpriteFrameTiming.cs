// Escolhe o quadro de uma animacao em laco a partir do tempo decorrido.
public static class SpriteFrameTiming
{
    public static int FrameIndex(double elapsedSeconds, float framesPerSecond, int frameCount)
    {
        if (frameCount <= 0)
            return -1;
        if (framesPerSecond <= 0f || double.IsNaN(elapsedSeconds) || elapsedSeconds <= 0d)
            return 0;

        var frame = (long)(elapsedSeconds * framesPerSecond);
        return (int)(frame % frameCount);
    }
}
