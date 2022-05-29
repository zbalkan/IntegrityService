namespace IntegrityService.FIM
{
    public class FileSystemChange : Change
    {
        public string FullPath { get; set; }

        public string ACLs { get; set; }

        public string PreviousHash { get; set; }

        public string CurrentHash { get; set; }
    }
}