using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessEngine
{
    // BondFish1.1, 
    // adds proper minimax search with depth
    // adds a check for terminal positions
    public class BondFish1_1 : IBotEngine
    {
        public string GetName() => "BondFish1.1";
        int Pawn = 100;
        int Knight = 300;
        int Bishop = 300;
        int Rook = 500;
        int Queen = 900;

        int Depth;
        int Positions = 0;

        public BondFish1_1(int depth)
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

        public int Search(int depth, Board board)
        {
            Positions += 1;
            if ((depth == 0) || board.GameOver()[0])
            {
                return Evaluate(board);
            }
            var moves = board.GenerateMoves();
            
            int best_score = -1000000;
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int score = -Search(depth - 1, board);

                if (score > best_score)
                {
                    best_score = score;
                }
                board.UndoMove();
            }
            return best_score;
        }

        public Move GetBestMove(Board board)
        {
            Positions = 0;
            Move best_move = new Move(0, 0, 0);
            int best_score = -1000000;

            var moves = board.GenerateMoves();

            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int score = -Search(Depth - 1, board);
                board.UndoMove();

                if (score > best_score)
                {
                    best_move = move;
                    best_score = score;
                }
                
            }
            UnityEngine.Debug.Log("1.1");
            UnityEngine.Debug.Log(Positions);
            //UnityEngine.Debug.Log(best_move.data);
            return best_move;
        }


    }
}