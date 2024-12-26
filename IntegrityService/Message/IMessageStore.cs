// {{ FIM }} Copyright (C) {{ 2022 }} {{ Zafer Balkan }}
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System.Collections.Generic;
using System.Threading.Tasks;
using IntegrityService.FIM;

namespace IntegrityService.Message
{
    public interface IMessageStore<T, K>
        where T : IChange
        where K : IMessage<T>
    {
        Task Add(T change);

        public int Count();

        public bool HasNext();

        public List<K> Take(int count);

        public List<K> TakeAll();

        public K? TakeNext();
    }
}