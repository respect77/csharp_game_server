using MemoryPack;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Server.Common
{
    public interface IMessageChannel
    {
        void Publish<T>(T message) where T : IBaseMessage;
        Task UnsubscribeAsync();
    }

    public class InnerChannel : IMessageChannel
    {
        private static ConcurrentDictionary<ServerKind, Channel<IBaseMessage>> _channels = new();
        private readonly ServerKind _kind;

        public InnerChannel(ServerKind kind, Predicate<IBaseMessage> recvMessageFunc, CancellationToken token)
        {
            _kind = kind;
            if (!_channels.ContainsKey(kind))
            {
                var channel = Channel.CreateUnbounded<IBaseMessage>();
                _channels.TryAdd(kind, channel);
                Subscriber(channel, recvMessageFunc, token);
            }
        }

        private async void Subscriber(Channel<IBaseMessage> channel, Predicate<IBaseMessage> recvMessageFunc, CancellationToken token)
        {
            await foreach (var message in channel.Reader.ReadAllAsync(token))
            {
                recvMessageFunc(message);
            }
        }

        public void Publish<T>(T message) where T : IBaseMessage
        {
            if (_channels.TryGetValue(message.To, out var msgChannel))
            {
                msgChannel.Writer.TryWrite(message);
            }
        }

        public Task UnsubscribeAsync()
        {
            if (_channels.TryRemove(_kind, out var msgChannel))
            {
                msgChannel.Writer.Complete();
            }
            return Task.CompletedTask;
        }
    }

    public class RedisPubSubChannel : IMessageChannel
    {
        private readonly ILogger<RedisPubSubChannel> _logger;

        private ConnectionMultiplexer _redis;
        private readonly string _connectionString;
        private CancellationToken _cancelToken;
        private readonly ServerKind _kind;
        private readonly Channel<(ServerKind To, IBaseMessage Message)> _publishChannel
            = Channel.CreateUnbounded<(ServerKind, IBaseMessage)>();
        private ISubscriber? _subscriber;

        public RedisPubSubChannel(ServerKind kind, Predicate<IBaseMessage> recvMessageFunc, CancellationToken token, ILogger<RedisPubSubChannel> logger, string connectionString = "localhost:6379")
        {
            _connectionString = connectionString;
            _redis = ConnectionMultiplexer.Connect(_connectionString);
            _kind = kind;
            _cancelToken = token;
            _logger = logger;
            PublishSchedule();
            Subscriber(recvMessageFunc);
        }

        public void Publish<T>(T message) where T : IBaseMessage
        {
            _publishChannel.Writer.TryWrite((message.To, message));
        }

        public async void PublishSchedule()
        {
            var database = _redis.GetDatabase();
            while (!_cancelToken.IsCancellationRequested)
            {
                try
                {
                    await foreach (var (to, message) in _publishChannel.Reader.ReadAllAsync(_cancelToken))
                    {
                        var payload = MemoryPackSerializer.Serialize(typeof(IBaseMessage), message);
                        await database.PublishAsync(new RedisChannel(to.ToString(), RedisChannel.PatternMode.Literal), payload);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "RedisPubSubChannel Write Error");
                }
            }
        }

        public async void Subscriber(Predicate<IBaseMessage> recvMessageFunc)
        {
            _subscriber = _redis.GetSubscriber();

            await _subscriber.SubscribeAsync(new RedisChannel(_kind.ToString(), RedisChannel.PatternMode.Literal), (channel, message) =>
            {
                var baseMessage = MemoryPackSerializer.Deserialize<IBaseMessage>(((ReadOnlyMemory<byte>)message).Span);
                if (baseMessage == null)
                {
                    return;
                }
                recvMessageFunc(baseMessage);
            });
        }

        public async Task UnsubscribeAsync()
        {
            if (_subscriber != null)
            {
                await _subscriber.UnsubscribeAsync(new RedisChannel(_kind.ToString(), RedisChannel.PatternMode.Literal));
            }
        }
    }
}
