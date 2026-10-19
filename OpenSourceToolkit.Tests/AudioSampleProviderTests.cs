using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.Wave;
using OpenSourceToolkit.Hardware;
using System;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class AudioSampleProviderTests
    {
        [TestMethod]
        [DataRow("HighPass")]
        [DataRow("Equalizer")]
        [DataRow("Compressor")]
        [DataRow("Visualization")]
        public void ReadSpan_PreservesSliceBoundsAndUnfilledSamples(string kind)
        {
            var source = new ArraySampleProvider(new[] { 0.5f, -0.25f });
            ISampleProvider provider = kind switch
            {
                "HighPass" => new BiQuadFilterSampleProvider(source, BiQuadFilterType.HighPass, 80),
                "Equalizer" => new EqualizerSampleProvider(source),
                "Compressor" => new CompressorSampleProvider(source),
                "Visualization" => new VisualizationSampleProvider(source),
                _ => throw new ArgumentException(nameof(kind))
            };
            var buffer = new[] { 42f, 42f, 42f, 42f, 42f, 42f };

            Assert.AreEqual(0, provider.Read(Span<float>.Empty));
            Assert.AreEqual(2, provider.Read(buffer.AsSpan(1, 4)));
            Assert.IsTrue(float.IsFinite(buffer[1]));
            Assert.IsTrue(float.IsFinite(buffer[2]));
            Assert.AreEqual(42f, buffer[0]);
            Assert.AreEqual(42f, buffer[3]);
            Assert.AreEqual(42f, buffer[4]);
            Assert.AreEqual(42f, buffer[5]);
            Assert.AreEqual(0, provider.Read(buffer.AsSpan(1, 4)));
        }

        [TestMethod]
        public void Equalizer_ZeroGain_PreservesSamples()
        {
            var samples = new[] { 0.5f, -0.25f, 0.125f, -0.75f };
            var provider = new EqualizerSampleProvider(new ArraySampleProvider(samples));
            var buffer = new float[samples.Length];

            Assert.AreEqual(samples.Length, provider.Read(buffer));

            for (int i = 0; i < samples.Length; i++)
                Assert.AreEqual(samples[i], buffer[i], 0.00001f);
        }

        [TestMethod]
        public void Compressor_ReadSpan_AttenuatesSignalAboveThreshold()
        {
            var samples = new float[1000];
            Array.Fill(samples, 1f);
            var provider = new CompressorSampleProvider(new ArraySampleProvider(samples));
            provider.Update(-20, 4, 0.1f, 250);
            var buffer = new float[samples.Length];

            Assert.AreEqual(samples.Length, provider.Read(buffer));
            Assert.IsTrue(buffer[^1] > 0.1f && buffer[^1] < 0.3f);
        }

        [TestMethod]
        public void HighPass_ReadSpan_KeepsStereoChannelsIndependent()
        {
            var source = new ArraySampleProvider(new[] { 1f, 0f, 0f, 0f, 0f, 0f }, 2);
            var provider = new BiQuadFilterSampleProvider(source, BiQuadFilterType.HighPass, 80);
            var buffer = new float[6];

            Assert.AreEqual(6, provider.Read(buffer));
            Assert.IsTrue(buffer[0] > 0);
            Assert.AreEqual(0f, buffer[1]);
            Assert.AreEqual(0f, buffer[3]);
            Assert.AreEqual(0f, buffer[5]);
        }

        [TestMethod]
        public void Visualization_ReadSpan_CopiesAndSanitizesOnlyReadSamples()
        {
            var samples = new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1.5f, -1.5f, 0.25f };
            var provider = new VisualizationSampleProvider(new ArraySampleProvider(samples));
            float[] visualization = null;
            int events = 0;
            provider.OnDataAvailable += data => { visualization = data; events++; };
            var buffer = new float[10];

            Assert.AreEqual(samples.Length, provider.Read(buffer.AsSpan(1, 8)));
            CollectionAssert.AreEqual(new[] { 0f, 0f, 0f, 1f, -1f, 0.25f }, visualization);
            Assert.IsTrue(float.IsNaN(buffer[1]));
            Assert.AreEqual(1.5f, buffer[4]);
            Array.Fill(buffer, 42f);
            Assert.AreEqual(0.25f, visualization[^1]);
            Assert.AreEqual(0, provider.Read(buffer));
            Assert.AreEqual(1, events);
        }

        private sealed class ArraySampleProvider : ISampleProvider
        {
            private readonly float[] _samples;
            private int _position;

            public ArraySampleProvider(float[] samples, int channels = 1)
            {
                _samples = samples;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, channels);
            }

            public WaveFormat WaveFormat { get; }

            public int Read(Span<float> buffer)
            {
                int count = Math.Min(buffer.Length, _samples.Length - _position);
                _samples.AsSpan(_position, count).CopyTo(buffer);
                _position += count;
                return count;
            }
        }
    }
}
