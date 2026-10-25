using System;
using System.Collections.Generic;

// Conjunto das fichas ja desbloqueadas pelo jogador, guardado como texto com um id por linha.
public class AlmanacState
{
    private readonly HashSet<string> _unlocked = new HashSet<string>(StringComparer.Ordinal);

    public int UnlockedCount => _unlocked.Count;

    public IEnumerable<string> UnlockedIds => _unlocked;

    // Verdadeiro so quando o id ainda nao estava desbloqueado.
    public bool Unlock(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        return _unlocked.Add(id.Trim());
    }

    public bool IsUnlocked(string id)
    {
        return string.IsNullOrWhiteSpace(id) == false && _unlocked.Contains(id.Trim());
    }

    public void Clear()
    {
        _unlocked.Clear();
    }

    // Ordenado, pra o mesmo conjunto sempre gerar o mesmo texto.
    public string Serialize()
    {
        var ids = new List<string>(_unlocked);
        ids.Sort(StringComparer.Ordinal);
        return string.Join("\n", ids);
    }

    // Texto vazio ou nulo vira um almanaque vazio; linhas em branco sao ignoradas.
    public static AlmanacState Parse(string text)
    {
        var state = new AlmanacState();
        if (string.IsNullOrEmpty(text))
            return state;

        foreach (var line in text.Split('\n'))
            state.Unlock(line);
        return state;
    }
}
