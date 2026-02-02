namespace RadI0;

using System;
using RTLSDR.DAB;
using System.Collections.Concurrent;
using RTLSDR.Common;
using LoggerService;
using System.Reflection.Metadata.Ecma335;
using System.Text;

public class SpectrumWorker
{
    private readonly int _fftSize;
    private readonly float _sampleRate;

    private readonly float[] _window;
    private readonly FComplex[] _fftBuffer;

    private ThreadWorker<byte[]> _threadWorker = null;
    private ConcurrentQueue<byte[]> _spectrumQueue = new ConcurrentQueue<byte[]>();
    private ILoggingService _loggingService;
    private int _queueSize = 0;
    private System.Drawing.Point[] _spectrum;

    public SpectrumWorker(ILoggingService loggingService, int fftSize, float sampleRate)
    {
        if ((fftSize & (fftSize - 1)) != 0)
            throw new ArgumentException("FFT size must be power of two");

        _fftSize = fftSize;
        _sampleRate = sampleRate;
        _loggingService = loggingService;

        _window = CreateHannWindow(fftSize);
        _fftBuffer = new FComplex[fftSize];
        _spectrum = new System.Drawing.Point[_fftSize];

        _threadWorker = new ThreadWorker<byte[]>(loggingService, "SPECTRUM");
        _threadWorker.SetThreadMethod(SpectrumThreadWorkerGo, 500);
        //_threadWorker.SetQueue(_spectrumQueue);
        //_threadWorker.ReadingQueue = true;
        _threadWorker.Start();
    }

    public System.Drawing.Point[] Spectrum
    {
        get
        {
            return _spectrum;
        }
    }

    private void SpectrumThreadWorkerGo(object data = null)
    {
        try
        {
            if (_queueSize < 2*_fftSize)
            return;

            var buff = new byte[2*_fftSize];
            int size = 0;

            byte[] b;
            while (size< 2*_fftSize)
            {
                _spectrumQueue.TryDequeue(out b);
                if (b ==null)
                {
                    _spectrumQueue.Clear();
                    _queueSize = 0;
                    return; // no data
                }
                Buffer.BlockCopy(b, 0, buff, size,  b.Length + size > 2*_fftSize ?  2*_fftSize-size : b.Length);
                size += b.Length;
            }

            // clear queue
            _spectrumQueue.Clear();
            _queueSize = 0;

            PrepareBufferFromBytes(buff);

            UpdateSpectrum();

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            var s = new StringBuilder();
            s.AppendLine("X,Y");
            for (var i= 0;i<_fftSize;i++)
            {
                if (Spectrum[i].X < minX)
                     minX = Spectrum[i].X;
                if (Spectrum[i].Y < minY)
                     minY = Spectrum[i].Y;
                if (Spectrum[i].X > maxX)
                     maxX = Spectrum[i].X;
                if (Spectrum[i].Y > maxY)
                     maxY = Spectrum[i].Y;
                s.AppendLine($"{Spectrum[i].X},{Spectrum[i].Y}");
            }

            var fName = "/temp/" +  DateTime.Now.ToString("yyyy-MM-dd----hh-mm-ss-fff") + ".csv";
            System.IO.File.WriteAllText(fName,s.ToString());

            _loggingService.Info($"X: <{minX};{maxX}>   Y: <{minY};{maxY}>");
        }
        catch (Exception ex)
        {
            _loggingService.Error(ex, "Error while computing spectrum");
        } finally
        {

        }
    }

    public void AddData(byte[] data, int size)
    {
        if (_queueSize >= 2*_fftSize)
        return;

        var buff = new byte[size];
        Buffer.BlockCopy(data, 0, buff, 0, size);

        _queueSize += size;
        _spectrumQueue.Enqueue(buff);
    }

    private void PrepareBufferFromBytes(byte[] raw)
    {
        for (int i = 0; i < _fftSize; i++)
        {
            float iVal = (raw[2 * i] - 128) / 128.0f;
            float qVal = (raw[2 * i + 1] - 128) / 128.0f;

            _fftBuffer[i].Real = iVal * _window[i];
            _fftBuffer[i].Imaginary = qVal * _window[i];
        }
    }

    private void UpdateSpectrum()
    {

        // FFT
        Fourier.FFTBackward(_fftBuffer);

        // Shift DC
        FFTShift(_fftBuffer);

        // Magnitude → dB
        for (int i = 0; i < _fftSize; i++)
        {
            float re = _fftBuffer[i].Real;
            float im = _fftBuffer[i].Imaginary;

            float mag = MathF.Sqrt(re * re + im * im);
            _spectrum[i].Y = Convert.ToInt32(20.0f * MathF.Log10(mag + 1e-12f));
        }

        // Frequency axis
        for (int i = 0; i < _fftSize; i++)
        {
            _spectrum[i].X = Convert.ToInt32( ((float)i / _fftSize - 0.5f) * _sampleRate);
        }
    }

    // ================== HELPERS ==================

    private static float[] CreateHannWindow(int n)
    {
        float[] w = new float[n];

        for (int i = 0; i < n; i++)
        {
            w[i] = 0.5f *
                (1.0f - MathF.Cos(2.0f * MathF.PI * i / (n - 1)));
        }

        return w;
    }

    private static void FFTShift(FComplex[] data)
    {
        int half = data.Length / 2;

        for (int i = 0; i < half; i++)
        {
            FComplex tmp = data[i];
            data[i] = data[i + half];
            data[i + half] = tmp;
        }
    }


}
