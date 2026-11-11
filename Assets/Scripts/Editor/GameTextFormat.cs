using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public enum GameTextOrigin
{
    Card,
    Event,
    Advantage,
    Preset,
    Structure,
    Knowledge,
    News,
    RelationNote,
    ParameterUnit,
    Report,
    Interface,
    SceneOrPrefab,
    Theme,
    Dilemma,
}

public enum GameTextIssue
{
    MissingPortuguese,
    MissingEnglish,
    LikelyUntranslated,
    PlaceholderMismatch,
    Dash,
    MetisWithoutAccent,
    YearsInsteadOfTurns,
    ExtraSpace,
    AccentInTitleFont,
}

// Como um texto pode ser gravado; somente leitura quando a fonte ainda nao tem edicao segura.
public enum GameTextEditKind
{
    ReadOnly,
    Asset,
    News,
    Knowledge,
    RelationNote,
    Interface,
    NewsColumn,
    Theme,
    Dilemma,
}

// Trecho do codigo-fonte onde esta um ramo de idioma; so e editavel se for feito apenas de textos.
public class InterfaceBranch
{
    public int Start = -1;
    public int End = -1;
    public bool LiteralsOnly = true;
    public bool Interpolated;
    public bool Verbatim;
    public int Parts;
    public readonly List<string> Holes = new List<string>();

    public bool IsEditable => Start >= 0 && LiteralsOnly && Verbatim == false;

    public void Add(int start, int end, bool interpolated, bool verbatim, IEnumerable<string> holes)
    {
        if (Start < 0)
            Start = start;
        End = end;
        Parts++;
        Interpolated |= interpolated;
        Verbatim |= verbatim;
        Holes.AddRange(holes);
    }
}

// Um texto do jogo. Sem par quer dizer um campo unico, sem versao declarada nos dois idiomas.
public class GameTextItem
{
    public GameTextOrigin Origin;
    public string Id = string.Empty;
    public string Field = string.Empty;
    public string Pt = string.Empty;
    public string En = string.Empty;
    public bool IsPaired = true;
    public bool UsesTitleFont;
    public string Location = string.Empty;
    public int Line;

    public GameTextEditKind EditKind;
    public string ReadOnlyReason = string.Empty;
    public string PtField = string.Empty;
    public string EnField = string.Empty;
    public string JsonPtKey = string.Empty;
    public string JsonEnKey = string.Empty;

    // Lista do JSON onde o item esta, e a lista dentro dele quando o texto e de uma opcao.
    public string JsonListKey = string.Empty;
    public string JsonNestedKey = string.Empty;
    public string NoteSource = string.Empty;
    public string NoteTarget = string.Empty;
    public int ArrayIndex;
    public InterfaceBranch PtBranch;
    public InterfaceBranch EnBranch;
}

// Regras de verificacao, sem dependencia da Unity pra poder ser testadas fora do Editor.
public static class GameTextRules
{
    private const int MinUntranslatedLength = 12;

