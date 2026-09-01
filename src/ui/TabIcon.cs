using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace FangtasticPalette
{
    /// <summary>
    /// The Bat Form wardrobe tab icon. Resolution order:
    ///   1. a user-supplied PNG at <c>BepInEx/config/FangtasticPalette/tab-icon.png</c> - custom art
    ///      swapped in without a rebuild;
    ///   2. the mod's built-in bat icon, embedded in the DLL so it ships with the plugin (the release
    ///      zip bundles only the DLL, so a bundled default has to live inside it);
    ///   3. a generated glyph as a last resort, only if the embedded resource can't be read.
    /// The tab widget assigns the sprite without tinting, so each source's own colours show.
    /// </summary>
    internal static class TabIcon
    {
        private const string FileName = "tab-icon.png";

        // Pinned by <LogicalName> in the .csproj, so it does not depend on the file's on-disk folder.
        private const string EmbeddedResourceName = "FangtasticPalette.tab-icon.png";

        private static Sprite cached;          // the user-supplied override, once resolved
        private static bool tried;

        private static Sprite embeddedDefault; // the built-in bat icon, once loaded
        private static bool embeddedTried;

        internal static Sprite Get()
        {
            if (tried)
            {
                return cached != null ? cached : DefaultIcon();
            }

            tried = true;
            try
            {
                var path = Path.Combine(Paths.ConfigPath, "FangtasticPalette", FileName);
                if (!File.Exists(path))
                {
                    FangtasticPalettePlugin.Log.LogInfo(
                        $"[FangtasticPalette] Wardrobe: no custom tab icon at '{path}' - using the built-in bat icon.");
                    return DefaultIcon();
                }

                cached = SpriteFromPng(File.ReadAllBytes(path));
                if (cached != null)
                {
                    FangtasticPalettePlugin.Log.LogInfo(
                        $"[FangtasticPalette] Wardrobe: loaded custom tab icon {cached.texture.width}x{cached.texture.height} from '{path}'.");
                }
                else
                {
                    FangtasticPalettePlugin.Log.LogWarning(
                        $"[FangtasticPalette] Wardrobe: '{path}' is not a decodable PNG - using the built-in bat icon.");
                }
            }
            catch (Exception e)
            {
                FangtasticPalettePlugin.Log.LogError($"[FangtasticPalette] Wardrobe: failed to load custom tab icon: {e}");
            }

            return cached != null ? cached : DefaultIcon();
        }

        /// <summary>
        /// The built-in bat icon embedded in the DLL, loaded once. Falls back to the generated glyph
        /// only if the embedded resource is missing/undecodable, which shouldn't happen in a real build.
        /// </summary>
        private static Sprite DefaultIcon()
        {
            if (embeddedTried)
            {
                return embeddedDefault != null ? embeddedDefault : PawSprite.Get();
            }

            embeddedTried = true;
            try
            {
                var assembly = typeof(TabIcon).Assembly;
                using (var stream = assembly.GetManifestResourceStream(EmbeddedResourceName))
                {
                    if (stream == null)
                    {
                        FangtasticPalettePlugin.Log.LogWarning(
                            $"[FangtasticPalette] Wardrobe: embedded '{EmbeddedResourceName}' not found (have: " +
                            $"{string.Join(", ", assembly.GetManifestResourceNames())}) - using the generated glyph.");
                        return PawSprite.Get();
                    }

                    embeddedDefault = SpriteFromPng(ReadAll(stream));
                    if (embeddedDefault != null)
                    {
                        FangtasticPalettePlugin.Log.LogInfo(
                            $"[FangtasticPalette] Wardrobe: using the built-in bat icon {embeddedDefault.texture.width}x{embeddedDefault.texture.height}.");
                    }
                    else
                    {
                        FangtasticPalettePlugin.Log.LogWarning(
                            "[FangtasticPalette] Wardrobe: the embedded bat icon is not a decodable PNG - using the generated glyph.");
                    }
                }
            }
            catch (Exception e)
            {
                FangtasticPalettePlugin.Log.LogError($"[FangtasticPalette] Wardrobe: failed to load the built-in bat icon: {e}");
            }

            return embeddedDefault != null ? embeddedDefault : PawSprite.Get();
        }

        private static byte[] ReadAll(Stream stream)
        {
            var data = new byte[stream.Length];
            var read = 0;
            while (read < data.Length)
            {
                var n = stream.Read(data, read, data.Length - read);
                if (n <= 0)
                {
                    break;
                }

                read += n;
            }

            return data;
        }

        /// <summary>
        /// Decodes PNG bytes into a sprite, or null if the data isn't a decodable PNG. Destroys the
        /// scratch texture on any failure - a false result OR an exception - so it can never leak.
        /// </summary>
        private static Sprite SpriteFromPng(byte[] data)
        {
            var texture = NewIconTexture();
            var ok = false;
            try
            {
                // LoadImage resizes the texture to the PNG's dimensions and decodes it (alpha kept).
                if (ImageConversion.LoadImage(texture, data))
                {
                    var sprite = ToSprite(texture);
                    ok = true;
                    return sprite;
                }

                return null;
            }
            finally
            {
                if (!ok)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }

        private static Texture2D NewIconTexture() => new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
        {
            name = "FangtasticPalette_TabIcon",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        private static Sprite ToSprite(Texture2D texture) =>
            Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
