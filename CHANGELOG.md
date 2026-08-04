# Changelog

이 프로젝트의 주요 변경 사항을 기록합니다.

포맷은 [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/)를 따르며,
버전은 [Semantic Versioning](https://semver.org/lang/ko/)을 따릅니다.

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
