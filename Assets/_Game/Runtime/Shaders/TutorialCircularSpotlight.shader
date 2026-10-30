Shader "UI/TutorialCircularSpotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0, 0, 0, 0.8)
        _Center1 ("Center 1 (Local)", Vector) = (0, 0, 0, 0)
        _Radius1 ("Radius 1", Float) = 210
        _Center2 ("Center 2 (Local)", Vector) = (0, 0, 0, 0)
        _Radius2 ("Radius 2", Float) = 0
        _Softness ("Edge Softness", Float) = 14
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _Center1;
            float _Radius1;
            float4 _Center2;
            float _Radius2;
            float _Softness;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 pos = IN.worldPosition.xy;

                // Circle 1 (Target sticker spotlight)
                float a1 = 1.0;
                if (_Radius1 > 0.0)
                {
                    float d1 = distance(pos, _Center1.xy);
                    a1 = smoothstep(_Radius1, _Radius1 + _Softness, d1);
                }

                // Circle 2 (Destination slot spotlight, if active)
                float a2 = 1.0;
                if (_Radius2 > 0.0)
                {
                    float d2 = distance(pos, _Center2.xy);
                    a2 = smoothstep(_Radius2, _Radius2 + _Softness, d2);
                }

                fixed4 col = IN.color;
                col.a *= (a1 * a2);
                return col;
            }
            ENDCG
        }
    }
}
