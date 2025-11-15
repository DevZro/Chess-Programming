using System;
using System.Diagnostics;
using Chess;

class Program
{
    public static long Perft(ChessBoard chessboard, int depth)
    {
        long total = 0;

        if (depth == 1)
        {
            total = chessboard.GenerateMoves().Count;
        }
        else
        {
            foreach (Move move in chessboard.GenerateMoves())
            {
                chessboard.MakeMove(move);
                total += Perft(chessboard, depth - 1);
                chessboard.UndoMove();
            }
        }
        return total;
    }

    public static void Main()
    {
        
        ChessBoard chessBoard = new ChessBoard();

        long total;
        Stopwatch stopwatch;

        for (int i = 1; i < 8; i++)
        {
            stopwatch = Stopwatch.StartNew();
            total = Perft(chessBoard, i);
            stopwatch.Stop();
            Console.Write(total);
            Console.WriteLine($" {stopwatch.ElapsedMilliseconds} milliseconds");
        }

    }
}

