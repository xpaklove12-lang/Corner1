// 비디오 빨리감기처럼 화면 곳곳의 흰색·회색 가로줄이 오른쪽에서 왼쪽으로 지나가며 흐려져 사라지는 UI 셰이더.
// 시간은 FastForwardOverlay가 넘기는 전역 값 _FastForwardTime(실제 시간)을 쓴다.
Shader "EyeMaze/FastForwardLines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Rows ("Rows", Float) = 120
        _Density ("Density", Range(0, 1)) = 0.12
        _Rate ("Cycles Per Second", Float) = 1.5
        _Travel ("Travel Per Cycle", Float) = 3.6
        _MinLength ("Min Length", Range(0, 1)) = 0.15
        _MaxLength ("Max Length", Range(0, 1)) = 0.45
        _EdgeSoftness ("Head Softness", Range(0, 0.5)) = 0.08
        _MinBrightness ("Min Brightness", Range(0, 1)) = 0.5
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 0.35

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            fixed4 _Color;
            float _Rows;
            float _Density;
            float _Rate;
            float _Travel;
            float _MinLength;
            float _MaxLength;
            float _EdgeSoftness;
            float _MinBrightness;
            float _MaxAlpha;
            float4 _ClipRect;
            float _FastForwardTime;

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float row = floor(i.texcoord.y * max(1.0, _Rows));

                // 행마다 시작 시점을 어긋나게 해 모든 줄이 한꺼번에 바뀌지 않게 한다.
                float local = _FastForwardTime * _Rate + Hash(float2(row, 3.7));
                float cycle = floor(local);
                float life = frac(local);
                float on = step(1.0 - _Density, Hash(float2(row, cycle)));

                // 줄 머리는 무작위 위치에서 출발해 한 주기 동안 왼쪽으로 _Travel만큼 이동한다.
                float len = max(0.001, lerp(_MinLength, _MaxLength, Hash(float2(row + 41.7, cycle))));
                float headX = lerp(0.4, 1.2, Hash(float2(row + 17.3, cycle))) - life * _Travel;
                float u = (i.texcoord.x - headX) / len;

                // 왼쪽 머리가 가장 밝고 오른쪽 꼬리로 갈수록 흐려진다.
                float soft = max(0.001, _EdgeSoftness);
                float shape = smoothstep(0.0, soft, u) * (1.0 - smoothstep(soft, 1.0, u));
                // 짧게 나타난 뒤 이동하는 동안 점점 흐려져 사라진다.
                float fade = smoothstep(0.0, 0.1, life) * (1.0 - life);

                float brightness = lerp(_MinBrightness, 1.0, Hash(float2(row + 5.1, cycle)));
                fixed4 c = fixed4(brightness, brightness, brightness, on * shape * fade * _MaxAlpha) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
