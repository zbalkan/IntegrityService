﻿// {{ FIM }} Copyright (C) {{ 2022 }} {{ Zafer Balkan }}
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using IntegrityService.FIM;

namespace IntegrityService.Message
{
    public interface IMessage<T> where T : IChange
    {
        T Change { get; set; }

        string DocumentId { get; set; }

        string Index { get; set; }
    }
}