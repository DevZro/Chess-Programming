using System;

public static class Zobrist
{
    // Random keys
    public static readonly ulong[,] pieceKeys = new ulong[12, 64];   // 12 piece types × 64 squares
    public static readonly ulong sideKey;                           // Side to move
    public static readonly ulong[] castlingKeys = new ulong[4];      // KQkq
    public static readonly ulong[] enPassantKeys = new ulong[8];     // One per file (a-h)

    static Zobrist()
    {
        System.Random rnd = new System.Random(123456789); // Fixed seed = always same keys

        // Piece keys
        for (int piece = 0; piece < 12; piece++)
            for (int sq = 0; sq < 64; sq++)
                pieceKeys[piece, sq] = RandomUlong(rnd);

        // Side to move key
        sideKey = RandomUlong(rnd);

        // Castling keys (one for each possible right: WhiteKing, WhiteQueen, BlackKing, BlackQueen)
        for (int i = 0; i < 4; i++)
            castlingKeys[i] = RandomUlong(rnd);

        // En passant file keys
        for (int file = 0; file < 8; file++)
            enPassantKeys[file] = RandomUlong(rnd);
    }

    private static ulong RandomUlong(System.Random rnd)
    {
        ulong high = (ulong)rnd.Next();
        ulong low  = (ulong)rnd.Next();
        return (high << 32) | low;
    }
}