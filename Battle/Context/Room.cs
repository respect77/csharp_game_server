using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Server.Common;
using Server.Battle.InGame;

namespace Server.Battle.Context
{
    //Game 생성에 사용되는 플레이어 정보
    public class PlayerGameInfo
    {
        public int UserIndex { get; set; }
        public string Nickname { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public List<(int CardId, int Level)> CardDecks { get; set; } = new();
    }
    public class PlayerRoomInfo
    {
        public enum StateEnum
        {
            None, // 안들어옴
            Verified,
            Ready,
        }
        public int UserIndex { get; private set; }
        public int ClientId { get; private set; }
        public StateEnum State { get; set; } = StateEnum.None;
        public PlayerGameInfo GameInfo { get; private set; }
        public PlayerRoomInfo(int userIndex, PlayerGameInfo gameInfo)
        {
            UserIndex = userIndex;
            GameInfo = gameInfo;
        }
        public void Verified(ClientContext clientContext)
        {
            ClientId = clientContext.ClientId;
            State = StateEnum.Verified;
        }
    }
    public partial class Room
    {
        private int _roomNo;
        private StateMachine<RoomStateEnum> _stateMachine;
        public RoomStateEnum RoomState { get; private set; } = RoomStateEnum.Waiting;
        private IRoomManager _roomManager;
        // 룸 단위 전용 로거 — Serilog Logger를 SerilogLoggerFactory로 감싸서 Microsoft.Extensions.Logging.ILogger로 노출
        // dispose: true 로 factory가 Serilog Logger 소유권 가져가서 file handle 자동 종료
        private readonly ILoggerFactory _loggerFactory;
        private readonly Microsoft.Extensions.Logging.ILogger _roomLogger;
        private DataContext _dataContext = DataContext.Instance;
        private readonly Channel<(ClientContext?, IBasePacket?)> _requestChannel = Channel.CreateUnbounded<(ClientContext?, IBasePacket?)>();
        private ConcurrentQueue<(int/*userIndex?*/, PlayInputClientNotifyPacket)> _inputQueue = new();
        private Dictionary<int/*userIndex*/, PlayerRoomInfo> _players = new();
        private Game _game;
        private readonly CancellationTokenSource _channelCts = new();

        public int CurrentFrame => _game.CurrentFrame;
        public Room(int roomNo, IRoomManager roomManager, List<(int UserIndex, PlayerGameInfo GameInfo)> infoList)
        {
            _roomNo = roomNo;

            //Log
            var now = DateTime.Now;
            var logPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Logs",
                now.ToString("yyyy"),
                now.ToString("MM"),
                $"Room{_roomNo}_{now:yyyy-MM-dd-HH.mm.ss.fff}.log");

            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff}][{Level:u3}][Room" + _roomNo + "][{File}:{Line}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: logPath,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}][{Level:u3}][{File}:{Line}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _loggerFactory = new SerilogLoggerFactory(serilogLogger, dispose: true);
            _roomLogger = _loggerFactory.CreateLogger($"Room{_roomNo}");

            _stateMachine = new(
            [
                new RoomStateWaiting(this),
                new RoomStateReady(this),
                new RoomStatePlaying(this),
                new RoomStateFinish(this)
            ]);

            _roomManager = roomManager;
            foreach (var (userIndex, gameInfo) in infoList)
            {
                _players[userIndex] = new(userIndex, gameInfo);
            }
            _game = new(_roomLogger, infoList);
            RecvChannel();
        }

        public List<PlayerRoomInfo> GetPlayerList() => _players.Values.ToList();
        public RoomStateEnum GetRoomState() => _stateMachine.GetCurrentState();
        public void Close()
        {
            if (RoomState == RoomStateEnum.Closed)
            {
                return;
            }
            RoomState = RoomStateEnum.Closed;
            _requestChannel.Writer.Complete();
            _channelCts.Cancel();
            _loggerFactory.Dispose();   // dispose: true → 내부 Serilog Logger 도 같이 종료
        }

        public void Start()
        {
            _inputQueue.Clear(); // 이전에 눌린 입력은 무시
            _game.Start();
        }

        public bool AllReady() => _players.Values.All(player => player.State == PlayerRoomInfo.StateEnum.Ready);

        public async void ScheduleDelay(int deltaTime = 0)
        {
            if (0 < deltaTime)
            {
                await Task.Delay(deltaTime);
            }

            _requestChannel.Writer.TryWrite(new());
        }

        public bool FrameUpdate()
        {
            List<(int, PlayInputClientNotifyPacket)> inputList = new();

            while (_inputQueue.TryDequeue(out var inputInfo))
            {
                inputList.Add(inputInfo);
            }

            var framePacket = new FrameUpdateServerNotifyPacket()
            {
                FrameInfo = new()
                {
                    FrameIndex = CurrentFrame,
                }
            };
            _game.Update(inputList, framePacket.FrameInfo);
            //TODO 해당 프레임에 나오는 이벤트를 클라에 보내기
            SendPacketToAll(framePacket);
            //TODO 종료시 처리도 필요함
            return framePacket.FrameInfo.Finished;
        }
