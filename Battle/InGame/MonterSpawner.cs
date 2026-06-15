
namespace Server.Battle.InGame
{
    public delegate void CreateMonsterFunc(int dataId);
    public class MonterSpawner
    {
        private CreateMonsterFunc _createMonsterFunc;
        private float _spawnIntervalSec = 3f;
        private float _remainSpawnSec = 3f;
        public MonterSpawner(CreateMonsterFunc createMonsterFunc)
        {
            _createMonsterFunc = createMonsterFunc;
        }
        public void Update()
        {
            _remainSpawnSec -= Utils.FrameDeltaTime;
            if (_remainSpawnSec <= 0f)
            {
                //TODO 특정 유저에게만 스폰 시켜야 하는 경우도 있음
                _createMonsterFunc(1);
                _remainSpawnSec = _spawnIntervalSec;
            }
        }
    }
}
