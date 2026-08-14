using UnityEngine;
using System.Collections;
using ChessEngine;
public class BotPlayer : IPlayer
{
    public bool IsHuman => false;
    private IBotEngine engine;

    private Board board;
    private GraphicalBoard graphicalBoard;
    private bool isThinking;

    public BotPlayer(Board b, GraphicalBoard gb, IBotEngine chosenEngine)
    {
        board = b;
        graphicalBoard = gb;
        engine = chosenEngine;
    }

    public void OnTurnStarted()
    {
        graphicalBoard.StartCoroutine(MakeBotMove());
    }

    public void OnGameStarted()
    {
        
    }


    private IEnumerator MakeBotMove()
    {   
        // Give the UI one frame to breathe before starting
        yield return new WaitForSeconds(0.05f); // Optional: add a slight delay so the bot doesn't move instantly

        var task = System.Threading.Tasks.Task.Run(() => engine.GetBestMove());

        while (!task.IsCompleted) yield return null;

        if (task.IsFaulted)
        {
            Debug.LogException(task.Exception);
            yield break;
        }

        Move bestMove = task.Result;


        // 4. Validate and Execute
        if (bestMove.data != 0) 
        {
            graphicalBoard.ExecuteMoveVisually(bestMove);
            board.MakeMove(bestMove);     
        }

        // 5. CRITICAL: Always signal completion to swap the clock
        graphicalBoard.OnMoveCompleted();
    }
        


    /*private Move SimpleRandomMove()
    {
        var allMoves = board.GenerateMoves();
        if (allMoves.Count == 0) return new Move(0, 0, 0);
        
        return allMoves[UnityEngine.Random.Range(0, allMoves.Count)];
    }*/
}