#if DEBUG
        public bool AddPlayerForTest(int userIndex, PlayerGameInfo gameInfo)
        {
            if (_players.ContainsKey(userIndex))
            {
                return false;
            }
            _players[userIndex] = new(userIndex, gameInfo);
            return true;
        }
#endif
        private void Verify(ClientContext clientContext)
        {
            if (!_players.TryGetValue(clientContext.UserIndex, out var playerInfo))
            {
                _roomLogger.Error("Room {RoomNo} Client {UserIndex} Verify Failed - Not Found", _roomNo, clientContext.UserIndex);
                return;
            }

            if (playerInfo.State != PlayerRoomInfo.StateEnum.None)
            {
                _roomLogger.Error("Room {RoomNo} Client {UserIndex} Verify Failed - Invalid State {State}", _roomNo, clientContext.UserIndex, playerInfo.State);
                return;
            }
            playerInfo.Verified(clientContext);
            //TODO 방상태 알려주기
            clientContext.SendPacket(new VerifyServerPacket() { });
        }
        private void Ready(ClientContext clientContext)
        {
            if (!_players.TryGetValue(clientContext.UserIndex, out var playerInfo))
            {
                _roomLogger.Error("Room {RoomNo} Client {UserIndex} Verify Failed - Not Found", _roomNo, clientContext.UserIndex);
                return;
            }

            if (playerInfo.State != PlayerRoomInfo.StateEnum.Verified)
            {
                _roomLogger.Error("Room {RoomNo} Client {UserIndex} Ready Failed - Invalid State {State}", _roomNo, clientContext.UserIndex, playerInfo.State);
                return;
            }
            playerInfo.State = PlayerRoomInfo.StateEnum.Ready;

            bool allReady = _players.Values.All(player => player.State == PlayerRoomInfo.StateEnum.Ready);
            if (allReady)
            {
                //TODO 시작
            }
        }

        private void Disconnect(ClientContext clientContext)
        {
            //TODO
            _roomLogger.Info("Room {RoomNo} Client {ClientId} Disconnect", _roomNo, clientContext.ClientId);
        }
        private async void RecvChannel()
        {
            try
            {
                await foreach (var (clientContext, packet) in _requestChannel.Reader.ReadAllAsync(_channelCts.Token))
                {
                    if (clientContext == null && packet == null) // schedule
                    {
                        _stateMachine.Update();
                        continue;
                    }

                    if (clientContext == null)
                    {
                        _roomLogger.Error("Room {RoomNo} Invalid ClientContext Null", _roomNo);
                        continue;
                    }
                    switch (packet)
                    {
                        case null: // disconnect
                            {
                                Disconnect(clientContext);
                            }
                            break;
                        case VerifyClientPacket verifyPacket:
                            {
                                Verify(clientContext);
                            }
                            break;
                        case ReadyClientNotifyPacket:
                            {
                                Ready(clientContext);
                            }
                            break;
                        case PlugConnectClientPacket:
                            {
                            }
                            break;
                            /*
                            Verify -> 방정보
                            로딩완료 -> 시작
                            --n 초간 시작 연출을 가정하고 입력을 무시하고 있음
                            게임 입력 - 프레임 정보

                            */
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //종료
                return;
            }
            catch (Exception ex)
            {
                _roomLogger.Error(ex, "ScheduleExec() Error");
            }
            finally
            {
            }
        }

        public void SendPacketToAll<T>(T packet) where T : IBasePacket
        {
            foreach (var player in _players.Values)
            {
                _roomManager.SendPacket(packet, player.ClientId);
            }
        }

        public void RecvPacket(ClientContext clientContext, IBasePacket? basePacket = null)
        {
            if (basePacket is PlayInputClientNotifyPacket playInputPacket)
            {
                _inputQueue.Enqueue((clientContext.UserIndex, playInputPacket));
            }
            else
            {
                _requestChannel.Writer.TryWrite((clientContext, basePacket));
            }
        }
    }
}
