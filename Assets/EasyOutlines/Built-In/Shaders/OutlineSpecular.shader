// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "OutlineSpecular"
{
	Properties
	{
		[NoScaleOffset][SingleLineTexture]_MainTex("Albedo", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)
		[NoScaleOffset][Normal][SingleLineTexture]_BumpMap("Normal Texture", 2D) = "bump" {}
		[NoScaleOffset][SingleLineTexture]_SpecGlossMap("Specular Texture", 2D) = "white" {}
		[NoScaleOffset][SingleLineTexture]_MetallicGlossMap("Metallic Texture", 2D) = "white" {}
		_SpecColor("Specular Value", Color) = (0.6226415,0.6226415,0.6226415,0)
		_Glossiness("Smoothness Value", Range( 0 , 1)) = 0
		[NoScaleOffset][SingleLineTexture]_EmissionMap("Emission Texture", 2D) = "white" {}
		[HDR]_EmissionColor("Emission Color", Color) = (0,0,0,0)
		[NoScaleOffset][SingleLineTexture]_OcclusionMap("Occlusion Map", 2D) = "white" {}
		_Cutoff("Alpha Clip Threshold", Range( 0 , 1)) = 0
		[KeywordEnum(AlbedoAlpha,SpecularAlpha)] _GlossSource("Source", Float) = 0
		[Toggle(_SCREENSPACE_ON)] _ScreenSpace("Screen Space", Float) = 0
		[HDR]_OutlineColor("Outline Color", Color) = (0,0,0,0)
		[Toggle]_Enabled("Enabled", Float) = 1
		[HideInInspector]_RampMapLoaded("RampMapLoaded", Float) = 0
		_UseEmission("UseEmission", Float) = 0
		[HideInInspector][Spec]_UseSpecular("yes", Float) = 1
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
			float lerpResult245 = lerp( 1.0 , distance( _WorldSpaceCameraPos , ase_worldPos ) , _AdaptiveThicnkess);
			float3 ase_vertexNormal = v.normal.xyz;
			float3 ase_vertex3Pos = v.vertex.xyz;
			#if defined(_OUTLINETYPE_NORMAL)
				float3 staticSwitch240 = ase_vertexNormal;
			#elif defined(_OUTLINETYPE_POSITION)
				float3 staticSwitch240 = ase_vertex3Pos;
			#elif defined(_OUTLINETYPE_UVBAKED)
				float3 staticSwitch240 = float3( v.texcoord3.xy ,  0.0 );
			#else
				float3 staticSwitch240 = ase_vertexNormal;
			#endif
			float3 outlineVar = ( _Enabled == 1.0 ? ( lerpResult245 * ( staticSwitch240 * _Thicnkess ) ) : float3( 0,0,0 ) );
			v.vertex.xyz += outlineVar;
		}
		inline half4 LightingOutline( SurfaceOutput s, half3 lightDir, half atten ) { return half4 ( 0,0,0, s.Alpha); }
		void outlineSurf( Input i, inout SurfaceOutput o )
		{
			float temp_output_1_0_g1 = radians( _Rotation );
			float2 appendResult2_g1 = (float2(cos( temp_output_1_0_g1 ) , sin( temp_output_1_0_g1 )));
			float mulTime226 = _Time.y * _FlowSpeed;
			float2 temp_output_228_0 = ( appendResult2_g1 * mulTime226 );
			float2 uv_TexCoord229 = i.uv_texcoord + temp_output_228_0;
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_screenPos );
			float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
			#ifdef _SCREENSPACE_ON
				float4 staticSwitch254 = ( ( ase_grabScreenPosNorm * _NoiseScale ) + float4( temp_output_228_0, 0.0 , 0.0 ) );
			#else
				float4 staticSwitch254 = float4( uv_TexCoord229, 0.0 , 0.0 );
			#endif
			float gradientNoise232 = GradientNoise(staticSwitch254.xy,_NoiseScale);
			gradientNoise232 = gradientNoise232*0.5 + 0.5;
			float2 temp_cast_3 = (gradientNoise232).xx;
			#ifdef _USEGRADIENT_ON
				float4 staticSwitch248 = ( tex2D( _RampMap, temp_cast_3 ) * _Intesity );
			#else
				float4 staticSwitch248 = _OutlineColor;
			#endif
			o.Emission = staticSwitch248.rgb;
		}
		ENDCG
		

		Tags{ "RenderType" = "Opaque"  "Queue" = "AlphaTest+0" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#pragma target 3.0
		#pragma multi_compile _GLOSSSOURCE_ALBEDOALPHA _GLOSSSOURCE_SPECULARALPHA
		#pragma surface surf StandardSpecular keepalpha addshadow fullforwardshadows vertex:vertexDataFunc 
		struct Input
		{
			float2 uv_texcoord;
		};

		uniform sampler2D _MetallicGlossMap;
		uniform float _UseSpecular;
		uniform float _Metallic;
		uniform float _RampMapLoaded;
		uniform sampler2D _BumpMap;
		uniform sampler2D _MainTex;
		uniform float4 _MainTex_ST;
		uniform float4 _Color;
		uniform float _UseEmission;
		uniform sampler2D _EmissionMap;
		uniform float4 _EmissionColor;
		uniform sampler2D _SpecGlossMap;
		uniform float _Glossiness;
		uniform sampler2D _OcclusionMap;
		uniform float _Cutoff;

		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			v.vertex.xyz += 0;
			v.vertex.w = 1;
		}

		void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float3 TangentNormal97 = UnpackNormal( tex2D( _BumpMap, uv_MainTex ) );
			o.Normal = TangentNormal97;
			float4 tex2DNode7 = tex2D( _MainTex, uv_MainTex );
			float4 AlbedoColor90 = ( tex2DNode7 * _Color );
			o.Albedo = AlbedoColor90.rgb;
			float4 EmissionColor96 = ( _UseEmission == 1.0 ? ( tex2D( _EmissionMap, uv_MainTex ) * _EmissionColor ) : float4( 0,0,0,0 ) );
			o.Emission = EmissionColor96.rgb;
			float4 tex2DNode53 = tex2D( _SpecGlossMap, uv_MainTex );
			float4 SpecularColor94 = ( tex2DNode53.r * _SpecColor );
			o.Specular = SpecularColor94.rgb;
			#if defined(_GLOSSSOURCE_ALBEDOALPHA)
				float staticSwitch64 = tex2DNode7.a;
			#elif defined(_GLOSSSOURCE_SPECULARALPHA)
				float staticSwitch64 = tex2DNode53.a;
			#else
				float staticSwitch64 = tex2DNode7.a;
			#endif
			float SmoothnessValue95 = ( _Glossiness * staticSwitch64 );
			o.Smoothness = SmoothnessValue95;
			float OcclusionMap88 = tex2D( _OcclusionMap, uv_MainTex ).r;
			o.Occlusion = OcclusionMap88;
			o.Alpha = 1;
			float Alpha93 = AlbedoColor90.a;
			clip( Alpha93 - _Cutoff );
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "OutlineEditor"
}
/*ASEBEGIN
Version=18935
916;73;1643;654;-92.21375;1405.023;2.066457;True;False
Node;AmplifyShaderEditor.RangedFloatNode;225;-488.2589,-619.8369;Inherit;False;Property;_FlowSpeed;Flow Speed;26;0;Create;False;0;0;0;False;0;False;0.3647059;0.3647059;0;3;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;224;-447.2589,-826.8368;Inherit;False;Property;_Rotation;Flow Rotation;27;0;Create;False;0;0;0;False;0;False;0;0;0;360;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;227;-98.25885,-756.8368;Inherit;False;DegreesToDirection;-1;;1;ef517114e559b014bb611423825a9296;0;1;6;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;226;-157.2589,-645.8368;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GrabScreenPosition;251;-207.0947,-1038.919;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;230;378.7412,-563.8369;Inherit;False;Property;_NoiseScale;Noise Scale;25;0;Create;False;0;0;0;False;0;False;7.9;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;98;2513.396,-285.3597;Inherit;False;1441.691;2060.756;;28;61;94;97;96;95;88;93;54;44;48;67;71;91;72;42;64;47;43;90;22;46;53;49;23;7;221;222;223;Standard Inputs;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;228;139.7411,-693.8368;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;252;74.93922,-1007.944;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;229;338.7412,-721.8368;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;253;287.9162,-1012.685;Inherit;True;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;223;2582.467,-182.4197;Inherit;False;0;7;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PosVertexDataNode;236;205.7917,228.572;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexCoordVertexDataNode;234;214.7917,389.5723;Inherit;False;3;2;0;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.WorldSpaceCameraPos;233;430.1737,-350.5712;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldPosInputsNode;231;495.1737,-203.5709;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ColorNode;23;2879.239,401.7525;Inherit;False;Property;_Color;Color;1;0;Create;True;0;0;0;False;0;False;1,1,1,1;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.StaticSwitch;254;614.2454,-960.7398;Inherit;False;Property;_ScreenSpace;Screen Space;12;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT4;0,0,0,0;False;0;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;4;FLOAT4;0,0,0,0;False;5;FLOAT4;0,0,0,0;False;6;FLOAT4;0,0,0,0;False;7;FLOAT4;0,0,0,0;False;8;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.NormalVertexDataNode;235;241.7915,74.5722;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;7;2848.627,210.3292;Inherit;True;Property;_MainTex;Albedo;0;2;[NoScaleOffset];[SingleLineTexture];Create;False;1;PBR Settings;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;22;3163.637,383.2337;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;239;503.6447,-12.56516;Inherit;False;Property;_AdaptiveThicnkess;Adaptive Thicnkess;18;0;Create;False;0;0;0;False;0;False;0;0.32;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;240;487.7917,168.5721;Inherit;False;Property;_OutlineType;Outline Type;22;0;Create;False;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;3;Normal;Position;UVBaked;Create;True;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DistanceOpNode;242;782.1736,-209.5709;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;241;630.7918,362.5723;Inherit;False;Property;_Thicnkess;Thicnkess;21;0;Create;False;0;0;0;False;0;False;0.01;0;0;0.3;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;53;2926.939,617.5856;Inherit;True;Property;_SpecGlossMap;Specular Texture;3;2;[NoScaleOffset];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;49;2917.817,1112.985;Inherit;True;Property;_EmissionMap;Emission Texture;7;2;[NoScaleOffset];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;46;2963.308,1304.418;Inherit;False;Property;_EmissionColor;Emission Color;8;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NoiseGeneratorNode;232;610.4528,-669.4408;Inherit;True;Gradient;True;False;2;0;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;47;3292.367,98.17715;Inherit;False;Property;_Glossiness;Smoothness Value;6;0;Create;False;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;238;1059.144,-488.7949;Inherit;False;Property;_Intesity;Intesity;24;0;Create;False;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;43;2958.178,823.3995;Inherit;False;Property;_SpecColor;Specular Value;5;0;Fetch;False;0;0;0;False;0;False;0.6226415,0.6226415,0.6226415,0;0.6226415,0.6226415,0.6226415,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.StaticSwitch;64;3273.208,217.8466;Inherit;False;Property;_GlossSource;Source;11;0;Create;False;0;0;0;True;0;False;1;0;0;True;;KeywordEnum;2;AlbedoAlpha;SpecularAlpha;Create;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;72;3305.855,1115.494;Inherit;False;Property;_UseEmission;UseEmission;16;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;90;3339.615,380.1566;Inherit;False;AlbedoColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;245;1013.713,-68.03686;Inherit;False;3;0;FLOAT;1;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;237;845.1716,-696.7386;Inherit;True;Property;_RampMap;RampMap;20;1;[SingleLineTexture];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;42;3229.481,1247.403;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;243;921.7919,202.5721;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;247;1174.804,94.63902;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;255;1142.825,-67.94372;Inherit;False;Property;_Enabled;Enabled;14;1;[Toggle];Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;67;3586.947,149.5085;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;91;3538.411,456.9198;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.ColorNode;246;1057.111,-1077.816;Inherit;False;Property;_OutlineColor;Outline Color;13;1;[HDR];Create;False;1;Outline;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;48;3340.661,707.9747;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;54;2925.023,-220.7324;Inherit;True;Property;_OcclusionMap;Occlusion Map;9;2;[NoScaleOffset];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;244;1152.144,-614.7947;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.Compare;71;3463.392,1194.408;Inherit;False;0;4;0;FLOAT;0;False;1;FLOAT;1;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;44;2945.552,1523.099;Inherit;True;Property;_BumpMap;Normal Texture;2;3;[NoScaleOffset];[Normal];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;94;3514.425,714.7227;Inherit;False;SpecularColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.Compare;256;1316.825,39.05622;Inherit;False;0;4;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;219;2613.202,-539.0668;Inherit;False;282;232;Do not delete!;2;250;220;Editor Variable;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;95;3722.059,198.6727;Inherit;False;SmoothnessValue;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;93;3683.411,530.724;Inherit;False;Alpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;248;1298.427,-651.0095;Inherit;False;Property;_UseGradient;Use Gradient;23;0;Create;False;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;96;3651.063,1223.381;Inherit;False;EmissionColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;88;3224.762,-184.5968;Inherit;False;OcclusionMap;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;97;3276.961,1549.14;Inherit;False;TangentNormal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.OutlineNode;249;1480.117,-39.56232;Inherit;False;2;True;None;0;0;Front;True;True;True;True;0;False;-1;3;0;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;104;1728.959,-231.0146;Inherit;False;94;SpecularColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;61;2893.114,17.59496;Inherit;False;Property;_Cutoff;Alpha Clip Threshold;10;0;Fetch;False;0;0;0;True;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;221;3555.005,-220.5974;Inherit;True;Property;_MetallicGlossMap;Metallic Texture;4;2;[NoScaleOffset];[SingleLineTexture];Create;False;0;0;0;True;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;105;1695.298,-159.6012;Inherit;False;95;SmoothnessValue;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;103;1697.672,-334.3535;Inherit;False;96;EmissionColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;106;1717.713,-84.05376;Inherit;False;88;OcclusionMap;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;220;2663.202,-489.0667;Inherit;False;Property;_UseSpecular;yes;17;1;[HideInInspector];Create;False;0;0;0;True;1;Spec;False;1;1;1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;222;3632.637,20.29483;Inherit;False;Property;_Metallic;Metallic;19;0;Create;False;0;0;0;True;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;250;2655.257,-392.8966;Inherit;False;Property;_RampMapLoaded;RampMapLoaded;15;1;[HideInInspector];Create;False;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;99;1731.956,-526.0333;Inherit;False;90;AlbedoColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;107;1764.756,-430.3322;Inherit;False;97;TangentNormal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;101;1722.398,38.42839;Inherit;False;93;Alpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;218;2006.266,-468.021;Float;False;True;-1;2;OutlineEditor;0;0;StandardSpecular;OutlineSpecular;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.5;True;True;0;True;Opaque;;AlphaTest;All;18;all;True;True;True;True;0;False;-1;False;3;False;-1;255;False;-1;255;False;-1;7;False;-1;3;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;True;61;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;227;6;224;0
WireConnection;226;0;225;0
WireConnection;228;0;227;0
WireConnection;228;1;226;0
WireConnection;252;0;251;0
WireConnection;252;1;230;0
WireConnection;229;1;228;0
WireConnection;253;0;252;0
WireConnection;253;1;228;0
WireConnection;254;1;229;0
WireConnection;254;0;253;0
WireConnection;7;1;223;0
WireConnection;22;0;7;0
WireConnection;22;1;23;0
WireConnection;240;1;235;0
WireConnection;240;0;236;0
WireConnection;240;2;234;0
WireConnection;242;0;233;0
WireConnection;242;1;231;0
WireConnection;53;1;223;0
WireConnection;49;1;223;0
WireConnection;232;0;254;0
WireConnection;232;1;230;0
WireConnection;64;1;7;4
WireConnection;64;0;53;4
WireConnection;90;0;22;0
WireConnection;245;1;242;0
WireConnection;245;2;239;0
WireConnection;237;1;232;0
WireConnection;42;0;49;0
WireConnection;42;1;46;0
WireConnection;243;0;240;0
WireConnection;243;1;241;0
WireConnection;247;0;245;0
WireConnection;247;1;243;0
WireConnection;67;0;47;0
WireConnection;67;1;64;0
WireConnection;91;0;90;0
WireConnection;48;0;53;1
WireConnection;48;1;43;0
WireConnection;54;1;223;0
WireConnection;244;0;237;0
WireConnection;244;1;238;0
WireConnection;71;0;72;0
WireConnection;71;2;42;0
WireConnection;44;1;223;0
WireConnection;94;0;48;0
WireConnection;256;0;255;0
WireConnection;256;2;247;0
WireConnection;95;0;67;0
WireConnection;93;0;91;3
WireConnection;248;1;246;0
WireConnection;248;0;244;0
WireConnection;96;0;71;0
WireConnection;88;0;54;1
WireConnection;97;0;44;0
WireConnection;249;0;248;0
WireConnection;249;1;256;0
WireConnection;218;0;99;0
WireConnection;218;1;107;0
WireConnection;218;2;103;0
WireConnection;218;3;104;0
WireConnection;218;4;105;0
WireConnection;218;5;106;0
WireConnection;218;10;101;0
WireConnection;218;11;249;0
ASEEND*/
//CHKSM=90F19D671286CA3BC7BCC791A1DB2A15DA173FC9