Shader "StickyPeerSort/UI/StickerCurl"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BackColor ("Paper underside", Color) = (.99,.98,.955,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 curl:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 curl:TEXCOORD1; float4 local:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color, _BackColor, _TextureSampleAdd;
            float4 _ClipRect;
            v2f vert(appdata input)
            {
                v2f output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.local=input.vertex; output.vertex=UnityObjectToClipPos(input.vertex);
                output.color=input.color*_Color; output.uv=input.uv; output.curl=input.curl; return output;
            }
            fixed4 frag(v2f input):SV_Target
            {
                fixed4 tex=tex2D(_MainTex,input.uv)+_TextureSampleAdd;
                fixed4 output=tex*input.color;
                fixed3 paper=_BackColor.rgb*(.96+.04*saturate(input.curl.z*2));
                output.rgb=lerp(output.rgb,paper*input.color.rgb,saturate(input.curl.x));
                output.rgb*=1-.12*saturate(input.curl.y)-.18*saturate(input.curl.w);
                output.rgb+=.025*pow(saturate(input.curl.y),6)*input.color.rgb;
                #ifdef UNITY_UI_CLIP_RECT
                output.a*=UnityGet2DClipping(input.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(output.a-.001);
                #endif
                return output;
            }
            ENDCG
        }
    }
}
