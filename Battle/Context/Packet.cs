using MemoryPack;

namespace Server.Battle.Context
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(TestPlayClientPacket))]
    [MemoryPackUnion(1, typeof(TestPlayServerPacket))]

    [MemoryPackUnion(2, typeof(VerifyClientPacket))]
    [MemoryPackUnion(3, typeof(VerifyServerPacket))]

    [MemoryPackUnion(4, typeof(HeartBeatClientPacket))]
    [MemoryPackUnion(5, typeof(HeartBeatServerPacket))]

    [MemoryPackUnion(6, typeof(ReadyClientNotifyPacket))]
    [MemoryPackUnion(7, typeof(StartServerNotifyPacket))]
    [MemoryPackUnion(12, typeof(PlayServerNotifyPacket))]

    [MemoryPackUnion(8, typeof(PlugConnectClientPacket))]
    [MemoryPackUnion(9, typeof(PlugConnectServerPacket))]

    [MemoryPackUnion(10, typeof(PlayInputClientNotifyPacket))]
    [MemoryPackUnion(11, typeof(FrameUpdateServerNotifyPacket))]

    public partial interface IBasePacket { }

    [MemoryPackable]
    public partial class PlayerInfo
    {
        public List<(int CardId, int Level)> CardDeck { get; set; } = new();
        public PlayerInfo() { }
    }
    //배틀 테스트 접속용
    [MemoryPackable]
    public partial class TestPlayClientPacket : IBasePacket
    {
        public int MapId { get; set; }
        public PlayerInfo PlayerInfo { get; set; } = new();
        public TestPlayClientPacket() { }
    }

    [MemoryPackable]
    public partial class TestPlayServerPacket : IBasePacket
    {
        //VerifyClientPacket시 보낼 AccessToken
        public string AccessToken { get; set; } = string.Empty;
        public TestPlayServerPacket() { }
    }

    //인증
    [MemoryPackable]
    public partial class VerifyClientPacket : IBasePacket
    {
        public string AccessToken { get; set; } = string.Empty;
        public VerifyClientPacket() { }
    }

    [MemoryPackable]
    public partial class VerifyServerPacket : IBasePacket
    {
        public int MapId { get; set; }
        public List<PlayerInfo> PlayerInfos { get; set; } = new();
        //재로그인 시에 이전 프레임 정보도 내려줘야 할듯?
        public VerifyServerPacket() { }
    }

    [MemoryPackable]
    public partial class HeartBeatClientPacket : IBasePacket
    {
        public HeartBeatClientPacket() { }
    }

    [MemoryPackable]
    public partial class HeartBeatServerPacket : IBasePacket
    {
        public HeartBeatServerPacket() { }
    }

    //Verify 이후 로딩완료되어 준비되었을 때
    [MemoryPackable]
    public partial class ReadyClientNotifyPacket : IBasePacket
    {
        public ReadyClientNotifyPacket() { }
    }

    //모든 유저가 Ready되면 게임 시작된다(시작 연출 시작)
    [MemoryPackable]
    public partial class StartServerNotifyPacket : IBasePacket
    {
        public StartServerNotifyPacket() { }
    }

    //연출이 끝나고 플레이 시작을 알림
    [MemoryPackable]
    public partial class PlayServerNotifyPacket : IBasePacket
    {
        public PlayServerNotifyPacket() { }
    }

    //와이파이 <-> lte 등의 이슈로 재연결이 필요할때
    [MemoryPackable]
    public partial class PlugConnectClientPacket : IBasePacket
    {
        public string AccessToken { get; set; } = string.Empty;
        public int LastFrameIndex { get; set; }
        public PlugConnectClientPacket() { }
    }

    [MemoryPackable]
    public partial class PlugConnectServerPacket : IBasePacket
    {
        public List<FrameEventPacketInfo> FrameList { get; set; } = new();
        public PlugConnectServerPacket() { }
    }

    public enum InputTypeEnum
    {
        Create,
        Merge,
        Upgrade,
    }
    //게임 플레이 입력
    [MemoryPackable]
    public partial class PlayInputClientNotifyPacket : IBasePacket
    {
        public InputTypeEnum InputType { get; set; }
        public int CreateCardId { get; set; }
        public (int FromPosIndex, int ToPosIndex) MergeInfo { get; set; }
        public int UpgradeCardId { get; set; }
        public PlayInputClientNotifyPacket() { }
    }

    [MemoryPackable]
    public partial class FrameEventPacketInfo
    {
        public int FrameIndex { get; set; } // 0부터
        public List<(int UserIndex, int CardId, int Level)> UpgradeCardList { get; set; } = new(); //업그레이드 카드 리스트
        public List<(int UserIndex, int PosIndex)> RemoveCardList { get; set; } = new(); // 머지, 몬스터 공격으로 사라진 카드 리스트
        public List<(int UserIndex, int PosIndex, int CardId, int Grade/*?*/)> CreateCardList { get; set; } = new();
        public List<(int UserIndex, int MonsterId, int ObjectId)> CreateMonsterList { get; set; } = new();
        public List<int /*ObjectId*/> FinishMonsterList { get; set; } = new();
        public List<(int ObjectId, int DestroyerUserIndex)> DestroyMonsterList { get; set; } = new();
        public List<(int UserIndex, int CardPosIndex, int TargetMonsterObjectId)> CreateProjectileList { get; set; } = new(); //생성된 프로젝타일 오브젝트ID 리스트
        //TODO
        //몬스터 스킬 정보
        //플레이어 골드, 하트 정보?
        //게임종료 정보
        //보스 연출 시작과 끝도 있어야 하지 않을까?
        public bool Finished { get; set; } = false;
    }

    [MemoryPackable]
    public partial class FrameUpdateServerNotifyPacket : IBasePacket
    {
        public FrameEventPacketInfo FrameInfo { get; set; } = new();
        public FrameUpdateServerNotifyPacket() { }
    }

    /*
    c->s ready
    s->c start

    c->s action (create, merge, upgrade)
    s->c frame update (create, merge, upgrade, attack, enemy create, enemy destroy)
    s->c game over
    s->c return to api
    */
}
