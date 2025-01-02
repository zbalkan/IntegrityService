// {{ FIM }} Copyright (C) {{ 2022 }} {{ Zafer Balkan }}
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrityService.Data;
using IntegrityService.FIM;
using IntegrityService.Message;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntegrityService
{
    internal partial class PersistenceWorker : BackgroundService
    {
        private const int BUCKET_SIZE = 5000;

        private const int INTERVAL_MS = 50;

        private readonly ILiteDbContext _ctx;

        private readonly IMessageStore<FileSystemChange, FileSystemChangeMessage> _fsStore;

        private readonly ILogger<WatcherWorker> _logger;

        private readonly IMessageStore<RegistryChange, RegistryChangeMessage> _regStore;

        public PersistenceWorker(ILogger<WatcherWorker> logger,
                      IMessageStore<FileSystemChange, FileSystemChangeMessage> fsStore,
                      IMessageStore<RegistryChange, RegistryChangeMessage> regStore, ILiteDbContext ctx)
        {
            _logger = logger;
            _fsStore = fsStore;
            _regStore = regStore;
            _ctx = ctx;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
            Task.Run(async () =>
            {
                _logger.LogInformation("Initiated Persistence Worker");
                if (!Settings.Instance.DisableLocalDatabase)
                {// This loop must continue until service is stopped.
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        // read from stores as bulk and write to database.
                        var fsCount = Math.Min(_fsStore.Count(), BUCKET_SIZE);
                        if (fsCount > 0)
                        {
                            _ = _ctx.FileSystemChanges.InsertBulk(_fsStore.Take(fsCount).Select(m => m.Change));
                            Debug.WriteLine($"Succesfully inserted {fsCount} items.");
                        }

                        var regCount = Math.Min(_regStore.Count(), BUCKET_SIZE);
                        if (regCount > 0)
                        {
                            _ = _ctx.RegistryChanges.InsertBulk(_regStore.Take(regCount).Select(m => m.Change));
                            Debug.WriteLine($"Succesfully inserted {regCount} items.");
                        }

                        await Task.Delay(INTERVAL_MS, stoppingToken);
                    }
                }

            });
    }
}