using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Texto do relatorio do modelo nos dois idiomas, com uma marca opcional de cor (confianca, faixa).
[Serializable]
public class ReportText
{
    public string Pt;
    public string En;

    // Marca de cor da celula; vazia nas celulas comuns.
    public string Tag;

    // Texto no idioma pedido, com fallback pro outro se a traducao estiver vazia.
    public string Get(bool english)
    {
        if (english)
            return string.IsNullOrEmpty(En) ? Pt ?? string.Empty : En;
        return string.IsNullOrEmpty(Pt) ? En ?? string.Empty : Pt;
    }
}

[Serializable]
public class ReportHeaderField
{
    public ReportText Field = new ReportText();
    public ReportText Value = new ReportText();
}

[Serializable]
public class ReportRow
{
    public ReportText[] Cells = Array.Empty<ReportText>();
}

// Uma aba do relatorio: titulo, introducao, uma tabela, observacoes e links pra fichas ou pro mapa.
[Serializable]
public class ReportSheet
{
    public string Id;
    public ReportText Tab = new ReportText();
    public ReportText Title = new ReportText();
    public ReportText Intro = new ReportText();

    // Nome da tabela gerada a partir dos dados do modelo; vazio nas tabelas escritas a mao.
    public string Auto;
    public ReportHeaderField[] Header = Array.Empty<ReportHeaderField>();
    public ReportText[] Columns = Array.Empty<ReportText>();
    public ReportRow[] Rows = Array.Empty<ReportRow>();
    public ReportText[] Notes = Array.Empty<ReportText>();

    // Ids de fichas do almanaque, ou o do mapa de relacoes.
    public string[] SeeAlso = Array.Empty<string>();
}

[Serializable]
public class ReportFile
{
    public int Version;
    public string ModelVersion;

    // Numeros do modelo usados pra gerar as tabelas, pra saber quando o relatorio ficou desatualizado.
    public string Fingerprint;
    public ReportSheet[] Sheets = Array.Empty<ReportSheet>();
}

// Valores fixos do arquivo do relatorio.
public static class ModelReportLinks
{
    // Link especial que abre o mapa de relacoes em vez de uma ficha.
    public const string RelationMap = "RelationMap";

    // Tabelas geradas a partir dos dados do modelo.
    public static readonly string[] AutoTables = { "Parameters", "Matrix", "Zones", "Wellbeing", "Tiers" };

    // Marcas de cor: confianca do numero e faixas de operacao.
    public static readonly string[] Tags = { "source", "magnitude", "estimate", "design", "simulation", "crisis", "normal", "excess", "critical" };
}

public enum ReportCellStyle
{
    Title,
    Label,
    Text,
    ColumnHeader,
    RowHeader,
    Value,
    Matrix,
    NoteLabel,
    Link,
    AutoTag,
    Blank,
}

// Celula ja posicionada na folha (origem no canto superior esquerdo, y pra baixo).
public struct ReportCell
{
    public int Row;

    // Coluna da tabela; -1 fora dela.
    public int Column;
    public float X;
    public float Y;
    public float Width;
    public float Height;
    public string Text;
    public ReportCellStyle Style;
    public string Tag;
    public string LinkId;

    // Sinal e forca da influencia nas celulas da matriz, de -1 a 1.
    public float Tint;

    // Cabecalho escrito de baixo pra cima, nas colunas estreitas da matriz.
    public bool Vertical;

    // Ultima coluna da tabela, sem risco vertical a direita.
    public bool LastColumn;
}

// Rotulos fixos que a folha precisa, ja no idioma atual.
public class ReportLabels
{
    public string Notes = "Notes";
    public string NoteFormat = "Note {0}";
    public string SeeAlso = "See also";
    public string AutoTag = string.Empty;

    // Nome do link, ou nulo pra deixar o link de fora.
    public Func<string, string> LinkLabel = id => id;
}

// Medidas no tamanho cheio da letra; a folha encolhe tudo junto ate caber.
public class ReportLayoutOptions
{
    public float PageWidth = 920f;
    public float PageHeight = 1400f;
    public float Padding = 12f;
    public float MinRowHeight = 44f;
    public float BlankRowHeight = 18f;
    public int LinksPerRow = 3;
    public float MinScale = 0.5f;
    public float MaxScale = 1f;

