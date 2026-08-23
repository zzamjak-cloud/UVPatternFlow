# UVPatternFlow

패턴 텍스처 UV 스크롤/회전 효과와 스프라이트 시트 기반 그리드 플로우 효과를 제공하는 Unity 패키지입니다. 모바일 게임 최적화를 최우선으로 설계되었습니다.

## 요구 사항

- Unity 6000.0 이상
- com.unity.ugui (uGUI)

## 설치 방법

### 1. Unity Package Manager (Git URL) — 권장

1. Unity 에디터에서 `Window > Package Manager` 를 엽니다.
2. 좌측 상단 `+` 버튼 → `Install package from git URL...` 을 선택합니다.
3. 아래 URL을 입력하고 `Install` 을 누릅니다.

```
https://github.com/zzamjak-cloud/UVPatternFlow.git?path=/Packages/com.zzamjak.uvpatternflow
```

특정 버전을 설치하려면 태그를 붙입니다.

```
https://github.com/zzamjak-cloud/UVPatternFlow.git?path=/Packages/com.zzamjak.uvpatternflow#v1.0.0
```

### 2. manifest.json 직접 편집

`Packages/manifest.json` 의 `dependencies` 에 다음을 추가합니다.

```json
{
  "dependencies": {
    "com.zzamjak.uvpatternflow": "https://github.com/zzamjak-cloud/UVPatternFlow.git?path=/Packages/com.zzamjak.uvpatternflow#v1.0.0"
  }
}
```

## 포함 컴포넌트

| 컴포넌트 | 대상 | 설명 |
|----------|------|------|
| `UVPatternFlow` | RawImage / Image / SpriteRenderer | 패턴 텍스처 UV 스크롤 + 회전 |
| `UVSheetGridFlow` | RawImage / Image | 스프라이트 시트 기반 그리드 셀 랜덤 스위칭 + 무한 스크롤 |

