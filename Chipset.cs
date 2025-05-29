using System.Collections.Generic;

namespace SprdFlashTool.Core
{
    public class Chipset
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ulong Fdl1Address { get; set; }
        public string Fdl1Path { get; set; }
        public ulong Fdl2Address { get; set; }
        public string Fdl2Path { get; set; }
        public string BootKeyInstructions { get; set; }
        public uint FlashReadPartitionId { get; set; }
        public ulong FlashWriteBaseAddress { get; set; }
        public int BaudRate { get; set; }
        public bool UsesTranscoding { get; set; }
        public List<string> KnownCompatibleDevices { get; set; }

        public Chipset()
        {
            KnownCompatibleDevices = new List<string>();
            // Initialize other properties to default sensible values if necessary
            Name = string.Empty;
            Description = string.Empty;
            Fdl1Path = string.Empty;
            Fdl2Path = string.Empty;
            BootKeyInstructions = string.Empty;
        }
    }
}
