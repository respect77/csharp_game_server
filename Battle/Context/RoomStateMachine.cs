using Server.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Battle.Context
{
    public enum RoomStateEnum
    {
        Waiting, // 접속을 기다리고 ready를 기다린다 / 일정시간 접속 안하면 끊고 ai변경
        Ready, //  n초간 기다렸다 시작
        Playing, // 입력을 받고 스케줄 진행
        Finish, // 종료처리
        Closed, // 끝났다
    }

    public class RoomStateWaiting : StateBase<RoomStateEnum>
    {
        private Room _room;
        private DateTime _createDateTime;
        static int _deltaTimeMs = 100;
        static float _waitingTime = 10.0f;
        public RoomStateWaiting(Room room) : base(RoomStateEnum.Waiting)
        {
            _room = room;
        }

        public override bool Enter()
        {
            _createDateTime = DateTime.Now;
            _room.ScheduleDelay();
            return false;
        }

        public override bool Update()
        {
            //모두 verify 하고 ready 해야 함
            bool allReady = _room.AllReady(); //모두 준비했는지 체크
            if (allReady)
            {
                return true;
            }

            if (_waitingTime < (DateTime.Now - _createDateTime).TotalSeconds)
            {
                /*
                TODO 미 진입 유저 AI 교체 및 ready 처리
                아무도 안들어왔으면?
                */
            }

            _room.ScheduleDelay(_deltaTimeMs);
            return false;
        }
    }

    public class RoomStateReady : StateBase<RoomStateEnum>
    {
        private Room _room;
        private DateTime _readyDateTime;
        static int _deltaTimeMs = 100;
        static float _waitingTime = 1.0f;//연출시간
        public RoomStateReady(Room room) : base(RoomStateEnum.Ready)
        {
            _room = room;
        }

        public override bool Enter()
        {
            _readyDateTime = DateTime.Now;
            _room.SendPacketToAll(new StartServerNotifyPacket());
            _room.ScheduleDelay();
            return false;
        }

        public override bool Update()
        {
            if (_waitingTime < (DateTime.Now - _readyDateTime).TotalSeconds)
            {
                return true;
            }

            _room.ScheduleDelay(_deltaTimeMs);
            return false;
        }
    }

    public class RoomStatePlaying : StateBase<RoomStateEnum>
    {
        private Room _room;
        private DateTime _startDateTime;
        public RoomStatePlaying(Room room) : base(RoomStateEnum.Playing)
        {
            _room = room;
        }

        public override bool Enter()
        {
            _startDateTime = DateTime.Now;
            _room.SendPacketToAll(new PlayServerNotifyPacket());
            _room.Start();
            _room.ScheduleDelay();
            return false;
        }

        public override bool Update()
        {
            var updateBeginTime = DateTime.Now;
            TimeSpan interval = updateBeginTime - _startDateTime;
            var targetFrame = (int)Math.Floor(interval.TotalSeconds / Utils.FrameDeltaTime);

            while (_room.CurrentFrame < targetFrame)
            {
                if (_room.FrameUpdate())
                {
                    return true;
                }
            }

            var updateEndTime = DateTime.Now;

            var remainNextFrameTime = _startDateTime.AddSeconds(_room.CurrentFrame * Utils.FrameDeltaTime + 0.001) - updateEndTime;
            int milliSec = Math.Max((int)Math.Ceiling(remainNextFrameTime.TotalMilliseconds), 1);

            _room.ScheduleDelay(milliSec);
            return false;
        }
    }
    public class RoomStateFinish : StateBase<RoomStateEnum>
    {
        private Room _room;
        public RoomStateFinish(Room room) : base(RoomStateEnum.Finish)
        {
            _room = room;
        }

        public override bool Enter()
        {
            /*
            1.
            클라에 종료 노티
            결과 디비 저장
            클라 재접속 정보 api 전송
            클라에 api 접속 정보 전송(accessToken?)
            2.
            클라에 종료 노티
            api에 결과 전송
                api에서 디비 저장
                api에서 클라 재접속 정보 배틀로 전송
            클라에 api 접속 정보 전송(accessToken?)
            */
            //방 정리는?

            return false;
        }

        public override bool Update()
        {
            return false;
        }
    }
}
