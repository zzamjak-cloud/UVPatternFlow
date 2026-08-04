// UVSheetGridFlow 전용 셰이더 (RawImage 전용)
//
// 스프라이트 시트(예: 3×3)를 그리드 셀로 반복 배열하고,
// 셀마다 랜덤 프레임을 주기적으로 스위칭하면서 전체를 스크롤한다.
//
// - _MainTex: RawImage 의 Texture (CanvasRenderer 가 자동 주입)
// - _FlowOffset / _FlowTime: UVSheetGridFlow 가 매 프레임 주입 (C# 누적 → 속도 변경 시 튐 없음)
// - Wrap Mode 무관: 그리드 반복은 frac() 으로 처리, 시트 프레임은 내부 좌표만 샘플링
// - 모바일 최적화: 분기 없음(step/saturate), 텍스처 샘플링 1회, 해시 계산만 float
// - 제약: Mask/RectMask2D/SoftMask 계열 미대응 (클리핑/스텐실 없음)
Shader "CAT/Effects/UVSheetGridFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Sheet", 2D) = "white" {}
        _Tiles ("Sheet Tiles (X,Y)", Vector) = (3, 3, 0, 0)
        _GridCount ("Grid Count (X,Y)", Vector) = (4, 4, 0, 0)
        _Gap ("Cell Gap (X,Y ratio)", Vector) = (0.1, 0.1, 0, 0)
        _FrameInset ("Frame Edge Inset", Range(0, 0.05)) = 0.005
        [HideInInspector] _SwitchDuration ("Switch Duration (sec)", Float) = 0.5
        [HideInInspector] _FlowOffset ("Flow Offset (cells)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FlowTime ("Flow Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }
        LOD 100

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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Tiles;
            float4 _GridCount;
            float4 _Gap;
            float _FrameInset;
            float _SwitchDuration;
            float4 _FlowOffset;
            float _FlowTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color; // RawImage.color 는 정점 컬러로 전달됨
                return o;
            }

            // 2D → 1D 해시 (0~1 난수, 분기 없음)
            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 frag (v2f i) : SV_Target
            {
                // 1. 그리드 공간 변환 + 스크롤 (오프셋은 C# 누적값)
                float2 flow = i.uv * _GridCount.xy + _FlowOffset.xy;
                float2 cellId = floor(flow);
                float2 f = flow - cellId;

                // 2. 셀 간격: 이미지 영역을 [gap/2, 1-gap/2] 로 축소, 밖은 투명 (step 마스크)
                float2 inner = (f - _Gap.xy * 0.5) / max(1.0 - _Gap.xy, 1e-4);
                float2 in01 = step(0.0, inner) * step(inner, 1.0);
                half mask = (half)(in01.x * in01.y);

                // 3. 셀별 스위칭 위상 오프셋 → 셀마다 다른 타이밍에 전환 (동시 깜빡임 방지)
                float phase = Hash21(cellId + 0.17);
                float slot = floor(_FlowTime / max(_SwitchDuration, 1e-4) + phase);

                // 4. 셀 + 시간 슬롯 해시로 시트 프레임 랜덤 선택
                float frameCount = _Tiles.x * _Tiles.y;
                float rnd = Hash21(cellId + slot * 37.719);
                float frame = min(floor(rnd * frameCount), frameCount - 1.0);
                float row = floor(frame / _Tiles.x);
                float2 frameXY = float2(frame - _Tiles.x * row, row);

                // 5. 프레임 내부 UV (가장자리 인셋으로 인접 프레임 블리딩 방지)
                float2 fuv = saturate(inner) * (1.0 - _FrameInset * 2.0) + _FrameInset;
                float2 sheetUV = (frameXY + fuv) / _Tiles.xy;

                half4 col = tex2D(_MainTex, sheetUV) * i.color;
                col.a *= mask;
                return col;
            }
            ENDCG
        }
    }
}
