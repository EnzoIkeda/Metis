using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// Troca um texto dentro do codigo-fonte da interface e confere, relendo o resultado, que so ele mudou.
public static class InterfaceTextEditor
{
    // Coluna a partir da qual um texto que ja vinha em varias linhas volta a quebrar.
    private const int WrapColumn = 100;

    public static bool TryEdit(string source, string location, GameTextItem item, string pt, string en, out string result, out string error)
    {
        result = source;
        error = null;
        var before = InterfaceTextParser.Parse(source, location);
        var current = before.FirstOrDefault(other => other.Id == item.Id && other.Field == item.Field);
        if (current == null)
        {
            error = $"{item.Id} não existe mais no arquivo. Atualize a lista.";
            return false;
        }
        if (GameTextSaveRules.Normalize(current.Pt) != GameTextSaveRules.Normalize(item.Pt)
            || GameTextSaveRules.Normalize(current.En) != GameTextSaveRules.Normalize(item.En))
        {
            error = $"{item.Id} mudou no arquivo depois de carregado. Atualize a lista.";
            return false;
        }
        if (current.EditKind != GameTextEditKind.Interface)
        {
            error = current.ReadOnlyReason;
            return false;
        }

        var splices = new List<(InterfaceBranch Branch, string Code)>();
        if (TryPlan(source, current.PtBranch, current.Pt, pt, splices, out error) == false)
            return false;
        if (current.IsPaired && TryPlan(source, current.EnBranch, current.En, en, splices, out error) == false)
            return false;

        var edited = source;
        foreach (var splice in splices.OrderByDescending(s => s.Branch.Start))
            edited = edited.Substring(0, splice.Branch.Start) + splice.Code + edited.Substring(splice.Branch.End);

        if (OnlyTargetChanged(before, InterfaceTextParser.Parse(edited, location), current, pt, en) == false)
        {
            error = "A conferência depois da troca falhou; nada foi mudado.";
            return false;
        }
        result = edited;
        return true;
    }

    private static bool TryPlan(string source, InterfaceBranch branch, string oldText, string newText, List<(InterfaceBranch, string)> splices, out string error)
    {
        error = null;
        var value = GameTextSaveRules.Normalize(newText);
        if (GameTextSaveRules.Normalize(oldText) == value)
            return true;
        if (TryEncode(value, branch, ColumnOf(source, branch.Start), source.Contains("\r\n") ? "\r\n" : "\n", out var code, out error) == false)
            return false;
        splices.Add((branch, code));
        return true;
    }

    private static int ColumnOf(string source, int position)
    {
        var lineStart = source.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;
        return position - lineStart;
    }

    // Gera o literal; quebra de linha no texto vira uma nova linha concatenada no codigo.
    private static bool TryEncode(string text, InterfaceBranch branch, int column, string newLine, out string code, out string error)
    {
        code = null;
        error = null;
        var prefix = branch.Interpolated ? "$\"" : "\"";
        var builder = new StringBuilder(prefix);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var separator = "\" +" + newLine + new string(' ', column) + prefix;
        var wrap = branch.Parts > 1;
        var lineStart = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (branch.Interpolated && c == '{')
            {
                var hole = branch.Holes.FirstOrDefault(known => string.CompareOrdinal(text, i, known, 0, known.Length) == 0);
                if (hole == null)
                {
                    error = "O texto usa uma variável que não existe neste trecho. Use só as originais, sem alterá-las.";
                    return false;
                }
                used.Add(hole);
                builder.Append(hole);
                i += hole.Length - 1;
                continue;
            }
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\t': builder.Append("\\t"); break;
                case '\n':
                    builder.Append("\\n");
                    if (i < text.Length - 1 && text[i + 1] != '\n')
                    {
                        builder.Append(separator);
                        lineStart = builder.Length;
                    }
                    break;
                case ' ' when wrap && column + builder.Length - lineStart >= WrapColumn && i < text.Length - 1:
                    builder.Append(' ').Append(separator);
                    lineStart = builder.Length;
                    break;
                case '}':
                    builder.Append(branch.Interpolated ? "}}" : "}");
                    break;
                default: builder.Append(c); break;
            }
        }

        var missing = branch.Holes.FirstOrDefault(hole => used.Contains(hole) == false);
        if (missing != null)
        {
            error = $"O texto perdeu a variável {missing}.";
            return false;
        }
        code = builder.Append('"').ToString();
        return true;
    }

    private static bool OnlyTargetChanged(List<GameTextItem> before, List<GameTextItem> after, GameTextItem target, string pt, string en)
    {
        if (before.Count != after.Count)
            return false;
        for (var i = 0; i < before.Count; i++)
        {
            var old = before[i];
            var now = after[i];
            if (old.Id != now.Id || old.Field != now.Field || old.IsPaired != now.IsPaired)
                return false;
            var isTarget = old.Id == target.Id && old.Field == target.Field;
            var wantedPt = GameTextSaveRules.Normalize(isTarget ? pt : old.Pt);
            var wantedEn = GameTextSaveRules.Normalize(isTarget && old.IsPaired ? en : old.En);
            if (GameTextSaveRules.Normalize(now.Pt) != wantedPt || GameTextSaveRules.Normalize(now.En) != wantedEn)
                return false;
        }
        return true;
    }
}

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

    // Item de uma lista dentro de um objeto encontrado pelo campo Id.
    public static bool TryReplaceInNested(string json, string listKey, string id, string nestedKey, int index, IReadOnlyList<Change> changes, out string result, out string error)
    {
        return Edit(json, changes, out result, out error, root =>
        {
            var list = root.Kind == NodeKind.Object ? root.Member(listKey) : null;
            var owner = list?.Items.FirstOrDefault(item => item.Kind == NodeKind.Object && item.Member("Id")?.Text == id);
            var nested = owner?.Member(nestedKey);
            return nested != null && index >= 0 && index < nested.Items.Count ? nested.Items[index] : null;
        }, $"Id '{id}', {nestedKey}[{index}]");
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
