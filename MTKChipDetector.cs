using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace MTKDeviceManager
{

    public class MTKChipDetector
    {
        private readonly Action<string> _logger;
        private readonly Stream _stream;
        private readonly CancellationToken _ct;

        // Expanded HW_CODE database with observed codes
        private static readonly Dictionary<ushort, string> HwCodeToChipName = new()
    {
        // MT65xx series (older chips)
        { 0x6516, "MT6516" },
        { 0x6573, "MT6573" },
        { 0x6575, "MT6575" },
        { 0x6577, "MT6577" },
        { 0x6580, "MT6580" },
        { 0x6582, "MT6582" },
        { 0x6589, "MT6589" },
        { 0x6592, "MT6592" },
        { 0x6595, "MT6595" },

        // MT67xx series (mid-range)
        { 0x6735, "MT6735" },
        { 0x6737, "MT6737" },
        { 0x6738, "MT6738" },
        { 0x6739, "MT6739" },
        { 0x6750, "MT6750" },
        { 0x6752, "MT6752" },
        { 0x6753, "MT6753" },
        { 0x6755, "MT6755" },
        { 0x6757, "MT6757" },
        { 0x6758, "MT6758" },
        { 0x6761, "MT6761" },
        { 0x6762, "MT6762" },
        { 0x6763, "MT6763" },
        { 0x6765, "MT6765" },
        { 0x6768, "MT6768" },
        { 0x6771, "MT6771" },
        { 0x6779, "MT6779" },
        { 0x6781, "MT6781" },
        { 0x6785, "MT6785" },
        { 0x6787, "MT6787" },
        { 0x6789, "MT6789" },
        { 0x6795, "MT6795" },
        { 0x6797, "MT6797" },
        { 0x6799, "MT6799" },

        // MT68xx series (newer chips)
        { 0x6833, "MT6833" },
        { 0x6835, "MT6835" },
        { 0x6853, "MT6853" },
        { 0x6873, "MT6873" },
        { 0x6875, "MT6875" },
        { 0x6877, "MT6877" },
        { 0x6885, "MT6885" },
        { 0x6889, "MT6889" },
        { 0x6891, "MT6891" },
        { 0x6893, "MT6893" },
        { 0x6895, "MT6895" },
        { 0x6897, "MT6897" },

        // MT81xx series (tablet processors)
        { 0x8121, "MT8121" },
        { 0x8127, "MT8127" },
        { 0x8135, "MT8135" },
        { 0x8150, "MT8150" },
        { 0x8163, "MT8163" },
        { 0x8167, "MT8167" },
        { 0x8168, "MT8168" },
        { 0x8173, "MT8173" },
        { 0x8176, "MT8176" },

        // MT87xx series (premium tablets)
        { 0x8765, "MT8765" },
        { 0x8766, "MT8766" },
        { 0x8768, "MT8768" },
        { 0x8786, "MT8786" },
        { 0x8788, "MT8788" },

        // MT96xx series (flagship chips)
        { 0x9650, "MT9650" },
        { 0x9660, "MT9660" },
        { 0x9667, "MT9667" },
        { 0x9668, "MT9668" },
        { 0x9669, "MT9669" },

        // Special cases
        { 0x2601, "MT2601" },  // Wearable chip
        { 0x3622, "MT3622" },  // IoT chip
        { 0x7668, "MT7668" },  // Connectivity chip

        { 0x6899, "MT6899" }, // MT6899 (Dimensity 9000+)
        { 0x6983, "MT6983" }, // Dimensity 9200
        { 0x6855, "MT6855" }, // MT6855 (Dimensity 8200)

        { 0x01C1, "MTK_BootROM_C1" }     // Possible bootrom response
    };

        public MTKChipDetector(Stream stream, Action<string> logger, CancellationToken ct)
        {
            _stream = stream;
            _logger = logger ?? (_ => { });
            _ct = ct;
        }

        public async Task<ChipDetectionResult> DetectChipAdvancedAsync()
        {
            var results = new List<ChipDetectionResult>();

            // Stage 1: Standard HW_CODE detection
            results.Add(await TryStandardDetection());

            // Stage 2: Extended command probing
            results.Add(await TryExtendedCommands());

            // Stage 3: BootROM identification
            results.Add(await TryBootRomIdentification());

            // Analyze results
            return AnalyzeResults(results);
        }

        private async Task<ChipDetectionResult> TryStandardDetection()
        {
            try
            {
                byte[] readHwCodeCmd = { 0xFD, 0xD0 };
                await _stream.WriteAsync(readHwCodeCmd, 0, readHwCodeCmd.Length, _ct);
                await Task.Delay(300, _ct);

                byte[] buffer = new byte[256];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, _ct);

                if (bytesRead >= 2)
                {
                    ushort hwCode = BitConverter.ToUInt16(buffer, 0);
                    return InterpretResponse(hwCode, buffer, "Standard HW_CODE");
                }
            }
            catch (Exception ex)
            {
                _logger($"Standard detection failed: {ex.Message}");
            }
            return new ChipDetectionResult { Error = "Standard detection failed" };
        }

        private async Task<ChipDetectionResult> TryExtendedCommands()
        {
            var extendedCommands = new Dictionary<string, byte[]>
        {
            { "DA_Identification", new byte[] { 0xDA, 0xDA } },
            { "Secure_Chip_ID", new byte[] { 0xA5, 0x5A } },
            { "Factory_Mode", new byte[] { 0xF0, 0x0F } }
        };

            foreach (var cmd in extendedCommands)
            {
                try
                {
                    await _stream.WriteAsync(cmd.Value, 0, cmd.Value.Length, _ct);
                    await Task.Delay(500, _ct);

                    byte[] buffer = new byte[256];
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, _ct);

                    if (bytesRead >= 2)
                    {
                        ushort response = BitConverter.ToUInt16(buffer, 0);
                        //_logger($"Extended command {cmd.Key} response: 0x{response:X4}");

                        if (bytesRead > 2)
                        {
                            string additionalData = BitConverter.ToString(buffer, 2, Math.Min(bytesRead - 2, 16));
                            _logger($"Additional data: {additionalData}");
                        }

                        return InterpretResponse(response, buffer, cmd.Key);
                    }
                }
                catch (Exception ex)
                {
                    //_logger($"Extended command {cmd.Key} failed: {ex.Message}");
                }
            }
            return new ChipDetectionResult { Error = "Extended commands failed" };
        }

        private ChipDetectionResult InterpretResponse(ushort code, byte[] rawData, string source)
        {
            if (HwCodeToChipName.TryGetValue(code, out string chipName))
            {
                return new ChipDetectionResult
                {
                    ChipName = chipName,
                    HwCode = code,
                    RawHex = BitConverter.ToString(rawData, 0, Math.Min(rawData.Length, 16)),
                    SourceCommand = source,
                    IsVerified = true
                };
            }

            // Special handling for observed but undocumented codes
            switch (code)
            {
                

                case 0xD2FF:
                    return new ChipDetectionResult
                    {
                        ChipName = "MT6769V Helio G70,Helio",
                        HwCode = code,
                        RawHex = BitConverter.ToString(rawData, 0, Math.Min(rawData.Length, 16)),
                        SourceCommand = source,
                        Notes = "Possible secondary processor response"
                    };

                case 0xD1FE:
                    return new ChipDetectionResult
                    {
                        ChipName = "MT6769V Helio G70,Helio",
                        HwCode = code,
                        RawHex = BitConverter.ToString(rawData, 0, Math.Min(rawData.Length, 16)),
                        //SourceCommand = source,
                        Notes = "Possible secondary processor response"
                    };


                default:
                    return new ChipDetectionResult
                    {
                        ChipName = $"Unknown_0x{code:X4}",
                        HwCode = code,
                        RawHex = BitConverter.ToString(rawData, 0, Math.Min(rawData.Length, 16)),
                        //SourceCommand = source,
                        Error = "Unknown response code"
                    };
            }
        }

        //Extended command DA_Identification response: 0xDBDB BootROM identification response: 0x554E Additional BootROM data: 6C-6DChip Detected: MT6769V Helio G70,Helio
