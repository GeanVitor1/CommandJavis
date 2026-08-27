using System.Diagnostics;

namespace Vox;

public class ShortcutManager : IDisposable
{
    private readonly HotkeyWindow _window = new();
    private List<HotkeyBinding> _bindings = new();

    public event Action? Changed;
    public event Action<string>? Notification;

    public ShortcutManager()
    {
        _window.HotkeyPressed += OnHotkeyPressed;
        ReloadFromDisk();
    }

    public IReadOnlyList<HotkeyBinding> All => _bindings;

    public void ReloadFromDisk()
    {
        foreach (var b in _bindings)
            if (b.Id > 0)
                HotkeyApi.Unregister(_window.Handle, b.Id);
        _bindings = Config.Load();
        RegisterAll();
        Changed?.Invoke();
    }

    private void RegisterAll()
    {
        int id = 1;
        foreach (var b in _bindings)
        {
            b.Id = 0;
            if (string.IsNullOrWhiteSpace(b.Key))
                continue;
            if (HotkeyApi.Register(_window.Handle, id, b.Modifiers, b.Key))
                b.Id = id;
            id++;
        }
    }

    private void ReregisterAll()
    {
        foreach (var b in _bindings)
            if (b.Id > 0)
                HotkeyApi.Unregister(_window.Handle, b.Id);
        RegisterAll();
    }

    private void OnHotkeyPressed(int id)
    {
        var b = _bindings.FirstOrDefault(x => x.Id == id);
        if (b != null)
            Execute(b);
    }

    public void Execute(HotkeyBinding b)
    {
        try
        {
            var expandedTarget = Environment.ExpandEnvironmentVariables(b.Target ?? "");
            var expandedArgs = Environment.ExpandEnvironmentVariables(b.Arguments ?? "");
            ProcessStartInfo psi;
            if (b.Action.Equals("command", StringComparison.OrdinalIgnoreCase))
            {
                psi = new ProcessStartInfo("cmd.exe", $"/c {expandedTarget}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else
            {
                psi = new ProcessStartInfo(expandedTarget)
                {
                    UseShellExecute = true,
                    Arguments = expandedArgs
                };
            }
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Logger.Error($"ShortcutManager.Execute [{b.Description}]", ex);
            Notification?.Invoke($"Erro ao executar \"{b.Description}\": {ex.Message}");
        }
    }

    public string? Rebind(HotkeyBinding b, string modifiers, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            RemoveHotkey(b);
            return null;
        }

        var conflict = _bindings.FirstOrDefault(x => x != b && SameCombo(x, modifiers, key));
        if (conflict != null)
            return $"Este atalho já está em uso por \"{conflict.Description}\".";

        var oldMods = b.Modifiers;
        var oldKey = b.Key;
        b.Modifiers = modifiers;
        b.Key = key;
        ReregisterAll();

        if (b.Id == 0)
        {
            b.Modifiers = oldMods;
            b.Key = oldKey;
            ReregisterAll();
            return "Não foi possível registrar este atalho (pode já estar em uso por outro programa).";
        }

        Save();
        Changed?.Invoke();
        return null;
    }

    public string? Add(HotkeyBinding b)
    {
        if (!string.IsNullOrWhiteSpace(b.Key))
        {
            var conflict = _bindings.FirstOrDefault(x => SameCombo(x, b.Modifiers, b.Key));
            if (conflict != null)
                return $"Este atalho já está em uso por \"{conflict.Description}\".";
        }

        _bindings.Add(b);
        ReregisterAll();

        if (!string.IsNullOrWhiteSpace(b.Key) && b.Id == 0)
        {
            _bindings.Remove(b);
            ReregisterAll();
            return "Não foi possível registrar este atalho (pode já estar em uso por outro programa).";
        }

        Save();
        Changed?.Invoke();
        return null;
    }

    public void Remove(HotkeyBinding b)
    {
        if (b.Id > 0)
            HotkeyApi.Unregister(_window.Handle, b.Id);
        _bindings.Remove(b);
        ReregisterAll();
        Save();
        Changed?.Invoke();
    }

    public void RemoveHotkey(HotkeyBinding b)
    {
        if (b.Id > 0)
            HotkeyApi.Unregister(_window.Handle, b.Id);
        b.Id = 0;
        b.Modifiers = "";
        b.Key = "";
        ReregisterAll();
        Save();
        Changed?.Invoke();
    }

    private static bool SameCombo(HotkeyBinding x, string modifiers, string key)
    {
        return !string.IsNullOrWhiteSpace(x.Key)
            && x.Modifiers.Equals(modifiers, StringComparison.OrdinalIgnoreCase)
            && x.Key.Equals(key, StringComparison.OrdinalIgnoreCase);
    }

    private void Save()
    {
        try
        {
            Config.Save(_bindings);
        }
        catch (Exception ex)
        {
            Notification?.Invoke($"Erro ao salvar config.json: {ex.Message}");
        }
    }

    public void Dispose()
    {
        foreach (var b in _bindings)
            if (b.Id > 0)
                HotkeyApi.Unregister(_window.Handle, b.Id);
    }
}
