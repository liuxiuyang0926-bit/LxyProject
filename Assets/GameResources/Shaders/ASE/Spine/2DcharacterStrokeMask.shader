// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "ASE/Sp/2DcharacterStrokeMask"
{
	Properties
	{
		_Noise("Noise", 2D) = "white" {}
		_NoiseRGB("NoiseRGB", Vector) = (1,0,0,0)
		_NoisePannerWarp("NoisePannerWarp", Vector) = (0,0,0,0)
		[ToggleUI]_Float5("世界坐标切换", Float) = 0
		[ToggleUI]_Warp21("Warp*2-1", Float) = 0
		[ToggleUI]_NoiseUWrap("NoiseUWrap", Float) = 0
		[ToggleUI]_NoiseVWrap("NoiseVWrap", Float) = 0
		_MainTex("角色贴图", 2D) = "white" {}
		[HDR]_Color("角色贴图颜色", Color) = (1,1,1,1)
		_Float0("内描边大小", Range( 0 , 0.01)) = 0.05
		_Float9("描边大小倍数", Float) = 1
		[HDR]_Color0("内描边颜色", Color) = (0,0,0,0)
		[HideInInspector]_Cutting_ST("遮罩图UV平铺", Vector) = (1,1,0,0)
		[HideInInspector]_Mask1_ST("遮罩图UV平铺", Vector) = (1,1,0,0)
		[HideInInspector]_Noise_ST("遮罩图UV平铺", Vector) = (1,1,0,0)
		[HideInInspector]_Mask_ST("遮罩图UV平铺", Vector) = (1,1,0,0)
		_Mask("描边遮罩", 2D) = "white" {}
		_MaskRGB("遮罩通道", Vector) = (0,0,0,1)
		_MaskPannerWarp("遮罩图位移和扭曲", Vector) = (0,0,0,0)
		[ToggleUI]_Float2("世界坐标切换", Float) = 0
		[ToggleUI]_MaskUWrap("U重铺", Float) = 0
		[ToggleUI]_MaskVWrap("V重铺", Float) = 0
		_Mask1("高亮遮罩", 2D) = "white" {}
		[HDR]_Color1("遮罩图颜色", Color) = (0,0,0,0)
		_Mask1RGB("遮罩纹理通道", Vector) = (0,0,0,0)
		_TexMin("遮罩纹理对比度负", Range( -2 , 0)) = 0
		_TexMax("遮罩纹理对比度正", Range( 1 , 5)) = 1
		_Mask1PannerWarp("遮罩纹理位移和扭曲", Vector) = (0,0,0,0)
		[ToggleUI]_Float1("世界坐标切换", Float) = 0
		[ToggleUI]_Mask1UWrap("U重铺", Float) = 0
		[ToggleUI]_Mask1VWrap("V重铺", Float) = 0
		_Mask2("Alpha遮罩纹理", 2D) = "white" {}
		_Mask1RGB1("Alpha遮罩纹理通道", Vector) = (0,0,0,1)
		_Mask1PannerWarp1("Alpha遮罩纹理位移和扭曲", Vector) = (0,0,0,0)
		[HideInInspector]_Mask2_ST("Alpha遮罩图UV平铺", Vector) = (1,1,0,0)
		[ToggleUI]_Float10("世界坐标切换", Float) = 0
		[ToggleUI]_Mask1UWrap1("U重铺", Float) = 0
		[ToggleUI]_Mask1VWrap1("V重铺", Float) = 0
		_Cutting("Cutting", 2D) = "white" {}
		_CuttingRGB("CuttingRGB", Vector) = (0,0,0,1)
		_CuttingPannerWarp("CuttingPannerWarp", Vector) = (0,0,0,0)
		[ToggleUI]_Float4("世界坐标切换", Float) = 0
		[ToggleUI]_CuttingUWrap("CuttingUWrap", Float) = 0
		[ToggleUI]_CuttingVWrap("CuttingVWrap", Float) = 0
		_Float3("溶解", Range( -2 , 2)) = 1
		_CuttingStrength("溶解软硬", Range( 0 , 100)) = 0
		[ToggleUI]_StrokeSwitch("溶解描边开关", Float) = 0
		_Strokesize("溶解描边大小", Float) = 0
		[HDR]_StrokeColor("溶解描边颜色", Color) = (0,0,0,1)
		_Normalmap("光照法线贴图", 2D) = "bump" {}
		_Float6("法线强度", Float) = 1
		[HDR]_Color2("光照颜色", Color) = (0,0,0,0)
		_Vector1("光照方向", Vector) = (0,0,0,0)
		_Vector2("光照位置", Vector) = (0,0,0,0)
		_Float7("光照大小", Float) = 1
		_Float8("光照强度", Float) = 1
		[ToggleUI]_dimianzhezhaokaiguan("地面遮罩开关", Float) = 0
		_dimianzhezhaoqweizhi("地面遮罩位置", Float) = 1
		_dimanzhezhaoruanying("地面遮罩软硬", Range( 0 , 2)) = 0
		_postion("_postion", Vector) = (0,0,0,0)
		_black("_black", Range( 0 , 1)) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		//-------------------add----------------------
		_Margin("Margin", Vector) = (-1000,1000,-1000,1000)
		//-------------------add----------------------
		_StencilRef("Stencil Reference", Float) = 1.0
		[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comparison", Float) = 8 // Set to Always as default

	}

	SubShader
	{
		

		Tags { "RenderType"="Opaque" "Queue"="Transparent" }
	LOD 100

		CGINCLUDE
		#pragma target 3.0
		ENDCG
		Blend SrcAlpha OneMinusSrcAlpha
		AlphaToMask Off
		Cull Off
		ColorMask RGBA
		ZWrite Off
		ZTest LEqual
		Offset 0 , 0
		
		Stencil {
			Ref[_StencilRef]
			Comp[_StencilComp]
			Pass Keep
		}

		
		Pass
		{
			Name "Unlit"

			CGPROGRAM

			#define ASE_VERSION 19801


			#ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
			//only defining to not throw compilation error over Unity 5.5
			#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
			#endif
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			#include "UnityShaderVariables.cginc"
			#include "UnityStandardUtils.cginc"
			#define ASE_NEEDS_FRAG_WORLD_POSITION


			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_tangent : TANGENT;
				float3 ase_normal : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 worldPos : TEXCOORD0;
				#endif
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				//-------------------add----------------------
				float3 vpos : TEXCOORD5;
				//-------------------add----------------------S
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			uniform sampler2D _Mask1;
			uniform float4 _Mask1_ST;
			uniform float _Float1;
			uniform float4 _Mask1PannerWarp;
			uniform sampler2D _Noise;
			uniform float4 _Noise_ST;
			uniform float _Float5;
			uniform float4 _NoisePannerWarp;
			uniform float _NoiseUWrap;
			uniform float _NoiseVWrap;
			uniform float4 _NoiseRGB;
			uniform float _Warp21;
			uniform float _Mask1UWrap;
			uniform float _Mask1VWrap;
			uniform float4 _Mask1RGB;
			uniform float _TexMax;
			uniform float _TexMin;
			uniform float4 _Color1;
			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;
			uniform float4 _Color;
			uniform float _Float0;
			uniform float _Float9;
			uniform float4 _Color0;
			uniform sampler2D _Mask;
			uniform float4 _Mask_ST;
			uniform float _Float2;
			uniform float4 _MaskPannerWarp;
			uniform float _MaskUWrap;
			uniform float _MaskVWrap;
			uniform float4 _MaskRGB;
			uniform float4 _StrokeColor;
			uniform sampler2D _Cutting;
			uniform float4 _Cutting_ST;
			uniform float _Float4;
			uniform float4 _CuttingPannerWarp;
			uniform float _CuttingUWrap;
			uniform float _CuttingVWrap;
			uniform float4 _CuttingRGB;
			uniform float _CuttingStrength;
			uniform float _Float3;
			uniform float _Strokesize;
			uniform float _StrokeSwitch;
			uniform float4 _Color2;
			uniform sampler2D _Normalmap;
			uniform float4 _Normalmap_ST;
			uniform float _Float6;
			uniform float4 _Vector1;
			uniform float3 _Vector2;
			uniform float _Float7;
			uniform float _Float8;
			uniform float _black;
			uniform sampler2D _Mask2;
			uniform float4 _Mask2_ST;
			uniform float _Float10;
			uniform float4 _Mask1PannerWarp1;
			uniform float _Mask1UWrap1;
			uniform float _Mask1VWrap1;
			uniform float4 _Mask1RGB1;
			uniform float _dimianzhezhaoqweizhi;
			uniform float _dimanzhezhaoruanying;
			uniform float4 _postion;
			uniform float _dimianzhezhaokaiguan;

			//-------------------add----------------------
			float4 _Margin;
			//-------------------add----------------------
			v2f vert ( appdata v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				float3 ase_tangentWS = UnityObjectToWorldDir( v.ase_tangent );
				o.ase_texcoord2.xyz = ase_tangentWS;
				float3 ase_normalWS = UnityObjectToWorldNormal( v.ase_normal );
				o.ase_texcoord3.xyz = ase_normalWS;
				float ase_tangentSign = v.ase_tangent.w * ( unity_WorldTransformParams.w >= 0.0 ? 1.0 : -1.0 );
				float3 ase_bitangentWS = cross( ase_normalWS, ase_tangentWS ) * ase_tangentSign;
				o.ase_texcoord4.xyz = ase_bitangentWS;
				
				o.ase_texcoord1.xy = v.ase_texcoord.xy;
				//-------------------add----------------------
				o.vpos = mul(unity_ObjectToWorld, v.vertex).xyz;
				//-------------------add----------------------
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord1.zw = 0;
				o.ase_texcoord2.w = 0;
				o.ase_texcoord3.w = 0;
				o.ase_texcoord4.w = 0;
				float3 vertexValue = float3(0, 0, 0);
				#if ASE_ABSOLUTE_VERTEX_POS
				vertexValue = v.vertex.xyz;
				#endif
				vertexValue = vertexValue;
				#if ASE_ABSOLUTE_VERTEX_POS
				v.vertex.xyz = vertexValue;
				#else
				v.vertex.xyz += vertexValue;
				#endif
				o.vertex = UnityObjectToClipPos(v.vertex);

				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				#endif
				return o;
			}

			fixed4 frag (v2f i ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				fixed4 finalColor;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 WorldPosition = i.worldPos;
				#endif
				float2 uv_Mask1 = i.ase_texcoord1.xy * _Mask1_ST.xy + _Mask1_ST.zw;
				float3 objToWorld599 = mul( unity_ObjectToWorld, float4( float3( 0,0,0 ), 1 ) ).xyz;
				float3 temp_output_492_0 = ( WorldPosition - objToWorld599 );
				float3 ase_parentObjectScale = ( 1.0 / float3( length( unity_WorldToObject[ 0 ].xyz ), length( unity_WorldToObject[ 1 ].xyz ), length( unity_WorldToObject[ 2 ].xyz ) ) );
				float3 worldposition345 = ( temp_output_492_0 / ase_parentObjectScale );
				float2 appendResult369 = (float2(_Mask1_ST.x , _Mask1_ST.y));
				float2 appendResult368 = (float2(_Mask1_ST.z , _Mask1_ST.w));
				float3 lerpResult421 = lerp( float3( uv_Mask1 ,  0.0 ) , (worldposition345*float3( appendResult369 ,  0.0 ) + float3( appendResult368 ,  0.0 )) , _Float1);
				float4 break30_g64 = _Mask1PannerWarp;
				float2 appendResult8_g64 = (float2(break30_g64.x , break30_g64.y));
				float Time192 = _Time.y;
				float2 appendResult9_g64 = (float2(break30_g64.z , break30_g64.w));
				float2 uv_Noise = i.ase_texcoord1.xy * _Noise_ST.xy + _Noise_ST.zw;
				float2 appendResult364 = (float2(_Noise_ST.x , _Noise_ST.y));
				float2 appendResult363 = (float2(_Noise_ST.z , _Noise_ST.w));
				float3 lerpResult434 = lerp( float3( uv_Noise ,  0.0 ) , (worldposition345*float3( appendResult364 ,  0.0 ) + float3( appendResult363 ,  0.0 )) , _Float5);
				float4 break30_g57 = _NoisePannerWarp;
				float2 appendResult8_g57 = (float2(break30_g57.x , break30_g57.y));
				float2 appendResult9_g57 = (float2(break30_g57.z , break30_g57.w));
				float2 temp_output_17_0_g57 = ( lerpResult434.xy + ( appendResult8_g57 * Time192 ) + ( appendResult9_g57 * float2( 0,0 ) ) + ( 0.0 * 0.0 ) + ( float2( 0,0 ) * 0.0 ) );
				float2 appendResult36_g57 = (float2(_NoiseUWrap , _NoiseVWrap));
				float2 lerpResult33_g57 = lerp( frac( temp_output_17_0_g57 ) , temp_output_17_0_g57 , appendResult36_g57);
				float4 break6_g58 = tex2D( _Noise, lerpResult33_g57 );
				float4 break9_g58 = _NoiseRGB;
				float temp_output_141_0 = saturate( ( ( break6_g58.x * break9_g58.x ) + ( break6_g58.y * break9_g58.y ) + ( break6_g58.z * break9_g58.z ) + break9_g58.w ) );
				float lerpResult169 = lerp( temp_output_141_0 , (temp_output_141_0*2.0 + -1.0) , _Warp21);
				float Noise150 = lerpResult169;
				float2 temp_cast_9 = (Noise150).xx;
				float2 temp_output_17_0_g64 = ( lerpResult421.xy + ( appendResult8_g64 * Time192 ) + ( appendResult9_g64 * temp_cast_9 ) + ( 0.0 * 0.0 ) + ( float2( 0,0 ) * 0.0 ) );
				float2 appendResult36_g64 = (float2(_Mask1UWrap , _Mask1VWrap));
				float2 lerpResult33_g64 = lerp( frac( temp_output_17_0_g64 ) , temp_output_17_0_g64 , appendResult36_g64);
				float4 break6_g66 = tex2D( _Mask1, lerpResult33_g64 );
				float4 break9_g66 = _Mask1RGB;
				float Mask1136 = saturate( (saturate( ( ( break6_g66.x * break9_g66.x ) + ( break6_g66.y * break9_g66.y ) + ( break6_g66.z * break9_g66.z ) + break9_g66.w ) )*_TexMax + _TexMin) );
				float2 uv_MainTex = i.ase_texcoord1.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float4 tex2DNode273 = tex2D( _MainTex, uv_MainTex );
				float4 temp_output_600_0 = ( ( Mask1136 * _Color1 ) + ( (tex2DNode273).rgba * _Color ) );
				float temp_output_514_0 = ( _Float0 * _Float9 );
				float2 appendResult301 = (float2(0.0 , temp_output_514_0));
				float2 appendResult302 = (float2(0.0 , -temp_output_514_0));
				float2 appendResult303 = (float2(temp_output_514_0 , 0.0));
				float2 appendResult304 = (float2(-temp_output_514_0 , 0.0));
				float2 uv_Mask = i.ase_texcoord1.xy * _Mask_ST.xy + _Mask_ST.zw;
				float2 appendResult357 = (float2(_Mask_ST.x , _Mask_ST.y));
				float2 appendResult355 = (float2(_Mask_ST.z , _Mask_ST.w));
				float3 lerpResult423 = lerp( float3( uv_Mask ,  0.0 ) , (worldposition345*float3( appendResult357 ,  0.0 ) + float3( appendResult355 ,  0.0 )) , _Float2);
				float4 break30_g67 = _MaskPannerWarp;
				float2 appendResult8_g67 = (float2(break30_g67.x , break30_g67.y));
				float2 appendResult9_g67 = (float2(break30_g67.z , break30_g67.w));
				float2 temp_cast_15 = (Noise150).xx;
				float2 temp_output_17_0_g67 = ( lerpResult423.xy + ( appendResult8_g67 * Time192 ) + ( appendResult9_g67 * temp_cast_15 ) + ( 0.0 * 0.0 ) + ( float2( 0,0 ) * 0.0 ) );
				float2 appendResult36_g67 = (float2(_MaskUWrap , _MaskVWrap));
				float2 lerpResult33_g67 = lerp( frac( temp_output_17_0_g67 ) , temp_output_17_0_g67 , appendResult36_g67);
				float4 break6_g69 = tex2D( _Mask, lerpResult33_g67 );
				float4 break9_g69 = _MaskRGB;
				float Mask110 = saturate( ( ( break6_g69.x * break9_g69.x ) + ( break6_g69.y * break9_g69.y ) + ( break6_g69.z * break9_g69.z ) + break9_g69.w ) );
				float4 temp_output_292_0 = ( temp_output_600_0 + ( saturate( ( ( 1.0 - tex2DNode273.a ) + ( 1.0 - tex2D( _MainTex, ( uv_MainTex + appendResult301 ) ).a ) + ( 1.0 - tex2D( _MainTex, ( uv_MainTex + appendResult302 ) ).a ) + ( 1.0 - tex2D( _MainTex, ( uv_MainTex + appendResult303 ) ).a ) + ( 1.0 - tex2D( _MainTex, ( uv_MainTex + appendResult304 ) ).a ) ) ) * _Color0 * Mask110 ) );
				float2 uv_Cutting = i.ase_texcoord1.xy * _Cutting_ST.xy + _Cutting_ST.zw;
				float2 appendResult428 = (float2(_Cutting_ST.x , _Cutting_ST.y));
				float2 appendResult431 = (float2(_Cutting_ST.z , _Cutting_ST.w));
				float3 lerpResult427 = lerp( float3( uv_Cutting ,  0.0 ) , (worldposition345*float3( appendResult428 ,  0.0 ) + float3( appendResult431 ,  0.0 )) , _Float4);
				float4 break30_g65 = _CuttingPannerWarp;
				float2 appendResult8_g65 = (float2(break30_g65.x , break30_g65.y));
				float2 appendResult9_g65 = (float2(break30_g65.z , break30_g65.w));
				float2 temp_cast_21 = (Noise150).xx;
				float2 temp_output_17_0_g65 = ( lerpResult427.xy + ( appendResult8_g65 * Time192 ) + ( appendResult9_g65 * temp_cast_21 ) + ( 0.0 * 0.0 ) + ( float2( 0,0 ) * 0.0 ) );
				float2 appendResult36_g65 = (float2(_CuttingUWrap , _CuttingVWrap));
				float2 lerpResult33_g65 = lerp( frac( temp_output_17_0_g65 ) , temp_output_17_0_g65 , appendResult36_g65);
				float4 break6_g68 = tex2D( _Cutting, lerpResult33_g65 );
				float4 break9_g68 = _CuttingRGB;
				float temp_output_177_0 = ( saturate( ( ( break6_g68.x * break9_g68.x ) + ( break6_g68.y * break9_g68.y ) + ( break6_g68.z * break9_g68.z ) + break9_g68.w ) ) * _CuttingStrength );
				float lerpResult200 = lerp( _CuttingStrength , -1.5 , ( _Float3 - _Strokesize ));
				float Stroke203 = saturate( ( temp_output_177_0 - lerpResult200 ) );
				float4 lerpResult207 = lerp( _StrokeColor , temp_output_292_0 , Stroke203);
				float4 lerpResult208 = lerp( temp_output_292_0 , lerpResult207 , _StrokeSwitch);
				float3 temp_output_124_0 = (lerpResult208).rgb;
				float2 uv_Normalmap = i.ase_texcoord1.xy * _Normalmap_ST.xy + _Normalmap_ST.zw;
				float3 ase_tangentWS = i.ase_texcoord2.xyz;
				float3 ase_normalWS = i.ase_texcoord3.xyz;
				float3 ase_bitangentWS = i.ase_texcoord4.xyz;
				float3 tanToWorld0 = float3( ase_tangentWS.x, ase_bitangentWS.x, ase_normalWS.x );
				float3 tanToWorld1 = float3( ase_tangentWS.y, ase_bitangentWS.y, ase_normalWS.y );
				float3 tanToWorld2 = float3( ase_tangentWS.z, ase_bitangentWS.z, ase_normalWS.z );
				float3 tanNormal471 = UnpackScaleNormal( tex2D( _Normalmap, uv_Normalmap ), _Float6 );
				float3 worldNormal471 = float3( dot( tanToWorld0, tanNormal471 ), dot( tanToWorld1, tanNormal471 ), dot( tanToWorld2, tanNormal471 ) );
				float dotResult472 = dot( float4( worldNormal471 , 0.0 ) , _Vector1 );
				float3 temp_output_513_0 = ( ( temp_output_492_0 - _Vector2 ) / _Float7 );
				float dotResult501 = dot( temp_output_513_0 , temp_output_513_0 );
				float temp_output_477_0 = saturate( ( (dotResult472*0.49 + 0.5) * saturate( ( dotResult501 * _Float8 ) ) ) );
				float4 lerpResult495 = lerp( float4( temp_output_124_0 , 0.0 ) , ( float4( temp_output_124_0 , 0.0 ) + ( _Color2 * temp_output_477_0 ) ) , temp_output_477_0);
				float lerpResult180 = lerp( _CuttingStrength , -1.5 , _Float3);
				float Cutting167 = saturate( ( temp_output_177_0 - lerpResult180 ) );
				float2 uv_Mask2 = i.ase_texcoord1.xy * _Mask2_ST.xy + _Mask2_ST.zw;
				float2 appendResult576 = (float2(_Mask2_ST.x , _Mask2_ST.y));
				float2 appendResult579 = (float2(_Mask2_ST.z , _Mask2_ST.w));
				float3 lerpResult565 = lerp( float3( uv_Mask2 ,  0.0 ) , (worldposition345*float3( appendResult576 ,  0.0 ) + float3( appendResult579 ,  0.0 )) , _Float10);
				float4 break30_g70 = _Mask1PannerWarp1;
				float2 appendResult8_g70 = (float2(break30_g70.x , break30_g70.y));
				float2 appendResult9_g70 = (float2(break30_g70.z , break30_g70.w));
				float2 temp_cast_31 = (Noise150).xx;
				float2 temp_output_17_0_g70 = ( lerpResult565.xy + ( appendResult8_g70 * Time192 ) + ( appendResult9_g70 * temp_cast_31 ) + ( 0.0 * 0.0 ) + ( float2( 0,0 ) * 0.0 ) );
				float2 appendResult36_g70 = (float2(_Mask1UWrap1 , _Mask1VWrap1));
				float2 lerpResult33_g70 = lerp( frac( temp_output_17_0_g70 ) , temp_output_17_0_g70 , appendResult36_g70);
				float4 break6_g71 = tex2D( _Mask2, lerpResult33_g70 );
				float4 break9_g71 = _Mask1RGB1;
				float Mask2574 = saturate( saturate( ( ( break6_g71.x * break9_g71.x ) + ( break6_g71.y * break9_g71.y ) + ( break6_g71.z * break9_g71.z ) + break9_g71.w ) ) );
				float3 World601 = WorldPosition;
				float smoothstepResult547 = smoothstep( _dimianzhezhaoqweizhi , ( _dimianzhezhaoqweizhi + _dimanzhezhaoruanying ) , ( World601.y - _postion.y ));
				float lerpResult535 = lerp( 1.0 , saturate( smoothstepResult547 ) , _dimianzhezhaokaiguan);
				float4 appendResult17 = (float4(( lerpResult495 * _black ).rgb , ( tex2DNode273.a * Cutting167 * Mask2574 * lerpResult535 * _Color.a )));
				float4 Tex29 = appendResult17;
				

				finalColor = Tex29;
				//-------------------add----------------------		
				finalColor.a *= step(_Margin.x, i.vpos.x);
				finalColor.a *= step(i.vpos.x, _Margin.y);
				finalColor.a *= step(_Margin.z, i.vpos.y);
				finalColor.a *= step(i.vpos.y, _Margin.w);
				//-------------------add----------------------
				return finalColor;
			}
			ENDCG
		}
	}
	CustomEditor "AmplifyShaderEditor.MaterialInspector"
	
	Fallback Off
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.WorldPosInputsNode;480;-2479.312,-472.6804;Float;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.TransformPositionNode;599;-2486.624,-224.4484;Inherit;False;Object;World;False;Fast;True;1;0;FLOAT3;0,0,0;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;492;-2169.763,-292.021;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ObjectScaleNode;586;-2157.625,-93.65015;Inherit;False;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.CommentaryNode;140;-8997.039,310.9295;Inherit;False;3234.779;926.997;;20;365;366;364;363;362;150;146;225;148;151;248;144;197;170;169;168;149;141;434;435;Noise;0.1316573,0,0.3773585,1;0;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;592;-1786.697,-126.8785;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;191;-6456.921,-620.2189;Inherit;False;515.3335;181;;2;192;190;Time;1,1,1,1;0;0
Node;AmplifyShaderEditor.Vector4Node;366;-8868.796,505.3617;Inherit;False;Property;_Noise_ST;遮罩图UV平铺;14;1;[HideInInspector];Create;False;0;0;0;False;0;False;1,1,0,0;1,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;345;-1552.254,-127.51;Inherit;False;worldposition;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleTimeNode;190;-6406.921,-570.2189;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;363;-8627.473,595.3617;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;364;-8624.473,501.3617;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;365;-8637.679,408.3687;Inherit;False;345;worldposition;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;192;-6152.815,-572.4221;Inherit;False;Time;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;362;-8434.473,480.3617;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;1,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;144;-8397.256,618.1446;Inherit;False;0;151;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;435;-8331.015,785.8608;Inherit;False;Property;_Float5;世界坐标切换;3;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;197;-7922.024,350.6461;Inherit;False;192;Time;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;225;-7830.872,1132.515;Inherit;False;Property;_NoiseVWrap;NoiseVWrap;6;1;[ToggleUI];Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;146;-7969.633,709.513;Inherit;False;Property;_NoisePannerWarp;NoisePannerWarp;2;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;148;-7838.212,1034.497;Inherit;False;Property;_NoiseUWrap;NoiseUWrap;5;1;[ToggleUI];Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;434;-8034.015,541.8608;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.FunctionNode;248;-7545.427,612.2875;Inherit;False;UV Map;-1;;57;10c682b19dfe6fc45b2e14b1b2f0cbc0;0;10;35;FLOAT;0;False;7;FLOAT2;0,0;False;10;FLOAT4;0,0,0,0;False;15;FLOAT2;0,0;False;23;FLOAT;0;False;24;FLOAT;0;False;26;FLOAT2;0,0;False;25;FLOAT;0;False;31;FLOAT;0;False;37;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector4Node;149;-7124.781,818.1052;Inherit;False;Property;_NoiseRGB;NoiseRGB;1;0;Create;True;0;0;0;False;0;False;1,0,0,0;1,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;151;-7186.382,590.6794;Inherit;True;Property;_Noise;Noise;0;0;Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.CommentaryNode;126;-5317.886,732.8342;Inherit;False;3397.797;908.6277;;22;136;249;255;256;258;254;138;422;128;127;421;133;154;372;367;368;371;370;369;221;135;195;Mask1;0.5019608,0.6651372,1,1;0;0
Node;AmplifyShaderEditor.FunctionNode;141;-6775.541,729.5361;Inherit;False;Tex RGBA;-1;;58;97b553afc31c6594d92091a1f04b3f60;0;2;3;FLOAT4;0,0,0,0;False;10;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;168;-6550.833,817.4017;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;2;False;2;FLOAT;-1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;170;-6511.833,962.4017;Inherit;False;Property;_Warp21;Warp*2-1;4;1;[ToggleUI];Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;370;-4856.885,909.5265;Inherit;False;Property;_Mask1_ST;遮罩图UV平铺;13;1;[HideInInspector];Create;False;0;0;0;False;0;False;1,1,0,0;1,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;369;-4612.561,905.5264;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;368;-4611.561,1007.527;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;371;-4625.767,812.5341;Inherit;False;345;worldposition;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;169;-6250.833,740.4017;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;118;-8823.717,-1796.626;Inherit;False;2995.363;991.5475;;18;110;356;358;357;355;354;222;76;108;247;119;194;152;122;109;423;424;425;Mask;0.5,0.8053703,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;155;-6325.879,2772.865;Inherit;False;3947.271;1049.698;;22;165;167;224;246;164;157;196;178;182;179;177;180;161;156;158;160;426;427;428;431;432;433;Cutting;0,0.1803922,0.1589706,1;0;0
Node;AmplifyShaderEditor.Vector4Node;429;-6567.878,2945.153;Inherit;False;Property;_Cutting_ST;遮罩图UV平铺;12;1;[HideInInspector];Create;False;0;0;0;False;0;False;1,1,0,0;1,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;372;-4429.45,1122.711;Inherit;False;0;127;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ScaleAndOffsetNode;367;-4382.714,838.8624;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;1,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;422;-4458.076,1251.96;Inherit;False;Property;_Float1;世界坐标切换;28;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;150;-5986.092,752.1037;Inherit;False;Noise;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;356;-8318.016,-1577.119;Inherit;False;Property;_Mask_ST;遮罩图UV平铺;15;1;[HideInInspector];Create;False;0;0;0;False;0;False;1,1,0,0;1,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;428;-6323.555,2941.153;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;431;-6322.555,3043.153;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;298;-5773.458,-1502.468;Inherit;False;Property;_Float0;内描边大小;9;0;Create;False;0;0;0;False;0;False;0.05;0.01;0;0.01;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;516;-5694.303,-1381.885;Inherit;False;Property;_Float9;描边大小倍数;10;0;Create;False;0;0;0;False;0;False;1;0.8;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;195;-3932.345,874.0018;Inherit;False;192;Time;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;135;-4113.807,1444.745;Inherit;False;Property;_Mask1UWrap;U重铺;29;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;221;-4112.412,1516.234;Inherit;False;Property;_Mask1VWrap;V重铺;30;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;154;-4134.555,1351.644;Inherit;False;150;Noise;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;133;-4160.6,1183.086;Inherit;False;Property;_Mask1PannerWarp;遮罩纹理位移和扭曲;27;0;Create;False;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;421;-4047.075,958.9602;Inherit;True;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;430;-6336.761,2848.16;Inherit;False;345;worldposition;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;355;-8076.694,-1487.119;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;357;-8073.694,-1581.119;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;432;-6093.708,2874.489;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;1,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;157;-6019.297,3010.831;Inherit;False;0;160;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;433;-5993.891,3161.421;Inherit;False;Property;_Float4;世界坐标切换;41;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;514;-5443.348,-1486.343;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;10;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;249;-3675.13,1076.051;Inherit;False;UV Map;-1;;64;10c682b19dfe6fc45b2e14b1b2f0cbc0;0;10;35;FLOAT;0;False;7;FLOAT2;0,0;False;10;FLOAT4;0,0,0,0;False;15;FLOAT2;0,0;False;23;FLOAT;0;False;24;FLOAT;0;False;26;FLOAT2;0,0;False;25;FLOAT;0;False;31;FLOAT;0;False;37;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;358;-8086.9,-1674.112;Inherit;False;345;worldposition;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.NegateNode;300;-5229.231,-1462.067;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;354;-7883.694,-1602.119;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;1,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;425;-8026.318,-1382.092;Inherit;False;0;76;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;424;-7770.276,-1321.159;Inherit;False;Property;_Float2;世界坐标切换;19;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;158;-5797.935,3177.671;Inherit;False;Property;_CuttingPannerWarp;CuttingPannerWarp;40;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;165;-5756.703,3371.794;Inherit;False;150;Noise;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;164;-5766.928,3500.75;Inherit;False;Property;_CuttingUWrap;CuttingUWrap;42;1;[ToggleUI];Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;224;-5753.194,3582.752;Inherit;False;Property;_CuttingVWrap;CuttingVWrap;43;1;[ToggleUI];Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;427;-5649.242,2998.704;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector4Node;138;-3258.176,1330.494;Inherit;False;Property;_Mask1RGB;遮罩纹理通道;24;0;Create;False;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;196;-5825.449,2865.255;Inherit;False;192;Time;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;127;-3343.39,1063.577;Inherit;True;Property;_Mask1;高亮遮罩;22;0;Create;False;0;0;0;False;0;False;-1;None;66ec5b465575fcf42a06d9b4b25825ae;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.TexturePropertyNode;259;-5883.967,-2585.276;Inherit;True;Property;_MainTex;角色贴图;7;0;Create;False;0;0;0;False;0;False;None;8b406e04a360f33489060662b0488992;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.CommentaryNode;198;-4205.921,3652.423;Inherit;False;1851.631;456.457;;6;203;202;201;200;199;230;溶解描边;0,0.03822114,0.1509434,1;0;0
Node;AmplifyShaderEditor.DynamicAppendNode;304;-5022.521,-1484.313;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;302;-5096.014,-1306.689;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;303;-5153.15,-1175.602;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;301;-5153.683,-1617.659;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;194;-7421.761,-1717.313;Inherit;False;192;Time;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;152;-7569.434,-1116.674;Inherit;False;150;Noise;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;122;-7598.267,-1282.99;Inherit;False;Property;_MaskPannerWarp;遮罩图位移和扭曲;18;0;Create;False;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;423;-7461.114,-1487.441;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;119;-7564.475,-1037.202;Inherit;False;Property;_MaskUWrap;U重铺;20;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;222;-7562.851,-961.9051;Inherit;False;Property;_MaskVWrap;V重铺;21;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;246;-5213.767,3110.085;Inherit;False;UV Map;-1;;65;10c682b19dfe6fc45b2e14b1b2f0cbc0;0;10;35;FLOAT;0;False;7;FLOAT2;0,0;False;10;FLOAT4;0,0,0,0;False;15;FLOAT2;0,0;False;23;FLOAT;0;False;24;FLOAT;0;False;26;FLOAT2;0,0;False;25;FLOAT;0;False;31;FLOAT;0;False;37;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;128;-2954.181,1081.896;Inherit;False;Tex RGBA;-1;;66;97b553afc31c6594d92091a1f04b3f60;0;2;3;FLOAT4;0,0,0,0;False;10;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;255;-2953.942,1268.978;Inherit;False;Property;_TexMax;遮罩纹理对比度正;26;0;Create;False;0;0;0;False;0;False;1;1;1;5;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;47;-5510.971,-1901.211;Inherit;False;0;259;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;263;-5644.911,-2579.561;Inherit;False;Main;-1;True;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.RangedFloatNode;256;-2934.108,1355.457;Inherit;False;Property;_TexMin;遮罩纹理对比度负;25;0;Create;False;0;0;0;False;0;False;0;0;-2;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;268;-4881.435,-1724.091;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector4Node;161;-4675.928,3272.378;Inherit;False;Property;_CuttingRGB;CuttingRGB;39;0;Create;True;0;0;0;False;0;False;0,0,0,1;0,0,0,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;278;-4848.009,-1325.803;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;275;-4829.45,-1451.531;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;282;-4814.609,-1114.124;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;247;-7186.572,-1316.313;Inherit;False;UV Map;-1;;67;10c682b19dfe6fc45b2e14b1b2f0cbc0;0;10;35;FLOAT;0;False;7;FLOAT2;0,0;False;10;FLOAT4;0,0,0,0;False;15;FLOAT2;0,0;False;23;FLOAT;0;False;24;FLOAT;0;False;26;FLOAT2;0,0;False;25;FLOAT;0;False;31;FLOAT;0;False;37;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;199;-4126.175,3917.979;Inherit;False;Property;_Strokesize;溶解描边大小;47;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;426;-4592.712,3634.72;Inherit;False;Property;_Float3;溶解;44;0;Create;False;0;0;0;False;0;False;1;1;-2;2;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;160;-4757.457,3057.668;Inherit;True;Property;_Cutting;Cutting;38;0;Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ScaleAndOffsetNode;254;-2599.476,1212.183;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;264;-5488.891,-2125.569;Inherit;False;263;Main;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.CommentaryNode;559;-5104.464,1769.079;Inherit;False;3397.797;908.6277;;19;581;580;579;578;577;576;575;574;573;570;568;567;566;565;564;563;562;561;560;Mask1;0.5019608,0.6651372,1,1;0;0
Node;AmplifyShaderEditor.SamplerNode;270;-4681.34,-1702.554;Inherit;True;Property;_boss_faxiang;boss_faxiang;44;0;Create;True;0;0;0;False;0;False;-1;ebfdeb21e267b814683cf515d54fbdcb;ebfdeb21e267b814683cf515d54fbdcb;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;274;-4674.228,-1492.817;Inherit;True;Property;_boss_faxiang2;boss_faxiang;44;0;Create;True;0;0;0;False;0;False;-1;ebfdeb21e267b814683cf515d54fbdcb;ebfdeb21e267b814683cf515d54fbdcb;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;277;-4672.144,-1304.792;Inherit;True;Property;_boss_faxiang3;boss_faxiang;44;0;Create;True;0;0;0;False;0;False;-1;ebfdeb21e267b814683cf515d54fbdcb;ebfdeb21e267b814683cf515d54fbdcb;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;281;-4663.761,-1112.797;Inherit;True;Property;_boss_faxiang4;boss_faxiang;44;0;Create;True;0;0;0;False;0;False;-1;ebfdeb21e267b814683cf515d54fbdcb;ebfdeb21e267b814683cf515d54fbdcb;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.FunctionNode;156;-4366.134,3162.793;Inherit;False;Tex RGBA;-1;;68;97b553afc31c6594d92091a1f04b3f60;0;2;3;FLOAT4;0,0,0,0;False;10;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;230;-3910.4,3905.718;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;109;-6635.589,-1101.01;Inherit;False;Property;_MaskRGB;遮罩通道;17;0;Create;False;0;0;0;False;0;False;0,0,0,1;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;178;-4313.327,3322.792;Inherit;False;Property;_CuttingStrength;溶解软硬;45;0;Create;False;0;0;0;False;0;False;0;0;0;100;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;500;-1202.936,-146.6844;Inherit;False;Property;_Vector2;光照位置;53;0;Create;False;0;0;0;False;0;False;0,0,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SamplerNode;273;-4715.673,-2154.543;Inherit;True;Property;_boss_faxiang1;boss_faxiang;44;0;Create;True;0;0;0;False;0;False;-1;ebfdeb21e267b814683cf515d54fbdcb;ebfdeb21e267b814683cf515d54fbdcb;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SaturateNode;258;-2371.136,1213.305;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;76;-6707.954,-1336.336;Inherit;True;Property;_Mask;描边遮罩;16;0;Create;False;0;0;0;False;0;False;-1;None;8b406e04a360f33489060662b0488992;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.Vector4Node;577;-4695.463,1945.772;Inherit;False;Property;_Mask2_ST;Alpha遮罩图UV平铺;34;1;[HideInInspector];Create;False;0;0;0;False;0;False;1,1,0,0;1,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;320;-4317.701,-993.1583;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;319;-4325.142,-1192.09;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;318;-4328.401,-1402.636;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;317;-4320.851,-1591.213;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;177;-3899.201,3186.294;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;200;-3733.74,3873.679;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;-1.5;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;108;-6321.58,-1242.55;Inherit;False;Tex RGBA;-1;;69;97b553afc31c6594d92091a1f04b3f60;0;2;3;FLOAT4;0,0,0,0;False;10;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;323;-4317.56,-1805.195;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;469;-1474.981,-861.6036;Inherit;False;Property;_Float6;法线强度;50;0;Create;False;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;510;-946.1256,41.37482;Inherit;False;Property;_Float7;光照大小;54;0;Create;False;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;499;-984.3678,-267.9254;Inherit;True;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;136;-2210.907,1207.77;Inherit;False;Mask1;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;576;-4399.139,1941.772;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;579;-4398.139,2043.773;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;578;-4412.345,1848.779;Inherit;False;345;worldposition;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;321;-4010.082,-1629.355;Inherit;True;5;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;201;-3544.503,3859.634;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;295;-4358.469,-2286.562;Inherit;False;True;True;True;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;513;-738.0823,-255.6077;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;468;-1302.145,-913.4159;Inherit;True;Property;_Normalmap;光照法线贴图;49;0;Create;False;0;0;0;False;0;False;-1;None;None;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RegisterLocalVarNode;110;-6104.686,-1230.86;Inherit;False;Mask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;139;-4151.107,-2781.421;Inherit;False;136;Mask1;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;361;-4163.366,-2663.264;Inherit;False;Property;_Color1;遮罩图颜色;23;1;[HDR];Create;False;0;0;0;False;0;False;0,0,0,0;2.576744,1.669212,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode;556;-3991.225,-2201.953;Inherit;False;Property;_Color;角色贴图颜色;8;1;[HDR];Create;False;0;0;0;False;0;False;1,1,1,1;1,1,1,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ScaleAndOffsetNode;580;-4169.292,1875.108;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;1,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;581;-4244.654,2288.207;Inherit;False;Property;_Float10;世界坐标切换;35;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;575;-4216.028,2158.958;Inherit;False;0;566;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;329;-3763.503,-1601.026;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;202;-3352.728,3866.29;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;327;-3838.919,-1327.672;Inherit;False;Property;_Color0;内描边颜色;11;1;[HDR];Create;False;0;0;0;False;0;False;0,0,0,0;1,1,1,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;555;-3695.961,-2307.493;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.WorldNormalVector;471;-977.1964,-904.8575;Inherit;True;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.Vector4Node;489;-1005.381,-584.464;Inherit;False;Property;_Vector1;光照方向;52;0;Create;False;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;512;-587.8718,35.6394;Inherit;False;Property;_Float8;光照强度;55;0;Create;False;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;501;-603.3434,-264.1498;Inherit;True;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;111;-3808.732,-1073.633;Inherit;False;110;Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;360;-3692.37,-2660.393;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;560;-3718.924,1910.247;Inherit;False;192;Time;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;562;-3898.991,2552.48;Inherit;False;Property;_Mask1VWrap1;V重铺;37;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;563;-3921.134,2387.891;Inherit;False;150;Noise;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;565;-3833.654,1995.206;Inherit;True;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;561;-3900.386,2480.991;Inherit;False;Property;_Mask1UWrap1;U重铺;36;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;564;-3947.179,2219.333;Inherit;False;Property;_Mask1PannerWarp1;Alpha遮罩纹理位移和扭曲;33;0;Create;False;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;601;-2151.993,-477.0283;Inherit;False;World;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;204;-1971.95,-2355.971;Inherit;False;704.105;314.9378;;3;205;206;207;溶解边缘颜色;0.1444019,0.1956119,0.5566038,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;203;-3132.004,3864.44;Inherit;False;Stroke;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;326;-3430.372,-1538.949;Inherit;True;3;3;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;511;-330.7231,-71.19308;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;472;-657.2193,-847.5415;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;600;-3407.293,-2503.188;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.FunctionNode;573;-3461.709,2112.297;Inherit;False;UV Map;-1;;70;10c682b19dfe6fc45b2e14b1b2f0cbc0;0;10;35;FLOAT;0;False;7;FLOAT2;0,0;False;10;FLOAT4;0,0,0,0;False;15;FLOAT2;0,0;False;23;FLOAT;0;False;24;FLOAT;0;False;26;FLOAT2;0,0;False;25;FLOAT;0;False;31;FLOAT;0;False;37;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;517;-3139.072,-1335.266;Inherit;False;601;World;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;292;-2942.209,-2021.026;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;206;-1818.572,-2104.382;Inherit;False;203;Stroke;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;507;-179.4306,-257.4337;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;491;-542.3939,-862.7839;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.49;False;2;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;205;-1916.159,-2316.439;Inherit;False;Property;_StrokeColor;溶解描边颜色;48;1;[HDR];Create;False;0;0;0;False;0;False;0,0,0,1;0,0,0,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.LerpOp;180;-3898.328,3434.792;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;-1.5;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;568;-3044.755,2366.74;Inherit;False;Property;_Mask1RGB1;Alpha遮罩纹理通道;32;0;Create;False;0;0;0;False;0;False;0,0,0,1;0,0,0,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;566;-3129.969,2099.823;Inherit;True;Property;_Mask2;Alpha遮罩纹理;31;0;Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.Vector4Node;509;-2818.584,-1071.725;Inherit;False;Property;_postion;_postion;59;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.BreakToComponentsNode;526;-2766.227,-1349.364;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RangedFloatNode;534;-2493.341,-1089.014;Inherit;False;Property;_dimianzhezhaoqweizhi;地面遮罩位置;57;0;Create;False;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;530;-2574.721,-980.875;Inherit;False;Property;_dimanzhezhaoruanying;地面遮罩软硬;58;0;Create;False;0;0;0;False;0;False;0;0;0;2;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;207;-1513.208,-2268.063;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;209;-1270.142,-1807.765;Inherit;False;Property;_StrokeSwitch;溶解描边开关;46;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;505;-183.0112,-825.716;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;179;-3614.327,3266.792;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;546;-2566.838,-1335.289;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;567;-2740.76,2118.143;Inherit;False;Tex RGBA;-1;;71;97b553afc31c6594d92091a1f04b3f60;0;2;3;FLOAT4;0,0,0,0;False;10;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;554;-2271.779,-1020.172;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;208;-1043.254,-1902.546;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;477;40.90592,-919.2893;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;478;-290.6147,-1227.805;Inherit;False;Property;_Color2;光照颜色;51;1;[HDR];Create;False;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SaturateNode;182;-3421.327,3264.792;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;547;-2076.224,-1196.489;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;570;-2501.518,2120.423;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;476;64.90128,-1243.408;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ComponentMaskNode;124;-854.3143,-1923.865;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;167;-3217.97,3256.372;Inherit;False;Cutting;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;531;-1850.087,-1220.264;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;536;-1893.766,-911.8331;Inherit;False;Property;_dimianzhezhaokaiguan;地面遮罩开关;56;1;[ToggleUI];Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;574;-2305.673,2091.721;Inherit;False;Mask2;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;479;155.368,-1405.856;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;535;-1647.012,-1187.024;Inherit;True;3;0;FLOAT;1;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;582;-1705.273,-1436.945;Inherit;False;574;Mask2;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;176;-2130.377,-1482.822;Inherit;False;167;Cutting;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;495;384.8813,-1408.573;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;602;415.1999,-1138.147;Inherit;False;Property;_black;_black;60;0;Create;False;0;0;0;False;0;False;1;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;22;-1391.634,-1545.074;Inherit;False;5;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;603;663.1917,-1355.847;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.DynamicAppendNode;17;838.4254,-1300.147;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;29;1062.397,-1295.66;Inherit;False;Tex;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;467;863.4224,-1824.205;Inherit;False;29;Tex;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ComponentMaskNode;558;-3056.229,-2452.657;Inherit;False;False;False;False;True;1;0;COLOR;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;437;1125.323,-1763.872;Float;False;True;-1;2;AmplifyShaderEditor.MaterialInspector;100;5;ASE/Sp/2DcharacterStrokeMask;0770190933193b94aaa3065e307002fa;True;Unlit;0;0;Unlit;2;True;True;2;5;False;;10;False;;0;1;False;;0;False;;True;0;False;;0;False;;False;False;False;False;False;False;False;False;False;True;0;False;;True;True;2;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;True;True;False;0;True;_Reference;255;False;;255;False;;0;True;_Comparison;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;True;True;2;False;;True;3;False;;True;True;0;False;;0;False;;True;2;RenderType=Opaque=RenderType;Queue=Transparent=Queue=0;True;2;False;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;0;;0;0;Standard;1;Vertex Position,InvertActionOnDeselection;1;0;0;1;True;False;;False;0
WireConnection;492;0;480;0
WireConnection;492;1;599;0
WireConnection;592;0;492;0
WireConnection;592;1;586;0
WireConnection;345;0;592;0
WireConnection;363;0;366;3
WireConnection;363;1;366;4
WireConnection;364;0;366;1
WireConnection;364;1;366;2
WireConnection;192;0;190;0
WireConnection;362;0;365;0
WireConnection;362;1;364;0
WireConnection;362;2;363;0
WireConnection;434;0;144;0
WireConnection;434;1;362;0
WireConnection;434;2;435;0
WireConnection;248;35;197;0
WireConnection;248;7;434;0
WireConnection;248;10;146;0
WireConnection;248;31;148;0
WireConnection;248;37;225;0
WireConnection;151;1;248;0
WireConnection;141;3;151;0
WireConnection;141;10;149;0
WireConnection;168;0;141;0
WireConnection;369;0;370;1
WireConnection;369;1;370;2
WireConnection;368;0;370;3
WireConnection;368;1;370;4
WireConnection;169;0;141;0
WireConnection;169;1;168;0
WireConnection;169;2;170;0
WireConnection;367;0;371;0
WireConnection;367;1;369;0
WireConnection;367;2;368;0
WireConnection;150;0;169;0
WireConnection;428;0;429;1
WireConnection;428;1;429;2
WireConnection;431;0;429;3
WireConnection;431;1;429;4
WireConnection;421;0;372;0
WireConnection;421;1;367;0
WireConnection;421;2;422;0
WireConnection;355;0;356;3
WireConnection;355;1;356;4
WireConnection;357;0;356;1
WireConnection;357;1;356;2
WireConnection;432;0;430;0
WireConnection;432;1;428;0
WireConnection;432;2;431;0
WireConnection;514;0;298;0
WireConnection;514;1;516;0
WireConnection;249;35;195;0
WireConnection;249;7;421;0
WireConnection;249;10;133;0
WireConnection;249;15;154;0
WireConnection;249;31;135;0
WireConnection;249;37;221;0
WireConnection;300;0;514;0
WireConnection;354;0;358;0
WireConnection;354;1;357;0
WireConnection;354;2;355;0
WireConnection;427;0;157;0
WireConnection;427;1;432;0
WireConnection;427;2;433;0
WireConnection;127;1;249;0
WireConnection;304;0;300;0
WireConnection;302;1;300;0
WireConnection;303;0;514;0
WireConnection;301;1;514;0
WireConnection;423;0;425;0
WireConnection;423;1;354;0
WireConnection;423;2;424;0
WireConnection;246;35;196;0
WireConnection;246;7;427;0
WireConnection;246;10;158;0
WireConnection;246;15;165;0
WireConnection;246;31;164;0
WireConnection;246;37;224;0
WireConnection;128;3;127;0
WireConnection;128;10;138;0
WireConnection;263;0;259;0
WireConnection;268;0;47;0
WireConnection;268;1;301;0
WireConnection;278;0;47;0
WireConnection;278;1;303;0
WireConnection;275;0;47;0
WireConnection;275;1;302;0
WireConnection;282;0;47;0
WireConnection;282;1;304;0
WireConnection;247;35;194;0
WireConnection;247;7;423;0
WireConnection;247;10;122;0
WireConnection;247;15;152;0
WireConnection;247;31;119;0
WireConnection;247;37;222;0
WireConnection;160;1;246;0
WireConnection;254;0;128;0
WireConnection;254;1;255;0
WireConnection;254;2;256;0
WireConnection;270;0;264;0
WireConnection;270;1;268;0
WireConnection;274;0;264;0
WireConnection;274;1;275;0
WireConnection;277;0;264;0
WireConnection;277;1;278;0
WireConnection;281;0;264;0
WireConnection;281;1;282;0
WireConnection;156;3;160;0
WireConnection;156;10;161;0
WireConnection;230;0;426;0
WireConnection;230;1;199;0
WireConnection;273;0;264;0
WireConnection;273;1;47;0
WireConnection;258;0;254;0
WireConnection;76;1;247;0
WireConnection;320;0;281;4
WireConnection;319;0;277;4
WireConnection;318;0;274;4
WireConnection;317;0;270;4
WireConnection;177;0;156;0
WireConnection;177;1;178;0
WireConnection;200;0;178;0
WireConnection;200;2;230;0
WireConnection;108;3;76;0
WireConnection;108;10;109;0
WireConnection;323;0;273;4
WireConnection;499;0;492;0
WireConnection;499;1;500;0
WireConnection;136;0;258;0
WireConnection;576;0;577;1
WireConnection;576;1;577;2
WireConnection;579;0;577;3
WireConnection;579;1;577;4
WireConnection;321;0;323;0
WireConnection;321;1;317;0
WireConnection;321;2;318;0
WireConnection;321;3;319;0
WireConnection;321;4;320;0
WireConnection;201;0;177;0
WireConnection;201;1;200;0
WireConnection;295;0;273;0
WireConnection;513;0;499;0
WireConnection;513;1;510;0
WireConnection;468;5;469;0
WireConnection;110;0;108;0
WireConnection;580;0;578;0
WireConnection;580;1;576;0
WireConnection;580;2;579;0
WireConnection;329;0;321;0
WireConnection;202;0;201;0
WireConnection;555;0;295;0
WireConnection;555;1;556;0
WireConnection;471;0;468;0
WireConnection;501;0;513;0
WireConnection;501;1;513;0
WireConnection;360;0;139;0
WireConnection;360;1;361;0
WireConnection;565;0;575;0
WireConnection;565;1;580;0
WireConnection;565;2;581;0
WireConnection;601;0;480;0
WireConnection;203;0;202;0
WireConnection;326;0;329;0
WireConnection;326;1;327;0
WireConnection;326;2;111;0
WireConnection;511;0;501;0
WireConnection;511;1;512;0
WireConnection;472;0;471;0
WireConnection;472;1;489;0
WireConnection;600;0;360;0
WireConnection;600;1;555;0
WireConnection;573;35;560;0
WireConnection;573;7;565;0
WireConnection;573;10;564;0
WireConnection;573;15;563;0
WireConnection;573;31;561;0
WireConnection;573;37;562;0
WireConnection;292;0;600;0
WireConnection;292;1;326;0
WireConnection;507;0;511;0
WireConnection;491;0;472;0
WireConnection;180;0;178;0
WireConnection;180;2;426;0
WireConnection;566;1;573;0
WireConnection;526;0;517;0
WireConnection;207;0;205;0
WireConnection;207;1;292;0
WireConnection;207;2;206;0
WireConnection;505;0;491;0
WireConnection;505;1;507;0
WireConnection;179;0;177;0
WireConnection;179;1;180;0
WireConnection;546;0;526;1
WireConnection;546;1;509;2
WireConnection;567;3;566;0
WireConnection;567;10;568;0
WireConnection;554;0;534;0
WireConnection;554;1;530;0
WireConnection;208;0;292;0
WireConnection;208;1;207;0
WireConnection;208;2;209;0
WireConnection;477;0;505;0
WireConnection;182;0;179;0
WireConnection;547;0;546;0
WireConnection;547;1;534;0
WireConnection;547;2;554;0
WireConnection;570;0;567;0
WireConnection;476;0;478;0
WireConnection;476;1;477;0
WireConnection;124;0;208;0
WireConnection;167;0;182;0
WireConnection;531;0;547;0
WireConnection;574;0;570;0
WireConnection;479;0;124;0
WireConnection;479;1;476;0
WireConnection;535;1;531;0
WireConnection;535;2;536;0
WireConnection;495;0;124;0
WireConnection;495;1;479;0
WireConnection;495;2;477;0
WireConnection;22;0;273;4
WireConnection;22;1;176;0
WireConnection;22;2;582;0
WireConnection;22;3;535;0
WireConnection;22;4;556;4
WireConnection;603;0;495;0
WireConnection;603;1;602;0
WireConnection;17;0;603;0
WireConnection;17;3;22;0
WireConnection;29;0;17;0
WireConnection;558;0;600;0
WireConnection;437;0;467;0
ASEEND*/
//CHKSM=35A6804132FBB5A20CF277613DA1EF1BCB8C0F04