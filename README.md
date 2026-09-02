# UVPatternFlow

[![openupm](https://img.shields.io/npm/v/com.zzamjak.uvpatternflow?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.zzamjak.uvpatternflow/)
[![license](https://img.shields.io/badge/license-GPL--3.0--only-blue.svg)](Packages/com.zzamjak.uvpatternflow/LICENSE.md)

패턴 텍스처 UV 스크롤/회전 효과(`UVPatternFlow`)와 스프라이트 시트 기반 그리드 플로우 효과(`UVSheetGridFlow`)를 제공하는 Unity UPM 패키지입니다. 스프라이트 아틀라스와 UGUI `Mask`/`RectMask2D`를 지원하며 모바일 최적화를 우선했습니다.

이 레포지토리는 **개발용 Unity 프로젝트**이며, 패키지 본체는
[`Packages/com.zzamjak.uvpatternflow`](Packages/com.zzamjak.uvpatternflow) 에 임베디드되어 있습니다.
버전별 변경 사항은 [CHANGELOG](Packages/com.zzamjak.uvpatternflow/CHANGELOG.md) 를 참고하세요.

## 요구 사항

- Unity 6000.0 (Unity 6) 이상
- uGUI (`com.unity.ugui`) — Unity 기본 내장 모듈입니다
- 그 외 외부 패키지 의존성 없음

---

## 설치 방법

### 1. OpenUPM (권장)

```bash
openupm add com.zzamjak.uvpatternflow
```

또는 `Packages/manifest.json` 에 스코프 레지스트리를 직접 추가합니다.

```json
{
  "scopedRegistries": [
    {
      "name": "zzamjak",
      "url": "https://package.openupm.com",
      "scopes": ["com.zzamjak"]
    }
  ],
  "dependencies": {
    "com.zzamjak.uvpatternflow": "1.1.2"
  }
}
```

`scopes` 는 개별 패키지가 아니라 `com.zzamjak` 스코프 전체로 두는 것을 권장합니다. 그래야 이후 다른 `com.zzamjak.*` 패키지를 추가할 때 설정을 다시 고치지 않아도 되고, Package Manager 의 My Registries 에 `zzamjak` 하나로 묶여 보입니다.

### 2. Git URL 직접 설치

Package Manager → `Install package from git URL...`

```
https://github.com/zzamjak-cloud/UVPatternFlow.git?path=/Packages/com.zzamjak.uvpatternflow#v1.1.2
```

서브폴더 패키지이므로 `?path=` 가 필요합니다.

---

## 사용법

| 컴포넌트 | 대상 | 메뉴 |
|----------|------|------|
| `UVPatternFlow` | `RawImage` · `Image` · `SpriteRenderer` | `Add Component > CAT > ...` |
| `UVSheetGridFlow` | `RawImage` · `Image` | `Add Component > CAT > ...` |

1. 대상 오브젝트에 컴포넌트를 추가합니다.
2. 인스펙터에서 스크롤 속도·회전·타일링 값을 조절합니다.
3. `UVSheetGridFlow` 는 스프라이트 시트의 행·열 수를 지정해 그리드 단위로 흐르게 합니다.

전체 인스펙터 항목과 상세 사용법은
[패키지 README](Packages/com.zzamjak.uvpatternflow/README.md) 를 참고하세요.

## 문서

- [패키지 README — 설치 방법 및 사용법](Packages/com.zzamjak.uvpatternflow/README.md)
- [CHANGELOG](Packages/com.zzamjak.uvpatternflow/CHANGELOG.md)
- [LICENSE (GNU General Public License v3.0)](Packages/com.zzamjak.uvpatternflow/LICENSE.md)

## 라이선스

GNU General Public License v3.0 only (`GPL-3.0-only`).
