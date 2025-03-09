namespace NF_GY_GPS6MV2.Services
{
    using System;
    using System.IO.Ports;

    using nanoFramework.Hardware.Esp32;

    using NF_GY_GPS6MV2.Models;

    public class GpsService
    {
        private SerialPort _serialPort;
        private string _buffer = string.Empty;

        public GpsData LastGpsData { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GpsService"/> class.
        /// </summary>
        /// <param name="comPort"></param>
        /// <param name="baudRate"></param>
        /// <param name="rxPin"></param>
        /// <param name="txPin"></param>
        public GpsService(string comPort, int baudRate, int rxPin, int txPin)
        {
            // Set pin functions
            Configuration.SetPinFunction(rxPin, DeviceFunction.COM2_RX);
            Configuration.SetPinFunction(txPin, DeviceFunction.COM2_TX);

            // Initialize serial port
            _serialPort = new SerialPort(comPort, baudRate, Parity.None, 8, StopBits.One);
            _serialPort.DataReceived += SerialPort_DataReceived;
        }

        /// <summary>
        /// Starts the GPS service.
        /// </summary>
        public void Start()
        {
            _serialPort.Open();
            Console.WriteLine("GPS serial port opened successfully");
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
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
                    this.ProcessParsedMessage(message);

                    _buffer = _buffer.Substring(nextIndex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading serial port data: " + ex.Message);
            }
        }

        private void ProcessParsedMessage(string message)
        {
            if (this.LastGpsData == null)
            {
                this.LastGpsData = new GpsData();
            }

            if (message.StartsWith("$GNGGA"))
            {
                this.ParseGNGGA(message);
            }
        }

        private void ParseGNGGA(string message)
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
                    if (latDir == "S") latDegrees = -latDegrees;
                }

                if (!string.IsNullOrEmpty(longitude))
                {
                    int lonDeg = int.Parse(longitude.Substring(0, 3));
                    double lonMin = double.Parse(longitude.Substring(3));
                    lonDegrees = lonDeg + lonMin / 60.0;
                    if (lonDir == "W") lonDegrees = -lonDegrees;
                }

                this.LastGpsData.Time = time;
                this.LastGpsData.Latitude = latDegrees;
                this.LastGpsData.Longitude = lonDegrees;
                this.LastGpsData.FixQuality = fixQuality;
                this.LastGpsData.NumSatellites = numSatellites;
                this.LastGpsData.HDOP = hdop;
                this.LastGpsData.Altitude = altitude;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing GNGGA message: " + ex.Message);
            }
        }
    }
}