    // Largura maxima da coluna de rotulos (campos do cabecalho e observacoes), em fracao da folha.
    public float MaxLabelShare = 0.35f;
}

public class ReportLayout
{
    public readonly List<ReportCell> Cells = new List<ReportCell>();
    public float Width;
    public float Height;

    // Fator aplicado a letra, as margens e as alturas minimas.
    public float Scale = 1f;

    // Falso quando nem a menor letra cabe na folha.
    public bool Fits = true;
    public int ColumnCount;
    public int RowCount;
}

// Monta uma aba como folha de papel: tudo na largura da folha, com a letra reduzida ate a aba caber inteira.
public static class ReportSheetLayout
{
    private enum RowKind
    {
        Full,
        Field,
        Table,
        Links,
        Blank,
    }

    private class Slot
    {
        public string Text;
        public ReportCellStyle Style;
        public string Tag;
        public string LinkId;
        public bool Vertical;

        // Largura do texto sem quebra e da maior palavra, na letra cheia.
        public float Full;
        public float Word;

        // Comprimento do texto deitado, que vira a altura da celula.
        public float Length;
    }

    private class Line
    {
        public RowKind Kind;
        public readonly List<Slot> Slots = new List<Slot>();
    }

    private const int SearchSteps = 8;

    // measureWidth: largura do texto sem quebra; measureHeight: altura com o texto quebrado na largura dada.
    public static ReportLayout Build(
        ReportSheet sheet,
        bool english,
        ReportLabels labels,
        ReportLayoutOptions options,
        Func<string, ReportCellStyle, float> measureWidth,
        Func<string, ReportCellStyle, float, float> measureHeight)
    {
        if (sheet == null)
            return new ReportLayout();

        var columns = Math.Max(2, sheet.Columns.Length);
        var lines = BuildLines(sheet, english, labels, columns);
        foreach (var line in lines)
        {
            foreach (var slot in line.Slots)
                Measure(slot, measureWidth, measureHeight);
        }

        var best = Arrange(lines, columns, options.MaxScale, options, measureHeight);
        if (best.Fits)
            return best;

        // Busca binaria pela maior letra que cabe.
        var low = options.MinScale;
        var high = options.MaxScale;
        best = Arrange(lines, columns, low, options, measureHeight);
        if (best.Fits == false)
            return best;
        for (var i = 0; i < SearchSteps; i++)
        {
            var middle = (low + high) / 2f;
            var attempt = Arrange(lines, columns, middle, options, measureHeight);
            if (attempt.Fits)
            {
                best = attempt;
                low = middle;
            }
            else
            {
                high = middle;
            }
        }
        return best;
    }

    private static void Measure(Slot slot, Func<string, ReportCellStyle, float> measureWidth, Func<string, ReportCellStyle, float, float> measureHeight)
    {
        if (string.IsNullOrEmpty(slot.Text))
            return;
        if (slot.Vertical)
        {
            // Deitado, a largura da coluna e a altura de uma linha de texto.
            slot.Full = slot.Word = measureHeight(slot.Text, slot.Style, float.PositiveInfinity);
            slot.Length = measureWidth(slot.Text, slot.Style);
            return;
        }
        slot.Full = measureWidth(slot.Text, slot.Style);
        foreach (var word in slot.Text.Split(' '))
        {
            if (word.Length > 0)
                slot.Word = Math.Max(slot.Word, measureWidth(word, slot.Style));
        }
    }

