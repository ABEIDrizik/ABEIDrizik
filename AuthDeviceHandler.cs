using System;
using System.Collections.Generic;
using System.IO; // For Path.GetFileName
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace MTKDeviceManager
{
    /// <summary>
    /// Handles the authentication and device interaction process for Mediatek devices.
    /// This includes device detection, handshake, DA upload, and information retrieval.
    /// </summary>
    public class AuthDeviceHandler
    {
        private readonly IProgress<string> _logger;
        private readonly IProgress<int> _progressBar;
        private string _daFilePath;

        private const int DefaultHandshakeTimeoutMs = 5000; // Timeout for individual handshake steps
        private const int DefaultDetectionTimeoutSeconds = 30;
        private const int DefaultDaUploadChunkSize = 1024;
        private const uint DefaultDaLoadAddress = 0x201000; // Common DA load address

        // DA Compatibility Rules
        // This dictionary defines keywords expected in DA filenames for specific chip names or series.
        // It uses OrdinalIgnoreCase for case-insensitive comparisons.
        private static readonly Dictionary<string, List<string>> DaCompatibilityRules = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            // Chip Name (or part of it) -> List of keywords expected in compatible DA filenames
            { "MT6580", new List<string> { "DA_SWSEC_MT6580", "MTK_AllInOne_DA_MT6580", "_MT6580_"} },
            { "MT6735", new List<string> { "DA_SWSEC_MT6735", "MTK_AllInOne_DA_MT6735", "_MT6735_" } },
            { "MT6737", new List<string> { "DA_SWSEC_MT6737", "MTK_AllInOne_DA_MT6737", "_MT6737_" } },
            { "MT6739", new List<string> { "DA_SWSEC_MT6739", "MTK_AllInOne_DA_MT6739", "_MT6739_" } },
            { "MT6753", new List<string> { "DA_SWSEC_MT6753", "MTK_AllInOne_DA_MT6753", "_MT6753_" } },
            { "MT6761", new List<string> { "DA_SWSEC_MT6761", "MTK_AllInOne_DA_MT6761", "Helio_A22", "_MT6761_" } },
            { "MT6762", new List<string> { "DA_SWSEC_MT6762", "MTK_AllInOne_DA_MT6762", "Helio_P22", "_MT6762_" } },
            { "MT6763", new List<string> { "DA_SWSEC_MT6763", "MTK_AllInOne_DA_MT6763", "_MT6763_" } },
            { "MT6765", new List<string> { "DA_SWSEC_MT6765", "MTK_AllInOne_DA_MT6765", "Helio_P35", "_MT6765_" } },
            { "MT6768", new List<string> { "MTK_AllInOne_DA_G80", "DA_SWSEC_G80", "Helio_G80", "Helio_G85", "_MT6768_" } },
            { "MT6769V", new List<string> { "MTK_AllInOne_DA_G70", "DA_SWSEC_G70", "Helio_G70", "_MT6769_" } },
            { "Helio G70", new List<string> { "MTK_AllInOne_DA_G70", "DA_SWSEC_G70", "Helio_G70", "_MT6769_" } },
            { "MT6771", new List<string> { "DA_SWSEC_MT6771", "MTK_AllInOne_DA_MT6771", "Helio_P60", "Helio_P70", "_MT6771_" } },
            { "MT6785", new List<string> { "MTK_AllInOne_DA_G90", "DA_SWSEC_G90", "Helio_G90", "_MT6785_" } },
            { "MT6789", new List<string> { "DA_SWSEC_MT6789", "MTK_AllInOne_DA_G99", "Helio_G99", "_MT6789_" } },
            { "MT6833", new List<string> { "DA_SWSEC_MT6833", "MTK_AllInOne_DA_Dimensity_700", "Dimensity_700", "_MT6833_" } },
            { "MT6853", new List<string> { "DA_SWSEC_MT6853", "MTK_AllInOne_DA_Dimensity_800U", "Dimensity_720", "Dimensity_800U", "_MT6853_" } },
            { "MT6877", new List<string> { "DA_SWSEC_MT6877", "MTK_AllInOne_DA_Dimensity_1200", "Dimensity_1200", "Dimensity_1100", "_MT6877_" } },
            { "MT6893", new List<string> { "DA_SWSEC_MT6893", "MTK_AllInOne_DA_Dimensity_1200_MT6893", "Dimensity_1200", "_MT6893_" } },
            { "Dimensity", new List<string> { "MTK_AllInOne_DA_Dimensity", "DA_SWSEC_Dimensity", "_Dimensity_", "MT68" } },
            { "Helio", new List<string> { "MTK_AllInOne_DA_Helio", "DA_SWSEC_Helio", "_Helio_", "MT67" } }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthDeviceHandler"/> class.
        /// </summary>
        /// <param name="logger">An IProgress interface to report log messages.</param>
        /// <param name="progressBar">An IProgress interface to report progress updates.</param>
        public AuthDeviceHandler(IProgress<string> logger, IProgress<int> progressBar)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _progressBar = progressBar ?? throw new ArgumentNullException(nameof(progressBar));
        }

        /// <summary>
        /// Sets the Download Agent (DA) file path to be used for operations.
        /// </summary>
        /// <param name="daFilePath">The full path to the DA file.</param>
        /// <exception cref="ArgumentException">Thrown if the DA file path is invalid or the file does not exist.</exception>
        public void SetDAFile(string daFilePath)
        {
            if (string.IsNullOrWhiteSpace(daFilePath) || !File.Exists(daFilePath))
            {
                _logger.Report($"❌ Error: Invalid DA file path or file does not exist: '{daFilePath}'.\n");
                throw new ArgumentException($"DA file path is invalid or file does not exist: '{daFilePath}'.", nameof(daFilePath));
            }
            _daFilePath = daFilePath;
            _logger.Report($"📂 DA file set: {Path.GetFileName(_daFilePath)}\n");
        }

        /// <summary>
        /// Checks if the selected Download Agent (DA) file is compatible with the detected chip.
        /// </summary>
        /// <param name="detectedChipName">The name of the detected chip.</param>
        /// <param name="detectedHwCode">The hardware code of the detected chip.</param>
        /// <param name="daFilePath">The path to the DA file being checked.</param>
        /// <returns>True if the DA is deemed compatible or if compatibility cannot be determined; false if a known incompatibility is found.</returns>
        public bool IsDACompatible(string detectedChipName, ushort detectedHwCode, string daFilePath)
        {
            _logger.Report("🔎 Performing DA compatibility check...\n");
            if (string.IsNullOrWhiteSpace(daFilePath))
            {
                _logger.Report("ℹ️ No DA file selected. Skipping compatibility check.\n");
                return true; 
            }

            string daFileName = Path.GetFileName(daFilePath);
            if (string.IsNullOrWhiteSpace(detectedChipName) || detectedChipName.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Report($"⚠️ Could not determine a specific chip name (detected: '{detectedChipName}', HW Code: 0x{detectedHwCode:X4}). DA compatibility check will be less reliable.\n");
            }
            else
            {
                _logger.Report($"ℹ️ Checking DA '{daFileName}' for chip '{detectedChipName}' (HW Code: 0x{detectedHwCode:X4}).\n");
            }

            foreach (var rule in DaCompatibilityRules)
            {
                if (detectedChipName.IndexOf(rule.Key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (!string.IsNullOrWhiteSpace(detectedChipName) && rule.Key.IndexOf(detectedChipName, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    foreach (string keyword in rule.Value)
                    {
                        if (daFileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _logger.Report($"✅ DA Compatibility: MATCH! Chip '{detectedChipName}' (rule key '{rule.Key}') matches DA keyword '{keyword}'.\n");
                            return true;
                        }
                    }
                    _logger.Report($"❌ DA Compatibility: MISMATCH! Chip '{detectedChipName}' (rule key '{rule.Key}') found, but DA '{daFileName}' does not match expected keywords: {string.Join(", ", rule.Value)}.\n");
                    return false;
                }
            }
            
            if (detectedChipName.StartsWith("Unknown_", StringComparison.OrdinalIgnoreCase))
            {
                string seriesHint = "";
                if (detectedChipName.IndexOf("Helio", StringComparison.OrdinalIgnoreCase) >= 0) seriesHint = "Helio";
                else if (detectedChipName.IndexOf("Dimensity", StringComparison.OrdinalIgnoreCase) >= 0) seriesHint = "Dimensity";

                if (!string.IsNullOrEmpty(seriesHint) && DaCompatibilityRules.TryGetValue(seriesHint, out var genericKeywords))
                {
                     _logger.Report($"ℹ️ Using generic rule for '{seriesHint}' for chip '{detectedChipName}'.\n");
                    foreach (string keyword in genericKeywords)
                    {
                        if (daFileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _logger.Report($"✅ DA Compatibility: GENERIC MATCH! Chip series '{seriesHint}' matches DA keyword '{keyword}'.\n");
                            return true;
                        }
                    }
                    _logger.Report($"❌ DA Compatibility: GENERIC MISMATCH! Chip series '{seriesHint}' (for '{detectedChipName}') found, but DA '{daFileName}' does not match expected generic keywords: {string.Join(", ", genericKeywords)}.\n");
                    return false;
                }
            }

            string hwCodeStr = $"0x{detectedHwCode:X4}";
            if (DaCompatibilityRules.TryGetValue(hwCodeStr, out var hwCodeKeywords))
            {
                _logger.Report($"ℹ️ Using HW Code rule '{hwCodeStr}' for chip '{detectedChipName}'.\n");
                foreach (string keyword in hwCodeKeywords)
                {
                    if (daFileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _logger.Report($"✅ DA Compatibility: HW CODE MATCH! HW Code '{hwCodeStr}' matches DA keyword '{keyword}'.\n");
                        return true;
                    }
                }
                _logger.Report($"❌ DA Compatibility: HW CODE MISMATCH! HW Code '{hwCodeStr}' rule found, but DA '{daFileName}' does not match expected keywords: {string.Join(", ", hwCodeKeywords)}.\n");
                return false;
            }
            
            _logger.Report($"⚠️ No specific DA compatibility rules found for chip '{detectedChipName}' (HW Code: {hwCodeStr}). Assuming compatible. Please verify DA choice manually.\n");
            return true; 
        }

        /// <summary>
        /// Processes the connected Mediatek device. This involves detecting the device,
        /// performing a handshake, optionally uploading a Download Agent (DA), and retrieving device information.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>True if the process completes successfully; otherwise, false.</returns>
        public async Task<bool> ProcessDeviceAsync(CancellationToken cancellationToken = default)
        {
            _logger.Report("🚀 Starting device processing...\n");
            _progressBar.Report(0);
            Stream stream = null; 

            try
            {
                var deviceDetectionProgress = new Progress<string>(msg => _logger.Report($"[Detector] {msg}"));
                string port = await MediatekDeviceDetector.DetectMediatekPreloaderAsync(deviceDetectionProgress, DefaultDetectionTimeoutSeconds, cancellationToken);

                if (string.IsNullOrEmpty(port) || port.StartsWith("No Mediatek device detected", StringComparison.OrdinalIgnoreCase) || port.StartsWith("Detection failed", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Report($"❌ Device detection failed: {port}.\n");
                    _progressBar.Report(0);
                    return false;
                }

                _logger.Report($"✅ Device detected on: {port}\n");
                _progressBar.Report(10);

                using (var mtkDevice = new MTKPreloaderDevice(port, _logger)) // Ensure MTKPreloaderDevice is disposed
                {
                    stream = mtkDevice.OpenSerialStream();
                    if (stream == null) return false; // Error already logged by OpenSerialStream

                    ChipDetectionResult chipInfo = await HandshakeAsync(stream, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (chipInfo == null || !string.IsNullOrEmpty(chipInfo.Error) || chipInfo.ChipName == "Unknown")
                    {
                        _logger.Report($"❌ Handshake or chip identification failed. Details: {(chipInfo?.Error ?? "No chip details")}\n");
                        return false; 
                    }
                    _progressBar.Report(20);

                    bool proceedWithDAUpload = true;
                    if (!string.IsNullOrEmpty(_daFilePath))
                    {
                        if (!IsDACompatible(chipInfo.ChipName, chipInfo.HwCode, _daFilePath))
                        {
                            _logger.Report($"⚠️ WARNING: The selected DA file ('{Path.GetFileName(_daFilePath)}') may not be compatible with the detected chip ('{chipInfo.ChipName}'). Skipping DA upload.\n");
                            proceedWithDAUpload = false;
                        }
                    }
                    else
                    {
                        _logger.Report("ℹ️ No DA file path set. DA upload will be skipped.\n");
                        proceedWithDAUpload = false; 
                    }

                    if (proceedWithDAUpload)
                    {
                        if (!await UploadDAAsync(stream, cancellationToken))
                        {
                            _logger.Report("❌ DA Upload process failed critical step. Further operations requiring DA may not work.\n");
                        }
                        _progressBar.Report(60);
                    }
                    else 
                    {
                        _logger.Report("ℹ️ DA Upload skipped (either not set, or due to compatibility concerns).\n");
                        _progressBar.Report(60); 
                    }
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!await GetDeviceInfoAsync(stream, chipInfo, cancellationToken))
                    {
                        _logger.Report("⚠️ Failed to get complete device info. Some information might be missing.\n");
                    }
                } // MTKPreloaderDevice (and its stream via SerialPort) is disposed here

                _progressBar.Report(100);
                _logger.Report("🎉 Device processing finished.\n"); 
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Report("🚫 Operation was cancelled by the user.\n");
                _progressBar.Report(0);
                return false;
            }
            catch (IOException ioEx)
            {
                _logger.Report($"❌ An I/O error occurred during device processing: {ioEx.Message}\n");
                // Consider logging StackTrace for debug builds: #if DEBUG _logger.Report($"StackTrace: {ioEx.StackTrace}\n"); #endif
                _progressBar.Report(0);
                return false;
            }
            catch (UnauthorizedAccessException uaEx)
            {
                _logger.Report($"❌ Access denied error during device processing: {uaEx.Message}. Check permissions or if port is in use.\n");
                _progressBar.Report(0);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Report($"❌ An unexpected error occurred during device processing: {ex.GetType().Name} - {ex.Message}\n");
                // Consider logging StackTrace for debug builds
                _progressBar.Report(0);
                return false;
            }
            finally
            {
                // stream is managed by MTKPreloaderDevice's using block now.
                // If mtkDevice was not in a using block, manual stream closure would be here.
            }
        }

        private async Task<ChipDetectionResult> HandshakeAsync(Stream stream, CancellationToken ct)
        {
            _logger.Report("🤝 MTK BootROM Handshake...\n");
            byte[] recvBuf = new byte[1];
            ChipDetectionResult errorResult(string message, string details = null) => 
                new ChipDetectionResult { Error = message + (string.IsNullOrEmpty(details) ? "" : $" Details: {details}"), ChipName = "Unknown" };

            try
            {
                // Handshake Step 1: Send 0xA0, expect 0x5F
                await stream.WriteAsync(new byte[] { 0xA0 }, 0, 1, ct);
                _logger.Report("📡 Handshake: Sent 0xA0, waiting for 0x5F...\n");
                await Task.Delay(50, ct); // Brief delay for device to process

                int read = await stream.ReadAsync(recvBuf, 0, 1, ct);
                if (ct.IsCancellationRequested) return errorResult("Operation cancelled during handshake step 1.");
                if (read != 1 || recvBuf[0] != 0x5F)
                {
                    _logger.Report($"❌ Handshake Error: Step 1 failed. Expected 0x5F, got 0x{recvBuf[0]:X2}.\n");
                    return errorResult("Handshake step 1 failed.", $"Expected 0x5F, got 0x{recvBuf[0]:X2}");
                }
                _logger.Report("✅ Handshake: Received 0x5F.\n");

                // Handshake Step 2: Send 0x0A, expect 0xF5
                await stream.WriteAsync(new byte[] { 0x0A }, 0, 1, ct);
                _logger.Report("📡 Handshake: Sent 0x0A, waiting for 0xF5...\n");
                await Task.Delay(50, ct); // Brief delay

                read = await stream.ReadAsync(recvBuf, 0, 1, ct);
                if (ct.IsCancellationRequested) return errorResult("Operation cancelled during handshake step 2.");
                if (read != 1 || recvBuf[0] != 0xF5)
                {
                    _logger.Report($"❌ Handshake Error: Step 2 failed. Expected 0xF5, got 0x{recvBuf[0]:X2}.\n");
                    return errorResult("Handshake step 2 failed.", $"Expected 0xF5, got 0x{recvBuf[0]:X2}");
                }
                _logger.Report("✅ Handshake: Received 0xF5. MTK BootROM handshake successful!\n");

                _logger.Report("🕵️‍♂️ Detecting chip information via MTKChipDetector...\n");
                var mtkDetection = new MTKChipDetector(stream, msg => _logger.Report($"[ChipDetector] {msg}"), ct); 
                var result = await mtkDetection.DetectChipAdvancedAsync(); // This method internally handles its own cancellation checks

                if (result == null || !string.IsNullOrEmpty(result.Error))
                {
                    _logger.Report($"❌ Chip detection failed: {result?.Error ?? "Detector returned null."}\n");
                    return result ?? errorResult("Chip detection returned null or error.");
                }

                _logger.Report($"✅ Chip Detected: {result.ChipName}\n");
                _logger.Report($"ℹ️ HWID: 0x{result.HwCode:X4}\n");
                if (!string.IsNullOrEmpty(result.Notes))
                    _logger.Report($"ℹ️ Notes: {result.Notes}\n");

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Report("🚫 Handshake or chip detection was cancelled.\n");
                return errorResult("Operation cancelled during handshake/chip detection.");
            }
            catch (IOException ioEx)
            {
                _logger.Report($"❌ IOException during handshake/chip detection: {ioEx.Message}\n");
                return errorResult("I/O Error during handshake/chip detection.", ioEx.Message);
            }
            catch (Exception ex)
            {
                _logger.Report($"❌ Unexpected exception during handshake/chip detection: {ex.GetType().Name} - {ex.Message}\n");
                return errorResult("Unexpected exception during handshake/chip detection.", ex.Message);
            }
        }
         
        private async Task<bool> UploadDAAsync(Stream stream, CancellationToken ct)
        {
            _logger.Report("⬆️ Starting DA upload process...\n");
            if (string.IsNullOrEmpty(_daFilePath)) 
            {
                _logger.Report("ℹ️ No DA file set. Upload process skipped.\n");
                return true; 
            }
            if (!File.Exists(_daFilePath))
            {
                _logger.Report($"❌ DA file not found: '{_daFilePath}'. Cannot upload.\n");
                return false;
            }

            try
            {
                byte[] daBytes = await Task.Run(() => File.ReadAllBytes(_daFilePath), ct); // Offload file reading
                int daLength = daBytes.Length;
                _logger.Report($"📂 Loaded DA file: {Path.GetFileName(_daFilePath)} ({daLength} bytes).\n");

                if (daLength < 256) // 0x100
                {
                    _logger.Report("❌ Error: DA file is too small to be a valid DA (less than 256 bytes).\n");
                    return false;
                }

                const int maxSyncRetries = 3;
                bool syncSuccess = false;
                byte[] buffer = new byte[1];

                for (int i = 0; i < maxSyncRetries; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    _logger.Report($"📡 DA Upload: Sending SYNC command (0xA0) [Attempt {i + 1}/{maxSyncRetries}]...\n");
                    await stream.WriteAsync(new byte[] { 0xA0 }, 0, 1, ct);
                    await Task.Delay(100, ct); 

                    int read = await stream.ReadAsync(buffer, 0, 1, ct);
                    ct.ThrowIfCancellationRequested();

                    if (read != 1) { _logger.Report("❌ DA Upload: No response to SYNC command.\n"); continue; }
                    
                    string resp = $"0x{buffer[0]:X2}";
                    _logger.Report($"📥 DA Upload: SYNC Response: {resp}.\n");

                    if (buffer[0] == 0x5F) { _logger.Report("✅ DA Upload: SYNC OK with BootROM.\n"); syncSuccess = true; break; }
                    if (buffer[0] == 0xA1) { _logger.Report("⚠️ DA Upload: Device may already be in DA mode (response 0xA1). Assuming DA active.\n"); return true; }
                    
                    _logger.Report($"❌ DA Upload: Unexpected SYNC response: {resp}. Retrying...\n");
                    await Task.Delay(300, ct); 
                }

                if (!syncSuccess) { _logger.Report("❌ DA Upload: Failed to establish SYNC with BootROM.\n"); return false; }

                uint daAddress = DefaultDaLoadAddress; 
                uint daSize = (uint)daLength;
                _logger.Report($"📤 DA Upload: Sending DA Header (Address: 0x{daAddress:X8}, Size: {daSize} bytes (0x{daSize:X})).\n");

                byte[] addrBytes = BitConverter.GetBytes(daAddress);
                byte[] sizeBytes = BitConverter.GetBytes(daSize);
                if (!BitConverter.IsLittleEndian) { Array.Reverse(addrBytes); Array.Reverse(sizeBytes); }

                await stream.WriteAsync(addrBytes, 0, 4, ct);
                await stream.WriteAsync(sizeBytes, 0, 4, ct);
                await stream.FlushAsync(ct); 
                await Task.Delay(100, ct); 
                ct.ThrowIfCancellationRequested();

                _logger.Report("⏳ DA Upload: Waiting for BootROM ACK for DA (0xA1)...\n");
                int ackRead = await stream.ReadAsync(buffer, 0, 1, ct);
                ct.ThrowIfCancellationRequested();
                if (ackRead != 1) { _logger.Report("❌ DA Upload: No response from device after DA header.\n"); return false; }
                
                string ackResp = $"0x{buffer[0]:X2}";
                _logger.Report($"📥 DA Upload: BootROM DA ACK Response: {ackResp}.\n");
                if (buffer[0] != 0xA1) { _logger.Report($"❌ DA Upload: Unexpected BootROM DA ACK: {ackResp}. Expected 0xA1.\n"); return false; }
                _logger.Report("✅ DA Upload: BootROM acknowledged DA transfer.\n");

                int totalSent = 0;
                _logger.Report($"📦 DA Upload: Uploading DA payload ({daLength} bytes) in {DefaultDaUploadChunkSize}-byte chunks...\n");
                while (totalSent < daLength)
                {
                    ct.ThrowIfCancellationRequested();
                    int remaining = daLength - totalSent;
                    int sendNow = Math.Min(DefaultDaUploadChunkSize, remaining);
                    await stream.WriteAsync(daBytes, totalSent, sendNow, ct);
                    totalSent += sendNow;
                    _progressBar.Report(20 + (int)((double)totalSent / daLength * 40));
                    await Task.Delay(1, ct); 
                }
                _logger.Report($"📊 DA Upload: Payload sent ({totalSent}/{daLength} bytes).\n");
                ct.ThrowIfCancellationRequested();

                _logger.Report("⏳ DA Upload: Waiting for DA execution response...\n");
                int execRead = await stream.ReadAsync(buffer, 0, 1, ct);
                ct.ThrowIfCancellationRequested();
                if (execRead != 1) { _logger.Report("❌ DA Upload: No execution response from DA.\n"); return false; }
                
                string execResp = $"0x{buffer[0]:X2}";
                _logger.Report($"📥 DA Upload: DA Execution Response: {execResp}.\n");
                if (buffer[0] == 0xE0 || buffer[0] == 0xC0 || buffer[0] == 0xA1) 
                { _logger.Report($"✅ DA Upload: DA executed successfully! Response: {execResp}.\n"); return true; }
                
                _logger.Report($"❌ DA Upload: Unexpected DA execution response: {execResp}. Expected 0xE0, 0xC0, or 0xA1.\n");
                return false;
            }
            catch (OperationCanceledException) { _logger.Report("🚫 DA Upload was cancelled.\n"); return false; }
            catch (IOException ioEx) { _logger.Report($"❌ IOException during DA Upload: {ioEx.Message}\n"); return false; }
            catch (Exception ex) { _logger.Report($"❌ Unexpected exception during DA Upload: {ex.GetType().Name} - {ex.Message}\n"); return false; }
        }

        private async Task<bool> GetDeviceInfoAsync(Stream stream, ChipDetectionResult chipInfo, CancellationToken ct)
        {
            _logger.Report("🔍 Reading device info (requires DA to be active)...\n");
            if (!stream.CanRead || !stream.CanWrite)
            {
                _logger.Report("⚠️ Cannot read device info: Stream is not R/W. DA might not be running or stream closed.\n");
                return false;
            }

            var commandSets = GetCommandMap();
            Dictionary<string, string> results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CHIP_NAME"] = chipInfo?.ChipName ?? "Unknown",
                ["HW_CODE"] = $"0x{chipInfo?.HwCode:X4}" ?? "0x0000",
                ["CHIP_NOTES"] = chipInfo?.Notes ?? "N/A"
            };

            if (commandSets.TryGetValue("STANDARD", out var standardCommands))
                foreach (var key in standardCommands.Keys)
                    if (!results.ContainsKey(key)) results[key] = "Not Found";
            
            bool overallInfoRetrieved = chipInfo != null && !string.IsNullOrEmpty(chipInfo.ChipName) && chipInfo.ChipName != "Unknown";

            foreach (var cmdSetEntry in commandSets)
            {
                ct.ThrowIfCancellationRequested();
                string vendor = cmdSetEntry.Key;
                _logger.Report($"🔄 Trying device info command set for: {vendor}...\n");
                foreach (var cmdKv in cmdSetEntry.Value)
                {
                    ct.ThrowIfCancellationRequested();
                    string keyToQuery = cmdKv.Key;
                    byte[] command = cmdKv.Value;
                    string previousValue = results.TryGetValue(keyToQuery, out var pv) ? pv : "Not Set";
                    try
                    {
                        _logger.Report($"  ➡️ Querying: {keyToQuery} (Cmd: {BitConverter.ToString(command)})...\n");
                        await stream.WriteAsync(command, 0, command.Length, ct);
                        await Task.Delay(250, ct); 
                        byte[] buffer = new byte[512]; 
                        int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                        ct.ThrowIfCancellationRequested();
                        
                        if (read > 0)
                        {
                            string value = Encoding.ASCII.GetString(buffer, 0, read).Trim('\0', '\r', '\n', ' ');
                            if (!string.IsNullOrWhiteSpace(value) && value.Length > 1 && !value.ToLowerInvariant().Contains("error")) 
                            {
                                if (previousValue == "Not Found" || previousValue == "Not Set") _logger.Report($"  ✔️ {keyToQuery} ({vendor}): {value}\n");
                                else if (previousValue != value) _logger.Report($"  🔄 {keyToQuery} ({vendor}): {value} (overwritten '{previousValue}')\n");
                                else _logger.Report($"  ℹ️ {keyToQuery} ({vendor}): {value} (confirmed)\n");
                                results[keyToQuery] = value;
                                overallInfoRetrieved = true; 
                            }
                            else if (previousValue == "Not Found" || previousValue == "Not Set")
                            { _logger.Report($"  ⚠️ {keyToQuery} ({vendor}): No valid data (response: '{value}').\n"); results[keyToQuery] = "Not Found (empty/error)"; }
                        }
                        else if (previousValue == "Not Found" || previousValue == "Not Set")
                        { _logger.Report($"  ⚠️ {keyToQuery} ({vendor}): No response.\n"); results[keyToQuery] = "Not Found (no response)"; }
                    }
                    catch (IOException ioEx) { _logger.Report($"  ❌ I/O Error for {keyToQuery} ({vendor}): {ioEx.Message}.\n"); results[keyToQuery] = "Not Found (I/O Error)"; }
                    catch (Exception ex) { _logger.Report($"  ❌ Exception for {keyToQuery} ({vendor}): {ex.GetType().Name} - {ex.Message}\n"); results[keyToQuery] = "Not Found (Exception)"; }
                }
            }
            _logger.Report("\n--- Device Information Summary ---\n");
            foreach(var fr in results.OrderBy(kvp => kvp.Key)) _logger.Report($"🔹 {fr.Key}: {fr.Value}\n");
            _logger.Report("--------------------------------\n");

            if (!overallInfoRetrieved && (!results.ContainsKey("GET_BRAND") || results["GET_BRAND"] == "Not Found"))
            { _logger.Report("❌ Failed to get significant device info. DA/commands unsuitable.\n"); return false; }
            
            _logger.Report("✅ Device info retrieval attempt finished.\n");
            return true;
        }

        private Dictionary<string, Dictionary<string, byte[]>> GetCommandMap()
        {
            // These commands are examples and require real, verified commands for specific devices/DAs.
            return new Dictionary<string, Dictionary<string, byte[]>>
            {
                ["STANDARD"] = new Dictionary<string, byte[]> {
                    { "GET_BRAND", new byte[] { 0xD2, 0x00, 0x01 } }, { "GET_MODEL", new byte[] { 0xD2, 0x00, 0x02 } },
                    { "GET_ANDROID_VER", new byte[] { 0xD2, 0x00, 0x03 } }, { "GET_BUILD_ID", new byte[] { 0xD2, 0x00, 0x04 } }, 
                    { "GET_SERIALNO", new byte[] { 0xD2, 0x00, 0x05 } }, { "GET_PLATFORM", new byte[] { 0xD2, 0x00, 0x06 } }, 
                    { "GET_PRODUCT_NAME", new byte[] { 0xD2, 0x00, 0x07 } }, { "GET_BASEBAND_VER", new byte[] { 0xD2, 0x00, 0x08 } }, 
                    { "GET_DA_VERSION", new byte[] { 0xDA, 0x00, 0x00 } }, { "GET_SOC_ID", new byte[] { 0xD2, 0x00, 0x09 } } },
                ["XIAOMI"] = new Dictionary<string, byte[]> {
                    { "GET_XIAOMI_MARKET_NAME", new byte[] { 0XM, 0x01, 0x01, 0x00 } }, { "GET_XIAOMI_DEVICE_CODENAME", new byte[] { 0XM, 0x01, 0x01, 0x01 } }, 
                    { "GET_MIUI_VERSION", new byte[] { 0XM, 0x01, 0x02, 0x00 } }, { "GET_XIAOMI_REGION_LOCK_STATUS", new byte[] { 0XM, 0x01, 0x03, 0x00 } } },
                ["OPPO"] = new Dictionary<string, byte[]> {
                    { "GET_OPPO_PRODUCT_NAME_EXT", new byte[] { 0OP, 0x01, 0x00 } }, { "GET_COLOROS_VERSION", new byte[] { 0OP, 0x02, 0x00 } },    
                    { "GET_OPPO_SERIAL_NO_EXT", new byte[] { 0OP, 0x03, 0x00 } } }
            };
        }
    }
}
