// MOARdV/Monitor
//
// Pre-multiplied alpha shader for rendering textured or non-textured
// quads on a MASMonitor object.
//----------------------------------------------------------------------------
// The MIT License (MIT)
//
// Copyright (c) 2016 MOARdV
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//
//----------------------------------------------------------------------------
Shader "MOARdV/Monitor"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
        _Color("_Color", Color) = (1,1,1,1)
		_ClipCoords ("_ClipCoords", Vector) = (-1, -1, 1, 1)
		_MainTex_ST ("_MainTex_ST", Vector) = (1, 1, 0, 0)
	}

	SubShader
	{
		Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

		Pass
		{
			ZWrite On
			ColorMask 0
		}

		Pass
		{
		Lighting Off
		Blend One OneMinusSrcAlpha
		Cull Back
		Fog { Mode Off }
		ZWrite On
		ZTest LEqual

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
				float2 normalizedVertex : TEXCOORD1;
			};

			UNITY_DECLARE_TEX2D(_MainTex);
			float4 _MainTex_ST;
			float4 _Color;
			float4 _ClipCoords;

			// Minimal vertex shader: Do the appropriate pre-processing here.
			v2f_fontshader vert (appdata_t v)
			{
				v2f_fontshader dataOut;

				float4 transformedVtx = mul(UNITY_MATRIX_MVP, v.vertex);
				dataOut.vertex = transformedVtx;
				dataOut.normalizedVertex.xy = transformedVtx.xy / transformedVtx.w;
				dataOut.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				dataOut.color.r = v.color.r * _Color.r;
				dataOut.color.g = v.color.g * _Color.g;
				dataOut.color.b = v.color.b * _Color.b;
				dataOut.color.a = v.color.a * _Color.a;

				return dataOut;
			}

			// Apply device-normalized clipping, fetch the color from the
			// texture, and write the results.
			fixed4 frag (v2f_fontshader dataIn) : SV_TARGET
			{
				// dataIn.normalizedVertex is in device normalized coordinates.
				// Apply clipping here.  We only care about clipping in the XY
				// plane, so _ClipCoords contains the lower-left XY coordinate
				// in the .xy fields, and the upper-right XY coordinate in the
				// .zw fields.  Simple subtraction will let us figure out if
				// the fragment falls inside or outside the coords.

				clip(float4(
					dataIn.normalizedVertex.x - _ClipCoords.x,
					dataIn.normalizedVertex.y - _ClipCoords.y,
					_ClipCoords.z - dataIn.normalizedVertex.x,
					_ClipCoords.w - dataIn.normalizedVertex.y
					));

				fixed4 diffuse = UNITY_SAMPLE_TEX2D(_MainTex, dataIn.texcoord);
				diffuse.a *= dataIn.color.a;
				diffuse.rgb *= (dataIn.color.rgb) * diffuse.a;
				return diffuse;
			}
		ENDCG
		}
	}
}
