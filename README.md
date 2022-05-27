# IntegrityService

## Overview
File Integrity Monitoring (FIM) is a security related requirement for monitoring critical locations: directories in Linux, directory and Registry keys in Windows.

## Usage
It is designed to be a Windows Service. In first use, it will start a scan based on the settings. Settings are read from Windows Registry. 

For ease of use, an ADMX file is created -it needs cleanup. So, the monitored paths, excluded paths -such as log folders, and excluded file extensions -such as log, evtx, etl- can be set via the group policy.

If there is no path to monitor defined in the Registry, service will not do any action (no default value hard-coded).

In the first use, it will run a full discovery, search for all the files, calculate SHA256 checksum and save it in a local database. This wil be the baseline. Then, it will invoke FileSystemWatcher instances and when any changes occur, it will create an event log and update the database. You can see the SHA256 hashes for the current and previous versions.

Windows has a lot of quirks when it comes to low level callbacks, especially for NTFS. Many of the use cases are handled but it needs to be fine-tuned for edge cases.

### Event Logs
Event ID 7770 - An exception occurred
Event ID 7776 – File / Directory creation
Event ID 7777 – File modification
Event ID 7778 – File / Directory deletion
Event ID 7780 – Other events (heartbeat checks in every 60 seconds, service start and stop, etc.)

## Installation
Use `install.bat` and `uninstall.bat`.

The service will provide an installer in the future.

## Roadmap
- [ ] Monitor Registry
- [ ] Generate installer, preferably in MSI format.