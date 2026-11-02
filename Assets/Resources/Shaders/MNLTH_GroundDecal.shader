// MNLTH/GroundDecal
//
// Un disque plat pose au sol : l'ombre de contact sous chaque pion, et l'anneau
// de socle a la couleur de l'equipe. Une texture, une couleur, rien d'autre.
//
// Le mode de melange est reglable : "normal" pour l'ombre (elle assombrit), additif
// pour l'anneau (il eclaire). BoardReadability choisit pour chaque materiau.
Shader "MNLTH/GroundDecal"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0,0,0,0.6)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Offset -2, -2
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Un disque par pion (ombre + anneau), des pastilles par Shofar, la fleche
            // de selection : tous le meme quad, tous le meme materiau. _Color est
            // declaree par instance parce que ces disques recoivent leur couleur par
            // MaterialPropertyBlock (pastilles, fleche).
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 c = tex2D(_MainTex, i.uv) * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                return c;
            }
            ENDCG
        }
    }

    FallBack Off
}
