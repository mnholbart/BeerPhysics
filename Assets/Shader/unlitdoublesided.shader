//Originally by Jaap Kreijkamp

Shader "Unlit/UnlitSpecial" {
 
Properties {_MainTex ("Texture", 2D) = ""}
Category{
Cull off }
SubShader {Pass {SetTexture[_MainTex]} }

}
