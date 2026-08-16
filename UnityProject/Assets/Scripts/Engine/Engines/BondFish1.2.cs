using System;
using System.Diagnostics;

namespace ChessEngine
{
    // BondFish1.2, 
    // implements alpha-beta pruning to improve search
    public class BondFish1_2 : IBotEngine
    {
        public string GetName() => "BondFish1.2";
        int Pawn = 100;
        int Knight = 300;
        int Bishop = 300;
        int Rook = 500;
        int Queen = 900;

        int Depth;
        int Positions = 0;

        public BondFish1_2(int depth)
        {
            Depth = depth;
        }

        private int Evaluate(Board board)
        {
            int white_count = 0;
            int black_count = 0;

            if (board.GameOver()[0])
            {
                if (!board.GameOver()[1])
                {
                    return 0;
                }
                return -100000; // if checkmate then it is good for the side that played the move
            }

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

            // Evaluates the position from the perspective of the player to play in the current position
            if (board.isWhite) 
            {
                return (white_count - black_count);
            }
            else 
            {
                return (black_count - white_count);
            }
        }



        public int Search(int alpha, int beta, int depth, Board board)
        {
            Positions += 1;
            if ((depth == 0) || board.GameOver()[0])
            {
                return Evaluate(board);
            }
            var moves = board.GenerateMoves();

            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int score = -Search(-beta, -alpha, depth - 1, board);
                board.UndoMove();
              
                if (score >= beta)
                {
                    return beta;
                }   
                if (score > alpha)
                {
                    alpha = score;
                } 
            }
            return alpha;
        }

        public Move GetBestMove(Board board)
        {
            Positions = 0;
            Move best_move = new Move(0, 0, 0);
            
            int alpha = -1000000;
            int beta = 1000000;

            var moves = board.GenerateMoves();

            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int score = -Search(-beta, -alpha, Depth - 1, board);
                board.UndoMove();

                if (score > alpha)
                {
                    alpha = score;
                    best_move = move;
                }
                
            }

            return best_move;
        }


    }
}