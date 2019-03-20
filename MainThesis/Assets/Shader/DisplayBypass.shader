Shader "Unlit/DisplayBypass"
{
	Properties
	{
		_MainTex ("TexMain", 2D) = "white" {}
		//_PreviousTex ("TexPrevious", 2D) = "white" {}
		//_MotionTex ("TexMotion", 2D) = "black" {}
		//_Delta ("Delta", Vector) = (0, 0, 0, 0) 
		//_ShowMotion ("Motion", float) = 0
		//_TestShift ("Test Shift", range (0,1)) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Cutout" }
		LOD 100 Cull Off ZWrite Off ZTest Always

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

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;//, _PreviousTex, _MotionTex;
			//float4 _MainTex_ST;
			//half _TestShift;
			//sampler2D_half _CameraMotionVectorsTexture;

			float _ShowMotion;	

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				//fixed4 col;
				//if (_ShowMotion > 0) {
				//	
				//
				//	// SHOW MOTION VECTOR IN GRAYSCALE
				//	//bump up the pixels to see the motion vectors clearly
				//	//col = abs(300 * tex2D(_CameraMotionVectorsTexture, i.uv));
				//	//float combine = sqrt(col.r * col.r + col.g*col.g + col.b*col.b);
				//	//col.r = combine;
				//	//col.g = combine;
				//	//col.b = combine;
				//	//col.a = 1.0;
				//	//half2 uvShift = (((tex2D(_CameraMotionVectorsTexture, i.uv).rg)));
				//	//col = lerp(tex2D(_PreviousTex, i.uv - (uvShift*_TestShift)), tex2D(_MainTex, i.uv + (uvShift*(1 - _TestShift))), _TestShift);								
				//	//col = 300 * (tex2D(_CameraMotionVectorsTexture, i.uv));

				//	

				//	//APPLY INTERPOLATION TO PREVIOUS AND MAIN TEXTURE
				//	half2 uvShift = tex2D(_CameraMotionVectorsTexture, i.uv).rg;
				//	col = lerp(tex2D(_PreviousTex, i.uv - (uvShift*_TestShift)), tex2D(_MainTex, i.uv + (uvShift*(1 - _TestShift))), _TestShift);								
				//	//	
				//}
				//else {
				//	
				//	col = tex2D(_MainTex, i.uv);
				//}

				return tex2D(_MainTex, i.uv);

			}
			ENDCG
		}
	}
}
