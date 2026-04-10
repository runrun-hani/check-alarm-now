# 설계 문서: 알림 상태 관리 버그 수정

**버전:** 1.0.1  
**날짜:** 2026-04-09

---

## 문제

### 버그 1: 초기화 시 오래된 알림 재해석
- **증상:** 앱 시작 후 2~5초 후 확인할 알림이 없는데도 "확인 좀요~" 메시지 표시
- **원인:** NotificationMonitor의 `_initialized` 플래그가 첫 OnTick에서 toggle되어, 두 번째 tick부터 기존 알림을 새 알림으로 간주

### 버그 2: 포커스 감지 후 알림이 계속 표시
- **증상:** 알림을 보낸 앱 창을 클릭해 확인했는데도 여전히 "확인하라"고 표시
- **원인:** 포커스 감지 후 알림을 `_snoozedIds`에만 기록하고, NotificationCenter에서 완전히 제거하지 않음

---

## 선택한 접근법: B (이상적 버전)

명확한 상태 관리와 정확한 알림 제거를 통해 근본적으로 해결

### 핵심 변경
1. **NotificationMonitor에 `MarkAsRead(id)` 메서드 추가** — 알림을 명시적으로 읽음 처리
2. **AlertManager의 포커스 감지 시 `MarkAsRead()` 호출** — 알림을 확실히 제거
3. **Flash 알림 제거 로직 개선** — syntheticId 기반으로 정확히 제거
4. **초기화 로직 분리** — `_initialized` 플래그를 별도 메서드로 관리

---

## 변경 파일 목록

### 1. `src/CheckAlarmNow/Core/NotificationMonitor.cs`

**변경 내용:**
- `_snoozedIds` HashSet 추가 (AlertManager와 동기화)
- `MarkAsRead(uint id)` 메서드 추가
- `GetUnread()` 반환 값에서 snoozedIds 필터링
- `_initialized` 플래그 처리 개선 (별도 타이밍에서 설정)
- Flash 알림 제거 로직 정확화

**주요 메서드:**
```csharp
// 새로 추가
public void MarkAsRead(uint id)
{
    _snoozedIds.Add(id);
}

public void MarkFlashAsRead(string syntheticId)
{
    lock (_flashUnread)
    {
        _flashUnread.RemoveAll(n => n.Id == syntheticId);
    }
}

// 기존 수정
public List<Notification> GetUnread()
{
    var result = new List<Notification>();
    lock (_unread)
    {
        result.AddRange(_unread.Where(n => !_snoozedIds.Contains(n.Id)));
    }
    // ... Flash도 동일 처리
    return result;
}
```

---

### 2. `src/CheckAlarmNow/Core/AlertManager.cs`

**변경 내용:**
- 포커스 감지 시 `_monitor.MarkAsRead(id)` 호출 (기존 _snoozedIds 제거)
- Flash 알림도 `_monitor.MarkFlashAsRead(syntheticId)` 호출
- MonitoredApps 변경 감지 시 _snoozedIds 초기화
- 스누즈 타임아웃 메커니즘 추가 (선택사항, 5분 이상 스누즈되었으면 자동 해제)

**주요 변경 부분:**
```csharp
// 기존 (줄 112):
foreach (var m in matched)
{
    _snoozedIds.Add(m.Id);  // ← 제거
    _monitor.RemoveFlashNotification(m.AppName);  // ← 제거
}

// 새로 변경:
foreach (var m in matched)
{
    _monitor.MarkAsRead(m.Id);  // ← 새 메서드 호출
}
foreach (var flash in flashMatched)
{
    _monitor.MarkFlashAsRead(flash.Id);  // ← 새 메서드 호출
}
```

**_snoozedIds 제거:**
- AlertManager의 `_snoozedIds` HashSet를 **완전히 제거**
- 모든 스누즈 로직은 NotificationMonitor에서 관리

---

### 3. `src/CheckAlarmNow/Views/PetWindow.xaml.cs` (변경 없음)

기존 "Reset" 우클릭 메뉴 동작은 유지됩니다.

---

## 구현 단계

### 단계 1: NotificationMonitor.cs 수정

**작업:**
1. 클래스 변수에 `private readonly HashSet<uint> _snoozedIds = new();` 추가
2. `MarkAsRead(uint id)` 메서드 추가
3. `MarkFlashAsRead(string syntheticId)` 메서드 추가
4. `GetUnread()` 수정 — snoozedIds로 필터링
5. `_initialized` 플래그 초기화 개선
   - OnTick() 시작 시 체크하고 2초 후에 true로 설정하는 로직으로 변경
   - 또는 별도의 초기화 메서드 제공

**테스트 포인트:**
- 앱 시작 후 시스템에 알림이 있어도 2초 이상 대기 (잘못된 감지 여부)
- MarkAsRead 호출 후 GetUnread()에서 그 알림이 제외되는지 확인

---

### 단계 2: AlertManager.cs 수정

**작업:**
1. `_snoozedIds` 필드 **완전히 제거**
2. OnCheck() 메서드 수정
   - 포커스 감지 시 `_monitor.MarkAsRead(m.Id)` 호출
   - Flash 매칭 후 `_monitor.MarkFlashAsRead(syntheticId)` 호출
