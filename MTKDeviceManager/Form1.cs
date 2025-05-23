using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MTKDeviceManager
{
    /// <summary>
    /// Main form for the MTK Device Manager application.
    /// Provides a user interface to interact with MTK devices via the <see cref="MTKDeviceCommunicator"/>.
    /// </summary>
    public partial class Form1 : Form
    {
        /// <summary>
        /// Instance of the MTKDeviceCommunicator used to perform device operations.
        /// </summary>
        private MTKDeviceCommunicator _communicator;

        /// <summary>
        /// Initializes a new instance of the <see cref="Form1"/> class.
        /// Sets up UI components and initializes the <see cref="MTKDeviceCommunicator"/>.
        /// </summary>
        public Form1()
        {
            InitializeComponent();
            _communicator = new MTKDeviceCommunicator();
            // Subscribe to the LogMessage event from the communicator to display logs in the UI.
            _communicator.LogMessage += LogMessageHandler;
        }

        /// <summary>
        /// Handles messages logged by the <see cref="MTKDeviceCommunicator"/>.
        /// Appends messages to the RichTextBox in a thread-safe manner.
        /// </summary>
        /// <param name="message">The log message to display.</param>
        private void LogMessageHandler(string message)
        {
            // Check if this method is called from a different thread than the UI thread.
            if (richTextBoxLog.InvokeRequired)
            {
                // If so, use Invoke to marshal the call to the UI thread.
                // This prevents cross-threading exceptions when updating UI controls.
                richTextBoxLog.Invoke(new Action<string>(LogMessageHandler), message);
            }
            else
            {
                // If already on the UI thread, directly append the message.
                richTextBoxLog.AppendText(message + Environment.NewLine);
                richTextBoxLog.ScrollToCaret(); // Ensure the latest log message is visible.
            }
        }

        /// <summary>
        /// Event handler for the 'Start MTK Operations' button click.
        /// Initiates a sequence of asynchronous operations on an MTK device.
        /// </summary>
        /// <param name="sender">The source of the event (the button).</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private async void btnStartOperations_Click(object sender, EventArgs e)
        {
            // Disable the button to prevent multiple concurrent operations.
            btnStartOperations.Enabled = false;
            LogMessageHandler("Starting MTK operations...");

            try
            {
                // 1. Detect the device
                DeviceInfo detectedDevice = await _communicator.DetectDeviceAsync();
                if (detectedDevice != null)
                {
                    // 2. Connect to DA/SLAA if device detected
                    bool connected = await _communicator.ConnectDaSlaaAsync(detectedDevice);
                    if (connected)
                    {
                        // 3. Hold connection (simulated)
                        await _communicator.HoldConnectionAsync(detectedDevice);

                        // 4. Get detailed device information
                        string info = await _communicator.GetDeviceInfoAsync(detectedDevice);
                        LogMessageHandler($"Detailed Device Info:\n{info}");

                        // 5. Attempt FRP (Factory Reset Protection) removal
                        LogMessageHandler("Attempting FRP operation...");
                        bool frpSuccess = await _communicator.ExecuteFrpAsync(detectedDevice);
                        LogMessageHandler(frpSuccess ? "FRP operation successful." : "FRP operation failed.");

                        // 6. Attempt Factory Reset
                        LogMessageHandler("Attempting Factory Reset operation...");
                        bool factoryResetSuccess = await _communicator.ExecuteFactoryResetAsync(detectedDevice);
                        LogMessageHandler(factoryResetSuccess ? "Factory Reset successful." : "Factory Reset failed.");
                    }
                    else
                    {
                        LogMessageHandler("Failed to connect to DA/SLAA. Aborting further operations.");
                    }
                }
                else
                {
                    LogMessageHandler("No device detected. Aborting operations.");
                }
            }
            catch (Exception ex)
            {
                // Log any unexpected exceptions that occur during the operations.
                LogMessageHandler($"An unexpected error occurred in UI: {ex.Message} - StackTrace: {ex.StackTrace}");
            }
            finally
            {
                // This block executes regardless of whether an exception occurred or not.
                LogMessageHandler("All operations attempted.");
                // Re-enable the button so the user can try again if needed.
                btnStartOperations.Enabled = true;
            }
        }
    }
}
