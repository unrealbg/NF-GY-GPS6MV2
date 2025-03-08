namespace NF_GY_GPS6MV2
{
    public class GpsData
    {
        public string Time { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public string FixQuality { get; set; }

        public string NumSatellites { get; set; }

        public string HDOP { get; set; }

        public string Altitude { get; set; }
    }
}
