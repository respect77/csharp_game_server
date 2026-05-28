using MemoryPack;
using Microsoft.Extensions.Logging;
using Server.Common;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace Server.Api.Context
{
    public class ApiServerContext
    {
        private readonly ILogger<ApiServerContext> _logger;
        private IMessageChannel _messageChannel;
        private ConcurrentDictionary<int/*userIndex*/, UserMatchingStateEnum> _userMatchingState = new();
        public record MatchingAccessInfo(int UserIndex, string IpAddress, int Port, string AccessToken);
        private ConcurrentDictionary<int/*userIndex*/, MatchingAccessInfo> _userMatchingAccessInfo = new();
        public ApiServerContext(CancellationToken cancellationToken, ILogger<ApiServerContext> logger)
        {
            _logger = logger;
            _messageChannel = new InnerChannel(ServerKind.Api, RecvMessageFunc, cancellationToken);
            Init();
        }

        private void Init()
        {
            //디비에서 초기값을 받아오는등 처리
        }

        public UserMatchingStateEnum GetUserMatchingState(int userIndex)
        {
            if (_userMatchingState.TryGetValue(userIndex, out var state))
            {
                return state;
            }
            return UserMatchingStateEnum.MatchingNone;
        }

        public void RemoveUserMatcingState(int userIndex)
        {
            _userMatchingState.TryRemove(userIndex, out var _);
        }

        public MatchingAccessInfo? GetAndRemoveMatchingAccessInfo(int userIndex)
        {
            if (_userMatchingAccessInfo.TryRemove(userIndex, out var accessInfo))
            {
                return accessInfo;
            }
            return null;
        }

        public void Publish<T>(T message) where T : IBaseMessage
        {
            if (message.From != ServerKind.Api)
            {
                _logger.Error("ApiServer Publish: invalid From={From} (expected Api), To={To}, type={Type}", message.From, message.To, typeof(T).Name);
                return;
            }
            _messageChannel.Publish(message);
        }
        public bool RecvMessageFunc(IBaseMessage baseMessage)
        {
            switch (baseMessage)
            {
                case UserMatchingStateMessage stateMessage:
                    {
                        foreach (var userIndex in stateMessage.UserIndexList)
                        {
                            _userMatchingState[userIndex] = stateMessage.State;
                        }
                    }
                    break;
                case UserMatchingDoneMessage matchingDoneMessage:
                    {
                        _userMatchingState[matchingDoneMessage.UserIndex] = UserMatchingStateEnum.MatchingDone;
                        _userMatchingAccessInfo[matchingDoneMessage.UserIndex] = new MatchingAccessInfo(matchingDoneMessage.UserIndex, matchingDoneMessage.IpAddress, matchingDoneMessage.Port, matchingDoneMessage.AccessToken);
                        //TODO 접속정보 저장
                    }
                    break;
                default:
                    _logger.Warning("알수없는 메시지 타입: {Type}", baseMessage.GetType().Name);
                    break;
            }
            //_recvChannel.Writer.TryWrite(message);
            return true;
        }
    }
}
