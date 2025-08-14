namespace NF_GY_GPS6MV2
{
    using System;
    using System.Threading;

    using nanoFramework.Json;

    using NF_GY_GPS6MV2.Services;

    using static AppSettings;

    public class Program
    {
        private static ConnectionService _connectionService;
        private static MqttService _mqttService;
        private static GpsService _gpsService;
        private static Timer _repeatingTimer;
        private static ManualResetEvent _exitEvent = new ManualResetEvent(false);
        private static int _gpsErrorPublishCounter = 0;
        private const int MaxGpsErrorPublishes = 5;
        private static DateTime _lastGpsErrorPublish = DateTime.MinValue;

        public static void Main()
        {
            try
            {
                Console.WriteLine("Starting GPS tracker...");

                // Load config from storage before using settings
                AppSettings.LoadFromStorage();

                Thread.Sleep(GpsStartupDelayMs);

                _connectionService = new ConnectionService();
                _connectionService.ConnectionLost += OnConnectionLost;
                _connectionService.ConnectionRestored += OnConnectionRestored;
                _connectionService.Connect();
                Console.WriteLine("WiFi connected. IP: " + _connectionService.GetIpAddress());

                _mqttService = new MqttService(MqttBrokerAddress, MqttClientId, MqttUser, MqttPassword);
                _gpsService = new GpsService(GpsComPort, GpsBaudRate, GpsRxPin, GpsTxPin);
                _gpsService.Start();

                _repeatingTimer = new Timer(TimerTick, null, 0, PostGpsDataDelayMs);

                _exitEvent.WaitOne();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _repeatingTimer?.Dispose();
                _gpsService?.Dispose();
                _mqttService?.Stop();
            }
        }

        private static void TimerTick(object state)
        {
            try
            {
                _connectionService.CheckConnection();

                var data = _gpsService.LastGpsData;

                if (data != null && data.IsValid)
                {
                    string jsonData = JsonConvert.SerializeObject(data);
                    Console.WriteLine("GPS data: " + jsonData);
                    _mqttService.Publish(MqttTopic, jsonData);
                    _gpsErrorPublishCounter = 0;
                }
                else
                {
                    Console.WriteLine("Invalid or missing GPS data, skipping publish.");

                    if (_gpsErrorPublishCounter < MaxGpsErrorPublishes || (DateTime.UtcNow - _lastGpsErrorPublish).TotalMinutes > 10)
                    {
                        _mqttService.Publish("gps/error", $"Invalid or missing GPS data: {DateTime.UtcNow}");
                        _gpsErrorPublishCounter++;
                        _lastGpsErrorPublish = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TimerTick: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void OnConnectionLost(object sender, EventArgs e)
        {
            Console.WriteLine("WiFi connection lost! Will attempt to reconnect...");
        }

        private static void OnConnectionRestored(object sender, EventArgs e)
        {
            Console.WriteLine("WiFi connection restored. IP: " + _connectionService.GetIpAddress());
        }
    }
}
