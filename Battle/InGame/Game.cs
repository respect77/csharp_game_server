using Microsoft.Extensions.Logging;
using Server.Battle.Context;
using Server.Common;

namespace Server.Battle.InGame
{
    public abstract class InGameObject : ICustomObjectPoolable
    {
        protected ILogger _roomLogger { get; private set; }
        public int ObjectId { get; set; }
        public int OwnerUserIndex { get; set; }
        public bool IsDestroyed { get; set; }

        public InGameObject()
        {
            _roomLogger = null!;
        }
        public void Set(Game game, int ownerUserIndex)
        {
            _roomLogger = game.GetRoomLogger;
            ObjectId = game.GetNextObjectId();
            OwnerUserIndex = ownerUserIndex;
            IsDestroyed = false;
        }
        public virtual void Dispose()
        {
            _roomLogger = null!;
        }
    }
    public class Game
    {
        private readonly ILogger _roomLogger;
        public ILogger GetRoomLogger => _roomLogger;
        private DataContext _dataContext = DataContext.Instance;

        public int CurrentFrame { get; private set; } = 0;
        private Dictionary<int/*userIndex*/, Player> _players = new();

        private MonterSpawner _monsterSpawner;
        private int _lastObjectId = 0;
        //object
        private Dictionary<int/*userIndex*/, Dictionary<int/*posIndex*/, Character>> _characters = new();
        private List<Projectile> _projectiles = new();
        private List<Monster> _monsters = new();

        private float _remainBossSpawnCutSceneSec;

        public int GetNextObjectId()
        {
            return ++_lastObjectId;
        }

        public Game(ILogger roomLogger, List<(int UserIndex, PlayerGameInfo GameInfo)> infoList)
        {
            _roomLogger = roomLogger;
            for (int i = 0; i < infoList.Count; ++i)
            {
                var (userIndex, gameInfo) = infoList[i];
                _players[userIndex] = new(this, gameInfo);
            }

            _monsterSpawner = new(CreateMonster);
            _remainBossSpawnCutSceneSec = 0;
        }

        public void Start()
        {
            //TODO init?
        }

        public bool CreateCharacter(int userIndex, int cardId, int posIndex = -1)
        {
            if (!_players.ContainsKey(userIndex))
            {
                _roomLogger.Error("Game CreateCharacter Invalid Player UserIndex: {UserIndex}", userIndex);
                return false;
            }

            if (!_characters.TryGetValue(userIndex, out var userCharacters))
            {
                userCharacters = new();
                _characters[userIndex] = userCharacters;
            }

            if (posIndex == -1)
            {
                var posList = Enumerable.Range(0, Character.GetCellCount()).Except(userCharacters.Keys).ToList();
                if (posList.Count <= 0)
                {
                    _roomLogger.Error("Game CreateCharacter No Available Position UserIndex: {UserIndex}", userIndex);
                    return false;
                }
                posIndex = posList.GetRandomValue();
            }

            if (userCharacters.ContainsKey(posIndex))
            {
                _roomLogger.Error("Game CreateCharacter Position Already Occupied UserIndex: {UserIndex}, PosIndex: {PosIndex}", userIndex, posIndex);
                return false;
            }

            //TODO
            var character = CustomObjectPool<Character>.Get().Set(this, userIndex, cardId, posIndex);
            userCharacters.Add(posIndex, character);
            return true;
        }

        public void CreateProjectile(Character character)
        {
            //TODO 타겟팅 조건에 따라 변경 필요
            var targetMonster = _monsters.Where(m => m.OwnerUserIndex == character.OwnerUserIndex).MaxBy(m => m.MoveDistance);
            if (targetMonster == null)
            {
                //타겟이 없으면 생성 안함
                return;
            }
            var projectile = CustomObjectPool<Projectile>.Get().Set(this, character, targetMonster);
            /*
            시작위치
            타겟 지정
            추가적인 처리(ex:체인이 필요하면 미리 세팅해서)
            */
            _projectiles.Add(projectile);
        }

