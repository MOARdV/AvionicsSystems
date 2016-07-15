Shader "MOARdV/TextMonitor"
{
	// Simple premultiplied-alpha shader for drawing a Font on a MASMonitor
	// display.
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
        _Color("_Color", Color) = (1,1,1,1)
		_EmissiveFactor ("_EmissiveFactor", Range(0,1)) = 1
	}

	SubShader
	{

		Tags { "RenderType"="Overlay" "Queue" = "Transparent" }

		Pass
		{
		Lighting Off
		Blend One OneMinusSrcAlpha
		Cull Back
		Fog { Mode Off }
		ZWrite Off
		ZTest Always

		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#include "UnityCG.cginc"

			struct appdata_t
			{
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f_fontshader
			{
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			sampler2D _MainTex;
			float4 _Color;

			// Trivial vertex shader - transform vertices and pass everything
			// else through unchanged.
			v2f_fontshader vert (appdata_t v)
			{
				v2f_fontshader dataOut;

				dataOut.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				dataOut.texcoord = v.texcoord;
				dataOut.color.r = v.color.r * _Color.r;
				dataOut.color.g = v.color.g * _Color.g;
				dataOut.color.b = v.color.b * _Color.b;
				dataOut.color.a = v.color.a * _Color.a;

				return dataOut;
			}

			// Simple pre-multiplied fragment shader.
			fixed4 frag (v2f_fontshader dataIn) : SV_TARGET
			{
				fixed4 diffuse = tex2D(_MainTex, dataIn.texcoord);
				diffuse.a *= dataIn.color.a;
				diffuse.rgb = (dataIn.color.rgb) * diffuse.a;
				return diffuse;
			}
		ENDCG
		}
	}
}
