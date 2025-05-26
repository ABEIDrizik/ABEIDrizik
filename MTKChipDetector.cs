using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace MTKDeviceManager
{
    /// <summary>
    /// Detects Mediatek chipsets by interacting with the device over a provided stream.
    /// It uses various commands and analyzes responses to identify the chip.
    /// </summary>
    public class MTKChipDetector
    {
        private readonly Action<string> _logger;
        private readonly Stream _stream;
        private readonly CancellationToken _ct;

        private const int StandardCmdDelayMs = 300;
        private const int ExtendedCmdDelayMs = 500;
        private const int BootRomCmdDelayMs = 500;
        private const int ResponseBufferSize = 256;
        private const int MaxRawDataHexLength = 16; // For RawHex in ChipDetectionResult
        private const int AsciiHintSearchLength = 32; // Bytes to search for ASCII hints in rawData

        // Expanded HW_CODE database with observed codes.
        // This list needs regular updates from reliable sources like official documentation,
        // community findings, or by analyzing device responses.
        private static readonly Dictionary<ushort, string> HwCodeToChipName = new Dictionary<ushort, string>()
        {
            // MT65xx series (older chips)
            { 0x6516, "MT6516" }, { 0x6573, "MT6573" }, { 0x6575, "MT6575" }, { 0x6577, "MT6577" },
            { 0x6580, "MT6580" }, { 0x6582, "MT6582" }, { 0x6589, "MT6589" }, { 0x6592, "MT6592" }, { 0x6595, "MT6595" },
            // MT67xx series (mid-range)
            { 0x6735, "MT6735" }, { 0x6737, "MT6737" }, { 0x6738, "MT6738" }, { 0x6739, "MT6739" },
            { 0x6750, "MT6750" }, { 0x6752, "MT6752" }, { 0x6753, "MT6753" }, { 0x6755, "MT6755 (Helio P10)" },
            { 0x6757, "MT6757 (Helio P20/P25)" }, { 0x6758, "MT6758 (Helio P30)" }, { 0x6761, "MT6761 (Helio A22)" },
            { 0x6762, "MT6762 (Helio P22)" }, { 0x6763, "MT6763 (Helio P23)" }, { 0x6765, "MT6765 (Helio P35/A25)" },
            { 0x6768, "MT6768 (Helio G80/G85/G88)" }, { 0x6771, "MT6771 (Helio P60/P70)" }, { 0x6779, "MT6779 (Helio P90/P95)" },
            { 0x6781, "MT6781 (Helio G96)" }, { 0x6785, "MT6785 (Helio G90/G90T)" }, { 0x6787, "MT6787" }, 
            { 0x6789, "MT6789 (Helio G99)" }, { 0x6795, "MT6795 (Helio X10)" }, { 0x6797, "MT6797 (Helio X20/X23/X25/X27)" },
            { 0x6799, "MT6799 (Helio X30)" },
            // MT68xx series (Dimensity 5G chips)
            { 0x6833, "MT6833 (Dimensity 700)" }, { 0x6835, "MT6835" }, { 0x6853, "MT6853 (Dimensity 720/800U)" },
            { 0x6873, "MT6873 (Dimensity 800)" }, { 0x6875, "MT6875 (Dimensity 820)" }, { 0x6877, "MT6877 (Dimensity 1200/1100/1000C)" },
            { 0x6885, "MT6885 (Dimensity 1000/1000L)" }, { 0x6889, "MT6889 (Dimensity 1000+)" }, { 0x6891, "MT6891 (Dimensity 1100)" },
            { 0x6893, "MT6893 (Dimensity 1200)" }, { 0x6895, "MT6895" }, { 0x6897, "MT6897" },
            { 0x6899, "MT6899 (Dimensity 9000+)" }, { 0x6983, "MT6983 (Dimensity 9200)" }, { 0x6855, "MT6855 (Dimensity 8200/7020)" },
            // MT81xx series (tablet processors)
            { 0x8121, "MT8121" }, { 0x8127, "MT8127" }, { 0x8135, "MT8135" }, { 0x8150, "MT8150" },
            { 0x8163, "MT8163" }, { 0x8167, "MT8167" }, { 0x8168, "MT8168 (Kompanio 500 / MT8183 Base)" }, 
            { 0x8173, "MT8173" }, { 0x8176, "MT8176" }, { 0x8183, "MT8183 (Kompanio 500)" },
            { 0x8185, "MT8185 (Kompanio 1300T)" }, { 0x8192, "MT8192 (Kompanio 820)" }, { 0x8195, "MT8195 (Kompanio 1200)" },
            // MT87xx series (premium tablets)
            { 0x8765, "MT8765" }, { 0x8766, "MT8766" }, { 0x8768, "MT8768" }, { 0x8786, "MT8786" }, 
            { 0x8788, "MT8788" }, { 0x8789, "MT8789 (Kompanio 900T)" }, { 0x8791, "MT8791 (Dimensity 900T)" },
            // MT96xx series (flagship TV chips)
            { 0x9650, "MT9650" }, { 0x9660, "MT9660" }, { 0x9667, "MT9667" }, { 0x9668, "MT9668" }, { 0x9669, "MT9669" },
            // Special cases
            { 0x2601, "MT2601 (Wearable)" }, { 0x3622, "MT3622 (IoT)" }, { 0x7668, "MT7668 (Connectivity)" },
            // Example entries (verify HW codes)
            { 0x7001, "Dimensity 1200 (Example)" }, { 0x7002, "Dimensity 1100 (Example)" }, 
            { 0x7003, "Dimensity 800U (Example)" }, { 0x7004, "Helio G95 (Example)" }, 
            { 0x7005, "MT8183 (Kompanio 500) (Example)" }, { 0x6985, "Dimensity 9300 (Example)" }, 
            { 0x6879, "Dimensity 7050 (Example)" },
            { 0x01C1, "MTK_BootROM_C1 (Possible BootROM Response)" }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="MTKChipDetector"/> class.
        /// </summary>
        /// <param name="stream">The communication stream to the device.</param>
        /// <param name="logger">An action to log messages.</param>
        /// <param name="ct">A CancellationToken to observe for cancellation requests.</param>
        public MTKChipDetector(Stream stream, Action<string> logger, CancellationToken ct)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _logger = logger ?? (_ => { }); // Use a no-op logger if null
            _ct = ct;
        }

        /// <summary>
        /// Asynchronously detects the chip by trying various detection methods.
        /// </summary>
        /// <returns>A <see cref="ChipDetectionResult"/> containing the details of the detected chip or errors encountered.</returns>
        public async Task<ChipDetectionResult> DetectChipAdvancedAsync()
        {
            var results = new List<ChipDetectionResult>();
            _ct.ThrowIfCancellationRequested(); // Check before starting

            _logger?.Invoke("ℹ️ Starting advanced chip detection cycle...");
            results.Add(await TryStandardDetection());
            _ct.ThrowIfCancellationRequested();

            results.Add(await TryExtendedCommands());
            _ct.ThrowIfCancellationRequested();
            
            results.Add(await TryBootRomIdentification());
            _ct.ThrowIfCancellationRequested();

            _logger?.Invoke("ℹ️ Analyzing collected chip detection results...");
            return AnalyzeResults(results);
        }

        private async Task<ChipDetectionResult> TryStandardDetection()
        {
            _logger?.Invoke("➡️ Attempting standard HW_CODE detection...");
            try
            {
                byte[] readHwCodeCmd = { 0xFD, 0xD0 }; // Standard command to read HW Code
                await _stream.WriteAsync(readHwCodeCmd, 0, readHwCodeCmd.Length, _ct);
                await Task.Delay(StandardCmdDelayMs, _ct); 

                byte[] buffer = new byte[ResponseBufferSize]; 
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, _ct);

                if (bytesRead >= 2) 
                {
                    ushort hwCode = BitConverter.ToUInt16(buffer, 0);
                    _logger?.Invoke($"✅ Standard detection: Received HW_CODE 0x{hwCode:X4}.");
                    return InterpretResponse(hwCode, buffer, "Standard HW_CODE", bytesRead);
                }
                _logger?.Invoke("⚠️ Standard detection: Not enough bytes received for HW_CODE.");
                return new ChipDetectionResult { Error = "Standard detection: Insufficient data received", SourceCommand = "Standard HW_CODE" };
            }
            catch (OperationCanceledException)
            {
                _logger?.Invoke("🚫 Standard detection cancelled.");
                return new ChipDetectionResult { Error = "Standard detection cancelled", SourceCommand = "Standard HW_CODE" };
            }
            catch (IOException ioEx)
            {
                _logger?.Invoke($"❌ Standard detection I/O error: {ioEx.Message}");
                return new ChipDetectionResult { Error = $"Standard detection I/O error: {ioEx.Message}", SourceCommand = "Standard HW_CODE" };
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"❌ Standard detection failed: {ex.GetType().Name} - {ex.Message}");
                return new ChipDetectionResult { Error = $"Standard detection failed: {ex.Message}", SourceCommand = "Standard HW_CODE" };
            }
        }

        private async Task<ChipDetectionResult> TryExtendedCommands()
        {
            _logger?.Invoke("➡️ Attempting extended command probing...");
            var extendedCommands = new Dictionary<string, byte[]>
            {
                { "DA_Identification", new byte[] { 0xDA, 0xDA } }, 
                { "Secure_Chip_ID", new byte[] { 0xA5, 0x5A } },    
                { "Factory_Mode", new byte[] { 0xF0, 0x0F } }       
            };

            foreach (var cmdEntry in extendedCommands)
            {
                _ct.ThrowIfCancellationRequested();
                _logger?.Invoke($"  찔 Trying extended command: {cmdEntry.Key} ({BitConverter.ToString(cmdEntry.Value)})...");
                try
                {
                    await _stream.WriteAsync(cmdEntry.Value, 0, cmdEntry.Value.Length, _ct);
                    await Task.Delay(ExtendedCmdDelayMs, _ct); 

                    byte[] buffer = new byte[ResponseBufferSize];
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, _ct);

                    if (bytesRead >= 2)
                    {
                        ushort responseCode = BitConverter.ToUInt16(buffer, 0);
                        _logger?.Invoke($"  ✅ Extended command {cmdEntry.Key}: Received response code 0x{responseCode:X4}.");
                        if (bytesRead > 2) _logger?.Invoke($"    Additional data: {BitConverter.ToString(buffer, 2, Math.Min(bytesRead - 2, MaxRawDataHexLength))}");
                        return InterpretResponse(responseCode, buffer, cmdEntry.Key, bytesRead);
                    }
                    _logger?.Invoke($"  ⚠️ Extended command {cmdEntry.Key}: Not enough bytes received.");
                }
                catch (OperationCanceledException)
                {
                    _logger?.Invoke($"🚫 Extended command {cmdEntry.Key} cancelled.");
                    // Return a specific result for cancellation to allow AnalyzeResults to know this path was cancelled
                    return new ChipDetectionResult { Error = $"Extended command {cmdEntry.Key} cancelled", SourceCommand = cmdEntry.Key, IsVerified = false };
                }
                catch (IOException ioEx)
                {
                     _logger?.Invoke($"❌ Extended command {cmdEntry.Key} I/O error: {ioEx.Message}");
                     // Continue to next command on I/O error for this specific command
                }
                catch (Exception ex)
                {
                    _logger?.Invoke($"❌ Extended command {cmdEntry.Key} failed: {ex.GetType().Name} - {ex.Message}");
                }
            }
            _logger?.Invoke("⚠️ Extended commands yielded no conclusive results or all failed.");
            return new ChipDetectionResult { Error = "Extended commands failed or yielded no results", SourceCommand = "Multiple Extended" };
        }

        private ChipDetectionResult InterpretResponse(ushort code, byte[] rawData, string source, int bytesReadCount)
        {
            _ct.ThrowIfCancellationRequested(); // Check cancellation before processing
            string rawHex = BitConverter.ToString(rawData, 0, Math.Min(bytesReadCount, MaxRawDataHexLength)); 

            if (HwCodeToChipName.TryGetValue(code, out string chipName))
            {
                _logger?.Invoke($"ℹ️ Interpreted code 0x{code:X4} from {source} as: {chipName}.");
                return new ChipDetectionResult { ChipName = chipName, HwCode = code, RawHex = rawHex, SourceCommand = source, IsVerified = true };
            }

            // Special handling for observed but undocumented codes or specific responses
            switch (code)
            {
                case 0xD2FF: 
                    _logger?.Invoke($"ℹ️ Interpreted code 0x{code:X4} (special case D2FF) from {source} as MT6769V Helio G70 (Variant).");
                    return new ChipDetectionResult { ChipName = "MT6769V Helio G70 (Variant)", HwCode = code, RawHex = rawHex, SourceCommand = source, Notes = "Possible secondary processor or specific variant response." };
                case 0xD1FE: 
                    _logger?.Invoke($"ℹ️ Interpreted code 0x{code:X4} (special case D1FE) from {source} as MT6769V Helio G70 (Another Variant).");
                    return new ChipDetectionResult { ChipName = "MT6769V Helio G70 (Another Variant)", HwCode = code, RawHex = rawHex, SourceCommand = source, Notes = "Possible alternative response for a known family." };
                default:
                    string chipHint = "";
                    if (bytesReadCount > 2)
                    {
                        int offset = 2; 
                        int lengthToSearch = Math.Min(bytesReadCount - offset, AsciiHintSearchLength);
                        if (lengthToSearch > 0)
                        {
                            string rawAscii = Encoding.ASCII.GetString(rawData, offset, lengthToSearch);
                            if (rawAscii.IndexOf("Helio", StringComparison.OrdinalIgnoreCase) >= 0) chipHint = " (Helio series suspected)";
                            else if (rawAscii.IndexOf("Dimensity", StringComparison.OrdinalIgnoreCase) >= 0) chipHint = " (Dimensity series suspected)";
                            else if (rawAscii.IndexOf("Kompanio", StringComparison.OrdinalIgnoreCase) >= 0) chipHint = " (Kompanio series suspected)";
                        }
                    }
                    _logger?.Invoke($"⚠️ Interpreted code 0x{code:X4} from {source} as Unknown. Hint: {chipHint}. Raw: {rawHex}.");
                    return new ChipDetectionResult { ChipName = $"Unknown_0x{code:X4}{chipHint}", HwCode = code, RawHex = rawHex, SourceCommand = source, Error = "Unknown response code." };
            }
        }
        
        private async Task<ChipDetectionResult> TryBootRomIdentification()
        {
            _logger?.Invoke("➡️ Attempting BootROM identification...");
            try
            {
                byte[] bootRomCommand = { 0x4D, 0x54, 0x6B, 0x6C }; // ASCII "MTkl"
                await _stream.WriteAsync(bootRomCommand, 0, bootRomCommand.Length, _ct);
                await Task.Delay(BootRomCmdDelayMs, _ct);

                byte[] buffer = new byte[ResponseBufferSize];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, _ct);

                if (bytesRead > 0)
                {
                    ushort responseCode = (bytesRead >=2) ? BitConverter.ToUInt16(buffer, 0) : (ushort)0x0000; // Default if not enough bytes for a ushort
                    _logger?.Invoke($"ℹ️ BootROM identification: Received {bytesRead} bytes. Initial code: 0x{responseCode:X4}.");
                    if (bytesRead > 2) _logger?.Invoke($"    Additional BootROM data: {BitConverter.ToString(buffer, 2, Math.Min(bytesRead - 2, MaxRawDataHexLength))}");
                    
                    string asciiResponse = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    if (asciiResponse.Contains("USB_DOWNLOAD_AGENT") || asciiResponse.Contains("BROM"))
                    {
                        _logger?.Invoke("✅ BootROM identification: Detected generic MTK BootROM mode via ASCII signature.");
                        return new ChipDetectionResult { ChipName = "MTK_BootROM_Generic", HwCode = responseCode, RawHex = BitConverter.ToString(buffer, 0, Math.Min(bytesRead, MaxRawDataHexLength)), SourceCommand = "BootROM_Mode", IsVerified = true, Notes = "Device in USB Download Agent / BootROM mode." };
                    }
                    return InterpretResponse(responseCode, buffer, "BootROM_Mode (Parsed)", bytesRead);
                }
                _logger?.Invoke("⚠️ BootROM identification: No data received.");
                return new ChipDetectionResult { Error = "BootROM identification: No data received", SourceCommand = "BootROM_Mode" };
            }
            catch (OperationCanceledException)
            {
                _logger?.Invoke("🚫 BootROM identification cancelled.");
                return new ChipDetectionResult { Error = "BootROM identification cancelled", SourceCommand = "BootROM_Mode" };
            }
            catch (IOException ioEx)
            {
                 _logger?.Invoke($"❌ BootROM identification I/O error: {ioEx.Message}");
                 return new ChipDetectionResult { Error = $"BootROM identification I/O error: {ioEx.Message}", SourceCommand = "BootROM_Mode" };
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"❌ BootROM identification failed: {ex.GetType().Name} - {ex.Message}");
                return new ChipDetectionResult { Error = $"BootROM identification failed: {ex.Message}", SourceCommand = "BootROM_Mode" };
            }
        }

        private ChipDetectionResult AnalyzeResults(List<ChipDetectionResult> results)
        {
            _logger?.Invoke("ℹ️ Analyzing all collected detection results...");
            var validResults = results.Where(r => r != null && !(r.Error?.Contains("cancelled") == true)).ToList();
            if (!validResults.Any())
            {
                _logger?.Invoke("⚠️ No valid (non-cancelled) detection results to analyze.");
                return new ChipDetectionResult { ChipName = "Unknown", Error = "No valid detection results.", Notes = "All detection methods failed or were cancelled." };
            }

            // Priority 1: Verified result with a non-generic, non-unknown chip name.
            var verifiedResult = validResults.FirstOrDefault(r => r.IsVerified && 
                                                              !string.IsNullOrEmpty(r.ChipName) && 
                                                              !r.ChipName.StartsWith("Unknown_", StringComparison.OrdinalIgnoreCase) &&
                                                              !r.ChipName.StartsWith("MTK_BootROM_Generic", StringComparison.OrdinalIgnoreCase));
            if (verifiedResult != null)
            {
                verifiedResult.Notes = (string.IsNullOrEmpty(verifiedResult.Notes) ? "" : verifiedResult.Notes + " ") + "Primary identification from HwCodeToChipName.";
                _logger?.Invoke($"✅ Analysis: Best result is verified: {verifiedResult.ChipName} from {verifiedResult.SourceCommand}.");
                return verifiedResult;
            }

            // Priority 2: Results with specific chip names (even if not primary verified), preferring those with hints.
            var namedResults = validResults
                .Where(r => !string.IsNullOrEmpty(r.ChipName) && !r.ChipName.StartsWith("Unknown_", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.IsVerified) 
                .ThenByDescending(r => r.ChipName.Contains("Helio") || r.ChipName.Contains("Dimensity") || r.ChipName.Contains("Kompanio"))
                .ThenByDescending(r => (r.RawHex?.Length ?? 0))
                .ThenBy(r => string.IsNullOrEmpty(r.Error)) 
                .ToList();

            if (namedResults.Any())
            {
                var bestNamed = namedResults.First();
                bestNamed.Notes = (string.IsNullOrEmpty(bestNamed.Notes) ? "" : bestNamed.Notes + " ") +
                                  (bestNamed.IsVerified ? "Chip name confirmed. " : "Chip name inferred heuristically. ") +
                                  $"Source: {bestNamed.SourceCommand}.";
                if (bestNamed.ChipName.Contains("(Example)") || bestNamed.ChipName.Contains("(Variant)"))
                {
                     bestNamed.Notes += " This identification may be an example or variant; verify if possible.";
                }
                _logger?.Invoke($"✅ Analysis: Best result is named (heuristic): {bestNamed.ChipName} from {bestNamed.SourceCommand}.");
                return bestNamed;
            }

            var bootRomResult = validResults.FirstOrDefault(r => r.ChipName == "MTK_BootROM_Generic" && r.IsVerified);
            if (bootRomResult != null)
            {
                _logger?.Invoke("✅ Analysis: Result is generic BootROM mode.");
                return bootRomResult;
            }
            
            var hintedUnknownResults = validResults
                .Where(r => !string.IsNullOrEmpty(r.ChipName) && r.ChipName.StartsWith("Unknown_", StringComparison.OrdinalIgnoreCase) && (r.ChipName.Contains("Helio") || r.ChipName.Contains("Dimensity") || r.ChipName.Contains("Kompanio")))
                .OrderByDescending(r => (r.RawHex?.Length ?? 0))
                .ThenBy(r => string.IsNullOrEmpty(r.Error))
                .ToList();

            if (hintedUnknownResults.Any()) {
                var bestHinted = hintedUnknownResults.First();
                bestHinted.Notes = (string.IsNullOrEmpty(bestHinted.Notes) ? "" : bestHinted.Notes + " ") +
                                   "Chipset not definitively identified by code. " +
                                   "A potential series was suspected from device response. " +
                                   $"Check HwCode 0x{bestHinted.HwCode:X4} and RawHex. Source: {bestHinted.SourceCommand}.";
                _logger?.Invoke($"⚠️ Analysis: Result is unknown with series hint: {bestHinted.ChipName} from {bestHinted.SourceCommand}.");
                return bestHinted;
            }
            
            var bestOfTheRest = validResults
                .OrderByDescending(r => (r.RawHex?.Length ?? 0))
                .ThenBy(r => string.IsNullOrEmpty(r.Error)) 
                .ThenBy(r => r.SourceCommand == "Standard HW_CODE") 
                .FirstOrDefault();

            if (bestOfTheRest != null)
            {
                bestOfTheRest.ChipName = string.IsNullOrEmpty(bestOfTheRest.ChipName) || bestOfTheRest.ChipName.StartsWith("Unknown_") ? $"Unknown_0x{bestOfTheRest.HwCode:X4}" : bestOfTheRest.ChipName;
                bestOfTheRest.Notes = (string.IsNullOrEmpty(bestOfTheRest.Notes) ? "" : bestOfTheRest.Notes + " ") +
                                      "Chipset identification is uncertain. " +
                                      "The HwCodeToChipName list may need an update, or the device response was ambiguous. " +
                                      $"HwCode: 0x{bestOfTheRest.HwCode:X4}. Source: {bestOfTheRest.SourceCommand}.";
                 _logger?.Invoke($"⚠️ Analysis: Best of the rest is: {bestOfTheRest.ChipName} from {bestOfTheRest.SourceCommand}. Identification uncertain.");
                return bestOfTheRest;
            }
            
            _logger?.Invoke("❌ Analysis: No definitive result found after analyzing all attempts.");
            return new ChipDetectionResult { ChipName = "Unknown", Error = "Analysis failed to determine best result.", Notes = "Chipset could not be identified. All detection attempts failed or yielded insufficient data." };
        }
    }

    /// <summary>
    /// Represents the result of a chip detection attempt.
    /// </summary>
    public class ChipDetectionResult
    {
        /// <summary>Gets or sets the identified chip name.</summary>
        public string ChipName { get; set; }
        /// <summary>Gets or sets the hardware code (HW_CODE) read from the device.</summary>
        public ushort HwCode { get; set; }
        /// <summary>Gets or sets a hexadecimal string representation of the first few bytes of the raw response data.</summary>
        public string RawHex { get; set; }
        /// <summary>Gets or sets the command or method that yielded this result.</summary>
        public string SourceCommand { get; set; }
        /// <summary>Gets or sets any error message if the detection for this specific result failed.</summary>
        public string Error { get; set; }
        /// <summary>Gets or sets additional notes, such as heuristic analysis or confidence level.</summary>
        public string Notes { get; set; }
        /// <summary>Gets or sets a value indicating whether the chip identification is verified (e.g., from a direct match in HwCodeToChipName).</summary>
        public bool IsVerified { get; set; }
    }
}
