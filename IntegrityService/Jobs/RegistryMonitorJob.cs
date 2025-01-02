using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        private const int EventQueueThreshold = 10000;

        private const double MonitorTimeInSeconds = 0.2;

        private const NtKeywords TraceFlags = NtKeywords.Registry;

        private readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(MonitorTimeInSeconds);

        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly ConcurrentDictionary<RegistryTraceData, DateTime> _deduplicationCache = new();

        private readonly ConcurrentQueue<RegistryTraceData> _eventQueue = new();

        private readonly ILogger _logger;

        private readonly IMessageStore<RegistryChange, RegistryChangeMessage> _messageStore;

        private readonly int _pid;

        private readonly Dictionary<ulong, string> _regHandleToKeyName = new();

        private bool _disposedValue;

        public RegistryMonitorJob(ILogger logger, IMessageStore<RegistryChange, RegistryChangeMessage> regStore)
        {
            _logger = logger;
            _pid = Environment.ProcessId;
            _cancellationTokenSource = new CancellationTokenSource();
            _messageStore = regStore;
        }

        /// <summary>
        ///     Start monitoring selected Registry keys
        /// </summary>
        public void Start()
        {
            CleanupExistingSession();
            _logger.LogInformation("Started ETW session 'RegistryWatcher' for Registry changes.");

            // Start tasks
            var sessionTask = Task.Run(() => MonitorETWSession(_cancellationTokenSource.Token));
            var cleanupTask = Task.Run(() => CleanupDeduplicationCacheAsync(_cancellationTokenSource.Token));
            var processingTask = Task.Run(() => ProcessEventQueue(_cancellationTokenSource.Token));

            // Wait for all tasks to complete
            Task.WhenAll(sessionTask, cleanupTask, processingTask).Wait();
        }

        /// <summary>
        ///     Stop monitoring selected Registry keys
        /// </summary>
        public void Stop() => _cancellationTokenSource.Cancel();

        private async Task CleanupDeduplicationCacheAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var expirationThreshold = DateTime.UtcNow - _cacheExpiration;

                    foreach (var kvp in _deduplicationCache)
                    {
                        // Remove entries older than the expiration threshold
                        if (kvp.Value < expirationThreshold)
                        {
                            _deduplicationCache.TryRemove(kvp.Key, out _);
                        }
                    }

                    // Wait before the next cleanup cycle
                    await Task.Delay((int)(MonitorTimeInSeconds * 1000), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during deduplication cache cleanup.");
                }
            }
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

        private void EnqueueRawEvent(RegistryTraceData ev)
        {
            // Attempt to add the RegistryTraceData to the deduplication cache
            if (_deduplicationCache.TryAdd(ev, DateTime.UtcNow))
            {
                // If successfully added (not a duplicate), enqueue the event
                _eventQueue.Enqueue((RegistryTraceData)ev.Clone());
            }

            if (_eventQueue.Count > EventQueueThreshold)
            {
                _logger.LogWarning("Event queue size exceeded 10,000 items. Consider throttling input.");
            }
        }

        private string GetFullKeyName(ulong keyHandle, string eventKeyName, string eventValueName)
        {
            if (string.IsNullOrWhiteSpace(eventKeyName) && string.IsNullOrWhiteSpace(eventValueName))
                return string.Empty;

            var fullNameBuilder = new System.Text.StringBuilder();
            if (keyHandle != 0 && _regHandleToKeyName.TryGetValue(keyHandle, out var handleName))
            {
                fullNameBuilder.Append(handleName);
            }

            if (!string.IsNullOrWhiteSpace(eventKeyName))
            {
                if (fullNameBuilder.Length > 0) fullNameBuilder.Append('\\');
                fullNameBuilder.Append(eventKeyName);
            }

            if (!string.IsNullOrWhiteSpace(eventValueName))
            {
                if (fullNameBuilder.Length > 0) fullNameBuilder.Append('\\');
                fullNameBuilder.Append(eventValueName);
            }

            var fullName = fullNameBuilder.ToString();
            fullName = fullName.Replace(@"\REGISTRY\MACHINE", "HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase);
            fullName = fullName.Replace(@"\REGISTRY\USER", "HKEY_USERS", StringComparison.OrdinalIgnoreCase);

            return fullName;
        }

        private bool IsMonitoredEvent(string keyName, int pid) => pid != _pid && pid != -1 && !string.IsNullOrEmpty(keyName) && Settings.Instance.IsMonitoredKey(keyName);

        /// <summary>
        ///     Prepare ETW parser
        /// </summary>
        /// <param name="traceSessionSource">
        ///     WTW trace event source to listen
        /// </param>
        private void MakeKernelParserStateless(ETWTraceEventSource traceSessionSource)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(traceSessionSource);

                const KernelTraceEventParser.ParserTrackingOptions options = KernelTraceEventParser.ParserTrackingOptions.None;
                var kernelParser = new KernelTraceEventParser(traceSessionSource, options);

                var t = traceSessionSource.GetType();
                var kernelField = t.GetField("_Kernel", BindingFlags.Instance | BindingFlags.SetField | BindingFlags.NonPublic);
                kernelField?.SetValue(traceSessionSource, kernelParser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Kernel parser stateless configuration.");
            }
        }

        private void MonitorETWSession(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var session = new TraceEventSession(ETWSessionName, null)
                {
                    BufferSizeMB = 1024
                };

                session.EnableKernelProvider(TraceFlags);
                MakeKernelParserStateless(session.Source);

                session.Source.Kernel.RegistryKCBRundownEnd += (RegistryTraceData data) => _regHandleToKeyName[data.KeyHandle] = data.KeyName;
                session.Source.Kernel.RegistryCreate += EnqueueRawEvent;
                session.Source.Kernel.RegistryDelete += EnqueueRawEvent;
                session.Source.Kernel.RegistrySetValue += EnqueueRawEvent;
                session.Source.Kernel.RegistryDeleteValue += EnqueueRawEvent;
                session.Source.Kernel.RegistrySetInformation += EnqueueRawEvent;

                using (session)
                {
                    var timer = new Timer((_) => session.Stop(), null, (int)(MonitorTimeInSeconds * 1000), Timeout.Infinite);
                    session.Source.Process();
                }
            }
        }

        private void ProcessEventQueue(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    while (_eventQueue.TryDequeue(out var ev))
                    {
                        try
                        {
                            var fullname = GetFullKeyName(ev.KeyHandle, ev.KeyName, ev.ValueName);
                            if (!IsMonitoredEvent(fullname, ev.ProcessID))
                            {
                                continue;
                            }
                            Debug.WriteLine($"Processing event: {ev.EventName} for {fullname}");
                            var eev = new ExtendedRegistryTraceData(ev, fullname);
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

        #region Dispose

        public void Dispose()
        {
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