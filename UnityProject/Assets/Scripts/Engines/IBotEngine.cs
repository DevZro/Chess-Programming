namespace ChessEngine
{
    public interface IBotEngine
    {
        Move GetBestMove();           // The engine must return a move
        string GetName();                        // Optional: for UI/debug ("Random Bot", "Minimax v2", etc.)
    }
}
