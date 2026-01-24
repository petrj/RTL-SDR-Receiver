using RTLSDR.DAB;

namespace RadI0;

    public class Station
    {
        public string Name { get; set; }
        public int ServiceNumber { get; set; }
        public int Frequency { get; set; }

        public DABService Service { get; set; }

        public Station(string name, int serviceNumber, int frequency)
        {
            Name = name;
            ServiceNumber = serviceNumber;
        }
    }