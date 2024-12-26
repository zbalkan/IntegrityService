namespace IntegrityService.FIM
{
    public class RegistryChange : Change
    {
        public string Hive { get; set; }

        public string Key { get; set; }

        public string ValueData { get; set; }

        public string ValueName { get; set; }
    }
}