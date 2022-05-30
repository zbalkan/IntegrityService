# IntegrityService

## Overview
File Integrity Monitoring (FIM) is a security related requirement for monitoring critical locations:
* directories in Linux
* directories and Registry keys in Windows.

This application is a FIM servce for Windows.

## Usage
It is designed to be a Windows Service. In first use, it will start a scan based on the settings. Settings are read from Windows Registry.

For ease of use, an ADMX file is created -it needs cleanup. So, the monitored paths, excluded paths (such as log folders), and excluded file extensions (such as log, evtx, etl) can be set via Group Policy. Default paths in the ADMX file can be found below.

If there is no path to monitor defined in the Registry, service will not do any action (no default value hard-coded).

In the first use, it will run a full discovery, search for all the files, calculate SHA256 checksum and save it in a local database. This wil be the baseline. Then, it will invoke FileSystemWatcher instances and when any changes occur, it will create an event log and update the database. You can see the SHA256 hashes for the current and previous versions.

Windows has a lot of quirks when it comes to low level callbacks, especially for NTFS. Many of the use cases are handled but it needs to be fine-tuned for edge cases.

## Default Paths
### Monitored Paths
* C:\Windows\System32
* C:\Windows\SysWOW64
* C:\Program Files
* C:\Program Files (x86)
### Excluded Paths
* C:\Windows\System32\winevt
* C:\Windows\System32\sru
* C:\Windows\System32\config
* C:\Windows\System32\catroot2
* C:\Windows\System32\LogFiles
* C:\Windows\System32\wbem
* C:\Windows\System32\WDI\LogFiles
* C:\Windows\System32\Microsoft\Protect\Recovery
* C:\Windows\SysWOW64\winevt
* C:\Windows\SysWOW64\sru
* C:\Windows\SysWOW64\config
* C:\Windows\SysWOW64\catroot2
* C:\Windows\SysWOW64\LogFiles
* C:\Windows\SysWOW64\wbem
* C:\Windows\SysWOW64\WDI\LogFiles
* C:\Windows\SysWOW64\Microsoft\Protect\Recovery
* C:\Program Files\Windows Defender Advanced Threat Protection\Classification\Configuration
* C:\Program Files\Microsoft OneDrive\StandaloneUpdater\logs
### Excluded Extensions
* .log
* .evtx
* .etl

### Event Logs
Event logs IDs are taken from [WINFIM.NET](https://github.com/redblueteam/WinFIM.NET). Thanks [redblueteam](https://github.com/redblueteam) for inspiration.

Event ID 7770 - An exception occurred

Event ID 7776 – File / Directory creation

Event ID 7777 – File modification

Event ID 7778 – File / Directory deletion

Event ID 7786 – Registry key creation

Event ID 7787 – Registry key/value modification

Event ID 7788 – Registry key deletion

Event ID 7780 – Other events (heartbeat checks in every 60 seconds, service start and stop, etc.)


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


## Roadmap
- [x] Include ACLs
- [x] Monitor Registry (Partial)
- [x] Generate installer, preferably in MSI format.
- [ ] Keep a log of exceptions