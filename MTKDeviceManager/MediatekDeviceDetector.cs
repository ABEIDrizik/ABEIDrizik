using System;
using System.Management;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MTKDeviceManager
{
    public class MediatekDeviceDetector
    {
        public static async Task<string> DetectMediatekPreloaderAsync(
            IProgress<string> progress = null,
            int timeoutSeconds = 30)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                return await Task.Run(() =>
                {
                    progress?.Report("Scanning for Mediatek Preloader/BROM devices...");
                    DateTime startTime = DateTime.Now;

                    while (!cts.Token.IsCancellationRequested)
                    {
                        double elapsed = (DateTime.Now - startTime).TotalSeconds;
                        progress?.Report($"\rWaiting Preloader Device(s)... ({elapsed:0.0}s)");

                        var device = FindMediatekDevice();
                        if (device != null)
                        {
                            progress?.Report($"\nDetected Device: {device}");
                            return device;
                        }

                        Thread.Sleep(500);
                    }

                    progress?.Report("\nNo Mediatek device detected within timeout period.");
                    return "No Mediatek device detected";
                }, cts.Token);
            }
            catch (Exception ex)
            {
                progress?.Report($"\nDetection error: {ex.Message}");
                return "Detection failed";
            }
        }

        private static string FindMediatekDevice()
        {
            try
            {
                // Ensure this code runs on Windows where ManagementObjectSearcher is available.
                // Add a reference to System.Management if not already present in the project.
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_0E8D%'");

                    foreach (var obj in searcher.Get().OfType<ManagementObject>())
                    {
                        string deviceId = obj["DeviceID"]?.ToString() ?? "";
                        string caption = obj["Caption"]?.ToString() ?? "";

                        if (deviceId.Contains("VID_0E8D&PID_2000") || deviceId.Contains("VID_0E8D&PID_0003"))
                        {
                            //so no need to append again
                            return caption;
                        }
                    }
                }
                else
                {
                    // Handle non-Windows platforms or log that this feature is Windows-specific.
                    // For now, returning null if not on Windows.
                    Console.WriteLine("Mediatek device detection via WMI is Windows-specific.");
                }
            }
            catch (Exception ex) 
            {
                // Log the exception or report it through progress.
                Console.WriteLine($"Error in FindMediatekDevice: {ex.Message}");
            }

            return null;
        }
    }
}
