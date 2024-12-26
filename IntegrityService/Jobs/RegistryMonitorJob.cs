using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using IntegrityService.FIM;
using IntegrityService.Message;
using IntegrityService.Utils;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
using NtKeywords = Microsoft.Diagnostics.Tracing.Parsers.KernelTraceEventParser.Keywords;

namespace IntegrityService.Jobs
{
    /// <summary>
    ///     A class capturing Registry events.
    /// </summary>
    /// <see href="https://github.com/lowleveldesign/lowleveldesign-blog-samples/blob/master/monitoring-registry-activity-with-etw/Program.fs" />
    internal partial class RegistryMonitorJob : IMonitor
    {
        private const double MonitorTimeInSeconds = 0.2;

        private const NtKeywords TraceFlags = NtKeywords.Registry;

        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly List<ExtendedRegistryTraceData> _events;

        private readonly ILogger _logger;

        private readonly IMessageStore<RegistryChange, RegistryChangeMessage> _messageStore;

        private readonly int _pid;

        private readonly Dictionary<ulong, string> _regHandleToKeyName = new Dictionary<ulong, string>();

        private bool _disposedValue;

        public RegistryMonitorJob(ILogger logger, IMessageStore<RegistryChange, RegistryChangeMessage> regStore)
        {
            _logger = logger;
            _pid = Environment.ProcessId;
            _cancellationTokenSource = new CancellationTokenSource();
            _messageStore = regStore;
            _events = new List<ExtendedRegistryTraceData>();
        }

        /// <summary>
        ///     Start monitoring selected Registry keys
        /// </summary>
        /// <exception cref="FieldAccessException">
        /// </exception>
        /// <exception cref="TargetException">
        /// </exception>
        public void Start() =>

            // No baseline database for registry keys
            StartSession();

        /// <summary>
        ///     Stop monitoring selected Registry keys
        /// </summary>
        /// <exception cref="AggregateException">
        /// </exception>
        public void Stop() => _cancellationTokenSource.Cancel();

        /// <summary>
        ///     Prepare ETW parser
        /// </summary>
        /// <param name="traceSessionSource">
        ///     WTW trace event source to listen
        /// </param>
        /// <exception cref="FieldAccessException">
        /// </exception>
        /// <exception cref="TargetException">
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// </exception>
        private static void MakeKernelParserStateless(ETWTraceEventSource traceSessionSource)
        {
            ArgumentNullException.ThrowIfNull(traceSessionSource);

            const KernelTraceEventParser.ParserTrackingOptions options = KernelTraceEventParser.ParserTrackingOptions.None;
            var kernelParser = new KernelTraceEventParser(traceSessionSource, options);

            var t = traceSessionSource.GetType();
            var kernelField = t.GetField("_Kernel", BindingFlags.Instance | BindingFlags.SetField | BindingFlags.NonPublic);
            kernelField?.SetValue(traceSessionSource, kernelParser);
        }

        private bool IsMonitoredEvent(string keyName, int pid)
        {
            if (pid == _pid || pid == -1)
            {
                return false;
            }

            if (string.IsNullOrEmpty(keyName))
            {
                return false;
            }

            return Settings.Instance.IsMonitoredKey(keyName);
        }

        private void ProcessEvent(RegistryTraceData ev)
        {
            try
            {
                var keyName = GetFullKeyName(ev.KeyHandle, ev.KeyName, ev.ValueName);

                if (IsMonitoredEvent(keyName, ev.ProcessID))
                {
                    var eev = new ExtendedRegistryTraceData(ev, keyName);

                    _events.Add(eev);
                }
            }
            catch (Exception ex)
            {
                ex.Log(_logger);
            }
        }

        /// <summary>
        ///     Start a new ETW session
        /// </summary>
        /// <exception cref="FieldAccessException">
        /// </exception>
        /// <exception cref="TargetException">
        /// </exception>
        private void StartSession()
        {
            _logger.LogInformation("Started ETW session 'RegistryWatcher' for Registry changes.");

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                var session = new TraceEventSession("RegistryWatcher", null);
                session.EnableKernelProvider(TraceFlags);
                MakeKernelParserStateless(session.Source);

                session.Source.Kernel.RegistryKCBRundownEnd += (RegistryTraceData data) => _regHandleToKeyName[data.KeyHandle] = data.KeyName;

                session.Source.Kernel.RegistryCreate += ProcessEvent;
                session.Source.Kernel.RegistryDelete += ProcessEvent;
                session.Source.Kernel.RegistrySetValue += ProcessEvent;
                session.Source.Kernel.RegistryDeleteValue += ProcessEvent;
                session.Source.Kernel.RegistrySetInformation += ProcessEvent;

                // Run for 200ms and cancel
                using (session)
                {
                    var timer = new Timer((object? _) => session!.Stop(), null, (int)(MonitorTimeInSeconds * 1000), Timeout.Infinite);
                    session.Source.Process();
                }

                foreach (var ev in _events)
                {
                    try
                    {
                        _logger
                            .LogInformation("<Data>Change Type: {changeType:l}\nEvent Data:\n{ev:l}</Data>",
                            Enum.GetName(ConfigChangeType.Registry), ev.ToString());

                        var change = IO.Registry.GenerateChange(ev);
                        if (change != null)
                        {
                            _messageStore.Add(change);
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.Log(_logger);
                    }
                }
                _events.Clear();
                session.Stop();
                session.Dispose();
            }
        }

        private string GetFullKeyName(ulong keyHandle, string eventKeyName, string eventValueName)
        {
            if (string.IsNullOrWhiteSpace(eventKeyName) && string.IsNullOrWhiteSpace(eventValueName))
                return string.Empty;

            var fullName = string.Empty;

            if (keyHandle != 0 && _regHandleToKeyName.TryGetValue(keyHandle, out var result))
            {
                fullName = result;
            }
            if (!string.IsNullOrWhiteSpace(eventKeyName))
                fullName += "\\" + eventKeyName;
            if (!string.IsNullOrWhiteSpace(eventValueName))
                fullName += "\\" + eventValueName;

            fullName = Regex.Replace(fullName, @"\\REGISTRY\\MACHINE", "HKEY_LOCAL_MACHINE", RegexOptions.IgnoreCase);
            fullName = Regex.Replace(fullName, @"\\REGISTRY\\USER", "HKEY_USERS", RegexOptions.IgnoreCase);

            return fullName;
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
                }

                _disposedValue = true;
            }
        }

        #endregion Dispose
    }
}