using IntegrityService.FIM;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using NtKeywords = Microsoft.Diagnostics.Tracing.Parsers.KernelTraceEventParser.Keywords;

namespace IntegrityService.Utils
{
    /// <summary>
    ///     A class capturing Registry events.
    /// </summary>
    /// <see href="https://github.com/lowleveldesign/lowleveldesign-blog-samples/blob/master/monitoring-registry-activity-with-etw/Program.fs"/>
    internal sealed class RegistryMonitor : IMonitor
    {
        private readonly ILogger _logger;
        private readonly Dictionary<ulong, string> _regHandleToKeyName;
        private readonly string _sessionName;
        private readonly int _pid;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private bool disposedValue;

        const NtKeywords traceFlags = NtKeywords.Registry;
        const NtKeywords stackFlags = NtKeywords.None;

        public RegistryMonitor(ILogger logger)
        {
            _logger = logger;
            _regHandleToKeyName = new Dictionary<ulong, string>();
            _sessionName = "RegistryWatcher";
            _pid = Environment.ProcessId;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start() =>
            // No baseline database for registry keys
            StartSession();// TODO: Missing error handling

        public void Stop() => _cancellationTokenSource.Cancel();

        private void StartSession()
        {
            var session = new TraceEventSession(_sessionName);
            _ = session.EnableKernelProvider(traceFlags, stackFlags);
            MakeKernelParserStateless(session.Source);
            RundownSession(_sessionName + "-rundown");

            session.Source.Kernel.RegistryKCBCreate += (sender) => ProcessKCBCreateEvent(sender);
            session.Source.Kernel.RegistryKCBDelete += (sender) => ProcessKCBDeleteEvent(sender);

            // Key events
            session.Source.Kernel.RegistryCreate += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistryOpen += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistryClose += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistryFlush += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistryEnumerateKey += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistryQuery += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistrySetInformation += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistryVirtualize += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistryDelete += (ev) => ProcessEvent(ev);

            // Value events
            session.Source.Kernel.RegistryEnumerateValueKey += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistryQueryValue += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistryQueryMultipleValue += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistrySetValue += (ev) => ProcessEvent(ev);
            session.Source.Kernel.RegistryDeleteValue += (ev) => ProcessEvent(ev);
            var r = _cancellationTokenSource.Token.Register(() => session.Stop());
            _ = session.Source.Process();
        }

        private void RundownSession(string sessionName)
        {
            var session = new TraceEventSession(sessionName);
            session.EnableKernelProvider(traceFlags, stackFlags);
            session.Source.Kernel.RegistryKCBRundownBegin += (sender) => ProcessKCBCreateEvent(ev: sender);
            session.Source.Kernel.RegistryKCBRundownEnd += (sender) => ProcessKCBDeleteEvent(ev: sender);

            using var r = _cancellationTokenSource.Token.Register(() => session.Stop());
            _ = session.Source.Process();
        }

        private void ProcessKCBCreateEvent(RegistryTraceData ev)
        {
            _logger
                .LogInformation("Description: Key Control Block Created.\nTimestamp: {timestamp}\nEvent Name: {event}\nKey Handle: {keyHandle}\nKey Name: {keyName}",
                ev.TimeStampRelativeMSec, ev.EventName, ev.KeyHandle, ev.KeyName);
            _regHandleToKeyName[ev.KeyHandle] = ev.KeyName;
        }

        private void ProcessKCBDeleteEvent(RegistryTraceData ev)
        {
            _logger
                .LogInformation("Description: Key Control Block Deleted.\nTimestamp: {timestamp}\nEvent Name: {event}\nKey Handle: {keyHandle}\nKey Name: {keyName}",
                ev.TimeStampRelativeMSec, ev.EventName, ev.KeyHandle, ev.KeyName);
            _ = _regHandleToKeyName.Remove(ev.KeyHandle);
        }

        private static void MakeKernelParserStateless(TraceEventSource traceSessionSource)
        {
            const KernelTraceEventParser.ParserTrackingOptions options = KernelTraceEventParser.ParserTrackingOptions.None;
            var kernelParser = new KernelTraceEventParser(traceSessionSource, options);

            var t = traceSessionSource.GetType();
            var kernelField = t.GetField("_Kernel", BindingFlags.Instance | BindingFlags.SetField | BindingFlags.NonPublic);
            kernelField?.SetValue(traceSessionSource, kernelParser);
        }

        private bool Filter(RegistryTraceData ev) =>
            ev.ProcessID == _pid
            ||
            ev.ProcessID == -1
            ||
            IsMonitored(ev)
            ||
            !IsExcluded(ev);

        private bool IsExcluded(RegistryTraceData ev)
        {
            var keyName = GetFullKeyName(ev.KeyHandle, ev.KeyName);

            foreach (var key in Settings.Instance.ExcludedKeys)
            {
                if (keyName.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        
        private bool IsMonitored(RegistryTraceData ev)
        {
            var keyName = GetFullKeyName(ev.KeyHandle, ev.KeyName);

            foreach (var key in Settings.Instance.MonitoredKeys)
            {
                if (keyName.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void ProcessEvent(RegistryTraceData ev)
        {
            try
            {
                if (Filter(ev))
                {
                    var keyName = GetFullKeyName(ev.KeyHandle, ev.ValueName);
                    _logger
                   .LogInformation("Description: Key event.\nTimestamp: {timestamp}\nEvent Name: {event}\nKey Handle: {keyHandle}\nKey Name: {keyName}\nProcess Id: {processId}\nThread ID: {threadId}\nIndex: {index}\nStatus:{status}\nElapsed: {elapsed}",
                   ev.TimeStampRelativeMSec, ev.EventName, ev.KeyHandle, keyName, ev.ProcessID, ev.ThreadID, ev.Index, Enum.GetName((RegistryEventCategory)ev.Status), ev.ElapsedTimeMSec);
                    _ = _regHandleToKeyName.Remove(ev.KeyHandle);

                    var key = RegistryKey.FromHandle(new Microsoft.Win32.SafeHandles.SafeRegistryHandle(new IntPtr((long)ev.KeyHandle), true));

                    var change = new RegistryChange
                    {
                        Id = Guid.NewGuid(),
                        ChangeCategory = ChangeCategory.Changed,
                        ConfigChangeType = ConfigChangeType.Registry,
                        Entity = keyName,
                        DateTime = DateTime.Now,
                        Key = keyName,
                        Hive = Enum.GetName(ParseHive(keyName)) ?? string.Empty,
                        SourceComputer = Environment.MachineName,
                        ValueName = ev.ValueName,
                        ValueData = key.GetValue(ev.ValueName)?.ToString() ?? string.Empty,
                        ACLs = key.GetACL()

                    };
                    Database.Context.RegistryChanges.Insert(change);
                }
            }
            catch (Exception ex)
            {
                ex.Log(_logger);
            }
        }

        private string GetFullKeyName(ulong keyHandle, string eventKeyName)
        {
            var baseKeyName = string.Empty;
            if (_regHandleToKeyName.TryGetValue(keyHandle, out var result))
            {
                baseKeyName = result;
            }

            return Path.Combine(baseKeyName, eventKeyName);
        }

        private static RegistryHive ParseHive(string keyName)
        {
            if (keyName.Contains("HKEY_LOCAL_MACHINE"))
            {
                return RegistryHive.LocalMachine;
            }

            if (keyName.Contains("HKEY_CURRENT_USER"))
            {
                return RegistryHive.CurrentUser;
            }

            if (keyName.Contains("HKEY_CURRENT_CONFIG"))
            {
                return RegistryHive.CurrentConfig;
            }

            if (keyName.Contains("HKEY_CLASSES_ROOT"))
            {
                return RegistryHive.ClassesRoot;
            }
            else
            {
                return RegistryHive.Users;
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
