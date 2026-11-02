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

            // Tous les Tanks partagent ce materiau et, le plus souvent, le meme
            // maillage : instancies, leurs liseres tiennent dans un seul appel de
            // dessin au lieu d'un par pion.
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            // Les trois proprietes sont par instance, et pas seulement la couleur :
            // les bulles des Shofars changent leur force et leur puissance renderer par
            // renderer (PortalStatusVisual, pulsation et flash de fissure). Une seule
            // propriete oubliee ici suffirait a desactiver l'instanciation sur elles.
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _RimColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _RimPower)
                UNITY_DEFINE_INSTANCED_PROP(half, _RimStrength)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = _WorldSpaceCameraPos.xyz - worldPos;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half facing = saturate(dot(normalize(i.normal), normalize(i.viewDir)));
                half rim = pow(1.0 - facing, UNITY_ACCESS_INSTANCED_PROP(Props, _RimPower))
                           * UNITY_ACCESS_INSTANCED_PROP(Props, _RimStrength);
                return fixed4(UNITY_ACCESS_INSTANCED_PROP(Props, _RimColor).rgb * rim, 1);
            }
            ENDCG
        }
    }

    FallBack Off
}
