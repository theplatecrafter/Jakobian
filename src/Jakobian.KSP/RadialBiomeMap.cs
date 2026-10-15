using System;
using System.Linq;
using Jakobian.Core;
using UnityEngine;
using CoreVector = Jakobian.Core.Vector3d;

namespace Jakobian.KSP
{
    /// <summary>Native biome consumer bridge. GetAtt takes stock latitude/longitude in radians.</summary>
    public sealed class RadialBiomeMap : CBAttributeMapSO
    {
        private SurfaceAssets assets;
        internal void Initialize(SurfaceAssets value)
        {
            assets = value; Attributes = value.Biomes.Values.ToArray(); exactSearch = true;
            int width = value.BiomePixels.Width, height = value.BiomePixels.Height;
            var texture = new Texture2D(width,height,TextureFormat.RGBA32,false,true);
            var pixels = new Color[width*height];
            for (int y=0;y<height;y++) for (int x=0;x<width;x++)
            {
                // Stock MapSO chart reverses longitude and has a quarter-turn offset.
                double lat = (double)y/(height-1)*Math.PI-Math.PI/2, lon = (.25-(double)x/width)*2*Math.PI;
                pixels[y*width+x] = Attribute(lat,lon).mapColor;
            }
            texture.SetPixels(pixels); texture.Apply(); CreateMap(MapDepth.RGB,texture);
            UnityEngine.Object.Destroy(texture);
        }
        private MapAttribute Attribute(double latitude, double longitude)
        {
            if (!CoreVector.Finite(latitude) || !CoreVector.Finite(longitude)) return Attributes[0];
            double lat = Math.Max(-Math.PI/2,Math.Min(Math.PI/2,latitude));
            return assets.Biomes[assets.BiomePixels.Nearest(new CoreVector(Math.Cos(lat)*Math.Cos(longitude),Math.Sin(lat),Math.Cos(lat)*Math.Sin(longitude)))];
        }
        public override MapAttribute GetAtt(double lat, double lon) => Attribute(lat,lon);
    }
}
