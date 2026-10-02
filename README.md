<p align="center">
  <img src="Assets/penguin-anti-scan.png" width="180" alt="Penguin Anti-Scan mascot blocking a Wi-Fi scan">
</p>

<h1 align="center">Penguin Anti-Scan</h1>

<p align="center"><strong>Less scanning. More penguin.</strong></p>

<p align="center">
  A tiny Windows system-tray utility that disables periodic background Wi-Fi scans while you are connected, without disabling Wi-Fi or preventing on-demand network searches.
</p>

## What does it do?

Windows wireless adapters periodically scan for nearby access points. On some adapter and driver combinations, those background scans can coincide with brief latency spikes, ping jitter, or interruptions in latency-sensitive applications.

Penguin Anti-Scan asks the Windows Native Wi-Fi API to disable **background scanning** on the selected wireless adapter. It does not turn off the radio, disconnect the current network, install a driver, or disable WLAN AutoConfig.

When you open the Windows Wi-Fi panel, Windows can still request a network scan on demand. If the adapter disconnects, Windows automatically restores background scanning; Penguin Anti-Scan applies the protection again after you reconnect.

## Features

- Blocks periodic Wi-Fi background scans while connected.
- Keeps manual/on-demand network discovery available.
- Automatically detects installed Wi-Fi adapters.
- Remembers the selected adapter, such as `Wi-Fi 3`.
- Lives quietly in the Windows system tray.
- Restores normal scanning when you choose **Exit and restore Wi-Fi**.
- Prevents multiple copies from running simultaneously.
- Uses the official Windows Native Wi-Fi API—no custom driver or service.
- Distributed as one portable `.exe` with no companion files or installer.

## Download and use

1. Download `PenguinAntiScan.exe` from the latest release.
2. Run it and accept the Windows UAC prompt. Administrator privileges are required to change WLAN interface settings.
3. Select your Wi-Fi adapter.
4. Wait for the green **Penguin on guard** status.
5. Minimize or close the window; the app will continue running in the system tray.
6. Right-click the tray icon and choose **Exit and restore Wi-Fi** to quit safely.

The portable build targets .NET Framework 4.8, a standard Windows component on current Windows 10 and Windows 11 installations. No companion DLLs, configuration files, installer, or project-local runtime are required.

## Status messages

| Status | Meaning |
| --- | --- |
| **Penguin on guard** | Windows confirms that periodic background scanning is disabled. |
| **The penguin is waiting** | The adapter is disconnected. Protection will be applied after it connects. |
| **The penguin tripped** | The adapter or driver rejected the setting. The app will retry automatically. |

## How it works

Penguin Anti-Scan opens a persistent Native Wi-Fi client session and calls `WlanSetInterface` with `wlan_intf_opcode_background_scan_enabled`. The session remains open while the tray application is running because Windows treats this setting as a client request.

The application queries the same WLAN setting every few seconds. The green status is shown only when Windows reports that background scanning is disabled.

## Safety and limitations

- The setting is applied only while the selected adapter is connected.
- Closing the window hides the application; it does not exit.
- Exiting through the tray menu restores background scanning.
- A sudden process termination or system crash may prevent the normal exit cleanup. Windows also restores scanning when the adapter disconnects.
- Results depend on the wireless adapter and its Windows driver.
- This tool does not claim to improve latency on every computer. Measure before and after on your own system.

## Build from source

Requirements: Windows, the .NET SDK, and the .NET Framework 4.8 targeting pack.

```powershell
dotnet build -c Release
```

The self-contained single-file build is written to:

```text
bin\Release\net48\PenguinAntiScan.exe
```

## FAQ

### Can it fix Wi-Fi ping spikes?

It may help when latency spikes are caused by periodic WLAN background scans or roaming behavior. It cannot fix congestion, interference, router problems, weak signal, bufferbloat, or issues unrelated to scanning.

### Does it disable Wi-Fi roaming or disconnect the network?

It disables the Windows background-scan request for the selected connected adapter. It does not turn off the radio or intentionally disconnect the current network.

### Is this a Wi-Fi driver?

No. It is a lightweight Windows tray utility that talks to the existing adapter driver through the official Native Wi-Fi API.

### Why does it need administrator privileges?

Windows restricts changes to WLAN interface settings. The administrator prompt is declared in the embedded application manifest.
