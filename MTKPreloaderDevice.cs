using System;
using System.IO;
using System.IO.Ports;
using LibUsbDotNet;
using LibUsbDotNet.Main;

namespace MTKDeviceManager
{
    public class MTKPreloaderDevice
    {
        private readonly string _portName;
        private SerialPort _serialPort;

        public MTKPreloaderDevice(string portName)
        {
            _portName = portName ?? throw new ArgumentNullException(nameof(portName));
        }

        public Stream OpenSerialStream()
        {
            _serialPort = new SerialPort(_portName, 115200, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 3000,
                WriteTimeout = 3000,
                Handshake = Handshake.None
            };

            _serialPort.Open();
            return _serialPort.BaseStream;
        }

        public void Close()
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }
            }
            catch { }
        }
    }

}
