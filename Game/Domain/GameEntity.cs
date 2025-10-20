using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace Game.Domain
{
    public class GameEntity
    {
        [BsonElement]
        private readonly List<Player> players;

        public GameEntity(int turnsCount)
            : this(Guid.Empty, GameStatus.WaitingToStart, turnsCount, 0, new List<Player>())
        {
        }

        [BsonConstructor]
        public GameEntity(Guid id, GameStatus status, int turnsCount, int currentTurnIndex, List<Player> players)
        {
            Id = id;
            Status = status;
            TurnsCount = turnsCount;
            CurrentTurnIndex = currentTurnIndex;
            this.players = players;
        }

        public Guid Id
        {
            get;
            // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local For MongoDB
            private set;
        }

        public IReadOnlyList<Player> Players => players.AsReadOnly();

        public int TurnsCount { get; }

        public int CurrentTurnIndex { get; private set; }

        public GameStatus Status { get; private set; }

        public void AddPlayer(UserEntity user)
        {
            if (Status != GameStatus.WaitingToStart)
                throw new ArgumentException(Status.ToString());
            players.Add(new Player(user.Id, user.Login));
            if (Players.Count == 2)
                Status = GameStatus.Playing;
        }

        public bool IsFinished()
        {
            return CurrentTurnIndex >= TurnsCount
                   || Status == GameStatus.Finished
                   || Status == GameStatus.Canceled;
        }

        public void Cancel()
        {
            if (!IsFinished())
                Status = GameStatus.Canceled;
        }

        public bool HaveDecisionOfEveryPlayer => Players.All(p => p.Decision.HasValue);

        public void SetPlayerDecision(Guid userId, PlayerDecision decision)
        {
            if (Status != GameStatus.Playing)
                throw new InvalidOperationException(Status.ToString());
            foreach (var player in Players.Where(p => p.UserId == userId))
            {
                if (player.Decision.HasValue)
                    throw new InvalidOperationException(player.Decision.ToString());
                player.Decision = decision;
            }
        }

        public GameTurnEntity FinishTurn()
        {
            if (!HaveDecisionOfEveryPlayer)
                throw new InvalidOperationException("Not all players made decisions");

            var currentTurn = new GameTurnEntity(Id, CurrentTurnIndex)
            {
                Players = Players.Select(p => new PlayerTurnResult
                {
                    UserId = p.UserId,
                    UserName = p.Name,
                    Decision = p.Decision.Value
                }).ToList()
            };

            var decisions = Players.Select(p => p.Decision.Value).Distinct().ToList();
            if (decisions.Count == 2)
            {
                var winnerDecision = GetWinningDecision(decisions[0], decisions[1]);
                var winner = Players.First(p => p.Decision == winnerDecision);
                winner.Score++;
                currentTurn.WinnerId = winner.UserId;

                foreach (var player in currentTurn.Players)
                {
                    player.Result = player.UserId == winner.UserId ? TurnResult.Won : TurnResult.Lost;
                }
            }
            else
            {
                foreach (var player in currentTurn.Players)
                {
                    player.Result = TurnResult.Draw;
                }
            }

            foreach (var player in Players)
            {
                player.Decision = null;
            }

            CurrentTurnIndex++;

            return currentTurn;
        }

        private static PlayerDecision GetWinningDecision(PlayerDecision decision1, PlayerDecision decision2)
        {
            if ((decision1 == PlayerDecision.Rock && decision2 == PlayerDecision.Scissors) ||
                (decision1 == PlayerDecision.Scissors && decision2 == PlayerDecision.Paper) ||
                (decision1 == PlayerDecision.Paper && decision2 == PlayerDecision.Rock))
                return decision1;

            return decision2;
        }
    }
}