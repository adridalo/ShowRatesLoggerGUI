using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using ShowRatesLoggerGUI.Utilities;

namespace ShowRatesLoggerGUI.Services
{
    internal class MonitoringService : IDisposable
    {
        private string _logFilePath;
        private DispatcherTimer _timer;
        private Stopwatch _stopwatch;
        private readonly Action<string, IBrush> _updateRunStatus;
        public bool IsRunning { get; private set; }

        public MonitoringService(Action<string, IBrush> updateRunStatus)
        {
            _updateRunStatus = updateRunStatus;
        }

        public void Start(
            TelnetService telnetService,
            double fetchInterval,
            double? durationMinutes,
            string ip,
            bool showAll,
            bool csvOutput
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

            _timer.Tick += async (s, e) =>
            {
                if (durationMinutes.HasValue && _stopwatch.Elapsed.TotalMinutes >= durationMinutes.Value)
                {
                    Stop();
                    return;
                }

                var response = await telnetService.SendCommand("***showrates*** terminal");
                if (response.Contains("NotStarted"))
                {
                    Stop("Wall not started");
                    return;
                }

                var rates = RateParser.Parse(response);
                LogUtility.Log(_logFilePath, response, rates, showAll, csvOutput);
            };

            _timer.Start();
            IsRunning = true;
            _updateRunStatus("Running...", Brushes.Orange);
        }

        public void Stop(string reason = null)
        {
            _timer?.Stop();
            IsRunning = false;
            _updateRunStatus(reason ?? $"Stopped after {_stopwatch.Elapsed.TotalMinutes:F2} minutes", Brushes.White);
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
