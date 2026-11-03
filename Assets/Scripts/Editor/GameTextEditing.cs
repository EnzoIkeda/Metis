using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// Regras de salvamento de um texto editado.
public static class GameTextSaveRules
{
    public static List<string> Errors(GameTextItem item, string pt, string en)
    {
        var errors = new List<string>();
        if (item.EditKind == GameTextEditKind.ReadOnly)
            errors.Add("Este texto não pode ser editado aqui.");
        if (HasDash(pt) || HasDash(en))
            errors.Add("O texto tem travessão.");
        if (string.IsNullOrWhiteSpace(pt) && string.IsNullOrWhiteSpace(item.Pt) == false)
            errors.Add("O texto em português ficaria vazio.");
        if (item.IsPaired && string.IsNullOrWhiteSpace(en) && string.IsNullOrWhiteSpace(item.En) == false)
            errors.Add("O texto em inglês ficaria vazio.");
        return errors;
    }

    public static bool HasChange(GameTextItem item, string pt, string en)
    {
        return Normalize(item.Pt) != Normalize(pt) || (item.IsPaired && Normalize(item.En) != Normalize(en));
    }

    public static string Normalize(string text)
    {
        return (text ?? string.Empty).Replace("\r\n", "\n");
    }

    private static bool HasDash(string text)
    {
        return (text ?? string.Empty).IndexOf('\u2014') >= 0 || (text ?? string.Empty).IndexOf('\u2013') >= 0;
    }
}

[Serializable]
public class GameTextReviewEntry
{
    public string Key;
    public string Hash;
    public string Date;
}

[Serializable]
public class GameTextReviewFile
{
    public GameTextReviewEntry[] Reviewed = Array.Empty<GameTextReviewEntry>();
}

// Marca de revisao ligada ao conteudo: se o texto muda, a marca deixa de valer.
public class GameTextReview
{
    private readonly Dictionary<string, GameTextReviewEntry> _entries = new Dictionary<string, GameTextReviewEntry>(StringComparer.Ordinal);

    public GameTextReview(GameTextReviewFile file = null)
    {
        foreach (var entry in file?.Reviewed ?? Array.Empty<GameTextReviewEntry>())
        {
            if (entry != null && string.IsNullOrEmpty(entry.Key) == false)
                _entries[entry.Key] = entry;
        }
    }

    public static string KeyOf(GameTextItem item)
    {
        return $"{item.Origin}|{item.Location}|{item.Id}|{item.Field}";
    }

    public static string HashOf(GameTextItem item)
    {
        var text = GameTextSaveRules.Normalize(item.Pt) + "\u0001" + GameTextSaveRules.Normalize(item.En);
        using (var sha = SHA256.Create())
        {
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            return string.Concat(bytes.Take(12).Select(b => b.ToString("x2")));
        }
    }

    public bool IsReviewed(GameTextItem item)
    {
        return _entries.TryGetValue(KeyOf(item), out var entry) && entry.Hash == HashOf(item);
    }

    public void Mark(GameTextItem item, string date)
    {
        _entries[KeyOf(item)] = new GameTextReviewEntry { Key = KeyOf(item), Hash = HashOf(item), Date = date };
    }

    public void Unmark(GameTextItem item)
    {
        _entries.Remove(KeyOf(item));
    }

    public GameTextReviewFile ToFile()
    {
        return new GameTextReviewFile { Reviewed = _entries.Values.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray() };
    }
}

// Troca textos num JSON sem regravar o restante, para o arquivo manter a formatacao e a ordem.
public static class ContentJsonEditor
{
    public class Change
    {
        public string Key;
        public int Cell = -1;
        public string Expected;
        public string Value;
    }

    private enum NodeKind
    {
        Object,
        Array,
        Text,
        Other,
    }

    private class Node
    {
        public NodeKind Kind;
        public int Start;
        public int End;
        public string Text;
        public readonly List<KeyValuePair<string, Node>> Members = new List<KeyValuePair<string, Node>>();
        public readonly List<Node> Items = new List<Node>();

        public Node Member(string key)
        {
            foreach (var pair in Members)
            {
                if (pair.Key == key)
                    return pair.Value;
            }
            return null;
        }
    }

    // Item de lista de objetos, encontrado pelo campo Id.
    public static bool TryReplaceInObject(string json, string listKey, string id, IReadOnlyList<Change> changes, out string result, out string error)
    {
        return Edit(json, changes, out result, out error, root =>
        {
            var list = root.Kind == NodeKind.Object ? root.Member(listKey) : null;
            return list?.Items.FirstOrDefault(item => item.Kind == NodeKind.Object && item.Member("Id")?.Text == id);
        }, $"Id '{id}'");
    }

    // Linha de uma lista de listas, encontrada pelas duas primeiras celulas.
    public static bool TryReplaceInRow(string json, string first, string second, IReadOnlyList<Change> changes, out string result, out string error)
    {
        return Edit(json, changes, out result, out error, root =>
            root.Items.FirstOrDefault(row => row.Kind == NodeKind.Array && row.Items.Count >= 2 && row.Items[0].Text == first && row.Items[1].Text == second),
            $"linha '{first} > {second}'");
    }

