using System.Collections.Generic;
using UnityEngine;

namespace FangtasticPalette
{
    /// <summary>
    /// The original renderer state captured before the recolour overrides it, so clearing a setting
    /// back to blank restores the real original instead of the last override. Keyed by the live
    /// <see cref="Material"/> / <see cref="ParticleSystem"/> instance - a fresh one exists after every
    /// body swap (EntityCustomization.SetBodyView re-instantiates the prefab).
    ///
    /// Because the keys are live Unity objects, the dictionaries otherwise pin every Material and
    /// ParticleSystem the plugin has ever touched for the whole session (they can't be GC'd while a
    /// key references them). <see cref="PruneDead"/> drops entries whose object has been destroyed;
    /// <see cref="Purge"/> clears everything on plugin teardown.
    ///
    /// The capture/restore call sites in <see cref="BatColorPatch"/> still read/write these maps
    /// directly; folding the repeated capture-once idiom into a typed helper is the deferred P2
    /// (OriginalValueCache) - out of scope for this lifecycle change.
    /// </summary>
    internal static class RendererOverrideStore
    {
        internal static readonly Dictionary<(Material Material, string Property), Texture> OriginalTextures =
            new Dictionary<(Material, string), Texture>();
        internal static readonly Dictionary<(Material Material, string Property), Color> OriginalTints =
            new Dictionary<(Material, string), Color>();
        internal static readonly Dictionary<(Material Material, string Property), Color> OriginalWingDust =
            new Dictionary<(Material, string), Color>();

        // A particle system commonly drives its colour through the ParticleSystem module's startColor,
        // which is multiplied on top of the material colour - so tinting the material alone can look
        // like nothing changed. Captured for restore, keyed by the live system.
        internal static readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> OriginalWingDustStart =
            new Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient>();

        /// <summary>Clear every captured original (plugin teardown).</summary>
        internal static void Purge()
        {
            OriginalTextures.Clear();
            OriginalTints.Clear();
            OriginalWingDust.Clear();
            OriginalWingDustStart.Clear();
        }

        /// <summary>
        /// Drop entries whose keyed Unity object has been destroyed (Unity "fake null"), so the maps
        /// don't pin dead Materials / ParticleSystems from earlier body swaps for the whole session.
        /// </summary>
        internal static void PruneDead()
        {
            PruneMaterialKeyed(OriginalTextures);
            PruneMaterialKeyed(OriginalTints);
            PruneMaterialKeyed(OriginalWingDust);

            List<ParticleSystem> deadSystems = null;
            foreach (var kv in OriginalWingDustStart)
            {
                if (kv.Key == null)
                {
                    (deadSystems ?? (deadSystems = new List<ParticleSystem>())).Add(kv.Key);
                }
            }

            if (deadSystems != null)
            {
                foreach (var system in deadSystems)
                {
                    OriginalWingDustStart.Remove(system);
                }
            }
        }

        private static void PruneMaterialKeyed<TValue>(
            Dictionary<(Material Material, string Property), TValue> dict)
        {
            List<(Material, string)> dead = null;
            foreach (var kv in dict)
            {
                if (kv.Key.Material == null)
                {
                    (dead ?? (dead = new List<(Material, string)>())).Add(kv.Key);
                }
            }

            if (dead != null)
            {
                foreach (var key in dead)
                {
                    dict.Remove(key);
                }
            }
        }
    }
}
