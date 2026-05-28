using Server.Common;
using System.Threading.Channels;
using StackExchange.Redis;
using MemoryPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Server.Matching
{
    public class MatchingUserInfo
    {
        public int UserIndex { get; private set; }
        public DateTime MatchingExecTime { get; private set; }

        public MatchingUserInfo(int userIndex)
        {
            UserIndex = userIndex;
            //매칭 시작 후 n초 후에 매칭되도록 한다
            MatchingExecTime = DateTime.Now.AddSeconds(Utils.GetRandomValue(3, 10));
        }
    }
    public class MatchingServer : BackgroundService
    {
        public record MatchingKey(int MapId, int Tier);
        private IMessageChannel _messageChannel;
        private readonly ILogger<MatchingServer> _logger;
        private DataContext _dataContext = DataContext.Instance;
        private Channel<IBaseMessage> _recvMessageChannel = Channel.CreateUnbounded<IBaseMessage>();

        private Dictionary<MatchingKey, LinkedList<MatchingUserInfo>> _matchingPool = new();
        private Dictionary<int /*UserIndex*/, MatchingKey> _matchingUsers = new();

        //매칭 상태를 벗어난 유저들 기록
        private Dictionary<int, (UserMatchingStateEnum State, int HashCode)> _matchLeaveUsers = new();

        //최근 매칭된 유저들 기록
        private Dictionary<int, FixedSizedQueue<int>> _recentMatchedUsersHistory = new();

        private Dictionary<int, (int UserCount, DateTime LastUpdateDate)> _battleServers = new();
        public MatchingServer(ILogger<MatchingServer> logger)
        {
            _logger = logger;
            _messageChannel = null!;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _messageChannel = new InnerChannel(ServerKind.Matching, RecvMessageFunc, cancellationToken);
            _logger.Info("MatchingServer Started...");
            await RecvScheduleExec(cancellationToken);
            
        }
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Info("Server Stopped.");
            await _messageChannel.UnsubscribeAsync();
            return;
        }
        private async Task RecvScheduleExec(CancellationToken cancellationToken)
        {
            async void InnerRecvSchedule()
            {
                const int scheduleInterval = 1 * 1000;
                var heartBeatMessage = new InnerScheduleMessage();
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(scheduleInterval, cancellationToken);
                    _recvMessageChannel.Writer.TryWrite(heartBeatMessage);
                }
            }
            InnerRecvSchedule();
            try
            {
                await foreach (var message in _recvMessageChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    switch (message)
                    {
                        case InnerScheduleMessage:
                            {
                                MatchingExec();
                            }
                            break;
                        case BattleHeartBeatMessage battleHeartBeat:
                            {
                                //배틀서버로부터 주기적으로 상태를 받는다
                                //나중애 여러대의 배틀서버가 존재한다면 유저수에 따른 매칭을 고려해야 한다
                                _battleServers[battleHeartBeat.HashCode] = (battleHeartBeat.UserCount, DateTime.Now);
                            }
                            break;
                        case MatchingRequestMessage matchingRequest:
                            {
                                if (_matchingUsers.ContainsKey(matchingRequest.UserIndex))
                                {
                                    _logger.Error("이미 매칭중인 유저가 매칭 요청을 보냈습니다. UserIndex: {UserIndex}", matchingRequest.UserIndex);
                                    Publish(new UserMatchingStateMessage(UserMatchingStateEnum.MatchingFailed, new() { matchingRequest.UserIndex }));
                                    continue;
                                }
                                var data = new MatchingKey(matchingRequest.MapId, matchingRequest.Tier);

                                _matchingUsers[matchingRequest.UserIndex] = data;

                                if (!_matchingPool.TryGetValue(data, out var matchingList))
                                {
                                    matchingList = new();
                                    _matchingPool[data] = matchingList;
                                }
                                matchingList.AddLast(new MatchingUserInfo(matchingRequest.UserIndex));
                                Publish(new UserMatchingStateMessage(UserMatchingStateEnum.MatchingSuccessed, new() { matchingRequest.UserIndex }));
                            }
                            break;
                        case MatchingHeartBeatMessage matchingHeartBeat:
                            {
                                if (!_matchingUsers.ContainsKey(matchingHeartBeat.UserIndex))
                                {
                                    //TODO 이미 배틀서버에 매칭 요청을 보냈던가 취소되었음
                                    continue;
                                }
                                //TODO 매칭중 유저 heartbeat도 매칭 서버로 보내서 연걸 상태를 확인하도록 하자
                            }
                            break;
                        case MatchingCancelRequestMessage matchingCancelRequest:
                            {
                                int userIndex = matchingCancelRequest.UserIndex;
                                if (!_matchingUsers.TryGetValue(userIndex, out var matchingInfo))
                                {
                                    if (!_matchLeaveUsers.TryGetValue(userIndex, out var leaveValue))
                                    {
                                        _logger.Error("Not exists _matchLeaveUsers");
                                        continue;
                                    }

                                    var (leaveState, battleServerHashCode) = leaveValue;

                                    switch (leaveState)
                                    {
                                        case UserMatchingStateEnum.MatchingCanceled:
                                            {
                                                _logger.Info("이미 매칭이 취소된 유저가 매칭 취소 요청을 보냈습니다. UserIndex: {UserIndex}", userIndex);
                                                Publish(new UserMatchingStateMessage(UserMatchingStateEnum.MatchingCanceled, new() { userIndex }));
                                            }
                                            break;
                                        case UserMatchingStateEnum.MatchingDone:
                                            {
                                                /*
                                                TODO
                                                1안
                                                - 배틀서버로 보내서 api로 알려주게 한다
                                                2안
                                                - 그냥 여기서 끊어버린다?
                                                */
                                            }
                                            break;
                                        default:
                                            {
                                                _logger.Error("알수없는 매칭 상태의 유저가 매칭 취소 요청을 보냈습니다. UserIndex: {UserIndex}, State: {State}", userIndex, leaveState);
                                            }
                                            break;
                                    }
                                    continue;
                                }
                                if (!_matchingPool.TryGetValue(matchingInfo, out var matchingList))
                                {
                                    _logger.Error("매칭 큐에 없는 유저가 매칭 취소 요청을 보냈습니다. UserIndex: {UserIndex}", userIndex);
                                    continue;
                                }

                                _matchingUsers.Remove(userIndex);
                                var nodeToRemove = matchingList.FirstOrDefault(x => x.UserIndex == userIndex);
                                if (nodeToRemove != null)
                                {
                                    matchingList.Remove(nodeToRemove);
                                }

                                _matchLeaveUsers[userIndex] = (UserMatchingStateEnum.MatchingCanceled, 0);
                                Publish(new UserMatchingStateMessage(UserMatchingStateEnum.MatchingCanceled, new() { userIndex }));
                            }
                            break;
                        default:
                            _logger.Warning("알수없는 메시지 타입: {Type}", message.GetType().Name);
                            break;
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
                _logger.Error(ex, "ScheduleExec() Error");
            }
            finally
            {
            }
        }

        private void MatchingExec()
        {
            int? battleServerHashCode = GetMatchingBattleServer();
            if (battleServerHashCode is null)
            {
                _logger.Warning("No Available Battle Server for Matching");
                return;
            }
            var now = DateTime.Now;
            foreach (var (key, userList) in _matchingPool)
            {
                while (true)
                {
                    //TODO 우선은 1인 매칭을 진행
                    var firstUser = userList.First?.Value;

                    if (firstUser == null || now < firstUser.MatchingExecTime)
                    {
                        break;
                    }

                    userList.RemoveFirst();
                    _matchingUsers.Remove(firstUser.UserIndex);
                    Matched(battleServerHashCode.Value, key, firstUser);

                    /*
                    
                    var firstUser = userList.FirstValue();

                    if (firstUser == null || firstUser.MatchingExecTime < now)
                    {
                        break;
                    }

                    userList.RemoveFirst();

                    var nextUser = userList.FirstValue();


                    if (nextUser == null)
                    {
                        Matched(battleServerHashCode.Value, MapId, Tier, firstUser);
                    }
                    else
                    {
                        if (MatchAvailable(firstUser.UserIndex, nextUser.UserIndex) == false)
                        {
                            Matched(battleServerHashCode.Value, MapId, Tier, firstUser);
                        }
                        else
                        {
                            userList.RemoveFirst();
                            Matched(battleServerHashCode.Value, MapId, Tier, firstUser, nextUser);
                        }
                    }
                    */
                }
            }
        }

        private bool MatchAvailable(int userIndex, int targetUserIndex)
        {
            if (!_recentMatchedUsersHistory.TryGetValue(userIndex, out var userRecentQueue))
            {
                userRecentQueue = new(3);
                _recentMatchedUsersHistory[userIndex] = userRecentQueue;
            }

            if (!_recentMatchedUsersHistory.TryGetValue(targetUserIndex, out var targetUserRecentQueue))
            {
                targetUserRecentQueue = new(3);
                _recentMatchedUsersHistory[targetUserIndex] = targetUserRecentQueue;
            }

            return !userRecentQueue.Contains(targetUserIndex) && targetUserRecentQueue.Contains(userIndex);
        }

        private void Matched(int battleServerHashCode, MatchingKey matchKey, MatchingUserInfo user1, MatchingUserInfo? user2 = null)
        {
            _matchLeaveUsers[user1.UserIndex] = (UserMatchingStateEnum.MatchingDone, battleServerHashCode);
            if (user2 != null)
            {
                _matchLeaveUsers[user2.UserIndex] = (UserMatchingStateEnum.MatchingDone, battleServerHashCode);

                _recentMatchedUsersHistory[user1.UserIndex].Enqueue(user2.UserIndex);
                _recentMatchedUsersHistory[user2.UserIndex].Enqueue(user1.UserIndex);
            }
            Publish(new MatchingBattleRequestMessage(battleServerHashCode, matchKey.MapId, matchKey.Tier, user1.UserIndex, user2?.UserIndex ?? -1));
        }

        private int? GetMatchingBattleServer()
        {
            var threeSecondsAgo = DateTime.Now.AddSeconds(-3);

            // 1. 먼저 3초 이내의 서버들만 필터링합니다.
            var availableServers = _battleServers
                .Where(pair => threeSecondsAgo <= pair.Value.LastUpdateDate);

            // 2. 필터링된 서버가 있는지 확인합니다.
            if (!availableServers.Any())
            {
                return null; // 사용 가능한 서버가 없음
            }

            // 3. MinBy를 사용하여 UserCount가 가장 적은 서버의 Key를 직접 찾습니다.
            var bestServerKey = availableServers
                .MinBy(pair => pair.Value.UserCount)
                .Key;

            return bestServerKey;
        }

        private void Publish<T>(T message) where T : IBaseMessage
        {
            if (message.From != ServerKind.Matching)
            {
                _logger.Error("MatchingServer Publish: invalid From={From} (expected Matching), To={To}, type={Type}", message.From, message.To, typeof(T).Name);
                return;
            }
            _messageChannel.Publish(message);
        }

        public bool RecvMessageFunc(IBaseMessage baseMessage)
        {
            return _recvMessageChannel.Writer.TryWrite(baseMessage);
        }
    }
}
