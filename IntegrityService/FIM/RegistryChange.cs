namespace IntegrityService.FIM
{
    public class RegistryChange : Change
    {
        public string Key { get; set; }

        public string Hive { get; set; }

        public string ValueName { get; set; }

        public string ValueData { get; set; }

        public string ACLs { get; set; }
    }
}