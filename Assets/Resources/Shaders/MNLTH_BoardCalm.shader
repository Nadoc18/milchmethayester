// MNLTH/BoardCalm
//
// Le shader Standard du plateau, avec un "calmant" : desaturation et compression
// des valeurs AVANT l'eclairage. Le terrain reste lisible (on voit toujours le vert
// de la colline, le rouge de la montagne) mais il cesse de rivaliser avec les pions.
//
// Il ne sert jamais directement : BoardReadability fabrique, au lancement, une copie
// de chaque materiau de terrain avec ce shader, et laisse les originaux intacts.
//
// Cout : celui du Standard + 6 instructions arithmetiques par pixel. Aucune passe
// en plus, aucun post-effet : c'est ce qui le rend acceptable en WebGL.
Shader "MNLTH/BoardCalm"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}

        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1

        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _UseMetallicMap ("Use Metallic Map", Float) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _GlossMapScale ("Smoothness Scale", Range(0,1)) = 1

        _EmissionMap ("Emission", 2D) = "white" {}
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)

        _CalmDesaturate ("Calm - Desaturation", Range(0,1)) = 0.45
        _CalmContrast ("Calm - Contrast", Range(0,1)) = 0.7
        _CalmPivot ("Calm - Contrast Pivot", Range(0,1)) = 0.38
        _CalmBrightness ("Calm - Brightness", Range(0,2)) = 1
        _CalmEmissionDesaturate ("Calm - Emission Desaturation", Range(0,1)) = 0.6
        _CalmEmissionScale ("Calm - Emission Scale", Range(0,1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MetallicGlossMap;
        sampler2D _EmissionMap;

        fixed4 _Color;
        half _BumpScale;
        half _UseMetallicMap;
        half _Metallic;
        half _Glossiness;
        half _GlossMapScale;
        half4 _EmissionColor;

        half _CalmDesaturate;
        half _CalmContrast;
        half _CalmPivot;
        half _CalmBrightness;
        half _CalmEmissionDesaturate;
        half _CalmEmissionScale;

        struct Input
        {
            float2 uv_MainTex;
        };

        half Luma(half3 c)
        {
            return dot(c, half3(0.299, 0.587, 0.114));
        }

        half3 Calm(half3 c)
        {
            // 1. Moins de couleur : chaque case garde sa teinte, mais a mi-voix.
            c = lerp(c, Luma(c).xxx, _CalmDesaturate);

            // 2. Moins d'ecart de clarte : les cases les plus sombres (montagnes) et
            //    les plus claires (plaines) se rapprochent d'une valeur moyenne. Ce sont
            //    les pions qui doivent porter les extremes, pas le decor.
            c = (c - _CalmPivot) * _CalmContrast + _CalmPivot;

            return saturate(c * _CalmBrightness);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = Calm(albedo.rgb);

            half3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            n.xy *= _BumpScale;
            o.Normal = normalize(n);

            half4 mg = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            o.Metallic = lerp(_Metallic, mg.r, _UseMetallicMap);
            o.Smoothness = lerp(_Glossiness, mg.a * _GlossMapScale, _UseMetallicMap);

            // L'emission du decor (cristaux, flammes) est ce qui brillait le plus fort
            // sur le plateau : elle aussi baisse d'un ton, sinon elle volerait aux pions
            // le privilege d'etre ce qui brille.
            half3 e = tex2D(_EmissionMap, IN.uv_MainTex).rgb * _EmissionColor.rgb;
            e = lerp(e, Luma(e).xxx, _CalmEmissionDesaturate);
            o.Emission = e * _CalmEmissionScale;

            o.Alpha = albedo.a;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