3. 스누즈 정리 로직 제거 (줄 75-76)
   ```csharp
   // 제거:
   var currentIds = new HashSet<uint>(unread.Select(n => n.Id));
   _snoozedIds.RemoveWhere(id => !currentIds.Contains(id));
   ```
4. MonitoredApps 변경 감지 (선택사항)
   - Settings 변경 리스너 추가
   - MonitoredApps 변경 시 `_monitor.ClearSnooze()` 호출

**테스트 포인트:**
- 포커스 감지 후 즉시 알림이 사라지는지 확인
- Flash 알림도 정확히 제거되는지 확인

---

### 단계 3: 통합 테스트

**테스트 시나리오:**

| 시나리오 | 기대 동작 | 검증 방법 |
|---------|---------|---------|
| **초기 상태 (기존 알림 있음)** | 확인할 알림 없이 Idle 유지 | 앱 시작 후 5초 대기, 펫이 zzZ 상태 유지 |
| **신규 알림 도착** | Warn 상태 진입 | 카톡/슬랙 메시지 발송, 펫이 흔들림 |
| **포커스 감지 후** | 즉시 Idle 복귀 | 앱 창 클릭, 펫이 즉시 zzZ로 변경 |
| **MonitoredApps 비어있음** | 모든 앱 알림 감시 | 설정에서 모니터링 앱 초기화, 모든 앱의 알림 감지 |

---

## 재사용할 기존 코드

| 파일 | 함수/메서드 | 용도 |
|------|-----------|------|
| NotificationMonitor.cs | `GetUnread()` 구조 | snoozedIds 필터링 로직에 활용 |
| AlertManager.cs | `OnCheck()` 주기 | 기존 포커스 감지 루프 유지 |
| NativeMethods.cs | `GetForegroundProcessId()` | 포커스 감지 용도, 변경 없음 |

---

## 엣지 케이스 / 주의사항

1. **동시성 (Thread Safety):**
   - `_snoozedIds`도 NotificationMonitor 내부이므로 기존 lock 메커니즘 활용
   - AlertManager에서 MarkAsRead 호출할 때 추가 lock 불필요 (내부에서 처리)

2. **알림 ID 충돌:**
   - WinRT 토스트와 Flash는 다른 ID 체계 사용
   - `MarkAsRead()` (WinRT) vs `MarkFlashAsRead()` (Flash) 메서드 분리로 해결

3. **MonitoredApps 필터 변경:**
   - 필터 변경 후 기존 스누즈 상태가 유지될 수 있음
   - 필요 시 `ClearSnooze()` 메서드로 초기화

4. **Happy 상태 타이밍:**
   - MarkAsRead 호출 후도 2초간 Happy 상태 유지 가능
   - 또는 즉시 Idle 복귀 (사용자 경험에 맞게 선택)
   - **현재는 포커스 감지 시 즉시 Idle 복귀로 설정**

---

## 테스트 방법

### 자동 테스트
```csharp
// NotificationMonitor 테스트
[Test]
public void MarkAsRead_RemovesFromGetUnread()
{
    var notif = new Notification { Id = 123, AppName = "Test" };
    monitor._unread.Add(notif);
    
    monitor.MarkAsRead(123);
    
    Assert.That(monitor.GetUnread(), Does.Not.Contain(notif));
}

// AlertManager 테스트
[Test]
public void OnCheck_FocusDetected_CallsMarkAsRead()
{
    var mockMonitor = new Mock<INotificationMonitor>();
    var notif = new Notification { Id = 456 };
    
    alertManager.OnCheck();  // 포커스 감지 후
    
    mockMonitor.Verify(m => m.MarkAsRead(456), Times.Once);
}
```

### 수동 테스트
1. 앱 시작 → 5초 대기 → 펫이 Idle 유지 확인
2. 카톡 메시지 발송 → 펫이 Warn 상태 진입 확인
3. 카톡 앱 창 클릭 → 펫이 즉시 Idle 복귀 확인
4. 설정에서 MonitoredApps 초기화 → 모든 앱의 알림 감지 확인

---

## 열린 질문

1. Happy 상태를 유지할 것인가? (현재는 포커스 감지 시 즉시 Idle 복귀)
   - 사용자: 유지하지 않음 (현재 설정이 맞음)
2. Flash 알림의 syntheticId 생성 방식이 안정적인가?
   - 기존 코드 검토 필요

---

## 변경 요약

| 항목 | 변경 전 | 변경 후 |
|------|--------|--------|
| 스누즈 관리 위치 | AlertManager._snoozedIds | NotificationMonitor._snoozedIds |
| 포커스 감지 처리 | _snoozedIds에만 추가 | Monitor.MarkAsRead() 호출 |
| GetUnread() 필터 | OnCheck에서 필터링 | GetUnread() 내부에서 필터링 |
| 초기화 타이밍 | 첫 OnTick에서 toggle | 별도 메서드로 분리 |

---

이 설계를 기반으로 `/build` 명령으로 구현을 시작하세요.
