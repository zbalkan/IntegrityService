using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
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
    internal sealed class RegistryMonitor : IMonitor, IDisposable
    {
        private readonly ILogger _logger;
        private bool disposedValue;
        private readonly Dictionary<ulong, string> regHandleToKeyName;
        private readonly string sessionName;
        private readonly int pid;
        private readonly CancellationTokenSource cancellationTokenSource;

        const NtKeywords traceFlags = NtKeywords.Registry;
        const NtKeywords stackFlags = NtKeywords.None;

        public RegistryMonitor(ILogger logger)
        {
            _logger = logger;
            regHandleToKeyName = new Dictionary<ulong, string>();
            sessionName = "RegistryWatcher";
            pid = Environment.ProcessId;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            var session = new TraceEventSession(sessionName);
            _ = session.EnableKernelProvider(traceFlags, stackFlags);
            MakeKernelParserStateless(session.Source);
            RundownSession(sessionName + "-rundown");

            session.Source.Kernel.RegistryKCBCreate += (sender) => ProcessKCBCreateEvent(sender);
            session.Source.Kernel.RegistryKCBDelete += (sender) => ProcessKCBDeleteEvent(sender);

            // Key events
            session.Source.Kernel.RegistryCreate += (ev) => ProcessKeyEvent(ev);
            session.Source.Kernel.RegistryOpen += (ev) => ProcessKeyEvent(ev);
            session.Source.Kernel.RegistryClose += (ev) => ProcessKeyEvent(ev);
            session.Source.Kernel.RegistryFlush += (ev) => ProcessKeyEvent(ev);
            session.Source.Kernel.RegistryEnumerateKey += (ev) => ProcessKeyEvent(ev);
            session.Source.Kernel.RegistryQuery += (ev) => ProcessKeyEvent(ev);
            session.Source.Kernel.RegistrySetInformation += (ev) => ProcessKeyEvent(ev);
            session.Source.Kernel.RegistryVirtualize += (ev) => ProcessKeyEvent(ev);
            session.Source.Kernel.RegistryDelete += (ev) => ProcessKeyEvent(ev);

            // Value events
            session.Source.Kernel.RegistryEnumerateValueKey += (ev) => ProcessValueEvent(ev);
            session.Source.Kernel.RegistryQueryValue += (ev) => ProcessValueEvent(ev);
            session.Source.Kernel.RegistryQueryMultipleValue += (ev) => ProcessValueEvent(ev);
            session.Source.Kernel.RegistrySetValue += (ev) => ProcessValueEvent(ev);
            session.Source.Kernel.RegistryDeleteValue += (ev) => ProcessValueEvent(ev);

            using var r = cancellationTokenSource.Token.Register(() => session.Stop());
            _ = session.Source.Process();
        }

        public void Stop() => cancellationTokenSource.Cancel();

        private void RundownSession(string sessionName)
        {
            var session = new TraceEventSession(sessionName);
            session.EnableKernelProvider(traceFlags, stackFlags);
            session.Source.Kernel.RegistryKCBRundownBegin += (sender) => ProcessKCBCreateEvent(ev: sender);
            session.Source.Kernel.RegistryKCBRundownEnd += (sender) => ProcessKCBDeleteEvent(ev: sender);

            using var r = cancellationTokenSource.Token.Register(() => session.Stop());
            _ = session.Source.Process();
        }

        private void ProcessKCBCreateEvent(RegistryTraceData ev)
        {
            _logger
                .LogInformation("Description: Key Control Block Created.\nTimestamp: {timestamp}\nEvent Name: {event}\nKey Handle: {keyHandle}\nKey Name: {keyName}",
                ev.TimeStampRelativeMSec, ev.EventName, ev.KeyHandle, ev.KeyName);
            regHandleToKeyName[ev.KeyHandle] = ev.KeyName;
        }

        private void ProcessKCBDeleteEvent(RegistryTraceData ev)
        {
            _logger
                .LogInformation("Description: Key Control Block Deleted.\nTimestamp: {timestamp}\nEvent Name: {event}\nKey Handle: {keyHandle}\nKey Name: {keyName}",
                ev.TimeStampRelativeMSec, ev.EventName, ev.KeyHandle, ev.KeyName);
            _ = regHandleToKeyName.Remove(ev.KeyHandle);
        }

        private static void MakeKernelParserStateless(TraceEventSource traceSessionSource)
        {
            const KernelTraceEventParser.ParserTrackingOptions options = KernelTraceEventParser.ParserTrackingOptions.None;
            var kernelParser = new KernelTraceEventParser(traceSessionSource, options);

            var t = traceSessionSource.GetType();
            var kernelField = t.GetField("_Kernel", BindingFlags.Instance | BindingFlags.SetField | BindingFlags.NonPublic);
            kernelField?.SetValue(traceSessionSource, kernelParser);
        }

        private bool Filter(RegistryTraceData ev) => ev.ProcessID == pid
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

        private void ProcessKeyEvent(RegistryTraceData ev)
        {
            if (Filter(ev))
            {
                var keyName = GetFullKeyName(ev.KeyHandle, ev.ValueName);
                _logger
               .LogInformation("Description: Key event.\nTimestamp: {timestamp}\nEvent Name: {event}\nKey Handle: {keyHandle}\nKey Name: {keyName}\nProcess Id: {processId}\nThread ID: {threadId}\nIndex: {index}\nStatus:{status}\nElapsed: {elapsed}",
               ev.TimeStampRelativeMSec, ev.EventName, ev.KeyHandle, keyName, ev.ProcessID, ev.ThreadID, ev.Index, Enum.GetName((RegistryEventCategory)ev.Status), ev.ElapsedTimeMSec);
                _ = regHandleToKeyName.Remove(ev.KeyHandle);
            }
        }

        private void ProcessValueEvent(RegistryTraceData ev)
        {
            if (Filter(ev))
            {
                var keyName = GetFullKeyName(ev.KeyHandle, ev.ValueName);
                _logger
                    .LogInformation("Description: Key event.\nTimestamp: {timestamp}\nEvent Name: {event}\nKey Handle: {keyHandle}\nKey Name: {keyName}\nProcess Id: {processId}\nThread ID: {threadId}\nIndex: {index}\nStatus:{status}\nElapsed: {elapsed}",
               ev.TimeStampRelativeMSec, ev.EventName, ev.KeyHandle, keyName, ev.ProcessID, ev.ThreadID, ev.Index, Enum.GetName((RegistryEventCategory)ev.Status), ev.ElapsedTimeMSec);
            }
        }

        private string GetFullKeyName(ulong keyHandle, string eventKeyName)
        {
            var baseKeyName = string.Empty;
            if (regHandleToKeyName.TryGetValue(keyHandle, out var result))
            {
                baseKeyName = result;
            }

            return Path.Combine(baseKeyName, eventKeyName);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cancellationTokenSource.Dispose();
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
