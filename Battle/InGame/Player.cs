using Microsoft.Extensions.Logging;
using Server.Battle.Context;
using Server.Common;

namespace Server.Battle.InGame
{
    public class Card
    {
        public int CardId { get; private set; }
        public int Level { get; private set; }
        public int UpgradeLevel { get; set; }
        public int NextUpgradeCost { get; set; }
        public Card((int cardId, int level) card)
        {
            CardId = card.cardId;
            Level = card.level;
            UpgradeLevel = 1;
            NextUpgradeCost = 10;//TODO
        }
    }
    public class Player
    {
        private Game _game;
        private readonly ILogger _roomLogger;
        private int _userIndex;
        private int _gold;
        //private 
        private Dictionary<int, Card> _cards = new();
        public Player(Game game, PlayerGameInfo gameInfo)
        {
            _game = game;
            _roomLogger = game.GetRoomLogger;
            _userIndex = gameInfo.UserIndex;
            _gold = 100;//TODO
            foreach (var card in gameInfo.CardDecks)
            {
                _cards.Add(card.CardId, new(card));
            }
        }

        public void SetInput(PlayInputClientNotifyPacket input, FrameEventPacketInfo framePacketInfo)
        {
            switch (input.InputType)
            {
                case InputTypeEnum.Create:
                    {
                        int cardId = input.CreateCardId;
                        if (!_cards.TryGetValue(cardId, out var card))
                        {
                            _roomLogger.Error("Player tried to create a card with invalid cardId: {CardId}", cardId);
                            return;
                        }

                        if (_gold < card.NextUpgradeCost)
                        {
                            //_roomLogger.Error($"Player tried to create a card without enough gold. CardId: {cardId}, RequiredGold: {card.NextUpgradeCost}, CurrentGold: {_gold}");
                            return;
                        }

                        //배치 가능한 자리가 있는가
                        if (!_game.CreateCharacter(_userIndex, 1))
                        {
                            return;
                        }

                        _gold -= card.NextUpgradeCost;
                        card.NextUpgradeCost += 10; //TODO: Upgrade cost formula
                        //TODO
                        //캐릭터 생성
                    }
                    break;
                case InputTypeEnum.Merge:
                    {
                        int fromPosIndex = input.MergeInfo.FromPosIndex;
                        int toIdIndex = input.MergeInfo.ToPosIndex;



                        //TODO
                    }
                    break;
                case InputTypeEnum.Upgrade:
                    {
                        //TODO
                    }
                    break;
            }
            //TODO
        }
    }
}
