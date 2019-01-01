// MOARdV/Monochrome
//
// Monochrome shader for MASCamera diplay with config-controlled tinting.
//----------------------------------------------------------------------------
// The MIT License (MIT)
//
// Copyright (c) 2016-2017 MOARdV
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
Shader "MOARdV/Monochrome"
{
	Properties
	{
		_MainTex ("Render Input", 2D) = "white" {}
		_Gain ("_Gain", float) = 1.0
		_ChannelR ("Red Channel", float) = 1.0
		_ChannelG ("Green Channel", float) = 1.0
		_ChannelB ("Blue Channel", float) = 1.0
		_Opacity ("_Opacity", float) = 1.0
	}
	SubShader
	{
		ZTest Always Cull Off ZWrite Off Fog { Mode Off }
		Blend SrcAlpha OneMinusSrcAlpha
		Pass
		{
			CGPROGRAM
				#pragma vertex vert_img
				#pragma fragment frag
				#include "UnityCG.cginc"

				UNITY_DECLARE_TEX2D(_MainTex);
				uniform float _Gain;
				uniform float _ChannelR;
				uniform float _ChannelG;
				uniform float _ChannelB;
				uniform float _Opacity;

				float4 frag(v2f_img IN) : COLOR
				{
					float4 c = UNITY_SAMPLE_TEX2D(_MainTex, IN.uv);

					// CIE 1931 conversion of linear color to luminance
					float Y = c.r * 0.2126 + c.g * 0.7152 + c.b * 0.0722;
					// Apply gain
					float gainBoost = max(0.0, _Gain - 1.0) * 0.15;
					Y = (Y * _Gain + gainBoost);
					return float4(saturate(Y * _ChannelR), saturate(Y * _ChannelG), saturate(Y * _ChannelB), saturate(_Opacity));
				}
			ENDCG
		}
	}
}
