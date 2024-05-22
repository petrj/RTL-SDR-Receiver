
namespace RTLSDR.Audio
{
    public interface IRawAudioPlayer
    {
        void Init();

        void Play();

        void AddPCM(byte[] data);


        void Stop();
    }
}    

