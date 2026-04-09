# CheckAlarmNow (알리미)

[![Release](https://img.shields.io/github/v/release/runrun-hani/check-alarm-now)](https://github.com/runrun-hani/check-alarm-now/releases)
[![License](https://img.shields.io/github/license/runrun-hani/check-alarm-now)](LICENSE)

알림이 오면 점점 초조해하다가, 끝내 **앱 아이콘을 대포처럼 화면에 쏘는** Windows 데스크톱 펫입니다.
무시할수록 빨개지고, 커지고, 화면에 균열을 냅니다.

## 주요 기능

### 이중 알림 감지
- **알림 센터**: Windows 토스트 알림 실시간 모니터링 (WinRT UserNotificationListener)
- **태스크바 깜빡임**: 주황색으로 강조되는 앱 감지 (카카오톡, Slack 등)

### 인내심 기반 2단계 경고

| 단계 | 조건 | 동작 |
|------|------|------|
| 평온 (Idle) | 알림 없음 | zzZ... 파랗게 졸고 있음 |
| 주의 (Warn) | 알림 도착 즉시 | 좌우 흔들림 + 주황 틴트 + 말풍선 |
| 격노 (Alert) | 인내심 시간 초과 | 빨간 깜빡임 + 크기 증가 + 사운드 + 아이콘 대포 |
| 확인 완료 (Happy) | 모든 앱 확인 | "잘했어요!" 2초간 기쁜 표정 |

### 아이콘 대포
- 격노 상태에서 **5초마다 1개씩** 앱 아이콘을 화면에 고속 발사
- 직선 궤적으로 날아가 목표 지점에 **즉시 정지 + 균열 이펙트**
- 여러 앱 알림 시 각 앱의 아이콘을 **라운드로빈**으로 발사
- 알림 확인 시 모든 아이콘과 균열 즉시 제거

### 자동 복귀
- 알림이 온 앱의 창을 클릭(포커스)하면 자동으로 대기 상태로 복귀
- 여러 앱 알림 시 **모든 앱을 확인해야** 완전히 대기 상태로 돌아옴
- 앱 실행 전에 이미 있던 알림은 무시 (새 알림만 감지)

### 커스터마이징
- 펫 이미지 교체 (상태별 3종 — PNG/JPG 지원)
- 펫 크기 5단계 조절
- 모니터링 앱 필터 (비워두면 모든 앱 감시)
- 사운드 ON/OFF
- Windows 시작 시 자동 실행
- 설정 자동 저장 (`%APPDATA%/CheckAlarmNow/`)

## 설치 및 실행

### 실행 파일 (권장)

[Releases](https://github.com/runrun-hani/check-alarm-now/releases)에서 최신 `CheckAlarmNow.exe`를 다운로드하여 실행합니다.

> Windows SmartScreen 경고가 표시되면 "추가 정보 → 실행"을 클릭하세요.

### 소스에서 빌드

```bash
dotnet publish src/CheckAlarmNow/CheckAlarmNow.csproj -c Release -r win-x64 --self-contained -o dist
```

**요구사항**: .NET 7.0 SDK, Windows 10 (빌드 17763) 이상

## 사용법

1. 앱 실행 → 데스크톱에 펫이 나타남
2. **드래그**: 좌클릭으로 위치 이동
3. **우클릭**: 설정 / 알림 리셋 / 종료
4. 알림이 오면 펫이 반응, 인내심 시간 초과 시 강하게 경고
5. 해당 앱 창을 클릭하면 자동으로 대기 복귀

## 설정

| 설정 | 설명 | 기본값 |
|------|------|--------|
| 인내심 | 알림 후 격노까지 대기 시간 | 보통 (5분) |
| 모니터링 앱 | 감시할 앱 목록 (비어있으면 전체) | 전체 |
| 펫 크기 | 매우 작게 / 작게 / 보통 / 크게 / 매우 크게 | 보통 |
| 사운드 | 격노 시 사운드 재생 여부 | 켜짐 |
| 자동 시작 | Windows 시작 시 실행 | 꺼짐 |

### 인내심 프리셋

없음(즉시) / 매우 낮음(1분) / 낮음(3분) / **보통(5분)** / 높음(10분) / 매우 높음(20분) / 사용자 지정

## 기술 스택

| 영역 | 기술 |
|------|------|
| 프레임워크 | WPF (.NET 7.0) |
| UI 패턴 | MVVM (CommunityToolkit.Mvvm) |
| 알림 감지 | WinRT UserNotificationListener + Shell Hook (HSHELL_FLASH) |
| 시스템 트레이 | System.Windows.Forms NotifyIcon + Hardcodet.NotifyIcon.Wpf.NetCore |
| 배포 | PublishSingleFile + SelfContained (exe 1개) |

## 라이선스

MIT License
