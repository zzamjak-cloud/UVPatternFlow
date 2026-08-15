// UVPatternFlow Image(UGUI) 모드 전용 셰이더
//
// Image 는 스프라이트가 아틀라스에 포함될 수 있어 UV 가 아틀라스의 서브영역이다.
// 메시 UV 오프셋만으로는 서브영역 밖(이웃 스프라이트)을 샘플링하게 되므로,
// 프래그먼트에서 frac() 으로 서브영역 내 반복 샘플링한다. (Wrap Mode 무관)
//
// - uv1(TEXCOORD1): C#(IMeshModifier)이 계산한 변환된 패턴 UV (무한 스크롤 좌표)
// - uv2(TEXCOORD2): 스프라이트 외곽 UV Rect (min.xy, size.zw) — 아틀라스 서브영역
//   → Canvas 의 Additional Shader Channels 에 TexCoord1/2 필요 (컴포넌트가 자동 활성화)
// - frac() 의 미분 불연속으로 인한 밉맵 경계선 방지를 위해 tex2Dgrad 사용
// - UGUI Mask(스텐실) / RectMask2D(_ClipRect) 호환
Shader "CAT/Effects/UVPatternFlow (UI)"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        // UVPatternFlow 호환 material 식별용 마커 (값 미사용)
        [HideInInspector] _UVFlowUI ("UVPatternFlow Marker", Float) = 1
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;      // 미사용 (원본 스프라이트 UV)
                float2 pattern : TEXCOORD1; // 변환된 패턴 UV (무한 좌표)
                float4 rect : TEXCOORD2;    // 외곽 UV Rect (min.xy, size.zw)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 pattern : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.pattern = v.pattern;
                o.rect = v.rect;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // frac 으로 서브영역 내 반복. 밉맵 미분은 연속 좌표(cont)로 계산해 경계선 방지
                float2 cont = i.rect.xy + i.pattern * i.rect.zw;
                float2 uv = i.rect.xy + frac(i.pattern) * i.rect.zw;
                half4 color = (tex2Dgrad(_MainTex, uv, ddx(cont), ddy(cont)) + _TextureSampleAdd) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
