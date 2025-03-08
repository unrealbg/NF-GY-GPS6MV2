namespace NF_GY_GPS6MV2
{
    using nanoFramework.Networking;
    using nanoFramework.M2Mqtt;
    using nanoFramework.Json;
    using System;
    using System.IO.Ports;
    using System.Text;
    using System.Threading;

    public class Program
    {
        private static SerialPort _serialPort;
        private static string _buffer = string.Empty;
        private static GpsData _lastGpsData;
        private static Timer _displayTimer;

        private static MqttClient _mqttClient;

        private const string MqttBrokerAddress = "YourMqttBroker";
        private const string MqttTopic = "gps/coords";
        private const string MqttClientId = "MyGpsClient";

        public static void Main()
        {
            ConnectToWiFi();
            InitializeMqtt();
            InitializeSerial();

            _displayTimer = new Timer(DisplayTimerCallback, null, 60000, 60000);

            Thread.Sleep(Timeout.Infinite);
        }

        private static void ConnectToWiFi()
        {
            const string ssid = "SSID";
            const string password = "PASS";

            bool success = WifiNetworkHelper.ConnectDhcp(ssid, password, requiresDateTime: false);
            if (success)
            {
                Console.WriteLine("Connected to WiFi network successfully");
            }
            else
            {
                Console.WriteLine("WiFi connection failed");
            }
        }

        private static void InitializeMqtt()
        {
            try
            {
                const string mqttUser = "YourMqttUser";
                const string mqttPassword = "YourMqttPassword";

                _mqttClient = new MqttClient(MqttBrokerAddress);
                _mqttClient.Connect(MqttClientId, mqttUser, mqttPassword);

                if (_mqttClient.IsConnected)
                {
                    Console.WriteLine("Connected to MQTT broker: " + MqttBrokerAddress);
                }
                else
                {
                    Console.WriteLine("MQTT connection failed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error initializing MQTT: " + ex.Message);
            }
        }

        private static void InitializeSerial()
        {
            try
            {
                _serialPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();
                Console.WriteLine("Serial port initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error initializing serial port: " + ex.Message);
            }
        }

        private static void DisplayTimerCallback(object state)
        {
            if (_lastGpsData != null)
            {
                // 1) Текстово съобщение на конзолата
                string summary =
                    "GPS Data Summary:\r\n" +
                    "Time: " + _lastGpsData.Time + "\r\n" +
                    "Latitude: " + _lastGpsData.Latitude + "\r\n" +
                    "Longitude: " + _lastGpsData.Longitude + "\r\n" +
                    "Fix Quality: " + _lastGpsData.FixQuality + "\r\n" +
                    "Num Satellites: " + _lastGpsData.NumSatellites + "\r\n" +
                    "HDOP: " + _lastGpsData.HDOP + "\r\n" +
                    "Altitude: " + _lastGpsData.Altitude + "\r\n";

                Console.WriteLine(summary);

                string jsonData = JsonConvert.SerializeObject(_lastGpsData);

                if (_mqttClient != null && _mqttClient.IsConnected)
                {
                    try
                    {
                        _mqttClient.Publish(
                            MqttTopic,
                            Encoding.UTF8.GetBytes(jsonData));

                        Console.WriteLine("Published GPS data as JSON to MQTT topic: " + MqttTopic);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error publishing to MQTT: " + ex.Message);
                    }
                }
                else
                {
                    Console.WriteLine("MQTT client not connected");
                }
            }
            else
            {
                Console.WriteLine("No GPS data available");
            }
        }

        private static void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort.ReadExisting();
                _buffer += data;

                while (true)
                {
                    int startIndex = _buffer.IndexOf('$');
                    if (startIndex < 0)
                    {
                        _buffer = string.Empty;
                        break;
                    }

                    int nextIndex = _buffer.IndexOf('$', startIndex + 1);
                    if (nextIndex < 0)
                    {
                        if (startIndex > 0)
                        {
                            _buffer = _buffer.Substring(startIndex);
                        }

                        break;
                    }

                    string message = _buffer.Substring(startIndex, nextIndex - startIndex);
                    ProcessParsedMessage(message);

                    _buffer = _buffer.Substring(nextIndex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading serial port data: " + ex.Message);
            }
        }

        private static void ProcessParsedMessage(string message)
        {
            if (message.StartsWith("$GNGGA"))
            {
                ParseGNGGA(message);
            }
        }

        private static void ParseGNGGA(string message)
        {
            try
            {
                string[] parts = message.Split(',');
                if (parts.Length < 15)
                {
                    Console.WriteLine("Invalid GNGGA message: " + message);
                    return;
                }

                string time = parts[1];
                string latitude = parts[2];
                string latDir = parts[3];
                string longitude = parts[4];
                string lonDir = parts[5];
                string fixQuality = parts[6];
                string numSatellites = parts[7];
                string hdop = parts[8];
                string altitude = parts[9];

                if (fixQuality == "0")
                {
                    return;
                }

                double latDegrees = 0;
                double lonDegrees = 0;

                if (!string.IsNullOrEmpty(latitude))
                {
                    int latDeg = int.Parse(latitude.Substring(0, 2));
                    double latMin = double.Parse(latitude.Substring(2));
                    latDegrees = latDeg + latMin / 60.0;
                    if (latDir == "S")
                    {
                        latDegrees = -latDegrees;
                    }
                }

                if (!string.IsNullOrEmpty(longitude))
                {
                    int lonDeg = int.Parse(longitude.Substring(0, 3));
                    double lonMin = double.Parse(longitude.Substring(3));
                    lonDegrees = lonDeg + lonMin / 60.0;
                    if (lonDir == "W")
                    {
                        lonDegrees = -lonDegrees;
                    }
                }

                _lastGpsData = new GpsData
                {
                    Time = time,
                    Latitude = latDegrees,
                    Longitude = lonDegrees,
                    FixQuality = fixQuality,
                    NumSatellites = numSatellites,
                    HDOP = hdop,
                    Altitude = altitude
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing GNGGA message: " + ex.Message);
            }
        }
    }
}
