using System;

// Gerador pseudoaleatorio (SplitMix64) com estado exposto, pra salvar e retomar a mesma sequencia de sorteios.
// Herda de Random pra ser usado onde o jogo ja recebe um Random injetado.
public class SeededRandom : Random
{
    private const ulong Increment = 0x9E3779B97F4A7C15UL;

    public ulong State { get; private set; }

    public SeededRandom(ulong state) : base(0)
    {
        State = state;
    }

    // Semente nova a cada chamada, pra uma fase nova nunca repetir a anterior.
    public static ulong NewSeed()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        return BitConverter.ToUInt64(bytes, 0) ^ BitConverter.ToUInt64(bytes, 8);
    }

    public ulong NextUInt64()
    {
        State += Increment;
        var z = State;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public override int Next()
    {
        while (true)
        {
            var value = (int)(NextUInt64() >> 33);
            if (value != int.MaxValue)
                return value;
        }
    }

    public override int Next(int maxValue)
    {
        if (maxValue < 0)
            throw new ArgumentOutOfRangeException(nameof(maxValue));
        return (int)(((NextUInt64() >> 32) * (ulong)maxValue) >> 32);
    }

    public override int Next(int minValue, int maxValue)
    {
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException(nameof(minValue));
        var range = (ulong)((long)maxValue - minValue);
        return (int)(minValue + (long)(((NextUInt64() >> 32) * range) >> 32));
    }

    public override double NextDouble()
    {
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    protected override double Sample()
    {
        return NextDouble();
    }

    public override void NextBytes(byte[] buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = (byte)(NextUInt64() >> 56);
    }

    public override void NextBytes(Span<byte> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = (byte)(NextUInt64() >> 56);
    }
}
