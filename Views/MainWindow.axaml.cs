using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ShowRatesLoggerGUI.Models;
using ShowRatesLoggerGUI.Services;
using ShowRatesLoggerGUI.Utilities;
using Windows.Devices.Input;

namespace ShowRatesLoggerGUI;

public partial class MainWindow : Window
{
    private TelnetService _telnetService;
    private MonitoringService _monitoringService;
    private readonly UIComponentToggler _uiToggler;

    public MainWindow()
    {
        InitializeComponent();
        _uiToggler = new UIComponentToggler(this);
        _telnetService = new TelnetService(UpdateConnectionStatus);
        _monitoringService = new MonitoringService(UpdateRunStatus, OnStopMonitoring, UpdateCurrentRates, _uiToggler);
        InitializeUIState();
    }

    private void InitializeUIState()
    {
        IPAddressStatus.Text = "Disconnected";
        IPAddressStatus.Foreground = Brushes.Gray;
        RunStatus.Text = string.Empty;
        SetVisible(ShowRatesFetchIntervalSection, false);
        RunButton.Content = "Run";
        SetVisible(RunLoggingByIntervalSection, false);
        SetVisible(RCTNotificationsSection, false);
    }

    public async void OnConnect(object sender, RoutedEventArgs e)
    {
        var ip = IPAddressInput.Text;
        if (string.IsNullOrEmpty(ip)) return;

        var success = await _telnetService.Connect(ip);
        if(!success)
        {
            UpdateConnectionStatus("Connection failed", Brushes.Red);
            return;
        }

        UpdateConnectionStatus("Connected", Brushes.Green);
        SetVisible(ShowRatesFetchIntervalSection, true);
        SetEnabled(ShowRatesFetchIntervalSection, true);

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
            return;
        }

        if(_monitoringService.IsRunning)
        {
            _monitoringService.Stop();
            RunButton.Content = "Run";
            StopLoggingUIComponents();
            CurrentRatesText.Text = string.Empty;
            return;
        }

        double? logDuration = (double?)(RunLoggingByIntervalCheckbox.IsChecked == true ? RunLoggingByIntervalInput.Value : null);

        RateData? rctNotificationsObject = RCTNotificationsCheckbox.IsChecked == true ? new RateData
        {
            Render = double.Parse(RenderNotificationSetting.Text),
            RenderNotificationsEnabled = RenderNotificationsEnabled.IsChecked == true,

            Capture = double.Parse(CaptureNotificationSetting.Text),
            CaptureNotificationsEnabled = RenderNotificationsEnabled.IsChecked == true,

            Transfer = double.Parse(TransferNotificationSetting.Text),
            TransferNotificationsEnabled = RenderNotificationsEnabled.IsChecked == true,
        } : null;

        _monitoringService.Start(_telnetService, interval, logDuration, IPAddressInput.Text,
            ShowAllSourceRatesCheckbox.IsChecked ?? false,
            CsvOutputCheckbox.IsChecked ?? false,
            rctNotificationsObject);

        StartLoggingUIComponents();
        CurrentRatesText.Text = _monitoringService.CurrentRates;
    }

    private void StartLoggingUIComponents() => _uiToggler.ToggleLoggingUI(true);

    private void StopLoggingUIComponents() => _uiToggler.ToggleLoggingUI(false);

    private void UpdateCurrentRates(string currentRates)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentRatesText.Text = currentRates;
        });
    }

    private void OnStopMonitoring()
    {
        Dispatcher.UIThread.Post(() =>
        {
            RunButton.Content = "Run";
        });
    }

    public void RunLoggingByIntervalChecked(object sender, RoutedEventArgs e) => SetVisible(RunLoggingByIntervalSection, true);
    public void RunLoggingByIntervalUnchecked(object sender, RoutedEventArgs e) => SetVisible(RunLoggingByIntervalSection, false);
    public void RCTNotificationsChecked(object sender, RoutedEventArgs e) => SetVisible(RCTNotificationsSection, true);
    public void RCTNotificationsUnchecked(object sender, RoutedEventArgs e) => SetVisible(RCTNotificationsSection, false);

    private void SetEnabled(Control control, bool isEnabled) => control.IsEnabled = isEnabled;
    private void SetVisible(Control control, bool isVisible) => control.IsVisible = isVisible;

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