namespace NF_GY_GPS6MV2.Services
{
    using System;
    using System.Text;
    using System.Threading;

    using nanoFramework.M2Mqtt;

    public class MqttService : IDisposable
    {
        private MqttClient _client;
        private readonly string _brokerAddress;
        private readonly string _clientId;
        private readonly string _user;
        private readonly string _pass;
        private Timer _reconnectTimer;
        private bool _isReconnecting = false;
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 3;
        private const int ReconnectIntervalMs = 60000;
        private readonly object _syncLock = new object();
        private bool _disposed = false;

        public bool IsConnected
        {
            get { lock (_syncLock) { return _client != null && _client.IsConnected; } }
        }

        /// <summary> 
        /// Initializes a new instance of the <see cref="MqttService"/> class. 
        /// </summary> 
        /// <param name="brokerAddress">MQTT broker address</param> 
        /// <param name="clientId">Client ID</param> 
        /// <param name="user">Username</param> 
        /// <param name="pass">Password</param> 
        public MqttService(string brokerAddress, string clientId, string user, string pass)
        {
            _brokerAddress = brokerAddress;
            _clientId = clientId;
            _user = user;
            _pass = pass;

            this.Connect();
            _reconnectTimer = new Timer(CheckConnection, null, ReconnectIntervalMs, ReconnectIntervalMs);
        }

        private void CheckConnection(object state)
        {
            if (_isReconnecting)
            {
                return;
            }

            lock (_syncLock)
            {
                if (_client == null || !_client.IsConnected)
                {
                    _isReconnecting = true;
                    try
                    {
                        Console.WriteLine("Connection check: MQTT client disconnected. Attempting to reconnect...");
                        this.Connect();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Reconnection failed: {ex.Message}\n{ex.StackTrace}");
                    }

                    _isReconnecting = false;
                }
            }
        }

        private void Connect()
        {
            lock (_syncLock)
            {
                _reconnectAttempts = 0;
                while (_reconnectAttempts < MaxReconnectAttempts)
                {
                    try
                    {
                        if (_client != null)
                        {
                            try
                            {
                                if (_client.IsConnected)
                                {
                                    _client.Disconnect();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error during disconnect: {ex.Message}\n{ex.StackTrace}");
                            }

                            _client = null;
                        }

                        Thread.Sleep(100);
                        _client = new MqttClient(_brokerAddress);
                        _client.Connect(_clientId, _user, _pass);

                        if (_client.IsConnected)
                        {
                            Console.WriteLine("Connected to MQTT broker: " + _brokerAddress);
                            _reconnectAttempts = 0;
                            return;
                        }

                        Console.WriteLine("MQTT connection failed");
                        this._reconnectAttempts++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error connecting to MQTT: " + ex.Message + "\n" + ex.StackTrace);
                        _reconnectAttempts++;
                    }

                    Thread.Sleep(5000);
                }

                Console.WriteLine("Maximum reconnection attempts reached. Will try again later.");
            }
        }

        /// <summary> 
        /// Publishes a message to the specified topic. 
        /// </summary> 
        /// <param name="topic">Topic to publish to</param> 
        /// <param name="message">Message to publish</param>
        /// <returns>True if published successfully, false otherwise</returns> 
        public bool Publish(string topic, string message)
        {
            lock (_syncLock)
            {
                if (_client == null || !_client.IsConnected)
                {
                    Console.WriteLine("MQTT client not connected. Attempting to reconnect...");
                    try
                    {
                        this.Connect();
                        if (!this.IsConnected)
                        {
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during reconnect in Publish: {ex.Message}\n{ex.StackTrace}");
                        return false;
                    }
                }

                try
                {
                    _client.Publish(topic, Encoding.UTF8.GetBytes(message));
                    Console.WriteLine($"Published to {topic}: {message}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error publishing to MQTT: " + ex.Message + "\n" + ex.StackTrace);
                    return false;
                }
            }
        }

        /// <summary>
        /// Disconnects from the MQTT broker.
        /// </summary>
        public void Stop()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            lock (_syncLock)
            {
                if (_reconnectTimer != null)
                {
                    _reconnectTimer.Dispose();
                    _reconnectTimer = null;
                }

                if (_client != null)
                {
                    try
                    {
                        if (_client.IsConnected)
                        {
                            _client.Disconnect();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during Dispose disconnect: {ex.Message}\n{ex.StackTrace}");
                    }

                    _client = null;
                }

                _disposed = true;
            }
        }
    }
}