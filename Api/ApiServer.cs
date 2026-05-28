using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Api.Auth;
using Server.Api.Context;
using Server.Api.Handlers;
using Server.Common;

namespace Server.Api
{
    public class ApiServer : BackgroundService
    {
        private WebApplication? _app;
        private readonly string[] _urls;
        private ConnectionStrings _connectionString;
        private readonly ILogger<ApiServer> _logger;

        public const string UserSessionMappingPrefix = "Mapping_Prefix_";
        public const string SessionPrefix = "ApiServer_";

        public Handler Handler { get; private set; }

        public ApiServer(IOptions<ApiServerSettings> settings, IOptions<ConnectionStrings> connectionSettings, ILogger<ApiServer> logger)
        {
            _urls = [settings.Value.HttpUrl, settings.Value.HttpsUrl];
            _connectionString = connectionSettings.Value;
            _logger = logger;
            Handler = null!;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var builder = WebApplication.CreateBuilder();
            ConfigureServices(builder.Services, _connectionString);
            builder.WebHost.UseUrls(_urls);

            _app = builder.Build();
            Handler = CreateHandler(_app.Services, cancellationToken);
            ConfigureMiddleware(_app, Handler);

            _logger.Info("ApiServer Started...");
            await _app.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return _app?.StopAsync(cancellationToken) ?? Task.CompletedTask;
        }

        public static string GetSessionMappingKey(int userIndex) => $"{UserSessionMappingPrefix}{userIndex}";

        // ---- 테스트와 공유되는 구성 메서드들 ----

        public static void ConfigureServices(IServiceCollection services, ConnectionStrings connectionString)
        {
            //메모리 캐시 (Session/DistributedCache 백엔드)
            services.AddDistributedMemoryCache();
            /*
            Redis 캐시
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = connectionString.Redis;
                options.InstanceName = SessionPrefix;
            });
            */

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                //options.Cookie.IsEssential = true;
                //options.Cookie.HttpOnly = true;
            });

            services.AddScoped(sp => new AccountDbModule(connectionString.AccountDB, sp.GetRequiredService<ILogger<AccountDbModule>>()));
            services.AddScoped(sp => new GameDbModule(connectionString.GameDB, sp.GetRequiredService<ILogger<GameDbModule>>()));

            // HttpClient — IHttpClientFactory 등록 (핸들러 풀링/DNS 갱신)
            services.AddHttpClient("Google", c =>
            {
                c.BaseAddress = new Uri("https://oauth2.googleapis.com");
                c.Timeout = TimeSpan.FromSeconds(5);
            });

            // 인증/인가
            services
                .AddAuthentication(SessionAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, SessionAuthHandler>(SessionAuthHandler.SchemeName, _ => { });

            services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder(SessionAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build());

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
        }

        public static Handler CreateHandler(IServiceProvider services, CancellationToken cancellationToken)
        {
            var serverContext = new ApiServerContext(cancellationToken, services.GetRequiredService<ILogger<ApiServerContext>>());
            return new Handler(serverContext, services.GetRequiredService<ILogger<Handler>>());
        }

        // 미들웨어 순서: Session → Authentication → Authorization → Endpoints
        // Swagger 는 일반 미들웨어라서 Authorization 앞에 두면 인증 없이 접근 가능.
        public static void ConfigureMiddleware(WebApplication app, Handler handler)
        {
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            ApiMapping.Mapping(app, handler);
        }
    }
}
