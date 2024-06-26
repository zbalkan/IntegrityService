﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using IntegrityService.FIM;
using IntegrityService.Utils;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NtKeywords = Microsoft.Diagnostics.Tracing.Parsers.KernelTraceEventParser.Keywords;

namespace IntegrityService.Jobs
{
    /// <summary>
    ///     A class capturing Registry events.
    /// </summary>
    /// <see href="https://github.com/lowleveldesign/lowleveldesign-blog-samples/blob/master/monitoring-registry-activity-with-etw/Program.fs"/>
    internal partial class RegistryMonitorJob : IMonitor
    {
        const NtKeywords StackFlags = NtKeywords.None;
        const NtKeywords TraceFlags = NtKeywords.Registry;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger _logger;
        private readonly TraceEventSession _mainSession;
        private readonly int _pid;
        private readonly Dictionary<ulong, string> _regHandleToKeyName;
        private readonly TraceEventSession _rundownSession;

        private bool _disposedValue;
        public RegistryMonitorJob(ILogger logger)
        {
            _logger = logger;
            _regHandleToKeyName = [];
            _mainSession = new TraceEventSession("RegistryWatcher");
            _rundownSession = new TraceEventSession("RegistryWatcher-rundown");
            _pid = Environment.ProcessId;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        ///     Start monitoring selected Registry keys
        /// </summary>
        /// <exception cref="FieldAccessException"></exception>
        /// <exception cref="TargetException"></exception>
        public void Start() =>
            // No baseline database for registry keys
            StartSession();

        /// <summary>
        ///     Stop monitoring selected Registry keys
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        public void Stop() => _cancellationTokenSource.Cancel();

        /// <summary>
        ///     Get the Registry Key name from the event trace
        /// </summary>
        /// <param name="ev">ETW event trace</param>
        /// <returns>Registry Key</returns>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        /// <exception cref="OverflowException"></exception>
        private static RegistryKey GetKeyFromTraceData(RegistryTraceData ev) => RegistryKey.FromHandle(new Microsoft.Win32.SafeHandles.SafeRegistryHandle(new nint((long)ev.KeyHandle), true));

        /// <summary>
        ///     Prepare ETW parser
        /// </summary>
        /// <param name="traceSessionSource">WTW trace event source to listen</param>
        /// <exception cref="FieldAccessException"></exception>
        /// <exception cref="TargetException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        private static void MakeKernelParserStateless(ETWTraceEventSource traceSessionSource)
        {
            ArgumentNullException.ThrowIfNull(traceSessionSource);

            const KernelTraceEventParser.ParserTrackingOptions options = KernelTraceEventParser.ParserTrackingOptions.None;
            var kernelParser = new KernelTraceEventParser(traceSessionSource, options);

            var t = traceSessionSource.GetType();
            var kernelField = t.GetField("_Kernel", BindingFlags.Instance | BindingFlags.SetField | BindingFlags.NonPublic);
            kernelField?.SetValue(traceSessionSource, kernelParser);
        }

        private bool FilterMonitored(RegistryTraceData ev)
        {
            var keyName = GetFullKeyName(ev.KeyHandle, ev.KeyName);
            return ev.ProcessID == _pid
            ||
            ev.ProcessID == -1
            ||
            Settings.Instance.IsMonitoredKey(keyName);
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

        private Action<RegistryTraceData> NewChangeEvent() => (ev) => ProcessEvent(ev, ChangeCategory.Changed);

        private Action<RegistryTraceData> NewCreateEvent() => (ev) => ProcessEvent(ev, ChangeCategory.Created);

        private Action<RegistryTraceData> NewDeleteEvent() => (ev) => ProcessEvent(ev, ChangeCategory.Deleted);

        private void ProcessEvent(RegistryTraceData ev, ChangeCategory changeCategory)
        {
            try
            {
                if (FilterMonitored(ev))
                {
                    var key = GetKeyFromTraceData(ev);

                    _logger
                   .LogInformation("Category: {category}\nChange Type: {changeType}\nDescription: Key event.\nTimestamp: {timestamp}\nEvent Name: {event}\nKey Handle: {keyHandle}\nKey Name: {keyName}\nProcess Id: {processId}\nThread ID: {threadId}\nIndex: {index}\nStatus:{status}\nElapsed: {elapsed}",
                   Enum.GetName(changeCategory), Enum.GetName(ConfigChangeType.Registry), ev.TimeStampRelativeMSec, ev.EventName, ev.KeyHandle, key.Name, ev.ProcessID, ev.ThreadID, ev.Index, Enum.GetName((RegistryEventCategory)ev.Status), ev.ElapsedTimeMSec);
                    _ = _regHandleToKeyName.Remove(ev.KeyHandle);

                    IO.Registry.GenerateChange(key, ev.ValueName, key.GetValue(ev.ValueName)?.ToString() ?? string.Empty, changeCategory);
                }
            }
            catch (Exception ex)
            {
                ex.Log(_logger);
            }
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

        private void RundownSession()
        {
            _logger.LogInformation("Started ETW session {session} for Registry changes.", _rundownSession.SessionName);
            _rundownSession.EnableKernelProvider(TraceFlags, StackFlags);
            _rundownSession.Source.Kernel.RegistryKCBRundownBegin += ProcessKcbCreateEvent;
            _rundownSession.Source.Kernel.RegistryKCBRundownEnd += ProcessKcbDeleteEvent;

            using var r = _cancellationTokenSource.Token.Register(() => _rundownSession.Stop());
            _ = _rundownSession.Source.Process();
        }

        /// <summary>
        ///     Start a new ETW session
        /// </summary>
        /// <exception cref="FieldAccessException"></exception>
        /// <exception cref="TargetException"></exception>
        private void StartSession()
        {
            _logger.LogInformation("Started ETW session {session} for Registry changes.", _mainSession.SessionName);
            _ = _mainSession.EnableKernelProvider(TraceFlags, StackFlags);
            MakeKernelParserStateless(_mainSession.Source);
            RundownSession();

            // KCB vents
            _mainSession.Source.Kernel.RegistryKCBCreate += ProcessKcbCreateEvent;
            _mainSession.Source.Kernel.RegistryKCBDelete += ProcessKcbDeleteEvent;

            // Key events
            _mainSession.Source.Kernel.RegistryCreate += NewCreateEvent();
            _mainSession.Source.Kernel.RegistryFlush += NewChangeEvent();
            _mainSession.Source.Kernel.RegistrySetInformation += NewChangeEvent();
            _mainSession.Source.Kernel.RegistryDelete += NewDeleteEvent();

            // Value events
            _mainSession.Source.Kernel.RegistrySetValue += NewChangeEvent();
            _mainSession.Source.Kernel.RegistryDeleteValue += NewDeleteEvent();

            // Ignored read/numerate/query events
            // - Source.Kernel.RegistryOpen
            // - Source.Kernel.RegistryClose
            // - Source.Kernel.RegistryEnumerateKey
            // - Source.Kernel.RegistryQuery
            // - Source.Kernel.RegistryVirtualize
            // - Source.Kernel.RegistryEnumerateValueKey
            // - Source.Kernel.RegistryQueryValue
            // - Source.Kernel.RegistryQueryMultipleValue

            var r = _cancellationTokenSource.Token.Register(() => _mainSession.Stop());
            _ = _mainSession.Source.Process();
        }
        #region Dispose
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Dispose();

                    // KCB vents
                    _mainSession.Source.Kernel.RegistryKCBCreate -= ProcessKcbCreateEvent;
                    _mainSession.Source.Kernel.RegistryKCBDelete -= ProcessKcbDeleteEvent;

                    // Key events
                    _mainSession.Source.Kernel.RegistryCreate -= NewCreateEvent();
                    _mainSession.Source.Kernel.RegistryFlush -= NewChangeEvent();
                    _mainSession.Source.Kernel.RegistrySetInformation -= NewChangeEvent();
                    _mainSession.Source.Kernel.RegistryDelete -= NewDeleteEvent();

                    // Value events
                    _mainSession.Source.Kernel.RegistrySetValue -= NewChangeEvent();
                    _mainSession.Source.Kernel.RegistryDeleteValue -= NewDeleteEvent();

                    _mainSession.Dispose();

                    // Rundown session events
                    _rundownSession.Source.Kernel.RegistryKCBRundownBegin -= ProcessKcbCreateEvent;
                    _rundownSession.Source.Kernel.RegistryKCBRundownEnd -= ProcessKcbDeleteEvent;

                    _rundownSession.Dispose();
                }

                _disposedValue = true;
            }
        }

        #endregion Dispose
    }
}