    private static ReportLayout Arrange(List<Line> lines, int columns, float scale, ReportLayoutOptions options, Func<string, ReportCellStyle, float, float> measureHeight)
    {
        var layout = new ReportLayout { Scale = scale, ColumnCount = columns, RowCount = lines.Count, Width = options.PageWidth };
        var page = options.PageWidth;
        var padding = options.Padding * scale;
        var widths = ColumnWidths(lines, columns, scale, padding, page, out var overflow);
        var labelWidth = LabelWidth(lines, scale, padding, page * options.MaxLabelShare, ref overflow);
        var matrixMax = MatrixMax(lines);

        var y = 0f;
        for (var r = 0; r < lines.Count; r++)
        {
            var line = lines[r];
            if (line.Kind == RowKind.Blank)
            {
                y += options.BlankRowHeight * scale;
                continue;
            }

            if (line.Kind == RowKind.Links)
            {
                y = AddLinks(layout, line, r, y, scale, padding, options, measureHeight, ref overflow);
                continue;
            }

            var count = line.Slots.Count;
            var xs = new float[count];
            var ws = new float[count];
            for (var i = 0; i < count; i++)
            {
                if (line.Kind == RowKind.Table)
                {
                    xs[i] = i > 0 ? xs[i - 1] + ws[i - 1] : 0f;
                    ws[i] = widths[i];
                }
                else if (line.Kind == RowKind.Field)
                {
                    xs[i] = i == 0 ? 0f : labelWidth;
                    ws[i] = i == 0 ? labelWidth : page - labelWidth;
                }
                else
                {
                    ws[i] = page;
                }
                if (line.Slots[i].Vertical == false && line.Slots[i].Word * scale + 2f * padding > ws[i] + 0.5f)
                    overflow = true;
            }

            var height = options.MinRowHeight * scale;
            for (var i = 0; i < count; i++)
                height = Math.Max(height, SlotHeight(line.Slots[i], ws[i], scale, padding, measureHeight));

            for (var i = 0; i < count; i++)
            {
                var slot = line.Slots[i];
                layout.Cells.Add(new ReportCell
                {
                    Row = r,
                    Column = line.Kind == RowKind.Table ? i : -1,
                    X = xs[i],
                    Y = y,
                    Width = ws[i],
                    Height = height,
                    Text = slot.Text ?? string.Empty,
                    Style = slot.Style,
                    Tag = slot.Tag,
                    LinkId = slot.LinkId,
                    Vertical = slot.Vertical,
                    LastColumn = line.Kind != RowKind.Table || i == count - 1,
                    Tint = slot.Style == ReportCellStyle.Matrix && matrixMax > 0 && TryParseSigned(slot.Text, out var value)
                        ? (float)Math.Max(-1.0, Math.Min(1.0, value / matrixMax))
                        : 0f,
                });
            }
            y += height;
        }
        layout.Height = y;
        layout.Fits = overflow == false && y <= options.PageHeight + 0.5f;
        return layout;
    }

    private static float SlotHeight(Slot slot, float width, float scale, float padding, Func<string, ReportCellStyle, float, float> measureHeight)
    {
        if (string.IsNullOrEmpty(slot.Text))
            return 0f;
        if (slot.Vertical)
            return slot.Length * scale + 2f * padding;
        // Texto em letra menor quebra como o de letra cheia numa largura maior.
        return measureHeight(slot.Text, slot.Style, Math.Max(1f, (width - 2f * padding) / scale)) * scale + 2f * padding;
    }

    private static float AddLinks(ReportLayout layout, Line line, int row, float y, float scale, float padding, ReportLayoutOptions options, Func<string, ReportCellStyle, float, float> measureHeight, ref bool overflow)
    {
        var perRow = Math.Max(1, options.LinksPerRow);
        var width = options.PageWidth / perRow;
        for (var start = 0; start < line.Slots.Count; start += perRow)
        {
            var end = Math.Min(line.Slots.Count, start + perRow);
            var height = options.MinRowHeight * scale;
            for (var i = start; i < end; i++)
            {
                height = Math.Max(height, SlotHeight(line.Slots[i], width, scale, padding, measureHeight));
                if (line.Slots[i].Word * scale + 2f * padding > width + 0.5f)
                    overflow = true;
            }
            for (var i = start; i < end; i++)
            {
                var slot = line.Slots[i];
                layout.Cells.Add(new ReportCell
                {
                    Row = row,
                    Column = -1,
                    X = (i - start) * width,
                    Y = y,
                    Width = width,
                    Height = height,
                    Text = slot.Text,
                    Style = slot.Style,
                    LinkId = slot.LinkId,
                    LastColumn = true,
                });
            }
            y += height;
        }
        return y;
    }

