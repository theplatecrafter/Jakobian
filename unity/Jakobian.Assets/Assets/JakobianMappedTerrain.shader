Shader "Jakobian/MappedTerrain"
{
    Properties
    {
        _MainTex ("Albedo (sRGB)", 2D) = "white" {}
        _PlanetOpacity ("Scaled transition", Range(0,1)) = 0
        _Opacity ("Scaled visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        float _PlanetOpacity;
        float _Opacity;
        struct Input { float3 direction; float4 screenPos; };
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // PQS's retained planet-relative modifier owns UV0/UV1 (Mesh.uv/uv2).
            o.direction = float3(v.texcoord2.xy, v.texcoord3.x);
        }
        void surf(Input IN, inout SurfaceOutput o)
        {
            float3 d = normalize(IN.direction);
            float u = frac(atan2(d.z, d.x) / 6.28318530718);
            float v = asin(clamp(d.y,-1,1)) / 3.14159265359 + 0.5;
            // Match the CPU grid: periodic columns; rows include both poles.
            float2 uv = float2(u + _MainTex_TexelSize.x * 0.5, v * (1-_MainTex_TexelSize.y) + _MainTex_TexelSize.y * 0.5);
            o.Albedo = tex2D(_MainTex, uv).rgb;
            o.Alpha = 1;
            float2 pixel = floor(IN.screenPos.xy / IN.screenPos.w * _ScreenParams.xy);
            float threshold = frac(dot(pixel, float2(0.754877666,0.569840296)));
            clip(min(threshold - _PlanetOpacity, _Opacity - threshold));
        }
        ENDCG
    }
    Fallback "Diffuse"
}
