namespace IntegrityService.FIM
{
    public class FileSystemChange : Change
    {
        public string CurrentHash { get; set; }

        public string FullPath { get; set; }

        public string PreviousHash { get; set; }
    }
}