using System;

namespace ChessEngine
{
    public class RandomBotEngine : IBotEngine
    {
        private static readonly Random _random = new Random();
        public Move GetBestMove(Board board)
        {
            var moves = board.GenerateMoves();
            if (moves.Count == 0) return new Move(0, 0, 0);

            int randomIndex = _random.Next(moves.Count);

            return moves[randomIndex];
        }

        public string GetName() => "Random Bot";
    }
}
