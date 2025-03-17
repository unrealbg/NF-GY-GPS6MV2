namespace NF_GY_GPS6MV2.Services
{
    using System;
    using System.Text;
    using System.Threading;

    using nanoFramework.M2Mqtt;

    public class MqttService
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

        public bool IsConnected => _client != null && _client.IsConnected;

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

            _reconnectTimer = new Timer(this.CheckConnection, null, 60000, 60000);
        }

        private void CheckConnection(object state)
        {
            if (_isReconnecting) return;

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
                    Console.WriteLine($"Reconnection failed: {ex.Message}");
                }

                _isReconnecting = false;
            }
        }

        private void Connect()
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
                        catch { /* Ignore */ }

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
                    else
                    {
                        Console.WriteLine("MQTT connection failed");
                        _reconnectAttempts++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error connecting to MQTT: " + ex.Message);
                    _reconnectAttempts++;
                }

                Thread.Sleep(5000);
            }

            Console.WriteLine("Maximum reconnection attempts reached. Will try again later.");
        }

        /// <summary> 
        /// Publishes a message to the specified topic. 
        /// </summary> 
        /// <param name="topic">Topic to publish to</param> 
        /// <param name="message">Message to publish</param>
        /// <returns>True if published successfully, false otherwise</returns> 
        public bool Publish(string topic, string message)
        {
            if (_client == null || !_client.IsConnected)
            {
                Console.WriteLine("MQTT client not connected. Attempting to reconnect...");

                try
                {
                    Connect();

                    if (!this.IsConnected)
                    {
                        return false;
                    }
                }
                catch
                {
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
                Console.WriteLine("Error publishing to MQTT: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Disconnects from the MQTT broker.
        /// </summary>
        public void Stop()
        {
            if (_reconnectTimer != null)
            {
                _reconnectTimer.Dispose();
                _reconnectTimer = null;
            }

            if (_client != null && _client.IsConnected)
            {
                try
                {
                    _client.Disconnect();
                }
                catch { }

                _client = null;
            }
        }
    }
}