namespace NF_GY_GPS6MV2
{
    public static class AppSettings
    {
        // Broker settings
        public const string MqttBrokerAddress = "Broker Address";
        public const string MqttTopic = "gps/coords";
        public const string MqttClientId = "MyGpsClient";

        // WiFi settings
        public const string WifiSsid = "SSID";
        public const string WifiPassword = "Pass";

        // MQTT credentials
        public const string MqttUser = "mqttUser";
        public const string MqttPassword = "mqttPass";

        // GPS settings
        public const string GpsComPort = "COM2";
        public const int GpsBaudRate = 9600;
        public const int GpsRxPin = 16;
        public const int GpsTxPin = 17;

        // Timing settings
        public const int GpsStartupDelayMs = 10000;
        public const int PostGpsDataDelayMs = 3000;
        public const int DeepSleepMinutes = 1;
    }
}
