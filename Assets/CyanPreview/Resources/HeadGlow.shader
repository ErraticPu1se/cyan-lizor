Shader "CyanPreview/HeadGlow"
{
    Properties { _Tint ("Light color", Color) = (0,1,1,0.6) _DesktopCutout ("Desktop coverage", Float) = 0 _DstBlend ("Destination blend", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha [_DstBlend]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "DesktopTransparency.cginc"
            float _DesktopCutout;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            fixed4 _Tint;
            Output vert(Input v)
            {
                Output o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(Output i) : SV_Target
            {
                float falloff = pow(saturate(1 - length(i.uv * 2 - 1)), 4);
                return DesktopCoverage(fixed4(_Tint.rgb, _Tint.a * falloff * .25), i.vertex.xy, _DesktopCutout);
            }
            ENDCG
        }
    }
}
