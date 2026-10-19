using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSourceToolkit.Hardware
{
    public class SpeakerTester : IDisposable
    {
        private IWavePlayer _waveOut;
        private SignalGenerator _signalGenerator;

        public SpeakerTester()
        {
            if (!PlatformSupport.IsAudioSupported)
                throw new PlatformNotSupportedException("Speaker testing is only supported on Windows.");
        }

        public void PlayTone(float frequency, float durationSeconds, float volume = 0.5f, int deviceNumber = -1)
        {
             PlayToneAsync(frequency, durationSeconds, volume, deviceNumber).Wait();
        }

        public async Task PlayToneAsync(float frequency, float durationSeconds, float volume = 0.5f, int deviceNumber = -1)
        {
            Stop(); // Stop any current sound

            _signalGenerator = new SignalGenerator()
            {
                Gain = volume,
                Frequency = frequency,
                Type = SignalGeneratorType.Sin
            };

            _waveOut = new WaveOut { DeviceNumber = deviceNumber, BufferMilliseconds = 150 };

            var tcs = new TaskCompletionSource<bool>();
            EventHandler<StoppedEventArgs> handler = null;
            handler = (s, e) =>
            {
                tcs.TrySetResult(true);
            };

            _waveOut.PlaybackStopped += handler;

            try
            {
                _waveOut.Init(_signalGenerator.Take(TimeSpan.FromSeconds(durationSeconds)));
                _waveOut.Play();
                await tcs.Task;
            }
            finally
            {
                 if (_waveOut != null)
                 {
                     _waveOut.PlaybackStopped -= handler;
                 }
            }
        }

        public void PlaySweep(float startFreq, float endFreq, float durationSeconds, float volume = 0.5f, int deviceNumber = -1)
        {
             Stop();

            _signalGenerator = new SignalGenerator()
            {
                Gain = volume,
                Frequency = startFreq,
                Type = SignalGeneratorType.Sin
            };

            // Note: NAudio SignalGenerator doesn't do frequency sweeps out of the box easily without custom provider.
            // For simplicity in this port, we just play a fixed tone or would need a custom ISampleProvider.
            // Just playing the start frequency for now as a placeholder for sweep logic.

            _waveOut = new WaveOut { DeviceNumber = deviceNumber, BufferMilliseconds = 150 };
            _waveOut.Init(_signalGenerator.Take(TimeSpan.FromSeconds(durationSeconds)));
            _waveOut.Play();
        }

        public void Stop()
        {
            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
