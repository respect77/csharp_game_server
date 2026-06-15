using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Server.Common;

namespace Server.Battle.Context
{
    public interface IRoomManager
    {
        void SendPacket(IBasePacket packet, params int[] clientIds);
    }

    public class RoomManager : IRoomManager
    {
        private readonly BattleServer _server;
        private readonly ILogger<RoomManager> _logger;
        public object VerifyLock { get; } = new();
        private readonly ConcurrentDictionary<string/*accessToken*/, (int UserIndex, int RoomNo)> _accessTokens = new();
        private int _lastRoomNo = 0;
        private readonly ConcurrentDictionary<int, Room> _rooms = new();

        public RoomManager(BattleServer server, ILogger<RoomManager> logger)
        {
            _server = server;
            _logger = logger;
        }

        public int NextRoomNo() => Interlocked.Increment(ref _lastRoomNo);

        public bool TryGetRoom(int roomNo, [NotNullWhen(true)] out Room? room) => _rooms.TryGetValue(roomNo, out room);

        public bool TryAddRoom(int roomNo, Room room) => _rooms.TryAdd(roomNo, room);

        public bool TryGetAccessInfo(string accessToken, out (int UserIndex, int RoomNo) accessInfo)
            => _accessTokens.TryGetValue(accessToken, out accessInfo);

        public bool RegisterAccessToken(string accessToken, int userIndex, int roomNo)
            => _accessTokens.TryAdd(accessToken, (userIndex, roomNo));

        public void SendPacket(IBasePacket packet, params int[] clientIds) => _server.SendPacket(packet, clientIds);

        public void OnRoomClosed(int roomNo)
        {
            if (!_rooms.TryRemove(roomNo, out var room))
            {
                return;
            }

            foreach (var player in room.GetPlayerList())
            {
                _accessTokens.TryRemove(player.GameInfo.AccessToken, out var _);
            }

            room.Close();
            _logger.Info("Room {RoomNo} Closed.", roomNo);
        }
    }
}
