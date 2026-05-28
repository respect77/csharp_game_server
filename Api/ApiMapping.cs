using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Server.Api.Handlers;
using Server.Common;
using System;
using System.Text;

namespace Server.Api
{
    static class ApiMapping
    {
        public record LoginRequest(string Username, string Password);
        static public void Mapping(WebApplication app, Handler handler)
        {
            app.MapPost("/server_info", handler.ServerInfo).AllowAnonymous();
            app.MapPost("/login", handler.Login).AllowAnonymous();
            // 아래는 FallbackPolicy로 자동 인증 필요 (별도 .RequireAuthorization() 불필요)
            app.MapPost("/matching", handler.Matching);
            app.MapPost("/matching_polling", handler.MatchingPolling);
            app.MapPost("/matching_cancel", handler.MatchingCancel);
            app.MapPost("/matching_cancel_polling", handler.MatchingCancelPolling);

            // POST /get_friend_list
            app.MapPost("/get_friend_list", async (HttpContext ctx, GameDbModule db) =>
            {
                var userId = ctx.Session.GetString("UserId")!;
                var friends = await db.QuerySingleAsync<int>("SELECT ... WHERE UserId=@UserId");
                return Results.Ok(friends);
            });

            /*app.MapGet("/testcache", async (IDistributedCache cache, ILogger<Program> logger) =>
            {
                var key = "mytestkey";
                var value = $"Hello from Cache! Time: {DateTime.UtcNow:O}";

                try
                {
                    logger.LogInformation("테스트: 캐시에 값을 쓰는 중... Key: {Key}, Value: {Value}", key, value);

                    // 1. 캐시에 값 쓰기
                    await cache.SetStringAsync(key, value, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    });

                    logger.LogInformation("테스트: 캐시에 쓰기 성공.");

                    // 2. 캐시에서 값 다시 읽기
                    var retrievedValue = await cache.GetStringAsync(key);
                    logger.LogInformation("테스트: 캐시에서 읽은 값: {RetrievedValue}", retrievedValue);

                    if (value == retrievedValue)
                    {
                        return Results.Ok(new { message = "SUCCESS: Cache write and read successful.", value = retrievedValue });
                    }
                    else
                    {
                        return Results.Problem("FAIL: Value mismatch after write/read.");
                    }
                }
                catch (Exception ex)
                {
                    // Redis 연결 실패 등 예외가 발생하는지 확인
                    logger.LogError(ex, "테스트: 캐시 작업 중 심각한 오류 발생!");
                    return Results.Problem($"ERROR: {ex.Message}");
                }
            });*/
        }
    }
}