    private static readonly Regex Placeholder = new Regex(@"\{([A-Za-z0-9_.]+)(?::[^{}]*)?\}", RegexOptions.Compiled);
    private static readonly Regex MarkupOrHole = new Regex(@"<[^<>]*>|\{[^{}]*\}", RegexOptions.Compiled);
    private static readonly Regex Metis = new Regex(@"\bMetis\b", RegexOptions.Compiled);
    private static readonly Regex AnyYears = new Regex(@"\b(anos?|years?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhaseYears = new Regex(@"\b20\s+(anos|years)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<GameTextIssue> Check(GameTextItem item)
    {
        var issues = new List<GameTextIssue>();
        var pt = item.Pt ?? string.Empty;
        var en = item.En ?? string.Empty;

        if (item.IsPaired)
        {
            if (string.IsNullOrWhiteSpace(pt) && HasLetter(en))
                issues.Add(GameTextIssue.MissingPortuguese);
            if (string.IsNullOrWhiteSpace(en) && HasLetter(pt))
                issues.Add(GameTextIssue.MissingEnglish);
            if (pt == en && Visible(pt).Length >= MinUntranslatedLength && HasLetter(pt))
                issues.Add(GameTextIssue.LikelyUntranslated);
            if (pt.Length > 0 && en.Length > 0 && Placeholders(pt).SetEquals(Placeholders(en)) == false)
                issues.Add(GameTextIssue.PlaceholderMismatch);
        }

        var both = pt + "\n" + en;
        if (both.IndexOf('\u2014') >= 0 || both.IndexOf('\u2013') >= 0)
            issues.Add(GameTextIssue.Dash);
        if (Metis.IsMatch(both))
            issues.Add(GameTextIssue.MetisWithoutAccent);

        // Fatos reais citam anos em fichas e notas; na interface a fase e sempre em turnos.
        var isPlayerFrame = item.Origin == GameTextOrigin.Interface || item.Origin == GameTextOrigin.SceneOrPrefab;
        if ((isPlayerFrame ? AnyYears : PhaseYears).IsMatch(both))
            issues.Add(GameTextIssue.YearsInsteadOfTurns);

        // Na interface o espaco nas pontas costuma ser separador de uma concatenacao.
        var ignoreEdges = item.Origin == GameTextOrigin.Interface;
        if (HasExtraSpace(pt, ignoreEdges) || HasExtraSpace(en, ignoreEdges))
            issues.Add(GameTextIssue.ExtraSpace);
        if (item.UsesTitleFont && both.Any(c => c > 127 && char.IsLetter(c)))
            issues.Add(GameTextIssue.AccentInTitleFont);
        return issues;
    }

    public static bool Matches(GameTextItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;
        return new[] { item.Id, item.Field, item.Pt, item.En }.Any(text => (text ?? string.Empty).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
    }

    // Tem algo de texto alem de marcacao e buracos de codigo.
    public static bool HasLetter(string text)
    {
        return Visible(text).Any(char.IsLetter);
    }

    private static string Visible(string text)
    {
        return MarkupOrHole.Replace(text ?? string.Empty, string.Empty).Trim();
    }

    private static HashSet<string> Placeholders(string text)
    {
        return new HashSet<string>(Placeholder.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value), StringComparer.Ordinal);
    }

    private static bool HasExtraSpace(string text, bool ignoreEdges)
    {
        return text.Length > 0 && ((ignoreEdges == false && text != text.Trim()) || text.Contains("  "));
    }
}

// Le a tabela de textos de interface como codigo-fonte, sem executar nada.
public static class InterfaceTextParser
{
    private enum TokenKind
    {
        Word,
        Text,
        Symbol,
    }

    private class Token
    {
        public TokenKind Kind;
        public string Value;
        public int Line;
        public int Start;
        public int End;
        public bool Interpolated;
        public bool Verbatim;
        public List<string> Holes = new List<string>();
    }

    private const string LanguageTest = "IsPt";
    private const string CodePart = "{...}";
    private const string MixedCodeReason = "Este texto mistura código: use Abrir origem.";

    public static List<GameTextItem> Parse(string source, string location)
    {
        var tokens = Tokenize(source);
        var items = new List<GameTextItem>();
        var i = 0;
        while (i < tokens.Count)
        {
            if (IsMemberStart(tokens, i) == false)
            {
                i++;
                continue;
            }
            var end = i + 2;
            while (end < tokens.Count && IsMemberStart(tokens, end) == false)
                end++;
            ParseMember(tokens, i, end, location, items);
            i = end;
        }
        return items;
    }

    private static bool IsMemberStart(List<Token> tokens, int i)
    {
        return i + 1 < tokens.Count && tokens[i + 1].Value == "static"
            && (tokens[i].Value == "public" || tokens[i].Value == "private");
    }

