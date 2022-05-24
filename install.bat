@echo off
sc create FIM binpath="C:\Users\zafer\source\repos\IntegrityService\IntegrityService\bin\Release\net6.0\IntegrityService.exe" start="demand" displayname="File Integrity Monitoring Service" 
sc start FIM