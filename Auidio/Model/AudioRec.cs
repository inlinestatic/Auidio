using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Collections.Generic;
using System.Drawing;

using System.Windows.Threading;
using Accord.Math;
using System.Linq;
using System.IO;
using NAudio.Wave.SampleProviders;
using System;
using System.Reflection;

namespace Auidio.Model
{
    public class AudioRec
    {
        private WaveIn recorder;
        MainWindow mw { get; set; }
        Dictionary<int, List<double>> SearchedSamples = new Dictionary<int, List<double>>();
        public void AttachControlToModel(MainWindow mainW)
        {
            mw = mainW;
        }
        private Dictionary<string, MMDevice> GetInputAudioDevices()
        {
            Dictionary<string, MMDevice> retVal = new Dictionary<string, MMDevice>();
            using (MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
            {
                int waveInDevices = WaveIn.DeviceCount;
                for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
                {
                    WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice);
                    foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.All))
                    {
                        if (device.FriendlyName.StartsWith(deviceInfo.ProductName))
                        {
                            retVal.Add(device.FriendlyName, device);
                            break;
                        }
                    }
                }
            }
            return retVal;
        }
        public void StartRecording()
        { 
            var devices = GetInputAudioDevices();
            recorder = new WaveIn();
            recorder.BufferMilliseconds = 22;
            recorder.DeviceNumber = 0;
            recorder.WaveFormat = new WaveFormat(48000, 16, 1);
            recorder.DataAvailable += this.RecorderOnDataAvailable;
            recorder.StartRecording();
        }
        private void RecorderOnDataAvailable(object sender, WaveInEventArgs waveInEventArgs)
        {
            double[] points = new double[waveInEventArgs.Buffer.Length / 2];
            int i = 0;
            mw.PlotControl.Plot.Clear();
            mw.PlotControl2.Plot.Clear();
            for (int index = 0; index < waveInEventArgs.Buffer.Length; index += 2)
            {
                short sample = (short)((waveInEventArgs.Buffer[index + 1] << 8) |
                                        waveInEventArgs.Buffer[index + 0]);
                float sample32 = sample / 32768f;
                points[i] = sample32;
                i++;
            }
            System.Numerics.Complex[] fftComplex = new System.Numerics.Complex[1024];
            double[] fft = new double[512]; // this is where we will store the output (fft)
            for (i = 0; i < 1024; i++)
            {
                fftComplex[i] = new System.Numerics.Complex(points[i], 0.0); // make it complex format (imaginary = 0)
            }
            FourierTransform.FFT(fftComplex, FourierTransform.Direction.Forward);
            for (i = 0; i < 512; i++)
            {
                fft[i] = fftComplex[i].Magnitude*4;
            }
            mw.PlotControl.Plot.AddSignal(points, 20, Color.Blue);
            mw.PlotControl.Plot.SetAxisLimits(0, 20, -1, 1);
            mw.PlotControl.Plot.XAxis.Label("ms");
            mw.PlotControl.Refresh();
            mw.PlotControl2.Plot.AddSignal(fft, 20, Color.Blue);
            mw.PlotControl2.Plot.SetAxisLimits(0, 20, -1, 1);
            mw.PlotControl2.Plot.XAxis.Label("Hz");
            mw.PlotControl2.Refresh();
        }
        public bool AddFile(string filepath, int ind)
        {
            if (!File.Exists(filepath))
            {
                return false;
            }
            using (var audioFileReader = new AudioFileReader(filepath))
            {
                var outFormat = new WaveFormat(48000,16, audioFileReader.WaveFormat.Channels);
                using (var resampler = new MediaFoundationResampler(audioFileReader, outFormat))
                {
                    List<System.Numerics.Complex[]> listOfFFT = new List<System.Numerics.Complex[]>();
                    var provider = resampler.ToSampleProvider();
                    var waveFormat = provider.WaveFormat;
                    int sampleRate = waveFormat.SampleRate;
                    int channels = waveFormat.Channels;
                    TimeSpan delay = audioFileReader.TotalTime;
                    int samplesToDelay = (int)(sampleRate * delay.TotalSeconds) * channels;
                    float[] points = new float[samplesToDelay];
                    double[] pointsd = new double[samplesToDelay/2];
                    provider.Read(points, 0, samplesToDelay);
                    int i = 0;
                    for (int j = 0; j < samplesToDelay; j+=2, i++)
                    {
                        pointsd[i] = points[j];
                    }
                    mw.PlotControl3.Plot.Clear();
                    mw.PlotControl4.Plot.Clear();
                    mw.PlotControl3.Plot.AddSignal(pointsd, 48, Color.Blue);
                    mw.PlotControl3.Plot.SetAxisLimits(0, delay.TotalSeconds*1000, -1, 1);
                    mw.PlotControl3.Plot.XAxis.Label("ms");
                    mw.PlotControl3.Refresh();
                    int powTwo = 2;
                    i = 1;
                    while (powTwo < samplesToDelay / channels)
                    {
                        powTwo *= 2;
                        i++;
                        if (powTwo == Math.Pow(2, 14))
                            break;
                    }
                    int samplesInRecord = (samplesToDelay / channels) / powTwo;
                    System.Numerics.Complex[] pointsFFT1 = new System.Numerics.Complex[powTwo];
                    System.Numerics.Complex[] pointsFFT2 = null;
                    if (channels > 1)
                        pointsFFT2 = new System.Numerics.Complex[powTwo];
                    double[] FFT1 = new double[powTwo];
                    double[] FFT2 = null;
                    if (channels > 1)
                        FFT2 = new double[powTwo];
                    for (int sampleIndex = 0; sampleIndex < samplesInRecord; sampleIndex++)
                    {
                        i = 0;
                        int begin = sampleIndex * powTwo;
                        int end = (1 + sampleIndex) * powTwo;
                        for (int index = begin; index < end; index += channels)
                        {
                            pointsFFT1[i] = points[index];
                            if (channels > 1)
                                pointsFFT2[i] = points[index + 1];
                            i++;
                        }
                        FourierTransform.FFT(pointsFFT1, FourierTransform.Direction.Forward);
                        if (channels > 1)
                        {
                            FourierTransform.FFT(pointsFFT2, FourierTransform.Direction.Forward);
                        }
                        for (i = 0; i < powTwo; i++)
                        {
                            FFT1[i] = FFT1[i] + pointsFFT1[i].Magnitude*100;
                            if (channels > 1)
                            {
                                FFT2[i] += FFT2[i] + pointsFFT2[i].Magnitude * 100;
                            }
                        }
                    }
                    //for (i = 0; i < powTwo; i++)
                    //{
                    //    FFT1[i] /= samplesInRecord;
                    //    if (FFT2 != null)
                    //        FFT2[i] /= samplesInRecord;
                    //}
                    mw.PlotControl4.Plot.AddSignal(FFT1, 410, Color.Blue);
                    mw.PlotControl4.Plot.SetAxisLimits(0, 20, -1, 1);
                    mw.PlotControl4.Plot.XAxis.Label("Hz");
                    mw.PlotControl4.Refresh();
                }
            }
            return true;
        }
    }

}
