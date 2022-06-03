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

        private bool _disposedValue;

        const NtKeywords TraceFlags = NtKeywords.Registry;
        const NtKeywords StackFlags = NtKeywords.None;

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
            StartSession();

        public void Stop() => _cancellationTokenSource.Cancel();

        private void StartSession()
        {
            var session = new TraceEventSession(_sessionName);
            _logger.LogInformation("Started ETW session {session} for Registry changes.", _sessionName);
            _ = session.EnableKernelProvider(TraceFlags, StackFlags);
            MakeKernelParserStateless(session.Source);
            RundownSession(_sessionName + "-rundown");

            // KCB vents
            session.Source.Kernel.RegistryKCBCreate += (sender) => ProcessKcbCreateEvent(sender);
            session.Source.Kernel.RegistryKCBDelete += (sender) => ProcessKcbDeleteEvent(sender);

            // Key events
            session.Source.Kernel.RegistryCreate += (ev) => ProcessEvent(ev, ChangeCategory.Created);
            session.Source.Kernel.RegistryFlush += (ev) => ProcessEvent(ev, ChangeCategory.Changed);
            session.Source.Kernel.RegistrySetInformation += (ev) => ProcessEvent(ev, ChangeCategory.Changed);
            session.Source.Kernel.RegistryDelete += (ev) => ProcessEvent(ev, ChangeCategory.Deleted);

            // Value events
            session.Source.Kernel.RegistrySetValue += (ev) => ProcessEvent(ev, ChangeCategory.Changed);
            session.Source.Kernel.RegistryDeleteValue += (ev) => ProcessEvent(ev, ChangeCategory.Deleted);

            // Ignored read/numerate/query events
            //session.Source.Kernel.RegistryOpen += (ev) => ProcessEvent(ev);
            //session.Source.Kernel.RegistryClose += (ev) => ProcessEvent(ev);
            //session.Source.Kernel.RegistryEnumerateKey += (ev) => ProcessEvent(ev);
            //session.Source.Kernel.RegistryQuery += (ev) => ProcessEvent(ev);
            //session.Source.Kernel.RegistryVirtualize += (ev) => ProcessEvent(ev);
            //session.Source.Kernel.RegistryEnumerateValueKey += (ev) => ProcessEvent(ev);
            //session.Source.Kernel.RegistryQueryValue += (ev) => ProcessEvent(ev);
            //session.Source.Kernel.RegistryQueryMultipleValue += (ev) => ProcessEvent(ev);

            var r = _cancellationTokenSource.Token.Register(() => session.Stop());
            _ = session.Source.Process();
        }

        private void RundownSession(string sessionName)
        {
            var session = new TraceEventSession(sessionName);
            _logger.LogInformation("Started ETW session {session} for Registry changes.", _sessionName);
            session.EnableKernelProvider(TraceFlags, StackFlags);
            session.Source.Kernel.RegistryKCBRundownBegin += (sender) => ProcessKcbCreateEvent(ev: sender);
            session.Source.Kernel.RegistryKCBRundownEnd += (sender) => ProcessKcbDeleteEvent(ev: sender);

            using var r = _cancellationTokenSource.Token.Register(() => session.Stop());
            _ = session.Source.Process();
        }

        private void ProcessKcbCreateEvent(RegistryTraceData ev)
        {
            _logger
                .LogInformation("Category: {category}\nChange Type: {changeType}\nDescription: Key Control Block Created.\nTimestamp: {timestamp}\nEvent Name: {event}\nKey Handle: {keyHandle}\nKey Name: {keyName}",
                Enum.GetName(ChangeCategory.Created), Enum.GetName(ConfigChangeType.Registry), ev.TimeStampRelativeMSec, ev.EventName, ev.KeyHandle, ev.KeyName);
            _regHandleToKeyName[ev.KeyHandle] = ev.KeyName;
        }

        private void ProcessKcbDeleteEvent(RegistryTraceData ev)
        {
            _logger
                .LogInformation("Category: {category}\nChange Type: {changeType}\nDescription: Key Control Block Deleted.\nTimestamp: {timestamp}\nEvent Name: {event}\nKey Handle: {keyHandle}\nKey Name: {keyName}",
                Enum.GetName(ChangeCategory.Deleted), Enum.GetName(ConfigChangeType.Registry), ev.TimeStampRelativeMSec, ev.EventName, ev.KeyHandle, ev.KeyName);
            _ = _regHandleToKeyName.Remove(ev.KeyHandle);
        }

        private static void MakeKernelParserStateless(ETWTraceEventSource traceSessionSource)
        {
            if (traceSessionSource is null)
            {
                throw new ArgumentNullException(nameof(traceSessionSource));
            }

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

        private void ProcessEvent(RegistryTraceData ev, ChangeCategory changeCategory)
        {
            try
            {
                if (Filter(ev))
                {
                    var keyName = GetFullKeyName(ev.KeyHandle, ev.ValueName);
                    _logger
                   .LogInformation("Category: {category}\nChange Type: {changeType}\nDescription: Key event.\nTimestamp: {timestamp}\nEvent Name: {event}\nKey Handle: {keyHandle}\nKey Name: {keyName}\nProcess Id: {processId}\nThread ID: {threadId}\nIndex: {index}\nStatus:{status}\nElapsed: {elapsed}",
                   Enum.GetName(changeCategory), Enum.GetName(ConfigChangeType.Registry), ev.TimeStampRelativeMSec, ev.EventName, ev.KeyHandle, keyName, ev.ProcessID, ev.ThreadID, ev.Index, Enum.GetName((RegistryEventCategory)ev.Status), ev.ElapsedTimeMSec);
                    _ = _regHandleToKeyName.Remove(ev.KeyHandle);

                    var key = RegistryKey.FromHandle(new Microsoft.Win32.SafeHandles.SafeRegistryHandle(new IntPtr((long)ev.KeyHandle), true));

                    var change = new RegistryChange
                    {
                        Id = Guid.NewGuid(),
                        ChangeCategory = changeCategory,
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
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Dispose();
                }

                _disposedValue = true;
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
