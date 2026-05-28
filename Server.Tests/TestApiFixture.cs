using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Server;
using Server.Api;

namespace Server.Tests
{
    // ApiServer 의 production 흐름(ExecuteAsync)을 그대로 in-memory TestServer 로 구성하는 fixture.
    // ConfigureServices / CreateHandler / ConfigureMiddleware 를 ApiServer 와 공유해서
    // 라우팅 / 인증 / DI 설정 변경 시 테스트도 자동 반영.
    public class TestApiFixture : IAsyncLifetime
    {
        private WebApplication? _app;

        public async Task InitializeAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();   // ← 실제 소켓 대신 in-memory TestServer

            // 테스트용 연결 문자열 — [TEMP-NO-DB] 블록으로 DB 호출 자체 안 함
            var connectionStrings = new ConnectionStrings
            {
                Redis = "",
                AccountDB = "",
                GameDB = "",
            };

            ApiServer.ConfigureServices(builder.Services, connectionStrings);

            _app = builder.Build();
            var handler = ApiServer.CreateHandler(_app.Services, CancellationToken.None);
            ApiServer.ConfigureMiddleware(_app, handler);

            await _app.StartAsync();
        }

        // 매 호출마다 새로운 cookie container 가진 HttpClient 반환 — 테스트 간 세션 격리
        public HttpClient CreateClient()
        {
            if (_app == null) throw new InvalidOperationException("Fixture not initialized");
            var server = _app.GetTestServer();
            var cookieHandler = new CookieContainerHandler { InnerHandler = server.CreateHandler() };
            return new HttpClient(cookieHandler) { BaseAddress = server.BaseAddress };
        }

        public async Task DisposeAsync()
        {
            if (_app != null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }
        }
    }
}
