namespace NF_GY_GPS6MV2
{
    using System;
    using System.IO;
    using nanoFramework.Json;

    using NF_GY_GPS6MV2.Models;

    public static class AppSettings
    {
        // Broker settings (configurable)
        public static string MqttBrokerAddress = "mqtt.broker.com";
        public static string MqttTopic = "gps/coords";
        public static string MqttClientId = "MyGpsClient";

        // WiFi settings (sensitive)
        public static string WifiSsid = "WiFi";
        public static string WifiPassword = "pass";

        // MQTT credentials (sensitive)
        public static string MqttUser = "user";
        public static string MqttPassword = "pass";

        // GPS settings
        public static string GpsComPort = "COM2";
        public static int GpsBaudRate = 9600;
        public static int GpsRxPin = 16;
        public static int GpsTxPin = 17;

        // Storage settings (nanoFramework storage drive)
        // Use "I:" for internal flash, "D:" for SD card
        public const string StorageRoot = "I:";
        public static readonly string ConfigFilePath = StorageRoot + "\\appsettings.json";

        // Timing settings
        public static int GpsStartupDelayMs = 10000;
        public static int PostGpsDataDelayMs = 5000;
        public static int DeepSleepMinutes = 1;

        public static void LoadFromStorage()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    // Create a template config on first run for easy editing
                    var template = new AppSettingsModel
                    {
                        MqttBrokerAddress = MqttBrokerAddress,
                        MqttTopic = MqttTopic,
                        MqttClientId = MqttClientId,
                        WifiSsid = WifiSsid,
                        WifiPassword = WifiPassword,
                        MqttUser = MqttUser,
                        MqttPassword = MqttPassword,
                        GpsComPort = GpsComPort,
                        GpsBaudRate = GpsBaudRate,
                        GpsRxPin = GpsRxPin,
                        GpsTxPin = GpsTxPin,
                        GpsStartupDelayMs = GpsStartupDelayMs,
                        PostGpsDataDelayMs = PostGpsDataDelayMs,
                        DeepSleepMinutes = DeepSleepMinutes
                    };

                    var jsonTpl = JsonConvert.SerializeObject(template);
                    using (var fs = new FileStream(ConfigFilePath, FileMode.Create))
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.Write(jsonTpl);
                    }

                    Console.WriteLine($"Created default config at {ConfigFilePath}");
                    return;
                }

                using (var fs = new FileStream(ConfigFilePath, FileMode.Open))
                using (var sr = new StreamReader(fs))
                {
                    var json = sr.ReadToEnd();
                    if (!string.IsNullOrEmpty(json))
                    {
                        var loaded = JsonConvert.DeserializeObject(json, typeof(AppSettingsModel)) as AppSettingsModel;
                        if (loaded != null)
                        {
                            if (!string.IsNullOrEmpty(loaded.MqttBrokerAddress))
                            {
                                MqttBrokerAddress = loaded.MqttBrokerAddress;
                            }

                            if (!string.IsNullOrEmpty(loaded.MqttTopic))
                            {
                                MqttTopic = loaded.MqttTopic;
                            }

                            if (!string.IsNullOrEmpty(loaded.MqttClientId))
                            {
                                MqttClientId = loaded.MqttClientId;
                            }

                            if (!string.IsNullOrEmpty(loaded.WifiSsid))
                            {
                                WifiSsid = loaded.WifiSsid;
                            }

                            if (!string.IsNullOrEmpty(loaded.WifiPassword))
                            {
                                WifiPassword = loaded.WifiPassword;
                            }

                            if (!string.IsNullOrEmpty(loaded.MqttUser))
                            {
                                MqttUser = loaded.MqttUser;
                            }

                            if (!string.IsNullOrEmpty(loaded.MqttPassword))
                            {
                                MqttPassword = loaded.MqttPassword;
                            }

                            if (!string.IsNullOrEmpty(loaded.GpsComPort))
                            {
                                GpsComPort = loaded.GpsComPort;
                            }

                            if (loaded.GpsBaudRate > 0)
                            {
                                GpsBaudRate = loaded.GpsBaudRate;
                            }

                            if (loaded.GpsRxPin > 0)
                            {
                                GpsRxPin = loaded.GpsRxPin;
                            }

                            if (loaded.GpsTxPin > 0)
                            {
                                GpsTxPin = loaded.GpsTxPin;
                            }

                            if (loaded.GpsStartupDelayMs > 0)
                            {
                                GpsStartupDelayMs = loaded.GpsStartupDelayMs;
                            }

                            if (loaded.PostGpsDataDelayMs > 0)
                            {
                                PostGpsDataDelayMs = loaded.PostGpsDataDelayMs;
                            }

                            if (loaded.DeepSleepMinutes > 0)
                            {
                                DeepSleepMinutes = loaded.DeepSleepMinutes;
                            }

                            Console.WriteLine("Configuration loaded from storage.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
            }
        }
    }
}
