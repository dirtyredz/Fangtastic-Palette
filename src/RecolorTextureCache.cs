using System.Collections.Generic;
using UnityEngine;

namespace FangtasticPalette
{
    /// <summary>
    /// Bounded LRU cache of the regenerated recolour textures, owned centrally so the plugin can
    /// give them a lifecycle. Each distinct palette produces a new <see cref="Texture2D"/> (~1 MB at
    /// 512×512 RGBA); without a bound, fiddling colours in the wardrobe would spawn one per change
    /// and never free them (a slow leak). This caps the live set and <c>Object.Destroy</c>s whatever
    /// falls off the least-recently-used end.
    ///
    /// Evicting the LRU entry is safe against destroying an in-use texture: the per-frame
    /// <see cref="BatColorReapplier"/> re-fetches the active palettes every Update, so their keys are
    /// continually touched (<see cref="TryGet"/> moves a hit to the front) and stay at the
    /// most-recently-used end - only genuinely stale palettes (previous, no-longer-applied colour
    /// choices) age out to the tail.
    ///
    /// The <see cref="TryGet"/> / <see cref="Add"/> split (rather than a Func-based GetOrAdd) keeps
    /// the reapplier's every-frame hit path allocation-free - no closure is captured on a cache hit.
    /// <see cref="Purge"/> frees everything on plugin teardown (see <c>Plugin.OnDestroy</c>).
    /// </summary>
    internal static class RecolorTextureCache
    {
        // Headroom over the realistic active set (live body + eyes across two albedo slots, plus the
        // wardrobe preview's equivalents) so the reapplier's continually-touched entries never reach
        // the eviction tail.
        private const int Capacity = 32;

        private sealed class Entry
        {
            internal string Key;
            internal Texture2D Texture;
        }

        // First node = most recently used, last = least. Map gives O(1) lookup to each node.
        private static readonly LinkedList<Entry> Lru = new LinkedList<Entry>();
        private static readonly Dictionary<string, LinkedListNode<Entry>> Map =
            new Dictionary<string, LinkedListNode<Entry>>();

        /// <summary>
        /// Return the cached texture for <paramref name="key"/> and mark it most-recently-used. A
        /// cache entry whose texture was destroyed elsewhere is treated as a miss (and dropped).
        /// </summary>
        internal static bool TryGet(string key, out Texture2D texture)
        {
            if (Map.TryGetValue(key, out var node))
            {
                if (node.Value.Texture != null)
                {
                    Lru.Remove(node);
                    Lru.AddFirst(node);
                    texture = node.Value.Texture;
                    return true;
                }

                // Stale entry (texture destroyed externally) - drop it.
                Lru.Remove(node);
                Map.Remove(key);
            }

            texture = null;
            return false;
        }

        /// <summary>
        /// Cache <paramref name="texture"/> under <paramref name="key"/> as most-recently-used and
        /// evict the LRU entry (destroying its texture) if over capacity. Call only after
        /// <see cref="TryGet"/> reported a miss.
        /// </summary>
        internal static void Add(string key, Texture2D texture)
        {
            if (Map.TryGetValue(key, out var existing))
            {
                // Defensive: a value already exists for this key. Drop the old node, destroying its
                // texture unless it's the very one being re-added.
                Lru.Remove(existing);
                Map.Remove(key);
                if (existing.Value.Texture != null && existing.Value.Texture != texture)
                {
                    UnityEngine.Object.Destroy(existing.Value.Texture);
                }
            }

            var node = new LinkedListNode<Entry>(new Entry { Key = key, Texture = texture });
            Lru.AddFirst(node);
            Map[key] = node;
            EvictIfOverCapacity();
        }

        private static void EvictIfOverCapacity()
        {
            while (Map.Count > Capacity && Lru.Last != null)
            {
                var oldest = Lru.Last;
                Lru.RemoveLast();
                Map.Remove(oldest.Value.Key);
                if (oldest.Value.Texture != null)
                {
                    UnityEngine.Object.Destroy(oldest.Value.Texture);
                }
            }
        }

        /// <summary>Destroy every cached texture and clear the cache (plugin teardown).</summary>
        internal static void Purge()
        {
            foreach (var entry in Lru)
            {
                if (entry.Texture != null)
                {
                    UnityEngine.Object.Destroy(entry.Texture);
                }
            }

            Lru.Clear();
            Map.Clear();
        }
    }
}
