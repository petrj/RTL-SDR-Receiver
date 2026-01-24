using RTLSDR.DAB;

namespace RadI0;

    public class Station
    {
        public string Name { get; set; }
        public int ServiceNumber { get; set; }

        public DABService Service { get; set; }

        public Station(string name, int serviceNumber)
        {
            Name = name;
            ServiceNumber = serviceNumber;
        }
    }