    private static void ParseMember(List<Token> tokens, int start, int end, string location, List<GameTextItem> items)
    {
        // Nome: a palavra antes do primeiro "=>", "(" ou "{" da declaracao.
        var name = string.Empty;
        for (var i = start; i < end; i++)
        {
            if (tokens[i].Value == "=>" || tokens[i].Value == "(" || tokens[i].Value == "{")
                break;
            if (tokens[i].Kind == TokenKind.Word)
                name = tokens[i].Value;
        }

        var label = string.Empty;
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        var cursor = start;
        while (cursor < end)
        {
            var token = tokens[cursor];
            if (token.Value == "case" && token.Kind == TokenKind.Word)
            {
                var builder = new StringBuilder();
                cursor++;
                while (cursor < end && tokens[cursor].Value != ":")
                    builder.Append(tokens[cursor++].Value);
                label = builder.ToString();
                cursor++;
                continue;
            }

            // O rotulo vale ate o proximo caso ou o fim do bloco.
            if (token.Value == "default" && token.Kind == TokenKind.Word)
                label = "default";
            else if (token.Value == "}" && token.Kind == TokenKind.Symbol)
                label = string.Empty;

            if (token.Value == LanguageTest && cursor + 1 < end && tokens[cursor + 1].Value == "?")
            {
                var pt = ReadBranch(tokens, cursor + 2, end, out var afterPt, out var ptBranch);
                var en = ReadBranch(tokens, afterPt + 1, end, out var afterEn, out var enBranch);
                var editable = ptBranch.IsEditable && enBranch.IsEditable;
                items.Add(new GameTextItem
                {
                    Origin = GameTextOrigin.Interface,
                    Id = name,
                    Field = NextField(label, used),
                    Pt = pt,
                    En = en,
                    Location = location,
                    Line = token.Line,
                    EditKind = editable ? GameTextEditKind.Interface : GameTextEditKind.ReadOnly,
                    ReadOnlyReason = editable ? string.Empty : MixedCodeReason,
                    PtBranch = ptBranch,
                    EnBranch = enBranch,
                });
                cursor = afterEn;
                continue;
            }

            if (token.Kind == TokenKind.Text && GameTextRules.HasLetter(token.Value))
            {
                var branch = new InterfaceBranch();
                branch.Add(token.Start, token.End, token.Interpolated, token.Verbatim, token.Holes);
                items.Add(new GameTextItem
                {
                    Origin = GameTextOrigin.Interface,
                    Id = name,
                    Field = NextField(label, used),
                    Pt = token.Value,
                    IsPaired = false,
                    Location = location,
                    Line = token.Line,
                    EditKind = branch.IsEditable ? GameTextEditKind.Interface : GameTextEditKind.ReadOnly,
                    ReadOnlyReason = branch.IsEditable ? string.Empty : MixedCodeReason,
                    PtBranch = branch,
                });
            }
            cursor++;
        }
    }

    private static string NextField(string label, Dictionary<string, int> used)
    {
        used.TryGetValue(label, out var count);
        used[label] = count + 1;
        if (count == 0)
            return label;
        return string.IsNullOrEmpty(label) ? $"#{count + 1}" : $"{label} #{count + 1}";
    }

    // Junta as partes de um ramo do teste de idioma; para no separador do ramo ou no fim da expressao.
    private static string ReadBranch(List<Token> tokens, int start, int end, out int stop, out InterfaceBranch branch)
    {
        branch = new InterfaceBranch();
        var builder = new StringBuilder();
        var depth = 0;
        var pendingTernaries = 0;
        var inCode = false;
        var i = start;
        for (; i < end; i++)
        {
            var token = tokens[i];
            if (depth == 0 && pendingTernaries == 0 && (token.Value == ":" || token.Value == ";" || token.Value == "," || token.Value == ")" || token.Value == "]" || token.Value == "}"))
                break;
            if (token.Kind == TokenKind.Text)
            {
                builder.Append(token.Value);
                branch.Add(token.Start, token.End, token.Interpolated, token.Verbatim, token.Holes);
                inCode = false;
                continue;
            }
            if (token.Value == "(" || token.Value == "[" || token.Value == "{")
                depth++;
            else if (token.Value == ")" || token.Value == "]" || token.Value == "}")
                depth--;
            else if (depth == 0 && token.Value == "?")
                pendingTernaries++;
            else if (depth == 0 && token.Value == ":")
                pendingTernaries--;

            if (token.Value == "+" && depth == 0)
            {
                inCode = false;
                continue;
            }
            branch.LiteralsOnly = false;
            if (inCode == false)
            {
                builder.Append(CodePart);
                inCode = true;
            }
        }
        stop = i;
        return builder.ToString();
    }

