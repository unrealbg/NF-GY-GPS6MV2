namespace NF_GY_GPS6MV2.Models
{
    public class AppSettingsModel
    {
        public string MqttBrokerAddress { get; set; }

        public string MqttTopic { get; set; }

        public string MqttClientId { get; set; }

        public string WifiSsid { get; set; }

        public string WifiPassword { get; set; }

        public string MqttUser { get; set; }

        public string MqttPassword { get; set; }

        public string GpsComPort { get; set; }

        public int GpsBaudRate { get; set; }

        public int GpsRxPin { get; set; }

        public int GpsTxPin { get; set; }

        public int GpsStartupDelayMs { get; set; }

        public int PostGpsDataDelayMs { get; set; }

        public int DeepSleepMinutes { get; set; }
    }
}
