Shader "Custom/Background" {
	Properties {
		_Center ("Center", Vector) = (0, 0, 0, 0)
		_CenterHue ("Center Color", Float) = 0
		_CenterSaturation ("Center Saturation", Float) = 0
		_CenterValue ("Center Value", Float) = 0
		_Spreading ("Spreading", Float) = 0.9
	}
	SubShader {
		Pass {
			//Tags { "RenderType"="Opaque" }
			//LOD 200
		
			CGPROGRAM
		
			#pragma vertex vert_img
			#pragma fragment frag
			#pragma target 3.0
		
			#include "UnityCG.cginc"

			uniform sampler2D _MainTex;
			uniform float2 _Center;
			uniform float _CenterHue;
			uniform float _CenterSaturation;
			uniform float _CenterValue;
			uniform float _Spreading;

			float3 HSVtoRGB(float3 HSV)
			{
				float3 RGB = 0;
				float C = HSV.z * HSV.y;
				float H = frac(HSV.x) * 6;
				float X = C * (1 - abs(fmod(H, 2) - 1));
				if (HSV.y != 0)
				{
					if (H < 1) { RGB = float3(C, X, 0); }
					else if (H < 2) { RGB = float3(X, C, 0); }
					else if (H < 3) { RGB = float3(0, C, X); }
					else if (H < 4) { RGB = float3(0, X, C); }
					else if (H < 5) { RGB = float3(X, 0, C); }
					else { RGB = float3(C, 0, X); }
				}
				float M = HSV.z - C;
				return RGB + M;
			}

            fixed4 frag(v2f_img i) : SV_Target {
				float2 offsetToCenter = (i.uv - _Center.xy);
				float distanceToCenter = length(offsetToCenter);
				float3 hsv = float3(_CenterHue + distanceToCenter * _Spreading, _CenterSaturation, _CenterValue);
				return fixed4(HSVtoRGB(hsv), 1.0);
            }

			ENDCG
		} 
	}
	FallBack "Diffuse"
}
