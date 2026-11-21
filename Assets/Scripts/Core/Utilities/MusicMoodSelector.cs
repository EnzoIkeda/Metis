// O que deve tocar agora.
public enum MusicMood
{
    Playlist,
    Crisis,
    Sequence
}

// Prioridade da musica: sequencia de abertura ou de fim, depois a crise, depois a trilha de fundo.
// Cada metodo devolve se o humor mudou, para quem toca so trocar de faixa quando precisa.
public class MusicMoodSelector
{
    private bool _sequence;
    private bool _crisis;
    private bool _phaseOver;

    public MusicMood Current => _sequence ? MusicMood.Sequence : (_crisis && _phaseOver == false ? MusicMood.Crisis : MusicMood.Playlist);

    public bool SetSequence(bool active)
    {
        return Change(() => _sequence = active);
    }

    // Ignorada depois do fim da fase, para a crise nunca continuar tocando no desfecho.
    public bool SetCrisis(bool active)
    {
        return Change(() => _crisis = active);
    }

    public bool StartPhase()
    {
        return Change(() =>
        {
            _phaseOver = false;
            _crisis = false;
        });
    }

    public bool EndPhase()
    {
        return Change(() =>
        {
            _phaseOver = true;
            _crisis = false;
        });
    }

    private bool Change(System.Action apply)
    {
        var before = Current;
        apply();
        return Current != before;
    }
}
