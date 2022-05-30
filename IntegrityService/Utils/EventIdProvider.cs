using System;
using Serilog.Events;
using Serilog.Sinks.EventLog;

namespace IntegrityService.Utils
{
    internal sealed class EventIdProvider : IEventIdProvider
    {
        /// <summary>
        ///     Returns event ID based on log content
        /// </summary>
        /// <para>Event ID 7770 - An exception occurred</para>
        /// <para>Event ID 7776 – File / Directory creation</para>
        /// <para>Event ID 7777 – File modification</para>
        /// <para>Event ID 7778 – File / Directory deletion</para>
        /// <para>Event ID 7786 – Registry key creation</para>
        /// <para>Event ID 7787 – Registry key/value modification</para>
        /// <para>Event ID 7788 – Registry key deletion</para>
        /// <para>Event ID 7780 – Other events</para>
        /// <param name="logEvent">Log event to return the Event ID</param>
        /// <returns>Event ID</returns>
        public ushort ComputeEventId(LogEvent logEvent)
        {
            switch (logEvent)
            {
                case { Level: LogEventLevel.Error }:
                    return 7770;
                case { Level: LogEventLevel.Information }:
                    {
                        if (logEvent.Properties.TryGetValue("changeType", out var changeType))
                        {
                            if (Equals(changeType, "FileSystem"))
                            {
                                if (logEvent.Properties.TryGetValue("category", out var category))
                                {
                                    if (Equals(category, "Created"))
                                    {
                                        return 7776;
                                    }

                                    if (Equals(category, "Changed"))
                                    {
                                        return 7777;
                                    }

                                    if (Equals(category, "Deleted"))
                                    {
                                        return 7778;
                                    }
                                }
                            }
                            else
                            {
                                if (logEvent.Properties.TryGetValue("category", out var category))
                                {
                                    if (Equals(category, "Created"))
                                    {
                                        return 7786;
                                    }

                                    if (Equals(category, "Changed"))
                                    {
                                        return 7787;
                                    }

                                    if (Equals(category, "Deleted"))
                                    {
                                        return 7788;
                                    }
                                }
                            }
                        }
                        break;
                    }
            }

            return 7780;
        }

        private static bool Equals(LogEventPropertyValue a, string b) =>
            string.Equals(Sanitize(a), b, StringComparison.OrdinalIgnoreCase);

        private static string Sanitize(LogEventPropertyValue? result) => result?.ToString().Replace("\"", "") ?? string.Empty;
    }
}
