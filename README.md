# Game Backend Server

ASP.NET Core 8 기반 모바일 게임 백엔드. **API / 매칭 / 배틀** 3개 서버를 하나의 호스트에서 BackgroundService 로 실행가능

## 기술 스택

| 영역 | 사용 기술 |
|------|----------|
| 런타임 | .NET 8 / C# 12 |
| 웹 | ASP.NET Core Minimal API |
| 디비 | MySQL (Dapper + Stored Procedures) |
| 캐시 | Redis (StackExchange.Redis) + ASP.NET Session |
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


