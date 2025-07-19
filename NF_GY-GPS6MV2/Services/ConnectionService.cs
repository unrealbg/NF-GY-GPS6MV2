namespace NF_GY_GPS6MV2.Services
{
    using System;
    using System.Device.Wifi;
    using System.Net.NetworkInformation;
    using System.Threading;

    using static AppSettings;

    /// <summary>
    /// Service for managing network connections, including connecting to Wi-Fi and checking connection status.
    /// </summary>
    public class ConnectionService
    {
        private const int MAX_CONNECTION_ATTEMPTS = 10;
        private const int RECONNECT_DELAY_MS = 10000;
        private const int CONNECTION_CHECK_INTERVAL_MS = 200;

        private readonly WifiAdapter _wifiAdapter;
        private readonly object _connectionLock = new object();
        private bool _isInitialStart = true;
        private bool _isConnectionInProgress = false;
        private string _ipAddress;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionService"/> class.
        /// </summary>
        public ConnectionService()
        {
            try
            {
                var adapters = WifiAdapter.FindAllAdapters();
                if (adapters == null || adapters.Length == 0)
                {
                    Console.WriteLine("No WiFi adapters found!");
                    throw new InvalidOperationException("No WiFi adapters found!");
                }
                _wifiAdapter = adapters[0];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize WiFi adapter: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Event triggered when the connection is restored.
        /// </summary>
        public event EventHandler ConnectionRestored;

        /// <summary>
        /// Event triggered when the connection is lost.
        /// </summary>
        public event EventHandler ConnectionLost;

        /// <summary>
        /// Gets a value indicating whether the connection is in progress.
        /// </summary>
        public bool IsConnectionInProgress
        {
            get
            {
                lock (_connectionLock)
                {
                    return _isConnectionInProgress;
                }
            }
        }

        /// <summary>
        /// Initiates a connection to the network.
        /// </summary>
        public void Connect()
        {
            if (_wifiAdapter == null)
            {
                Console.WriteLine("Cannot connect: WiFi adapter not available");
                throw new InvalidOperationException("WiFi adapter not available");
            }

            if (this.IsAlreadyConnected(out var currentIp))
            {
                Console.WriteLine($"Already connected. IP: {currentIp}");
                return;
            }

            lock (_connectionLock)
            {
                _isConnectionInProgress = true;
            }

            while (!this.IsAlreadyConnected(out _))
            {
                int attemptCount = 0;
                bool connected = false;

                while (!this.IsAlreadyConnected(out string ipAddress) && attemptCount < MAX_CONNECTION_ATTEMPTS)
                {
                    attemptCount++;
                    Console.WriteLine($"Connecting... [Attempt {attemptCount}/{MAX_CONNECTION_ATTEMPTS}]");

                    try
                    {
                        var result = _wifiAdapter.Connect(WifiSsid, WifiReconnectionKind.Automatic, WifiPassword);

                        if (this.TryWaitForConnection(result, out ipAddress))
                        {
                            this.HandleSuccessfulConnection(ipAddress);
                            connected = true;
                            break;
                        }

                        Console.WriteLine($"{this.GetErrorMessage(result.ConnectionStatus)}. Retrying in {RECONNECT_DELAY_MS / 1000} seconds...");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Connection error: {ex.Message}\n{ex.StackTrace}");
                    }

                    Thread.Sleep(RECONNECT_DELAY_MS);
                }

                if (connected)
                {
                    break;
                }

                if (this.IsAlreadyConnected(out string ip))
                {
                    lock (_connectionLock)
                    {
                        _ipAddress = ip;
                        _isConnectionInProgress = false;
                    }
                    Console.WriteLine($"Connection restored. IP Address: {ip}");
                    this.RaiseConnectionRestored();
                    break;
                }

                Console.WriteLine($"Failed to connect after {MAX_CONNECTION_ATTEMPTS} attempts. Sleeping for 1 minute before retrying...");
                Thread.Sleep(60000);
            }
            lock (_connectionLock)
            {
                _isConnectionInProgress = false;
            }
        }

        /// <summary>
        /// Checks the network connection and attempts to reconnect if it is lost.
        /// </summary>
        public void CheckConnection()
        {
            lock (_connectionLock)
            {
                if (_isConnectionInProgress)
                {
                    return;
                }
                _isConnectionInProgress = true;
            }

            if (!this.IsAlreadyConnected(out _))
            {
                this.RaiseConnectionLost();
                Console.WriteLine("Lost network connection. Attempting to reconnect...");
                this.Connect();
            }
            else
            {
                lock (_connectionLock)
                {
                    _isConnectionInProgress = false;
                }
            }
        }

        /// <summary>
        /// Gets the IP address of the device.
        /// </summary>
        /// <returns>The IP address of the device.</returns>
        public string GetIpAddress()
        {
            if (string.IsNullOrEmpty(_ipAddress) || _ipAddress == "0.0.0.0")
            {
                if (this.IsAlreadyConnected(out string currentIp))
                {
                    _ipAddress = currentIp;
                }
                else
                {
                    return "IP address not available";
                }
            }

            return _ipAddress;
        }

        /// <summary>
        /// Checks if the device is already connected to the network.
        /// </summary>
        /// <param name="ipAddress">The IP address of the device if connected.</param>
        /// <returns><c>true</c> if the device is connected; otherwise, <c>false</c>.</returns>
        private bool IsAlreadyConnected(out string ipAddress)
        {
            ipAddress = null;

            try
            {
                var networkInterface = NetworkInterface.GetAllNetworkInterfaces()[0];
                ipAddress = networkInterface.IPv4Address;
                return !(string.IsNullOrEmpty(ipAddress) || ipAddress == "0.0.0.0");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Network interface error: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Tries to wait for a successful connection.
        /// </summary>
        /// <param name="result">The WiFi connection result.</param>
        /// <param name="ipAddress">The resulting IP address.</param>
        /// <returns>True if connection successful, false otherwise.</returns>
        private bool TryWaitForConnection(WifiConnectionResult result, out string ipAddress)
        {
            ipAddress = null;

            if (result.ConnectionStatus != WifiConnectionStatus.Success)
            {
                return false;
            }

            for (int i = 0; i < MAX_CONNECTION_ATTEMPTS; i++)
            {
                if (this.IsAlreadyConnected(out string currentIp))
                {
                    ipAddress = currentIp;
                    return true;
                }

                Thread.Sleep(CONNECTION_CHECK_INTERVAL_MS);
            }

            return false;
        }

        /// <summary>
        /// Handles logic for a successful connection.
        /// </summary>
        /// <param name="ipAddress">The assigned IP address.</param>
        private void HandleSuccessfulConnection(string ipAddress)
        {
            _ipAddress = ipAddress;

            if (_isInitialStart)
            {
                Console.WriteLine($"Connection established. IP address: {ipAddress}");
                _isInitialStart = false;
            }
            else
            {
                Console.WriteLine($"Connection restored. IP Address: {ipAddress}");
                this.RaiseConnectionRestored();
            }

            _isConnectionInProgress = false;
        }

        /// <summary>
        /// Gets the error message corresponding to the Wi-Fi connection status.
        /// </summary>
        /// <param name="status">The Wi-Fi connection status.</param>
        /// <returns>The error message.</returns>
        private string GetErrorMessage(WifiConnectionStatus status)
        {
            switch (status)
            {
                case WifiConnectionStatus.AccessRevoked: return "Access to the network has been revoked";
                case WifiConnectionStatus.InvalidCredential: return "Invalid credential was presented";
                case WifiConnectionStatus.NetworkNotAvailable: return "Network is not available";
                case WifiConnectionStatus.Timeout: return "Connection attempt timed out";
                case WifiConnectionStatus.UnspecifiedFailure: return "Unspecified error [connection refused]";
                case WifiConnectionStatus.UnsupportedAuthenticationProtocol: return "Authentication protocol is not supported";
                default: return "Unknown error";
            }
        }

        private void RaiseConnectionRestored()
        {
            if (this.ConnectionRestored != null)
            {
                this.ConnectionRestored(this, EventArgs.Empty);
            }
        }

        private void RaiseConnectionLost()
        {
            if (this.ConnectionLost != null)
            {
                this.ConnectionLost(this, EventArgs.Empty);
            }
        }
    }
}