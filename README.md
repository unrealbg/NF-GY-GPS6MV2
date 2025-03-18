# NF-GY-GPS6MV2

A [.NET nanoFramework](https://www.nanoframework.net/) project that reads GPS data from a **GY-GPS6MV2** module on an **ESP32** device and publishes this data to an MQTT broker. After sending the data, the device enters **deep sleep** to save power.

## Features

- **Wi-Fi connection** with credentials from `AppSettings.cs`
- **GPS data** parsed from the GY-GPS6MV2 module (via `GpsService`)
- **MQTT publishing** of parsed GPS data (via `MqttService`)
- **Deep sleep** for low power consumption

## Requirements

- **Hardware**  
  - An ESP32 development board (e.g., ESP32-WROOM)  
  - A GY-GPS6MV2 (or compatible) GPS module  

- **Software**  
  - [Visual Studio 2022](https://visualstudio.microsoft.com/) (or VS Code) with the [nanoFramework extension](https://marketplace.visualstudio.com/items?itemName=nanoframework.nanoFramework-VS2019-Extension)  
  - [.NET nanoFramework firmware](https://docs.nanoframework.net/content/getting-started-guides/index.html) flashed onto the ESP32  
  - This repository cloned locally

- **NuGet Packages** (usually restored automatically by the project)  
  - `nanoFramework.Networking`  
  - `nanoFramework.Hardware.Esp32`  
  - `nanoFramework.Json`  
  - `nanoFramework.M2Mqtt`

## Wiring

Below is an example wiring between the ESP32 and the GY-GPS6MV2 module:

| GY-GPS6MV2 Pin | ESP32 Pin | Description          |
|----------------|-----------|----------------------|
| VCC            | 3.3V      | Power (3.3V)         |
| GND            | GND       | Ground               |
| TX             | GPIO16    | ESP32 RX (COM2)      |
| RX             | GPIO17    | ESP32 TX (COM2)      |

> **Note**: Ensure you power the GPS module with 3.3V if your ESP32 operates at 3.3V logic. Some GPS modules support 5V power, but the TX/RX pins must still be 3.3V-compatible.

## Getting Started

1. **Clone the repository**  
   ```bash
   git clone https://github.com/unrealbg/NF-GY-GPS6MV2.git
   
2. **Open the project**  
   - In Visual Studio, open the `NF-GY-GPS6MV2.sln` solution file.

3. **Configure your settings**  
   - In `AppSettings.cs`, set:
     - `WifiSsid` and `WifiPassword` for your Wi-Fi network  
     - `MqttBrokerAddress`, `MqttUser`, `MqttPassword`, etc., for your MQTT broker  
     - `GpsComPort`, `GpsBaudRate`, `GpsRxPin`, and `GpsTxPin` if needed
   - **Timing Settings:**  
     The configuration constants such as `GpsStartupDelayMs`, `PostGpsDataDelayMs`, and `DeepSleepMinutes` (intended for future deep sleep implementation) allow you to fine-tune the timing behavior. Currently, the deep sleep is simulated via a delay.

4. **Deploy to the ESP32**  
   - Connect your board via USB and ensure the correct target (ESP32) is selected in the **nanoFramework Device Explorer** in Visual Studio.  
   - Press **F5** (or **Deploy**) to build and flash the firmware.

5. **Monitor the output:**

   - Open the Debug Output window in Visual Studio or use a serial terminal on the appropriate COM port.
   - The device will:
     1. Connect to Wi-Fi.
     2. Read GPS data (GNGGA sentences).
     3. Publish JSON-formatted coordinates to the MQTT broker.
     4. Wait for the configured interval (simulating deep sleep) before repeating the cycle.

## Example Output

When the device sends GPS data, it publishes a JSON message similar to the following:

```json
{
  "NumSatellites": "10",
  "Latitude": 42.69751,
  "Date": "2025-03-16T20:01:08.1760350Z",
  "SpeedKmh": 0,
  "HDOP": "1.1",
  "Altitude": "95.8",
  "Longitude": 23.32415,
  "Time": "200038.000",
  "FixQuality": "1"
}
```
- This JSON includes the number of satellites, coordinates (latitude and longitude), date and time of the GPS fix, speed in km/h, horizontal dilution of precision (HDOP), altitude, and fix quality.

## Project Structure

- **`AppSettings.cs`**  
  Holds constants for Wi-Fi, MQTT, and GPS settings (pins, baud rate, etc.).
- **`Program.cs`**  
  Main entry point: connects to Wi-Fi, initializes the services (GpsService, MqttService, and ConnectionService), sends data, and then waits before starting a new cycle.
- **`GpsService.cs`**  
  Reads from the serial port (COM2) and parses GNGGA messages into a `GpsData` object.
- **`MqttService.cs`**  
  Manages MQTT publishing to the specified broker/topic.
- **`ConnectionService.cs`
  Handles Wi-Fi connectivity, including connection establishment and reconnection attempts.
- **`Models/GpsData.cs`**  
  A simple class holding the parsed GPS data.

## Usage

- **Adjust timing**: If you need more time to get a GPS fix or want to publish data at different intervals, modify the `Thread.Sleep` values in `Program.cs` or move them to `AppSettings.cs`.
- **Change pins**: If you use different GPIO pins, update `GpsRxPin` and `GpsTxPin` in `AppSettings.cs` and adjust `Configuration.SetPinFunction()` in `GpsService.cs`.
- **Logging**: Use `Console.WriteLine` (or `Debug.WriteLine`) to see messages in the debug console or a serial terminal.
- **MQTT Testing**: If you do not have your own MQTT broker, you can test using a public broker such as test.mosquitto.org.

## Contributing

Feel free to open an **issue** or **pull request** if you want to contribute to this project.

## License

This project is licensed under the [MIT License](https://github.com/unrealbg/NF-GY-GPS6MV2/blob/master/LICENSE.txt).
