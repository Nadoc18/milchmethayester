// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "OnlyOutlineWithMask"
{
	Properties
	{
		_AdaptiveThicnkess("Adaptive Thicnkess", Range( 0 , 1)) = 0
		_Thicnkess("Thicnkess", Range( 0 , 0.3)) = 0.01
		[KeywordEnum(Normal,Position,UVBaked)] _OutlineType("Outline Type", Float) = 0
		[HDR]_OutlineColor("Outline Color", Color) = (0,0,0,0)
		[SingleLineTexture]_RampMap("RampMap", 2D) = "white" {}
		[Toggle(_USEGRADIENT_ON)] _UseGradient("Use Gradient", Float) = 0
		_Intesity("Intesity", Float) = 1
		_NoiseScale("Noise Scale", Float) = 7.9
		_FlowSpeed("Flow Speed", Range( 0 , 3)) = 0.3647059
		_Rotation("Flow Rotation", Range( 0 , 360)) = 0
		[HideInInspector]_RampMapLoaded("RampMapLoaded", Float) = 0
		[Toggle(_SCREENSPACE_ON)] _ScreenSpace("Screen Space", Float) = 0
		[Toggle]_Enabled("Enabled", Float) = 1
		_MaskRef("Stencil Reference", Int) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Front
		Stencil
		{
			Ref [_MaskRef]
			Comp NotEqual
		}
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma shader_feature_local _OUTLINETYPE_NORMAL _OUTLINETYPE_POSITION _OUTLINETYPE_UVBAKED
		#pragma shader_feature_local _USEGRADIENT_ON
		#pragma shader_feature_local _SCREENSPACE_ON
		#pragma surface surf Unlit keepalpha addshadow fullforwardshadows exclude_path:deferred noambient novertexlights nolightmap  nodynlightmap nodirlightmap nofog nometa noforwardadd vertex:vertexDataFunc 
		struct Input
		{
			float3 worldPos;
			float2 uv_texcoord;
			float4 screenPos;
		};

		uniform float _RampMapLoaded;
		uniform int _MaskRef;
		uniform float _Enabled;
		uniform float _AdaptiveThicnkess;
		uniform float _Thicnkess;
		uniform float4 _OutlineColor;
		uniform sampler2D _RampMap;
		uniform float _Rotation;
		uniform float _FlowSpeed;
		uniform float _NoiseScale;
		uniform float _Intesity;


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


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex );
			float lerpResult7 = lerp( 1.0 , distance( _WorldSpaceCameraPos , ase_worldPos ) , _AdaptiveThicnkess);
			float3 ase_vertexNormal = v.normal.xyz;
			float3 ase_vertex3Pos = v.vertex.xyz;
			#if defined(_OUTLINETYPE_NORMAL)
				float3 staticSwitch14 = ase_vertexNormal;
			#elif defined(_OUTLINETYPE_POSITION)
				float3 staticSwitch14 = ase_vertex3Pos;
			#elif defined(_OUTLINETYPE_UVBAKED)
				float3 staticSwitch14 = float3( v.texcoord3.xy ,  0.0 );
			#else
				float3 staticSwitch14 = ase_vertexNormal;
			#endif
			v.vertex.xyz += ( _Enabled == 1.0 ? ( lerpResult7 * ( staticSwitch14 * _Thicnkess ) ) : float3( 0,0,0 ) );
			v.vertex.w = 1;
		}

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float temp_output_1_0_g1 = radians( _Rotation );
			float2 appendResult2_g1 = (float2(cos( temp_output_1_0_g1 ) , sin( temp_output_1_0_g1 )));
			float mulTime24 = _Time.y * _FlowSpeed;
			float2 temp_output_25_0 = ( appendResult2_g1 * mulTime24 );
			float2 uv_TexCoord26 = i.uv_texcoord + temp_output_25_0;
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_screenPos );
			float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
			#ifdef _SCREENSPACE_ON
				float4 staticSwitch42 = ( ( ase_grabScreenPosNorm * _NoiseScale ) + float4( temp_output_25_0, 0.0 , 0.0 ) );
			#else
				float4 staticSwitch42 = float4( uv_TexCoord26, 0.0 , 0.0 );
			#endif
			float gradientNoise28 = GradientNoise(staticSwitch42.xy,_NoiseScale);
			gradientNoise28 = gradientNoise28*0.5 + 0.5;
			float2 temp_cast_3 = (gradientNoise28).xx;
			#ifdef _USEGRADIENT_ON
				float4 staticSwitch33 = ( tex2D( _RampMap, temp_cast_3 ) * _Intesity );
			#else
				float4 staticSwitch33 = _OutlineColor;
			#endif
			o.Emission = staticSwitch33.rgb;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "MaskOutlineEditor"
}
/*ASEBEGIN
Version=18935
916;73;1643;654;909.5715;97.17929;1;True;False
Node;AmplifyShaderEditor.RangedFloatNode;22;-2116.645,-293.1179;Inherit;False;Property;_FlowSpeed;Flow Speed;9;0;Create;False;0;0;0;False;0;False;0.3647059;0.3647059;0;3;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;21;-2075.645,-500.1179;Inherit;False;Property;_Rotation;Flow Rotation;10;0;Create;False;0;0;0;False;0;False;0;0;0;360;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;27;-1214.208,-340.8968;Inherit;False;Property;_NoiseScale;Noise Scale;8;0;Create;False;0;0;0;False;0;False;7.9;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;23;-1726.645,-430.1179;Inherit;False;DegreesToDirection;-1;;1;ef517114e559b014bb611423825a9296;0;1;6;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;24;-1785.645,-319.1179;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GrabScreenPosition;37;-1910.981,-805.4221;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;25;-1488.645,-367.1179;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;43;-1628.947,-774.4477;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;26;-1254.208,-498.8968;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;38;-1415.97,-779.1881;Inherit;True;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.StaticSwitch;42;-1089.641,-727.243;Inherit;False;Property;_ScreenSpace;Screen Space;12;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT4;0,0,0,0;False;0;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;4;FLOAT4;0,0,0,0;False;5;FLOAT4;0,0,0,0;False;6;FLOAT4;0,0,0,0;False;7;FLOAT4;0,0,0,0;False;8;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.PosVertexDataNode;19;-1492.783,346.5561;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexCoordVertexDataNode;20;-1483.783,507.5562;Inherit;False;3;2;0;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NormalVertexDataNode;18;-1456.783,192.5561;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.WorldPosInputsNode;3;-1203.401,-85.58698;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldSpaceCameraPos;2;-1268.401,-232.587;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DistanceOpNode;4;-916.4016,-91.58698;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;6;-1194.93,105.4187;Inherit;False;Property;_AdaptiveThicnkess;Adaptive Thicnkess;0;0;Create;True;0;0;0;False;0;False;0;0.32;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;10;-1067.783,480.5561;Inherit;False;Property;_Thicnkess;Thicnkess;2;0;Create;True;0;0;0;False;0;False;0.01;0;0;0.3;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;14;-1210.783,286.5561;Inherit;False;Property;_OutlineType;Outline Type;3;0;Create;True;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;3;Normal;Position;UVBaked;Create;True;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.NoiseGeneratorNode;28;-982.4963,-446.5008;Inherit;True;Gradient;True;False;2;0;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;30;-533.8046,-265.8548;Inherit;False;Property;_Intesity;Intesity;7;0;Create;False;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;29;-747.7774,-473.7986;Inherit;True;Property;_RampMap;RampMap;5;1;[SingleLineTexture];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;7;-684.8615,49.94713;Inherit;False;3;0;FLOAT;1;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;9;-776.7833,320.5561;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;46;-448.2855,40.45035;Inherit;False;Property;_Enabled;Enabled;13;1;[Toggle];Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;31;-440.8046,-391.8546;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode;34;129.1985,-426.5432;Inherit;False;286;272;Do not delete!;1;36;Editor Variable;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;8;-523.7709,212.6228;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ColorNode;32;-535.8376,-854.8755;Inherit;False;Property;_OutlineColor;Outline Color;4;1;[HDR];Create;False;1;Outline;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;36;175.4669,-271.3881;Inherit;False;Property;_RampMapLoaded;RampMapLoaded;11;1;[HideInInspector];Create;False;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;33;-294.5216,-428.0694;Inherit;False;Property;_UseGradient;Use Gradient;6;0;Create;False;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.Compare;45;-274.2855,147.4503;Inherit;False;0;4;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.IntNode;48;-325.5413,389.1716;Inherit;False;Property;_MaskRef;Stencil Reference;14;0;Create;False;0;0;0;True;0;False;1;0;False;0;1;INT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;-61,-66;Float;False;True;-1;2;MaskOutlineEditor;0;0;Unlit;OnlyOutlineWithMask;False;False;False;False;True;True;True;True;True;True;True;True;False;False;False;False;False;False;False;False;False;Front;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;ForwardOnly;18;all;True;True;True;True;0;False;-1;True;3;True;48;255;False;-1;255;False;-1;6;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;23;6;21;0
WireConnection;24;0;22;0
WireConnection;25;0;23;0
WireConnection;25;1;24;0
WireConnection;43;0;37;0
WireConnection;43;1;27;0
WireConnection;26;1;25;0
WireConnection;38;0;43;0
WireConnection;38;1;25;0
WireConnection;42;1;26;0
WireConnection;42;0;38;0
WireConnection;4;0;2;0
WireConnection;4;1;3;0
WireConnection;14;1;18;0
WireConnection;14;0;19;0
WireConnection;14;2;20;0
WireConnection;28;0;42;0
WireConnection;28;1;27;0
WireConnection;29;1;28;0
WireConnection;7;1;4;0
WireConnection;7;2;6;0
WireConnection;9;0;14;0
WireConnection;9;1;10;0
WireConnection;31;0;29;0
WireConnection;31;1;30;0
WireConnection;8;0;7;0
WireConnection;8;1;9;0
WireConnection;33;1;32;0
WireConnection;33;0;31;0
WireConnection;45;0;46;0
WireConnection;45;2;8;0
WireConnection;0;2;33;0
WireConnection;0;11;45;0
ASEEND*/
//CHKSM=E7847DF4025AA37748553313ADAC2436AD1A471A