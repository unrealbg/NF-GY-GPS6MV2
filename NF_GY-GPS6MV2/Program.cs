namespace NF_GY_GPS6MV2
{
    using System;
    using System.Threading;

    using nanoFramework.Hardware.Esp32;
    using nanoFramework.Json;
    using nanoFramework.Networking;

    using NF_GY_GPS6MV2.Services;

    using static AppSettings;

    public class Program
    {
        public static void Main()
        {
            bool success = WifiNetworkHelper.ConnectDhcp(WifiSsid, WifiPassword, requiresDateTime: false);

            Console.WriteLine(success ? "Connected to WiFi network successfully" : "WiFi connection failed");

            var mqttService = new MqttService(MqttBrokerAddress, MqttClientId, MqttUser, MqttPassword);

            var gpsService = new GpsService(GpsComPort, GpsBaudRate, GpsRxPin, GpsTxPin);
            gpsService.Start();

            Thread.Sleep(GpsStartupDelayMs);

            if (gpsService.LastGpsData != null)
            {
                string jsonData = JsonConvert.SerializeObject(gpsService.LastGpsData);
                Console.WriteLine("GPS data: " + jsonData);

                mqttService.Publish(MqttTopic, jsonData);
            }
            else
            {
                Console.WriteLine("No GPS data available");
            }

            Thread.Sleep(PostGpsDataDelayMs);

            Console.WriteLine("Going to deep sleep for 1 minute...");
            var deepSleepDuration = new TimeSpan(0, DeepSleepMinutes, 0);
            Sleep.EnableWakeupByTimer(deepSleepDuration);
            Sleep.StartDeepSleep();
        }
    }
}