    // Como numa tabela de papel: cada coluna ganha pelo menos a maior palavra, e a sobra vai pra quem tem mais texto.
    private static float[] ColumnWidths(List<Line> lines, int columns, float scale, float padding, float page, out bool overflow)
    {
        var min = new float[columns];
        var max = new float[columns];
        foreach (var line in lines)
        {
            if (line.Kind != RowKind.Table)
                continue;
            for (var c = 0; c < columns && c < line.Slots.Count; c++)
            {
                var slot = line.Slots[c];
                if (string.IsNullOrEmpty(slot.Text))
                    continue;
                min[c] = Math.Max(min[c], slot.Word * scale + 2f * padding);
                max[c] = Math.Max(max[c], slot.Full * scale + 2f * padding);
            }
        }
        var minTotal = 0f;
        var maxTotal = 0f;
        for (var c = 0; c < columns; c++)
        {
            min[c] = Math.Max(min[c], 2f * padding);
            max[c] = Math.Max(max[c], min[c]);
            minTotal += min[c];
            maxTotal += max[c];
        }

        overflow = minTotal > page + 0.5f;
        var widths = new float[columns];
        for (var c = 0; c < columns; c++)
        {
            if (maxTotal <= page)
                widths[c] = max[c] + (page - maxTotal) * max[c] / maxTotal;
            else if (overflow)
                widths[c] = min[c] * page / minTotal;
            else
                widths[c] = min[c] + (page - minTotal) * (max[c] - min[c]) / (maxTotal - minTotal);
        }
        return widths;
    }

    private static float LabelWidth(List<Line> lines, float scale, float padding, float limit, ref bool overflow)
    {
        var width = 0f;
        foreach (var line in lines)
        {
            if (line.Kind == RowKind.Field && line.Slots.Count > 0)
            {
                var slot = line.Slots[0];
                width = Math.Max(width, Math.Min(slot.Full * scale + 2f * padding, limit));
                if (slot.Word * scale + 2f * padding > limit + 0.5f)
                    overflow = true;
            }
        }
        return width;
    }

    private static List<Line> BuildLines(ReportSheet sheet, bool english, ReportLabels labels, int columns)
    {
        var lines = new List<Line>();
        lines.Add(Full(sheet.Title.Get(english), ReportCellStyle.Title));

        if (sheet.Header.Length > 0)
        {
            foreach (var field in sheet.Header)
                lines.Add(Field(field.Field.Get(english), ReportCellStyle.Label, field.Value.Get(english)));
            lines.Add(new Line { Kind = RowKind.Blank });
        }

        lines.Add(Full(sheet.Intro.Get(english), ReportCellStyle.Text));
        lines.Add(new Line { Kind = RowKind.Blank });

        if (string.IsNullOrEmpty(sheet.Auto) == false && string.IsNullOrEmpty(labels.AutoTag) == false)
            lines.Add(Full(labels.AutoTag, ReportCellStyle.AutoTag));

        var matrix = sheet.Auto == "Matrix";
        var header = new Line { Kind = RowKind.Table };
        for (var c = 0; c < columns; c++)
        {
            header.Slots.Add(new Slot
            {
                Text = c < sheet.Columns.Length ? sheet.Columns[c].Get(english) : string.Empty,
                Style = ReportCellStyle.ColumnHeader,
                Vertical = matrix && c > 0,
            });
        }
        lines.Add(header);

        var valueStyle = matrix ? ReportCellStyle.Matrix : ReportCellStyle.Value;
        foreach (var row in sheet.Rows)
        {
            var line = new Line { Kind = RowKind.Table };
            for (var c = 0; c < columns; c++)
            {
                var cell = row.Cells != null && c < row.Cells.Length ? row.Cells[c] : null;
                line.Slots.Add(new Slot
                {
                    Text = cell?.Get(english) ?? string.Empty,
                    Style = c == 0 ? ReportCellStyle.RowHeader : valueStyle,
                    Tag = cell?.Tag,
                });
            }
            lines.Add(line);
        }

        if (sheet.Notes.Length > 0)
        {
            lines.Add(new Line { Kind = RowKind.Blank });
            lines.Add(Full(labels.Notes, ReportCellStyle.Label));
            for (var i = 0; i < sheet.Notes.Length; i++)
            {
                var label = string.Format(CultureInfo.InvariantCulture, labels.NoteFormat, i + 1);
                lines.Add(Field(label, ReportCellStyle.NoteLabel, sheet.Notes[i].Get(english)));
            }
        }

        var links = new Line { Kind = RowKind.Links };
        foreach (var id in sheet.SeeAlso)
        {
            var name = labels.LinkLabel?.Invoke(id);
            if (string.IsNullOrEmpty(name) == false)
                links.Slots.Add(new Slot { Text = name, Style = ReportCellStyle.Link, LinkId = id });
        }
        if (links.Slots.Count > 0)
        {
            lines.Add(new Line { Kind = RowKind.Blank });
            lines.Add(Full(labels.SeeAlso, ReportCellStyle.Label));
            lines.Add(links);
        }
        return lines;
    }

