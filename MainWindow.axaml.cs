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
    private Stopwatch _stopwatch;

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
            AttemptConnection();
            var response = await GetResponseAsync(ShowRatesTerminalCommand);
            if(response.Contains("NotStarted"))
            {
                await GetResponseAsync("start\n\r");
            }
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
    private void AttemptConnection()
    {
        _localHostIpAddress = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        
        _telnetClient = new Client(_localHostIpAddress == null ? IPAddressInput.Text : _localHostIpAddress, 23, _cancellationTokenSource.Token);
         
        _isConnected = true;

        UpdateConnectionStatus("Connected!", Brushes.Green);
        ShowRatesFetchIntervalSection.IsEnabled = true;
        ShowRatesFetchIntervalSection.IsVisible = true;
        ShowRatesFetchIntervalInput.Text = string.Empty;
        // TODO: Fix formatting (causing incorrect window value)
        // GetNumberOfWindows();
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
            if(RunLoggingByIntervalInput.Value != null)
                StartMonitoring((double)RunLoggingByIntervalInput.Value); 
            else
                StartMonitoring();
        }
    }

    public void RunLoggingByIntervalChecked(object sender, RoutedEventArgs e) => RunLoggingByIntervalSection.IsVisible = true;

    public void RunLoggingByIntervalUnchecked(object sender, RoutedEventArgs e) => RunLoggingByIntervalSection.IsVisible = false;

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
                return;
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

    private async Task StartMonitoring(double? logForSeconds = null)
    {
        try
        {
            // Initialize/clear the log file
            _logFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"ShowRatesLoggerGUI_{IPAddressInput.Text}"
            );

            if(!Directory.Exists(_logFilePath))
                Directory.CreateDirectory(_logFilePath);

            _logFilePath += (bool)CsvOutputCheckbox.IsChecked ? $"\\{DateTime.Now:yyyy-MM-dd_HH_mm_ss}-RatesAverage.csv" : $"\\{DateTime.Now:yyyy-MM-dd_HH_mm_ss}-RatesAverage.txt";

            // Create initial empty file
            if ((bool)CsvOutputCheckbox.IsChecked)
            {
                File.WriteAllText(_logFilePath, $"Time,Window #,Render,Capture,Transfer{Environment.NewLine}");
            } else
            {
                File.WriteAllText(_logFilePath, $"ShowRatesLoggerGUI || {IPAddressInput.Text} || Started at {DateTime.Now} | {_windowsQuantity} windows{Environment.NewLine}");
            }

            // Reset averages
            _renderAverage = 0;
            _captureAverage = 0;
            _transferAverage = 0;

            // Stop any existing timer
            _executionTimer?.Stop();

            _stopwatch = Stopwatch.StartNew();

            // Execute first command immediately
            await ExecuteCommandAsync();

            // Set up timer for subsequent executions
            _executionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_fetchInterval)
            };

            _executionTimer.Tick += async (s, e) =>
            {
                if(logForSeconds.HasValue && _stopwatch.Elapsed.TotalMinutes >= logForSeconds.Value) 
                {
                    _stopwatch.Stop();
                    await ExecuteCommandAsync();
                    StopMonitoring();
                    return;
                }

                await ExecuteCommandAsync();
                OpenFileButton.IsEnabled = true;
            };

            _executionTimer.Start();
            _isRunning = true;

            // Update UI
            RunButton.Content = "Stop";
            RunButton.Foreground = Brushes.Red;
            ShowRatesFetchIntervalInput.IsEnabled = false;
            RunLoggingByIntervalInput.IsEnabled = false;

            ShowAllSourceRatesCheckbox.IsEnabled = false;
            CsvOutputCheckbox.IsEnabled = false;
            RunLoggingByIntervalCheckbox.IsEnabled = false;

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
        RunLoggingByIntervalInput.IsEnabled = true;
        ShowAllSourceRatesCheckbox.IsEnabled = true;
        CsvOutputCheckbox.IsEnabled = true;
        RunLoggingByIntervalCheckbox.IsEnabled = true;

        OpenFileButton.IsVisible = false;
        OpenFileButton.IsEnabled = false;

        IPAddressInput.IsEnabled = true;
        ConnectButton.IsEnabled = true;

        if (!wallNotStarted) { UpdateRunStatus($"Stopped logging after {Math.Round(_stopwatch.Elapsed.TotalMinutes, 2)} minute(s)", Brushes.White); }
        else { UpdateRunStatus("Wall not started", Brushes.Red); }
    }

    private async Task<string> GetResponseAsync(string command, int readFromServer = 2)
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

            ProcessResponse(response, ShowAllSourceRatesCheckbox.IsChecked, CsvOutputCheckbox.IsChecked);


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
            StopMonitoring();
        }
    }

    private void ProcessResponse(string response, bool? showAllSourceRates = false, bool? outputToCsv = false)
    {
        if ((bool)showAllSourceRates && (bool)outputToCsv)
        {
            LogToFile(response, true, true);
        }
        else
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
            else if ((bool)outputToCsv)
            {
                GetAverageRates(response);
                LogToFile(response, true);
            }
            else
            {
                GetAverageRates(response);
                LogToFile();
            }
        }
    }

    private void WriteAllRatesToCsv(string response)
    {
        var lines = response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        var windowRegex = new Regex(@"Window (\d+)\s+Render, Capture, Transfer: ([\d.]+), ([\d.]+), ([\d.]+)");
        var layoutRegex = new Regex(@"Layout average\s+Render, Capture, Transfer: ([\d.]+), ([\d.]+), ([\d.]+)");

        foreach ( var line in lines )
        {
            Match windowMatch = windowRegex.Match(line);
            if(windowMatch.Success)
            {
                var windowNumber = windowMatch.Groups[1].Value;
                var render = windowMatch.Groups[2].Value;
                var capture = windowMatch.Groups[3].Value;
                var transfer = windowMatch.Groups[4].Value;

                File.AppendAllText(_logFilePath, $"{DateTime.Now},{windowNumber},{render},{capture},{transfer}{Environment.NewLine}");
            }
            else if(layoutRegex.IsMatch(line))
            {
                var layoutMatch = layoutRegex.Match(line);
                var render = layoutMatch.Groups[1].Value;
                var capture = layoutMatch.Groups[2].Value;
                var transfer = layoutMatch.Groups[3].Value;

                File.AppendAllText(_logFilePath, $"{DateTime.Now},Average,{render},{capture},{transfer}{Environment.NewLine}{Environment.NewLine}");
            }
        }
    }

    private void GetAverageRates(string response)
    {
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
    }

    // TODO: Fix formatting (causing incorrect window value)
    // private async void GetNumberOfWindows()
    // {
    //     string status = await GetResponseAsync(StatusCommand);
    //     string currentLayout = "";

    //     foreach (var line in status.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
    //     {
    //         if(line.StartsWith("CurrentLayout:"))
    //         {
    //             currentLayout = line.Split(":")[1].Trim();
    //             break;
    //         }
    //     }

    //     string windowsResponse = await GetResponseAsync($"window \"{currentLayout}\" \r\n");

    //     var match = Regex.Match(windowsResponse, @"-(\d+)");
    //     if(match.Success)
    //     {
    //         _windowsQuantity = int.Parse(match.Groups[1].Value.Trim()) + 1;
    //     } 
    //     return;
    // }

    private void UpdateAverages(double render, double capture, double transfer)
    {
        _renderAverage = _renderAverage == 0 ? render : Math.Round((render + _renderAverage) / 2, 2);
        _captureAverage = _captureAverage == 0 ? capture : Math.Round((capture + _captureAverage) / 2, 2);
        _transferAverage = _transferAverage == 0 ? transfer : Math.Round((transfer + _transferAverage) / 2, 2);
    }

    private void LogToFile(string content = null, bool outputToCsv = false, bool showAllRates = false)
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
                if(outputToCsv)
                {
                    if (showAllRates)
                        WriteAllRatesToCsv(content);
                    else
                        File.AppendAllText(_logFilePath, $"{DateTime.Now},Average,{_renderAverage},{_captureAverage},{_transferAverage}{Environment.NewLine}");
                }
                else
                {
                    File.AppendAllText(_logFilePath, content);
                }
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