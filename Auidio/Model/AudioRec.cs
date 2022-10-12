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
using System.Threading.Channels;

namespace Auidio.Model
{
    public class AudioRec
    {
        private WaveIn recorder;
        MainWindow mw { get; set; }
        Dictionary<int, Tuple<double[], double[]>> SearchedSamples = new Dictionary<int, Tuple<double[], double[]>>();
        public void AttachControlToModel(MainWindow mainW)
        {
            mw = mainW;
        }
        private Dictionary<string, MMDevice> GetInputAudioDevices(ref List<int> channels)
        {
            channels = new List<int>();
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
                            channels.Add(deviceInfo.Channels);
                            retVal.Add(device.FriendlyName, device);
                            break;
                        }
                    }
                }
            }
            return retVal;
        }
        public bool StartRecording()
        {
            List<int> channels = null;
            var devices = GetInputAudioDevices(ref channels);
            if (channels == null) return false;
            recorder = new WaveIn();
            recorder.BufferMilliseconds = 22;
            recorder.DeviceNumber = 0;
            recorder.WaveFormat = new WaveFormat(48000, 16, channels[0]);
            recorder.DataAvailable += this.RecorderOnDataAvailable;
            recorder.StartRecording();
            return true;
        }
        private void RecorderOnDataAvailable(object sender, WaveInEventArgs waveInEventArgs)
        {
            double[] points = new double[waveInEventArgs.Buffer.Length / 2];
            double[] points2 = null;
            if (recorder.WaveFormat.Channels > 1) 
                points2 = new double[waveInEventArgs.Buffer.Length / 2];
            int i = 0;
            mw.PlotControl.Plot.Clear();
            mw.PlotControl2.Plot.Clear();
            for (int index = 0; index < waveInEventArgs.Buffer.Length; index += 2* recorder.WaveFormat.Channels)
            {
                short sample = (short)((waveInEventArgs.Buffer[index + 1] << 8) |
                                        waveInEventArgs.Buffer[index + 0]);
                float sample32 = sample / 32768f;
                points[i] = sample32;

                i++;
            }
            if (recorder.WaveFormat.Channels > 1)// needs revision
            {
                for (int index = 2; index < waveInEventArgs.Buffer.Length; index += 2 * recorder.WaveFormat.Channels)
                {
                    short sample = (short)((waveInEventArgs.Buffer[index + 1] << 8) |
                                            waveInEventArgs.Buffer[index + 0]);
                    float sample32 = sample / 32768f;
                    points2[i] = sample32;
                }
            }
            System.Numerics.Complex[] fftComplex = new System.Numerics.Complex[1024];
            System.Numerics.Complex[] fftComplex2 = null;
            if (recorder.WaveFormat.Channels > 1)
                fftComplex2 = new System.Numerics.Complex[1024];
            double[] fft1 = new double[1024]; // this is where we will store the output (fft)
            double[] fft2 = null;
            if (recorder.WaveFormat.Channels > 1)
                fft2 = new double[1024];
            for (i = 0; i < 1024; i++)
            {
                fftComplex[i] = new System.Numerics.Complex(points[i], 0.0); // make it complex format (imaginary = 0)
            }
            if (recorder.WaveFormat.Channels > 1)
            {
                fft2 = new double[1024];
                for (i = 0; i < 1024; i++)
                {
                    fftComplex2[i] = new System.Numerics.Complex(points2[i], 0.0); // make it complex format (imaginary = 0)
                }
            }
            FourierTransform.FFT(fftComplex, FourierTransform.Direction.Forward);
            double maxF = 0;
            double maxF2 = 0;
            for (i = 0; i < 1024; i++)
            {
                fft1[i] = fftComplex[i].Magnitude*100;
                maxF = Math.Max(fft1[i], maxF);
            }
            if (recorder.WaveFormat.Channels > 1)
                for (i = 0; i < 1024; i++)
                {
                    fft2[i] = fftComplex[i].Magnitude * 100;
                    maxF2 = Math.Max(fft2[i], maxF2);
                }

            maxF = 1 / maxF;
            for (i = 0; i < 1024; i++)
            {
                fft1[i] *= maxF;
            }
            mw.PlotControl.Plot.AddSignal(points, 20, Color.Blue);
            mw.PlotControl.Plot.SetAxisLimits(0, 20, -1, 1);
            mw.PlotControl.Plot.XAxis.Label("ms");
            mw.PlotControl.Refresh();
            mw.PlotControl2.Plot.AddSignal(fft1, 20, Color.Blue);
            mw.PlotControl2.Plot.SetAxisLimits(0, 20, -1, 1);
            mw.PlotControl2.Plot.XAxis.Label("Hz");
            mw.PlotControl2.Refresh();
            WorkCompareWaves(fft1,fft2);
        }
        private void WorkCompareWaves(double[] left, double[] right)
        {
            List<int> ActivatedWaveforms = new List<int>();
            double diviation = -1;
            foreach ( var item in SearchedSamples)
            {
                //maybe need to scale up ethalon scale sample from 1024 values
                for(int i=0; i< left.Length; i++)
                {
                    diviation += Math.Abs(item.Value.Item1[i] - left[i]);//Absolute diviation
                    if(right!=null)
                        diviation += Math.Abs(item.Value.Item2[i] - right[i]);
                }
            }
        }
        public bool AddFile(string filepath, int ind)
        {
            if (!File.Exists(filepath))
            {
                return false;
            }
            using (var audioFileReader = new AudioFileReader(filepath))
            {
                var outFormat = new WaveFormat(48000, 16, audioFileReader.WaveFormat.Channels);
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
                    double[] pointsd = new double[samplesToDelay / 2];
                    provider.Read(points, 0, samplesToDelay);
                    int i = 0;
                    double maxS = 0;
                    for (int j = 0; j < samplesToDelay; j += 2, i++)
                    {
                        pointsd[i] = points[j];
                        maxS = Math.Max(points[j], maxS);
                    }
                    mw.PlotControl3.Plot.Clear();
                    mw.PlotControl4.Plot.Clear();
                    mw.PlotControl3.Plot.AddSignal(pointsd, 48, Color.Blue);
                    mw.PlotControl3.Plot.SetAxisLimits(0, delay.TotalSeconds * 1000, -1, 1);
                    mw.PlotControl3.Plot.XAxis.Label("ms");
                    mw.PlotControl3.Refresh();
                    int powTwo = 2;
                    i = 1;
                    while (powTwo < samplesToDelay / channels)
                    {
                        powTwo *= 2;
                        i++;
                        if (powTwo == Math.Pow(2, 9))
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
                    double maxF = 0;
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
                            FFT1[i] = FFT1[i] + pointsFFT1[i].Magnitude * 100;
                            maxF = Math.Max(FFT1[i], maxF);
                            if (channels > 1)
                            {
                                FFT2[i] += FFT2[i] + pointsFFT2[i].Magnitude * 100;
                            }
                        }
                    }
                    maxS = 1 / maxS;
                    maxF = 1 / maxF;
                    for (i = 0; i < powTwo; i++)
                    {
                        FFT1[i] *= maxF;
                        if (FFT2 != null)
                            FFT2[i] *= maxF;
                    }
                    mw.PlotControl4.Plot.AddSignal(FFT1, 13, Color.Blue);
                    mw.PlotControl4.Plot.SetAxisLimits(0, 20, -1, 1);
                    mw.PlotControl4.Plot.XAxis.Label("Hz");
                    mw.PlotControl4.Refresh();
                    SearchedSamples.Add(ind, new Tuple<double[], double[]>(FFT1, FFT2));
                }
            }
            return true;
        }
    }

}
