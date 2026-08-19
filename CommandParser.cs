using System.Globalization;
using System.Text;

namespace Vox;

public record VoiceCommand(HotkeyBinding? Binding, string? Query, SystemCommand? System, string? SystemText = null, int SystemNumber = 0)
{
    public static VoiceCommand Open(HotkeyBinding b, string? q) => new(b, q, null);
    public static VoiceCommand SystemAction(SystemCommand cmd, string? text = null, int number = 0) => new(null, null, cmd, text, number);
}

public static class CommandParser
{
    public static VoiceCommand? Parse(string raw, IReadOnlyList<HotkeyBinding> bindings)
    {
        var t = Normalize(raw);
        if (string.IsNullOrWhiteSpace(t)) return null;

        StripStart(ref t, "por favor ");
        StripWakeWord(ref t);

        var sys = TrySystemCommand(t);
        if (sys != null)
            return VoiceCommand.SystemAction(sys.Value.Command, sys.Value.Text, sys.Value.Number);

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

    private static (SystemCommand Command, string? Text, int Number)? TrySystemCommand(string t)
    {
        if (HasAny(t, "lembre em", "lembrar em", "me lembre em", "me lembra em", "me avise em", "me avisa em",
                      "lembrete em", "alarme em", "alarme para", "timer de", "cronometro de", "desperte em",
                      "me desperte em", "avise em") && TryParseDuration(t, out var seconds))
            return (SystemCommand.Timer, seconds.ToString(), seconds);

        if (HasAny(t, "que horas sao", "que horas e", "que horas", "me diga as horas", "me diz as horas",
                      "fala a hora", "diga a hora", "que hora sao", "hora atual"))
            return (SystemCommand.Time, null, 0);

        if (HasAny(t, "que dia e hoje", "que dia e", "qual a data", "qual data", "data de hoje",
                      "que dia sao hoje", "que data e hoje"))
            return (SystemCommand.Date, null, 0);

        if (HasWord(t, "cancelar", "cancela", "cancele"))
            return (SystemCommand.Cancel, null, 0);

        if (HasAny(t, "leia a area de transferencia", "leia o que esta na area", "ler a area de transferencia")
            || HasWord(t, "clipboard", "area de transferencia"))
            return (SystemCommand.Clipboard, null, 0);

        var theme = TryTheme(t);
        if (theme != null)
            return (SystemCommand.Theme, theme, 0);

        if (StripStartWord(ref t, "quanto e ", "quanto da ", "quanto ficou ", "calcule ", "calcula ", "me calcula ", "calcular ")
            && t.Length > 0 && Calculator.Evaluate(t) != null)
            return (SystemCommand.Calc, t, 0);

        var vol = TryVolumeSet(t);
        if (vol != null)
            return (SystemCommand.VolumeSet, null, vol.Value);

        if (HasAny(t, "recarregue o config", "recarregar o config", "recarregar config", "recarregue o vox",
                      "recarregar o vox", "atualize o config", "atualizar o config"))
            return (SystemCommand.ReloadConfig, null, 0);

        if (HasAny(t, "tire um print", "tira um print", "tirar um print", "captura de tela", "capturar a tela",
                      "capturar tela", "print da tela", "foto da tela", "tirar print", "tirar foto da tela")
            || HasWord(t, "screenshot", "print"))
            return (SystemCommand.Screenshot, null, 0);

        if (HasAny(t, "minimize tudo", "minimizar tudo", "minimize as janelas", "mostre a area de trabalho",
                      "mostrar a area de trabalho", "mostra a area de trabalho", "mostrar o desktop",
                      "mostre o desktop", "mostra o desktop", "voltar para a area de trabalho"))
            return (SystemCommand.ShowDesktop, null, 0);

        if (HasAny(t, "que clima faz", "previsao do tempo", "como esta o tempo", "que tempo faz", "tempo hoje",
                      "previsao de hoje", "previsao do dia", "como esta o clima", "clima de hoje", "esta calor",
                      "esta frio", "esta chovendo", "esta ensolarado"))
            return (SystemCommand.Weather, null, 0);

        var appName = TryCloseApp(t);
        if (appName != null)
            return (SystemCommand.CloseApp, appName, 0);

        if (HasAny(t, "mostre o vox", "mostrar o vox", "mostra o vox", "abra o vox", "abre o vox", "abrir o vox",
                      "abra o assistente", "mostre o assistente", "mostrar o assistente"))
            return (SystemCommand.ShowWindow, null, 0);

        if (HasAny(t, "aumenta o volume", "aumentar o volume", "aumentar volume", "volume mais alto", "mais alto",
                      "aumenta o som", "aumentar o som", "sobe o volume", "aumenta volume", "subir o volume"))
            return (SystemCommand.VolumeUp, null, 0);
        if (HasAny(t, "diminui o volume", "diminuir o volume", "diminuir volume", "volume mais baixo", "mais baixo",
                      "abaixa o volume", "abaixar o volume", "diminui o som", "desce o volume", "diminui volume", "baixar o volume"))
            return (SystemCommand.VolumeDown, null, 0);
        if (HasAny(t, "sem som", "sem volume", "mutar", "silenciar", "tirar o som", "desmutar", "ativar o som")
            || HasWord(t, "mudo", "mutado", "silencioso"))
            return (SystemCommand.Mute, null, 0);
        if (HasAny(t, "proxima faixa", "proxima musica", "passar a faixa", "pular faixa")
            || HasWord(t, "proxima", "pular", "passa"))
            return (SystemCommand.Next, null, 0);
        if (HasAny(t, "faixa anterior", "musica anterior", "volta a musica", "voltar a musica")
            || HasWord(t, "anterior"))
            return (SystemCommand.Previous, null, 0);
        if (HasAny(t, "pausar", "parar a musica", "para a musica", "continuar", "retomar", "parar musica", "pausar a musica")
            || HasWord(t, "pausa", "play", "continua", "retoma"))
            return (SystemCommand.PlayPause, null, 0);
        if (HasAny(t, "bloqueia a tela", "bloquear a tela", "trancar a tela", "travar a tela", "bloquear tela", "travar tela")
            || HasWord(t, "bloqueia", "bloquear", "tranca", "trava"))
            return (SystemCommand.Lock, null, 0);
        if (HasWord(t, "hiberna", "hibernar"))
            return (SystemCommand.Hibernate, null, 0);
        if (HasAny(t, "vai dormir", "colocar para dormir")
            || HasWord(t, "dormir", "suspender", "dorme"))
            return (SystemCommand.Sleep, null, 0);

        return null;
    }

    private static string? TryTheme(string t)
    {
        if (HasAny(t, "tema escuro", "modo escuro", "tema preto", "tema dark"))
            return "dark";
        if (HasAny(t, "tema claro", "modo claro", "tema branco", "tema light"))
            return "light";
        if (HasAny(t, "tema do sistema", "modo automatico", "tema sistema", "modo sistema"))
            return "system";
        return null;
    }

    private static string? TryCloseApp(string t)
    {
        foreach (var w in new[] { "fecha o ", "feche o ", "fechar o ", "fechar a ", "fecha a ", "feche a ",
                                  "fecha ", "feche ", "fechar ", "encerre o ", "encerra o " })
        {
            if (t.StartsWith(w, StringComparison.Ordinal) && t.Length > w.Length)
            {
                var rest = t[w.Length..].Trim();
                if (rest.Length > 0 && !IsStopWord(rest.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]))
                    return rest;
            }
        }
        return null;
    }

