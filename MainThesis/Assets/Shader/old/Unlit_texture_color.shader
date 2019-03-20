Shader "Unlit/Texture_color" {
Properties {
	_MainTex ("Texture", 2D) = "white" {}
	_Color ("Main Color", Color) = (1,1,1,1) 
}

SubShader {
	Tags {"RenderType"="Opaque"}
	LOD 100
	
	ZWrite on

	Pass {
		Lighting Off
		SetTexture [_MainTex] {

            constantColor [_Color]

            Combine texture * constant, texture * constant 

         } 
	}
}
}