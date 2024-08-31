
using LoggerService;
using RTLSDR.Common;

namespace RTLSDR.Audio
{
    public interface IRawAudioPlayer
    {
        void Init(AudioDataDescription audioDescription);

        void Play();

        void AddPCM(byte[] data);


        void Stop();
    }
}

