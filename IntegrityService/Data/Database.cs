namespace IntegrityService.Data
{
    internal static class Database
    {
        public static Context Context => _context ??= new Context();

        private static Context _context;

        public static void Start() => _ = Context; // Just initiate it.

        public static void Stop() => _context?.Dispose();
    }
}
