// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "OutlineMaskMetallic"
{
	Properties
	{
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
		[HideInInspector]_UseSpecular("UseSpecular", Float) = 0
		_Metallic("Metallic", Range( 0 , 1)) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "AlphaTest+0" "IsEmissive" = "true"  }
		Cull Back
		Stencil
		{
			Ref 3
			Comp Always
			Pass Replace
		}
		CGPROGRAM
		#pragma target 3.0
		#pragma multi_compile _GLOSSSOURCE_ALBEDOALPHA _GLOSSSOURCE_METALLICALPHA
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
		};

		uniform float _Cutoff;
		uniform float _UseSpecular;
		uniform sampler2D _BumpMap;
		uniform sampler2D _MainTex;
		uniform float4 _MainTex_ST;
		uniform float4 _Color;
		uniform float _UseEmission;
		uniform sampler2D _EmissionMap;
		uniform float4 _EmissionColor;
		uniform sampler2D _MetallicGlossMap;
		uniform float _Metallic;
		uniform float _Glossiness;
		uniform sampler2D _OcclusionMap;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float3 TangentNormal97 = UnpackNormal( tex2D( _BumpMap, uv_MainTex ) );
			o.Normal = TangentNormal97;
			float4 tex2DNode7 = tex2D( _MainTex, uv_MainTex );
			float4 AlbedoColor90 = ( tex2DNode7 * _Color );
			o.Albedo = AlbedoColor90.rgb;
			float4 EmissionColor96 = ( _UseEmission == 1.0 ? ( tex2D( _EmissionMap, uv_MainTex ) * _EmissionColor ) : float4( 0,0,0,0 ) );
			o.Emission = EmissionColor96.rgb;
			float4 tex2DNode53 = tex2D( _MetallicGlossMap, uv_MainTex );
			float MetallicValue94 = ( tex2DNode53.r * _Metallic );
			o.Metallic = MetallicValue94;
			#if defined(_GLOSSSOURCE_ALBEDOALPHA)
				float staticSwitch64 = tex2DNode7.a;
			#elif defined(_GLOSSSOURCE_METALLICALPHA)
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
	CustomEditor "BasicShaderEditor"
}
/*ASEBEGIN
Version=18935
916;73;1643;654;-1999.099;702.5521;1.005235;True;False
Node;AmplifyShaderEditor.CommentaryNode;98;2513.396,-285.3597;Inherit;False;1441.691;2060.756;;25;61;94;97;96;95;88;93;54;44;67;71;91;72;42;64;47;90;22;46;53;49;23;7;221;223;Standard Inputs;1,1,1,1;0;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;221;2620.622,-194.4274;Inherit;False;0;7;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;23;2879.239,401.7525;Inherit;False;Property;_Color;Color;1;0;Create;True;0;0;0;False;0;False;1,1,1,1;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;7;2848.627,210.3292;Inherit;True;Property;_MainTex;Albedo;0;2;[NoScaleOffset];[SingleLineTexture];Create;False;1;PBR Settings;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;49;2917.817,1112.985;Inherit;True;Property;_EmissionMap;Emission Texture;5;2;[NoScaleOffset];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;22;3163.637,383.2337;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;46;2963.308,1304.418;Inherit;False;Property;_EmissionColor;Emission Color;6;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;53;2926.939,617.5856;Inherit;True;Property;_MetallicGlossMap;Metallic Texture;3;2;[NoScaleOffset];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.StaticSwitch;64;3273.208,217.8466;Inherit;False;Property;_GlossSource;Source;9;0;Create;False;0;0;0;True;0;False;1;0;0;True;;KeywordEnum;2;AlbedoAlpha;MetallicAlpha;Create;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;42;3229.481,1247.403;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;90;3339.615,380.1566;Inherit;False;AlbedoColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;47;3292.367,98.17715;Inherit;False;Property;_Glossiness;Smoothness Value;4;0;Create;False;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;72;3305.855,1115.494;Inherit;False;Property;_UseEmission;UseEmission;10;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;223;3004.571,858.4777;Inherit;False;Property;_Metallic;Metallic;12;0;Create;False;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;44;2945.552,1523.099;Inherit;True;Property;_BumpMap;Normal Texture;2;3;[NoScaleOffset];[Normal];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;54;2925.023,-220.7324;Inherit;True;Property;_OcclusionMap;Occlusion Map;7;2;[NoScaleOffset];[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;67;3586.947,149.5085;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Compare;71;3463.392,1194.408;Inherit;False;0;4;0;FLOAT;0;False;1;FLOAT;1;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;222;3272.571,714.4777;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;91;3538.411,456.9198;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RegisterLocalVarNode;94;3455.425,676.7227;Inherit;False;MetallicValue;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;219;2573.079,-608.7397;Inherit;False;270;166;Do not delete!;1;220;Editor Variable;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;88;3224.762,-184.5968;Inherit;False;OcclusionMap;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;97;3276.961,1549.14;Inherit;False;TangentNormal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;95;3722.059,198.6727;Inherit;False;SmoothnessValue;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;96;3651.063,1223.381;Inherit;False;EmissionColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;93;3683.411,530.724;Inherit;False;Alpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;220;2623.079,-558.7397;Inherit;False;Property;_UseSpecular;UseSpecular;11;1;[HideInInspector];Create;False;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;106;1717.713,-84.05376;Inherit;False;88;OcclusionMap;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;105;1695.298,-159.6012;Inherit;False;95;SmoothnessValue;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;99;1731.956,-526.0333;Inherit;False;90;AlbedoColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;61;2893.114,17.59496;Inherit;False;Property;_Cutoff;Alpha Clip Threshold;8;0;Create;False;0;0;0;True;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;104;1676.959,-248.0146;Inherit;False;94;MetallicValue;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;103;1697.672,-334.3535;Inherit;False;96;EmissionColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;101;1722.398,38.42839;Inherit;False;93;Alpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;107;1764.756,-430.3322;Inherit;False;97;TangentNormal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;218;2006.266,-468.021;Float;False;True;-1;2;BasicShaderEditor;0;0;Standard;OutlineMaskMetallic;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.5;True;True;0;True;Opaque;;AlphaTest;All;18;all;True;True;True;True;0;False;-1;True;3;False;-1;255;False;-1;255;False;-1;7;False;-1;3;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;True;61;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;7;1;221;0
WireConnection;49;1;221;0
WireConnection;22;0;7;0
WireConnection;22;1;23;0
WireConnection;53;1;221;0
WireConnection;64;1;7;4
WireConnection;64;0;53;4
WireConnection;42;0;49;0
WireConnection;42;1;46;0
WireConnection;90;0;22;0
WireConnection;44;1;221;0
WireConnection;54;1;221;0
WireConnection;67;0;47;0
WireConnection;67;1;64;0
WireConnection;71;0;72;0
WireConnection;71;2;42;0
WireConnection;222;0;53;1
WireConnection;222;1;223;0
WireConnection;91;0;90;0
WireConnection;94;0;222;0
WireConnection;88;0;54;1
WireConnection;97;0;44;0
WireConnection;95;0;67;0
WireConnection;96;0;71;0
WireConnection;93;0;91;3
WireConnection;218;0;99;0
WireConnection;218;1;107;0
WireConnection;218;2;103;0
WireConnection;218;3;104;0
WireConnection;218;4;105;0
WireConnection;218;5;106;0
WireConnection;218;10;101;0
ASEEND*/
//CHKSM=8150DBC00B544C318365582D6C284F93F3550AC6