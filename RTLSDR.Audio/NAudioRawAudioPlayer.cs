using System;
using NAudio.Wave;

namespace RTLSDR.Audio
{
    public class NAudioRawAudioPlayer : IRawAudioPlayer
    {
        public static WaveOutEvent _outputDevice;
        public static BufferedWaveProvider _bufferedWaveProvider;

        public void Init()
        {
            _outputDevice = new WaveOutEvent();
            var waveFormat = new WaveFormat(48000, 16, 2);
            _bufferedWaveProvider = new BufferedWaveProvider(waveFormat);
            _outputDevice.Init(_bufferedWaveProvider);        }

        public void Play()
        {
            _outputDevice.Play();
        }

        public void AddPCM(byte[] data)
        {
            _bufferedWaveProvider.AddSamples(data, 0, data.Length);
        }

        public void Stop()
        {
            _outputDevice.Stop();
            _bufferedWaveProvider.ClearBuffer();
        }
    }
}