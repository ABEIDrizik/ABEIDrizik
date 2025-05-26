using System;
using System.Management;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MTKDeviceManager
{
    /// <summary>
    /// Provides functionality to detect Mediatek Preloader/BROM devices connected to the system.
    /// </summary>
    public static class MediatekDeviceDetector // Made static as it only contains static methods
    {
        private const int DevicePollingIntervalMs = 500; // Interval for checking device presence
        private const string WmiQueryScope = "SELECT DeviceID, Caption FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_0E8D&PID_2000%' OR DeviceID LIKE '%VID_0E8D&PID_0003%'";
        // Common Mediatek VID. PID_2000 for Preloader, PID_0003 for BROM (can vary).

        /// <summary>
        /// Asynchronously detects a Mediatek Preloader or BROM device.
        /// </summary>
        /// <param name="progress">An optional progress reporter for status updates.</param>
        /// <param name="timeoutSeconds">The maximum time in seconds to wait for a device.</param>
        /// <param name="externalToken">An optional external CancellationToken to allow cancellation from outside.</param>
        /// <returns>
        /// A string containing the COM port of the detected device (e.g., "COM5"),
        /// or a status message indicating timeout, cancellation, or failure.
        /// </returns>
        public static async Task<string> DetectMediatekPreloaderAsync(
            IProgress<string> progress = null,
            int timeoutSeconds = 30,
            CancellationToken externalToken = default)
        {
            // Combine timeout with external token for comprehensive cancellation control.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken, timeoutCts.Token);
            var cancellationToken = linkedCts.Token;

            try
            {
                progress?.Report("ℹ️ Scanning for Mediatek Preloader/BROM devices...\n");
                DateTime startTime = DateTime.Now;

                while (!cancellationToken.IsCancellationRequested)
                {
                    double elapsedSeconds = (DateTime.Now - startTime).TotalSeconds;
                    progress?.Report($"\rℹ️ Waiting for Preloader Device(s)... ({elapsedSeconds:0.0}s / {timeoutSeconds}s) ");

                    // Task.Run to offload the potentially blocking WMI query from the calling thread (if it's UI thread).
                    string devicePort = await Task.Run(() => FindMediatekDevice(progress, cancellationToken), cancellationToken);
                    
                    if (!string.IsNullOrEmpty(devicePort))
                    {
                        progress?.Report($"\r" + new string(' ', 60) + "\r"); // Clear the waiting line
                        progress?.Report($"✅ Mediatek Preloader device detected on: {devicePort}\n");
                        return devicePort;
                    }
                    
                    await Task.Delay(DevicePollingIntervalMs, cancellationToken); 
                }
                
                progress?.Report("\r" + new string(' ', 60) + "\r"); // Clear the waiting line
                if (timeoutCts.IsCancellationRequested && !externalToken.IsCancellationRequested)
                {
                    progress?.Report("❌ No Mediatek Preloader device detected within the timeout period.\n");
                    return "No Mediatek device detected (Timeout)";
                }
                if (externalToken.IsCancellationRequested)
                {
                     progress?.Report("🚫 Device detection was cancelled by the user.\n");
                     return "No Mediatek device detected (Cancelled)";
                }
                
                progress?.Report("❌ No Mediatek Preloader device detected (unknown reason for loop exit).\n");
                return "No Mediatek device detected";
            }
            catch (ManagementException mgmtEx)
            {
                progress?.Report($"\n❌ WMI Error during device detection: {mgmtEx.Message}. Ensure WMI service is running and accessible.\n");
                return "Detection failed (WMI Error)";
            }
            catch (OperationCanceledException) // Catches cancellation from linkedCts more reliably
            {
                progress?.Report("\r" + new string(' ', 60) + "\r"); // Clear the waiting line
                if (timeoutCts.IsCancellationRequested && !externalToken.IsCancellationRequested)
                {
                    progress?.Report("❌ No Mediatek Preloader device detected within the timeout period (OperationCanceledException).\n");
                    return "No Mediatek device detected (Timeout)";
                }
                progress?.Report("🚫 Device detection was cancelled by the user (OperationCanceledException).\n");
                return "No Mediatek device detected (Cancelled)";
            }
            catch (Exception ex)
            {
                progress?.Report($"\n❌ An unexpected error occurred during device detection: {ex.GetType().Name} - {ex.Message}\n");
                return "Detection failed (Unexpected Error)";
            }
            finally
            {
                 // Ensure the "Waiting..." line is cleared if progress is used.
                 // The \r in progress report should handle most cases, but this is a fallback.
                 // progress?.Report("\r" + new string(' ', 70) + "\r"); 
            }
        }

        /// <summary>
        /// Finds a Mediatek device by querying WMI for PnP entities.
        /// </summary>
        /// <param name="progress">Optional progress reporter for logging issues within this method.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>The COM port string if a device is found; otherwise, null.</returns>
        private static string FindMediatekDevice(IProgress<string> progress, CancellationToken cancellationToken)
        {
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    progress?.Report("⚠️ Mediatek device detection via WMI is Windows-specific. This method will not work on other platforms.\n");
                    return null;
                }
                
                cancellationToken.ThrowIfCancellationRequested();

                using var searcher = new ManagementObjectSearcher(WmiQueryScope);
                // ManagementObjectCollection is IDisposable, so Get() should be within a using or iterated carefully.
                // However, searcher.Get() itself returns the collection, and obj is disposed in the loop.
                // For simplicity and common practice, direct iteration is often used, relying on obj disposal.
                foreach (ManagementBaseObject pnpObject in searcher.Get()) 
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using (var obj = (ManagementObject)pnpObject) // Explicitly cast and use 'using' for each ManagementObject
                    {
                        string deviceId = obj["DeviceID"]?.ToString() ?? "";
                        string caption = obj["Caption"]?.ToString() ?? "";

                        // DeviceID check is already part of WMI query, but can be double-checked if needed.
                        // The primary goal here is to extract the COM port from the Caption.
                        int startIndex = caption.LastIndexOf("(COM", StringComparison.OrdinalIgnoreCase);
                        if (startIndex >= 0)
                        {
                            int endIndex = caption.IndexOf(")", startIndex, StringComparison.OrdinalIgnoreCase);
                            if (endIndex > startIndex)
                            {
                                return caption.Substring(startIndex + 1, endIndex - startIndex - 1); // e.g., "COM5"
                            }
                        }
                    }
                }
            }
            catch (ManagementException mgmtEx) 
            {
                progress?.Report($"❌ WMI Query Error in FindMediatekDevice: {mgmtEx.Message}. WMI might be corrupted or service stopped.\n");
            }
            catch (OperationCanceledException)
            {
                progress?.Report("🚫 Search for Mediatek device was cancelled within FindMediatekDevice.\n");
                // Re-throw if this method is expected to honor cancellation by throwing.
                // For now, returning null as per original logic flow for "not found".
            }
            catch (Exception ex) 
            {
                progress?.Report($"❌ Unexpected error in FindMediatekDevice: {ex.GetType().Name} - {ex.Message}\n");
            }
            return null;
        }
    }
}
