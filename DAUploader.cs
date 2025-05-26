using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTKDeviceManager
{
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

        public async Task UploadDAAsync(byte[] daBytes)
        {
            if (!_serialPort.IsOpen)
                throw new IOException("Serial port is not open.");

            int offset = 0;
            int total = daBytes.Length;
            Stream stream = _serialPort.BaseStream;

            try
            {
                _logAction("⬆️ Starting DA upload in chunks...");

                while (offset < total)
                {
                    int length = Math.Min(_chunkSize, total - offset);
                    await stream.WriteAsync(daBytes, offset, length);
                    await stream.FlushAsync();
                    offset += length;

                    _logAction($"📤 Uploaded {offset}/{total} bytes...");
                    await Task.Delay(5); // short delay helps stability
                }

                _logAction("✅ DA upload complete!");
            }
            catch (IOException ex)
            {
                _logAction($"❌ IO Error during DA upload: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logAction($"❌ Unexpected error during DA upload: {ex.Message}");
                throw;
            }
        }
    }
}
