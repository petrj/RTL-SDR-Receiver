using System;
using System.Net.Sockets;
using System.Net;
using NAudio.Wave;
using RTLSDR.Common;
using LoggerService;
using System.Collections.Concurrent;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RTLSDR.Audio
{
    public class NAudioRawAudioPlayer : IRawAudioPlayer
    {
        private ILoggingService _loggingService;

        public static WaveOutEvent _outputDevice;
        public static BufferedWaveProvider _bufferedWaveProvider;

        private DateTime _lastAudioProcessTime = DateTime.MinValue;
        private double _inputBitRate = 0;

        private Thread _audioThread = null;
        private bool _audioThreadRunning = true;
        private long _queueDataSize = 0;

        private ConcurrentQueue<byte[]> _audioQueue;
        private AudioDataDescription _audioDescription;

        private BitRateCalculation _bitrateCalculation;

        public NAudioRawAudioPlayer(ILoggingService loggingService)
        {
            _loggingService = loggingService;
            if (_loggingService == null)
            {
                _loggingService = new DummyLoggingService();
            }

            _bitrateCalculation = new BitRateCalculation(_loggingService, "NAudio BitRate");
            _audioQueue = new ConcurrentQueue<byte[]>();
            _audioDescription = new AudioDataDescription()
            {
                BitsPerSample = 16,
                Channels = 2,
                SampleRate = 48000
            };
        }

        public void Init(AudioDataDescription audioDescription)
        {
            _audioDescription = audioDescription;
            _outputDevice = new WaveOutEvent();
            var waveFormat = new WaveFormat(audioDescription.SampleRate, audioDescription.BitsPerSample, audioDescription.Channels);
            _bufferedWaveProvider = new BufferedWaveProvider(waveFormat);
            _outputDevice.Init(_bufferedWaveProvider);

            _audioThread = new Thread(AudioLoop);
            _audioThread.Start();
            _audioThreadRunning = true;
        }

        public void Play()
        {
            _audioQueue.Clear();
            _queueDataSize = 0;
            _outputDevice.Play();
        }

        public void AddPCM(byte[] data)
        {
            if (data == null)
                return;

            _inputBitRate = _bitrateCalculation.GetBitRate(data.Length);
            _audioQueue.Enqueue(data);
            _queueDataSize += data.Length;

            //_bufferedWaveProvider.AddSamples(data, 0, data.Length);
        }

        public void Stop()
        {
            _outputDevice.Stop();
            _bufferedWaveProvider.ClearBuffer();

            while (_audioThread != null && _audioThread.ThreadState == ThreadState.Running)
            {
                _audioThreadRunning = false;
                Thread.Sleep(50);
            };
        }

        private void AudioLoop()
        {
            _loggingService.Info("NAudioRawAudioPlayer AudioLoop");

            while (_audioThreadRunning)
            {
                if (_audioQueue.Count>0)
                {
                    byte[] bytes;
                    if (_audioQueue.TryDequeue(out bytes))
                    {
                        _queueDataSize -= bytes.Length;
                        _bufferedWaveProvider.AddSamples(bytes, 0, bytes.Length);
                       // Thread.Sleep(50);
                    }
                } else
                {
                    Thread.Sleep(50);
                }

                    /*

                    if ((DateTime.Now - _lastAudioProcessTime).TotalMilliseconds >= 1000)
                    {
                        // dequeue 1s => 96 kbps samples (16 bit mono) => 96 000 * 2 bytes per 1 sec =>
                        double bytesToDequeue = _audioDescription.SampleRate * _audioDescription.Channels * (_audioDescription.BitsPerSample / 8);

                        _loggingService.Debug($" ##-## _queueDataSize: {_queueDataSize},  bytesToDequeue: {bytesToDequeue}");

                        var dequeuedBytes = 0;
                        var buffer = new List<byte>();

                        if (_queueDataSize >= bytesToDequeue)
                        {
                            while (dequeuedBytes < bytesToDequeue)
                            {
                                byte[] bytes;
                                if (_audioQueue.TryDequeue(out bytes))
                                {
                                    // _loggingService.Debug($" ##-## dequeuedBytes: {dequeuedBytes}");

                                    buffer.AddRange(bytes);

                                    _queueDataSize -= bytes.Length;
                                    dequeuedBytes += bytes.Length;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            //_bufferedWaveProvider.AddSamples(buffer.ToArray(), 0, dequeuedBytes);
                        }

                        _lastAudioProcessTime = DateTime.Now;
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                    */
            }
        }
    }
}