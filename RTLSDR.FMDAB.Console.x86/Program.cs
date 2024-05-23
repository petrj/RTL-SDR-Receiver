using System;
using System.Diagnostics;
using System.IO;
using LoggerService;
using RTLSDR.FM;
using RTLSDR.DAB;
using RTLSDR.Common;
using RTLSDR.FMDAB.Console.Common;

namespace RTLSDR.FMDAB.Console.x86
{
    public class MainClass
    {

        public static void Main(string[] args)
        {
            // test:
            //var aacDecoder = new AACDecoder(logger);
            //var decodeTest = aacDecoder.Test("c:\\temp\\AUData.1.aac.superframe");


            var app = new ConsoleApp("RTLSDR.FMDAB.Console.x86");
            app.OnDemodulated += Program_OnDemodulated;
            app.Run(args);

        }

        private static void Program_OnDemodulated(object sender, EventArgs e)
        {
            if (e is DataDemodulatedEventArgs ed)
            {
                if (ed.Data == null || ed.Data.Length == 0)
                {
                    return;
                }


            }
        }
    }
}
