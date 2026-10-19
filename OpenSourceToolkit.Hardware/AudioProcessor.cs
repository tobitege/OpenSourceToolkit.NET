using NAudio.Dsp;
using NAudio.Lame;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;
using System.Linq;

namespace OpenSourceToolkit.Hardware
{
    public enum AudioExportFormat
    {
        Wav,
        Mp3
    }

    public class AudioProcessor : IDisposable
    {
        private WaveIn _waveIn;
        private WaveIn _monitorWaveIn;
        private WaveOut _waveOut;
        private WaveOut _monitorWaveOut;
        private BufferedWaveProvider _monitorBuffer;
        private AudioFileReader _audioFileReader;
        private string _tempRecordingPath;

        // Processing chain
        private ISampleProvider _processingChain;
        private EqualizerSampleProvider _equalizer;
        private CompressorSampleProvider _compressor;
        private BiQuadFilterSampleProvider _highPass;
        private BiQuadFilterSampleProvider _lowPass;
        private VolumeSampleProvider _volumeProvider;

        public event Action<float[]> OnVisualizationDataAvailable;
        public event Action OnPlaybackStopped;

        public bool IsRecording { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsMonitoring { get; private set; }

        public AudioProcessor()
        {
            if (!PlatformSupport.IsAudioSupported)
                throw new PlatformNotSupportedException("Audio processing is only supported on Windows.");
        }

        public void StartRecording(int deviceNumber = 0)
        {
            StopRecording();

            _tempRecordingPath = Path.Combine(Path.GetTempPath(), $"recording_{DateTime.Now.Ticks}.wav");

            _waveIn = new WaveIn();
            _waveIn.DeviceNumber = deviceNumber;
            _waveIn.WaveFormat = new WaveFormat(44100, 16, 2);

            var writer = new WaveFileWriter(_tempRecordingPath, _waveIn.WaveFormat);

            _waveIn.DataAvailable += (s, e) =>
            {
                writer.Write(e.Buffer, 0, e.BytesRecorded);

                // Visualization for recording
                // Convert byte buffer to float samples for visualization
                var samples = new float[e.BytesRecorded / 2];
                for(int i=0; i < e.BytesRecorded/2; i++)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                    samples[i] = sample / 32768f;
                }
                OnVisualizationDataAvailable?.Invoke(samples);
            };

            _waveIn.RecordingStopped += (s, e) =>
            {
                writer.Dispose();
                IsRecording = false;
                LoadAudio(_tempRecordingPath);
            };

            _waveIn.StartRecording();
            IsRecording = true;
        }

        public void StopRecording()
        {
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
            }
        }