    public static string Encode(string text)
    {
        var builder = new StringBuilder("\"");
        foreach (var c in text ?? string.Empty)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                default:
                    if (c < 0x20)
                        builder.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        builder.Append(c);
                    break;
            }
        }
        return builder.Append('"').ToString();
    }

    private static bool Edit(string json, IReadOnlyList<Change> changes, out string result, out string error, Func<Node, Node> find, string description)
    {
        result = json;
        error = null;
        Node owner;
        try
        {
            var cursor = 0;
            owner = find(ParseValue(json, ref cursor));
        }
        catch (FormatException exception)
        {
            error = "JSON inválido: " + exception.Message;
            return false;
        }
        if (owner == null)
        {
            error = $"Não encontrei {description} no arquivo.";
            return false;
        }

        var splices = new List<(Node Target, string Value)>();
        foreach (var change in changes)
        {
            var target = change.Cell >= 0
                ? (change.Cell < owner.Items.Count ? owner.Items[change.Cell] : null)
                : owner.Member(change.Key);
            var label = change.Cell >= 0 ? $"célula {change.Cell}" : change.Key;
            if (target == null)
            {
                error = $"{description}: campo {label} não existe.";
                return false;
            }
            var current = target.Kind == NodeKind.Text ? target.Text : string.Empty;
            if (GameTextSaveRules.Normalize(current).Trim() != GameTextSaveRules.Normalize(change.Expected).Trim())
            {
                error = $"{description}: {label} no arquivo difere do asset. Nada foi mudado; reimporte antes de editar.";
                return false;
            }
            splices.Add((target, change.Value));
        }

        foreach (var splice in splices.OrderByDescending(s => s.Target.Start))
            result = result.Substring(0, splice.Target.Start) + Encode(splice.Value) + result.Substring(splice.Target.End);
        return true;
    }

    private static Node ParseValue(string json, ref int i)
    {
        SkipSpace(json, ref i);
        if (i >= json.Length)
            throw new FormatException("fim inesperado");
        var start = i;
        var c = json[i];
        if (c == '{')
            return ParseObject(json, ref i);
        if (c == '[')
            return ParseArray(json, ref i);
        if (c == '"')
        {
            var text = ParseString(json, ref i);
            return new Node { Kind = NodeKind.Text, Start = start, End = i, Text = text };
        }
        while (i < json.Length && ",]} \t\r\n".IndexOf(json[i]) < 0)
            i++;
        if (i == start)
            throw new FormatException($"valor inesperado na posição {start}");
        return new Node { Kind = NodeKind.Other, Start = start, End = i };
    }

    private static Node ParseObject(string json, ref int i)
    {
        var node = new Node { Kind = NodeKind.Object, Start = i };
        i++;
        SkipSpace(json, ref i);
        while (i < json.Length && json[i] != '}')
        {
            if (json[i] != '"')
                throw new FormatException($"chave esperada na posição {i}");
            var key = ParseString(json, ref i);
            SkipSpace(json, ref i);
            if (i >= json.Length || json[i] != ':')
                throw new FormatException($"':' esperado na posição {i}");
            i++;
            node.Members.Add(new KeyValuePair<string, Node>(key, ParseValue(json, ref i)));
            SkipSeparator(json, ref i, '}');
        }
        Close(json, ref i, '}');
        node.End = i;
        return node;
    }

    private static Node ParseArray(string json, ref int i)
    {
        var node = new Node { Kind = NodeKind.Array, Start = i };
        i++;
        SkipSpace(json, ref i);
        while (i < json.Length && json[i] != ']')
        {
            node.Items.Add(ParseValue(json, ref i));
            SkipSeparator(json, ref i, ']');
        }
        Close(json, ref i, ']');
        node.End = i;
        return node;
    }

    private static void SkipSeparator(string json, ref int i, char close)
    {
        SkipSpace(json, ref i);
        if (i < json.Length && json[i] == ',')
        {
            i++;
            SkipSpace(json, ref i);
        }
        else if (i < json.Length && json[i] != close)
        {
            throw new FormatException($"',' esperada na posição {i}");
        }
    }

    private static void Close(string json, ref int i, char close)
    {
        if (i >= json.Length || json[i] != close)
            throw new FormatException($"'{close}' esperado na posição {i}");
        i++;
    }

    private static string ParseString(string json, ref int i)
    {
        var builder = new StringBuilder();
        i++;
        while (i < json.Length && json[i] != '"')
        {
            var c = json[i++];
            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }
            if (i >= json.Length)
                break;
            var next = json[i++];
            switch (next)
            {
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'u':
                    if (i + 4 > json.Length)
                        throw new FormatException("escape incompleto");
                    builder.Append((char)Convert.ToInt32(json.Substring(i, 4), 16));
                    i += 4;
                    break;
                default: builder.Append(next); break;
            }
        }
        if (i >= json.Length)
            throw new FormatException("texto sem fim");
        i++;
        return builder.ToString();
    }

    private static void SkipSpace(string json, ref int i)
    {
        while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == '\uFEFF'))
            i++;
    }
}
