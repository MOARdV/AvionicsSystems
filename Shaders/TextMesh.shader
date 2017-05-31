// MOARdV/TextMesh
//
// Derived from older KSP Alpha / Transparent
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
Shader "MOARdV/TextMesh"
{
	Properties
	{
		_MainTex("MainTex (RGBA)", 2D) = "white" {}
        _Color("_Color", Color) = (1,1,1,1)

		_Opacity("_Opacity", Range(0,1)) = 1

		_RimFalloff("Rim Falloff", Range(0.01,5) ) = 0.1
		_RimColor("Rim Color", Color) = (0,0,0,0)
		_TemperatureColor("_TemperatureColor", Color) = (0,0,0,0)
		_BurnColor ("Burn Color", Color) = (1,1,1,1)

		_EmissiveFactor ("_EmissiveFactor", Range(0,1)) = 1
		_Cutoff ("Alpha cutoff", Range(0,1)) = 0.35
	}

	SubShader
	{
		Tags {"Queue"="Transparent"}

		ZWrite Off
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha
		Cull Back

		CGPROGRAM

		#pragma surface surf Lambert alpha

		#pragma target 3.0

		half _Shininess;

		sampler2D _MainTex;

		float _Opacity;
		float _Fresnel;
		float _RimFalloff;
		float4 _RimColor;
		float4 _TemperatureColor;
		float4 _BurnColor;
		float _EmissiveFactor;


		struct Input
		{
			float2 uv_MainTex;
			float2 uv_BumpMap;
			float2 uv_Emissive;
			float3 viewDir;
			float3 worldPos;

			fixed4 color : COLOR;
		};

		void surf (Input IN, inout SurfaceOutput o)
		{
			float4 color = IN.color * _BurnColor;

			float alpha = tex2D(_MainTex, (IN.uv_MainTex)).a * IN.color.a;

			float3 normal = float3(0.0,0.0,1.0);
			half rim = 1.0 - saturate(dot (normalize(IN.viewDir), normal));

            float3 fresnel = pow(1 - rim, _Fresnel);

			float3 emission = (_RimColor.rgb * pow(rim, _RimFalloff)) * _RimColor.a;
			emission += _TemperatureColor.rgb * _TemperatureColor.a;

			o.Albedo = color.rgb;
            o.Emission = emission * (1.0 - _EmissiveFactor) + (_EmissiveFactor * color.rgb) * alpha;
			o.Normal = normal;
			o.Emission *= _Opacity;
			o.Alpha = alpha;
		}

		ENDCG
	}
	Fallback "Standard"
}