        public void StartMonitoring(int deviceNumber = 0)
        {
            StopMonitoring();

            _monitorWaveIn = new WaveIn();
            _monitorWaveIn.DeviceNumber = deviceNumber;
            _monitorWaveIn.WaveFormat = new WaveFormat(44100, 16, 2);

            _monitorBuffer = new BufferedWaveProvider(_monitorWaveIn.WaveFormat)
            {
                DiscardOnBufferOverflow = true
            };

            _monitorWaveIn.DataAvailable += (s, e) =>
            {
                _monitorBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);

                // Visualization for monitoring
                var samples = new float[e.BytesRecorded / 2];
                for (int i = 0; i < e.BytesRecorded / 2; i++)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                    samples[i] = sample / 32768f;
                }
                OnVisualizationDataAvailable?.Invoke(samples);
            };

            _monitorWaveOut = new WaveOut { BufferMilliseconds = 150 };
            _monitorWaveOut.Init(_monitorBuffer);
            _monitorWaveOut.Play();

            _monitorWaveIn.StartRecording();
            IsMonitoring = true;
        }

        public void StopMonitoring()
        {
            if (_monitorWaveIn != null)
            {
                _monitorWaveIn.StopRecording();
                _monitorWaveIn.Dispose();
                _monitorWaveIn = null;
            }
            if (_monitorWaveOut != null)
            {
                _monitorWaveOut.Stop();
                _monitorWaveOut.Dispose();
                _monitorWaveOut = null;
            }
            _monitorBuffer = null;
            IsMonitoring = false;
        }

        public void LoadAudio(string filePath)
        {
            StopPlayback();

            // Dispose previous reader if any
            _audioFileReader?.Dispose();

            try
            {
                _audioFileReader = new AudioFileReader(filePath);
                BuildProcessingChain();
            }
            catch (Exception ex)
            {
                // Handle error (file not found, format not supported)
                Console.WriteLine($"Error loading audio: {ex.Message}");
            }
        }

        private void BuildProcessingChain()
        {
            if (_audioFileReader == null) return;

            // Restart chain from reader
            _audioFileReader.Position = 0;

            ISampleProvider source = _audioFileReader;

            // 1. High Pass
            _highPass = new BiQuadFilterSampleProvider(source, BiQuadFilterType.HighPass, 80);
            source = _highPass;

            // 2. Low Pass
            _lowPass = new BiQuadFilterSampleProvider(source, BiQuadFilterType.LowPass, 8000);
            source = _lowPass;

            // 3. Equalizer (Bass, Mid, Treble)
            _equalizer = new EqualizerSampleProvider(source);
            source = _equalizer;

            // 4. Compressor
            _compressor = new CompressorSampleProvider(source);
            source = _compressor;

            // 5. Gain
            _volumeProvider = new VolumeSampleProvider(source);
            _processingChain = _volumeProvider;
        }

        public void UpdateSettings(
            float gainDb,
            float highPassFreq,
            float lowPassFreq,
            float bassDb, float midDb, float trebleDb,
            float compThreshold, float compRatio, float compAttack, float compRelease)
        {
            if (_processingChain == null) return;

            _volumeProvider.Volume = (float)Math.Pow(10, gainDb / 20);

            _highPass.SetFrequency(highPassFreq);
            _lowPass.SetFrequency(lowPassFreq);

            _equalizer.Update(bassDb, midDb, trebleDb);

            _compressor.Update(compThreshold, compRatio, compAttack, compRelease);
        }

        public void Play(bool original = false)
        {
            StopPlayback();

            if (_audioFileReader == null) return;

            _waveOut = new WaveOut { BufferMilliseconds = 50 };

            ISampleProvider playbackSource;

            if (original)
            {
                // For original, we create a new reader to not mess with the processing chain state
                // Or we just reset the main reader and bypass processing
                _audioFileReader.Position = 0;
                playbackSource = _audioFileReader;
            }
            else
            {
                if (_processingChain == null) BuildProcessingChain();
                _audioFileReader.Position = 0; // Reset position
                playbackSource = _processingChain;
            }

            // Add visualization support during playback
            var visualizer = new VisualizationSampleProvider(playbackSource);
            visualizer.OnDataAvailable += (data) => OnVisualizationDataAvailable?.Invoke(data);

            _waveOut.Init(visualizer);
            _waveOut.PlaybackStopped += OnWaveOutPlaybackStopped;
            _waveOut.Play();
            IsPlaying = true;
        }

        private void OnWaveOutPlaybackStopped(object sender, StoppedEventArgs e)
        {
            IsPlaying = false;
            OnPlaybackStopped?.Invoke();
        }

        public void StopPlayback()
        {
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnWaveOutPlaybackStopped;
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }
            IsPlaying = false;
        }

        public void ExportProcessed(string outputPath, AudioExportFormat format = AudioExportFormat.Wav, int mp3Bitrate = 192)
        {
            if (_audioFileReader == null) return;

            // Reset to beginning
            _audioFileReader.Position = 0;

            // Ensure chain is built
            if (_processingChain == null) BuildProcessingChain();

            switch (format)
            {
                case AudioExportFormat.Mp3:
                    ExportToMp3(outputPath, mp3Bitrate);
                    break;
                case AudioExportFormat.Wav:
                default:
                    WaveFileWriter.CreateWaveFile16(outputPath, _processingChain);
                    break;
            }

            // Reset for playback
            _audioFileReader.Position = 0;
        }

        private void ExportToMp3(string outputPath, int bitrate)
        {
            // Convert to 16-bit PCM for LAME
            var waveProvider = _processingChain.ToWaveProvider16();

            using (var mp3Writer = new LameMP3FileWriter(outputPath, waveProvider.WaveFormat, bitrate))
            {
                var buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = waveProvider.Read(buffer)) > 0)
                {
                    mp3Writer.Write(buffer, 0, bytesRead);
                }
            }
        }

        public void Dispose()
        {
            StopRecording();
            StopMonitoring();
            StopPlayback();
            _audioFileReader?.Dispose();
        }
    }

    public enum BiQuadFilterType { HighPass, LowPass }

    public class BiQuadFilterSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly BiQuadFilter[] _filters;
        private readonly int _channels;
        private BiQuadFilterType _type;
        private float _frequency;
        private float _q = 0.7f;

        public BiQuadFilterSampleProvider(ISampleProvider source, BiQuadFilterType type, float frequency)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            _filters = new BiQuadFilter[_channels];
            _type = type;
            _frequency = frequency;
            WaveFormat = source.WaveFormat;
            UpdateFilters();
        }

        public WaveFormat WaveFormat { get; }

        public int Read(Span<float> buffer)
        {
            int samplesRead = _source.Read(buffer);

            for (int i = 0; i < samplesRead; i++)
            {
                // Channel interleaving: 0, 1, 0, 1...
                int channel = i % _channels;
                if (_filters[channel] != null)
                {
                    buffer[i] = _filters[channel].Transform(buffer[i]);
                }
            }
            return samplesRead;
        }

        public void SetFrequency(float frequency)
        {
            if (Math.Abs(_frequency - frequency) > 0.1f)
            {
                _frequency = frequency;
                UpdateFilters();
            }
        }

        private void UpdateFilters()
        {
            for (int i = 0; i < _channels; i++)
            {
                if (_type == BiQuadFilterType.HighPass)
                    _filters[i] = BiQuadFilter.HighPassFilter(WaveFormat.SampleRate, _frequency, _q);
                else
                    _filters[i] = BiQuadFilter.LowPassFilter(WaveFormat.SampleRate, _frequency, _q);
            }
        }
    }

    public class EqualizerSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly BiQuadFilter[] _bassFilters;
        private readonly BiQuadFilter[] _midFilters;
        private readonly BiQuadFilter[] _trebleFilters;
        private readonly int _channels;

        public EqualizerSampleProvider(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            WaveFormat = source.WaveFormat;

            _bassFilters = new BiQuadFilter[_channels];
            _midFilters = new BiQuadFilter[_channels];
            _trebleFilters = new BiQuadFilter[_channels];

            Update(0, 0, 0);
        }

        public WaveFormat WaveFormat { get; }

        public void Update(float bassDb, float midDb, float trebleDb)
        {
            for (int i = 0; i < _channels; i++)
            {
                _bassFilters[i] = BiQuadFilter.LowShelf(WaveFormat.SampleRate, 320, 0.7f, bassDb);
                _midFilters[i] = BiQuadFilter.PeakingEQ(WaveFormat.SampleRate, 1000, 0.7f, midDb);
                _trebleFilters[i] = BiQuadFilter.HighShelf(WaveFormat.SampleRate, 3200, 0.7f, trebleDb);
            }
        }

        public int Read(Span<float> buffer)
        {
            int samplesRead = _source.Read(buffer);
            for (int i = 0; i < samplesRead; i++)
            {
                int channel = i % _channels;
                float sample = buffer[i];
                sample = _bassFilters[channel].Transform(sample);
                sample = _midFilters[channel].Transform(sample);
                sample = _trebleFilters[channel].Transform(sample);
                buffer[i] = sample;
            }
            return samplesRead;
        }
    }

    public class CompressorSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        public WaveFormat WaveFormat => _source.WaveFormat;

        private float _thresholdDb = -24;
        private float _ratio = 4;
        private float _attackMs = 3;
        private float _releaseMs = 250;

        // Runtime state
        private float _envelope = 0;

        public CompressorSampleProvider(ISampleProvider source)
        {
            _source = source;
        }

        public void Update(float threshold, float ratio, float attack, float release)
        {
            _thresholdDb = threshold;
            _ratio = ratio;
            _attackMs = attack;
            _releaseMs = release;
        }

        public int Read(Span<float> buffer)
        {
            int samplesRead = _source.Read(buffer);
            float sampleRate = WaveFormat.SampleRate;

            float attackCoeff = (float)Math.Exp(-1.0 / (0.001 * _attackMs * sampleRate));
            float releaseCoeff = (float)Math.Exp(-1.0 / (0.001 * _releaseMs * sampleRate));
            float thresholdLinear = (float)Math.Pow(10, _thresholdDb / 20.0);

            for (int i = 0; i < samplesRead; i++)
            {
                float sample = buffer[i];
                float absSample = Math.Abs(sample);

                // Envelope follower
                if (absSample > _envelope)
                    _envelope = attackCoeff * _envelope + (1 - attackCoeff) * absSample;
                else
                    _envelope = releaseCoeff * _envelope + (1 - releaseCoeff) * absSample;

                // Gain calculation
                float gain = 1.0f;
                if (_envelope > thresholdLinear)
                {
                    float envDb = 20 * (float)Math.Log10(_envelope + 1e-6);
                    float gainReductionDb = (_thresholdDb - envDb) * (1 - 1 / _ratio);
                    gain = (float)Math.Pow(10, gainReductionDb / 20.0);
                }

                buffer[i] = sample * gain;
            }

            return samplesRead;
        }
    }

    public class VisualizationSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        public event Action<float[]> OnDataAvailable;

        public VisualizationSampleProvider(ISampleProvider source)
        {
            _source = source;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(Span<float> buffer)
        {
            int read = _source.Read(buffer);
            if (read > 0)
            {
                // Make a copy for visualization to avoid thread issues or modification
                // Also clamp values to valid range to prevent meter glitches
                float[] vizData = new float[read];
                for (int i = 0; i < read; i++)
                {
                    float val = buffer[i];
                    // Sanity check and clamp
                    if (float.IsNaN(val) || float.IsInfinity(val)) val = 0;
                    if (val > 1.0f) val = 1.0f;
                    else if (val < -1.0f) val = -1.0f;
                    vizData[i] = val;
                }
                OnDataAvailable?.Invoke(vizData);
            }
            return read;
        }
    }
}
