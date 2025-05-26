using System;
using System.Collections.Generic;
using System.IO; // For Path.GetFileName
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Forms;
using System.Management;

namespace MTKDeviceManager
{
    public class AuthDeviceHandler
    {
        private readonly IProgress<string> _logger;
        private readonly IProgress<int> _progressBar;
        private string _daFilePath;

        public AuthDeviceHandler(IProgress<string> logger, IProgress<int> progressBar)
        {
            _logger = logger;
            _progressBar = progressBar;
        }

        public void SetDAFile(string daFilePath)
        {
            if (string.IsNullOrWhiteSpace(daFilePath) || !File.Exists(daFilePath))
            {
                _logger.Report($"❌ Invalid DA file: {daFilePath}\n");
                throw new ArgumentException("DA file path is invalid or file does not exist.", nameof(daFilePath));
            }

            _daFilePath = daFilePath;
            _logger.Report($"📂 DA file set: {Path.GetFileName(_daFilePath)}\n");
        }

        public async Task<bool> ProcessDeviceAsync(CancellationToken cancellationToken = default)
        {
            _logger.Report("🚀 Starting device processing...\n");
            _progressBar.Report(0);

            var deviceDetectionProgress = new Progress<string>(msg => _logger.Report($"[Detector] {msg}"));
            string port = await MediatekDeviceDetector.DetectMediatekPreloaderAsync(deviceDetectionProgress, 30);

            if (string.IsNullOrEmpty(port))
            {
                _logger.Report("❌ Device detection failed.\n");
                _progressBar.Report(0);
                return false;
            }

            _logger.Report($"✅ Device detected on: {port}\n");
            _progressBar.Report(10);

            var mtk = new MTKPreloaderDevice(port);
            Stream stream = mtk.OpenSerialStream();

            if (stream == null)
            {
                _logger.Report("❌ Failed to open communication stream.\n");
                return false;
            }

            // ✅ Use correct handshake
            if (!await HandshakeAsync(stream, cancellationToken)) return false;

            _progressBar.Report(20);

            // ✅ Now upload DA
            if (!await UploadDAAsync(stream, cancellationToken)) return false;

            _progressBar.Report(60);

            // ✅ Then get device info
            if (!await GetDeviceInfoAsync(stream, cancellationToken)) return false;

            _progressBar.Report(100);
            _logger.Report("🎉 Device info retrieved successfully.\n");

            return true;
        }


        private async Task<bool> HandshakeAsync(Stream stream, CancellationToken ct)
        {
            _logger.Report("🤝 MTK BootROM Handshake...\n");

            byte[] recvBuf = new byte[1];

            // Step 1: Send 0xA0
            await stream.WriteAsync(new byte[] { 0xA0 }, 0, 1, ct);
            _logger.Report("📡 Waiting Response...\n");

            // Wait for response
            int read = await stream.ReadAsync(recvBuf, 0, 1, ct);
            if (read != 1 || recvBuf[0] != 0x5F)
            {
                _logger.Report($"❌ Unexpected error: 0x{recvBuf[0]:X2}\n");
                return false;
            }
            _logger.Report("📥 Step 1 - Syncrosing Device.\n");

            // Step 2: Send 0x0A
            await stream.WriteAsync(new byte[] { 0x0A }, 0, 1, ct);
            _logger.Report("📡 Waiting Device Response..\n");

            // Wait for second response
            read = await stream.ReadAsync(recvBuf, 0, 1, ct);
            if (read != 1 || recvBuf[0] != 0xF5)
            {
                _logger.Report($"❌ An error occoured 0x{recvBuf[0]:X2}\n");
                return false;
            }
            _logger.Report("📥 Syncrosing Device...\n");

            _logger.Report("✅ MTK BootROM handshake successful!\n");
            var mtkdetection = new MTKChipDetector(stream, _logger.Report, ct);
            var result = await mtkdetection.DetectChipAdvancedAsync();

            _logger.Report($"Chip Detected: {result.ChipName}\n");
            _logger.Report($"HWID: 0x{result.HwCode:X4}\n");
            if (!string.IsNullOrEmpty(result.Notes))
                _logger.Report($"Notes: {result.Notes}\n");

            return true;
        }
         
