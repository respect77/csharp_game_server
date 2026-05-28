using Server.Common;

namespace Server.Battle.InGame
{
    public class Monster : InGameObject
    {
        static private List<Vector2> Path = new() { new(0, 0), new(0, 5), new(5, 5), new(5, 0) };
        public int DataId { get; private set; }
        public Vector2 Position { get; private set; } = new();
        public int Hp { get; private set; }

        private float _speed;
        public float MoveDistance { get; private set; }
        public Monster()
        {
        }

        public Monster Set(Game game, int ownerUserIndex, int dataId)
        {
            base.Set(game, ownerUserIndex);
            DataId = dataId;
            _speed = 1.0f;
            MoveDistance = 0;
            Hp = 10;
            _roomLogger.Info("Monster Set DataId:{DataId} Speed:{Speed}", DataId, _speed);
            return this;
        }
        public override void Dispose()
        {
            base.Dispose();
        }
        public void Update()
        {
            MoveDistance += _speed * Utils.FrameDeltaTime;
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            float distanceRemaining = MoveDistance;

            // 경로의 각 선분(segment)을 순회
            // _path.Count - 1 까지만 반복하여 마지막 점에서 다음 점으로 가는 선분을 처리
            for (int i = 0; i < Path.Count - 1; i++)
            {
                Vector2 startPoint = Path[i];
                Vector2 endPoint = Path[i + 1];

                // 현재 선분의 길이를 계산
                float segmentLength = Utils.Distance(startPoint, endPoint);

                // 남은 이동 거리가 현재 선분 길이보다 작거나 같으면,
                // 목표 지점은 이 선분 위에 있습니다.
                if (distanceRemaining <= segmentLength)
                {
                    // 선분 내에서의 이동 비율(0.0 ~ 1.0)을 계산
                    float t = distanceRemaining / segmentLength;

                    // Vector2.Lerp (선형 보간)를 사용하여 정확한 위치를 찾습니다.
                    // startPoint에서 endPoint 방향으로 t 비율만큼 이동한 지점을 반환
                    Position = Utils.Lerp(startPoint, endPoint, t);
                    return;
                }
                else
                {
                    // 남은 이동 거리가 현재 선분 길이보다 길면,
                    // 이 선분은 완전히 통과한 것이므로, 선분 길이를 빼고 다음 선분으로 넘어갑니다.
                    distanceRemaining -= segmentLength;
                }
            }

            // 모든 선분을 순회했는데도 여기까지 왔다면,
            // 총 이동 거리가 경로의 전체 길이보다 길다는 의미입니다.
            // 따라서 경로의 가장 마지막 점을 반환합니다.
            Position = Path[^1];
            //지나감
            _roomLogger.Info("Monster Reached End Position:{Position}", Position);
            IsDestroyed = true;
        }

        public bool TakeDamage(int damage)
        {
            Hp -= damage;
            if (Hp <= 0)
            {
                Hp = 0;
                IsDestroyed = true;
                _roomLogger.Info("Monster Destroyed DataId:{DataId}", DataId);
                return true;
            }
            return false;
        }
    }
}