        public void CreateMonster(int dataId)
        {
            //TODO 여기서 보스일땐 멈춤 / 기존 몬스터 / HP 처리가 필요함
            foreach (var userIndex in _players.Keys)
            {
                var monster = CustomObjectPool<Monster>.Get().Set(this, userIndex, dataId);
                //_monsters.Add(monster.ObjectId, monster);
                _monsters.Add(monster);
            }

            bool boss = false;
            if (boss)
            {
                _remainBossSpawnCutSceneSec = 1f;
            }
        }

        public void MissMonster(int userIndex, int point)
        {
            //TODO 연결
            //점수처리
        }

        public void Update(List<(int, PlayInputClientNotifyPacket)> inputList, FrameEventPacketInfo framePacketInfo)
        {
            /*
            캐릭터
            - 삭제: 누가, 어디
            - 생성: 누가, 어디에, 어떤
            - 강화: 누가, 어떤
            몬스터
            - 생성: 누가
            - 이동종료 제거 -> 하트 처리
            - 파괴 -> 점수 처리
            프로젝타일
            - 생성: 누가, 어떤 캐릭이, 타겟
            - 삭제는 안보냄
            */
            //TODO 보스 연출시 잠시 멈춤 처리가 필요
            if (0 < _remainBossSpawnCutSceneSec)
            {
                _remainBossSpawnCutSceneSec -= Utils.FrameDeltaTime;
                if (0 < _remainBossSpawnCutSceneSec)
                {
                    return;
                }
                //시간이 되었으면 다시 진행
                _remainBossSpawnCutSceneSec = 0;
            }

            foreach (var (userIndex, input) in inputList)
            {
                if (!_players.TryGetValue(userIndex, out var player))
                {
                    _roomLogger.Error("Game Update Invalid Player UserIndex: {UserIndex}", userIndex);
                    continue;
                }
                player.SetInput(input, framePacketInfo);
            }

            _monsterSpawner.Update();

            //TODO update
            /*
            player 입력 처리
            monster 업데이트
            monster 스폰 업데이트?
            character 업데이터 // 타게팅에 연관이 있으니 monster 이후에 해야 할듯
            projectile 업데이트
            */

            foreach (var monster in _monsters)
            {
                monster.Update();
            }

            //TODO spawn monster

            foreach (var characterDic in _characters.Values)
            {
                foreach (var character in characterDic.Values)
                {
                    //여기서 projectile 생성됨
                    character.Update(CreateProjectile);
                }
            }

            foreach (var projectile in _projectiles)
            {
                //여기서 moster 맞고 삭제 처리
                projectile.Update();
                if (projectile.IsDestroyed)
                {
                    CustomObjectPool.Dispose(projectile);
                }
            }
            _projectiles.RemoveAll(p => p.IsDestroyed);

            foreach (var monster in _monsters.Where(m => m.IsDestroyed))
            {
                CustomObjectPool.Dispose(monster);
            }
            _monsters.RemoveAll(m => m.IsDestroyed);


            //게임 종료 체크

            CurrentFrame++;
        }

        public bool Merge(int userIndex, (int FromObjectId, int ToObjectId) mergeInfo)
        {
            if (!_characters.TryGetValue(userIndex, out var characterDict))
            {
                _roomLogger.Error("Game Merge Invalid Player UserIndex: {UserIndex}", userIndex);
                return false;
            }

            if (!characterDict.TryGetValue(mergeInfo.FromObjectId, out var fromCharacter))
            {
                _roomLogger.Error("Game Merge Invalid FromObjectId: {FromObjectId}", mergeInfo.FromObjectId);
                return false;
            }
            if (!characterDict.TryGetValue(mergeInfo.ToObjectId, out var toCharacter))
            {
                _roomLogger.Error("Game Merge Invalid ToObjectId: {ToObjectId}", mergeInfo.ToObjectId);
                return false;
            }
            //TODO merge logic
            return true;
        }
        public void Close()
        {
        }
    }
}
