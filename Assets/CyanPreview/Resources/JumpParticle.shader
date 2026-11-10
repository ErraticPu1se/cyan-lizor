Shader "CyanPreview/JumpParticle"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float3 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float r=length(i.uv.xy);
                float soft=pow(saturate(1-r),2);
                float ring=saturate(1-abs(r-.8)*12);
                return fixed4(i.color.rgb,i.color.a*lerp(soft,ring,i.uv.z));
            }
            ENDCG
        }
    }
}
