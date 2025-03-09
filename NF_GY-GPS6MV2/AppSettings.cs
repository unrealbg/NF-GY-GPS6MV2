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
    }
}
