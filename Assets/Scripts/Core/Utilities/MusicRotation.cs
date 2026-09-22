using System;

// Escolhe a proxima faixa de uma playlist, evitando repetir a ultima tocada quando ha mais de uma opcao.
public class MusicRotation
{
    private int _lastIndex = -1;

    public int Next(int trackCount, Random random)
    {
        if (trackCount <= 0)
            return -1;

        if (trackCount == 1)
        {
            _lastIndex = 0;
            return 0;
        }

        int next;
        do
        {
            next = random.Next(trackCount);
        } while (next == _lastIndex);

        _lastIndex = next;
        return next;
    }
}