두 컴포넌트 모두 `AddComponentMenu`: **CAT/Effects/** 하위에 등록되며, 네임스페이스는 `CAT.Effects` 입니다.

---

## UVPatternFlow 사용법

패턴 텍스처의 UV를 스크롤/회전시키는 컴포넌트입니다. 배경 흐름, 무한 스크롤 패턴, 회전하는 방사 효과 등에 사용합니다.

### 모드 자동 감지

- **RawImage 모드**: `IMeshModifier` 로 메시 UV를 직접 변환합니다. Material을 건드리지 않으므로 **SoftMask / SoftMaskLight와 자동 호환**됩니다.
- **Image 모드**: `IMeshModifier` 로 uv1/uv2 채널에 패턴 UV와 스프라이트 외곽 UV Rect를 싣고, 전용 UI 셰이더(`CAT/Effects/UVPatternFlow (UI)`)가 프래그먼트에서 서브영역 내 반복 샘플링합니다. **스프라이트 아틀라스에 포함된 스프라이트도 지원**하며 Wrap Mode 설정이 필요 없습니다. Canvas의 Additional Shader Channels(TexCoord1/2)는 자동 활성화됩니다. UGUI Mask / RectMask2D와 호환됩니다.
- **Sprite 모드 (SpriteRenderer)**: 전용 셰이더(`CAT/Effects/UVPatternFlow (Sprite)`) + `MaterialPropertyBlock` 으로 UV를 변환합니다. 공유 Material 1개를 모든 인스턴스가 사용하므로 배칭이 유지됩니다.

### 기본 사용

1. `RawImage`, `Image` 또는 `SpriteRenderer` 가 있는 GameObject에 `UVPatternFlow` 컴포넌트를 추가합니다.
2. 텍스처의 **Wrap Mode를 Repeat** 로 설정합니다. (Image 모드는 불필요)
3. 인스펙터에서 `Scroll Speed`, `UV Rect`, `Rotation` 등을 조정합니다.
4. `Play On Enable` 이 켜져 있으면 활성화 시 자동 재생됩니다.

### 인스펙터 프로퍼티

| 프로퍼티 | 설명 |
|----------|------|
| `Scroll Speed` | 초당 UV 스크롤 속도 (X/Y축) |
| `UV Rect` | 타일링(W/H)과 기본 오프셋(X/Y). RawImage.uvRect 대신 이 값을 사용 |
| `Rotation` | 패턴 회전 각도 (도). 양수 = 화면상 반시계 |
| `Rotation Speed` | 회전 속도 (도/초). 0 = 회전 애니메이션 없음 |
| `Aspect Compensation` | 비정사각 영역에서 회전 시 패턴 찌그러짐 보정 |
| `Play On Enable` | 컴포넌트 활성화 시 자동 재생 |

### 스크립트 제어

```csharp
using CAT.Effects;

UVPatternFlow flow = GetComponent<UVPatternFlow>();

flow.Play();                              // 재생
flow.Pause();                             // 일시정지 (위치 유지)
flow.Stop();                              // 정지 + 오프셋/각도 리셋
flow.SetOffset(new Vector2(0.5f, 0f));    // 오프셋 직접 지정
flow.ResetOffset();                       // 오프셋/각도만 리셋

flow.ScrollSpeed = new Vector2(0.2f, 0f); // 스크롤 속도 변경
flow.Rotation = 45f;                      // 회전 각도 변경
flow.RotationSpeed = 30f;                 // 초당 30도 회전
flow.UVRect = new Rect(0f, 0f, 3f, 3f);   // 3×3 타일링
```

### 제약 사항

- RawImage/Sprite 모드: 텍스처 **Wrap Mode = Repeat** 필수
- RawImage 모드: `RawImage.uvRect` 는 기본값 `(0,0,1,1)` 로 두고 이 컴포넌트의 `UV Rect` 사용 권장
- Image 모드: **Image Type = Simple**, **Use Sprite Mesh OFF** 필수. 아틀라스 사용 시 SpriteAtlas의 **Tight Packing / Allow Rotation 을 꺼야** 합니다. 밉맵 텍스처는 반복 경계에 미세한 심(seam)이 보일 수 있습니다.
- Sprite 모드: 스프라이트 아틀라스 불가 (아틀라스가 필요하면 Image 모드 사용), **Mesh Type = Full Rect**, **Draw Mode = Simple** 권장

---

## UVSheetGridFlow 사용법

스프라이트 시트(예: 3×3 아틀라스)를 그리드 셀로 반복 배열하고, 각 셀이 지정 주기마다 시트 내 프레임을 랜덤 스위칭하면서 전체 그리드가 무한 스크롤하는 효과입니다. 아이템/이모지 배경 벽지 같은 연출에 사용합니다.

### 기본 사용

1. `RawImage` 또는 `Image` 가 있는 GameObject에 `UVSheetGridFlow` 컴포넌트를 추가합니다.
2. `RawImage.texture`(또는 `Image.sprite`) 에 스프라이트 시트를 지정합니다. Image 모드는 **스프라이트 아틀라스에 포함된 시트도 지원**합니다.
3. `Sheet Tiles` 를 시트 분할 수(예: 3×3)에 맞게 설정합니다.
4. `Grid Count`, `Cell Gap`, `Scroll Speed`, `Switch Duration` 을 조정합니다.

### 인스펙터 프로퍼티

| 프로퍼티 | 설명 |
|----------|------|
| `Sheet Tiles` | 시트 분할 수 (가로×세로). 예: 3×3 = 9프레임 |
| `Grid Count` | 화면에 반복할 그리드 셀 수 (가로×세로) |
| `Cell Gap` | 셀 간격 (셀 크기 대비 비율 0~0.9). 간격 부분은 투명 |
| `Scroll Speed` | 초당 스크롤 속도 (그리드 셀 단위) |
| `Switch Duration` | 이미지 스위칭 주기 (초). 셀마다 위상이 달라 자연스럽게 전환 |
| `Frame Inset` | 프레임 가장자리 인셋 — 인접 프레임 블리딩 방지 |
| `Play On Enable` | 컴포넌트 활성화 시 자동 재생 |

### 스크립트 제어

```csharp
using CAT.Effects;

UVSheetGridFlow gridFlow = GetComponent<UVSheetGridFlow>();

gridFlow.Play();                                // 재생
gridFlow.Pause();                               // 일시정지 (위치 유지)
gridFlow.Stop();                                // 정지 + 리셋

gridFlow.SheetTiles = new Vector2Int(4, 4);     // 4×4 시트
gridFlow.GridCount = new Vector2(6f, 6f);       // 화면에 6×6 셀
gridFlow.CellGap = new Vector2(0.2f, 0.2f);     // 셀 간격 20%
gridFlow.ScrollSpeed = new Vector2(0.5f, 0.5f); // 대각선 흐름
gridFlow.SwitchDuration = 1f;                   // 1초마다 프레임 스위칭
```

### 제약 사항

- UGUI **Mask / RectMask2D 호환**. SoftMask 계열은 미대응 (전용 셰이더 변형 필요)
- Wrap Mode 무관 (셰이더 내부 frac 처리)
- RawImage 모드: `uvRect` 는 기본값 `(0,0,1,1)` 권장
- Image 모드: **Image Type = Simple**, **Use Sprite Mesh OFF** 필수. 아틀라스 사용 시 SpriteAtlas의 **Tight Packing / Allow Rotation 을 꺼야** 합니다.

---

## 모바일 최적화 설계

- **UVPatternFlow (UI 모드)**: 메시 UV 직접 변환 — Material 인스턴스 생성 없음, 마스크 체인 호환
- **UVPatternFlow (Sprite 모드)**: 공유 Material 1개 + `MaterialPropertyBlock` — per-instance 할당 없음
- **UVSheetGridFlow**: 셀 분할/간격/프레임 선택을 전부 프래그먼트 셰이더에서 처리 — 쿼드 1개, Canvas rebuild 없음
- 셰이더는 `half` precision 우선, 분기 없는 수학 연산, sin-free 해시(모바일 GPU 정밀도 안전) 사용
- `Shader.PropertyToID` static 캐싱, 오프셋/각도 래핑으로 부동소수점 정밀도 유지
- 런타임 핫패스(Update/ModifyMesh)에 GC 할당 없음

## 모바일 성능 가이드 (사용 시 주의)

- **UVPatternFlow UI 모드는 전용 하위 Canvas 로 분리하세요.** 스크롤/회전 중 매 프레임 메시를 갱신하므로, UI 가 많은 Canvas 에 같이 두면 전체 배칭이 매 프레임 재계산됩니다. 컴포넌트 부착 시 전용 Canvas 가 자동 추가되며, 기존 오브젝트는 인스펙터의 "전용 Canvas 추가" 버튼을 사용하세요. (UVSheetGridFlow 는 material 프로퍼티 방식이라 해당 없음)
- **Sprite 모드는 인스턴스별 드로우콜입니다.** `MaterialPropertyBlock` 특성상 배칭이 깨지므로 수십 개 이상 대량 배치에는 부적합합니다.
- **풀스크린 배경으로 쓸 때는 오버드로우를 감안하세요.** 투명 블렌드 풀스크린 1패스입니다. 배경이 항상 화면을 가리면 배경 뒤 요소를 비활성화하는 것이 좋습니다.

## 라이선스

Copyright (C) 2026 zzamjak

이 패키지는 [GNU General Public License v3.0](LICENSE.md)으로 배포됩니다.

수정본과 파생 저작물을 배포하는 경우 GPLv3 조건에 따라 동일한 라이선스로 소스 코드를 공개해야 합니다. 이 프로젝트의 저작권 표기와 라이선스 고지는 제거하거나 다른 저작권자가 원저작권을 소유한 것처럼 표시할 수 없습니다.

GPLv3는 강한 copyleft 라이선스이지만, 오픈소스 라이선스의 성격상 상업적 사용 또는 판매 자체를 전면 금지하지는 않습니다. 다만 배포자는 GPLv3가 보장하는 소스 공개, 수정 권리, 재배포 권리 위에 추가 제한을 부과할 수 없습니다.
