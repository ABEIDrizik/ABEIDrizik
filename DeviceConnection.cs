using System;
using System.Collections.Generic;
using System.Linq;
using SprdFlashTool.Core.Comm;
using SprdFlashTool.Core.Protocol;

namespace SprdFlashTool.Core
{
    public class DeviceConnection
    {
        private readonly IFlashUI _ui;
        private readonly IUsbCommunicator _usbCommunicator;
        private bool _useFdlCrc = false;

        public bool IsConnected { get; private set; }

        public DeviceConnection(IFlashUI ui, IUsbCommunicator usbCommunicator)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _usbCommunicator = usbCommunicator ?? throw new ArgumentNullException(nameof(usbCommunicator));
            IsConnected = false;
            _ui.LogMessage("DeviceConnection component initialized.", LogLevel.Info);
        }

        public void SetFdlCrcMode(bool useFdlCrc)
        {
            _useFdlCrc = useFdlCrc;
            _ui.LogMessage($"CRC mode set to: {(_useFdlCrc ? "FDL CRC" : "XMODEM CRC")}", LogLevel.Info);
        }

        public bool ConnectDevice()
        {
            _ui.LogMessage("Attempting to connect to Spreadtrum device (VID:0x1782, PID:0x4D00)...", LogLevel.Info);
            IsConnected = _usbCommunicator.Connect(0x1782, 0x4D00);
            if (IsConnected)
            {
                _ui.LogMessage("Device connected successfully.", LogLevel.Info);
            }
            else
            {
                _ui.LogMessage("Failed to connect to device.", LogLevel.Error);
            }
            return IsConnected;
        }

        public void DisconnectDevice()
        {
            if (IsConnected)
            {
                _usbCommunicator.Disconnect();
                IsConnected = false;
                _ui.LogMessage("Device disconnected.", LogLevel.Info);
            }
            else
            {
                _ui.LogMessage("No device was connected.", LogLevel.Warning);
            }
        }

        private static byte[] EscapeData(IEnumerable<byte> data)
        {
            var escaped = new List<byte>();
            foreach (byte b in data)
            {
                if (b == 0x7e)
                {
                    escaped.Add(0x7d);
                    escaped.Add(0x5e);
                }
                else if (b == 0x7d)
                {
                    escaped.Add(0x7d);
                    escaped.Add(0x5d);
                }
                else
                {
                    escaped.Add(b);
                }
            }
            return escaped.ToArray();
        }

        private static byte[] UnescapeData(IEnumerable<byte> data)
        {
            var unescaped = new List<byte>();
            bool isEscaped = false;
            foreach (byte b in data)
            {
                if (isEscaped)
                {
                    if (b == 0x5e)
                    {
                        unescaped.Add(0x7e);
                    }
                    else if (b == 0x5d)
                    {
                        unescaped.Add(0x7d);
                    }
                    else // Should not happen in valid data
                    {
                        unescaped.Add(0x7d); // Keep the escape char
                        unescaped.Add(b);    // Keep the unexpected byte
                    }
                    isEscaped = false;
                }
                else if (b == 0x7d)
                {
                    isEscaped = true;
                }
                else
                {
                    unescaped.Add(b);
                }
            }
            if (isEscaped) // Trailing escape character, invalid sequence
            {
                unescaped.Add(0x7d);
            }
            return unescaped.ToArray();
        }

