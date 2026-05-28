using Microsoft.Extensions.Logging;
using Server.Common;
using System.Net.Sockets;
using System.Threading.Channels;
using MemoryPack;
using System.Buffers;

namespace Server.Battle.Context
{
    public enum ClientState
    {
        NeedVerify,
        Verified,
        Closed,
    }
    public class ClientContext
    {
        private readonly static int SendChannelCount = 1000;
        private readonly TcpClient _client;
        public readonly int ClientId;
        private readonly NetworkStream _stream;
        private readonly CancellationTokenSource _cts = new();
        private readonly BattleServer _battleServer;

        private readonly Channel<IBasePacket> _sendChannel = Channel.CreateBounded<IBasePacket>(SendChannelCount);
        private readonly ILogger<ClientContext> _logger;

        public ClientState State { get; set; } = ClientState.NeedVerify;
        public int UserIndex { get; set; }
        public int RoomNo { get; set; }

        public ClientContext(TcpClient client, int clientId, BattleServer battleServer, ILogger<ClientContext> logger)
        {
            _client = client;
            _client.NoDelay = true;
            ClientId = clientId;
            _stream = client.GetStream();
            _battleServer = battleServer;
            _logger = logger;

            SendExecAsync();
            ReadExecAsync();
        }

        private async void ReadExecAsync()
        {
            byte[] header = new byte[4];
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    int readByteCount = 0;
                    while (readByteCount < header.Length)
                    {
                        int byteCount = await _stream.ReadAsync(header.AsMemory(readByteCount, header.Length - readByteCount), _cts.Token);
                        if (byteCount <= 0)
                        {
                            throw new IOException("end of stream");
                        }
                        readByteCount += byteCount;
                    }
                    int packetSize = BitConverter.ToInt32(header, 0);

                    if (packetSize <= 0)
                    {
                        throw new IOException("packetSize <= 0");
                    }

                    readByteCount = 0;
                    byte[] packetBuffer = ArrayPool<byte>.Shared.Rent(packetSize);

                    try
                    {
                        while (readByteCount < packetSize)
                        {
                            int byteCount = await _stream.ReadAsync(packetBuffer.AsMemory(readByteCount, packetSize - readByteCount), _cts.Token);
                            if (byteCount <= 0)
                            {
                                throw new IOException("byteCount <= 0");
                            }
                            readByteCount += byteCount;
                        }

                        var basePacket = MemoryPackSerializer.Deserialize<IBasePacket>(packetBuffer.AsSpan(0, packetSize)) ?? throw new IOException("basePacket == null");
                        _battleServer.RecvPacket(this, basePacket);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(packetBuffer);
                    }
                    
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Client {ClientId} Read Error", ClientId);
                Close();
            }
        }

        private async void SendExecAsync()
        {
            byte[] header = new byte[4];
            var writer = new ArrayBufferWriter<byte>();
            try
            {
                await foreach (var packet in _sendChannel.Reader.ReadAllAsync(_cts.Token))
                {
                    //packet 변수는 반드시 IBasePacket 여야 함 아니면 Serialize시 아래와 같이 형식을 강제해주어야 함
                    //MemoryPackSerializer.Serialize<IBasePacket, ArrayBufferWriter<byte>>(writer, packet);
                    writer.Clear();
                    MemoryPackSerializer.Serialize(writer, packet);
                    BitConverter.TryWriteBytes(header.AsSpan(), writer.WrittenCount);
                    await _stream.WriteAsync(header.AsMemory(), _cts.Token);
                    await _stream.WriteAsync(writer.WrittenMemory, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Client {ClientId} Write Error", ClientId);
                Close();
            }
        }

        public bool SendPacket(IBasePacket packet)
        {
            if (!_sendChannel.Writer.TryWrite(packet))
            {
                _logger.Error("Client {ClientId} !_sendChannel.Writer.TryWrite(packet)", ClientId);
                Close();
                return false;
            }
            return true;
        }

        public void Verifed(int roomNo, int userIndex)
        {
            State = ClientState.Verified;
            RoomNo = roomNo;
            UserIndex = userIndex;
        }

        public void OverappedClose()
        {
            _logger.Info("Client {ClientId} userIndex:{UserIndex} OverappedClose.", ClientId, UserIndex);
            Close();
        }

        public void Close()
        {
            if (State == ClientState.Closed)
            {
                return;
            }
            State = ClientState.Closed;

            _cts.Cancel();
            //_writeChannel.Writer.Complete(); // 채널을 완료하여 작업 종료
            _client.Close();
            _battleServer.OnClientDisconnted(ClientId);
        }
    }
}
