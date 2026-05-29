# Game Backend Server

ASP.NET Core 8 기반 모바일 게임 백엔드. **API / 매칭 / 배틀** 3개 서버를 하나의 호스트에서 BackgroundService 로 실행하며, 인증·로깅·테스트를 표준 .NET 컨벤션으로 구축했습니다.

## 기술 스택

| 영역 | 사용 기술 |
|------|----------|
| 런타임 | .NET 8 / C# 12 |
| 웹 | ASP.NET Core Minimal API |
| 데이터 | MySQL (Dapper + Stored Procedures) |
| 캐시 | Redis (StackExchange.Redis) + ASP.NET Session |
| IPC | MemoryPack 기반 메시지 + Redis Pub/Sub (또는 in-process Channel) |
| 직렬화 | MemoryPack |
| 로깅 | Serilog (Console + File + per-room sink) |
| 테스트 | xUnit + Microsoft.AspNetCore.Mvc.Testing + FluentAssertions |

## 아키텍처

```
                    ┌─────────────────────┐
            HTTPS   │   ASP.NET Core      │   세션 쿠키
   Client ─────────▶│   API Server        │◀──────────
                    │ (BackgroundService) │
                    └─────────────────────┘
                              │ Pub/Sub
                              ▼
   ┌──────────────────┐               ┌──────────────────┐
   │  Matching Server │───매칭완료───▶│   Battle Server  │
   │ (BackgroundService)              │ (BackgroundService) │
   │                  │◀──HeartBeat───│                  │
   └──────────────────┘               └──────────────────┘
                                             │ TCP
                                             ▼
                                          Client
```

- **API Server**: HTTP 엔드포인트 (로그인, 매칭 요청, 매칭 취소). 세션 인증 + Redis 매핑 검증.
- **Matching Server**: 매칭 풀 관리, 일정 시간 후 매칭 성사, 배틀 서버에 룸 생성 요청.
- **Battle Server**: TCP 소켓으로 클라이언트와 실시간 통신. 룸 단위 게임 로직, 프레임 기반 업데이트.

## 주요 설계 결정

### 1. 메시지 라우팅 — 컴파일 타임 타입 안전
모든 서버 간 메시지는 `IBaseMessage` 를 구현. 각 메시지에 `From` / `To` (ServerKind) 를 박아서 **잘못된 채널 발행을 컴파일 타임에 차단**:

```csharp
public partial class MatchingRequestMessage : IBaseMessage
{
    public ServerKind From => ServerKind.Api;
    public ServerKind To   => ServerKind.Matching;
}

_messageChannel.Publish(new MatchingRequestMessage());  // 채널 자동 라우팅
```

### 2. 로깅 — File:Line 컴파일 타임 캡처
`ILogger<T>` 확장 메서드 + `[CallerFilePath]` / `[CallerLineNumber]` 로 **StackTrace 없이** 호출 위치 자동 기록:

```csharp
_logger.Info("Client {ClientId} connected", clientId);
// 출력: [14:30:45.123][INF][BattleServer.cs:71] Client 12 connected
```

- 글로벌 로그: `Logs/YYYY/MM/{api,matching,battle}-{date}.log` — SourceContext 네임스페이스 prefix 로 라우팅
- 룸 로그: `Logs/YYYY/MM/Room{N}_{timestamp}.log` — Room 인스턴스가 자체 Serilog Logger 보유

### 3. 인증 — ASP.NET Core 표준 파이프라인
커스텀 `AuthenticationHandler` 로 세션 쿠키 + Redis 매핑 검증. 라우트 옆에 `.AllowAnonymous()` / `RequireAuthorization()` 으로 정책 선언:

```csharp
app.MapPost("/login", handler.Login).AllowAnonymous();
app.MapPost("/matching", handler.Matching);    // FallbackPolicy 로 자동 인증 필요
```

### 4. 외부 HTTP 호출 — `IHttpClientFactory`
Google OAuth 토큰 검증 등 외부 API 호출은 named client 로 등록. 핸들러 풀링 / DNS 갱신 자동:

```csharp
services.AddHttpClient("Google", c => {
    c.BaseAddress = new Uri("https://oauth2.googleapis.com");
    c.Timeout = TimeSpan.FromSeconds(5);
});
```

### 5. 테스트 — in-memory TestServer 통합 테스트
`Microsoft.AspNetCore.TestHost` + `CookieContainerHandler` 로 실제 HTTP 파이프라인을 in-memory 실행. 라우팅·인증·세션까지 전부 통과:

```csharp
[Fact]
public async Task Matching_WithoutLogin_Returns401()
{
    var client = _fixture.CreateClient();
    var resp = await client.PostAsJsonAsync("/matching", new { ModeType = 1 });
    resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

## 프로젝트 구조

```
Server/
├── Api/                    # ASP.NET Core API 서버
│   ├── ApiServer.cs        # BackgroundService + 미들웨어 구성
│   ├── ApiMapping.cs       # 엔드포인트 라우팅
│   ├── Auth/               # 커스텀 AuthenticationHandler
│   ├── Context/            # ApiServerContext (메시지 수신, 상태 관리)
│   └── Handlers/           # 엔드포인트 핸들러 (partial class)
├── Matching/               # 매칭 서버 (BackgroundService)
├── Battle/                 # 배틀 서버 (TCP, BackgroundService)
│   ├── Context/            # ClientContext, RoomManager, Room
│   └── InGame/             # Game, Player, Monster, Projectile
├── Common/                 # 공유 모듈
│   ├── IpcMessage.cs       # 서버 간 메시지 정의
│   ├── RedisPubSubChannel.cs # 메시지 채널 추상화
│   ├── LogManager.cs       # ILogger 확장 메서드
│   └── MySqlModule.cs      # Dapper 기반 DB 래퍼
└── Server.Tests/           # xUnit 통합 테스트
```

## 실행

### 사전 요구사항
- .NET 8 SDK
- MySQL 8.x
- Redis 7.x (선택 — 멀티 인스턴스 LB 시 필수)

### 빌드 / 실행
```bash
dotnet build
dotnet run --project Server
```

### 테스트
```bash
dotnet test
```

## 학습 / 개선 사례 (선택 섹션)

이 프로젝트를 진행하며 적용한 마이그레이션:

| Before | After | 효과 |
|--------|-------|------|
| 싱글톤 `LogManager.Instance` | DI 기반 `ILogger<T>` + Serilog | 카테고리 필터링, 구조화 로그, 테스트 가능성 |
| 수동 인증 미들웨어 + skip-list | `AuthenticationHandler` + `[AllowAnonymous]` | 정책이 라우트 옆에 선언, Swagger 통합 |
| `new HttpClient()` | `IHttpClientFactory` | 소켓 고갈 / DNS 정체 회피 |
| 채널명 문자열 라우팅 | 메시지 타입의 `From`/`To` 기반 라우팅 | 컴파일 타임 안전성 |
