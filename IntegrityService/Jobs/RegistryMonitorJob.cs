using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
        private const string ETWSessionName = "RegistryWatcher";

        private const double MonitorTimeInSeconds = 5.0;

        private const NtKeywords TraceFlags = NtKeywords.Registry;

        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly ConcurrentQueue<RegistryTraceData> _eventQueue = new();

        private readonly ILogger _logger;

        private readonly IMessageStore<RegistryChange, RegistryChangeMessage> _messageStore;

        private readonly int _pid;

        private readonly List<RegistryTraceData> _rawEvents;

        private readonly Dictionary<ulong, string> _regHandleToKeyName = new Dictionary<ulong, string>();

        private bool _disposedValue;

        public RegistryMonitorJob(ILogger logger, IMessageStore<RegistryChange, RegistryChangeMessage> regStore)
        {
            _logger = logger;
            _pid = Environment.ProcessId;
            _cancellationTokenSource = new CancellationTokenSource();
            _messageStore = regStore;
            _rawEvents = new List<RegistryTraceData>();
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

        private void CleanupExistingSession()
        {
            try
            {
                var activeSessions = TraceEventSession.GetActiveSessionNames();
                if (activeSessions.Contains(ETWSessionName))
                {
                    using var session = new TraceEventSession(ETWSessionName);
                    session.Stop();
                    _logger.LogInformation("Cleaned up lingering ETW session 'RegistryWatcher' from a previous run.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while checking or cleaning up lingering ETW session.");
            }
        }

        private string GetFullKeyName(ulong keyHandle, string eventKeyName, string eventValueName)
        {
            if (string.IsNullOrWhiteSpace(eventKeyName) && string.IsNullOrWhiteSpace(eventValueName))
                return string.Empty;

            // Use a StringBuilder for more efficient string concatenation
            var fullNameBuilder = new System.Text.StringBuilder();

            // Lookup the key handle in the dictionary
            if (keyHandle != 0 && _regHandleToKeyName.TryGetValue(keyHandle, out var handleName))
            {
                fullNameBuilder.Append(handleName);
            }

            // Append event key name if available
            if (!string.IsNullOrWhiteSpace(eventKeyName))
            {
                if (fullNameBuilder.Length > 0) fullNameBuilder.Append('\\');
                fullNameBuilder.Append(eventKeyName);
            }

            // Append event value name if available
            if (!string.IsNullOrWhiteSpace(eventValueName))
            {
                if (fullNameBuilder.Length > 0) fullNameBuilder.Append('\\');
                fullNameBuilder.Append(eventValueName);
            }

            // Replace registry prefixes with human-readable paths
            var fullName = fullNameBuilder.ToString();
            fullName = fullName.Replace(@"\REGISTRY\MACHINE", "HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase);
            fullName = fullName.Replace(@"\REGISTRY\USER", "HKEY_USERS", StringComparison.OrdinalIgnoreCase);

            return fullName;
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
                    _eventQueue.Enqueue((RegistryTraceData)ev!.Clone());
                }
            }
            catch (Exception ex)
            {
                ex.Log(_logger);
            }
        }

        private async Task ProcessEventQueueAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    while (_eventQueue.TryDequeue(out var ev))
                    {
                        try
                        {
                            var eev = new ExtendedRegistryTraceData(ev, GetFullKeyName(ev.KeyHandle, ev.KeyName, ev.ValueName));
                            _logger.LogInformation("Change Type: {changeType:l}\nCategory: {category:l}\nEvent Data:\n{eev:l}",
                                Enum.GetName(ConfigChangeType.Registry), Enum.GetName(eev.ChangeCategory), eev.ToString());

                            var change = RegistryChange.FromTrace(eev);
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
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event queue.");
                }
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
            CleanupExistingSession();

            _logger.LogInformation("Started ETW session 'RegistryWatcher' for Registry changes.");

            // Start event queue processing
            var processingTask = Task.Run(() => ProcessEventQueueAsync(_cancellationTokenSource.Token));

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                var session = new TraceEventSession(ETWSessionName, null);
                session.BufferSizeMB = 1024;
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

                foreach (var ev in _rawEvents)
                {
                    try
                    {
                        var eev = new ExtendedRegistryTraceData(ev, GetFullKeyName(ev.KeyHandle, ev.KeyName, ev.ValueName));
                        _logger
                            .LogInformation("Change Type: {changeType:l}\nCategory: {category:l}\nEvent Data:\n{eev:l}",
                            Enum.GetName(ConfigChangeType.Registry), Enum.GetName(eev.ChangeCategory), eev.ToString());

                        var change = RegistryChange.FromTrace(eev);
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
                _rawEvents.Clear();
                session.Stop();
                session.Dispose();
            }

            // Ensure processing task completes when stopping
            processingTask.Wait();
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