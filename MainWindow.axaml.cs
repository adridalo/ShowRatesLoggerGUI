using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PrimS.Telnet;

namespace ShowRatesLoggerGUI;

public partial class MainWindow : Window
{
    private string _logFilePath;
    private Client _telnetClient;
    private const string ShowRatesTerminalCommand = "***showrates*** terminal\n\r";
    private const string StatusCommand = "status\n\r";
    private double _renderAverage;
    private double _captureAverage;
    private double _transferAverage;
    private double _fetchInterval;
    private int _windowsQuantity = 0;
    private DispatcherTimer _executionTimer;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isRunning;
    private bool _isConnected;
    private string _localHostIpAddress;

    public MainWindow()
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = $"advfirewall firewall add rule name=\"ShowRatesLoggerGUI Port\" dir=in action=allow protocol=TCP localport=23",
            Verb = "runas",
            UseShellExecute = true
        };

        try
        {
            Process.Start(psi)?.WaitForExit();
            System.Console.WriteLine($"Port 23 opened in firewall");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine("Failed to open port 23:", ex.Message);
        }
        InitializeComponent();
        InitializeUIState();
    }

    private void InitializeUIState()
    {
        IPAddressStatus.Text = "Disconnected";
        IPAddressStatus.Foreground = Brushes.Gray;
        RunStatus.Text = string.Empty;
        ShowRatesFetchIntervalSection.IsEnabled = false;
        ShowRatesFetchIntervalSection.IsVisible = false;
        RunButton.Content = "Run";
        RunButton.Foreground = Brushes.White;
    }

    public async void OnConnect(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(IPAddressInput.Text))
        {
            return;
        }

        if (IPAddressInput.Text.ToLower().Equals("localhost"))
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    _localHostIpAddress = ip.ToString();
                    break;
                }
            }
        }

        else if (!IPAddress.TryParse(IPAddressInput.Text, out _))
        {
            _localHostIpAddress = null;
            UpdateConnectionStatus("Invalid IP address", Brushes.Red);
            return;
        }

        UpdateConnectionStatus("Connecting...", Brushes.Orange);

        try
        {
            _localHostIpAddress = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            _telnetClient = new Client(_localHostIpAddress == null ? IPAddressInput.Text : _localHostIpAddress, 23, _cancellationTokenSource.Token);
            _isConnected = true;

            _logFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"{IPAddressInput.Text}-RatesAverage.txt");

            UpdateConnectionStatus("Connected!", Brushes.Green);
            ShowRatesFetchIntervalSection.IsEnabled = true;
            ShowRatesFetchIntervalSection.IsVisible = true;
            ShowRatesFetchIntervalInput.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _isConnected = false;
            UpdateConnectionStatus(@$"Failed to connect. Either:
- Device is unreachable
- Port 23 (telnet) is blocked
- Device is not running", Brushes.Red);
        }
    }

    public async void OnRun(object sender, RoutedEventArgs e)
    {
        if (!_isConnected)
        {
            UpdateRunStatus("Not connected", Brushes.Red);
            return;
        }

        if (!double.TryParse(ShowRatesFetchIntervalInput.Text, out _fetchInterval))
        {
            UpdateRunStatus("Invalid interval", Brushes.Red);
            return;
        }

        if (_executionTimer?.IsEnabled == true)
        {
            await _telnetClient.WriteLineAsync("***showrates*** on\n\r");
            StopMonitoring();
        }
        else
        {
            StartMonitoring();
        }
    }

    public void OpenLogFile(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_logFilePath))
            {
                UpdateRunStatus("No log file path set", Brushes.Orange);
                return;
            }

            if (!File.Exists(_logFilePath))
            {
                UpdateRunStatus("Log file doesn't exist yet", Brushes.Orange);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _logFilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            UpdateRunStatus($"Error opening file: {ex.Message}", Brushes.Red);
        }
    }

    private void StartMonitoring()
    {
        try
        {
            // Initialize/clear the log file
            _logFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"{IPAddressInput.Text}-RatesAverage.txt");

            if (File.Exists(_logFilePath))
                File.Delete(_logFilePath);

            // Create initial empty file
            File.WriteAllText(_logFilePath, $"ShowRatesLoggerGUI || {IPAddressInput.Text} || Started at {DateTime.Now + Environment.NewLine}");

            // Reset averages
            _renderAverage = 0;
            _captureAverage = 0;
            _transferAverage = 0;

            // Stop any existing timer
            _executionTimer?.Stop();

            // Execute first command immediately
            ExecuteCommandAsync();

            // Set up timer for subsequent executions
            _executionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_fetchInterval)
            };
            _executionTimer.Tick += async (s, e) =>
            {
                await ExecuteCommandAsync();
                OpenFileButton.IsEnabled = true;
            };

            _executionTimer.Start();
            _isRunning = true;

            // Update UI
            RunButton.Content = "Stop";
            RunButton.Foreground = Brushes.Red;
            ShowRatesFetchIntervalInput.IsEnabled = false;
            ShowAllSourceRatesCheckbox.IsEnabled = false;
            IPAddressInput.IsEnabled = false;
            ConnectButton.IsEnabled = false;
            OpenFileButton.IsVisible = true;
            OpenFileButton.Content = "Open File";
            UpdateRunStatus("Running...", Brushes.Orange);
        }
        catch (Exception ex)
        {
            UpdateRunStatus($"Start failed: {ex.Message}", Brushes.Red);
        }
    }

    private void StopMonitoring(bool wallNotStarted = false)
    {
        _isRunning = false;
        _executionTimer?.Stop();

        RunButton.Content = "Run";
        RunButton.Foreground = Brushes.White;
        ShowRatesFetchIntervalInput.IsEnabled = true;
        ShowAllSourceRatesCheckbox.IsEnabled = true;

        OpenFileButton.IsVisible = false;
        OpenFileButton.IsEnabled = false;

        IPAddressInput.IsEnabled = true;
        ConnectButton.IsEnabled = true;

        if (!wallNotStarted) { UpdateRunStatus("Stopped", Brushes.White); }
        else { UpdateRunStatus("Wall not started", Brushes.Red); }
    }

    private async Task<string> GetResponseAsync(string command, int readFromServer = 1)
    {
        await _telnetClient.WriteLineAsync(command);
        string response = await _telnetClient.ReadAsync(TimeSpan.FromMinutes(readFromServer));
        return response;
    }

    private async Task ExecuteCommandAsync()
    {
        if (!_isRunning || _telnetClient == null || _cancellationTokenSource?.IsCancellationRequested == true)
            return;

        try
        {
            string response = await GetResponseAsync(ShowRatesTerminalCommand);

            if (response.Contains("NotStarted"))
            {
                StopMonitoring(true);
                return;
            }

            ProcessResponse(response, ShowAllSourceRatesCheckbox.IsChecked);
        }
        catch (TaskCanceledException)
        {
            // Expected during cancellation
        }
        catch (ObjectDisposedException)
        {
            UpdateRunStatus("Connection closed", Brushes.Red);
            StopMonitoring();
        }
        catch (Exception ex)
        {
            UpdateRunStatus($"Error: {ex.Message}", Brushes.Red);
        }
    }

    private void ProcessResponse(string response, bool? showAllSourceRates = false)
    {
        if ((bool)showAllSourceRates)
        {
            var timestamp = DateTime.Now.ToString();
            var filteredLines = response
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !l.Contains('>') && !l.Contains("***showrates***"));

            var cleanedResponse = $"{timestamp}:\n{string.Join(Environment.NewLine, filteredLines)}\n\n";

            if (cleanedResponse == null) return;

            LogToFile(cleanedResponse);
        }
        else
        {
            GetNumberOfWindows();
            var averages = response?
            .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith("Layout average"));

            if (averages == null) return;

            var match = Regex.Matches(averages, @"\d+\.\d+");
            if (match.Count < 3) return;

            UpdateAverages(
                double.Parse(match[0].Value),
                double.Parse(match[1].Value),
                double.Parse(match[2].Value));

            LogToFile();
        }
    }

    private async void GetNumberOfWindows()
    {
        string status = await GetResponseAsync(StatusCommand);
        string currentLayout = "";

        foreach (var line in status.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            if(line.StartsWith("CurrentLayout:"))
            {
                currentLayout = line.Split(":")[1].Trim();
                break;
            }
        }

        string windowsResponse = await GetResponseAsync($"window \"{currentLayout}\" \r\n");

        var match = Regex.Match(windowsResponse, @"-(\d+)");
        if(match.Success)
        {
            _windowsQuantity = int.Parse(match.Groups[1].Value.Trim()) + 1;
        } 
        return;
    }

    private void UpdateAverages(double render, double capture, double transfer)
    {
        _renderAverage = _renderAverage == 0 ? render : Math.Round((render + _renderAverage) / 2, 2);
        _captureAverage = _captureAverage == 0 ? capture : Math.Round((capture + _captureAverage) / 2, 2);
        _transferAverage = _transferAverage == 0 ? transfer : Math.Round((transfer + _transferAverage) / 2, 2);
    }

    private void LogToFile(string content = null)
    {
        try
        {
            if (content == null)
            {
                var logEntry = $"{DateTime.Now} || " +
                           $"Rates Average : {_renderAverage}, {_captureAverage}, {_transferAverage}";

                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            else
            {
                File.AppendAllText(_logFilePath, content);
            }
        }
        catch (Exception ex)
        {
            UpdateRunStatus($"File error: {ex.Message}", Brushes.Red);
        }
    }

    private void UpdateConnectionStatus(string message, IBrush color)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IPAddressStatus.Text = message;
            IPAddressStatus.Foreground = color;
        });
    }

    private void UpdateRunStatus(string message, IBrush color)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RunStatus.Text = message;
            RunStatus.Foreground = color;
        });
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        CleanupResources();
        base.OnClosing(e);
    }

    private void CleanupResources()
    {
        _isRunning = false;
        _executionTimer?.Stop();

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();

        _telnetClient?.Dispose();
    }
}