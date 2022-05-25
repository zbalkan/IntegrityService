using System;
using Serilog.Events;
using Serilog.Sinks.EventLog;

namespace IntegrityService.Utils
{
    internal class EventIdProvider : IEventIdProvider
    {
        /// <summary>
        ///     Returns event ID based on log content
        /// </summary>
        /// <para>Event ID 7770 - An exception occurred</para>
        /// <para>Event ID 7776 – File / Directory creation</para>
        /// <para>Event ID 7777 – File modification</para>
        /// <para>Event ID 7778 – File / Directory deletion</para>
        /// <para>Event ID 7780 – Other events</para>
        /// <param name="logEvent">Log event to return the Event ID</param>
        /// <returns>Event ID</returns>
        public ushort ComputeEventId(LogEvent logEvent)
        {
            switch (logEvent)
            {
                case { Level: LogEventLevel.Error }:
                    return 7770;
                case {Level: LogEventLevel.Information} when logEvent.Properties.TryGetValue("category", out var result):
                {
                    if (Equals(result, "Created"))
                    {
                        return 7776;
                    }

                    if (Equals(result, "Changed"))
                    {
                        return 7777;
                    }

                    if (Equals(result, "Deleted"))
                    {
                        return 7778;
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
