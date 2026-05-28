using MemoryPack;
using Microsoft.Extensions.Logging;
using Server.Battle.Context;
using Server.Common;
using System.Buffers;
using System.Net.Sockets;

namespace Server.Battle.Test
{
    public class BattleClientTest
    {
        private readonly ILogger<BattleClientTest> _logger;
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly CancellationTokenSource _cts = new();
        private byte[] _sendHeader = new byte[4];
        private ArrayBufferWriter<byte> _writer = new();
        public BattleClientTest(ILogger<BattleClientTest> logger)
        {
            _logger = logger;
            _client = new TcpClient("127.0.0.1", 7788);
            _stream = _client.GetStream();
            ReadExecAsync();
            SendPacket(new TestPlayClientPacket
            {
                MapId = 1,
                PlayerInfo = new()
                {
                    CardDeck = new()
                    {
                        (101,1),
                        (102,1),
                        (103,1),
                    }
                },
            });
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
                        //Error
                        _logger.Error("Invalid packetSize: {PacketSize}", packetSize);
                        return;
                    }

                    readByteCount = 0;
                    byte[] packetBuffer = ArrayPool<byte>.Shared.Rent(packetSize);

                    while (readByteCount < packetSize)
                    {
                        int byteCount = await _stream.ReadAsync(packetBuffer.AsMemory(readByteCount, packetSize - readByteCount), _cts.Token);
                        if (byteCount <= 0)
                        {
                            ArrayPool<byte>.Shared.Return(packetBuffer);
                            throw new IOException("byteCount <= 0");
                        }
                        readByteCount += byteCount;
                    }

                    var basePacket = MemoryPackSerializer.Deserialize<IBasePacket>(packetBuffer.AsSpan(0, packetSize));
                    if (basePacket == null)
                    {
                        ArrayPool<byte>.Shared.Return(packetBuffer);
                        throw new IOException("basePacket == null");
                    }
                    RecvPacket(basePacket);
                    ArrayPool<byte>.Shared.Return(packetBuffer);
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
                _logger.Error(ex, "BattleClientTest Read Error");
            }
        }

        private async void SendPacket(IBasePacket packet)
        {
            _writer.Clear();
            MemoryPackSerializer.Serialize(_writer, packet);
            int payloadLength = _writer.WrittenCount;
            BitConverter.TryWriteBytes(_sendHeader.AsSpan(), payloadLength);
            await _stream.WriteAsync(_sendHeader.AsMemory(0, 4));
            await _stream.WriteAsync(_writer.WrittenMemory);
        }

        public void RecvPacket(IBasePacket packet)
        {
            switch (packet)
            {
                case TestPlayServerPacket testPlayPacket:
                    {
                        SendPacket(new VerifyClientPacket() {
                            AccessToken = testPlayPacket.AccessToken,
                        });
                    }
                    break;
                case VerifyServerPacket verifyPacket:
                    {
                        SendPacket(new ReadyClientNotifyPacket() { });
                    }
                    break;
                case StartServerNotifyPacket:
                    {
                        _logger.Info("Battle Start!");
                    }
                    break;
                case PlayServerNotifyPacket:
                    {
                        _logger.Info("Battle Play!");
                        InputExec();
                    }
                    break;
                case FrameUpdateServerNotifyPacket frameUpdate:
                    {
                        //_logManager.Info($"Frame Update: {frameUpdate.FrameInfo.FrameIndex}");
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

        public async void InputExec()
        {
            while (true)
            {
                await Task.Delay(150);
                SendPacket(new PlayInputClientNotifyPacket()
                {
                    InputType = InputTypeEnum.Create,
                    CreateCardId = 101,
                });
            }
        }
    }
}
