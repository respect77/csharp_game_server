using Server.Common;

namespace Server.Battle.InGame
{
    public class Projectile: InGameObject
    {
        public int DataId { get; set; }
        public Monster? TargetMonster { get; private set; }
        public Vector2 Position { get; set; } = new();
        private float _speed;

        public Projectile Set(Game game, Character character, Monster? targetMonster)
        {
            base.Set(game, character.OwnerUserIndex);
            //DataId = dataId;
            TargetMonster = targetMonster;
            Position = character.Position;
            _speed = 1.1f;
            return this;
        }

        public override void Dispose()
        {
            base.Dispose();
            TargetMonster = null;
        }

        public void Update()
        {
            // 타겟 몬스터가 파괴된 경우 프로젝타일도 파괴
            // 동일 프레임 내에서 처리되므로 풀 재활용 전에 참조가 끊긴다
            if (TargetMonster == null || TargetMonster.IsDestroyed)
            {
                _roomLogger.Info("Projectile {ObjectId} Destroyed - Target lost", ObjectId);
                IsDestroyed = true;
                return;
            }

            var direction = (TargetMonster.Position - Position).Normalize;
            Position += direction * _speed * Utils.FrameDeltaTime;

            //충돌 처리
            // 이동 후 타겟까지의 벡터와 진행 방향의 내적이 음수면 타겟을 지나친 것
            var toTarget = TargetMonster.Position - Position;
            bool overshot = Utils.Dot(direction, toTarget) < 0;

            float checkLengthSq = (float)Math.Pow(2, 2);//프로젝타일과 몬스터 반지름
            if (overshot || Utils.DistanceSq(TargetMonster.Position, Position) <= checkLengthSq)
            {
                _roomLogger.Info("Projectile {ObjectId} hit! TargetMonsterId: {TargetMonsterId}", ObjectId, TargetMonster.ObjectId);
                if (TargetMonster.TakeDamage(3))
                {
                    //TODO 플레이어 재화 획득
                }
                IsDestroyed = true;
            }
        }
    }
}