        private bool SendPacket(byte[] payload)
        {
            if (!IsConnected)
            {
                _ui.LogMessage("SendPacket: Not connected to device.", LogLevel.Error);
                return false;
            }
            if (payload == null || payload.Length == 0)
            {
                 _ui.LogMessage("SendPacket: Payload is null or empty.", LogLevel.Error);
                return false;
            }

            ushort crc = _useFdlCrc ? CrcUtils.CalculateFdlCrc(payload) : CrcUtils.CalculateXmodemCrc(payload);
            byte[] crcBytes = new byte[2];
            crcBytes[0] = (byte)(crc >> 8);   // BigEndian
            crcBytes[1] = (byte)(crc & 0xFF); // BigEndian

            var packetWithCrc = new List<byte>(payload);
            packetWithCrc.AddRange(crcBytes);

            byte[] escapedPacket = EscapeData(packetWithCrc);
            
            var hdlcFrame = new List<byte>();
            hdlcFrame.Add(0x7e);
            hdlcFrame.AddRange(escapedPacket);
            hdlcFrame.Add(0x7e);

            byte[] frameBytes = hdlcFrame.ToArray();
            _ui.LogMessage($"Sending HDLC Frame: {BitConverter.ToString(frameBytes)}", LogLevel.Info);

            bool success = _usbCommunicator.Write(frameBytes);
            if (success)
            {
                _ui.LogMessage("Data sent successfully.", LogLevel.Info);
            }
            else
            {
                _ui.LogMessage("Failed to send data.", LogLevel.Error);
            }
            return success;
        }
        
        private byte[] ReceivePacket(int timeoutMilliseconds = 2000)
        {
            if (!IsConnected)
            {
                _ui.LogMessage("ReceivePacket: Not connected to device.", LogLevel.Error);
                return null;
            }

            byte[] rawData = _usbCommunicator.Read(timeoutMilliseconds);

            if (rawData == null || rawData.Length == 0)
            {
                _ui.LogMessage("ReceivePacket: No data received or timeout.", LogLevel.Warning);
                return null;
            }

            _ui.LogMessage($"Received HDLC Frame: {BitConverter.ToString(rawData)}", LogLevel.Info);

            if (rawData.Length < 2 || rawData[0] != 0x7e || rawData[rawData.Length - 1] != 0x7e)
            {
                _ui.LogMessage("ReceivePacket: Invalid HDLC frame (missing start/end markers or too short).", LogLevel.Error);
                return null;
            }

            // Extract content between 0x7e markers
            byte[] extractedContent = new byte[rawData.Length - 2];
            Array.Copy(rawData, 1, extractedContent, 0, rawData.Length - 2);

            byte[] unescapedContent = UnescapeData(extractedContent);

            if (unescapedContent.Length < 2) // Must be at least 2 bytes for CRC
            {
                _ui.LogMessage("ReceivePacket: Unescaped content too short for CRC.", LogLevel.Error);
                return null;
            }

            byte[] receivedPayload = unescapedContent.Take(unescapedContent.Length - 2).ToArray();
            ushort receivedCrc = (ushort)((unescapedContent[unescapedContent.Length - 2] << 8) | unescapedContent[unescapedContent.Length - 1]); // BigEndian

            ushort calculatedCrc = _useFdlCrc ? CrcUtils.CalculateFdlCrc(receivedPayload) : CrcUtils.CalculateXmodemCrc(receivedPayload);

            if (receivedCrc != calculatedCrc)
            {
                _ui.LogMessage($"ReceivePacket: CRC mismatch. Expected: {calculatedCrc:X4}, Received: {receivedCrc:X4}", LogLevel.Error);
                return null;
            }

            _ui.LogMessage("ReceivePacket: CRC check passed. Payload received successfully.", LogLevel.Info);
            return receivedPayload;
        }

        public byte[] ExecuteCommand(byte[] commandPayload, int timeout = 2000)
        {
            if (commandPayload == null || commandPayload.Length == 0)
            {
                _ui.LogMessage("ExecuteCommand: Command payload is null or empty.", LogLevel.Error);
                return null;
            }
            _ui.LogMessage($"ExecuteCommand: Sending payload: {BitConverter.ToString(commandPayload)}", LogLevel.Info);
            
            if (!SendPacket(commandPayload))
            {
                _ui.LogMessage("ExecuteCommand: Failed to send packet.", LogLevel.Error);
                return null;
            }

            byte[] responsePayload = ReceivePacket(timeout);

            if (responsePayload == null)
            {
                _ui.LogMessage("ExecuteCommand: Did not receive valid response packet.", LogLevel.Warning);
                return null;
            }
            
            _ui.LogMessage($"ExecuteCommand: Received response payload: {BitConverter.ToString(responsePayload)}", LogLevel.Info);
            return responsePayload;
        }
    }
}
