using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;

namespace Vox;

public sealed class MicrophoneDevice
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public override string ToString() => Name;
}

public static class MicrophoneSelector
{
    public static async Task<List<MicrophoneDevice>> GetMicrophonesAsync()
    {
        var devices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
        var list = new List<MicrophoneDevice>();
        foreach (var d in devices)
        {
            if (string.IsNullOrWhiteSpace(d.Id) || string.IsNullOrWhiteSpace(d.Name))
                continue;
            list.Add(new MicrophoneDevice { Id = d.Id, Name = d.Name });
            Logger.Info($"Microphone: {d.Name} id={d.Id}");
        }
        if (list.Count == 0)
            Logger.Info($"MicrophoneSelector: FindAllAsync retornou {devices.Count} device(s), nenhum aproveitado");
        return list
            .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static bool SetDefaultCaptureDevice(string deviceId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return false;
            var mmId = ToMmDeviceId(deviceId);
            Logger.Info($"SetDefaultCaptureDevice: input={ShortId(deviceId)} mmId={ShortId(mmId)}");
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            // Verifica se o device existe
            int hrGet = enumerator.GetDevice(mmId, out var device);
            if (hrGet != 0)
            {
                Logger.Info($"SetDefaultCaptureDevice: GetDevice falhou hr=0x{hrGet:X8} id={ShortId(mmId)}");
                return false;
            }
            var policy = (IPolicyConfig)new PolicyConfigClient();
            int hrConsole = policy.SetDefaultEndpoint(mmId, ERole.eConsole);
            int hrComm = policy.SetDefaultEndpoint(mmId, ERole.eCommunications);
            Logger.Info($"SetDefaultCaptureDevice({ShortId(mmId)}) -> hrConsole=0x{hrConsole:X8} hrComm=0x{hrComm:X8}");
            return hrConsole == 0 && hrComm == 0;
        }
        catch (Exception ex)
        {
            Logger.Error("SetDefaultCaptureDevice", ex);
            return false;
        }
    }

    private static string ToMmDeviceId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return id;
        // DeviceInformation id = \\?\SWD#MMDEVAPI#{0.0.1.00000000}.{guid}#{...}
        // MMDevice id = {0.0.1.00000000}.{guid}
        if (id.Contains("SWD#MMDEVAPI", StringComparison.OrdinalIgnoreCase))
        {
            int start = id.IndexOf("#{0.", StringComparison.Ordinal);
            if (start >= 0)
            {
                start += 1; // pula '#', fica em '{'
                int end = id.IndexOf("}#{", start, StringComparison.Ordinal);
                if (end > start)
                    return id.Substring(start, end - start + 1);
            }
            // fallback: tenta extrair entre #{ e }#{ de forma genérica
            int s = id.IndexOf("#{", StringComparison.Ordinal);
            if (s >= 0)
            {
                s += 1;
                int e = id.IndexOf("}#{", s, StringComparison.Ordinal);
                if (e > s) return id.Substring(s, e - s + 1);
            }
        }
        return id;
    }

    private static string ShortId(string? id) => string.IsNullOrEmpty(id) ? "(vazio)" : id.Length > 50 ? id.Substring(id.Length - 50) : id;

    public static string? GetDefaultCaptureId()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eConsole, out var dev);
            return GetDeviceId(dev);
        }
        catch (Exception ex)
        {
            Logger.Error("GetDefaultCaptureId", ex);
            return null;
        }
    }

    private static string? GetDeviceId(IMMDevice device)
    {
        try
        {
            device.GetId(out var id);
            return id;
        }
        catch
        {
            return null;
        }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClient { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint dwClsCtx, IntPtr activationParams, out IntPtr instance);
        [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int @default, IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr format);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int @default, IntPtr value);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr value);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int key, IntPtr value);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int visible);
    }

    private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }
}
