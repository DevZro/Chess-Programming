public class HumanPlayer : IPlayer
{
    public bool IsHuman => true;

    private GraphicalBoard graphicalBoard;
    private Board board;

    public HumanPlayer(GraphicalBoard gb, Board b)
    {
        graphicalBoard = gb;
        board = b;
    }

    public void OnTurnStarted() { }

    public void OnGameStarted() { }
}