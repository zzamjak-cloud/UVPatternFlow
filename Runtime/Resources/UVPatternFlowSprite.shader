// UVPatternFlow Sprite 모드 전용 셰이더
//
// SpriteRenderer 에는 RawImage.uvRect 같은 UV 제어 수단이 없고,
// URP 기본 스프라이트 셰이더에는 UV 오프셋/회전 파라미터가 없으므로 전용 셰이더로 처리한다.
//
// - _MainTex: SpriteRenderer 가 [PerRendererData] 로 스프라이트 텍스처를 자동 주입
// - _UVFlowMat / _UVFlowST / _RendererColor: UVPatternFlow 가 MaterialPropertyBlock 으로 주입
//   (material 은 전 인스턴스 공유 — 인스턴스 생성 없음)
// - 회전 행렬(aspect 보정 포함)은 C# 에서 계산 → 정점 셰이더는 곱셈 2회 (분기 없음)
// - 모바일 최적화: half precision, 프래그먼트는 단일 텍스처 샘플링 × 컬러
Shader "CAT/Effects/UVPatternFlow (Sprite)"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _UVFlowMat ("UV Rotation Matrix (m00,m01,m10,m11)", Vector) = (1,0,0,1)
        [HideInInspector] _UVFlowST ("UV Tiling(XY) Offset(ZW)", Vector) = (1,1,0,0)
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _Color;
            half4 _RendererColor;
            float4 _UVFlowMat;
            float4 _UVFlowST;

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

            // 정점: 회전(피벗 0.5,0.5) → 타일링/오프셋
            // 스크롤 오프셋으로 UV 가 0~1 범위를 벗어나므로 float 유지 (Wrap=Repeat 텍스처 전제)
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float2 p = v.uv - 0.5;
                float2 r = float2(dot(_UVFlowMat.xy, p), dot(_UVFlowMat.zw, p)) + 0.5;
                o.uv = r * _UVFlowST.xy + _UVFlowST.zw;
                // Unity 6: SpriteRenderer 색상은 정점 컬러에 실리지 않으므로 _RendererColor(MPB) 사용
                o.color = v.color * _RendererColor * _Color;
                return o;
            }

            // 프래그먼트: 텍스처 × 컬러 (분기 없음)
            half4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
