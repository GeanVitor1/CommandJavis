using System.Globalization;
using System.Text;

namespace Vox;

public static class Calculator
{
    private static readonly Dictionary<string, long> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["um"] = 1, ["uma"] = 1, ["dois"] = 2, ["duas"] = 2, ["tres"] = 3,
        ["quatro"] = 4, ["cinco"] = 5, ["seis"] = 6, ["sete"] = 7, ["oito"] = 8, ["nove"] = 9,
        ["dez"] = 10, ["onze"] = 11, ["doze"] = 12, ["treze"] = 13, ["quatorze"] = 14,
        ["catorze"] = 14, ["quinze"] = 15, ["dezesseis"] = 16, ["dezessete"] = 17,
        ["dezoito"] = 18, ["dezenove"] = 19
    };

    private static readonly Dictionary<string, long> Tens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vinte"] = 20, ["trinta"] = 30, ["quarenta"] = 40, ["cinquenta"] = 50, ["cincoenta"] = 50,
        ["sessenta"] = 60, ["setenta"] = 70, ["oitenta"] = 80, ["noventa"] = 90
    };

    private static readonly Dictionary<string, long> Hundreds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cem"] = 100, ["cento"] = 100, ["duzentos"] = 200, ["duzentas"] = 200,
        ["trezentos"] = 300, ["trezentas"] = 300, ["quatrocentos"] = 400, ["quatrocentas"] = 400,
        ["quinhentos"] = 500, ["quinhentas"] = 500, ["seiscentos"] = 600, ["seiscentas"] = 600,
        ["setecentos"] = 700, ["setecentas"] = 700, ["oitocentos"] = 800, ["oitocentas"] = 800,
        ["novecentos"] = 900, ["novecentas"] = 900
    };

    public static double? Evaluate(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr))
            return null;

        var t = Normalize(expr);

        var percent = TryPercent(t);
        if (percent != null)
            return percent;

        var tokens = Tokenize(t);
        if (tokens.Count == 0)
            return null;

        try
        {
            var pos = 0;
            var result = ParseAdd(tokens, ref pos);
            if (pos != tokens.Count)
                return null;
            return result;
        }
        catch
        {
            return null;
        }
    }

    public static bool TryParseNumber(string token, out double value)
    {
        token = token.Trim().Trim(',').Replace(".", ",");
        var cleaned = Normalize(token);
        if (cleaned.Length == 0)
        {
            value = 0;
            return false;
        }

        if (cleaned.All(c => char.IsDigit(c) || c == ',' || c == '.'))
        {
            if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
            if (double.TryParse(cleaned, NumberStyles.Float, new CultureInfo("pt-BR"), out value))
                return true;
            value = 0;
            return false;
        }

        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var parsed = ParseNumberWords(words, 0, out var consumed);
        if (parsed != null && consumed == words.Length)
        {
            value = parsed.Value;
            return true;
        }

        value = 0;
        return false;
    }

    private static double? TryPercent(string t)
    {
        var i = t.IndexOf("porcento", StringComparison.OrdinalIgnoreCase);
        if (i < 0)
            i = t.IndexOf("por cento", StringComparison.OrdinalIgnoreCase);
        if (i < 0)
            return null;

        var left = t[..i].Trim();
        if (!TryParseNumber(left, out var pct))
            return null;

        var rest = t[(i + ("porcento".Length))..].Trim();
        if (StripStartWord(ref rest, "de ", "do ", "da ", "dos ", "das ", "em "))
        {
        }
        if (rest.Length > 0 && TryParseNumber(rest, out var baseVal))
            return pct / 100.0 * baseVal;
        return pct / 100.0;
    }

    private static List<string> Tokenize(string t)
    {
        t = t.Replace("multiplicado por", " * ")
             .Replace("multiplicar por", " * ")
             .Replace("vezes", " * ")
             .Replace("elevado a", " ^ ")
             .Replace("dividido por", " / ")
             .Replace("dividido pelo", " / ")
             .Replace("dividir por", " / ")
             .Replace("menos", " - ")
             .Replace("mais", " + ")
             .Replace("aberto parenteses", " ( ")
             .Replace("fechado parenteses", " ) ")
             .Replace("parenteses aberto", " ( ")
             .Replace("parenteses fechado", " ) ")
             .Replace("porcento", " % ")
             .Replace("por cento", " % ");

        return t.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static double ParseAdd(List<string> tokens, ref int pos)
    {
        var left = ParseMul(tokens, ref pos);
        while (pos < tokens.Count && (tokens[pos] == "+" || tokens[pos] == "-"))
        {
            var op = tokens[pos];
            pos++;
            var right = ParseMul(tokens, ref pos);
            left = op == "+" ? left + right : left - right;
        }
        return left;
    }

    private static double ParseMul(List<string> tokens, ref int pos)
    {
        var left = ParsePower(tokens, ref pos);
        while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/"))
        {
            var op = tokens[pos];
            pos++;
            var right = ParsePower(tokens, ref pos);
            left = op == "*" ? left * right : left / right;
        }
        return left;
    }

    private static double ParsePower(List<string> tokens, ref int pos)
    {
        var left = ParsePrimary(tokens, ref pos);
        if (pos < tokens.Count && tokens[pos] == "^")
        {
            pos++;
            var right = ParsePower(tokens, ref pos);
            return Math.Pow(left, right);
        }
        return left;
    }

    private static double ParsePrimary(List<string> tokens, ref int pos)
    {
        if (pos >= tokens.Count)
            throw new InvalidOperationException();
        var tok = tokens[pos];
        if (tok == "(")
        {
            pos++;
            var val = ParseAdd(tokens, ref pos);
            if (pos >= tokens.Count || tokens[pos] != ")")
                throw new InvalidOperationException();
            pos++;
            return val;
        }
        if (tok == "-")
        {
            pos++;
            return -ParsePrimary(tokens, ref pos);
        }
        if (tok == "%")
        {
            pos++;
            return ParsePrimary(tokens, ref pos) / 100.0;
        }

        if (tok.Any(c => char.IsDigit(c)))
        {
            if (TryParseNumber(tok, out var num))
            {
                pos++;
                return num;
            }
            throw new InvalidOperationException();
        }

        var numberWords = ParseNumberWords(tokens, pos, out var consumed);
        if (numberWords != null && consumed > pos)
        {
            pos = consumed;
            return numberWords.Value;
        }

        throw new InvalidOperationException();
    }

    private static double? ParseNumberWords(IReadOnlyList<string> words, int start, out int consumed)
    {
        consumed = start;
        if (start >= words.Count)
            return null;

        double total = 0;
        double current = 0;
        var pos = start;
        var any = false;

        while (pos < words.Count)
        {
            var w = words[pos];

            if (w is "milhao" or "milhoes")
            {
                if (!any) current = 1;
                total += current * 1_000_000;
                current = 0;
                any = true;
                pos++;
                continue;
            }
            if (w == "bilhao" || w == "bilhoes")
            {
                if (!any) current = 1;
                total += current * 1_000_000_000;
                current = 0;
                any = true;
                pos++;
                continue;
            }
            if (w == "mil")
            {
                if (!any) current = 1;
                total += current * 1000;
                current = 0;
                any = true;
                pos++;
                continue;
            }
            if (w == "e")
            {
                pos++;
                continue;
            }

            if (Units.TryGetValue(w, out var u))
            {
                current += u;
                any = true;
                pos++;
                continue;
            }
            if (Tens.TryGetValue(w, out var te))
            {
                current += te;
                any = true;
                pos++;
                continue;
            }
            if (Hundreds.TryGetValue(w, out var h))
            {
                current += h;
                any = true;
                pos++;
                continue;
            }

            break;
        }

        total += current;
        if (!any)
        {
            consumed = start;
            return null;
        }
        consumed = pos;
        return total;
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(ch) || ch == ' ' || ch is ',' or '.' or '(' or ')' or '+' or '-' or '*' or '/' or '^' or '%')
                sb.Append(ch);
        }
        return string.Join(" ", sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool StripStartWord(ref string t, params string[] words)
    {
        foreach (var w in words)
        {
            if (t.StartsWith(w, StringComparison.Ordinal))
            {
                t = t[w.Length..].Trim();
                return true;
            }
        }
        return false;
    }
}
