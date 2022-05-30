namespace IntegrityService.Utils
{
    /// <summary>
    ///     RegistryTraceData Status as enum
    /// </summary>
    /// <see href="https://github.com/microsoftarchive/bcl/blob/master/Tools/ETW/traceEvent/KernelTraceEventParser.cs"/>
    internal enum RegistryEventCategory
    {
        Create = 10,
        Open = 11,
        Delete = 12,
        Query = 13,
        SetValue = 14,
        DeleteValue = 15,
        QueryValue = 16,
        EnumerateKey = 17,
        enumerateValueKey = 18,
        QueryMultipleValue = 19,
        SetInformation = 20,
        Flush = 21,
        RunDown = 22,
#pragma warning disable CA1069 // Enums values should not be duplicated
        KCBCreate = 22,
#pragma warning restore CA1069 // Enums values should not be duplicated
        KCBdelete = 23,
        KCBRundownBegin = 24,
        KCBRundownEnd = 25,
        Virtualize=26,
        Close=27
    }
}
