using System.Threading;
using RTLSDR.Common;
using System.Collections.Generic;
using System.Collections.Concurrent;
using LoggerService;

namespace RTLSDR.Audio;

public class BalanceBuffer
{    
    private Thread _thread = null;
    private bool _running = false;
    private int _actionMSDelay = 100;
    private Action<byte[]> _actionPlay = null;
    private DateTime _timeStarted = DateTime.Now;
    private ConcurrentQueue<byte[]> _queue = new ConcurrentQueue<byte[]>();
            
    private const int MinThreadNoDataMSDelay = 25;

    private ILoggingService _loggingService;

    private AudioDataDescription _audioDescription;

    public BalanceBuffer(ILoggingService loggingService, Action<byte[]> actionPlay)
    {
        _loggingService = loggingService;

        _actionPlay = actionPlay;

        _loggingService.Info("Starting Balance buffer");        
        _timeStarted = DateTime.Now;

        _queue = new ConcurrentQueue<byte[]>();

        _audioDescription = new AudioDataDescription();

        _running = true;
        _thread = new Thread(ThreadLoop);
        _thread.Start();
    }

    public void SetAudioDataDescription(AudioDataDescription audioDescription)
    {
        _audioDescription = audioDescription;

        _loggingService.Info($"Adio Balance      : Samplerate: {_audioDescription.SampleRate}");
        _loggingService.Info($"Adio Channels     : Samplerate: {_audioDescription.Channels}");
        _loggingService.Info($"Adio BitsPerSample: Samplerate: {_audioDescription.BitsPerSample}");
    }

    public void AddData(byte[] data)
    {
        _loggingService.Info($"Adding {data.Length} bytes to balance buffer");        
        _queue.Enqueue(data);
    }

    private void ThreadLoop()
    {
        _loggingService.Info("Starting Balance thread");        

        try
        {
            while (_running)
            {
                //_cycles++;
                byte[] data = null;

                var ok = _queue.TryDequeue(out data);
                /*
                if (_action != null && data != null)
                {
                    var startTime = DateTime.Now;

                    _action(data);

                    _workingTimeMS += (DateTime.Now - startTime).TotalMilliseconds;
                } else
                {
                    Thread.Sleep(_actionMSDelay);
                }
                */

                Thread.Sleep(_actionMSDelay);
            }
        }
        catch (Exception ex)
        {
            _loggingService.Error(ex);
        }

        _loggingService.Info("Balance thread stopped");        
    }

    private void ReceiveData(byte[] AUData)
    {    
        if (AUData == null)
        {
            _loggingService.Info("No data");
            return;
        }

        _loggingService.Info($"Received: {AUData.Length} bytes");     
    }
}