    private static int? TryVolumeSet(string t)
    {
        var idx = FindWordIndex(t, "volume", "som");
        if (idx < 0) return null;

        var rest = t[idx..];
        StripStartWord(ref rest, "volume ", "som ");

        rest = StripLeadingStopwords(rest);
        rest = CleanNumberPhrase(rest);
        if (rest.Length == 0) return null;

        if (HasWord(rest, "maximo", "total", "cheio", "todo", "cem")) return 100;
        if (HasWord(rest, "minimo", "zero", "desligado")) return 0;
        if (HasWord(rest, "metade", "meio")) return 50;

        if (Calculator.TryParseNumber(rest, out var v))
            return (int)Math.Round(Math.Clamp(v, 0, 100));

        return null;
    }

    private static bool TryParseDuration(string t, out int seconds)
    {
        seconds = 0;
        var tokens = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            long mult = tokens[i] switch
            {
                "segundo" or "segundos" => 1,
                "minuto" or "minutos" => 60,
                "hora" or "horas" => 3600,
                _ => 0
            };
            if (mult == 0) continue;
            if (i >= 1 && Calculator.TryParseNumber(tokens[i - 1], out var val))
            {
                seconds = (int)(val * mult);
                return seconds > 0;
            }
        }
        if (HasAny(t, "meia hora", "meia-hora"))
        {
            seconds = 1800;
            return true;
        }
        return false;
    }

    private static string CleanNumberPhrase(string s)
    {
        foreach (var w in new[] { "por cento", "porcento" })
            if (s.EndsWith(w, StringComparison.Ordinal) && s.Length > w.Length)
            {
                s = s[..^w.Length].Trim();
                break;
            }
        return s;
    }

    private static int FindWordIndex(string t, params string[] words)
    {
        int pos = 0;
        foreach (var tok in t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (words.Contains(tok))
                return pos;
            pos += tok.Length + 1;
        }
        return -1;
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
        var vsCode = bindings.FirstOrDefault(b =>
            b.Description != null && b.Description.Contains("visual studio code", StringComparison.OrdinalIgnoreCase));
        if (vsCode != null)
            names.Add((vsCode, "vs code"));
        return names;
    }

    private static (HotkeyBinding Binding, string Name)? FuzzyMatch(string t, List<(HotkeyBinding Binding, string Name)> names)
    {
        var phraseWords = t.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(IsSignificantWord).ToList();
        if (phraseWords.Count == 0) return null;
        if (phraseWords.Count == 1 && phraseWords[0].Length < 3) return null;

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
            if (matched >= 1 && score >= 0.5 && score > bestScore)
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
        foreach (var w in new[] { "ei vox ", "hey vox ", "oi vox ", "ola vox ", "e ai vox ", "vox " })
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

    private static bool HasWord(string t, params string[] words)
    {
        var tokens = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(tok => words.Any(w => tok == w));
    }

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