    private static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var line = 1;
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (c == '\n')
            {
                line++;
                i++;
            }
            else if (char.IsWhiteSpace(c) || c == '﻿')
            {
                i++;
            }
            else if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n')
                    i++;
            }
            else if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                var close = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                var stop = close < 0 ? source.Length : close + 2;
                line += source.Substring(i, stop - i).Count(ch => ch == '\n');
                i = stop;
            }
            else if (c == '\'')
            {
                i++;
                while (i < source.Length && source[i] != '\'')
                    i += source[i] == '\\' ? 2 : 1;
                i++;
                tokens.Add(new Token { Kind = TokenKind.Symbol, Value = "'", Line = line });
            }
            else if (TryReadString(source, i, out var literal))
            {
                tokens.Add(new Token { Kind = TokenKind.Text, Value = literal.Text, Line = line, Start = i, End = literal.End, Interpolated = literal.Interpolated, Verbatim = literal.Verbatim, Holes = literal.Holes });
                line += literal.Lines;
                i = literal.End;
            }
            else if (char.IsLetterOrDigit(c) || c == '_')
            {
                var start = i;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                    i++;
                tokens.Add(new Token { Kind = TokenKind.Word, Value = source.Substring(start, i - start), Line = line });
            }
            else if (c == '=' && i + 1 < source.Length && source[i + 1] == '>')
            {
                tokens.Add(new Token { Kind = TokenKind.Symbol, Value = "=>", Line = line });
                i += 2;
            }
            else if (c == '=' && i + 1 < source.Length && source[i + 1] == '=')
            {
                tokens.Add(new Token { Kind = TokenKind.Symbol, Value = "==", Line = line });
                i += 2;
            }
            else if (c == '?' && i + 1 < source.Length && (source[i + 1] == '?' || source[i + 1] == '.'))
            {
                tokens.Add(new Token { Kind = TokenKind.Symbol, Value = source.Substring(i, 2), Line = line });
                i += 2;
            }
            else
            {
                tokens.Add(new Token { Kind = TokenKind.Symbol, Value = c.ToString(), Line = line });
                i++;
            }
        }
        return tokens;
    }

    private class Literal
    {
        public string Text;
        public int End;
        public int Lines;
        public bool Interpolated;
        public bool Verbatim;
        public readonly List<string> Holes = new List<string>();
    }

    private static bool TryReadString(string source, int i, out Literal literal)
    {
        literal = null;
        var lines = 0;
        var start = i;
        var interpolated = false;
        var verbatim = false;
        while (start < source.Length && (source[start] == '$' || source[start] == '@'))
        {
            interpolated |= source[start] == '$';
            verbatim |= source[start] == '@';
            start++;
        }
        if (start >= source.Length || source[start] != '"')
            return false;

        literal = new Literal { Interpolated = interpolated, Verbatim = verbatim };
        var builder = new StringBuilder();
        var cursor = start + 1;
        while (cursor < source.Length)
        {
            var c = source[cursor];
            if (c == '\n')
                lines++;
            if (verbatim && c == '"' && cursor + 1 < source.Length && source[cursor + 1] == '"')
            {
                builder.Append('"');
                cursor += 2;
            }
            else if (c == '"')
            {
                cursor++;
                break;
            }
            else if (verbatim == false && c == '\\')
            {
                builder.Append(DecodeEscape(source, ref cursor));
            }
            else if (interpolated && c == '{' && cursor + 1 < source.Length && source[cursor + 1] == '{')
            {
                builder.Append('{');
                cursor += 2;
            }
            else if (interpolated && c == '}' && cursor + 1 < source.Length && source[cursor + 1] == '}')
            {
                builder.Append('}');
                cursor += 2;
            }
            else if (interpolated && c == '{')
            {
                var close = FindHoleEnd(source, cursor);
                builder.Append(source, cursor, close - cursor + 1);
                literal.Holes.Add(source.Substring(cursor, close - cursor + 1));
                cursor = close + 1;
            }
            else
            {
                builder.Append(c);
                cursor++;
            }
        }
        literal.Text = builder.ToString();
        literal.End = cursor;
        literal.Lines = lines;
        return true;
    }

    // Um buraco pode ter strings e chaves dentro; o texto dele fica cru.
    private static int FindHoleEnd(string source, int open)
    {
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            var c = source[i];
            if (c == '"')
            {
                i++;
                while (i < source.Length && source[i] != '"')
                    i += source[i] == '\\' ? 2 : 1;
            }
            else if (c == '{')
            {
                depth++;
            }
            else if (c == '}' && --depth == 0)
            {
                return i;
            }
        }
        return source.Length - 1;
    }

    private static string DecodeEscape(string source, ref int cursor)
    {
        var next = cursor + 1 < source.Length ? source[cursor + 1] : '\\';
        cursor += 2;
        switch (next)
        {
            case 'n': return "\n";
            case 't': return "\t";
            case 'r': return "\r";
            case '0': return "\0";
            case 'u':
                var hex = source.Substring(cursor, Math.Min(4, source.Length - cursor));
                cursor += hex.Length;
                return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) ? ((char)code).ToString() : string.Empty;
            default: return next.ToString();
        }
    }
}

// Le os textos gravados em cenas e prefabs direto do arquivo, sem abrir a cena no Editor.
public static class SceneTextParser
{
    private const string TextKey = "  m_text: ";
    private const string FontKey = "  m_fontAsset: ";
    private const string NameKey = "  m_Name: ";
    private const string OwnerKey = "  m_GameObject: {fileID: ";

    private class Pending
    {
        public string Text;
        public int Line;
        public string ComponentId;
        public string OwnerId;
        public bool UsesTitleFont;
    }

