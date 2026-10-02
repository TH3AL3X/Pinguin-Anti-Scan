using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace ScannerDisabler;

internal sealed class WirelessInterface
{
    public WirelessInterface(Guid id, string name, string description, bool? backgroundScanEnabled, bool connected)
    {
        Id = id;
        Name = name;
        Description = description;
        BackgroundScanEnabled = backgroundScanEnabled;
        Connected = connected;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string Description { get; }
    public bool? BackgroundScanEnabled { get; }
    public bool Connected { get; }
}

internal sealed class CommandResult
{
    public CommandResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; }
    public string Message { get; }
}

internal static class WirelessService
{
    private static readonly object Sync = new();
    private static IntPtr _clientHandle;

    public static IReadOnlyList<WirelessInterface> GetInterfaces()
    {
        var nativeStates = GetNativeStates();
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .OrderBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(adapter =>
            {
                Guid.TryParse(adapter.Id, out var id);
                nativeStates.TryGetValue(id, out var state);
                return new WirelessInterface(
                    id,
                    adapter.Name,
                    adapter.Description,
                    state?.BackgroundScanEnabled,
                    state?.Connected == true);
            })
            .ToList();
    }

    public static CommandResult EnableAutoConfig(Guid interfaceId) =>
        SetBoolean(interfaceId, WlanIntfOpcode.AutoconfEnabled, true);

    public static CommandResult SetBackgroundScan(Guid interfaceId, bool enabled) =>
        SetBoolean(interfaceId, WlanIntfOpcode.BackgroundScanEnabled, enabled);

    public static void Shutdown()
    {
        lock (Sync)
        {
            if (_clientHandle == IntPtr.Zero) return;
            WlanCloseHandle(_clientHandle, IntPtr.Zero);
            _clientHandle = IntPtr.Zero;
        }
    }

    private static CommandResult SetBoolean(Guid interfaceId, WlanIntfOpcode opcode, bool enabled)
    {
        lock (Sync)
        {
            try
            {
                var handleResult = EnsureClientHandle();
                if (!handleResult.Success) return handleResult;
                var value = enabled ? 1 : 0;
                var code = WlanSetInterface(_clientHandle, ref interfaceId, opcode, sizeof(int), ref value, IntPtr.Zero);
                return code == 0 ? new CommandResult(true, string.Empty) : Error(code);
            }
            catch (Exception ex)
            {
                return new CommandResult(false, ex.Message);
            }
        }
    }

    private static Dictionary<Guid, NativeState> GetNativeStates()
    {
        var result = new Dictionary<Guid, NativeState>();
        IntPtr interfaceList = IntPtr.Zero;
        lock (Sync)
        {
            try
            {
                if (!EnsureClientHandle().Success)
                    return result;
                if (WlanEnumInterfaces(_clientHandle, IntPtr.Zero, out interfaceList) != 0)
                    return result;

                var count = Marshal.ReadInt32(interfaceList);
                var current = IntPtr.Add(interfaceList, 8);
                var itemSize = Marshal.SizeOf<WlanInterfaceInfo>();
                for (var index = 0; index < count; index++)
                {
                    var info = Marshal.PtrToStructure<WlanInterfaceInfo>(current);
                    current = IntPtr.Add(current, itemSize);
                    result[info.InterfaceGuid] = new NativeState(
                        QueryBoolean(_clientHandle, info.InterfaceGuid, WlanIntfOpcode.BackgroundScanEnabled),
                        info.State == WlanInterfaceState.Connected);
                }
            }
            finally
            {
                if (interfaceList != IntPtr.Zero) WlanFreeMemory(interfaceList);
            }
        }
        return result;
    }

    private static CommandResult EnsureClientHandle()
    {
        if (_clientHandle != IntPtr.Zero)
            return new CommandResult(true, string.Empty);
        var code = WlanOpenHandle(2, IntPtr.Zero, out _, out _clientHandle);
        return code == 0 ? new CommandResult(true, string.Empty) : Error(code);
    }

    private static bool? QueryBoolean(IntPtr clientHandle, Guid interfaceId, WlanIntfOpcode opcode)
    {
        IntPtr data = IntPtr.Zero;
        try
        {
            var code = WlanQueryInterface(clientHandle, ref interfaceId, opcode, IntPtr.Zero,
                out var dataSize, out data, out _);
            return code == 0 && data != IntPtr.Zero && dataSize >= sizeof(int)
                ? Marshal.ReadInt32(data) != 0
                : null;
        }
        finally
        {
            if (data != IntPtr.Zero) WlanFreeMemory(data);
        }
    }

    private static CommandResult Error(uint code) =>
        new CommandResult(false, $"{new Win32Exception((int)code).Message} (error code {code})");

    private sealed class NativeState
    {
        public NativeState(bool? backgroundScanEnabled, bool connected)
        {
            BackgroundScanEnabled = backgroundScanEnabled;
            Connected = connected;
        }

        public bool? BackgroundScanEnabled { get; }
        public bool Connected { get; }
    }

    private enum WlanIntfOpcode
    {
        AutoconfEnabled = 1,
        BackgroundScanEnabled = 2
    }

    private enum WlanInterfaceState
    {
        NotReady, Connected, AdHocNetworkFormed, Disconnecting,
        Disconnected, Associating, Discovering, Authenticating
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanInterfaceInfo
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Description;
        public WlanInterfaceState State;
    }

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiatedVersion, out IntPtr clientHandle);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr clientHandle, IntPtr reserved);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr clientHandle, IntPtr reserved, out IntPtr interfaceList);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(IntPtr clientHandle, ref Guid interfaceGuid, WlanIntfOpcode opcode,
        IntPtr reserved, out uint dataSize, out IntPtr data, out int opcodeValueType);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanSetInterface(IntPtr clientHandle, ref Guid interfaceGuid, WlanIntfOpcode opcode,
        int dataSize, ref int data, IntPtr reserved);
    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr memory);
}
