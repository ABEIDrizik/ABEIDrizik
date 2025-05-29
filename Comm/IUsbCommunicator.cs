using System;

namespace SprdFlashTool.Core.Comm
{
    /// <summary>
    /// Provides data for the <see cref="IUsbCommunicator.ErrorOccurred"/> event.
    /// </summary>
    public class UsbErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a message describing the error.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the exception associated with the error, if any.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UsbErrorEventArgs"/> class.
        /// </summary>
        /// <param name="message">A message describing the error.</param>
        /// <param name="exception">The exception associated with the error (optional).</param>
        public UsbErrorEventArgs(string message, Exception exception = null)
        {
            Message = message;
            Exception = exception;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="IUsbCommunicator.DataReceived"/> event.
    /// </summary>
    public class DataReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the data received from the USB device.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="data">The data received from the USB device.</param>
        public DataReceivedEventArgs(byte[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }
    }

    /// <summary>
    /// Defines the contract for a USB communication interface.
    /// This interface is responsible for handling low-level USB device interactions.
    /// </summary>
    public interface IUsbCommunicator
    {
        /// <summary>
        /// Connects to the Nth USB device matching the given Vendor ID (VID) and Product ID (PID).
        /// </summary>
        /// <param name="vid">The Vendor ID of the USB device.</param>
        /// <param name="pid">The Product ID of the USB device.</param>
        /// <param name="NthDevice">The zero-based index of the device to connect to if multiple matching devices are found.</param>
        /// <returns>True if the connection is successful, false otherwise.</returns>
        bool Connect(ushort vid, ushort pid, int NthDevice = 0);

        /// <summary>
        /// Disconnects from the currently connected USB device.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Sends data to the connected USB device, typically to an OUT endpoint.
        /// </summary>
        /// <param name="data">The byte array containing the data to send.</param>
        /// <returns>True if the data was sent successfully, false otherwise.</returns>
        bool Write(byte[] data);

        /// <summary>
        /// Reads data from the connected USB device, typically from an IN endpoint.
        /// This method should block until data is received or a timeout occurs.
        /// </summary>
        /// <param name="timeoutMilliseconds">The maximum time to wait for data, in milliseconds.</param>
        /// <returns>A byte array containing the received data, or null or an empty array on timeout or error.</returns>
        byte[] Read(int timeoutMilliseconds = 1000);

        /// <summary>
        /// Occurs when an asynchronous error is detected with the USB device (e.g., unexpected disconnection).
        /// </summary>
        event EventHandler<UsbErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Occurs when data is received asynchronously from the USB device.
        /// This is optional if the implementation primarily relies on synchronous reads via the <see cref="Read"/> method.
        /// </summary>
        event EventHandler<DataReceivedEventArgs> DataReceived;
    }
}
