using UnityEngine;

namespace FangtasticPalette
{
    /// <summary>
    /// Reapplies the bat's body colour every frame as a safety net against the game reverting
    /// customization - the cat needed this because something reinstantiates form materials on no
    /// fixed schedule (16-recolouring-characters.md §8). Cheap: TextureRecolor caches the
    /// regenerated textures, so a same-material reapply is a couple of dictionary lookups and a
    /// SetTexture, not a pixel rebuild.
    ///
    /// The bat probe reported HasPropertyBlock=false on the body, so it may not actually revert -
    /// but the loop costs nothing when the texture is cached, and if reversion does happen this
    /// closes the gap to ~16ms rather than letting the wrong colour show.
    ///
    /// includeEyes: TRUE - the bat's eyes reverted after dropping and re-entering Bat Form when they
    /// were only applied on equip, so they're reapplied every frame like the body. Unlike the cat
    /// (whose eyes flickered in the loop because they fought a MaterialPropertyBlock writer), the bat
    /// eyes report HasPropertyBlock=false, so nothing competes at matched frequency - the reapply
    /// just wins. If flicker ever appears here, that assumption changed.
    /// </summary>
    internal sealed class BatColorReapplier : MonoBehaviour
    {
        private void Update()
        {
            BatColorPatch.ApplyBatColors(logVerbose: false, includeEyes: true);
        }
    }
}