    public static List<GameTextItem> Parse(string content, string location, string titleFontGuid)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new List<Pending>();
        var docId = string.Empty;
        var isGameObject = false;
        var owner = string.Empty;
        Pending current = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("--- !u!", StringComparison.Ordinal))
            {
                var parts = line.Split(' ');
                isGameObject = parts[1] == "!u!1";
                docId = parts.Length > 2 ? parts[2].TrimStart('&') : string.Empty;
                owner = string.Empty;
                current = null;
            }
            else if (isGameObject && line.StartsWith(NameKey, StringComparison.Ordinal))
            {
                names[docId] = DecodeScalar(line.Substring(NameKey.Length), Array.Empty<string>());
            }
            else if (line.StartsWith(OwnerKey, StringComparison.Ordinal))
            {
                owner = line.Substring(OwnerKey.Length).TrimEnd('}', ' ');
            }
            else if (line.StartsWith(TextKey, StringComparison.Ordinal))
            {
                var startLine = i + 1;
                var extra = new List<string>();
                while (i + 1 < lines.Length && IsContinuation(lines[i + 1]))
                    extra.Add(lines[++i]);
                current = new Pending
                {
                    Text = DecodeScalar(line.Substring(TextKey.Length), extra),
                    Line = startLine,
                    ComponentId = docId,
                    OwnerId = owner,
                };
                pending.Add(current);
            }
            else if (current != null && line.StartsWith(FontKey, StringComparison.Ordinal))
            {
                current.UsesTitleFont = string.IsNullOrEmpty(titleFontGuid) == false && line.Contains(titleFontGuid);
            }
        }

        var items = new List<GameTextItem>();
        foreach (var entry in pending)
        {
            if (GameTextRules.HasLetter(entry.Text) == false)
                continue;
            items.Add(new GameTextItem
            {
                Origin = GameTextOrigin.SceneOrPrefab,
                Id = names.TryGetValue(entry.OwnerId, out var name) ? name : entry.OwnerId,
                Field = entry.ComponentId,
                Pt = entry.Text,
                ReadOnlyReason = "Texto de cena ou prefab: edite no próprio arquivo pela Unity.",
                IsPaired = false,
                UsesTitleFont = entry.UsesTitleFont,
                Location = location,
                Line = entry.Line,
            });
        }
        return items;
    }

    private static bool IsContinuation(string line)
    {
        return line.Length == 0 || line.StartsWith("   ", StringComparison.Ordinal);
    }

    private static string DecodeScalar(string first, IReadOnlyList<string> extra)
    {
        var raw = Fold(first, extra);
        if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
            return DecodeDoubleQuoted(raw.Substring(1, raw.Length - 2));
        if (raw.Length >= 2 && raw[0] == '\'' && raw[raw.Length - 1] == '\'')
            return raw.Substring(1, raw.Length - 2).Replace("''", "'");
        return raw;
    }

    // Quebra simples vira espaco, linha em branco vira quebra, barra no fim une sem espaco.
    private static string Fold(string first, IReadOnlyList<string> extra)
    {
        var builder = new StringBuilder(first.TrimEnd());
        var blanks = 0;
        foreach (var line in extra)
        {
            var part = line.Trim();
            if (part.Length == 0)
            {
                blanks++;
                continue;
            }
            if (blanks > 0)
            {
                builder.Append('\n', blanks);
            }
            else if (builder.Length > 0 && builder[builder.Length - 1] == '\\')
            {
                builder.Length--;
                if (part.StartsWith("\\ ", StringComparison.Ordinal))
                    part = part.Substring(1);
            }
            else
            {
                builder.Append(' ');
            }
            blanks = 0;
            builder.Append(part);
        }
        return builder.ToString();
    }

    private static string DecodeDoubleQuoted(string text)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\' || i + 1 >= text.Length)
            {
                builder.Append(text[i]);
                continue;
            }
            var next = text[++i];
            switch (next)
            {
                case 'n': builder.Append('\n'); break;
                case 't': builder.Append('\t'); break;
                case 'r': builder.Append('\r'); break;
                case 'x': i = AppendHex(text, i, 2, builder); break;
                case 'u': i = AppendHex(text, i, 4, builder); break;
                default: builder.Append(next); break;
            }
        }
        return builder.ToString();
    }

    private static int AppendHex(string text, int marker, int size, StringBuilder builder)
    {
        if (marker + size < text.Length && int.TryParse(text.Substring(marker + 1, size), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
            builder.Append((char)code);
        return marker + size;
    }
}
