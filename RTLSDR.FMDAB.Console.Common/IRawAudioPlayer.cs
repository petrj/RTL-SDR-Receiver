using LoggerService;
using NLog;
using RTLSDR.DAB;
using RTLSDR.FM;
using System;
using System.IO;
using RTLSDR.Common;

namespace RTLSDR.FMDAB.Console.Common
{
    public interface IRawAudioPlayer
    {
        void Init();

        void Play();

        void AddPCM(byte[] data);


        void Stop();
    }
}    

