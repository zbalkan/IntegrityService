namespace IntegrityService.Utils
{
    internal static class Database
    {
        public static Context Context
        {
            get
            {
                if (_context == null)
                {
                    _context = new Context(Settings.Instance.ConnectionString);
                }
                return _context;
            }
        }

        private static Context _context;

        public static void Start() => _ = _context; // Just initiate it.

        public static void Stop() => _context.Dispose();
    }
}
