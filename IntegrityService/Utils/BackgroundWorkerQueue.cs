﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrityService.Utils
{
    public class BackgroundWorkerQueue
    {
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _workItems = new();

        private readonly SemaphoreSlim _signal = new(0);

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
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            _workItems.Enqueue(workItem);
            _signal.Release();
        }
    }
}
