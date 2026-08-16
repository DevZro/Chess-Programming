using System;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace ChessEngine
{
    // BondFish1.2, 
    // implements alpha-beta pruning to improve search
    // Adds Move Ordering to improve speed.
    // MVV-LVA as well as promotion heuristics are used.
    public class BondFish1_3 : IBotEngine
    {
        public string GetName() => "BondFish1.3";
        int Pawn = 100;
        int Knight = 300;
        int Bishop = 300;
        int Rook = 500;
        int Queen = 900;
        int King = 10000; // irrelevant but populates the PieceValue array

        int[] PieceValues = new int[6];
        int Depth;
        int Positions = 0;

        public BondFish1_3(int depth)
        {
            Depth = depth;

            PieceValues[0] = Pawn;
            PieceValues[1] = Knight;
            PieceValues[2] = Bishop;
            PieceValues[3] = Rook;
            PieceValues[4] = Queen;
            PieceValues[5] = King;
        }

        private int Evaluate(Board board)
        {
            int white_count = 0;
            int black_count = 0;

            if (board.GameOver() != GameResult.Ongoing)
            {
                if (board.GameOver() == GameResult.Draw)
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

        // Receives bitboard index as input
        // Pawn = 0 or 6
        // Knight = 1 or 7
        // Bishop = 2 or 8
        // Rook = 3 or 9
        // Queen = 4 or 10
        // King = 5 or 11
        public int GetValue(int pieceType)
        {
            return PieceValues[pieceType % 6];
        }

        public int ScoreMove(Move move, Board board)
        {
            int startsquare = move.data & 0x003F;
            int stopsquare = (move.data >> 6) & 0x003F;
            int flag = (move.data >> 12) & 0x003F;

            int score = 0;
            int movePieceType = board.OccupyingPiece(startsquare);
            int targetPieceType = board.OccupyingPiece(stopsquare);

            // 1. MVV-LVA for Captures
            if (targetPieceType != -1) // Captures
            {
                // Higher score = searched earlier
                // Formula: (10 * VictimValue) - AttackerValue
                score = 10000 + (10 * GetValue(targetPieceType)) - GetValue(movePieceType);
            }
            
            // 2. Promotions
            // Search promotions right after high-value captures
            // Promotions are sorted from queen to knight
            if (flag >= 6) // Prone to bug but represents capture flags.
            {
                score += 9000 + GetValue(flag - 5); // converts flag to pieceType and adds to flat score. 
            }

            return score;
        }



        public int Search(int alpha, int beta, int depth, Board board)
        {
            Positions += 1;
            if ((depth == 0) || board.GameOver() != GameResult.Ongoing)
            {
                return Evaluate(board);
            }
            var moves = board.GenerateMoves();

            int[] moveScores = new int[moves.Count];
            for (int i = 0; i < moves.Count; i++)
            {
                moveScores[i] = ScoreMove(moves[i], board);
            }

            for (int i = 0; i < moves.Count; i++)
            {
                // Selection Sort for the best move
                for (int j = i + 1; j < moves.Count; j++)
                {
                    if (moveScores[j] > moveScores[i])
                    {
                        // Swap scores
                        int tempScore = moveScores[i];
                        moveScores[i] = moveScores[j];
                        moveScores[j] = tempScore;

                        // Swap moves
                        Move tempMove = moves[i];
                        moves[i] = moves[j];
                        moves[j] = tempMove;
                    }
                }

                Move move = moves[i];
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

            int[] moveScores = new int[moves.Count];
            for (int i = 0; i < moves.Count; i++)
            {
                moveScores[i] = ScoreMove(moves[i], board);
            }

            for (int i = 0; i < moves.Count; i++)
            {
                // Selection Sort for the best move
                for (int j = i + 1; j < moves.Count; j++)
                {
                    if (moveScores[j] > moveScores[i])
                    {
                        // Swap scores
                        int tempScore = moveScores[i];
                        moveScores[i] = moveScores[j];
                        moveScores[j] = tempScore;

                        // Swap moves
                        Move tempMove = moves[i];
                        moves[i] = moves[j];
                        moves[j] = tempMove;
                    }
                }

                Move move = moves[i];
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