    private static double MatrixMax(List<Line> lines)
    {
        var max = 0.0;
        foreach (var line in lines)
        {
            foreach (var slot in line.Slots)
            {
                if (slot.Style == ReportCellStyle.Matrix && TryParseSigned(slot.Text, out var value))
                    max = Math.Max(max, Math.Abs(value));
            }
        }
        return max;
    }

    // Aceita "+0,4", "-1.3" e "0,5".
    public static bool TryParseSigned(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var cleaned = text.Trim().Replace(',', '.');
        return double.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static Line Full(string text, ReportCellStyle style)
    {
        var line = new Line { Kind = RowKind.Full };
        line.Slots.Add(new Slot { Text = text, Style = style });
        return line;
    }

    private static Line Field(string label, ReportCellStyle labelStyle, string text)
    {
        var line = new Line { Kind = RowKind.Field };
        line.Slots.Add(new Slot { Text = label, Style = labelStyle });
        line.Slots.Add(new Slot { Text = text, Style = ReportCellStyle.Text });
        return line;
    }
}

// Numeros do modelo de que as tabelas do relatorio dependem, na ordem dos enums.
public class ModelReportNumbers
{
    public double[] Initial = Array.Empty<double>();
    public double[] Min = Array.Empty<double>();
    public double[] Max = Array.Empty<double>();
    public double[] Critical = Array.Empty<double>();
    public double[] Deriva = Array.Empty<double>();

    // Neutro, escala, crise, excesso, pressao na crise, apoio na crise, apoio no excesso, correcao e penalidade de Colapso.
    public double[] Interaction = Array.Empty<double>();

    // Coeficientes alvo por fonte, linha a linha.
    public double[] Matrix = Array.Empty<double>();
    public double[] RealMin = Array.Empty<double>();
    public double[] RealMax = Array.Empty<double>();
    public int HandSize;
    public int VictoryTurns;
    public int[] TierCounts = Array.Empty<int>();
    public int Events;
    public int ConditionalEvents;
}

// Texto curto e estavel com os numeros do modelo; o gerador do relatorio grava o mesmo texto.
public static class ModelReportFingerprint
{
    public static string Build(ModelReportNumbers numbers)
    {
        var builder = new StringBuilder();
        builder.Append("P:");
        for (var i = 0; i < numbers.Initial.Length; i++)
        {
            if (i > 0)
                builder.Append(';');
            builder.Append(Join("/", numbers.Initial[i], numbers.Min[i], numbers.Max[i], numbers.Critical[i], numbers.Deriva[i]));
        }
        builder.Append("|Z:").Append(Join("/", numbers.Interaction));
        builder.Append("|M:").Append(Join(",", numbers.Matrix));
        builder.Append("|R:");
        for (var i = 0; i < numbers.RealMin.Length; i++)
        {
            if (i > 0)
                builder.Append(';');
            builder.Append(Join("/", numbers.RealMin[i], numbers.RealMax[i]));
        }
        builder.Append("|T:").Append(numbers.HandSize.ToString(CultureInfo.InvariantCulture))
            .Append('/').Append(numbers.VictoryTurns.ToString(CultureInfo.InvariantCulture));
        builder.Append("|C:");
        for (var i = 0; i < numbers.TierCounts.Length; i++)
        {
            if (i > 0)
                builder.Append('/');
            builder.Append(numbers.TierCounts[i].ToString(CultureInfo.InvariantCulture));
        }
        builder.Append("|E:").Append(numbers.Events.ToString(CultureInfo.InvariantCulture))
            .Append('/').Append(numbers.ConditionalEvents.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static string Join(string separator, params double[] values)
    {
        var parts = new string[values.Length];
        for (var i = 0; i < values.Length; i++)
            parts[i] = Format(values[i]);
        return string.Join(separator, parts);
    }

    // Tres casas, ponto decimal, sem "-0.000".
    public static string Format(double value)
    {
        var text = value.ToString("0.000", CultureInfo.InvariantCulture);
        return text == "-0.000" ? "0.000" : text;
    }
}
