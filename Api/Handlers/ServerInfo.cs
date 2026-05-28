using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Server.Common;

namespace Server.Api.Handlers
{
    public partial class Handler
    {
        //Get public async Task<IResult> ServerInfo([FromQuery] int version, HttpContext ctx, ...)
        public record ServerInfoRequest(int Version);
        public async Task<IResult> ServerInfo([FromBody] ServerInfoRequest req, HttpContext ctx, [FromServices] AccountDbModule accountDB, [FromServices] IDistributedCache cache)
        {
            /*
            버전 체크
              - 업데이트 강제
              - 검수서버 리다이렉트
            점검중 
            기타 등등
            */
            return Results.Ok(new { message = "ok" });
        }
    }
}