        private async Task<bool> UploadDAAsync(Stream stream, CancellationToken ct)
        {
            _logger.Report("⬆️ Starting DA upload process...\n");

            if (string.IsNullOrEmpty(_daFilePath))
            {
                _logger.Report("⚠️ No DA file set. Skipping upload.\n");
                return false;
            }

            if (!File.Exists(_daFilePath))
            {
                _logger.Report($"❌ DA file does not exist: {_daFilePath}\n");
                return false;
            }

            try
            {
                // Load DA file
                byte[] daBytes = await Task.Run(() => File.ReadAllBytes(_daFilePath), ct);
                int daLength = daBytes.Length;

                _logger.Report($"📂 Loaded DA file: {_daFilePath} ({daLength} bytes)\n");

                if (daLength < 0x100)
                {
                    _logger.Report("❌ DA file is too small to be valid.\n");
                    return false;
                }

                // Step 1: SYNC (0xA0) - Expect 0x5F or tolerate 0xA1
                const int maxSyncRetries = 3;
                bool syncSuccess = false;
                byte[] buffer = new byte[1];

                for (int i = 0; i < maxSyncRetries; i++)
                {
                    _logger.Report($"📡 Sending SYNC command (0xA0) [Attempt {i + 1}]...\n");
                    await stream.WriteAsync(new byte[] { 0xA0 }, 0, 1, ct);
                    await Task.Delay(100, ct);

                    int read = await stream.ReadAsync(buffer, 0, 1, ct);
                    if (read != 1)
                    {
                        _logger.Report("❌ No response to SYNC command.\n");
                        continue;
                    }

                    string resp = $"0x{buffer[0]:X2}";
                    _logger.Report($"📥 SYNC Response: {resp}\n");

                    if (buffer[0] == 0x5F)
                    {
                        _logger.Report("📶 SYNC OK\n");
                        syncSuccess = true;
                        break;
                    }
                    else if (buffer[0] == 0xA1)
                    {
                        _logger.Report("⚠️ Device already in DA mode. Skipping DA upload.\n");
                        return true;
                    }
                    else
                    {
                        _logger.Report($"❌ Unexpected SYNC response: {resp}. Retrying...\n");
                        await Task.Delay(300, ct);
                    }
                }

                if (!syncSuccess)
                {
                    _logger.Report("❌ Failed to establish SYNC with BootROM.\n");
                    return false;
                }

                // Step 2: Send DA Header (Address + Size)
                uint daAddress = 0x201000; // Standard MTK load address
                uint daSize = (uint)daLength;

                _logger.Report($"📤 Sending DA Header:\n   Address: 0x{daAddress:X8}\n   Size: {daSize} bytes\n");

                byte[] addrBytes = BitConverter.GetBytes(daAddress);
                byte[] sizeBytes = BitConverter.GetBytes(daSize);

                if (BitConverter.IsLittleEndian == false)
                {
                    Array.Reverse(addrBytes);
                    Array.Reverse(sizeBytes);
                }

                await stream.WriteAsync(addrBytes, 0, 4, ct);
                await stream.WriteAsync(sizeBytes, 0, 4, ct);
                stream.Flush();
                await Task.Delay(100, ct);

                // Step 3: Wait for DA ACK (0xA1)
                _logger.Report("⏳ Waiting for DA ACK (0xA1)...\n");
                int ackRead = await stream.ReadAsync(buffer, 0, 1, ct);
                if (ackRead != 1)
                {
                    _logger.Report("❌ No response from device after DA header.\n");
                    return false;
                }

                string ackResp = $"0x{buffer[0]:X2}";
                _logger.Report($"📥 DA ACK Response: {ackResp}\n");

                if (buffer[0] != 0xA1)
                {
                    _logger.Report($"❌ Unexpected DA ACK response: {ackResp}\n");
                    return false;
                }

                _logger.Report("✅ DA acknowledged by BootROM.\n");

                // Step 4: Upload DA Payload in Chunks
                int chunkSize = 1024;
                int totalSent = 0;

                _logger.Report($"📦 Uploading DA payload ({daLength} bytes)...\n");

                while (totalSent < daLength)
                {
                    ct.ThrowIfCancellationRequested();

                    int remaining = daLength - totalSent;
                    int sendNow = Math.Min(chunkSize, remaining);

                    await stream.WriteAsync(daBytes, totalSent, sendNow, ct);
                    totalSent += sendNow;

                    int progress = 20 + (int)((double)totalSent / daLength * 40);
                    _progressBar.Report(progress);

                    _logger.Report($"📊 Sent {totalSent}/{daLength} bytes\n");

                    await Task.Delay(1, ct); // Small delay for stability
                }

                _logger.Report("📥 DA payload sent. Waiting for execution response...\n");

                // Step 5: Wait for Execution Result (0xE0 or 0xC0)
                int execRead = await stream.ReadAsync(buffer, 0, 1, ct);
                if (execRead != 1)
                {
                    _logger.Report("❌ No execution response from device.\n");
                    return false;
                }

                string execResp = $"0x{buffer[0]:X2}";
                _logger.Report($"📥 Execution Response: {execResp}\n");

                if (buffer[0] == 0xE0 || buffer[0] == 0xC0)
                {
                    _logger.Report($"✅ DA executed successfully! Response: {execResp}\n");
                    return true;
                }
                else
                {
                    _logger.Report($"❌ Unexpected execution response: {execResp}\n");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Report("🚫 DA upload was cancelled.\n");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Report($"❌ Exception during DA upload: {ex.Message}\n");
                return false;
            }
        }

        private async Task<bool> GetDeviceInfoAsync(Stream stream, CancellationToken ct)
        {
            _logger.Report("🔍 Reading device info...\n");

            var commandSets = GetCommandMap();
            Dictionary<string, string> results = new();

            foreach (var cmdSet in commandSets)
            {
                string vendor = cmdSet.Key;
                _logger.Report($"🔄 Trying vendor command set: {vendor}");

                foreach (var kv in cmdSet.Value)
                {
                    string key = kv.Key;
                    byte[] command = kv.Value;

                    try
                    {
                        await stream.WriteAsync(command, 0, command.Length, ct);

                        byte[] buffer = new byte[512];
                        int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                        if (read > 0)
                        {
                            string value = Encoding.ASCII.GetString(buffer, 0, read).Trim('\0', '\r', '\n');
                            results[key] = value;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Report($"⚠️ Failed to get {key}: {ex.Message}");
                    }
                }

                if (results.Count >= 4) break; // Acceptable result
            }

            if (results.Count == 0)
            {
                _logger.Report("❌ Failed to get device info.\n");
                return false;
            }

            // Show results
            foreach (var kv in results)
            {
                _logger.Report($"🔹 {kv.Key}: {kv.Value}");
            }

            return true;
        }

        private Dictionary<string, Dictionary<string, byte[]>> GetCommandMap()
        {
            return new Dictionary<string, Dictionary<string, byte[]>>
            {
                ["STANDARD"] = new Dictionary<string, byte[]>
                {
                    ["GET_BRAND"] = new byte[] { 0xA0, 0x0A, 0x02, 0x00 },
                    ["GET_MODEL"] = new byte[] { 0xA0, 0x0A, 0x02, 0x01 },
                    ["GET_ANDROID_VER"] = new byte[] { 0xA0, 0x0A, 0x02, 0x02 },
                    ["GET_BUILD"] = new byte[] { 0xA0, 0x0A, 0x02, 0x03 },
                    ["GET_SERIAL"] = new byte[] { 0xA0, 0x0A, 0x02, 0x04 },
                    ["GET_HARDWARE"] = new byte[] { 0xA0, 0x0A, 0x02, 0x05 }
                },
                ["SAMSUNG"] = new Dictionary<string, byte[]>
                {
                    ["GET_BRAND"] = new byte[] { 0xB0, 0x0B, 0x01, 0x00 },
                    ["GET_MODEL"] = new byte[] { 0xB0, 0x0B, 0x01, 0x01 },
                    ["GET_ANDROID_VER"] = new byte[] { 0xB0, 0x0B, 0x01, 0x02 },
                    ["GET_BUILD"] = new byte[] { 0xB0, 0x0B, 0x01, 0x03 },
                    ["GET_SERIAL"] = new byte[] { 0xB0, 0x0B, 0x01, 0x04 },
                    ["GET_HARDWARE"] = new byte[] { 0xB0, 0x0B, 0x01, 0x05 }
                },
                ["TECNO"] = new Dictionary<string, byte[]>
                {
                    ["GET_BRAND"] = new byte[] { 0xC0, 0x0C, 0x01, 0x00 },
                    ["GET_MODEL"] = new byte[] { 0xC0, 0x0C, 0x01, 0x01 },
                    ["GET_ANDROID_VER"] = new byte[] { 0xC0, 0x0C, 0x01, 0x02 },
                    ["GET_BUILD"] = new byte[] { 0xC0, 0x0C, 0x01, 0x03 },
                    ["GET_SERIAL"] = new byte[] { 0xC0, 0x0C, 0x01, 0x04 },
                    ["GET_HARDWARE"] = new byte[] { 0xC0, 0x0C, 0x01, 0x05 }
                },
                ["VIVO"] = new Dictionary<string, byte[]>
                {
                    ["GET_BRAND"] = new byte[] { 0xD0, 0x0D, 0x01, 0x00 },
                    ["GET_MODEL"] = new byte[] { 0xD0, 0x0D, 0x01, 0x01 },
                    ["GET_ANDROID_VER"] = new byte[] { 0xD0, 0x0D, 0x01, 0x02 },
                    ["GET_BUILD"] = new byte[] { 0xD0, 0x0D, 0x01, 0x03 },
                    ["GET_SERIAL"] = new byte[] { 0xD0, 0x0D, 0x01, 0x04 },
                    ["GET_HARDWARE"] = new byte[] { 0xD0, 0x0D, 0x01, 0x05 }
                }
            };
        }
    }

}
