using System.Globalization;
using System.Text;

namespace JarvisComando;

public record VoiceCommand(HotkeyBinding? Binding, string? Query, SystemCommand? System)
{
    public static VoiceCommand Open(HotkeyBinding b, string? q) => new(b, q, null);
    public static VoiceCommand SystemAction(SystemCommand cmd) => new(null, null, cmd);
}

public static class CommandParser
{
    public static VoiceCommand? Parse(string raw, IReadOnlyList<HotkeyBinding> bindings)
    {
        var t = Normalize(raw);
        if (string.IsNullOrWhiteSpace(t)) return null;

        StripStart(ref t, "por favor ");
        StripWakeWord(ref t);

        var system = TrySystemCommand(t);
        if (system != null)
            return VoiceCommand.SystemAction(system.Value);

        var wantPlay = false;
        if (StripStartWord(ref t, "tocar ", "toque ", "toca "))
            wantPlay = true;
        var wantSearch = StripStartWord(ref t, "pesquise ", "pesquisar ", "procure ");

        StripStartWord(ref t, "abra ", "abre ", "abrir ", "abrindo ", "quero abrir o ", "quero abrir ", "eu quero abrir o ", "eu quero abrir ", "me abra ", "me abre ", "pode abrir ");
        StripStartWord(ref t, "site ", "aplicativo ", "app ", "o site ", "a site ");
        StripStartWord(ref t, "o ", "a ", "os ", "as ", "no ", "na ", "em ");
        StripStartWord(ref t, "site ", "aplicativo ", "app ");

        if (StripStartWord(ref t, "fecha ", "feche ", "fechar ", "fecha o ", "feche o "))
            return null;

        t = t.Trim(' ', ',', '.');
        if (t.Length == 0) return null;

        if (wantSearch)
        {
            var yt = FindYoutube(bindings);
            if (yt == null) return null;
            StripSuffix(ref t, "no youtube", "no site youtube", "em youtube", "na youtube");
            return VoiceCommand.Open(yt, t.Length > 0 ? t : null);
        }

        if (wantPlay)
        {
            var yt = FindYoutube(bindings);
            if (yt == null) return null;
            StripSuffix(ref t, "no youtube", "no site youtube", "em youtube", "na youtube");
            return VoiceCommand.Open(yt, t.Length > 0 ? t : null);
        }

        var names = BuildNames(bindings);

        HotkeyBinding? best = null;
        string? bestName = null;
        string? query = null;

        foreach (var (binding, name) in names.OrderByDescending(x => x.Name.Length))
        {
            if (name.Length < 2) continue;
            var idx = t.IndexOf(name, StringComparison.Ordinal);
            if (idx < 0) continue;

            var before = t[..idx].Trim();
            var after = t[(idx + name.Length)..].Trim();
            var q = (before + " " + after).Replace("por favor", " ").Trim();
            q = StripLeadingStopwords(q);

            if (best == null || name.Length > bestName!.Length)
            {
                best = binding;
                bestName = name;
                query = q.Length == 0 ? null : q;
            }
        }

        if (best == null)
        {
            var fuzzy = FuzzyMatch(t, names);
            if (fuzzy != null)
                return VoiceCommand.Open(fuzzy.Value.Binding, null);
            return null;
        }

        if (query != null && best.Category != "site")
            query = null;
        return VoiceCommand.Open(best, query);
    }

