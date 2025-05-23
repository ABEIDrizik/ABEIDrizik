namespace MTKDeviceManager
{
    /// <summary>
    /// Holds information about a connected MTK device.
    /// </summary>
    public class DeviceInfo
    {
        /// <summary>
        /// Gets or sets the communication port of the device (e.g., "COM5").
        /// </summary>
        public string Port { get; set; }

        /// <summary>
        /// Gets or sets the hardware ID of the device (e.g., "USB\VID_0E8D&PID_0003").
        /// </summary>
        public string HardwareId { get; set; }

        /// <summary>
        /// Gets or sets the chipset identifier of the device (e.g., "MT6765").
        /// </summary>
        public string Chipset { get; set; }

        /// <summary>
        /// Gets or sets the serial number of the device.
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the firmware version installed on the device.
        /// </summary>
        public string FirmwareVersion { get; set; }

        /// <summary>
        /// Gets or sets the board name or model of the device.
        /// </summary>
        public string BoardName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceInfo"/> class,
        /// populating properties with empty strings.
        /// </summary>
        public DeviceInfo()
        {
            // Initialize with default or empty values
            Port = string.Empty;
            HardwareId = string.Empty;
            Chipset = string.Empty;
            SerialNumber = string.Empty;
            FirmwareVersion = string.Empty;
            BoardName = string.Empty;
        }

        /// <summary>
        /// Returns a string representation of the device information.
        /// </summary>
        /// <returns>A string containing key device properties.</returns>
        public override string ToString()
        {
            return $"Port: {Port}, HWID: {HardwareId}, Chipset: {Chipset}, SN: {SerialNumber}, FW: {FirmwareVersion}, Board: {BoardName}";
        }
    }
}
