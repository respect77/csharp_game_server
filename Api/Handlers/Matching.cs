using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Server.Common;

namespace Server.Api.Handlers
{
    public partial class Handler
    {
        public class MatchingRequestDto
        {
            public int UserIndex { get; set; }
            public AccountTypeEnum AccountType { get; set; }
        }
        /*
        매칭 요청을 하면 
        - 세션에 매칭요청중 세팅
        - 매칭서버로 요청 전송

        매칭 heartbeat요청(일반 heartbeat랑 분리)
        - api 서버 상태에 취소/매칭 성공이면 관련 처리
        -  이것도 매칭서버로 전송

        매칭서버에서
        - heartbeat가 왔는데
            - 매칭 중이면 무시
            - 매칭 되었으면?
            - 매칭 취소 되었으면 api로 다시 노티

        - heartbeat가 일정 시간(3초?) 이상 안오면 매칭 취소
        - 매칭이 되면 
        */

        public record MatchingRequest(int ModeType);
        public async Task<IResult> Matching(MatchingRequest req, HttpContext ctx, AccountDbModule db)
        {
            int userIndex = ctx.Session.GetInt32(SessionKey.UserIndex) ?? 0;
            if (userIndex == 0)
            {
                return Results.Unauthorized();
            }

            var currentState = (UserSessionStateEnum)(ctx.Session.GetInt32(SessionKey.UserState) ?? 0);
            //매칭 요청 이미 보냈다
            if (currentState == UserSessionStateEnum.MatchingRequesting)
            {
                return Results.BadRequest(new { message = "이미 매칭중입니다." });
            }

            if (currentState == UserSessionStateEnum.MatchingCanceling)
            {
                return Results.BadRequest(new { message = "매칭 취소중" });
            }
            //TODO var currentServerState = _servetCtx.GetUserMatchingState(userIndex); ??

            _servetCtx.Publish(new MatchingRequestMessage());

            ctx.Session.SetInt32(SessionKey.UserState, (int)UserSessionStateEnum.MatchingRequesting);

            return Results.Ok(new { message = "ok" });
        }

        private IResult CheckMatchingState(int userIndex, HttpContext ctx)
        {
            var currentServerState = _servetCtx.GetUserMatchingState(userIndex);
            switch (currentServerState)
            {
                case UserMatchingStateEnum.MatchingNone: //아직 매칭중 응답 못받음
                case UserMatchingStateEnum.MatchingSuccessed:
                    {
                        //TODO 매칭서버로 주기적으로 보내본다
                        _servetCtx.Publish(new MatchingHeartBeatMessage(userIndex));
                        return Results.Ok(new { message = "매칭 계속 요청중" });
                    }
                case UserMatchingStateEnum.MatchingFailed:
                case UserMatchingStateEnum.MatchingCanceled:
                    {
                        ctx.Session.SetInt32(SessionKey.UserState, (int)UserSessionStateEnum.None);
                        _servetCtx.RemoveUserMatcingState(userIndex);
                        return Results.BadRequest(new { message = "매칭이 취소되었습니다." });
                    }
                case UserMatchingStateEnum.MatchingDone:
                    {
                        //MatchingDone 이상태가 맞나?
                        ctx.Session.SetInt32(SessionKey.UserState, (int)UserSessionStateEnum.MatchingDone);
                        //TODO 세션을 날리는게 맞을듯
                        _servetCtx.RemoveUserMatcingState(userIndex);
                        var accessInfo = _servetCtx.GetAndRemoveMatchingAccessInfo(userIndex);
                        if (accessInfo == null)
                        {
                            return Results.BadRequest(new { message = "매칭 정보가 없습니다." });
                        }
                        //TODO
                        //세션을 날리는게 맞을듯
                        ctx.Session.Clear();
                        //배틀서버 접속정보 내려준다
                        return Results.Ok(new { message = "매칭이 성공되었습니다." , accessInfo });
                    }
                default:
                    {
                        _logger.Error("알수없는 매칭 상태입니다. UserIndex: {UserIndex}, State: {State}", userIndex, currentServerState);
                        return Results.BadRequest(new { message = "알수없는 매칭 상태입니다." });
                    }
            }
        }

        public record MatchingPollingRequest();
        public async Task<IResult> MatchingPolling(MatchingPollingRequest req, HttpContext ctx, AccountDbModule db)
        {
            int userIndex = ctx.Session.GetInt32("UserIndex") ?? 0;
            if (userIndex == 0)
            {
                return Results.Unauthorized();
            }

            var currentState = (UserSessionStateEnum)(ctx.Session.GetInt32(SessionKey.UserState) ?? 0);
            //매칭 보낸 상태가 아니다
            if (currentState != UserSessionStateEnum.MatchingRequesting)
            {
                return Results.BadRequest(new { message = "매칭 요청중이 아님" });
            }

            return CheckMatchingState(userIndex, ctx);
        }

        public record MatchingRequestRequest();
        public async Task<IResult> MatchingCancel(MatchingRequestRequest req, HttpContext ctx, AccountDbModule db)
        {
            int userIndex = ctx.Session.GetInt32("UserIndex") ?? 0;
            if (userIndex == 0)
            {
                return Results.Unauthorized();
            }

            var currentState = (UserSessionStateEnum)(ctx.Session.GetInt32(SessionKey.UserState) ?? 0);
            //매칭 보낸 상태가 아니다
            if (currentState != UserSessionStateEnum.MatchingRequesting)
            {
                return Results.BadRequest(new { message = "매칭 요청중이 아님" });
            }
            ctx.Session.SetInt32(SessionKey.UserState, (int)UserSessionStateEnum.MatchingCanceling);

            return CheckMatchingState(userIndex, ctx);
        }

        public record MatchingCancelPollingRequest();
        public async Task<IResult> MatchingCancelPolling(MatchingCancelPollingRequest req, HttpContext ctx, AccountDbModule db)
        {
            int userIndex = ctx.Session.GetInt32("UserIndex") ?? 0;
            if (userIndex == 0)
            {
                return Results.Unauthorized();
            }

            var currentState = (UserSessionStateEnum)(ctx.Session.GetInt32(SessionKey.UserState) ?? 0);

            if (currentState != UserSessionStateEnum.MatchingCanceling)
            {
                return Results.BadRequest(new { message = "매칭 취소 요청중이 아님" });
            }

            return CheckMatchingState(userIndex, ctx);
        }
    }
}
