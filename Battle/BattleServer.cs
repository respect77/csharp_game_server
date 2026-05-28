using Server.Common;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Server.Battle.Context;
using StackExchange.Redis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Battle.InGame;

namespace Server.Battle
{
    public class BattleServer : BackgroundService
    {
        private readonly TcpListener _listener;
        private bool _isRunning = false;
        // 연결된 클라이언트들을 저장할 ConcurrentDictionary
        private readonly ConcurrentDictionary<int/*socketId*/, ClientContext> _clients = new();
        private readonly ILogger<BattleServer> _logger;
        private readonly ILoggerFactory _loggerFactory;

        private IMessageChannel _messageChannel;
        private string _publicIP = string.Empty;
        private int _port;
        private int _hashCode;

        private readonly RoomManager _roomManager;

        private MySqlModule _gameDB;
        public BattleServer(IOptions<BattleServerSettings> settings, IOptions<ConnectionStrings> connectionSettings, ILogger<BattleServer> logger, ILoggerFactory loggerFactory)
        {
            _port = settings.Value.Port;
            _listener = new TcpListener(IPAddress.Any, _port);
            _logger = logger;
            _loggerFactory = loggerFactory;
            _messageChannel = null!;
            _gameDB = new GameDbModule(connectionSettings.Value.GameDB, loggerFactory.CreateLogger<GameDbModule>());
            _roomManager = new RoomManager(this, loggerFactory.CreateLogger<RoomManager>());
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _messageChannel = new InnerChannel(ServerKind.Battle, RecvMessageFunc, cancellationToken);

            var httpClient = new HttpClient();
            _publicIP = await httpClient.GetStringAsync("https://api.ipify.org", cancellationToken);
            _hashCode = (_publicIP, _port).GetHashCode();

            HeartBeat(cancellationToken);

            _isRunning = true;
            _listener.Start();
            _logger.Info("BattleServer started...");

#if DEBUG
            Task.Run(() =>
            {
                Task.Delay(1000).Wait(); // 서버가 준비될 시간을 잠시 줍니다.
                var test = new Test.BattleClientTest(_loggerFactory.CreateLogger<Test.BattleClientTest>());
            });
#endif

            int clientId = 1;

            while (_isRunning)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                if (!_clients.TryAdd(clientId, new ClientContext(client, clientId, this, _loggerFactory.CreateLogger<ClientContext>())))
                {
                    _logger.Error("Client {ClientId} Exsited", clientId);
                }
                else
                {
                    _logger.Info("Client {ClientId} connected.", clientId);
                }

                clientId++;
            }
        }

#if DEBUG
        static int TestUserIndex = 0;
#endif
        public void RecvPacket(ClientContext clientContext, IBasePacket basePacket)
        {
            switch (basePacket)
            {
                case TestPlayClientPacket testPlayPacket:
                    {
#if DEBUG
                        //동시에 여러 유저가 들어오면 문제가 있으나 일단 테스트 로직이니 패스
                        const int DebugRoomNo = -1;
                        if (clientContext.State != ClientState.NeedVerify)
                        {
                            _logger.Error("Client {Client} Already Verified", clientContext);
                            clientContext.Close();
                            return;
                        }

                        int userIndex = Interlocked.Increment(ref TestUserIndex);

                        var accessToken = Guid.NewGuid().ToString();
                        PlayerGameInfo gameInfo = new()
                        {
                            UserIndex = userIndex,
                            Nickname = $"TestUser{userIndex}",
                            AccessToken = accessToken,
                            CardDecks = testPlayPacket.PlayerInfo.CardDeck, //TODO MemoryPackable 풀링 하려면 복사를 해야 할듯
                        };
                        List<(int UserIndex, PlayerGameInfo GameInfo)> infoList = new() { (userIndex, gameInfo) };
                        if (!_roomManager.TryGetRoom(DebugRoomNo, out var room))
                        {
                            room = new Room(DebugRoomNo, _roomManager, infoList);
                            if (!_roomManager.TryAddRoom(DebugRoomNo, room))
                            {
                                _logger.Error("Room {RoomNo} Already Exsited", DebugRoomNo);
                                clientContext.Close();
                                return;
                            }
                        }
                        else
                        {
                            // 이미 만들어진 방으로 유저 추가
                            if(room.GetRoomState() != RoomStateEnum.Waiting)
                            {
                                _logger.Error("Room {RoomNo} Not in Waiting State", DebugRoomNo);
                                clientContext.Close();
                                return;
                            }
                            room.AddPlayerForTest(userIndex, gameInfo);
                        }

                        _roomManager.RegisterAccessToken(accessToken, userIndex, DebugRoomNo);

                        clientContext.SendPacket(new TestPlayServerPacket() {
                            AccessToken = accessToken,
                        });
#else
                        _logger.Error("Client {ClientId} TestPlayPacket Received in Non-Debug Mode", clientContext.ClientId);
                        clientContext.Close();
#endif
                    }
                    break;
                case VerifyClientPacket verifyPacket:
                    {
                        if (clientContext.State != ClientState.NeedVerify)
                        {
                            _logger.Error("Client {Client} Already Verified", clientContext);
                            clientContext.Close();
                            return;
                        }

                        // 이걸 여기서 지우면 재로그인 할때 애매해짐.. 그럼 언제 지우지? room 종료시점?
                        if (!_roomManager.TryGetAccessInfo(verifyPacket.AccessToken, out var accessInfo))
                        {
                            _logger.Warning("Client {Client} Invalid AccessToken", clientContext);
                            clientContext.Close();
                            return;
                        }

                        if (!_roomManager.TryGetRoom(accessInfo.RoomNo, out var room))
                        {
                            _logger.Error("Client {Client} Invalid RoomNo {RoomNo}", clientContext, clientContext.RoomNo);
                            clientContext.Close();
                            return;
                        }

                        //여기서 락 걸어서 동시요청 제어 필요함
                        lock (_roomManager.VerifyLock)
                        {
                            //중복유저 close 처리
                            _clients.Values.FirstOrDefault(_client => _client.UserIndex == accessInfo.UserIndex && _client.State != ClientState.Closed)?.OverappedClose();
                            clientContext.Verifed(accessInfo.RoomNo, accessInfo.UserIndex);
                        }

                        room.RecvPacket(clientContext, basePacket);
                    }
                    break;
                case HeartBeatClientPacket:
                    {
                        clientContext.SendPacket(new HeartBeatServerPacket());
                    }
                    break;
                case PlugConnectClientPacket:
                    {
                        //TODO PlugConnect 처리
                    }
                    break;
                case ReadyClientNotifyPacket:
                case PlayInputClientNotifyPacket:
                    {
                        if (clientContext.State != ClientState.Verified)
                        {
                            _logger.Error("Client {Client} Not Verified State", clientContext);
                            clientContext.Close();
                            return;
                        }

                        if (!_roomManager.TryGetRoom(clientContext.RoomNo, out var room))
                        {
                            _logger.Error("Client {Client} Invalid RoomNo {RoomNo}", clientContext, clientContext.RoomNo);
                            clientContext.Close();
                            return;
                        }
                        room.RecvPacket(clientContext, basePacket);
                    }
                    break;
                default:
                    {
                        _logger.Error("Client {Client} 알수없는 패킷 타입: {Type}", clientContext, basePacket.GetType().Name);
                        clientContext.Close();
                    }
                    break;
            }
        }

