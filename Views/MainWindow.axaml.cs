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
            RunButton.Content = "Run";
            StopLoggingUIComponents();
            CurrentRatesText.Text = "";
        }
        else
        {
            double? logDuration = (double?)RunLoggingByIntervalInput.Value;
            RateData? rctNotificationObject;
            if ((bool)RCTNotificationsCheckbox.IsChecked)
            {
                rctNotificationObject = (RateData?)new RateData()
                {
                    Render = double.Parse(RenderNotificationSetting.Text),
                    RenderNotificationsEnabled = (bool)RenderNotificationsEnabled.IsChecked,
                    Capture = double.Parse(CaptureNotificationSetting.Text),
                    CaptureNotificationsEnabled = (bool)CaptureNotificationsEnabled.IsChecked,
                    Transfer = double.Parse(TransferNotificationSetting.Text),
                    TransferNotificationsEnabled = (bool)TransferNotificationsEnabled.IsChecked,
                };
            }
            else
            {
                rctNotificationObject = null;
            }

            _monitoringService.Start(_telnetService, interval, logDuration, IPAddressInput.Text,
                ShowAllSourceRatesCheckbox.IsChecked ?? false,
                CsvOutputCheckbox.IsChecked ?? false,
                rctNotificationObject);
            StartLoggingUIComponents();
            CurrentRatesText.Text = _monitoringService.CurrentRates;
        }
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

    public void RunLoggingByIntervalChecked(object sender, RoutedEventArgs e) => RunLoggingByIntervalSection.IsVisible = true;
    public void RunLoggingByIntervalUnchecked(object sender, RoutedEventArgs e) => RunLoggingByIntervalSection.IsVisible = false;
    public void RCTNotificationsChecked(object sender, RoutedEventArgs e) => RCTNotificationsSection.IsVisible = true;
    public void RCTNotificationsUnchecked(object sender, RoutedEventArgs e) => RCTNotificationsSection.IsVisible = false;

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