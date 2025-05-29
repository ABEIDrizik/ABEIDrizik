using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SprdFlashTool.Core
{
    // Chipset class is defined in Chipset.cs

    public class FlashProcess
    {
        private readonly IFlashUI _ui;
        private readonly DeviceConnection _deviceConnection;

        private const ushort BSL_REP_ACK = 0x80;
        private const ushort CMD_CONNECT = 0x0000;
        private const ushort CMD_START_DATA = 0x0001;
        private const ushort CMD_MIDST_DATA = 0x0002;
        private const ushort CMD_END_DATA = 0x0003;
        private const ushort CMD_EXEC_DATA = 0x0004;
        private const ushort CMD_CHANGE_BAUD = 0x0009;


        public FlashProcess(IFlashUI ui, DeviceConnection deviceConnection)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _deviceConnection = deviceConnection ?? throw new ArgumentNullException(nameof(deviceConnection));
            _ui.LogMessage("FlashProcess component initialized.", LogLevel.Info);
        }
        
        // BigEndian Helpers (inline for now as requested)
        private byte[] UintToBytesBE(uint value) => new byte[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };
        private byte[] UlongToBytesBE(ulong value) => new byte[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value }; // For 32-bit address
        private byte[] UshortToBytesBE(ushort value) => new byte[] { (byte)(value >> 8), (byte)value };


        private async Task<bool> SendCommandAndVerifyAck(byte[] commandPacket, string commandName)
        {
            byte[] response = await Task.Run(() => _deviceConnection.ExecuteCommand(commandPacket)); // Run ExecuteCommand on a background thread

            if (response == null || response.Length < 2) // Min 2 bytes for response code
            {
                _ui.ReportError($"No valid response received for {commandName}.", $"Expected ACK (0x{BSL_REP_ACK:X2}), got null or too short response.");
                return false;
            }

            ushort responseCode = (ushort)((response[0] << 8) | response[1]); // BigEndian

            if (responseCode != BSL_REP_ACK)
            {
                _ui.ReportError($"Error response received for {commandName}.", $"Expected ACK (0x{BSL_REP_ACK:X2}), got 0x{responseCode:X4}. Full response: {BitConverter.ToString(response)}");
                return false;
            }

            _ui.LogMessage($"{commandName} acknowledged by device.", LogLevel.Info);
            return true;
        }

        private async Task<bool> PerformHandshakeAsync(bool isFdlHandshake)
        {
            _ui.LogMessage($"Performing handshake (FDL mode: {isFdlHandshake})...", LogLevel.Info);
            _deviceConnection.SetFdlCrcMode(isFdlHandshake);

            byte[] syncCommand = { 0x7e }; // This is just the HDLC frame delimiter, actual sync command is usually 0x00
                                           // The DeviceConnection.ExecuteCommand should handle the actual command structure.
                                           // For BootROM handshake, the first command is often just sending 0x7E and expecting 0x7E back,
                                           // or sending a specific sync byte like 0x55.
                                           // Here, we assume ExecuteCommand sends the 0x7E, and awaits a proper response.
                                           // A more robust handshake might involve sending a specific SYNC command payload.
                                           // For now, let's send a "check connection" or "sync" type of command.
                                           // A common initial command is a single 0xCE or similar, or just expecting device to respond to 0x7E.
                                           // Let's assume the protocol expects a specific "sync" or "get version" command.
                                           // The provided Python code implies sending 0x7E and expecting 0x7E.
                                           // The ExecuteCommand already wraps data in 0x7E. So we send an empty payload for sync.
                                           // Or, if the very first byte sent should be 0x7E with no content:
                                           // This is tricky, as ExecuteCommand expects a payload.
                                           // For a simple sync, we might need a more direct write/read from DeviceConnection.
                                           // Let's assume for now ExecuteCommand with an empty payload is a "ping"
                                           // and the device responds with its identifier.
                                           // A typical Spreadtrum sync command is {0x00, 0x00, 0x00, 0x00} (CMD_CONNECT) but that's after initial sync.
                                           // The initial sync is just sending 0x7E and receiving 0x7E or a version string.
                                           // Given the context, let's assume we are past the raw 0x7E detection and are sending a "get version" like command
                                           // which is not specified, so I will use a generic empty command to solicit a response.
                                           // If the device just sends back 0x7E (ack) as a response to just 0x7E, this needs adjustment in DeviceConnection or a raw write.
                                           // For now, let's assume ExecuteCommand with a "version" command (not specified, using a placeholder command type 0xFE)
            
            byte[] getVersionCmd = { 0x00, 0xFE, 0x00, 0x00 }; // Placeholder for a "get version" command.
                                                               // Or, if it's just sending 0x7E and expecting 0x7E, that's handled by ExecuteCommand's framing.
                                                               // Let's simplify: send a specific connect command for handshake.
            byte[] handshakeConnectCmd = { (byte)(CMD_CONNECT >> 8), (byte)CMD_CONNECT, 0x00, 0x00 }; // CMD_CONNECT, length 0

            byte[] response = await Task.Run(() => _deviceConnection.ExecuteCommand(handshakeConnectCmd, timeout: 5000));


            if (response == null || response.Length < 2) // code + length
            {
                _ui.ReportError("Handshake failed: No valid response from device.", "Device did not reply to initial handshake command.");
                return false;
            }

            ushort responseCode = (ushort)((response[0] << 8) | response[1]);
            string identifier = string.Empty;
            if (response.Length > 4) // Assuming code (2b) + length (2b) + data
            {
                ushort dataLength = (ushort)((response[2] << 8) | response[3]);
                if (response.Length >= 4 + dataLength)
                {
                    identifier = Encoding.ASCII.GetString(response, 4, dataLength);
                }
            }


            if (responseCode == BSL_REP_ACK) // BSL_REP_ACK (0x80) often precedes version string in some protocols
            {
                 // If BSL_REP_ACK is the *only* thing, and version comes after a *different* command, this logic needs adjustment.
                 // For now, assume ACK means the initial connect for handshake is good.
                _ui.LogMessage($"Handshake successful. Device ACKed. Identifier (if any): '{identifier}'", LogLevel.Info);
                // Specific identifier checks:
                if (isFdlHandshake && !identifier.Contains("Spreadtrum Boot Block") && !identifier.Contains("SPRD UBoot")) // Example FDL identifiers
                {
                    _ui.LogMessage($"Warning: FDL Handshake, but identifier is '{identifier}', expected FDL bootloader signature.", LogLevel.Warning);
                }
                else if (!isFdlHandshake && identifier != "SPRD3" && !string.IsNullOrEmpty(identifier)) // Example BootROM identifier
                {
                     _ui.LogMessage($"Warning: BootROM Handshake, but identifier is '{identifier}', expected 'SPRD3' or similar.", LogLevel.Warning);
                }
                // After initial sync/version, send actual CMD_CONNECT
                // This seems redundant if handshakeConnectCmd already *is* CMD_CONNECT. Let's assume it is.
                // return await SendCommandAndVerifyAck(handshakeConnectCmd, "CMD_CONNECT (Handshake)");
                return true; // Handshake was successful
            }
            else
            {
                _ui.ReportError("Handshake failed: Unexpected response from device.", $"Received code 0x{responseCode:X4}, expected 0x{BSL_REP_ACK:X4}. Identifier: '{identifier}'");
                return false;
            }
        }
        
        private async Task<bool> LoadFdlAsync(string fdlPath, ulong fdlAddress)
        {
            if (!File.Exists(fdlPath))
            {
                _ui.ReportError($"FDL file not found: {fdlPath}", null);
                return false;
            }
            _ui.LogMessage($"Loading FDL from: {fdlPath} to address 0x{fdlAddress:X8}", LogLevel.Info);

            byte[] fdlData = File.ReadAllBytes(fdlPath);
            uint fdlLength = (uint)fdlData.Length;

            // CMD_START_DATA (0x01)
            var startCmdPayload = new MemoryStream();
            startCmdPayload.Write(UshortToBytesBE(CMD_START_DATA), 0, 2);
            startCmdPayload.Write(UshortToBytesBE(8), 0, 2); // Length of params (4 bytes addr + 4 bytes len)
            startCmdPayload.Write(UlongToBytesBE(fdlAddress), 0, 4); // Using only lower 4 bytes of ulong for 32-bit address
            startCmdPayload.Write(UintToBytesBE(fdlLength), 0, 4);
            if (!await SendCommandAndVerifyAck(startCmdPayload.ToArray(), "CMD_START_DATA")) return false;

            _ui.LogMessage("CMD_START_DATA acknowledged. Starting FDL data transfer...", LogLevel.Info);

            // CMD_MIDST_DATA (0x02)
            int chunkSize = 1024; // Common chunk size
            int bytesSent = 0;
            while (bytesSent < fdlLength)
            {
                int currentChunkSize = Math.Min(chunkSize, (int)(fdlLength - bytesSent));
                byte[] chunk = new byte[currentChunkSize];
                Array.Copy(fdlData, bytesSent, chunk, 0, currentChunkSize);

                var midstCmdPayload = new MemoryStream();
                midstCmdPayload.Write(UshortToBytesBE(CMD_MIDST_DATA), 0, 2);
                midstCmdPayload.Write(UshortToBytesBE((ushort)chunk.Length), 0, 2); // Length of this chunk
                midstCmdPayload.Write(chunk, 0, chunk.Length);
                
                if (!await SendCommandAndVerifyAck(midstCmdPayload.ToArray(), $"CMD_MIDST_DATA (Bytes {bytesSent}-{bytesSent + currentChunkSize -1})")) return false;
                
                bytesSent += currentChunkSize;
                _ui.UpdateProgress((int)((double)bytesSent / fdlLength * 100));
            }
            _ui.LogMessage("All FDL data chunks sent and acknowledged.", LogLevel.Info);

            // CMD_END_DATA (0x03)
            byte[] endCmdPacket = { (byte)(CMD_END_DATA >> 8), (byte)CMD_END_DATA, 0x00, 0x00 }; // Length 0
            if (!await SendCommandAndVerifyAck(endCmdPacket, "CMD_END_DATA")) return false;
            
            _ui.LogMessage("CMD_END_DATA acknowledged. FDL loading complete.", LogLevel.Info);
            return true;
        }

        private async Task<bool> ExecuteFdlAsync(ulong fdlAddress)
        {
            _ui.LogMessage($"Requesting execution of FDL at address 0x{fdlAddress:X8}...", LogLevel.Info);
            var execCmdPayload = new MemoryStream();
            execCmdPayload.Write(UshortToBytesBE(CMD_EXEC_DATA), 0, 2);
            execCmdPayload.Write(UshortToBytesBE(4), 0, 2); // Length of params (4 bytes addr)
            execCmdPayload.Write(UlongToBytesBE(fdlAddress), 0, 4); // Using only lower 4 bytes of ulong
            
            return await SendCommandAndVerifyAck(execCmdPayload.ToArray(), "CMD_EXEC_DATA");
        }

        public async Task StartFlashAsync(string firmwarePath, Chipset selectedChipset) // firmwarePath not used yet
        {
            _ui.SetBusyState(true);
            _ui.ClearProgress();
            _ui.LogMessage("Starting flash process...", LogLevel.Info);

            try
            {
                if (selectedChipset == null)
                {
                    _ui.ReportError("No chipset selected.", "Chipset configuration is required to start flashing.");
                    return;
                }
                
                _ui.LogMessage($"Selected Chipset: {selectedChipset.Name}", LogLevel.Info);

                if (!_deviceConnection.ConnectDevice())
                {
                    _ui.ReportError("Failed to connect to the device.", "Please check device connection and drivers.");
                    return;
                }

                // BootROM Handshake
                _ui.LogMessage("Attempting BootROM handshake...", LogLevel.Info);
                if (!await PerformHandshakeAsync(false)) // false for BootROM handshake
                {
                    _ui.ReportError("BootROM handshake failed.", null);
                    return;
                }
                _ui.LogMessage("BootROM handshake successful.", LogLevel.Info);
                _ui.UpdateProgress(5);

                // Load FDL1
                _ui.LogMessage("Loading FDL1...", LogLevel.Info);
                if (string.IsNullOrEmpty(selectedChipset.Fdl1Path) || selectedChipset.Fdl1Address == 0)
                {
                     _ui.ReportError("FDL1 path or address not configured for selected chipset.", null);
                     return;
                }
                if (!await LoadFdlAsync(selectedChipset.Fdl1Path, selectedChipset.Fdl1Address)) 
                {
                    _ui.ReportError("Failed to load FDL1.", null);
                    return;
                }
                _ui.UpdateProgress(25);
                _ui.LogMessage("FDL1 loaded successfully. Executing FDL1...", LogLevel.Info);
                if (!await ExecuteFdlAsync(selectedChipset.Fdl1Address))
                {
                    _ui.ReportError("Failed to execute FDL1.", null);
                    return;
                }
                _ui.LogMessage("FDL1 executed. Reconnecting for FDL1 environment...", LogLevel.Info);
                _ui.UpdateProgress(30);

                // Reconnect for FDL1
                _deviceConnection.DisconnectDevice();
                _ui.LogMessage("Device disconnected. Waiting for FDL1 to initialize...", LogLevel.Info);
                await Task.Delay(1500); // Give device time to re-enumerate with FDL1
                if (!_deviceConnection.ConnectDevice())
                {
                    _ui.ReportError("Failed to reconnect to device after FDL1 execution.", "FDL1 might not have started correctly.");
                    return;
                }
                _ui.LogMessage("Reconnected to device (hopefully in FDL1 mode).", LogLevel.Info);
                _ui.UpdateProgress(35);

                // FDL1 Handshake
                _ui.LogMessage("Attempting FDL1 handshake...", LogLevel.Info);
                if (!await PerformHandshakeAsync(true)) // true for FDL handshake
                {
                    _ui.ReportError("FDL1 handshake failed.", null);
                    return;
                }
                _ui.LogMessage("FDL1 handshake successful.", LogLevel.Info);
                _ui.UpdateProgress(40);

                // Load FDL2
                _ui.LogMessage("Loading FDL2...", LogLevel.Info);
                 if (string.IsNullOrEmpty(selectedChipset.Fdl2Path) || selectedChipset.Fdl2Address == 0)
                {
                     _ui.LogMessage("FDL2 path or address not configured, skipping FDL2.", LogLevel.Warning);
                }
                else
                {
                    if (!await LoadFdlAsync(selectedChipset.Fdl2Path, selectedChipset.Fdl2Address))
                    {
                        _ui.ReportError("Failed to load FDL2.", null);
                        return;
                    }
                    _ui.UpdateProgress(65);
                    _ui.LogMessage("FDL2 loaded successfully. Executing FDL2...", LogLevel.Info);
                    if (!await ExecuteFdlAsync(selectedChipset.Fdl2Address))
                    {
                        _ui.ReportError("Failed to execute FDL2.", null);
                        return;
                    }
                    _ui.LogMessage("FDL2 loaded and executed.", LogLevel.Info);
                }
                _ui.UpdateProgress(70);
                
                // Verify FDL2 (CMD_CHANGE_BAUD) - or just FDL if FDL2 is skipped
                _ui.LogMessage("Attempting to set baud rate with FDL...", LogLevel.Info);
                var changeBaudPayload = new MemoryStream();
                changeBaudPayload.Write(UshortToBytesBE(CMD_CHANGE_BAUD), 0, 2);
                changeBaudPayload.Write(UshortToBytesBE(4), 0, 2); // Length of params (4 bytes baud)
                changeBaudPayload.Write(UintToBytesBE((uint)selectedChipset.BaudRate), 0, 4);
                
                if (!await SendCommandAndVerifyAck(changeBaudPayload.ToArray(), "CMD_CHANGE_BAUD"))
                {
                    _ui.ReportError("Failed to set baud rate with FDL.", "This might be non-critical for some FDLs.");
                    // Don't necessarily bail, as requested
                }
                else
                {
                    _ui.LogMessage($"Baud rate set to {selectedChipset.BaudRate} successfully.", LogLevel.Info);
                }
                _ui.UpdateProgress(75);

                _ui.LogMessage("Flash process initialized (FDLs loaded). Ready for further operations (e.g., flashing PAC).", LogLevel.Info);
                _ui.UpdateProgress(100); // Placeholder for now
            }
            catch (Exception ex)
            {
                _ui.ReportError("An unexpected error occurred during the flash process.", ex.ToString());
                _ui.LogMessage($"Flash process failed: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                _ui.SetBusyState(false);
                if (_deviceConnection.IsConnected)
                {
                    _deviceConnection.DisconnectDevice();
                }
                _ui.LogMessage("Flash process finished.", LogLevel.Info);
            }
            // Return Task.CompletedTask; // For async void-like behavior
        }

        /// <summary>
        /// Stops the ongoing flashing process.
        /// </summary>
        public void StopFlash() // This needs to be made async and use a CancellationToken
        {
            _ui.LogMessage("StopFlash method called. Attempting to stop the flash process...", LogLevel.Info);
            // TODO: Implement logic to gracefully stop the flashing process
            // This might involve:
            // 1. Signaling a CancellationToken to all async operations within StartFlashAsync
            // 2. Releasing any resources held by the flashing process
            // 3. Communicating with the device to halt operations (e.g., sending CMD_END_PROCESS)
            _ui.LogMessage("Flash process stop requested. (Actual stop logic to be implemented with CancellationToken)", LogLevel.Info);
            
            // For now, just try to disconnect and reset UI state
            if (_deviceConnection.IsConnected)
            {
                _deviceConnection.DisconnectDevice();
            }
            _ui.ClearProgress();
            _ui.SetBusyState(false); 
        }
    }
}
