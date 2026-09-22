// MNLTH/PawnRim
//
// Un lisere lumineux qui suit la silhouette : la ou la surface se detourne de la
// camera, elle s'allume a la couleur de l'equipe.
//
// Ce n'est pas un contour dessine : c'est une seconde passe additive sur le meme
// modele. Le materiau d'origine du pion n'est pas touche, on ajoute juste cette
// passe par-dessus. Resultat : un pion bleu se detache sur une case bleue, parce que
// son bord brille et que la case, elle, ne brille pas.
//
// Cout : une passe de plus par pion, trois instructions par pixel. Rien en plein
// ecran, aucune texture.
Shader "MNLTH/PawnRim"
{
    Properties
    {
        _RimColor ("Rim Color", Color) = (0.25, 0.85, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 4)) = 1.3
    }

    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _RimColor;
            half _RimPower;
            half _RimStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = _WorldSpaceCameraPos.xyz - worldPos;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half facing = saturate(dot(normalize(i.normal), normalize(i.viewDir)));
                half rim = pow(1.0 - facing, _RimPower) * _RimStrength;
                return fixed4(_RimColor.rgb * rim, 1);
            }
            ENDCG
        }
    }

    FallBack Off
}
