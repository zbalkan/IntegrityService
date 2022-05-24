@echo off
sc create FIM binpath="C:\Users\zafer\source\repos\IntegrityService\IntegrityService\bin\Release\net6.0-windows\publish\win-x64\IntegrityService.exe" start="demand" displayname="File Integrity Monitoring Service" 
sc failure FIM reset=0 actions=restart/60000/restart/60000/run/1000
sc description FIM "File Integrity Monitoring Service"
sc start FIM