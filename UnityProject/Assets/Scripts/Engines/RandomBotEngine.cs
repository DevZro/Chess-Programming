namespace ChessEngine
{
    public class RandomBotEngine : IBotEngine
    {
        Board board;
        public RandomBotEngine(Board chessBoard)
        {
            board = chessBoard;
        }
        public Move GetBestMove()
        {
            var moves = board.GenerateMoves();
            if (moves.Count == 0) return new Move(0, 0, 0);

            return moves[UnityEngine.Random.Range(0, moves.Count)];
        }

        public string GetName() => "Random Bot";
    }
}
