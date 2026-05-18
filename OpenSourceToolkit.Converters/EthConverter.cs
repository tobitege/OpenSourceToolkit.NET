using System;
using System.Numerics;

namespace OpenSourceToolkit.Converters
{
    /// <summary>
    /// Provides conversion helpers for Ethereum denominations.
    /// </summary>
    public static class EthConverter
    {
        // 1 ETH = 10^18 Wei
        // 1 Gwei = 10^9 Wei

        /// <summary>
        /// Converts Ether to Wei.
        /// </summary>
        /// <param name="eth">The Ether amount.</param>
        /// <returns>The equivalent amount in Wei.</returns>
        public static decimal ToWei(decimal eth)
        {
            return eth * 1_000_000_000_000_000_000m;
        }

        /// <summary>
        /// Converts Ether to Gwei.
        /// </summary>
        /// <param name="eth">The Ether amount.</param>
        /// <returns>The equivalent amount in Gwei.</returns>
        public static decimal ToGwei(decimal eth)
        {
            return eth * 1_000_000_000m;
        }

        /// <summary>
        /// Converts Wei to Ether.
        /// </summary>
        /// <param name="wei">The Wei amount.</param>
        /// <returns>The equivalent amount in Ether.</returns>
        public static decimal FromWei(decimal wei)
        {
            return wei / 1_000_000_000_000_000_000m;
        }

        /// <summary>
        /// Converts Gwei to Ether.
        /// </summary>
        /// <param name="gwei">The Gwei amount.</param>
        /// <returns>The equivalent amount in Ether.</returns>
        public static decimal FromGwei(decimal gwei)
        {
            return gwei / 1_000_000_000m;
        }
    }
}
