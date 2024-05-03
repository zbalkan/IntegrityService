using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrityService.Utils
{
    public class BackgroundWorkerQueue : IDisposable
    {
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _workItems = new();

        private readonly SemaphoreSlim _signal = new(0);
        private bool disposedValue;

        public async Task<Func<CancellationToken, Task>?> DequeueAsync(CancellationToken cancellationToken)
        {
            if (!_workItems.IsEmpty)
            {
                await _signal.WaitAsync(cancellationToken);
                _workItems.TryDequeue(out var workItem);

                return workItem;
            }
            return null;
        }

        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            ArgumentNullException.ThrowIfNull(workItem);

            _workItems.Enqueue(workItem);
            _signal.Release();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _signal.Dispose();
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
