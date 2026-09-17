// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "OutlineMetallic"
{
	Properties
	{
		_Cutoff( "Mask Clip Value", Float ) = 0.5
		[NoScaleOffset][SingleLineTexture]_MainTex("Albedo", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)
		[NoScaleOffset][Normal][SingleLineTexture]_BumpMap("Normal Texture", 2D) = "bump" {}
		[NoScaleOffset][SingleLineTexture]_MetallicGlossMap("Metallic Texture", 2D) = "white" {}
		_Glossiness("Smoothness Value", Range( 0 , 1)) = 0
		[NoScaleOffset][SingleLineTexture]_EmissionMap("Emission Texture", 2D) = "white" {}
		[HDR]_EmissionColor("Emission Color", Color) = (0,0,0,0)
		[NoScaleOffset][SingleLineTexture]_OcclusionMap("Occlusion Map", 2D) = "white" {}
		_Cutoff("Alpha Clip Threshold", Range( 0 , 1)) = 0
		[KeywordEnum(AlbedoAlpha,MetallicAlpha)] _GlossSource("Source", Float) = 0
		_UseEmission("UseEmission", Float) = 0
		[Toggle(_SCREENSPACE_ON)] _ScreenSpace("Screen Space", Float) = 0
		[HDR]_OutlineColor("Outline Color", Color) = (0,0,0,0)
		[HideInInspector]_UseSpecular("UseSpecular", Float) = 0
		[Toggle]_Enabled("Enabled", Float) = 1
		[HideInInspector]_RampMapLoaded("RampMapLoaded", Float) = 0
		_AdaptiveThicnkess("Adaptive Thicnkess", Range( 0 , 1)) = 0
		_Metallic("Metallic", Range( 0 , 1)) = 0
		[SingleLineTexture]_RampMap("RampMap", 2D) = "white" {}
		_Thicnkess("Thicnkess", Range( 0 , 0.3)) = 0.01
		[KeywordEnum(Normal,Position,UVBaked)] _OutlineType("Outline Type", Float) = 0
		[Toggle(_USEGRADIENT_ON)] _UseGradient("Use Gradient", Float) = 0
		_Intesity("Intesity", Float) = 1
		_NoiseScale("Noise Scale", Float) = 7.9
		_FlowSpeed("Flow Speed", Range( 0 , 3)) = 0.3647059
		_Rotation("Flow Rotation", Range( 0 , 360)) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ }
		Cull Front
		CGPROGRAM
		#pragma target 3.0
		#pragma surface outlineSurf Outline nofog  keepalpha noshadow noambient novertexlights nolightmap nodynlightmap nodirlightmap nometa noforwardadd vertex:outlineVertexDataFunc 
		
		#include "UnityShaderVariables.cginc"
		
		#pragma shader_feature_local _USEGRADIENT_ON
		#pragma shader_feature_local _SCREENSPACE_ON
		#pragma shader_feature_local _OUTLINETYPE_NORMAL _OUTLINETYPE_POSITION _OUTLINETYPE_UVBAKED
		
		
		struct Input
		{
			float2 uv_texcoord;
			float4 screenPos;
			float3 worldPos;
		};
		uniform float4 _OutlineColor;
		uniform sampler2D _RampMap;
		uniform float _Rotation;
		uniform float _FlowSpeed;
		uniform float _NoiseScale;
		uniform float _Intesity;
		uniform float _Enabled;
		uniform float _AdaptiveThicnkess;
		uniform float _Thicnkess;
		
		inline float4 ASE_ComputeGrabScreenPos( float4 pos )
		{
			#if UNITY_UV_STARTS_AT_TOP
			float scale = -1.0;
			#else
			float scale = 1.0;
			#endif
			float4 o = pos;
			o.y = pos.w * 0.5f;
			o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
			return o;
		}


		//https://www.shadertoy.com/view/XdXGW8
		float2 GradientNoiseDir( float2 x )
		{
			const float2 k = float2( 0.3183099, 0.3678794 );
			x = x * k + k.yx;
			return -1.0 + 2.0 * frac( 16.0 * k * frac( x.x * x.y * ( x.x + x.y ) ) );
		}
		
		float GradientNoise( float2 UV, float Scale )
		{
			float2 p = UV * Scale;
			float2 i = floor( p );
			float2 f = frac( p );
			float2 u = f * f * ( 3.0 - 2.0 * f );
			return lerp( lerp( dot( GradientNoiseDir( i + float2( 0.0, 0.0 ) ), f - float2( 0.0, 0.0 ) ),
					dot( GradientNoiseDir( i + float2( 1.0, 0.0 ) ), f - float2( 1.0, 0.0 ) ), u.x ),
					lerp( dot( GradientNoiseDir( i + float2( 0.0, 1.0 ) ), f - float2( 0.0, 1.0 ) ),
					dot( GradientNoiseDir( i + float2( 1.0, 1.0 ) ), f - float2( 1.0, 1.0 ) ), u.x ), u.y );
		}


		void outlineVertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex );
			float lerpResult229 = lerp( 1.0 , distance( _WorldSpaceCameraPos , ase_worldPos ) , _AdaptiveThicnkess);
			float3 ase_vertexNormal = v.normal.xyz;
			float3 ase_vertex3Pos = v.vertex.xyz;
			#if defined(_OUTLINETYPE_NORMAL)
				float3 staticSwitch224 = ase_vertexNormal;
			#elif defined(_OUTLINETYPE_POSITION)
				float3 staticSwitch224 = ase_vertex3Pos;
			#elif defined(_OUTLINETYPE_UVBAKED)
				float3 staticSwitch224 = float3( v.texcoord3.xy ,  0.0 );
			#else
				float3 staticSwitch224 = ase_vertexNormal;
			#endif
			float3 outlineVar = ( _Enabled == 1.0 ? ( lerpResult229 * ( staticSwitch224 * _Thicnkess ) ) : float3( 0,0,0 ) );
			v.vertex.xyz += outlineVar;
		}
		inline half4 LightingOutline( SurfaceOutput s, half3 lightDir, half atten ) { return half4 ( 0,0,0, s.Alpha); }
		void outlineSurf( Input i, inout SurfaceOutput o )
		{
			float temp_output_1_0_g1 = radians( _Rotation );
			float2 appendResult2_g1 = (float2(cos( temp_output_1_0_g1 ) , sin( temp_output_1_0_g1 )));
			float mulTime243 = _Time.y * _FlowSpeed;
			float2 temp_output_245_0 = ( appendResult2_g1 * mulTime243 );
			float2 uv_TexCoord242 = i.uv_texcoord + temp_output_245_0;
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_screenPos );
			float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
			#ifdef _SCREENSPACE_ON
				float4 staticSwitch281 = ( ( ase_grabScreenPosNorm * _NoiseScale ) + float4( temp_output_245_0, 0.0 , 0.0 ) );
			#else
				float4 staticSwitch281 = float4( uv_TexCoord242, 0.0 , 0.0 );
			#endif
			float gradientNoise240 = GradientNoise(staticSwitch281.xy,_NoiseScale);
			gradientNoise240 = gradientNoise240*0.5 + 0.5;
			float2 temp_cast_3 = (gradientNoise240).xx;
			#ifdef _USEGRADIENT_ON
				float4 staticSwitch237 = ( tex2D( _RampMap, temp_cast_3 ) * _Intesity );
			#else
				float4 staticSwitch237 = _OutlineColor;
			#endif
			o.Emission = staticSwitch237.rgb;
		}
		ENDCG
		

		Tags{ "RenderType" = "Opaque"  "Queue" = "AlphaTest+0" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#pragma target 3.0
		#pragma multi_compile _GLOSSSOURCE_ALBEDOALPHA _GLOSSSOURCE_METALLICALPHA
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows vertex:vertexDataFunc 
		struct Input
		{
			float2 uv_texcoord;
		};

		uniform float _UseSpecular;
		uniform float _RampMapLoaded;
		uniform sampler2D _BumpMap;
		uniform sampler2D _MainTex;
		uniform float4 _Color;
		uniform float _UseEmission;
		uniform sampler2D _EmissionMap;
		uniform float4 _EmissionColor;
		uniform sampler2D _MetallicGlossMap;
		uniform float _Metallic;
		uniform float _Glossiness;
		uniform sampler2D _OcclusionMap;
		uniform float _Cutoff = 0.5;

		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			v.vertex.xyz += 0;
			v.vertex.w = 1;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float3 TangentNormal274 = UnpackNormal( tex2D( _BumpMap, i.uv_texcoord ) );
			o.Normal = TangentNormal274;
			float4 tex2DNode251 = tex2D( _MainTex, i.uv_texcoord );
			float4 AlbedoColor258 = ( tex2DNode251 * _Color );
			o.Albedo = AlbedoColor258.rgb;
			float4 EmissionColor268 = ( _UseEmission == 1.0 ? ( tex2D( _EmissionMap, i.uv_texcoord ) * _EmissionColor ) : float4( 0,0,0,0 ) );
			o.Emission = EmissionColor268.rgb;
			float4 tex2DNode255 = tex2D( _MetallicGlossMap, i.uv_texcoord );
			float MetallicValue273 = ( tex2DNode255.r * _Metallic );
			o.Metallic = MetallicValue273;
			#if defined(_GLOSSSOURCE_ALBEDOALPHA)
				float staticSwitch256 = tex2DNode251.a;
			#elif defined(_GLOSSSOURCE_METALLICALPHA)
				float staticSwitch256 = tex2DNode255.a;
			#else
				float staticSwitch256 = tex2DNode251.a;
			#endif
			float SmoothnessValue269 = ( _Glossiness * staticSwitch256 );
			o.Smoothness = SmoothnessValue269;
			float OcclusionMap271 = tex2D( _OcclusionMap, i.uv_texcoord ).r;
			o.Occlusion = OcclusionMap271;
			o.Alpha = 1;
			float Alpha270 = AlbedoColor258.a;
			clip( Alpha270 - _Cutoff );
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "OutlineEditor"
}
/*ASEBEGIN
Version=18935
916;73;1643;654;202.4792;1585.592;2.760116;True;False
Node;AmplifyShaderEditor.RangedFloatNode;246;-403.4522,-869.5601;Inherit;False;Property;_Rotation;Flow Rotation;25;0;Create;False;0;0;0;False;0;False;0;0;0;360;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;247;-444.4522,-662.5601;Inherit;False;Property;_FlowSpeed;Flow Speed;24;0;Create;False;0;0;0;False;0;False;0.3647059;0.3647059;0;3;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;243;-113.4522,-688.5601;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;244;-54.45221,-799.5601;Inherit;False;DegreesToDirection;-1;;1;ef517114e559b014bb611423825a9296;0;1;6;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;241;422.5478,-606.5601;Inherit;False;Property;_NoiseScale;Noise Scale;23;0;Create;False;0;0;0;False;0;False;7.9;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GrabScreenPosition;278;-245.4768,-1091.41;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;248;2041.299,-763.3965;Inherit;False;1441.691;2060.756;;27;275;274;273;271;270;269;268;267;266;265;264;263;262;261;260;259;258;257;256;255;254;253;252;251;250;249;218;Standard Inputs;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;245;183.5478,-736.5601;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;279;36.55712,-1060.435;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;242;382.5478,-764.5601;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;280;249.5341,-1065.176;Inherit;True;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;249;2148.525,-672.4642;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PosVertexDataNode;222;249.5983,185.8488;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NormalVertexDataNode;221;285.5982,31.84895;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.WorldSpaceCameraPos;219;473.9803,-393.2944;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldPosInputsNode;220;538.9803,-246.2941;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ColorNode;250;2407.142,-76.28429;Inherit;False;Property;_Color;Color;1;0;Create;True;0;0;0;False;0;False;1,1,1,1;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.StaticSwitch;281;575.8632,-1013.231;Inherit;False;Property;_ScreenSpace;Screen Space;11;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT4;0,0,0,0;False;0;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;4;FLOAT4;0,0,0,0;False;5;FLOAT4;0,0,0,0;False;6;FLOAT4;0,0,0,0;False;7;FLOAT4;0,0,0,0;False;8;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;223;258.5983,346.8491;Inherit;False;3;2;0;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;251;2376.53,-267.7076;Inherit;True;Property;_MainTex;Albedo;0;2;[NoScaleOffset];[SingleLineTexture];Create;False;1;PBR Settings;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;226;547.4513,-55.2884;Inherit;False;Property;_AdaptiveThicnkess;Adaptive Thicnkess;16;0;Create;False;0;0;0;False;0;False;0;0.32;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;227;674.5983,319.8491;Inherit;False;Property;_Thicnkess;Thicnkess;19;0;Create;False;0;0;0;False;0;False;0.01;0;0;0.3;0;1;FLOAT;0
Node;AmplifyShaderEditor.DistanceOpNode;225;825.9802,-252.2941;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;224;531.5983,125.8489;Inherit;False;Property;_OutlineType;Outline Type;20;0;Create;False;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;3;Normal;Position;UVBaked;Create;True;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;252;2445.719,634.9482;Inherit;True;Property;_EmissionMap;Emission Texture;5;2;[NoScaleOffset];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;253;2691.54,-94.80309;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;255;2454.842,139.5487;Inherit;True;Property;_MetallicGlossMap;Metallic Texture;3;2;[NoScaleOffset];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;254;2491.211,826.3812;Inherit;False;Property;_EmissionColor;Emission Color;6;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NoiseGeneratorNode;240;654.2593,-712.1641;Inherit;True;Gradient;True;False;2;0;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;261;2532.474,380.4409;Inherit;False;Property;_Metallic;Metallic;17;0;Create;False;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;256;2801.111,-260.1901;Inherit;False;Property;_GlossSource;Source;9;0;Create;False;0;0;0;True;0;False;1;0;0;True;;KeywordEnum;2;AlbedoAlpha;MetallicAlpha;Create;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;259;2820.27,-379.8597;Inherit;False;Property;_Glossiness;Smoothness Value;4;0;Create;False;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;260;2833.758,637.4573;Inherit;False;Property;_UseEmission;UseEmission;10;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;234;888.9782,-739.462;Inherit;True;Property;_RampMap;RampMap;18;1;[SingleLineTexture];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;258;2867.518,-97.88017;Inherit;False;AlbedoColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;239;1102.951,-531.5181;Inherit;False;Property;_Intesity;Intesity;22;0;Create;False;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;229;1057.52,-110.7601;Inherit;False;3;0;FLOAT;1;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;228;965.5986,159.8489;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;257;2757.384,769.3662;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;230;1100.918,-1120.539;Inherit;False;Property;_OutlineColor;Outline Color;12;1;[HDR];Create;False;1;Outline;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.BreakToComponentsNode;262;3066.313,-21.117;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RangedFloatNode;282;1251.256,-66.93283;Inherit;False;Property;_Enabled;Enabled;14;1;[Toggle];Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;264;2800.474,236.4408;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;265;2452.926,-698.7692;Inherit;True;Property;_OcclusionMap;Occlusion Map;7;2;[NoScaleOffset];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;266;3114.85,-328.5284;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Compare;263;2991.295,716.3712;Inherit;False;0;4;0;FLOAT;0;False;1;FLOAT;1;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;267;2473.455,1045.063;Inherit;True;Property;_BumpMap;Normal Texture;2;3;[NoScaleOffset];[Normal];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;231;1218.611,51.91579;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;238;1195.951,-657.518;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode;272;2100.982,-1086.776;Inherit;False;347;282;Do not delete!;2;276;277;Editor Variable;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;268;3178.966,745.3442;Inherit;False;EmissionColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.Compare;283;1425.256,40.06712;Inherit;False;0;4;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;271;2752.665,-662.6336;Inherit;False;OcclusionMap;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;273;2983.328,198.6859;Inherit;False;MetallicValue;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;237;1342.234,-693.7328;Inherit;False;Property;_UseGradient;Use Gradient;21;0;Create;False;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;269;3249.962,-279.3641;Inherit;False;SmoothnessValue;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;274;2804.864,1071.104;Inherit;False;TangentNormal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;270;3211.313,52.68722;Inherit;False;Alpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;275;2421.017,-460.4419;Inherit;False;Property;_Cutoff;Alpha Clip Threshold;8;0;Fetch;False;0;0;0;True;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;99;1731.956,-526.0333;Inherit;False;258;AlbedoColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;276;2150.982,-1036.776;Inherit;False;Property;_UseSpecular;UseSpecular;13;1;[HideInInspector];Create;False;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;105;1704.298,-190.6012;Inherit;False;269;SmoothnessValue;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;104;1728.959,-275.0146;Inherit;False;273;MetallicValue;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;106;1716.713,-84.05376;Inherit;False;271;OcclusionMap;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;277;2164.07,-932.1051;Inherit;False;Property;_RampMapLoaded;RampMapLoaded;15;1;[HideInInspector];Create;False;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;103;1697.672,-334.3535;Inherit;False;268;EmissionColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;101;1722.398,38.42839;Inherit;False;270;Alpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.OutlineNode;232;1522.982,-105.2647;Inherit;False;2;True;None;0;0;Front;True;True;True;True;0;False;-1;3;0;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;107;1764.756,-430.3322;Inherit;False;274;TangentNormal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;218;2067.02,-411.9409;Float;False;True;-1;2;OutlineEditor;0;0;Standard;OutlineMetallic;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.5;True;True;0;True;Opaque;;AlphaTest;All;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;243;0;247;0
WireConnection;244;6;246;0
WireConnection;245;0;244;0
WireConnection;245;1;243;0
WireConnection;279;0;278;0
WireConnection;279;1;241;0
WireConnection;242;1;245;0
WireConnection;280;0;279;0
WireConnection;280;1;245;0
WireConnection;281;1;242;0
WireConnection;281;0;280;0
WireConnection;251;1;249;0
WireConnection;225;0;219;0
WireConnection;225;1;220;0
WireConnection;224;1;221;0
WireConnection;224;0;222;0
WireConnection;224;2;223;0
WireConnection;252;1;249;0
WireConnection;253;0;251;0
WireConnection;253;1;250;0
WireConnection;255;1;249;0
WireConnection;240;0;281;0
WireConnection;240;1;241;0
WireConnection;256;1;251;4
WireConnection;256;0;255;4
WireConnection;234;1;240;0
WireConnection;258;0;253;0
WireConnection;229;1;225;0
WireConnection;229;2;226;0
WireConnection;228;0;224;0
WireConnection;228;1;227;0
WireConnection;257;0;252;0
WireConnection;257;1;254;0
WireConnection;262;0;258;0
WireConnection;264;0;255;1
WireConnection;264;1;261;0
WireConnection;265;1;249;0
WireConnection;266;0;259;0
WireConnection;266;1;256;0
WireConnection;263;0;260;0
WireConnection;263;2;257;0
WireConnection;267;1;249;0
WireConnection;231;0;229;0
WireConnection;231;1;228;0
WireConnection;238;0;234;0
WireConnection;238;1;239;0
WireConnection;268;0;263;0
WireConnection;283;0;282;0
WireConnection;283;2;231;0
WireConnection;271;0;265;1
WireConnection;273;0;264;0
WireConnection;237;1;230;0
WireConnection;237;0;238;0
WireConnection;269;0;266;0
WireConnection;274;0;267;0
WireConnection;270;0;262;3
WireConnection;232;0;237;0
WireConnection;232;1;283;0
WireConnection;218;0;99;0
WireConnection;218;1;107;0
WireConnection;218;2;103;0
WireConnection;218;3;104;0
WireConnection;218;4;105;0
WireConnection;218;5;106;0
WireConnection;218;10;101;0
WireConnection;218;11;232;0
ASEEND*/
//CHKSM=F240BADB5AC47E585AC657DBD16422DFFD3E4750