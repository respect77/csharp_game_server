using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Common;

namespace Server.Api.Auth
{
    // ASP.NET Core 표준 인증 파이프라인에 맞춰 작성된 커스텀 인증 핸들러.
    // 기존 ApiServer.cs 의 수동 미들웨어 검사 로직을 그대로 옮김:
    //   1) Session 의 UserIndex 확인
    //   2) Redis 에 저장된 (UserIndex → SessionId) 매핑이 현재 SessionId 와 일치하는지 확인
    // 통과하면 "UserIndex" Claim 을 가진 ClaimsPrincipal 을 만들어 HttpContext.User 에 세팅.
    public class SessionAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "SessionAuth";

        public SessionAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            int userIndex = Context.Session.GetInt32(SessionKey.UserIndex) ?? 0;
            if (userIndex == 0)
            {
                // 익명 요청 — Fail 이 아니라 NoResult 로 통과시켜야 AllowAnonymous 엔드포인트가 정상 동작
                return AuthenticateResult.NoResult();
            }

            var cache = Context.RequestServices.GetRequiredService<IDistributedCache>();
            var validToken = await cache.GetStringAsync(ApiServer.GetSessionMappingKey(userIndex));

            if (validToken != Context.Session.Id)
            {
                return AuthenticateResult.Fail("Session mismatch");
            }

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userIndex.ToString()),
                new Claim("UserIndex", userIndex.ToString()),
            }, Scheme.Name);

            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }
    }
}
