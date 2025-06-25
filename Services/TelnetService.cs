using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using PrimS.Telnet;

namespace ShowRatesLoggerGUI.Services
{
    internal class TelnetService : IDisposable
    {
        private Client _client;
        private string _ipAddress;
        private CancellationTokenSource _cts;
        private readonly Action<string, IBrush> _updateStatus;

        public bool IsConnected { get; private set; }

        public TelnetService(Action<string, IBrush> updateStatus)
        {
            _updateStatus = updateStatus;
        }

        public async Task<bool> Connect(string ip)
        {
            _ipAddress = ip.ToLower() == "localhost"
                ? Dns.GetHostEntry(Dns.GetHostName())
                .AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString()
                : ip;

            if(!IPAddress.TryParse(_ipAddress, out _))
            {
                _updateStatus("Invalid IP address", Brushes.Red);
                return false;
            }

            try
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                _client = new Client(_ipAddress, 23, _cts.Token);
                IsConnected = true;
                _updateStatus("Connected!", Brushes.Green);
                return true;
            }
            catch (Exception ex) 
            {
                IsConnected = false;
                _updateStatus("Failed to connect", Brushes.Red);
                return false;
            }
        }

        public Task<string> SendCommand(string command, int timeoutMinutes = 2)
        {
            return Task.Run(async () =>
            {
                await _client.WriteLineAsync(command + "\n\r");
                return await _client.ReadAsync(TimeSpan.FromMinutes(timeoutMinutes));
            });
        }

        public string GetApplianceType()
        {

        }

        public void Dispose()
        {
            _client?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
