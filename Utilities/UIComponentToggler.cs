using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace ShowRatesLoggerGUI.Utilities
{
    internal class UIComponentToggler
    {
        private readonly MainWindow _window;

        public UIComponentToggler(MainWindow window)
        {
            _window = window;
        }

        public void ToggleLoggingUI(bool isLogging)
        {
            var isEnabled = !isLogging;

            var controlsToToggle = new Control[]
            {
                _window.IPAddressInput,
                _window.ConnectButton,

                _window.RunLoggingByIntervalInput,
                _window.ShowAllSourceRatesCheckbox,
                _window.CsvOutputCheckbox,
                _window.RCTNotificationsCheckbox,

                _window.RunLoggingByIntervalSection,
                _window.RunLoggingByIntervalCheckbox,
                _window.ShowRatesFetchIntervalInput,

                _window.RCTNotificationsSection,
                _window.RenderNotificationsEnabled,
                _window.CaptureNotificationsEnabled,
                _window.TransferNotificationsEnabled,
                _window.RenderNotificationSetting,
                _window.CaptureNotificationSetting,
                _window.TransferNotificationSetting,
            };

            foreach (var control in controlsToToggle) 
                control.IsEnabled = isEnabled;

            _window.RunButton.Content = isLogging ? "Stop" : "Run";

            SetControlState(_window.OpenFileButton, isLogging);
        }

        private void SetControlState(Control control, bool state)
        {
            control.IsEnabled = state;
            control.IsVisible= state;
        }
    }
}