    private static List<(HotkeyBinding Binding, string Name)> BuildNames(IReadOnlyList<HotkeyBinding> bindings)
    {
        var names = new List<(HotkeyBinding, string)>();
        foreach (var b in bindings)
        {
            if (!string.IsNullOrWhiteSpace(b.Description))
                names.Add((b, Normalize(b.Description)));
            if (b.Category == "site" && Uri.TryCreate(b.Target, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
                names.Add((b, uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).Split('.')[0]));
        }
        return names;
    }

    private static (HotkeyBinding Binding, string Name)? FuzzyMatch(string t, List<(HotkeyBinding Binding, string Name)> names)
    {
        var phraseWords = t.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (phraseWords.Count == 0) return null;

        HotkeyBinding? best = null;
        string? bestName = null;
        double bestScore = 0;

        foreach (var (binding, name) in names)
        {
            var nameWords = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(IsSignificantWord).ToList();
            if (nameWords.Count == 0) continue;

            var matched = phraseWords.Count(pw => nameWords.Any(nw => FuzzyEquals(pw, nw)));
            var score = (double)matched / phraseWords.Count;
            if (score >= 0.5 && score > bestScore)
            {
                best = binding;
                bestName = name;
                bestScore = score;
            }
        }
        return best == null ? null : (best, bestName!);
    }

    private static bool IsSignificantWord(string w)
        => w.Length >= 2 && !IsStopWord(w);

    private static bool IsStopWord(string w)
        => w is "o" or "a" or "os" or "as" or "do" or "da" or "de" or "e" or "em" or "no" or "na" or "para" or "por";

    private static bool FuzzyEquals(string a, string b)
    {
        if (a == b) return true;
        if (a.Length >= 2 && b.Length >= 2 && (a.StartsWith(b) || b.StartsWith(a)))
            return true;
        var maxLen = Math.Max(a.Length, b.Length);
        return maxLen >= 4 && Levenshtein(a, b) <= Math.Max(1, maxLen / 4);
    }

    private static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    private static SystemCommand? TrySystemCommand(string t)
    {
        if (HasAny(t, "aumenta o volume", "aumentar o volume", "aumentar volume", "volume mais alto", "mais alto", "aumenta o som", "aumentar o som", "sobe o volume"))
            return SystemCommand.VolumeUp;
        if (HasAny(t, "diminui o volume", "diminuir o volume", "diminuir volume", "volume mais baixo", "mais baixo", "abaixa o volume", "abaixar o volume", "diminui o som", "desce o volume"))
            return SystemCommand.VolumeDown;
        if (HasAny(t, "sem som", "mutar", "silenciar") || HasWord(t, "mudo"))
            return SystemCommand.Mute;
        if (HasAny(t, "proxima faixa", "proxima musica", "passar a faixa") || HasWord(t, "proxima", "pular", "passa"))
            return SystemCommand.Next;
        if (HasAny(t, "faixa anterior", "musica anterior", "volta a musica") || HasWord(t, "anterior"))
            return SystemCommand.Previous;
        if (HasAny(t, "pausar", "parar a musica", "para a musica", "continuar", "retomar")
            || HasWord(t, "pausa", "play", "continua", "retoma"))
            return SystemCommand.PlayPause;
        if (HasAny(t, "bloqueia a tela", "bloquear a tela", "trancar a tela", "travar a tela")
            || HasWord(t, "bloqueia", "bloquear", "tranca", "trava"))
            return SystemCommand.Lock;
        if (HasWord(t, "hiberna", "hibernar"))
            return SystemCommand.Hibernate;
        if (HasAny(t, "vai dormir") || HasWord(t, "dormir", "suspender", "dorme"))
            return SystemCommand.Sleep;
        return null;
    }

    private static bool HasWord(string t, params string[] words)
    {
        var tokens = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(tok => words.Any(w => tok == w));
    }

    private static HotkeyBinding? FindYoutube(IReadOnlyList<HotkeyBinding> bindings)
        => bindings.FirstOrDefault(b => b.Category == "site" && b.Target.Contains("youtube", StringComparison.OrdinalIgnoreCase));

    private static string StripLeadingStopwords(string q)
    {
        var stop = new[] { "em ", "no ", "na ", "com ", "de ", "da ", "do ", "e ", "a ", "o ", "para ", "por ", "musica de ", "musica ", "cancao de " };
        for (int i = 0; i < 3 && q.Length > 0; i++)
            foreach (var w in stop)
                if (StripStart(ref q, w))
                    break;
        foreach (var w in new[] { " da", " de", " do", " em", " no", " na", " com", " e", " a", " o", " para", " por" })
            StripSuffix(ref q, w);
        return q.Trim();
    }

    private static void StripWakeWord(ref string t)
    {
        foreach (var w in new[] { "ei jarvis ", "hey jarvis ", "oi jarvis ", "ola jarvis ", "e ai jarvis ", "jarvis " })
            if (StripStart(ref t, w))
                return;
        StripStart(ref t, "ei ");
    }

    private static bool StripStartWord(ref string t, params string[] words)
    {
        foreach (var w in words)
            if (StripStart(ref t, w))
                return true;
        return false;
    }

    private static bool StripStart(ref string t, string prefix)
    {
        if (t.StartsWith(prefix, StringComparison.Ordinal))
        {
            t = t[prefix.Length..].Trim();
            return true;
        }
        return false;
    }

    private static bool StripSuffix(ref string t, params string[] suffixes)
    {
        foreach (var s in suffixes)
        {
            if (t.EndsWith(s, StringComparison.Ordinal) && t.Length > s.Length)
            {
                t = t[..^s.Length].Trim();
                return true;
            }
        }
        return false;
    }

    private static bool HasAny(string t, params string[] candidates)
        => candidates.Any(c => t.Contains(c, StringComparison.Ordinal));

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == ',' || ch == '.')
                sb.Append(ch);
        }
        return string.Join(" ", sb.ToString().Replace(",", " ").Replace(".", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}