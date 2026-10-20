using System;
using UnityEngine;

namespace GridCut.Game
{
    /// <summary>
    /// Ad surface. In the shipped WebGL build the portal integration layer
    /// (#if GRIDCUT_CRAZYGAMES / GRIDCUT_COOLMATH / GRIDCUT_ARMORGAMES) supplies a
    /// real implementation; otherwise a no-op is used.
    /// </summary>
    public interface IAdService
    {
        /// <summary>True when an interstitial may be shown now (portal + rate limiting).</summary>
        bool InterstitialReady { get; }

        /// <summary>Attempts to show an interstitial; done(false) if none available.</summary>
        void ShowInterstitial(Action<bool> done);

        /// <summary>Called by portals that allow gameplay pause (audio must mute during ads).</summary>
        event Action<bool> AdStateChange;
    }

    public sealed class NullAdService : IAdService
    {
        public static readonly NullAdService Instance = new NullAdService();
        public bool InterstitialReady => false;
        public void ShowInterstitial(Action<bool> done) => done?.Invoke(false);
        public event Action<bool> AdStateChange;
    }
}
