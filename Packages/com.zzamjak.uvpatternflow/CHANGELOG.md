# Changelog

이 프로젝트의 주요 변경 사항을 기록합니다.

포맷은 [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/)를 따르며,
버전은 [Semantic Versioning](https://semver.org/lang/ko/)을 따릅니다.

## [1.1.1] - 2026-08-23

### Changed

- 라이선스를 MIT에서 GNU General Public License v3.0으로 변경했습니다.
- 패키지 메타데이터의 라이선스 식별자를 `GPL-3.0-only`로 갱신했습니다.
- README에 저작권자와 GPLv3 배포 조건을 명확히 기재했습니다.

## [1.1.0] - 2026-08-15

### Added

- `UVPatternFlow`: **Image 컴포넌트 지원** — 전용 UI 셰이더(`CAT/Effects/UVPatternFlow (UI)`) + uv1/uv2 정점 채널 방식
  - **스프라이트 아틀라스에 포함된 스프라이트 지원** (프래그먼트 frac 반복 — Wrap Mode 불필요)
  - UGUI Mask(스텐실) / RectMask2D(_ClipRect) 호환, Canvas Additional Shader Channels 자동 활성화
- `UVSheetGridFlow`: **Image 컴포넌트 + 아틀라스 시트 지원** (`_OuterUV` 서브영역 샘플링)
- `UVSheetGridFlow` 셰이더: UGUI Mask / RectMask2D 지원 (스텐실 + 클리핑)
- UI 모드 컴포넌트 부착 시 **전용 하위 Canvas 자동 추가** (부모 Canvas 배칭 보호 안전장치)
- 에디터: 모드 표시(RawImage/Image/Sprite), Image·아틀라스 설정 경고, Sprite Mesh Type=Tight 경고 + 원클릭 수정 버튼, 전용 Canvas 추가 버튼

### Fixed

- 플레이 모드에서 인스펙터 변경(UV Rect 등)이 즉시 반영되지 않던 문제
- 에딧 모드에서 값 변경 시 SceneView 갱신이 지연되던 문제
- 모바일 GPU 장시간 재생 시 sin 기반 해시 정밀도 저하로 셀 프레임 선택이 붕괴할 수 있던 문제 (sin-free 해시 + 래핑 주기 2048 조정)

### Changed

- `UVSheetGridFlow`: `RequireComponent(RawImage)` 제거 — RawImage/Image 자동 감지
- 아틀라스 외곽 UV 계산을 스프라이트 변경 시에만 수행하도록 캐싱
- README: 모바일 성능 가이드(전용 Canvas 분리, 드로우콜, 오버드로우) 추가

## [1.0.0] - 2026-08-04

### Added

- `UVPatternFlow`: 패턴 텍스처 UV 스크롤/회전 컴포넌트 (RawImage / SpriteRenderer 양용)
  - UI 모드: IMeshModifier 기반 메시 UV 변환 — SoftMask / SoftMaskLight 자동 호환
  - Sprite 모드: 전용 셰이더 + MaterialPropertyBlock — 공유 Material로 배칭 유지
  - 회전(aspect 보정) → 타일링 → 오프셋 순 UV 변환, Play/Pause/Stop API
- `UVSheetGridFlow`: 스프라이트 시트 기반 그리드 플로우 컴포넌트 (RawImage 전용)
  - 그리드 셀 랜덤 프레임 스위칭 + 무한 스크롤을 프래그먼트 셰이더에서 처리
  - 셀 간격/프레임 인셋/스위칭 주기 설정 지원
- 커스텀 에디터: 에디터 미리보기(EditorAdvance) 지원 인스펙터 2종
- 셰이더: `CAT/Effects/UVPatternFlow (Sprite)`, `CAT/Effects/UVSheetGridFlow`
