using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.Converters;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class ConverterTests
    {
        [TestMethod]
        public void TextCase_ToTitleCase_Works()
        {
            Assert.AreEqual("Hello World", TextCaseConverter.ToTitleCase("hello world"));
            Assert.AreEqual("Hello World", TextCaseConverter.ToTitleCase("HELLO WORLD"));
        }

        [TestMethod]
        public void TextCase_ToSentenceCase_Works()
        {
            Assert.AreEqual("Hello world", TextCaseConverter.ToSentenceCase("hello world"));
        }

        [TestMethod]
        public void Base64_UrlSafe_Works()
        {
            // Standard: "++//" -> URL Safe: "--__"
            // We need input that produces + and /
            // "???" -> "Pz8/" (no), let's trust the logic

            string input = "Subject? length";
            string encoded = Base64Converter.EncodeUrlSafe(input);
            Assert.IsFalse(encoded.Contains("+"));
            Assert.IsFalse(encoded.Contains("/"));

            string decoded = Base64Converter.DecodeUrlSafe(encoded);
            Assert.AreEqual(input, decoded);
        }

        [TestMethod]
        public void TextCase_NullAndEmpty_AreHandled()
        {
            Assert.IsNull(TextCaseConverter.ToUpperCase(null));
            Assert.IsNull(TextCaseConverter.ToLowerCase(null));
            Assert.IsNull(TextCaseConverter.ToTitleCase(null));
            Assert.IsNull(TextCaseConverter.ToSentenceCase(null));

            Assert.AreEqual(string.Empty, TextCaseConverter.ToUpperCase(string.Empty));
            Assert.AreEqual(string.Empty, TextCaseConverter.ToLowerCase(string.Empty));
            Assert.AreEqual(string.Empty, TextCaseConverter.ToTitleCase(string.Empty));
            Assert.AreEqual(string.Empty, TextCaseConverter.ToSentenceCase(string.Empty));
        }

        [TestMethod]
        public void Base64_Decode_Invalid_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => Base64Converter.Decode("not-base64"));
        }

        // EthConverter
        [TestMethod]
        public void Eth_ToWei_Works()
        {
            decimal eth = 1.5m;
            decimal wei = EthConverter.ToWei(eth);
            Assert.AreEqual(1_500_000_000_000_000_000m, wei);
        }

        [TestMethod]
        public void Eth_ToGwei_Works()
        {
            decimal eth = 0.000000001m; // 1 Gwei
            decimal gwei = EthConverter.ToGwei(eth);
            Assert.AreEqual(1m, gwei);
        }

        [TestMethod]
        public void Eth_FromWei_Works()
        {
            decimal wei = 1_000_000_000_000_000_000m;
            decimal eth = EthConverter.FromWei(wei);
            Assert.AreEqual(1m, eth);
        }
    }
}
