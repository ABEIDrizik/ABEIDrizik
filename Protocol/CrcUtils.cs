using System;

namespace SprdFlashTool.Core.Protocol
{
    /// <summary>
    /// Provides utility methods for calculating CRC (Cyclic Redundancy Check) values.
    /// These CRC algorithms are typically used in communication protocols with Spreadtrum devices.
    /// </summary>
    public static class CrcUtils
    {
        /// <summary>
        /// Calculates the CRC-16-XMODEM value for the given data.
        /// This algorithm is commonly used in boot mode for Spreadtrum devices.
        /// </summary>
        /// <param name="data">The byte array to calculate the CRC for.</param>
        /// <returns>The calculated CRC-16-XMODEM as a ushort.</returns>
        public static ushort CalculateXmodemCrc(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            ushort crc = 0;
            byte msb = (byte)(crc >> 8);
            byte lsb = (byte)(crc & 0xFF);

            foreach (byte c_byte in data)
            {
                byte x = (byte)(c_byte ^ msb);
                x ^= (byte)(x >> 4);
                msb = (byte)((lsb ^ (x >> 3) ^ (x << 4)) & 0xFF);
                lsb = (byte)((x ^ (x << 5)) & 0xFF);
            }
            return (ushort)((msb << 8) | lsb);
        }

        /// <summary>
        /// Calculates the CRC-16 value used in FDL1/FDL2 mode for Spreadtrum devices.
        /// </summary>
        /// <param name="data">The byte array to calculate the CRC for.</param>
        /// <returns>The calculated FDL CRC-16 as a ushort.</returns>
        public static ushort CalculateFdlCrc(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            uint crc = 0; // Use uint for intermediate calculations to allow overflow similar to Python
            int length = data.Length;

            for (int i = 0; i < length; i += 2)
            {
                if (i + 1 == length) // Odd number of bytes
                {
                    crc += data[i];
                }
                else
                {
                    crc += (uint)((data[i] << 8) | data[i + 1]);
                }
            }

            crc = (crc >> 16) + (crc & 0xFFFF); // Fold 32-bit sum to 16-bit
            crc += (crc >> 16); // Add carry from folding
            
            return (ushort)(~crc & 0xFFFF); // Invert and mask to 16 bits
        }
    }
}
