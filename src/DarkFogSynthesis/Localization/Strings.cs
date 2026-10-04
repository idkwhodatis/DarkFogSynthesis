using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using DarkFogSynthesis.Core.Definitions;
using UnityEngine;

namespace DarkFogSynthesis.Localization
{
    internal static class Strings
    {
        private static readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        internal static void Register()
        {
            var english = Read("Strings.en-US.json");
            var chinese = Read("Strings.zh-CN.json");
            if (!english.Keys.OrderBy(k => k).SequenceEqual(chinese.Keys.OrderBy(k => k)))
                throw new InvalidDataException("English/Chinese localization keys differ.");
            foreach (var entry in english) LocalizationModule.RegisterTranslation(entry.Key, entry.Value, chinese[entry.Key], "");
        }

        internal static Sprite LoadIcon(string name)
        {
            if (sprites.TryGetValue(name, out var existing)) return existing;
            string path = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, "assets", name + ".png");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false)) throw new InvalidDataException("Invalid icon: " + name);
            texture.name = Plugin.Guid + "." + name;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            assets.Add(texture); assets.Add(sprite);
            sprites.Add(name, sprite);
            return sprite;
        }

        internal static void Dispose()
        {
            foreach (var asset in assets) if (asset != null) UnityEngine.Object.Destroy(asset);
            assets.Clear();
            sprites.Clear();
        }

        private static Dictionary<string, string> Read(string suffix)
        {
            var assembly = typeof(Plugin).Assembly;
            string resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(suffix, StringComparison.Ordinal));
            using (var stream = assembly.GetManifestResourceStream(resource))
            using (var reader = new StreamReader(stream!))
                return new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
        }
    }
}
