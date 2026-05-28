
namespace Server.Common
{
    public enum SocialTypeEnum
    {
        None = 0,
        Guest = 1,
        Google = 2,
        Apple = 3,
    }

    public enum OsTypeEnum
    {
        None = 0,
        Android = 1,
        iOS = 2,
    }
    public enum MarketTypeEnum
    {
        None = 0,
        GooglePlay = 1,
        OneStore = 2,
    }

    public enum AccountTypeEnum
    {
        Normal = 0,
        Blocked = 1,
    }

    public static class SessionKey
    {
        public const string UserIndex = "UserIndex";
        public const string UserState = "UserState";
    }

    public enum UserSessionStateEnum
    {
        Verified,
        None,
        MatchingRequesting,
        MatchingCanceling,
        MatchingDone,// 이경우 세션을 지워야 할듯
    }

    public enum UserMatchingStateEnum
    {
        MatchingNone,
        MatchingStarted,
        MatchingSuccessed, //매칭 서버 등록
        MatchingFailed, //매칭 등록 실패
        MatchingCanceled,
        MatchingDone,
    }
}
