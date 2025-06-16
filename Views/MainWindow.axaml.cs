using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ShowRatesLoggerGUI.Services;

namespace ShowRatesLoggerGUI;

public partial class MainWindow : Window
{
    private TelnetService _telnetService;
    private MonitoringService _monitoringService;

    public MainWindow()
    {
        InitializeComponent();
        _telnetService = new TelnetService(UpdateConnectionStatus);
        _monitoringService = new MonitoringService(UpdateRunStatus);
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
    }

    public async void OnConnect(object sender, RoutedEventArgs e)
    {
        var ip = IPAddressInput.Text;
        if (string.IsNullOrEmpty(ip)) return;

        var success = await _telnetService.Connect(ip);
        if(!success) return;

        ShowRatesFetchIntervalSection.IsEnabled = true;
        ShowRatesFetchIntervalSection.IsVisible = true;

        var response = await _telnetService.SendCommand("***showrates*** terminal");
        if (response.Contains("NotStarted"))
            await _telnetService.SendCommand("start");
    }

    public void OnRun(object sender, RoutedEventArgs e)
    {
        if(!_telnetService.IsConnected)
        {
            UpdateRunStatus("Not connected", Brushes.Red);
            return;
        }

        if(!double.TryParse(ShowRatesFetchIntervalInput.Text, out var interval))
        {
            UpdateRunStatus("Invalid interval", Brushes.Red);
        }

        if(_monitoringService.IsRunning)
        {
            _monitoringService.Stop();
            StopLoggingUIComponents();
        }
        else
        {
            double? logDuration = (double?)RunLoggingByIntervalInput.Value;
            _monitoringService.Start(_telnetService, interval, logDuration, IPAddressInput.Text,
                ShowAllSourceRatesCheckbox.IsChecked ?? false,
                CsvOutputCheckbox.IsChecked ?? false);
            StartLoggingUIComponents();
        }
    }

    private void StartLoggingUIComponents()
    {
        ConnectButton.IsEnabled = false;
        RunLoggingByIntervalInput.IsEnabled = false;
        ShowAllSourceRatesCheckbox.IsEnabled = false;
        CsvOutputCheckbox.IsEnabled = false;
        RunLoggingByIntervalSection.IsEnabled = false;
        RunLoggingByIntervalCheckbox.IsEnabled = false;
        RunButton.Content = "Stop";
        OpenFileButton.IsVisible = true;
        OpenFileButton.IsEnabled = true;
    }

    private void StopLoggingUIComponents()
    {
        ConnectButton.IsEnabled = true;
        RunLoggingByIntervalInput.IsEnabled = true;
        ShowAllSourceRatesCheckbox.IsEnabled = true;
        CsvOutputCheckbox.IsEnabled = true;
        RunLoggingByIntervalSection.IsEnabled = true;
        RunLoggingByIntervalCheckbox.IsEnabled = true;
        RunButton.Content = "Run";
        OpenFileButton.IsVisible = false;
        OpenFileButton.IsEnabled = false;
    }

    public void RunLoggingByIntervalChecked(object sender, RoutedEventArgs e) => RunLoggingByIntervalSection.IsVisible = true;
    public void RunLoggingByIntervalUnchecked(object sender, RoutedEventArgs e) => RunLoggingByIntervalSection.IsVisible = false;

    public void OpenLogFile(object sender, RoutedEventArgs e)
    {
        _monitoringService.OpenLogFile();
    }

    private void UpdateConnectionStatus(string message, IBrush color)
    {
        IPAddressStatus.Text = message;
        IPAddressStatus.Foreground = color;
    }

    private void UpdateRunStatus(string message, IBrush color)
    {
        RunStatus.Text = message;
        RunStatus.Foreground = color;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _monitoringService?.Dispose();
        _telnetService?.Dispose();
        base.OnClosing(e);
    }
}