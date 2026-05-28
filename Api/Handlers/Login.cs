using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Server.Common;
using System.Text.Json;

namespace Server.Api.Handlers
{
    public partial class Handler
    {
        public class LoginDBResponseDto
        {
            public int UserIndex { get; set; }
            public AccountTypeEnum AccountType { get; set; }
        }

        public class GoogleIdTokenPayload
        {
            public string Sub { get; set; } = string.Empty;      // 구글 유저 ID
            public string Email { get; set; } = string.Empty;    // 이메일
        }

        public record LoginRequest(SocialTypeEnum SocialType, string Token, OsTypeEnum OsType, MarketTypeEnum MarketType, string PushToken);
        public async Task<IResult> Login(
            [FromBody] LoginRequest req,
            HttpContext ctx,
            [FromServices] AccountDbModule accountDB,
            [FromServices] IDistributedCache cache,
            [FromServices] IHttpClientFactory httpClientFactory)
        {
            string SocialId = string.Empty;
            string Email = string.Empty;
            switch (req.SocialType)
            {
                case SocialTypeEnum.Guest:
                    {
                        //게스트는 소셜 아이디를 토큰으로 사용 비어있으면 가입 처리 해야 함
                        SocialId = req.Token;
                    }
                    break;
                case SocialTypeEnum.Google:
                    {
                        // IHttpClientFactory가 핸들러를 풀링/재활용 — Dispose해도 TCP 연결 유지
                        var client = httpClientFactory.CreateClient("Google");
                        var resp = await client.GetAsync($"/tokeninfo?id_token={req.Token}");
                        if (!resp.IsSuccessStatusCode)
                        {
                            return Results.BadRequest(new { message = "Invalid Google token" });
                        }
                        var json = await resp.Content.ReadAsStringAsync();
                        var googleData = JsonSerializer.Deserialize<GoogleIdTokenPayload>(json);
                        if (googleData == null)
                        {
                            return Results.BadRequest();
                        }
                        SocialId = googleData.Sub;
                        Email = googleData.Email;
                    }
                    break;
                default:
                    return Results.BadRequest();
            }

            // ===== [TEMP-NO-DB] DB 호출 임시 비활성화 — 복원 시 이 블록 삭제 =====
            var result = new LoginDBResponseDto
            {
                UserIndex = 1,
                AccountType = AccountTypeEnum.Normal,
            };
            // ===== [TEMP-NO-DB] 끝 =====
            /*
            var param = new DynamicParameters();
            // IN 파라미터
            param.
                AddInputInt("p_social_type", (int)req.SocialType).
                AddInputString("p_social_id", SocialId).
                AddInputString("p_email", Email).
                AddInputString("p_push_token", req.PushToken);

            var (ok, result) = await accountDB.CallProcedureSingle<LoginDBResponseDto>("sp_login", param);
            if (!ok || result == null)
            {
                return Results.Unauthorized();
            }

            if (result.AccountType == AccountTypeEnum.Blocked)
            {
                return Results.StatusCode(StatusCodes.Status400BadRequest);
            }
            */

            if (result.AccountType == AccountTypeEnum.Blocked)
            {
                return Results.StatusCode(StatusCodes.Status400BadRequest);
            }

            bool setSession = await SetSession(result.UserIndex, ctx, cache);
            if (!setSession)
            {
                return Results.StatusCode(StatusCodes.Status400BadRequest);
            }

            //이전에 매칭 중이었으면??
            //TODO 어찌되었건 진입을 했으면 이전 매칭과 연결은 끊는다

            return Results.Ok(new { message = "ok" });
        }

        private async Task<bool> SetSession(int userIndex, HttpContext ctx, IDistributedCache cache)
        {
            try
            {
                var userSessionMapKey = ApiServer.GetSessionMappingKey(userIndex);

                //세션 새로 세팅하기
                await cache.SetStringAsync(userSessionMapKey, ctx.Session.Id, new DistributedCacheEntryOptions
                {
                    //TODO 만료시간 세팅 접속중이면 주기적으로 갱신해야 한다
                    //AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });

                ctx.Session.SetInt32(SessionKey.UserIndex, userIndex);
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "SetSession Error");
                return false;
            }
            return true;
        }
    }
}
