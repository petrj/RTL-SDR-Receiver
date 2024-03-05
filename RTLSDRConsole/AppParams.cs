using System;
namespace RTLSDRConsole
{
    public struct AppParams
    {
        public bool Help { get; set; }
        public bool FM { get; set; }
        public bool DAB { get; set; }
        public bool Emphasize { get; set; }

        public string InputFileName { get; set; }

        public string OutputFileName
        {
            get
            {
                var res = InputFileName;
                if (FM)
                {
                    res += ".fm";
                }
                if (DAB)
                {
                    res += ".aac";
                }
                return res;
            }
        }
    }
}
