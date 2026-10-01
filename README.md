# NicConfig

A small Windows Forms tool for viewing and changing the IPv4 settings of physical Ethernet adapters. It does the same job as the adapter's **Internet Protocol Version 4 (TCP/IPv4) Properties** dialog, but in one window.

## Download

<!-- latest-exe -->[Download the latest NicConfig.exe](https://github.com/arelpha24/NicConfig/raw/main/releases/5e8bf05/NicConfig.exe) (version `5e8bf05`)<!-- /latest-exe -->

Every Release build is archived in [releases/](releases), in a folder named after its version.

## Features

- Lists physical, IP-enabled Ethernet adapters. Wi-Fi, virtual, and loopback adapters are excluded.
- Shows the adapter's current IP address, subnet mask, default gateway, and DNS servers.
- Switches the IP address between DHCP and a static address.
- Switches DNS between automatic (DHCP-assigned) and up to two static servers.
- Checks that IPv4 input is valid before applying changes.
- **Refresh** reloads the adapter list and keeps your current selection.

## Requirements

- Windows 10 or 11
- .NET Framework 4.8
- Administrator rights. The app manifest requests elevation, so a UAC prompt appears at launch.

To build from source, you also need the [.NET SDK](https://dotnet.microsoft.com/download) (SDK-style project targeting `net48`).

## Build

```powershell
dotnet build -c Release
```

The executable is written to `bin\Release\net48\NicConfig.exe`. Each Release build also does the following:

- Copies the executable to `releases\<version>\NicConfig.exe`.
- Updates the download link at the top of this README to point to that copy.

Commit the new `releases` folder and the README to publish them.

### Versioning

Each build stamps the short git commit ID into the executable's product version, which also appears in the window title. If git isn't available, the version is `nogit`.

The numeric file version still comes from `<Version>` in `NicConfig.csproj`, because Windows requires that value to be numeric.

## Usage

1. Run `NicConfig.exe` and accept the UAC prompt.
2. Select an adapter from the **Adapter** drop-down.
3. Under **IP settings**, choose one of these:
   - **Obtain an IP address automatically** (DHCP)
   - **Use the following IP address**, then enter the IP address, subnet mask, and an optional default gateway
4. Under **DNS settings**, choose one of these:
   - **Obtain DNS server address automatically**
   - **Use the following DNS server addresses**, then enter a preferred and an optional alternate DNS server
5. Click **Apply**. The status line at the bottom shows whether the change succeeded.

## How it works

The adapter list comes from WMI:

- `Win32_NetworkAdapter`, filtered with `AdapterTypeId = 0` (Ethernet) and `PhysicalAdapter = TRUE`
- `Win32_NetworkAdapterConfiguration`, filtered with `IPEnabled = TRUE`

Changes are applied through these `Win32_NetworkAdapterConfiguration` methods:

| Action | WMI method |
| --- | --- |
| Set a static IP address and subnet mask | `EnableStatic` |
| Set the default gateway | `SetGateways` |
| Turn on DHCP | `EnableDHCP` |
| Set DNS servers or reset them to automatic | `SetDNSServerSearchOrder` |

Return code `0` means success, and `1` means success but a reboot is required. Any other return code is shown as an error.

## Limitations

- IPv4 only.
- Only one IP address and one gateway per adapter. If an adapter has extra addresses, only the first IPv4 address is shown. Applying a static configuration replaces all existing addresses.
- Adapter metrics, WINS, and other advanced TCP/IP settings are not supported.

## License

[MIT](LICENSE)
