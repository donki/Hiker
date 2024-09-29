using System.Xml.Serialization;

namespace Hiker.Helpers
{

    // Definir las clases para el formato GPX 1.1

    [XmlRoot("gpx", Namespace = "http://www.topografix.com/GPX/1/1")]
    public class GPXFileHelper
    {
        [XmlAttribute("version")]
        public string Version { get; set; } = "1.1";

        [XmlAttribute("creator")]
        public string Creator { get; set; }

        [XmlElement("trk")]
        public List<Track> Tracks { get; set; } = new List<Track>();

        public static GPXFileHelper FromStream(Stream stream)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GPXFileHelper));
            return (GPXFileHelper)serializer.Deserialize(stream);
        }

        public static GPXFileHelper FromXML(string XML)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GPXFileHelper));

            // Convertir el string XML a un StringReader, que implementa TextReader
            using (StringReader reader = new StringReader(XML))
            {
                // Deserializar el XML desde el StringReader
                return (GPXFileHelper)serializer.Deserialize(reader);
            }
        }


        public Stream ToStream(GPXFileHelper helper)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GPXFileHelper));

            MemoryStream memoryStream = new MemoryStream();

            serializer.Serialize(memoryStream, helper);

            memoryStream.Position = 0;

            return memoryStream;
        }

        public Stream ToStream()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GPXFileHelper));

            MemoryStream memoryStream = new MemoryStream();

            serializer.Serialize(memoryStream, this);

            memoryStream.Position = 0;

            return memoryStream;
        }

        public string ToXML()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GPXFileHelper));

            // Usar StringWriter para escribir el XML en una cadena
            using (StringWriter stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, this);
                return stringWriter.ToString(); // Retornar el XML como string
            }
        }
    }

    public class Track
    {
        [XmlElement("name")]
        public string Name { get; set; }

        [XmlElement("trkseg")]
        public List<TrackSegment> Segments { get; set; } = new List<TrackSegment>();
    }

    public class TrackSegment
    {
        [XmlElement("trkpt")]
        public List<TrackPoint> TrackPoints { get; set; } = new List<TrackPoint>();
    }

    public class TrackPoint
    {

        public TrackPoint()
        {
            // Constructor sin parámetros para la serialización
        }
        public TrackPoint(double latitude, double longitude, double? altitude, DateTime dateTime)
        {
            Latitude = latitude;
            Longitude = longitude;
            if (altitude.HasValue)
            {
                Elevation = altitude.Value;
            }
            else
            {
                Elevation = 0;
            }
            Time = dateTime;
        }

        [XmlAttribute("lat")]
        public double Latitude { get; set; }

        [XmlAttribute("lon")]
        public double Longitude { get; set; }

        [XmlElement("ele")]
        public double Elevation { get; set; }

        [XmlElement("time")]
        public DateTime Time { get; set; }
    }

}


