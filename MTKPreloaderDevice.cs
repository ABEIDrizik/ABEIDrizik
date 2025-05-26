using System;
using System.IO;
using System.IO.Ports;
using System.Threading; // Required for CancellationToken (though not directly used in this version, good for future)

namespace MTKDeviceManager
{
    /// <summary>
    /// Represents a connection to an MTK Preloader device via a serial port.
    /// Provides methods to open and close the serial communication stream.
    /// </summary>
    public class MTKPreloaderDevice : IDisposable
    {
        private readonly string _portName;
        private SerialPort _serialPort;
        private readonly IProgress<string> _logger; 

        private const int DefaultBaudRate = 115200;
        private const Parity DefaultParity = Parity.None;
        private const int DefaultDataBits = 8;
        private const StopBits DefaultStopBits = StopBits.One;
        private const int DefaultReadTimeoutMs = 5000;
        private const int DefaultWriteTimeoutMs = 5000;

        private bool _isDisposed = false; // To detect redundant calls

        /// <summary>
        /// Initializes a new instance of the <see cref="MTKPreloaderDevice"/> class.
        /// </summary>
        /// <param name="portName">The name of the serial port (e.g., "COM5").</param>
        /// <param name="logger">An optional progress reporter for logging messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if portName is null or empty.</exception>
        public MTKPreloaderDevice(string portName, IProgress<string> logger = null)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentNullException(nameof(portName), "Port name cannot be null or empty.");
            
            _portName = portName;
            _logger = logger; 
        }

        /// <summary>
        /// Opens the serial port and returns the underlying stream for communication.
        /// </summary>
        /// <returns>A <see cref="Stream"/> object for device communication, or null if the port cannot be opened.</returns>
        public Stream OpenSerialStream()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(MTKPreloaderDevice), "Cannot open stream on a disposed device object.");
            if (_serialPort?.IsOpen == true)
            {
                _logger?.Report($"ℹ️ Serial port {_portName} is already open.\n");
                return _serialPort.BaseStream;
            }

            try
            {
                _serialPort = new SerialPort(_portName, DefaultBaudRate, DefaultParity, DefaultDataBits, DefaultStopBits)
                {
                    ReadTimeout = DefaultReadTimeoutMs,
                    WriteTimeout = DefaultWriteTimeoutMs,
                    Handshake = Handshake.None, // Typically None for MTK Preloader
                    DtrEnable = true, 
                    RtsEnable = true  
                };
                
                _logger?.Report($"ℹ️ Opening serial port {_portName} at {DefaultBaudRate} baud...\n");
                _serialPort.Open();
                _logger?.Report($"✅ Serial port {_portName} opened successfully.\n");
                return _serialPort.BaseStream;
            }
            catch (IOException ioEx)
            {
                _logger?.Report($"❌ IOException opening serial port {_portName}: {ioEx.Message}\n");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                _logger?.Report($"❌ UnauthorizedAccessException opening serial port {_portName}: {uaEx.Message}. Check permissions.\n");
            }
            catch (ArgumentOutOfRangeException argEx)
            {
                 _logger?.Report($"❌ ArgumentOutOfRangeException for serial port {_portName}: {argEx.Message}. Check port parameters.\n");
            }
            catch (InvalidOperationException invOpEx)
            {
                _logger?.Report($"❌ InvalidOperationException opening serial port {_portName}: {invOpEx.Message}. Port might be in an invalid state or already open by another process.\n");
            }
            catch (Exception ex)
            {
                _logger?.Report($"❌ An unexpected error occurred opening serial port {_portName}: {ex.GetType().Name} - {ex.Message}\n");
            }
            
            // Ensure serial port is cleaned up if open failed partway
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen) _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }
            return null; 
        }

        /// <summary>
        /// Closes the serial port if it is open.
        /// </summary>
        public void Close()
        {
            if (_isDisposed) return; // Don't do anything if already disposed

            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _logger?.Report($"ℹ️ Closing serial port {_portName}.\n");
                    _serialPort.Close(); // Closes the port and the underlying stream.
                    _logger?.Report($"✅ Serial port {_portName} closed.\n");
                }
            }
            catch (IOException ioEx)
            {
                string errorMessage = $"⚠️ IOException during serial port {_portName} closing: {ioEx.Message}\n";
                _logger?.Report(errorMessage);
            }
            catch (Exception ex) 
            {
                string errorMessage = $"⚠️ Unexpected error during serial port {_portName} closing: {ex.GetType().Name} - {ex.Message}\n";
                _logger?.Report(errorMessage);
            }
            // Note: _serialPort.Dispose() is handled in the Dispose method
        }

        /// <summary>
        /// Releases all resources used by the <see cref="MTKPreloaderDevice"/> object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // Suppress finalization as resources are now managed.
        }

        /// <summary>
        /// Protected virtual method to dispose managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                // Dispose managed resources
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                    {
                        try { _serialPort.Close(); }
                        catch (Exception ex) { _logger?.Report($"⚠️ Exception during Dispose/Close of serial port: {ex.Message}\n"); }
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                    _logger?.Report($"ℹ️ Serial port {_portName} disposed.\n");
                }
            }
            // No unmanaged resources to free directly in this class (SerialPort handles its own)
            _isDisposed = true;
        }

        /// <summary>
        /// Finalizer for MTKPreloaderDevice.
        /// </summary>
        ~MTKPreloaderDevice()
        {
            Dispose(false); // Dispose only unmanaged resources (if any) from finalizer
        }
    }
}
