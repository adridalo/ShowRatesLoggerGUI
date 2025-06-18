using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Toolkit.Uwp.Notifications;
using ShowRatesLoggerGUI.Models;
using ShowRatesLoggerGUI.Utilities;

namespace ShowRatesLoggerGUI.Services
{
    internal class MonitoringService : IDisposable
    {
        private string _logFilePath;
        private DispatcherTimer _timer;
        private Stopwatch _stopwatch;
        private readonly Action<string, IBrush> _updateRunStatus;
        private readonly Action<string> _updateCurrentRates;
        private UIComponentToggler _toggler;
        private readonly Action _onStop;
        public bool IsRunning { get; private set; }
        public string CurrentRates {  get; private set; }

        public MonitoringService(Action<string, IBrush> updateRunStatus, Action onStop, Action<string> updateCurrentRates, UIComponentToggler toggler)
        {
            this._updateRunStatus = updateRunStatus;
            this._onStop = onStop;
            this._updateCurrentRates = updateCurrentRates;
            this._toggler = toggler;
        }

        public async void Start(
            TelnetService telnetService,
            double fetchInterval,
            double? durationMinutes,
            string ip,
            bool showAll,
            bool csvOutput,
            RateData rctNotificationsSettings
        )
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"ShowRatesLoggerGUI_{ip}");
            Directory.CreateDirectory( logDir );
            var now = DateTime.Now.ToString("yyyy-MM-dd_HH_mm_ss");
            _logFilePath = Path.Combine(logDir, $"{now}-RatesAverage{(csvOutput ? ".csv" : ".txt")}");

            if(csvOutput)
                File.WriteAllText(_logFilePath, $"Time,Window #,Render,Capture,Transfer{Environment.NewLine}");
            else
                File.WriteAllText(_logFilePath, $"Started at {DateTime.Now}{Environment.NewLine}");

            _stopwatch = Stopwatch.StartNew();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(fetchInterval),
            };

            // Log once immediately
            var firstResponse = await telnetService.SendCommand("***showrates*** terminal");
            var firstResponseRates = RateParser.Parse(firstResponse);
            LogUtility.Log(_logFilePath, firstResponse, firstResponseRates, showAll, csvOutput);

            _timer.Tick += async (s, e) =>
            {
                if (durationMinutes.HasValue && _stopwatch.Elapsed.TotalMinutes >= durationMinutes.Value)
                {
                    _updateCurrentRates(string.Empty);
                    _toggler.ToggleLoggingUI(false);
                    Stop();
                    _onStop?.Invoke();
                    return;
                }

                var response = await telnetService.SendCommand("***showrates*** terminal");
                var currentRates = RateParser.Parse(response);

                if (rctNotificationsSettings != null)
                    RCTNotificationsHandling(currentRates, rctNotificationsSettings);

                CurrentRates = RateParser.ShowRatesCleanOutput(response);
                _updateCurrentRates?.Invoke(CurrentRates);
                LogUtility.Log(_logFilePath, response, currentRates, showAll, csvOutput);
            };

            _timer.Start();
            IsRunning = true;
            _updateRunStatus("Running...", Brushes.Orange);
        }

        private void RCTNotificationsHandling(RateData currentRates, RateData rctNotificationSettings)
        {
            // Render check
            if (rctNotificationSettings.RenderNotificationsEnabled && currentRates.Render < rctNotificationSettings.Render)
            {
                new ToastContentBuilder()
                .AddText($"Render has gone below {rctNotificationSettings.Render}!")
                .Show();
            }
            // Capture check
            if (rctNotificationSettings.CaptureNotificationsEnabled && currentRates.Capture < rctNotificationSettings.Capture)
            {
                new ToastContentBuilder()
                .AddText($"Capture has gone below {rctNotificationSettings.Capture}")
                .Show();
            }
            // Transfer
            if (rctNotificationSettings.TransferNotificationsEnabled && currentRates.Transfer < rctNotificationSettings.Transfer)
            {
                new ToastContentBuilder()
                .AddText($"Transfer has gone below {rctNotificationSettings.Transfer}")
                .Show();
            }
        }

        public void Stop(string reason = null)
        {
            _timer?.Stop();
            IsRunning = false;
            _updateRunStatus(reason ?? $"Stopped after {MinutesToHumanReadableTime(_stopwatch)}", Brushes.White);
        }

        private string MinutesToHumanReadableTime(Stopwatch totalTime)
        {
            if (totalTime.Elapsed.TotalMinutes < 1) return $"{totalTime.Elapsed.TotalSeconds:F2} seconds.";
            if (totalTime.Elapsed.TotalMinutes >= 1 && totalTime.Elapsed.TotalMinutes < 60) return $"{totalTime.Elapsed.TotalMinutes:F2} minutes.";
            if (totalTime.Elapsed.TotalMinutes >= 60 && totalTime.Elapsed.TotalMinutes < 1440) return $"{totalTime.Elapsed.TotalHours:F2} hours.";
            if (totalTime.Elapsed.TotalMinutes >= 1440) return $"{totalTime.Elapsed.TotalDays:F2} days.";
            else return string.Join("", totalTime.Elapsed.TotalMinutes);
        }

        public void OpenLogFile()
        {
            if(File.Exists(_logFilePath)) 
                Process.Start(new ProcessStartInfo { FileName = _logFilePath, UseShellExecute = true });
        }

        public void Dispose()
        {
            _timer?.Stop();
        }
    }
}
