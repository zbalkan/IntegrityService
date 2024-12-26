// {{ FIM }} Copyright (C) {{ 2022 }} {{ Zafer Balkan }}
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntegrityService.FIM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace IntegrityService.Message
{
    public class RegistryMessageStore : IMessageStore<RegistryChange, RegistryChangeMessage>
    {
        private const string INDEX_NAME = "fileSystemChanges";

        private readonly ILogger<RegistryMessageStore> _logger;

        private readonly ObjectPool<StringBuilder> pool = new DefaultObjectPoolProvider().CreateStringBuilderPool();

        private readonly ConcurrentDictionary<string, RegistryChangeMessage> store = new();

        public RegistryMessageStore(ILogger<RegistryMessageStore> logger)
        {
            _logger = logger;
        }

        public Task Add(RegistryChange change)
        {
            ArgumentNullException.ThrowIfNull(change);

            var documentId = BuildDocumentId(INDEX_NAME, change.Entity);

            var item = new RegistryChangeMessage
            {
                Index = INDEX_NAME,
                DocumentId = documentId,
                Change = change
            };

            store.AddOrUpdate(item.DocumentId, item, (_, _) => item);
            return Task.CompletedTask;
        }

        public int Count() => store.Count;

        public bool HasNext() => !store.IsEmpty;

        public List<RegistryChangeMessage> Take(int count)
        {
            var result = new List<RegistryChangeMessage>();
            var counter = 0;
            foreach (var key in store.Keys)
            {
                if (counter == count)
                {
                    break;
                }
                store.TryRemove(key, out var message);
                if (message != null) { result.Add(message); }

                counter++;
            }

            return result;
        }

        public List<RegistryChangeMessage> TakeAll()
        {
            var result = new List<RegistryChangeMessage>();
            foreach (var key in store.Keys)
            {
                store.TryRemove(key, out var message);
                if (message != null) { result.Add(message); }
            }

            return result;
        }

        public RegistryChangeMessage? TakeNext() => store.TryRemove(store.Keys.First(), out var message) ? message : null;

        private string BuildDocumentId(string index, string fileName)
        {
            const char separator = '_';

            var sb = pool.Get();
            sb.Append(index);
            sb.Append(separator);
            sb.Append(fileName.Replace(Path.DirectorySeparatorChar, separator).Replace(' ', separator));
            var result = sb.ToString();
            pool.Return(sb);

            return result;
        }
    }
}