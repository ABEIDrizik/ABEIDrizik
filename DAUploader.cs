using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTKDeviceManager
{
    /// <summary>
    /// Provides basic functionality to upload data (like a Download Agent) in chunks over a serial port.
    /// This class was intended for raw data transmission but lacks the specific protocol handling
    /// (e.g., MTK BootROM handshakes, acknowledgments, command sequences) required for
    /// reliable DA uploading in the context of this application.
    /// The more specialized logic in AuthDeviceHandler.UploadDAAsync should be used instead.
    /// </summary>
    [Obsolete("This class is replaced by more specific DA upload logic in AuthDeviceHandler.UploadDAAsync, which includes full MTK protocol handling.")]
    public class DAUploader
    {
        private readonly SerialPort _serialPort;
        private readonly int _chunkSize;
        private readonly Action<string> _logAction;

        public DAUploader(SerialPort serialPort, Action<string> logAction, int chunkSize = 1024)
        {
            _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
            _chunkSize = chunkSize;
            _logAction = logAction ?? Console.WriteLine;
        }

        /// <summary>
        /// Asynchronously uploads the provided byte array (DA binary) to the connected serial port.
        /// This method only performs chunked raw data transfer without MTK-specific protocol steps.
        /// </summary>
        /// <param name="daBytes">The byte array containing the Download Agent binary.</param>
        /// <returns>A task representing the asynchronous upload operation.</returns>
        /// <exception cref="IOException">If the serial port is not open or an IO error occurs.</exception>
        /// <exception cref="ArgumentNullException">If daBytes is null.</exception>
        [Obsolete("Use AuthDeviceHandler.UploadDAAsync for MTK-specific DA uploading.")]
        public async Task UploadDAAsync(byte[] daBytes)
        {
            if (daBytes == null)
                throw new ArgumentNullException(nameof(daBytes));
            if (!_serialPort.IsOpen)
                throw new IOException("Serial port is not open.");

            int offset = 0;
            int total = daBytes.Length;
            Stream stream = _serialPort.BaseStream;

            try
            {
                _logAction("⬆️ Starting generic DA upload in chunks (obsolete method)...");

                while (offset < total)
                {
                    int length = Math.Min(_chunkSize, total - offset);
                    await stream.WriteAsync(daBytes, offset, length);
                    await stream.FlushAsync(); // Ensure data is sent immediately
                    offset += length;

                    _logAction($"📤 Uploaded {offset}/{total} bytes (generic uploader)...");
                    await Task.Delay(5); // Short delay, potentially for stability with some hardware
                }

                _logAction("✅ Generic DA upload complete (obsolete method)!");
            }
            catch (IOException ex)
            {
                _logAction($"❌ IO Error during generic DA upload (obsolete method): {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logAction($"❌ Unexpected error during generic DA upload (obsolete method): {ex.Message}");
                throw;
            }
        }
    }
}
