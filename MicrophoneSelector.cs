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
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eConsole, out var current);
            var currentId = GetDeviceId(current);
            if (string.Equals(currentId, deviceId, StringComparison.OrdinalIgnoreCase))
                return true;

            enumerator.GetDevice(deviceId, out var device);
            var policy = (IPolicyConfig)device;
            policy.SetDefaultEndpoint(deviceId, ERole.eConsole);
            policy.SetDefaultEndpoint(deviceId, ERole.eCommunications);
            return true;
        }
        catch
        {
            return false;
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

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
        int GetDevice(string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, uint dwClsCtx, IntPtr activationParams, out IntPtr instance);
        int OpenPropertyStore(uint stgmAccess, out IntPtr properties);
        int GetId(out string id);
        int GetState(out uint state);
    }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        int GetMixFormat(string deviceId, IntPtr format);
        int GetDeviceFormat(string deviceId, int @default, IntPtr format);
        int ResetDeviceFormat(string deviceId);
        int SetDeviceFormat(string deviceId, IntPtr format);
        int GetProcessingPeriod(string deviceId, int @default, IntPtr value);
        int SetProcessingPeriod(string deviceId, IntPtr value);
        int GetShareMode(string deviceId, IntPtr mode);
        int SetShareMode(string deviceId, IntPtr mode);
        int GetPropertyValue(string deviceId, int key, IntPtr value);
        int SetPropertyValue(string deviceId, int key, IntPtr value);
        int SetDefaultEndpoint(string deviceId, ERole role);
        int SetEndpointVisibility(string deviceId, int visible);
    }

    private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }
}
