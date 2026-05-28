using MemoryPack;

namespace Server.Common
{
    public enum ServerKind
    {
        Api,
        Matching,
        Battle,
    }

    [MemoryPackable]
    [MemoryPackUnion(0, typeof(InnerScheduleMessage))]
    [MemoryPackUnion(1, typeof(MatchingRequestMessage))]
    [MemoryPackUnion(2, typeof(MatchingHeartBeatMessage))]
    [MemoryPackUnion(3, typeof(BattleHeartBeatMessage))]
    [MemoryPackUnion(4, typeof(MatchingCancelRequestMessage))]
    [MemoryPackUnion(5, typeof(MatchingBattleRequestMessage))]
    [MemoryPackUnion(6, typeof(UserMatchingStateMessage))]
    [MemoryPackUnion(7, typeof(UserMatchingDoneMessage))]
    public partial interface IBaseMessage
    {
        // get-only 라 MemoryPack 직렬화 대상에서 자동 제외됨
        ServerKind From { get; }
        ServerKind To { get; }
    }

    // Matching → Matching (self-schedule)
    [MemoryPackable]
    public partial class InnerScheduleMessage : IBaseMessage
    {
        public ServerKind From => ServerKind.Matching;
        public ServerKind To => ServerKind.Matching;

        public InnerScheduleMessage() { }
    }

    // Battle → Matching
    [MemoryPackable]
    public partial class BattleHeartBeatMessage : IBaseMessage
    {
        public ServerKind From => ServerKind.Battle;
        public ServerKind To => ServerKind.Matching;

        public int HashCode { get; set; }
        public int UserCount { get; set; }
        public BattleHeartBeatMessage(int hashCode, int userCount)
        {
            HashCode = hashCode;
            UserCount = userCount;
        }
    }

    // Matching → Api
    [MemoryPackable]
    public partial class UserMatchingStateMessage : IBaseMessage
    {
        public ServerKind From => ServerKind.Matching;
        public ServerKind To => ServerKind.Api;

        public UserMatchingStateEnum State { get; set; }
        public List<int> UserIndexList { get; set; }

        public UserMatchingStateMessage(UserMatchingStateEnum state, List<int> userIndexList)
        {
            State = state;
            UserIndexList = userIndexList;
        }
    }

    // Api → Matching
    [MemoryPackable]
    public partial class MatchingRequestMessage : IBaseMessage
    {
        public ServerKind From => ServerKind.Api;
        public ServerKind To => ServerKind.Matching;

        public int UserIndex { get; set; }
        public int Tier { get; set; }
        public int MapId { get; set; }
        public MatchingRequestMessage() { }
    }

    // Api → Matching (heartbeat / 진행상태 요청)
    [MemoryPackable]
    public partial class MatchingHeartBeatMessage : IBaseMessage
    {
        public ServerKind From => ServerKind.Api;
        public ServerKind To => ServerKind.Matching;

        public int UserIndex { get; set; }
        public MatchingHeartBeatMessage(int userIndex)
        {
            UserIndex = userIndex;
        }
    }

    // Api → Matching (매칭 취소)
    [MemoryPackable]
    public partial class MatchingCancelRequestMessage : IBaseMessage
    {
        public ServerKind From => ServerKind.Api;
        public ServerKind To => ServerKind.Matching;

        public int UserIndex { get; set; }
        public MatchingCancelRequestMessage(int userIndex)
        {
            UserIndex = userIndex;
        }
    }

    // Matching → Battle (매칭성공)
    [MemoryPackable]
    public partial class MatchingBattleRequestMessage : IBaseMessage
    {
        public ServerKind From => ServerKind.Matching;
        public ServerKind To => ServerKind.Battle;

        public int HashCode { get; set; }
        public int MapId { get; set; }
        public int Tier { get; set; }
        public int User1Index { get; set; }
        public int User2Index { get; set; }
        public MatchingBattleRequestMessage(int hashCode, int mapId, int tier, int user1Index, int user2Index)
        {
            HashCode = hashCode;
            MapId = mapId;
            Tier = tier;
            User1Index = user1Index;
            User2Index = user2Index;
        }
    }

    // Battle → Api (매칭준비 완료)
    [MemoryPackable]
    public partial class UserMatchingDoneMessage : IBaseMessage
    {
        public ServerKind From => ServerKind.Battle;
        public ServerKind To => ServerKind.Api;

        public int UserIndex { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public UserMatchingDoneMessage() { }
    }
}
