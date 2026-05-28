using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Server.Common
{
    // ILogger 확장 메서드 모음. [CallerFilePath]/[CallerLineNumber] 로 컴파일 타임에 호출 위치를 캡처하고,
    // BeginScope 로 Serilog property("File", "Line") 에 첨부함.
    // 호출부 패턴:
    //   _logger.Info(...) / Warning(...) / Error(...) / Error(ex, ...) / Debug(...)
    // StackTrace 비용 없음. 글로벌 outputTemplate 의 {File}:{Line} 가 자동으로 채워짐.
    public static partial class ExtendsMethod
    {
        private static IDisposable? BeginCaller(ILogger logger, string file, int line) =>
            logger.BeginScope(new[]
            {
                new KeyValuePair<string, object?>("File", Path.GetFileName(file)),
                new KeyValuePair<string, object?>("Line", line),
            });

        // ===== Info =====
        public static void Info(this ILogger logger, string template,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogInformation(template);
        }
        public static void Info(this ILogger logger, string template, object? arg0,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogInformation(template, arg0);
        }
        public static void Info(this ILogger logger, string template, object? arg0, object? arg1,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogInformation(template, arg0, arg1);
        }
        public static void Info(this ILogger logger, string template, object? arg0, object? arg1, object? arg2,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogInformation(template, arg0, arg1, arg2);
        }

        // ===== Warning =====
        public static void Warning(this ILogger logger, string template,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogWarning(template);
        }
        public static void Warning(this ILogger logger, string template, object? arg0,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogWarning(template, arg0);
        }
        public static void Warning(this ILogger logger, string template, object? arg0, object? arg1,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogWarning(template, arg0, arg1);
        }
        public static void Warning(this ILogger logger, string template, object? arg0, object? arg1, object? arg2,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogWarning(template, arg0, arg1, arg2);
        }

        // ===== Error (no exception) =====
        public static void Error(this ILogger logger, string template,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogError(template);
        }
        public static void Error(this ILogger logger, string template, object? arg0,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogError(template, arg0);
        }
        public static void Error(this ILogger logger, string template, object? arg0, object? arg1,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogError(template, arg0, arg1);
        }
        public static void Error(this ILogger logger, string template, object? arg0, object? arg1, object? arg2,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogError(template, arg0, arg1, arg2);
        }

        // ===== Error (with exception) =====
        public static void Error(this ILogger logger, Exception ex, string template,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogError(ex, template);
        }
        public static void Error(this ILogger logger, Exception ex, string template, object? arg0,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogError(ex, template, arg0);
        }
        public static void Error(this ILogger logger, Exception ex, string template, object? arg0, object? arg1,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogError(ex, template, arg0, arg1);
        }
        public static void Error(this ILogger logger, Exception ex, string template, object? arg0, object? arg1, object? arg2,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogError(ex, template, arg0, arg1, arg2);
        }

        // ===== Debug =====
        public static void Debug(this ILogger logger, string template,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogDebug(template);
        }
        public static void Debug(this ILogger logger, string template, object? arg0,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogDebug(template, arg0);
        }
        public static void Debug(this ILogger logger, string template, object? arg0, object? arg1,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogDebug(template, arg0, arg1);
        }
        public static void Debug(this ILogger logger, string template, object? arg0, object? arg1, object? arg2,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            using var _ = BeginCaller(logger, file, line);
            logger.LogDebug(template, arg0, arg1, arg2);
        }
    }
}
