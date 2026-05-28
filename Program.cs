namespace Server
{
    using Api;
    using Battle;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Serilog;
    using Server.Common;
    using Server.Matching;

    public class ServerSettings
    {
        public ApiServerSettings ApiServer { get; set; } = new();
        public BattleServerSettings BattleServer { get; set; } = new();
        public MatchingServerSettings MatchingServer { get; set; } = new();
        //public ConnectionStrings ConnectionStrings { get; set; } = new();
        public string DataPath { get; set; } = string.Empty;
    }

    public class ConnectionStrings
    {
        public string Redis { get; set; } = string.Empty;
        public string AccountDB { get; set; } = string.Empty;
        public string GameDB { get; set; } = string.Empty;
    }

    public class ApiServerSettings
    {
        public bool UseApiServer { get; set; } = false;
        public string HttpUrl { get; set; } = string.Empty;
        public string HttpsUrl { get; set; } = string.Empty;
    }

    public class BattleServerSettings
    {
        public bool UseBattleServer { get; set; } = false;
        public int Port { get; set; }
    }

    public class MatchingServerSettings
    {
        public bool UseMatchingServer { get; set; } = false;
    }
    class Program
    {
        static async Task Main(string[] args)
        {
            ConfigureSerilog();

            try
            {
                await RunHost();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        // 호스트 시작 전에 Serilog 부트스트랩 — 시작 단계 로그까지 캡처
        static void ConfigureSerilog()
        {
            var bootstrapConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            // Logs/YYYY/MM/ 폴더 기준 — 서버 시작 시점의 년/월. 월 경계 넘어가도 동일 폴더.
            var now = DateTime.Now;
            var logBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", now.ToString("yyyy"), now.ToString("MM"));

            const string fileTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}][{Level:u3}][{File}:{Line}]{UserIdPrefix} {Message:lj}{NewLine}{Exception}";
            const string consoleTemplate = "[{Timestamp:HH:mm:ss.fff}][{Level:u3}][{File}:{Line}]{UserIdPrefix} {Message:lj}{NewLine}{Exception}";

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(bootstrapConfig)              // MinimumLevel만 외부 설정에서 읽음
                // 콘솔: 모든 로그 표시 — File/Line은 LoggerExtensions(Info/Warning/Error/Debug)가 컴파일 타임 캡처 후 BeginScope로 첨부
                .WriteTo.Console(outputTemplate: consoleTemplate)
                // 파일: SourceContext 네임스페이스로 분리
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => SourceContextStartsWithAny(e, "Server.Api", "Server.Common", "Microsoft.AspNetCore"))
                    .WriteTo.File(
                        path: Path.Combine(logBase, "api-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate: fileTemplate))
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => SourceContextStartsWithAny(e, "Server.Matching", "Server.Common"))
                    .WriteTo.File(
                        path: Path.Combine(logBase, "matching-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate: fileTemplate))
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => SourceContextStartsWithAny(e, "Server.Battle", "Server.Common"))
                    .WriteTo.File(
                        path: Path.Combine(logBase, "battle-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate: fileTemplate))
                .CreateLogger();
        }

        static async Task RunHost()
        {
            var host = new HostBuilder().ConfigureAppConfiguration((hostContext, config) =>
            {
                // 기존의 ConfigurationBuilder 로직을 여기에...
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                // ...
            })
            .UseSerilog() // Microsoft.Extensions.Logging.ILogger를 Serilog로 라우팅
            .ConfigureServices((hostContext, services) =>
            {
                var configuration = hostContext.Configuration;

                // 1. 설정을 IOptions 패턴으로 등록 (강력히 권장)
                services.Configure<ServerSettings>(configuration.GetSection("ServerSettings"));
                services.Configure<ApiServerSettings>(configuration.GetSection("ServerSettings:ApiServer"));
                services.Configure<BattleServerSettings>(configuration.GetSection("ServerSettings:BattleServer"));
                services.Configure<MatchingServerSettings>(configuration.GetSection("ServerSettings:MatchingServer"));
                services.Configure<ConnectionStrings>(configuration.GetSection("ConnectionStrings"));
                // 2. 다른 서비스들 등록 (로거, DB 모듈 등)

                var dataPath = configuration.GetValue<string>("DataPath") ?? throw new Exception("DataPath 설정이 없습니다.");
                DataContext.Instance.LoadData(dataPath);

                //services.AddLogging(builder => builder.AddConsole()); // 예시 로깅

                // 3. 설정 파일에 따라 필요한 서버(HostedService)를 조건부로 등록
                var serverSettings = configuration.GetSection("ServerSettings").Get<ServerSettings>() ?? throw new Exception("ServerSettings 설정이 없습니다.");
                if (serverSettings.ApiServer.UseApiServer)
                {
                    services.AddHostedService<ApiServer>();
                }
                if (serverSettings.BattleServer.UseBattleServer)
                {
                    services.AddHostedService<BattleServer>();
                }
                if (serverSettings.MatchingServer.UseMatchingServer)
                {
                    services.AddHostedService<MatchingServer>();
                }
            }).
            Build();

            await host.RunAsync(); // 이 한 줄이 시작, Ctrl+C 처리, 종료까지 모두 관리해줌
        }

        // 파일별 라우팅용 SourceContext prefix 매칭
        static bool SourceContextStartsWithAny(Serilog.Events.LogEvent e, params string[] prefixes)
        {
            if (!e.Properties.TryGetValue("SourceContext", out var v)) return false;
            if (v is not Serilog.Events.ScalarValue sv || sv.Value is not string sc) return false;
            foreach (var p in prefixes)
            {
                if (sc.StartsWith(p)) return true;
            }
            return false;
        }
    }
}
