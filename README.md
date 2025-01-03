# IntegrityService

[![DevSkim](https://github.com/zbalkan/IntegrityService/actions/workflows/devskim.yml/badge.svg)](https://github.com/zbalkan/IntegrityService/actions/workflows/devskim.yml) [![Release](https://github.com/zbalkan/IntegrityService/actions/workflows/dotnet.yml/badge.svg)](https://github.com/zbalkan/IntegrityService/actions/workflows/dotnet.yml)

## Overview

Integrity Service is a security related requirement for monitoring critical directories and Registry keys in Windows.

Yes, we need a better name.

## Usage

1. Install the service using `IntegrityService.msi`.
2. The default values will be written to Registry.
3. The filesystem monitoring will always be started.
4. If the database is not disabled, and there is not a completed filesystem discovery, a filesystem discovery will be started.
5. Use the ADMX file for domain installations to manage the configuration.
6. The service does not provide enough information about a security incident, but constitutes a supportive information to collaborate. It is advised to use `Sysmon` and collaborate events together. Related Sysmon event IDs are 2, 9, 11, 12, 13, 14, 15, 23 and 26.

## Internals

It is designed to be a Windows Service. In first use, it will start a scan based on the settings from Windows Registry, under `HKLM\SOFTWARE\FIM`.

If there is no path to monitor defined in the Registry, service will not do any action (no default value hard-coded).

In the first use, it will run a full discovery, search for all the files, calculate SHA256 checksum and save it in a local database as the baseline. File search process reads the data from NTFS MFT (Master File Table) so it will take up to 10 seconds. But file search will generally catch at least 500.000 files and folders on a fresh Windows 10 installation and take about 30 to 90 minutes for calculating hashes, obtaining and parsing ACLs and writing to database depending on the number of files and the system specifications. This search can be disabled via Group Policy or Registry. If you disabled the local database, just skip to the next paragraph.

The service will subscribe to file system events and when any changes occur, it will create an event log and update the database. You can see the SHA256 hashes for the current and (if exist) previous versions.

Windows has a lot of quirks when it comes to low level callbacks, especially for NTFS. Many of the use cases are handled but it needs to be fine-tuned for edge cases.

The registry monitoring uses ETW traces. It is detected under heavy loads, there may be misses. Espceially if there are frequent changes on the same keys, the changes may be lost at some point. It happens when the ETW trace production is higher than the trace consumption speed. 

For ease of use, an ADMX file is created. So, the monitored paths, excluded paths (such as log folders), and excluded file extensions (such as log, evtx, etl) can be set via Group Policy. Suggested values for Group Policies can be found below.

## Suggested values

<table style="border-collapse: collapse; width: 100%; height: 144px;" border="1">
    <tbody>
        <tr style="height: 18px;">
            <td style="width: 23.1657%; height: 18px;"><h3>Registry Value</h3></td>
            <td style="width: 76.8343%; height: 18px;"><h3>Registry ValueData</h3></td>
        </tr>
        <tr style="height: 18px;">
            <td style="width: 23.1657%; height: 18px;">Monitored Paths</td>
            <td style="width: 76.8343%; height: 18px;">
                <ul>
                    <li>%SystemRoot%\System32</li>
                    <li>%SystemRoot%\SysWOW64</li>
                    <li>%ProgramFiles%</li>
                    <li>%ProgramFiles(x86)%</li>
                    <li>%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs\Startup</li>
                    <li>%SYSTEMDRIVE%\Users\*\Downloads</li>
                    <li>%SYSTEMDRIVE%\Users\*\Documents\PowerShell</li>
                    <li>%SYSTEMDRIVE%\Users\*\Documents\WindowsPowerShell</li>
                </ul>
            </td>
        </tr>
        <tr style="height: 18px;">
            <td style="width: 23.1657%; height: 18px;">Excluded Paths</td>
            <td style="width: 76.8343%; height: 18px;">
                <ul>
                    <li>%SystemRoot%\System32\winevt</li>
                    <li>%SystemRoot%\System32\sru</li>
                    <li>%SystemRoot%\System32\config</li>
                    <li>%SystemRoot%\System32\catroot2</li>
                    <li>%SystemRoot%\System32\LogFiles</li>
                    <li>%SystemRoot%\System32\wbem</li>
                    <li>%SystemRoot%\System32\WDI\LogFiles</li>
                    <li>%SystemRoot%\System32\Microsoft\Protect\Recovery</li>
                    <li>%SystemRoot%\SysWOW64\winevt</li>
                    <li>%SystemRoot%\SysWOW64\sru</li>
                    <li>%SystemRoot%\SysWOW64\config</li>
                    <li>%SystemRoot%\SysWOW64\catroot2</li>
                    <li>%SystemRoot%\SysWOW64\LogFiles</li>
                    <li>%SystemRoot%\SysWOW64\wbem</li>
                    <li>%SystemRoot%\SysWOW64\WDI\LogFiles</li>
                    <li>%SystemRoot%\SysWOW64\Microsoft\Protect\Recovery</li>
                    <li>%ProgramFiles%\Windows Defender Advanced Threat Protection\Classification\Configuration</li>
                    <li>%ProgramFiles%\Microsoft OneDrive\StandaloneUpdater\logs</li>
                </ul>
            </td>
        </tr>
        <tr style="height: 18px;">
            <td style="width: 23.1657%; height: 18px;">Excluded Extensions</td>
            <td style="width: 76.8343%; height: 18px;">
                <ul>
                    <li>.log</li>
                    <li>.evtx</li>
                    <li>.etl</li>
                    <li>.wal</li>
                    <li>.db-wal</li>
                    <li>.db</li>
                </ul>
            </td>
        </tr>
        <tr style="height: 18px;">
            <td style="width: 23.1657%; height: 18px;">Enable Registry Monitoring</td>
            <td style="width: 76.8343%; height: 18px;">
                <p>1 (true)</p>
            </td>
        </tr>
        <tr style="height: 18px;">
            <td style="width: 23.1657%; height: 18px;">Monitored Keys</td>
            <td style="width: 76.8343%; height: 18px;">
                <ul>
                    <li>HKEY_LOCAL_MACHINE\SOFTWARE\FIM</li>
                    <li>HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Microsoft Defender</li>
                    <li>HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders</li>
                    <li>HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders</li>
                    <li>HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Session Manager</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\RunServicesOnce</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\RunServices</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\RunOnce</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Windows</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\RunServicesOnce</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\RunServices</li>
                    <li>HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL</li>
                    <li>HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Lsa\FipsAlgorithmPolicy</li>
                    <li>HKEY_LOCAL_MACHINE\SHKLM\SOFTWARE\Policies\Microsoft\Cryptography\Configuration\SSL\00010002</li>
                    <li>HKEY_CURRENT_USER\Software\Classes\Mscfile\Shell\Open\Command</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths\Control.exe</li>
                    <li>HKEY_CURRENT_USER\Software\Classes\Exefile\Shell\Runas\Command\IsolatedCommand</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows Nt\CurrentVersion\Imagefileexecutionoptions</li>
                    <li>HKEY_LOCAL_MACHINE\System\CurrentControlSet\Enum\USBTor</li>
                    <li>HKEY_LOCAL_MACHINE\System\CurrentControlSet\Enum\USB</li>
                    <li>HKEY_CURRENT_USER\Environment</li>
                    <li>HKEY_CURRENT_USER\Control Panel\Desktop\Scrnsave.exe</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Command Processor\Autorun</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Desktop\Components</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Explorer Bars</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Extensions</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\UrlSearchHooks\Server\Install\Software\Microsoft\Windows\CurrentVersion\Run</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Windows\Run</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Winlogon</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Run</li>
                    <li>HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Control Panel\Desktop\Scrnsave.exe</li>
                    <li>HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\System\Scripts\Logoff</li>
                    <li>HKEY_CURRENT_USER\Software\Wow6432Node\Microsoft\Internet Explorer\Explorer Bars</li>
                    <li>HKEY_CURRENT_USER\Software\Wow6432Node\Microsoft\Internet Explorer\Extensions</li>
                    <li>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Winlogon</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Notify</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Winlogon</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\System</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\Taskman</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\GroupPolicy\Scripts\Shutdown</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\GroupPolicy\Scripts\Startup</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Policies\System\Shell</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\RunOnce</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\System\Scripts\Logoff</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\System\Scripts\Logon</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\System\Scripts\Shutdown</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\System\Scripts\Startup</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Wow6432Node\Microsoft\Command\Processor\Autorun</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Wow6432Node\Microsoft\Internet Explorer\Explorer Bars</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Wow6432Node\Microsoft\Internet Explorer\Extensions</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Wow6432Node\Microsoft\Internet Explorer\Toolbar</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run</li>
                    <li>HKEY_LOCAL_MACHINE\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\RunOnce</li>
                    <li>HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\LSA</li>
                    <li>HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Keyboard Layout</li>
                    <li>HKEY_CURRENT_USER\Keyboard Layout\Preload</li>
                </ul>
            </td>
        </tr>
        <tr style="height: 18px;">
            <td style="width: 23.1657%; height: 18px;">Excluded keys</td>
            <td style="width: 76.8343%; height: 18px;"></td>
        </tr>
        <tr style="height: 18px;">
            <td style="width: 23.1657%; height: 18px;">Heartbeat interval</td>
            <td style="width: 76.8343%; height: 18px;">60</td>
        </tr>
    </tbody>
</table>

### Event Logs

Event logs IDs are taken from [WINFIM.NET](https://github.com/redblueteam/WinFIM.NET). Thanks [redblueteam](https://github.com/redblueteam) for inspiration.

| Event ID | Description |
|----------|-------------|
| 7770 | An exception occurred |
| 7776 | File / Directory creation |
| 7777 | File modification |
| 7778 | File / Directory deletion |
| 7786 | Registry key creation |
| 7787 | Registry key/value modification |
| 7788 | Registry key deletion |
| 7780 | Other events (heartbeat checks in every 60 seconds, service start and stop, etc.) |

## Installation

### MSI package installation

Use the `IntegrityService.Installer.msi` file to install. This is specifically used for ease of deployment. It will install the service with an automatic start setting. It does not start the service immediately. It is up to the administrators to let it start on next boot or an immediate start.

### Details

The second project called `IntegrityService.Installer` is a Wix project to create the uninstaller. Currently, it seeks for a single-file executable in path ".\publish\", which is the Publish path in my profile. You just need to change it to match yours.

### Suggested `PublishProfile.pubxml` setup

```xml
<?xml version="1.0" encoding="utf-8"?>
<!--
https://go.microsoft.com/fwlink/?LinkID=208121.
-->
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <Platform>Any CPU</Platform>
    <PublishDir>..\publish\</PublishDir>
    <PublishProtocol>FileSystem</PublishProtocol>
    <TargetFramework>net8.0-windows</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishReadyToRun>true</PublishReadyToRun>
  </PropertyGroup>
</Project>
```

## Development

You need to have .NET 8 for the service. The installer project requires Wix Toolset, and that requires enabling .NET 3.5 on development machine.

## Special thanks to:

### Icons8

[Film Noir](https://icons8.com/icon/6883/film-noir) icon by [Icons8](https://icons8.com) is used for the executable.

### Mariano S. Cosentino

Thanks to Mariano S. Cosentino's [REG_2_ADMX script](https://mscosentino-en.blogspot.com/2010/02/convert-registry-file-to-admx-policy.html), the initial draft of the ADMX files are created.
