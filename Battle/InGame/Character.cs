namespace Server.Battle.InGame
{
    public delegate void CreateProjectileFunc(Character character);
    public class Character : InGameObject
    {
        static private Vector2 _characterBeginOffset = new(3, 2);
        static private (float X, float Y) CharacterCellSize = (1f, 1f);
        static private (int XCount, int YCount) CharacterCellMatrix = (5, 3);
        static public int GetCellCount() => CharacterCellMatrix.XCount * CharacterCellMatrix.YCount;
        //public int DataId { get; private set; }
        //public int Level { get; private set; }
        public Vector2 Position { get; set; }
        public int UpgradeLevel { get; set; }
        private float _remainNextAttackSec;
        public Character()
        {
        }

        static private Vector2 GetCharacterPosition(int posIndex)
        {
            int x = posIndex % CharacterCellMatrix.XCount;
            int y = posIndex / CharacterCellMatrix.XCount;

            return new Vector2(
                _characterBeginOffset.X + (x * CharacterCellSize.X) + (CharacterCellSize.X / 2f),
                _characterBeginOffset.Y + (y * CharacterCellSize.Y) + (CharacterCellSize.Y / 2f)
            );
        }

        public Character Set(Game game, int userIndex, int cardId, int posIndex)
        {
            base.Set(game, userIndex);
            //DataId = dataId;
            //Level = level;
            Position = GetCharacterPosition(posIndex);
            _remainNextAttackSec = 0f;
            return this;
        }
        public override void Dispose()
        {
            base.Dispose();
        }
        public void Update(CreateProjectileFunc createProjectileFunc)
        {
            _remainNextAttackSec -= Utils.FrameDeltaTime;
            if(_remainNextAttackSec < 0f)
            {
                //Attack
                createProjectileFunc(this);
                _remainNextAttackSec = 1f;
            }
        }
    }
}
