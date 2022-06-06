# IntegrityService

## Overview
File Integrity Monitoring (FIM) is a security related requirement for monitoring critical locations:
* directories in Linux
* directories and Registry keys in Windows.

This application provides a FIM service for Windows.

## Usage
1. Install the service via `sc.exe` manually, or using `install.bat` or `IntegrityService.msi`.
2. If local file search started, the file system monitoring will require a restart when search is completed.
3. Use the `default.reg` file for local and ADMX file for domain installations to manage the configuration.
4. The service does not provide enough information about a security incident, but constitutes a supportive information to collaborate. It is advised to use `Sysmon` and collaborate events together. Related Sysmon event IDs are 2, 9, 11, 12, 13, 14, 15, 23 and 26.

## Internals
It is designed to be a Windows Service. In first use, it will start a scan based on the settings from Windows Registry, under `HKLM\SOFTWARE\FIM`.

If there is no path to monitor defined in the Registry, service will not do any action (no default value hard-coded).

In the first use, it will run a full discovery, search for all the files, calculate SHA256 checksum and save it in a local database as the baseline. File search process reads the data from NTFS MFT (Master File Table) so it will take up to 10 seconds. But file search will generally catch at least 100.000 files and folders on a fresh Windows 10 installation and take about 30 to 90 minutes for calculating hashes, obtaining and parsing ACLs and writing to database depending on the number of files and the system specifications. This search can be disabled via Group Policy or registry. If you will use a central logging solution, you can disable it.

If the discovery is completed, restart the service. If you disabled the local database, just skip to the next paragraph.

The service  will subscribe to file system events and when any changes occur, it will create an event log and update the database. You can see the SHA256 hashes for the current and (if exist) previous versions.

Windows has a lot of quirks when it comes to low level callbacks, especially for NTFS. Many of the use cases are handled but it needs to be fine-tuned for edge cases.

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
                    <li>C:\Windows\System32</li>
                    <li>C:\Windows\SysWOW64</li>
                    <li>C:\Program Files</li>
                    <li>C:\Program Files (x86)</li>
                </ul>
            </td>
        </tr>
        <tr style="height: 18px;">
            <td style="width: 23.1657%; height: 18px;">Excluded Paths</td>
            <td style="width: 76.8343%; height: 18px;">
                <ul>
                    <li>C:\Windows\System32\winevt</li>
                    <li>C:\Windows\System32\sru</li>
                    <li>C:\Windows\System32\config</li>
                    <li>C:\Windows\System32\catroot2</li>
                    <li>C:\Windows\System32\LogFiles</li>
                    <li>C:\Windows\System32\wbem</li>
                    <li>C:\Windows\System32\WDI\LogFiles</li>
                    <li>C:\Windows\System32\Microsoft\Protect\Recovery</li>
                    <li>C:\Windows\SysWOW64\winevt</li>
                    <li>C:\Windows\SysWOW64\sru</li>
                    <li>C:\Windows\SysWOW64\config</li>
                    <li>C:\Windows\SysWOW64\catroot2</li>
                    <li>C:\Windows\SysWOW64\LogFiles</li>
                    <li>C:\Windows\SysWOW64\wbem</li>
                    <li>C:\Windows\SysWOW64\WDI\LogFiles</li>
                    <li>C:\Windows\SysWOW64\Microsoft\Protect\Recovery</li>
                    <li>C:\Program Files\Windows Defender Advanced Threat Protection\Classification\Configuration</li>
                    <li>C:\Program Files\Microsoft OneDrive\StandaloneUpdater\logs</li>
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
                </ul>
            </td>
        </tr>
        <tr style="height: 18px;">
            <td style="width: 23.1657%; height: 18px;">Enable Registry Monitoring</td>
            <td style="width: 76.8343%; height: 18px;">
                <p>0 (false)</p>
            </td>
        </tr>
        <tr style="height: 18px;">
            <td style="width: 23.1657%; height: 18px;">Monitored Keys</td>
            <td style="width: 76.8343%; height: 18px;">
                <ul>
                    <li>Computer\HKEY_LOCAL_MACHINE\SOFTWARE\FIM</li>
                    <li>Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run</li>
                    <li>Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce</li>
                    <li>Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon</li>
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
### Plain installation
1. Download the executable.
2. Use the `install.bat` and `uninstall.bat` for your purposes.
### MSI package installation
Use the `IntegrityService.Installer.msi` file to install. This is specifically used for ease of deployment. It will install the service with an automatic start setting. It does not start the service immediately. It is up to the administrators to let it start on next boot or an immediate start.

### Details
The second project called `IntegrityService.Installer` is a Wix project to create the uninstaller. Currently, it seeks for a single-file executable in path ".\publish\", which is the Publish path in my profile. You just need to change it to match yours.

### Suggested PublishProfile.pubxml setup
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
    <TargetFramework>net6.0-windows</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishReadyToRun>true</PublishReadyToRun>
  </PropertyGroup>
</Project>
```

## Development
You need to have .NET 6 for the service. The installer project requires Wix Toolset, and that requires enabling .NET 3.5 on development machine.

## Roadmap
- [x] Include ACLs
- [x] Monitor Registry
- [x] Generate installer, preferably in MSI format.
- [ ] Translate AD addresses (resource consuming task)
- [ ] Use Observable pattern for registry instead of looping
- [ ] Fine tune MSI

## Special thanks to:
### Icons8
[Film Noir](https://icons8.com/icon/6883/film-noir) icon by [Icons8](https://icons8.com) is used for the executable.
### Mariano S. Cosentino
Thanks to Mariano S. Cosentino's [REG_2_ADMX script](https://mscosentino-en.blogspot.com/2010/02/convert-registry-file-to-admx-policy.html), the initial draft of the ADMX files are created.
