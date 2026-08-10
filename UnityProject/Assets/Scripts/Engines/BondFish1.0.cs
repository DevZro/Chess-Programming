using System;

namespace ChessEngine
{
    // BondFish1.0, 
    // finds the best move by choosing the move with the highest evaluation in the position
    // Evaluation is found exclusively by move count of the resulting position
    // essentially a one level deep search with a crude evaluation function
    public class BondFish1_0 : IBotEngine
    {
        public string GetName() => "BondFish1.0";
        int Pawn = 100;
        int Knight = 300; 
        int Bishop = 300;
        int Rook = 500;
        int Queen = 900;
        Board board;

        public BondFish1_0(int depth, Board chessBoard)
        {
            board = chessBoard;
        }


        private int Evaluate()
        {
            int white_count = 0;
            int black_count = 0;

            white_count += board.PieceCount(0) * Pawn;
            white_count += board.PieceCount(1) * Knight;
            white_count += board.PieceCount(2) * Bishop;
            white_count += board.PieceCount(3) * Rook;
            white_count += board.PieceCount(4) * Queen;

            black_count += board.PieceCount(6) * Pawn;
            black_count += board.PieceCount(7) * Knight;
            black_count += board.PieceCount(8) * Bishop;
            black_count += board.PieceCount(9) * Rook;
            black_count += board.PieceCount(10) * Queen;

            // If it is black to move in this position, then we just evaluated a move for white and 
            // should give the result from white's perspective
            // the function actually aims to evaluates moves not positions. 
            // It just does so by evaluating the position resulting from said move
            if (board.isWhite) 
            {
                return (white_count - black_count);
            }
            else 
            {
                return (black_count - white_count);
            }
        }

        public Move GetBestMove()
        {
            Move best_move = new Move(0, 0, 0);
            //float best_score = float.NegativeInfinity;

            int best_score = int.MinValue;
            foreach (Move move in board.GenerateMoves())
            {
                board.MakeMove(move);
                int score = -Evaluate();

                if (score > best_score)
                {
                    best_score = score;
                    best_move = move;
                }
                board.UndoMove();
            }
            return best_move;
        }
    }
}