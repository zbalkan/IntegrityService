using System;

namespace IntegrityService.FIM
{
    public class Change
    {
        public string Id { get; set; }

        public string Entity { get; set; }

        public string SourceComputer { get; set; }

        public DateTime DateTime { get; set; }

        public ConfigChangeType ConfigChangeType { get; set; }

        public ChangeCategory ChangeCategory { get; set; }
    }
}