        public void SendPacket(IBasePacket packet, params int[] clientIds)
        {
            foreach (var clientId in clientIds)
            {
                if (!_clients.TryGetValue(clientId, out var clientContext))
                {
                    _logger.Error("Client {ClientId} Not Found SendPacket.", clientId);
                    return;
                }
                clientContext.SendPacket(packet);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _isRunning = false;
            _listener.Stop();

            foreach (var client in _clients.Values)
            {
                client.Close();
            }
            _clients.Clear();
            _logger.Info("Server Stopped.");
            return Task.CompletedTask;
        }

        public void OnClientDisconnted(int clientId)
        {
            if (!_clients.TryRemove(clientId, out var clientContext))
            {
                _logger.Error("Client {ClientId} Not Found Disconnected.", clientId);
                return;
            }
            _logger.Info("Client {ClientId} Disconnected.", clientId);

            if (!_roomManager.TryGetRoom(clientContext.RoomNo, out var room))
            {
                _logger.Error("Client {Client} Invalid RoomNo {RoomNo}", clientContext, clientContext.RoomNo);
                return;
            }
            room.RecvPacket(clientContext);
        }

        private async void HeartBeat(CancellationToken cancellationToken)
        {
            const int scheduleInterval = 1 * 1000;
            while (!cancellationToken.IsCancellationRequested)
            {
                //TODO 매칭서버에 정보 갱신 
                Publish(new BattleHeartBeatMessage(_hashCode, _clients.Count));
                await Task.Delay(scheduleInterval, cancellationToken);
            }
        }
        /*
        public class PlayUserInfoDto
        {
            public int UserIndex { get; set; }
            public string CardListStr { private get; set; } = string.Empty;
            public List<(int CardIndex, int Level)> CardList => string.IsNullOrEmpty(CardListStr) ?
                new() :
                CardListStr.Split(',')
                    .Select(cardStr =>
                    {
                        var parts = cardStr.Split(':');
                        return (CardIndex: int.Parse(parts[0]), Level: int.Parse(parts[1]));
                    })
                    .ToList();
        }
        */
        public class PlayCardDeckDto
        {
            public int CardId { get; set; }
            public int Level { get; set; }
        }
        private async void MakeRoomRequest(MatchingBattleRequestMessage matchingRequestMessage)
        {
            _logger.Info("MakeRoom Request: {User1Index} {User2Index}", matchingRequestMessage.User1Index, matchingRequestMessage.User2Index);

            List<(int UserIndex, PlayerGameInfo GameInfo)> infoList = new();

            int roomNo = _roomManager.NextRoomNo();

            foreach (var userIndex in new[] { matchingRequestMessage.User1Index, matchingRequestMessage.User2Index })
            {
                var (ok, cardList) = await _gameDB.QueryMultiAsync<PlayCardDeckDto>($"select card_index, level from play_card_deck where UserIndex={userIndex};");
                if (!ok || cardList == null)
                {
                    return;
                }

                var accessToken = Guid.NewGuid().ToString();
                _roomManager.RegisterAccessToken(accessToken, userIndex, roomNo);

                PlayerGameInfo gameInfo = new()
                {
                    AccessToken = accessToken,
                    CardDecks = cardList.Select((info) => (info.CardId, info.Level)).ToList(),
                };

                infoList.Add((userIndex, gameInfo));
            }

            if (!_roomManager.TryAddRoom(roomNo, new Room(roomNo, _roomManager, infoList)))
            {
                _logger.Error("Room {RoomNo} Already Exsited", roomNo);
                return;
            }

            foreach (var (userIndex, info) in infoList)
            {
                Publish(new UserMatchingDoneMessage()
                {
                    UserIndex = userIndex,
                    IpAddress = _publicIP,
                    Port = _port,
                    AccessToken = info.AccessToken
                });
            }

            /*
            유저 정보 읽어오고(봇이면 정보 세팅)
            방을 만들고
            유저들 accesstoken 발급 후 PublishExec
            */
        }

        private void Publish<T>(T message) where T : IBaseMessage
        {
            if (message.From != ServerKind.Battle)
            {
                _logger.Error("BattleServer Publish: invalid From={From} (expected Battle), To={To}, type={Type}", message.From, message.To, typeof(T).Name);
                return;
            }
            _messageChannel.Publish(message);
        }

        public bool RecvMessageFunc(IBaseMessage baseMessage)
        {
            switch (baseMessage)
            {
                case MatchingBattleRequestMessage matchingRequestMessage:
                    {
                        if (matchingRequestMessage.HashCode != _hashCode)
                        {
                            //여기로 요청 들어온게 아니다
                            return false;
                        }
                        MakeRoomRequest(matchingRequestMessage);
                    }
                    break;
                default:
                    _logger.Warning("알수없는 메시지 타입: {Type}", baseMessage.GetType().Name);
                    break;
            }
            //일단은 매칭 요청
            //_recvChannel.Writer.TryWrite(message);
            return true;
        }

    }
}