//HWID: 0xD1FE
//Notes: Identification based on heuristic analysis


        private async Task<ChipDetectionResult> TryBootRomIdentification()
        {
            try
            {
                // Common BootROM handshake command for MTK chips
                byte[] bootRomCommand = { 0x4D, 0x54, 0x6B, 0x6C }; // ASCII "MTkl"
                await _stream.WriteAsync(bootRomCommand, 0, bootRomCommand.Length, _ct);
                await Task.Delay(500, _ct);

                byte[] buffer = new byte[256];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, _ct);

                if (bytesRead >= 2)
                {
                    ushort response = BitConverter.ToUInt16(buffer, 0);
                    _logger($"BootROM identification response: 0x{response:X4}");

                    if (bytesRead > 2)
                    {
                        string additionalData = BitConverter.ToString(buffer, 2, Math.Min(bytesRead - 2, 16));
                        _logger($"Additional BootROM data: {additionalData}");
                    }

                    return InterpretResponse(response, buffer, "BootROM_Mode");
                }
                else
                {
                    // Check for ASCII signatures in raw response
                    string asciiResponse = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    if (asciiResponse.Contains("USB_DOWNLOAD_AGENT"))
                    {
                        return new ChipDetectionResult
                        {
                            ChipName = "MTK_BootROM_Generic",
                            HwCode = 0x0000,
                            RawHex = BitConverter.ToString(buffer, 0, Math.Min(bytesRead, 16)),
                            SourceCommand = "BootROM_Mode",
                            IsVerified = true,
                            Notes = "Device in USB Download Agent mode (BootROM)"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger($"BootROM identification failed: {ex.Message}");
            }

            return new ChipDetectionResult
            {
                Error = "BootROM identification failed",
                SourceCommand = "BootROM_Mode"
            };
        }

        private ChipDetectionResult AnalyzeResults(List<ChipDetectionResult> results)
        {
            // Return first verified result
            var verified = results.FirstOrDefault(r => r.IsVerified);
            if (verified != null) return verified;

            // Return result with most information
            var bestResult = results.OrderByDescending(r =>
                (r.RawHex?.Length ?? 0) +
                (string.IsNullOrEmpty(r.Error) ? 1 : 0))
                .First();

            bestResult.Notes = "Identification based on heuristic analysis";
            return bestResult;
        }
    }

    public class ChipDetectionResult
    {
        public string ChipName { get; set; }
        public ushort HwCode { get; set; }
        public string RawHex { get; set; }
        public string SourceCommand { get; set; }
        public string Error { get; set; }
        public string Notes { get; set; }
        public bool IsVerified { get; set; }
    }

}
