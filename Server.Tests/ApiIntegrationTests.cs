using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Server.Tests
{
    public class ApiIntegrationTests : IClassFixture<TestApiFixture>
    {
        private readonly TestApiFixture _fixture;

        public ApiIntegrationTests(TestApiFixture fixture)
        {
            _fixture = fixture;
        }

        // ===== Anonymous endpoints =====

        [Fact]
        public async Task ServerInfo_AllowsAnonymous()
        {
            var client = _fixture.CreateClient();
            var resp = await client.PostAsJsonAsync("/server_info", new { Version = 1 });
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Login_AllowsAnonymous()
        {
            var client = _fixture.CreateClient();
            var resp = await client.PostAsJsonAsync("/login", DefaultLoginBody);
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // ===== Protected endpoints =====

        [Fact]
        public async Task Matching_WithoutLogin_Returns401()
        {
            var client = _fixture.CreateClient();
            var resp = await client.PostAsJsonAsync("/matching", new { ModeType = 1 });
            resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Matching_AfterLogin_Returns200()
        {
            var client = _fixture.CreateClient();
            await Login(client);

            var resp = await client.PostAsJsonAsync("/matching", new { ModeType = 1 });
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task MatchingCancel_WithoutMatchingFirst_Returns400()
        {
            var client = _fixture.CreateClient();
            await Login(client);

            // 매칭 요청 없이 취소 시도 → 세션 상태가 MatchingRequesting 이 아니어서 BadRequest
            var resp = await client.PostAsJsonAsync("/matching_cancel", new { });
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Matching_TwiceInSameSession_SecondReturns400()
        {
            var client = _fixture.CreateClient();
            await Login(client);

            var first = await client.PostAsJsonAsync("/matching", new { ModeType = 1 });
            first.StatusCode.Should().Be(HttpStatusCode.OK);

            var second = await client.PostAsJsonAsync("/matching", new { ModeType = 1 });
            second.StatusCode.Should().Be(HttpStatusCode.BadRequest, "이미 매칭중이라는 응답이 와야 함");
        }

        [Fact]
        public async Task Matching_Cancel()
        {
            var client = _fixture.CreateClient();
            await Login(client);

            var first = await client.PostAsJsonAsync("/matching", new { ModeType = 1 });
            first.StatusCode.Should().Be(HttpStatusCode.OK);

            var second = await client.PostAsJsonAsync("/matching_cancel", new { });
            second.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // ===== Helpers =====

        private static readonly object DefaultLoginBody = new
        {
            SocialType = 1,
            Token = "test-guest-001",
            OsType = 1,
            MarketType = 1,
            PushToken = "dummy",
        };

        private static async Task Login(HttpClient client)
        {
            var resp = await client.PostAsJsonAsync("/login", DefaultLoginBody);
            resp.EnsureSuccessStatusCode();
        }
    }
}
