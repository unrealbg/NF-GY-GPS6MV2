namespace NF_GY_GPS6MV2
{
    using System;
    using System.Threading;

    using nanoFramework.Json;

    using NF_GY_GPS6MV2.Services;

    using static AppSettings;

    public class Program
    {
        private static ConnectionService connectionService;
        private static MqttService mqttService;
        private static GpsService gpsService;
        private static Timer repeatingTimer;

        public static void Main()
        {
            Console.WriteLine("Starting GPS tracker...");

            Thread.Sleep(10000);

            connectionService = new ConnectionService();
            connectionService.ConnectionLost += OnConnectionLost;
            connectionService.ConnectionRestored += OnConnectionRestored;

            connectionService.Connect();

            Console.WriteLine("WiFi connected. IP: " + connectionService.GetIpAddress());

            mqttService = new MqttService(MqttBrokerAddress, MqttClientId, MqttUser, MqttPassword);

            gpsService = new GpsService(GpsComPort, GpsBaudRate, GpsRxPin, GpsTxPin);
            gpsService.Start();

            repeatingTimer = new Timer(TimerTick, null, 0, PostGpsDataDelayMs);

            Thread.Sleep(Timeout.Infinite);
        }

        private static void TimerTick(object state)
        {
            connectionService.CheckConnection();

            var data = gpsService.LastGpsData;

            if (data != null && data.IsValid)
            {
                string jsonData = JsonConvert.SerializeObject(data);
                Console.WriteLine("GPS data: " + jsonData);

                mqttService.Publish(MqttTopic, jsonData);
            }
            else
            {
                Console.WriteLine("Invalid or missing GPS data, skipping publish.");
                Thread.Sleep(10000);
                mqttService.Publish("gps/error", $"Invalid or missing GPS data: {DateTime.UtcNow}");
            }
        }

        private static void OnConnectionLost(object sender, EventArgs e)
        {
            Console.WriteLine("WiFi connection lost! Will attempt to reconnect...");
        }

        private static void OnConnectionRestored(object sender, EventArgs e)
        {
            Console.WriteLine("WiFi connection restored. IP: " + connectionService.GetIpAddress());
        }
    }
}
