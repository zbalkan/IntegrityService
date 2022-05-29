# IntegrityService

## Overview
File Integrity Monitoring (FIM) is a security related requirement for monitoring critical locations:
* directories in Linux
* directories and Registry keys in Windows.

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

Event ID 7770 - An exception occurred

Event ID 7776 – File / Directory creation

Event ID 7777 – File modification

Event ID 7778 – File / Directory deletion

Event ID 7780 – Other events (heartbeat checks in every 60 seconds, service start and stop, etc.)


## Installation
Use the IntegrityService.Installer.msi

### Details
The second project called `IntegrityService.Installer` is a Wix project to create the uninstaller. Currently, it seeks for a single-file executable in path ".\publish\", which is the Publish path in my profile. You just need to change it to match yours.


## Roadmap
- [ ] Include ACLs
- [ ] Monitor Registry
- [x] Generate installer, preferably in MSI format.