namespace NF_GY_GPS6MV2.Services
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.IO.Ports;
    using System.Threading;

    using nanoFramework.Hardware.Esp32;
    using nanoFramework.Json;

    using NF_GY_GPS6MV2.Models;
    using static NF_GY_GPS6MV2.AppSettings;

    public class GpsService : IDisposable
    {
        private SerialPort _serialPort;
        private string _buffer = string.Empty;
        private bool _disposed = false;
        private const int MAX_BUFFER_SIZE = 1024;
        private int _errorCount = 0;
        private const int MAX_ERROR_COUNT = 10;
        private const int ERROR_RESET_INTERVAL = 60000;
        private Timer _errorResetTimer;
        private double _lastSpeedKmh = 0;
        private const string LAST_GPS_FILE = StorageRoot + "\\last_gps.txt";

        private readonly object _dataLock = new object();
        private GpsData _lastGpsData;
        public GpsData LastGpsData
        {
            get
            {
                lock (_dataLock)
                {
                    return _lastGpsData;
                }
            }
        }

        private static bool TryParseDoubleCultureAware(string s, out double value)
        {
            try
            {
                char ds = NumberFormatInfo.CurrentInfo.NumberDecimalSeparator[0];
                if (ds != '.')
                {
                    char[] chars = s.ToCharArray();
                    for (int i = 0; i < chars.Length; i++)
                    {
                        if (chars[i] == '.')
                        {
                            chars[i] = ds;
                        }
                    }
                    s = new string(chars);
                }
                value = double.Parse(s);
                return true;
            }
            catch
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GpsService"/> class.
        /// </summary>
        /// <param name="comPort">COM port name </param>
        /// <param name="baudRate">Baud rate</param>
        /// <param name="rxPin">RX pin number</param>
        /// <param name="txPin">TX pin number</param>
        public GpsService(string comPort, int baudRate, int rxPin, int txPin)
        {
            Configuration.SetPinFunction(rxPin, DeviceFunction.COM2_RX);
            Configuration.SetPinFunction(txPin, DeviceFunction.COM2_TX);

            _serialPort = new SerialPort(comPort, baudRate, Parity.None, 8, StopBits.One);
            _serialPort.ReadTimeout = 500;
            _serialPort.WriteTimeout = 500;
            _serialPort.DataReceived += this.SerialPort_DataReceived;

            _errorResetTimer = new Timer(this.ResetErrorCount, null, ERROR_RESET_INTERVAL, ERROR_RESET_INTERVAL);

            this.LoadLastGpsData();
        }

        private void ResetErrorCount(object state)
        {
            if (_errorCount > 0)
            {
                _errorCount = 0;
                Console.WriteLine("GPS error count reset");
            }
        }

        /// <summary>
        /// Starts the GPS service.
        /// </summary>
        public void Start()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("GpsService");
            }

            try
            {
                if (!_serialPort.IsOpen)
                {
                    _serialPort.Open();
                    Console.WriteLine("GPS serial port opened successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open GPS serial port: {ex.Message}");
                throw;
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (_buffer.Length >= MAX_BUFFER_SIZE)
                {
                    Console.WriteLine("Buffer size limit reached, clearing buffer");
                    _buffer = string.Empty;
                }

                string data = _serialPort.ReadExisting();
                if (string.IsNullOrEmpty(data))
                {
                    return;
                }

                _buffer += data;

                this.ProcessBuffer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading serial port data: " + ex.Message);
                _errorCount++;

                if (_errorCount >= MAX_ERROR_COUNT)
                {
                    Console.WriteLine("Too many consecutive errors, resetting GPS service");
                    this.SafeReset();
                }

                Thread.Sleep(1000);
            }
        }

        private void ProcessBuffer()
        {
            if (_buffer.Length > MAX_BUFFER_SIZE)
            {
                Console.WriteLine("Buffer too large, truncating");
                int lastDollarIndex = _buffer.LastIndexOf('$');
                if (lastDollarIndex > 0)
                {
                    _buffer = _buffer.Substring(lastDollarIndex);
                }
                else
                {
                    _buffer = string.Empty;
                }
            }

            while (true)
            {
                int startIndex = _buffer.IndexOf('$');
                if (startIndex < 0)
                {
                    _buffer = string.Empty;
                    break;
                }

                // Support CR or LF as sentence terminators
                int endCR = _buffer.IndexOf('\r', startIndex);
                int endLF = _buffer.IndexOf('\n', startIndex);
                int endIndex = -1;
                if (endCR >= 0 && endLF >= 0)
                {
                    endIndex = endCR < endLF ? endCR : endLF;
                }
                else if (endCR >= 0)
                {
                    endIndex = endCR;
                }
                else if (endLF >= 0)
                {
                    endIndex = endLF;
                }

                if (endIndex < 0)
                {
                    if (startIndex > 0)
                    {
                        _buffer = _buffer.Substring(startIndex);
                    }

                    break;
                }

                if (endIndex - startIndex > 5 && endIndex - startIndex < MAX_BUFFER_SIZE)
                {
                    string message = _buffer.Substring(startIndex, endIndex - startIndex);

                    if (IsValidNmeaChecksum(message))
                    {
                        this.SafeProcessMessage(message);
                    }
                }

                if (endIndex + 1 < _buffer.Length)
                {
                    _buffer = _buffer.Substring(endIndex + 1);
                }
                else
                {
                    _buffer = string.Empty;
                    break;
                }
            }
        }

        private static bool IsValidNmeaChecksum(string sentence)
        {
            int starPos = sentence.IndexOf('*');
            if (starPos <= 0)
            {
                // No checksum present; accept for tolerance
                return true;
            }

            int checksum = 0;
            for (int i = 1; i < starPos; i++)
            {
                checksum ^= sentence[i];
            }

            string hex = sentence.Substring(starPos + 1);
            if (hex.Length < 2) return false;

            try
            {
                int parsed = Convert.ToInt32(hex.Substring(0, 2), 16);
                return checksum == parsed;
            }
            catch
            {
                return false;
            }
        }

        private void SafeProcessMessage(string message)
        {
            try
            {
                this.ProcessParsedMessage(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing GPS message: {ex.Message}");
                _errorCount++;
            }
        }

        private void ProcessParsedMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (message.StartsWith("$GNGGA") || message.StartsWith("$GPGGA"))
            {
                this.ParseGNGGA(message);
            }
            else if (message.StartsWith("$GNVTG") || message.StartsWith("$GPVTG"))
            {
                this.ParseGNVTG(message);
            }
            else if (message.StartsWith("$GNRMC") || message.StartsWith("$GPRMC"))
            {
                this.ParseGNRMC(message);
            }
        }

        private void ParseGNGGA(string message)
        {
            try
            {
                string[] parts = message.Split(',');
                if (parts.Length < 10)
                {
                    Console.WriteLine($"GNGGA message too short: '{message}'");
                    return;
                }

                string time = parts.Length > 1 ? parts[1] : string.Empty;
                string latitude = parts.Length > 2 ? parts[2] : string.Empty;
                string latDir = parts.Length > 3 ? parts[3] : string.Empty;
                string longitude = parts.Length > 4 ? parts[4] : string.Empty;
                string lonDir = parts.Length > 5 ? parts[5] : string.Empty;
                string fixQuality = parts.Length > 6 ? parts[6] : string.Empty;
                string numSatellites = parts.Length > 7 ? parts[7] : string.Empty;
                string hdop = parts.Length > 8 ? parts[8] : string.Empty;
                string altitude = parts.Length > 9 ? parts[9] : string.Empty;

                if (string.IsNullOrEmpty(fixQuality) || fixQuality == "0" || string.IsNullOrEmpty(latitude) || string.IsNullOrEmpty(longitude))
                {
                    Console.WriteLine($"GNGGA invalid fix or missing lat/lon: '{message}'");
                    return;
                }

                double latDegrees = 0;
                double lonDegrees = 0;

                int latDeg = 0;
                double latMin = 0;
                bool okLatDeg = int.TryParse(latitude.Length >= 2 ? latitude.Substring(0, 2) : string.Empty, out latDeg);
                bool okLatMin = TryParseDoubleCultureAware(latitude.Length > 2 ? latitude.Substring(2) : string.Empty, out latMin);
                if (latitude.Length >= 4 && okLatDeg && okLatMin)
                {
                    latDegrees = latDeg + latMin / 60.0;
                    if (latDir == "S")
                    {
                        latDegrees = -latDegrees;
                    }
                }
                else
                {
                    Console.WriteLine($"GNGGA invalid latitude: '{latitude}' in '{message}'");
                }

                int lonDeg = 0;
                double lonMin = 0;
                bool okLonDeg = int.TryParse(longitude.Length >= 3 ? longitude.Substring(0, 3) : string.Empty, out lonDeg);
                bool okLonMin = TryParseDoubleCultureAware(longitude.Length > 3 ? longitude.Substring(3) : string.Empty, out lonMin);
                if (longitude.Length >= 5 && okLonDeg && okLonMin)
                {
                    lonDegrees = lonDeg + lonMin / 60.0;
                    if (lonDir == "W")
                    {
                        lonDegrees = -lonDegrees;
                    }
                }
                else
                {
                    Console.WriteLine($"GNGGA invalid longitude: '{longitude}' in '{message}'");
                }

                var newData = new GpsData
                {
                    Time = time,
                    Latitude = latDegrees,
                    Longitude = lonDegrees,
                    FixQuality = fixQuality,
                    NumSatellites = numSatellites,
                    HDOP = hdop,
                    Altitude = altitude,
                    SpeedKmh = _lastSpeedKmh,
                    IsValid = !string.IsNullOrEmpty(fixQuality) && fixQuality != "0" && latDegrees != 0 && lonDegrees != 0,
                    Date = DateTime.UtcNow
                };

                lock (_dataLock)
                {
                    _lastGpsData = newData;
                }
                _errorCount = 0;
                this.SaveLastGpsData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing GNGGA message: {ex.Message}\n{ex.StackTrace}");
                _errorCount++;
            }
        }

        private void ParseGNVTG(string message)
        {
            try
            {
                string[] parts = message.Split(',');
                if (parts.Length < 8)
                {
                    Console.WriteLine($"GNVTG message too short: '{message}'");
                    return;
                }

                string speedKmhStr = parts[7];
                if (!string.IsNullOrEmpty(speedKmhStr))
                {
                    double parsed;
                    if (TryParseDoubleCultureAware(speedKmhStr, out parsed))
                    {
                        _lastSpeedKmh = parsed;
                    }
                    else
                    {
                        Console.WriteLine($"GNVTG invalid speed: '{speedKmhStr}' in '{message}'");
                    }
                }

                _errorCount = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing GNVTG message: {ex.Message}\n{ex.StackTrace}");
                _errorCount++;
            }
        }

        private void ParseGNRMC(string message)
        {
            try
            {
                // RMC: $xxRMC,time,status,lat,NS,lon,EW,speedKnots,course,date,...
                var parts = message.Split(',');
                if (parts.Length < 10)
                {
                    Console.WriteLine($"GNRMC message too short: '{message}'");
                    return;
                }

                string status = parts[2];
                if (status != "A") // A=Active, V=Void
                {
                    return;
                }

                string time = parts[1]; // hhmmss.sss
                string date = parts[9]; // ddmmyy

                if (!string.IsNullOrEmpty(time) && time.Length >= 6 && !string.IsNullOrEmpty(date) && date.Length == 6)
                {
                    try
                    {
                        int hh = (time.Length >= 6) ? int.Parse(time.Substring(0, 2)) : 0;
                        int mm = (time.Length >= 6) ? int.Parse(time.Substring(2, 2)) : 0;
                        int ss = (time.Length >= 6) ? int.Parse(time.Substring(4, 2)) : 0;

                        int day = int.Parse(date.Substring(0, 2));
                        int month = int.Parse(date.Substring(2, 2));
                        int year = 2000 + int.Parse(date.Substring(4, 2));

                        DateTime utc = new DateTime(year, month, day, hh, mm, ss);

                        lock (_dataLock)
                        {
                            if (_lastGpsData != null)
                            {
                                _lastGpsData.Date = utc;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing RMC date/time: {ex.Message}");
                    }
                }

                _errorCount = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing GNRMC message: {ex.Message}\n{ex.StackTrace}");
                _errorCount++;
            }
        }

        private void SafeReset()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                    Thread.Sleep(1000);
                    _serialPort.Open();
                }

                _buffer = string.Empty;
                _errorCount = 0;

                Console.WriteLine("GPS service reset successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resetting GPS service: {ex.Message}");
            }
        }

        private void SaveLastGpsData()
        {
            try
            {
                var snapshot = this.LastGpsData;
                if (snapshot != null && snapshot.IsValid)
                {
                    var json = JsonConvert.SerializeObject(snapshot);
                    using (var fs = new FileStream(LAST_GPS_FILE, FileMode.Create))
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.Write(json);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving last GPS data: {ex.Message}");
            }
        }

        private void LoadLastGpsData()
        {
            try
            {
                if (!File.Exists(LAST_GPS_FILE))
                {
                    Console.WriteLine("No last GPS data file, skipping load.");
                    return;
                }

                using (var fs = new FileStream(LAST_GPS_FILE, FileMode.Open))
                using (var sr = new StreamReader(fs))
                {
                    var json = sr.ReadToEnd();
                    if (!string.IsNullOrEmpty(json))
                    {
                        var data = JsonConvert.DeserializeObject(json, typeof(GpsData)) as GpsData;
                        if (data != null)
                        {
                            lock (_dataLock)
                            {
                                _lastGpsData = data;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading last GPS data: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes the GPS service.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                if (_errorResetTimer != null)
                {
                    _errorResetTimer.Dispose();
                    _errorResetTimer = null;
                }

                if (_serialPort != null)
                {
                    _serialPort.DataReceived -= SerialPort_DataReceived;

                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }

                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }

            _disposed = true;
        }
    }
}