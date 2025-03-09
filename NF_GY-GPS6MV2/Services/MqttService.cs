namespace NF_GY_GPS6MV2.Services
{
    using System;
    using System.Text;

    using nanoFramework.M2Mqtt;

    public class MqttService
    {
        private MqttClient _client;

        public bool IsConnected => _client != null && _client.IsConnected;

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttService"/> class.
        /// </summary>
        /// <param name="brokerAddress"></param>
        /// <param name="clientId"></param>
        /// <param name="user"></param>
        /// <param name="pass"></param>
        public MqttService(string brokerAddress, string clientId, string user, string pass)
        {
            try
            {
                _client = new MqttClient(brokerAddress);
                _client.Connect(clientId, user, pass);

                if (_client.IsConnected)
                {
                    Console.WriteLine("Connected to MQTT broker: " + brokerAddress);
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

        /// <summary>
        /// Publishes a message to the specified topic.
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="message"></param>
        public void Publish(string topic, string message)
        {
            if (!this.IsConnected)
            {
                Console.WriteLine("MQTT client not connected");
                return;
            }
            try
            {
                _client.Publish(topic, Encoding.UTF8.GetBytes(message));
                Console.WriteLine($"Published to {topic}: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error publishing to MQTT: " + ex.Message);
            }
        }
    }
}
