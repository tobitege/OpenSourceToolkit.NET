using System;
using System.Text;

namespace OpenSourceToolkit.Converters
{
    /// <summary>
    /// Provides helpers for Base64 and URL-safe Base64 text conversion.
    /// </summary>
    public static class Base64Converter
    {
        /// <summary>
        /// Encodes text as a Base64 string.
        /// </summary>
        /// <param name="text">The text to encode.</param>
        /// <param name="encoding">The text encoding to use, or <c>null</c> to use UTF-8.</param>
        /// <returns>The Base64-encoded text, or <c>null</c> when <paramref name="text"/> is <c>null</c>.</returns>
        public static string Encode(string text, Encoding encoding = null)
        {
            if (text == null) return null;
            var bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Decodes a Base64 string to text.
        /// </summary>
        /// <param name="base64">The Base64 string to decode.</param>
        /// <param name="encoding">The text encoding to use, or <c>null</c> to use UTF-8.</param>
        /// <returns>The decoded text, or <c>null</c> when <paramref name="base64"/> is <c>null</c>.</returns>
        public static string Decode(string base64, Encoding encoding = null)
        {
            if (base64 == null) return null;
            var bytes = Convert.FromBase64String(base64);
            return (encoding ?? Encoding.UTF8).GetString(bytes);
        }

        /// <summary>
        /// Encodes text as a URL-safe Base64 string without padding.
        /// </summary>
        /// <param name="text">The text to encode.</param>
        /// <param name="encoding">The text encoding to use, or <c>null</c> to use UTF-8.</param>
        /// <returns>The URL-safe Base64-encoded text.</returns>
        public static string EncodeUrlSafe(string text, Encoding encoding = null)
        {
            var base64 = Encode(text, encoding);
            return base64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        /// <summary>
        /// Decodes a URL-safe Base64 string to text.
        /// </summary>
        /// <param name="base64Url">The URL-safe Base64 string to decode.</param>
        /// <param name="encoding">The text encoding to use, or <c>null</c> to use UTF-8.</param>
        /// <returns>The decoded text.</returns>
        public static string DecodeUrlSafe(string base64Url, Encoding encoding = null)
        {
            var base64 = base64Url.Replace("-", "+").Replace("_", "/");
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Decode(base64, encoding);
        }
    }
}
