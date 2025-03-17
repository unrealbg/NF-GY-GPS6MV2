﻿namespace NF_GY_GPS6MV2.Services
{
    using System;
    using System.IO.Ports;
    using System.Threading;

    using nanoFramework.Hardware.Esp32;

    using NF_GY_GPS6MV2.Models;

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

        public GpsData LastGpsData { get; private set; }

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

                int endIndex = _buffer.IndexOf('\r', startIndex);
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
                    this.SafeProcessMessage(message);
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

            if (message.StartsWith("$GNGGA"))
            {
                this.ParseGNGGA(message);
            }
            else if (message.StartsWith("$GNVTG"))
            {
                this.ParseGNVTG(message);
            }
        }

        private void ParseGNGGA(string message)
        {
            try
            {
                string[] parts = message.Split(',');
                if (parts.Length < 15)
                {
                    Console.WriteLine("Invalid GNGGA message format");
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

                if (fixQuality == "0" || string.IsNullOrEmpty(latitude) || string.IsNullOrEmpty(longitude))
                {
                    return;
                }

                double latDegrees = 0;
                double lonDegrees = 0;

                if (latitude.Length >= 2)
                {
                    int latDeg;
                    if (int.TryParse(latitude.Substring(0, 2), out latDeg))
                    {
                        double latMin;
                        if (double.TryParse(latitude.Substring(2), out latMin))
                        {
                            latDegrees = latDeg + latMin / 60.0;
                            if (latDir == "S") latDegrees = -latDegrees;
                        }
                    }
                }

                if (longitude.Length >= 3)
                {
                    int lonDeg;
                    if (int.TryParse(longitude.Substring(0, 3), out lonDeg))
                    {
                        double lonMin;
                        if (double.TryParse(longitude.Substring(3), out lonMin))
                        {
                            lonDegrees = lonDeg + lonMin / 60.0;
                            if (lonDir == "W") lonDegrees = -lonDegrees;
                        }
                    }
                }

                var newData = new GpsData
                {
                    Time = time,
                    Latitude = latDegrees,
                    Longitude = lonDegrees,
                    FixQuality = fixQuality,
                    NumSatellites = numSatellites,
                    HDOP = hdop,
                    Altitude = altitude
                };

                this.LastGpsData = newData;

                _errorCount = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing GNGGA message: " + ex.Message);
                _errorCount++;
            }
        }

        private void ParseGNVTG(string message)
        {
            try
            {
                string[] parts = message.Split(',');
                if (parts.Length < 9)
                {
                    Console.WriteLine("Invalid GNVTG message format");
                    return;
                }

                string speedKmhStr = parts[7];
                double speedKmh = 0;
                if (!string.IsNullOrEmpty(speedKmhStr))
                {
                    double.TryParse(speedKmhStr, out speedKmh);
                }

                if (this.LastGpsData == null)
                {
                    this.LastGpsData = new GpsData();
                }

                this.LastGpsData.SpeedKmh = speedKmh;

                _errorCount = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing GNVTG message: " + ex.Message);
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

        /// <summary>
        /// Disposes the GPS service.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_errorResetTimer != null)
                {
                    _errorResetTimer.Dispose();
                    _errorResetTimer = null;
                }

                if (_serialPort != null)
                {
                    try
                    {
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.Close();
                        }

                        _serialPort.DataReceived -= SerialPort_DataReceived;
                        _serialPort.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing serial port: {ex.Message}");
                    }

                    _serialPort = null;
                }
            }

            _disposed = true;
        }
    }
}