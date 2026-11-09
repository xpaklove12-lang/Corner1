Shader "EyeMaze/NPC Interaction Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.3,0.95,1,1)
        _OutlineWidth ("Outline Width", Float) = 2
        _OutlineEnabled ("Outline Enabled", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineEnabled;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float2 stepUV = float2(length(float2(ddx(input.uv.x), ddy(input.uv.x))),
                                       length(float2(ddx(input.uv.y), ddy(input.uv.y)))) * _OutlineWidth;
                half edge = 0;
                edge = max(edge, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(stepUV.x, 0)).a);
                edge = max(edge, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(stepUV.x, 0)).a);
                edge = max(edge, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, stepUV.y)).a);
                edge = max(edge, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, stepUV.y)).a);
                edge = max(edge, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + stepUV).a);
                edge = max(edge, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - stepUV).a);
                edge = max(edge, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(stepUV.x, -stepUV.y)).a);
                edge = max(edge, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-stepUV.x, stepUV.y)).a);
                half outline = edge * (1 - sprite.a) * _OutlineEnabled * _OutlineColor.a * input.color.a;
                half alpha = sprite.a + outline;
                half3 rgb = (sprite.rgb * sprite.a + _OutlineColor.rgb * outline) / max(alpha, 0.0001h);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
