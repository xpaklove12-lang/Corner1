// 인트로 컷툰 충격파 껍질. 운석 충돌로 퍼지는 가스·먼지 구름의 앞면을 그린다.
// 시선과 면이 비스듬할수록 밝아져, 가장자리는 얇고 밝은 테두리, 그 안쪽은 옅은 빛 띠, 가운데는 햇빛 음영만 있는 아주 옅은 채움이 된다.
// 색 속성 이름을 _BaseColor로 두어 CuttoonColorTween이 MaterialPropertyBlock으로 투명도를 바꿀 수 있다.
Shader "IntroCuttoon/Shockwave"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.95, 0.88, 1)
        _Rim ("Rim Strength", Float) = 1.6
        _RimPower ("Rim Power", Float) = 12
        _Band ("Band Strength", Float) = 0.12
        _BandPower ("Band Power", Float) = 3
        _Fill ("Fill Strength", Float) = 0.03
        _Lump ("Edge Lump", Float) = 0.006
        _LightDir ("Light Direction", Vector) = (0.3, -0.906, 0.3, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            // 빛을 더하는 방식으로 그려, 뒤의 별과 지구가 비쳐 보인다.
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            float _Rim;
            float _RimPower;
            float _Band;
            float _BandPower;
            float _Fill;
            float _Lump;
            float4 _LightDir;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 view : TEXCOORD1;
            };

            v2f vert(appdata_base input)
            {
                v2f output;
                // 구 방향의 사인 무늬로 정점을 반지름의 _Lump배만큼 밀어 가장자리를 잔물결처럼 거칠게 한다.
                float3 direction = normalize(input.vertex.xyz);
                float lump = sin(direction.x * 23 + 1.3) * sin(direction.y * 27 + 0.7) * sin(direction.z * 25 + 2.1)
                    + 0.5 * sin(direction.x * 57 + direction.z * 47) * sin(direction.y * 53 + 0.4);
                float4 position = float4(input.vertex.xyz + direction * length(input.vertex.xyz) * _Lump * lump, 1);
                output.pos = UnityObjectToClipPos(position);
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.view = WorldSpaceViewDir(position);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 normal = normalize(input.normal);
                float edge = 1 - saturate(dot(normal, normalize(input.view)));
                float shade = lerp(0.3, 1, saturate(dot(normal, -_LightDir.xyz)));
                float alpha = _Fill * shade + _Band * pow(edge, _BandPower) + _Rim * pow(edge, _RimPower);
                return fixed4(_BaseColor.rgb * lerp(0.55, 1, shade), saturate(alpha) * _BaseColor.a);
            }
            ENDCG
        }
    }
}
