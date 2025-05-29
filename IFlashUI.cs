using System;

namespace SprdFlashTool.Core
{
    /// <summary>
    /// Defines the levels for logging messages.
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Interface for the UI to implement, allowing core logic to interact with the UI.
    /// </summary>
    public interface IFlashUI
    {
        /// <summary>
        /// Updates the progress display (e.g., a progress bar).
        /// </summary>
        /// <param name="percentage">The progress percentage (0-100).</param>
        void UpdateProgress(int percentage);

        /// <summary>
        /// Logs a message to the UI, with a specified severity level.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The severity level of the message.</param>
        void LogMessage(string message, LogLevel level);

        /// <summary>
        /// Sets the busy state of the UI, typically disabling controls during long operations.
        /// </summary>
        /// <param name="isBusy">True to set the UI to a busy state, false to return to normal.</param>
        void SetBusyState(bool isBusy);

        /// <summary>
        /// Reports a critical error to the user, potentially with additional details.
        /// </summary>
        /// <param name="errorMessage">The main error message to display.</param>
        /// <param name="details">Optional additional details about the error.</param>
        void ReportError(string errorMessage, string details = null);

        /// <summary>
        /// Prompts the user to select a file, typically a firmware file.
        /// </summary>
        /// <param name="filter">The filter string for the file dialog (e.g., "Firmware files (*.pac)|*.pac|All files (*.*)|*.*").</param>
        /// <returns>The path to the selected file, or null if the user cancels.</returns>
        string PromptForFile(string filter);

        /// <summary>
        /// Clears or resets the progress indication in the UI.
        /// </summary>
        void ClearProgress();
    }
}
