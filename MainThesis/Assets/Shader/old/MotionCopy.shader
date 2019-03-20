Shader "Unlit/MotionCopy"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Previous("TexturePrevious", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};
			sampler2D _MainTex, _Previous;
			float4 _MainTex_ST;
			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};
			fixed4 c;
			half MotionSampler(sampler2D Target, half2 Shift, fixed2 iuv)
			{
				c = tex2D(Target, iuv);
				c += tex2D(Target, iuv + Shift + (half2(1, 0) * _MainTex_ST.xy));
				c += tex2D(Target, iuv + Shift + (half2(0, 1) * _MainTex_ST.xy));
				c += tex2D(Target, iuv + Shift + (half2(-1, 0) * _MainTex_ST.xy));
				c += tex2D(Target, iuv + Shift + (half2(0, -1) * _MainTex_ST.xy));
				return c.r + c.g + c.b;
			}

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			half CheckValue, FirstValue, PrevValue, LastMatch;
			float2 FinalUvShift, CheckUvShift;
			fixed4 frag (v2f i) : SV_Target
			{
					PrevValue = MotionSampler(_Previous, half2(0, 0), i.uv);
					LastMatch = abs(PrevValue - MotionSampler(_MainTex, half2(0, 0), i.uv));
					for (int Circle = 1; Circle < 4; ++Circle)
					{
						for (int j = 0; j < 4 * Circle; ++j)
						{
							if ((CheckValue = abs(PrevValue - MotionSampler(_MainTex, CheckUvShift = half2(sin((FirstValue = 6.28 / (4 * Circle))*j), cos(FirstValue*j))*_MainTex_ST.xy*Circle, i.uv))) < LastMatch)
							{
								LastMatch = CheckValue;
								FinalUvShift = CheckUvShift;
							}
						}
					}
					return fixed4(FinalUvShift.xy, 0, 1);
			}
			ENDCG
		}
	}
}
