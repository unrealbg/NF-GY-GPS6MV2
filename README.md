# NF-GY-GPS6MV2

**A nanoFramework project for ESP32-S3 that reads and parses GPS data from a GY-GPS6MV2 module. It periodically logs valid position data, serving as a simple foundation for IoT tracking or navigation applications.**

## Features

- **GPS Data Parsing:** Efficiently reads and parses data from the GY-GPS6MV2 module.
- **Periodic Logging:** Logs valid GPS position data periodically.
- **IoT Ready:** Ideal foundation for IoT tracking or navigation applications.
- **ESP32-S3:** Utilizes the powerful ESP32-S3 microcontroller.

## Getting Started

### Prerequisites

- **Hardware:**
  - ESP32-S3 microcontroller
  - GY-GPS6MV2 GPS module

- **Software:**
  - [nanoFramework](https://nanoframework.net/)
  - [Visual Studio](https://visualstudio.microsoft.com/)

### Installation

2. **Setup nanoFramework:**
   - Follow the [getting started guide](https://nanoframework.net/getting-started) to set up nanoFramework on your ESP32-S3.

3. **Build and Deploy:**
   - Open the project in Visual Studio.
   - Build the solution.
   - Deploy to your ESP32-S3 device.

### Usage

- **Connecting the GPS Module:**
  - Connect the GY-GPS6MV2 module to the ESP32-S3 as per the pin configuration.

- **Running the Application:**
  - Once deployed, the application will start reading and logging GPS data automatically.

### Configuration

- **Adjust Logging Interval:**
  - Modify the logging interval in the source code according to your requirements.
 
## Contributing

Feel free to fork the repository and submit pull requests. For major changes, please open an issue to discuss what you would like to change.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Thanks to the [nanoFramework](https://nanoframework.net/) community for their support and resources.
