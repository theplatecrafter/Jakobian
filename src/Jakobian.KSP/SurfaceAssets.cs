using System;
using System.Collections.Generic;
using System.IO;
using Jakobian.Core;
using UnityEngine;

namespace Jakobian.KSP
{
    internal sealed class SurfaceAssets
    {
        internal MappedRadialSurface Surface;
        internal Texture2D AlbedoTexture;
        internal RadialMap BiomePixels;
        internal readonly Dictionary<string, string> MapFingerprints = new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> MapSources = new Dictionary<string, string>(StringComparer.Ordinal);
        internal static string ShaderFingerprint { get; private set; }
        internal readonly Dictionary<uint, CBAttributeMapSO.MapAttribute> Biomes = new Dictionary<uint, CBAttributeMapSO.MapAttribute>();
        private static Shader shader;
        private static AssetBundle bundle;
        internal static Shader TerrainShader
        {
            get
            {
                if (shader != null) return shader;
                string path = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/Jakobian/Assets/jakobian-shaders");
                ShaderFingerprint = WorldBodyDescriptor.HashBytes(File.ReadAllBytes(path));
                bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null) throw new ArgumentException("Missing or incompatible Jakobian shader bundle: " + path);
                shader = bundle.LoadAsset<Shader>("Assets/JakobianMappedTerrain.shader");
                if (shader == null || !shader.isSupported) throw new ArgumentException("Jakobian mapped terrain shader is unsupported.");
                return shader;
            }
        }
        internal static SurfaceAssets Load(BodyDefinition d, ConfigNode node)
        {
            BodyDefinition.Check(node, "heightMap,colorMap,offset,deformity,biomeMap", "Biomes");
            var assets = new SurfaceAssets();
            RadialMap heights = null;
            if (node != null && node.HasValue("heightMap"))
            {
                Texture2D texture;
                heights = ReadMap(node.GetValue("heightMap"), true, out texture, out var fingerprint);
                assets.MapFingerprints.Add("height", fingerprint); assets.MapSources.Add("height", node.GetValue("heightMap"));
                UnityEngine.Object.Destroy(texture);
                for (int y = 0; y < heights.Height; y++) for (int x = 0; x < heights.Width; x++)
                {
                    uint p = heights.Pixel(x,y);
                    if ((p >> 24) != ((p >> 16) & 255) || (p >> 24) != ((p >> 8) & 255) || (p & 255) != 255)
                        throw new ArgumentException(d.Name + ": height map must be opaque grayscale PNG8 (R=G=B).");
                }
            }
            assets.Surface = new MappedRadialSurface(d.Shape, heights, BodyDefinition.Number(node, "offset", 0), BodyDefinition.Number(node, "deformity", 0));
            if (node != null && node.HasValue("colorMap"))
            {
                // Validate the same pole convention even though albedo is sampled by the GPU.
                Texture2D texture; ReadMap(node.GetValue("colorMap"), false, out texture, out var fingerprint); assets.AlbedoTexture = texture;
                assets.MapFingerprints.Add("color", fingerprint); assets.MapSources.Add("color", node.GetValue("colorMap"));
            }
            ConfigNode biomes = node?.GetNode("Biomes");
            if ((biomes != null) != (node != null && node.HasValue("biomeMap"))) throw new ArgumentException(d.Name + ": Biomes and biomeMap must be supplied together.");
            if (biomes != null)
            {
                BodyDefinition.Check(biomes, "", "Biome");
                var names = new HashSet<string>();
                foreach (ConfigNode biome in biomes.GetNodes("Biome"))
                {
                    BodyDefinition.Check(biome, "name,displayName,red,green,blue,value", "");
                    string name = BodyDefinition.Required(biome, "name");
                    if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_]{0,63}$") || !names.Add(name)) throw new ArgumentException("Invalid or duplicate biome identity: " + name);
                    byte r = Byte(biome,"red"), g = Byte(biome,"green"), b = Byte(biome,"blue");
                    uint key = ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | 255;
                    double value = BodyDefinition.Number(biome, "value", 1);
                    if (value < 0 || value > 100 || assets.Biomes.ContainsKey(key)) throw new ArgumentException("Invalid biome value or duplicate biome color: " + name);
                    assets.Biomes.Add(key, new CBAttributeMapSO.MapAttribute { name = name, displayname = biome.GetValue("displayName") ?? name,
                        localizationTag = biome.GetValue("displayName") ?? name, value = (float)value, mapColor = new Color32(r,g,b,255) });
                }
                if (assets.Biomes.Count == 0 || assets.Biomes.Count > 256) throw new ArgumentException("Biomes requires 1..256 palette entries.");
                Texture2D texture;
                assets.BiomePixels = ReadMap(node.GetValue("biomeMap"), true, out texture, out var fingerprint);
                assets.MapFingerprints.Add("biome", fingerprint); assets.MapSources.Add("biome", node.GetValue("biomeMap"));
                UnityEngine.Object.Destroy(texture);
                for (int y = 0; y < assets.BiomePixels.Height; y++) for (int x = 0; x < assets.BiomePixels.Width; x++)
                    if (!assets.Biomes.ContainsKey(assets.BiomePixels.Pixel(x,y))) throw new ArgumentException(d.Name + ": unmapped biome color at " + x + "," + y);
            }
            return assets;
        }
        private static byte Byte(ConfigNode node, string key)
        {
            double value = BodyDefinition.Number(node,key,-1);
            if (value < 0 || value > 255 || value != Math.Floor(value)) throw new ArgumentException("Biome palette channels must be integers 0..255.");
            return (byte)value;
        }
        internal static string MapPath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || !System.Text.RegularExpressions.Regex.IsMatch(relative, "^[A-Za-z0-9_ /.-]+[.]png$")
                || relative.StartsWith("/") || relative.Contains("..") || relative.Contains("//")) throw new ArgumentException("Map must be a relative GameData PNG path: " + relative);
            string root = Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath,"GameData"));
            string path = Path.GetFullPath(Path.Combine(root,relative));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Map path escapes GameData.");
            for (string part = path; !string.Equals(part,root,StringComparison.OrdinalIgnoreCase); part = Path.GetDirectoryName(part))
                if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Map paths may not traverse links: " + relative);
            return path;
        }
        private static RadialMap ReadMap(string relative, bool linear, out Texture2D texture, out string fingerprint)
        {
            string path = MapPath(relative);
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new ArgumentException("PNG exceeds 16 MiB: " + relative);
            byte[] bytes = File.ReadAllBytes(path);
            fingerprint = WorldBodyDescriptor.HashBytes(bytes);
            byte[] signature = {137,80,78,71,13,10,26,10};
            if (bytes.Length < 33) throw new ArgumentException("Truncated PNG: " + relative);
            for (int i=0; i<8; i++) if (bytes[i] != signature[i]) throw new ArgumentException("Invalid PNG signature: " + relative);
            int w = BigEndian(bytes,16), h = BigEndian(bytes,20);
            if (bytes[12]!=73 || bytes[13]!=72 || bytes[14]!=68 || bytes[15]!=82 || bytes[24]!=8 || (bytes[25]!=0 && bytes[25]!=2 && bytes[25]!=6)
                || w < 4 || w > 2048 || h < 3 || h > 1025) throw new ArgumentException("Requires non-indexed PNG8 within 2048x1025: " + relative);
            texture = new Texture2D(2,2,TextureFormat.RGBA32,false,linear) { name = relative, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            if (!ImageConversion.LoadImage(texture,bytes,false) || texture.width != w || texture.height != h) throw new ArgumentException("PNG decode failed: " + relative);
            Color32[] colors = texture.GetPixels32();
            var rgba = new byte[colors.Length * 4];
            for (int i=0; i<colors.Length; i++) { rgba[i*4]=colors[i].r; rgba[i*4+1]=colors[i].g; rgba[i*4+2]=colors[i].b; rgba[i*4+3]=colors[i].a; }
            return new RadialMap(w,h,rgba);
        }
        private static int BigEndian(byte[] b, int i) => (b[i]<<24) | (b[i+1]<<16) | (b[i+2]<<8) | b[i+3];
    }
}
