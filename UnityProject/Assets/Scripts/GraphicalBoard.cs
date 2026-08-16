using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using ChessEngine;
using System.Diagnostics;
public class GraphicalBoard : MonoBehaviour
{
    
    public const string starting_position_fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    //public const string starting_position_fen = "rnbqkbnr/pppppp1p/8/6p1/5P2/8/PPPPP1PP/RNBQKBNR w KQkq - 0 2";
    public Board board;
    public ChessMan chessPiece;
    public Tile tile;
    public ChessMan selectedPiece;
    public bool isGameOn;
    public bool clicked = false;
    public int promotedTo = 4;
    public GameStatusController controller;
    public Clock clock;

    public Tile[,] tiles = new Tile[8, 8];           // [file, rank]  (0,0) = a1
    public ChessMan[,] pieces = new ChessMan[8, 8];

    public bool whiteIsHuman;
    public bool blackIsHuman;

    public IPlayer whitePlayer;
    public IPlayer blackPlayer;
    public IPlayer currentPlayer;


    // Start is called before the first frame update
    void Start()
    {   

        whiteIsHuman = false;
        blackIsHuman = false;

        board = new Board();
        board.LoadFen(starting_position_fen);

        Color lightCol = new Color (192/255f, 164/255f, 132/255f);
        Color darkCol = new Color (92/255f, 64/255f, 51/255f);


        for (int file = 0; file < 8; file ++) {
            for (int rank = 0; rank < 8; rank ++)
            {
                bool isLightSquare = (file + rank) % 2 != 0;

                var position = new Vector3 (-3.5f + file, -3.5f + rank, 0);
                Tile newTile;

                if (isLightSquare) 
                {
                    tile.GetComponent<SpriteRenderer>().color = lightCol;
                    newTile = Instantiate(tile, position, Quaternion.identity);
                }
                else 
                {
                    tile.GetComponent<SpriteRenderer>().color = darkCol;
                    newTile = Instantiate(tile, position, Quaternion.identity);
                }

                tiles[rank, file] = newTile;
            }
        }

        SetBoard();
        /*UnityEngine.Debug.Log(board.GenerateMoves()[19].data & 0x003F);
        UnityEngine.Debug.Log((board.GenerateMoves()[19].data >> 6) & 0x003F);
        board.MakeMove(board.GenerateMoves()[19]);
        foreach (Move move in board.GenerateMoves())
            {
                UnityEngine.Debug.Log(move.data & 0x003F);
                UnityEngine.Debug.Log((move.data >> 6) & 0x003F);
                board.MakeMove(move);
                UnityEngine.Debug.Log(Perft(1));
                board.UndoMove();
            }
        */

    }

    public void OnMoveCompleted()
    {               
        GameResult result = board.GameOver();
        isGameOn = result == GameResult.Ongoing;

        switch (result)
        {
            case GameResult.WhiteWins:
                controller.ShowGameResult("White Wins!"); 
                clock.StopClocks();
                break;
            case GameResult.BlackWins:
                controller.ShowGameResult("Black Wins!");
                clock.StopClocks();
                break;
            case GameResult.Draw:
                clock.StopClocks();
                controller.ShowGameResult("Draw!");
                break;
            case GameResult.Ongoing:
                clock.SwitchTurn(board.isWhite);
                break; // nothing to do
        }
        currentPlayer = (currentPlayer == whitePlayer) ? blackPlayer : whitePlayer;
        currentPlayer.OnTurnStarted();
    }

    public void SetBoard() {
        for (int i = 0; i < 64; i++) 
        {
            ChessMan current = null;

            int file = 7 - (i % 8);
            int rank = i / 8;
            int piece = board.OccupyingPiece(i);
            if (piece != -1) {
                chessPiece.Activate(piece);
                current = Instantiate (chessPiece, new Vector3(-3.5f + file, -3.5f + rank, -1), Quaternion.identity);
            }
            pieces[rank, file] = current;
        }
    }

    public void StartNewGame()
    {
        //whitePlayer = whiteIsHuman ? new HumanPlayer(this, board) : new BotPlayer(board, this, new BondFish1_0());
        //blackPlayer = blackIsHuman ? new HumanPlayer(this, board) : new BotPlayer(board, this, new RandomBotEngine());

        whitePlayer = new BotPlayer(board, this, new BondFish1_4(6));
        blackPlayer = new BotPlayer(board, this, new RandomBotEngine());

        //whitePlayer = new HumanPlayer(this, board);
        //blackPlayer = new HumanPlayer(this, board);


        currentPlayer = whitePlayer;
        isGameOn = true;

        clock.StartClocks();

        currentPlayer.OnGameStarted();
        currentPlayer.OnTurnStarted();
    }

    public void TimeLoss(bool isWhite)
    {
        isGameOn = false;
        if (isWhite)
        {
            controller.ShowGameResult("Black Wins on Time!"); //needs update when draw by insufficient material is added
        }
        else
        {
            controller.ShowGameResult("White Wins on Time!");
        }
        clock.StopClocks();
    }

    public long Perft(int depth)
    {
        long total = 0;

        if (depth == 1)
        {
            total = board.GenerateMoves().Count;
        }
        else
        {
            foreach (Move move in board.GenerateMoves())
            {
                board.MakeMove(move);
                total += Perft(depth - 1);
                board.UndoMove();
            }
        }
        return total;
    }
    public void PrintPerft(int depth) 
    {

        long total;
        Stopwatch stopwatch;

        for (int i = 1; i < depth; i++)
        {
            stopwatch = Stopwatch.StartNew();
            total = Perft(i);
            stopwatch.Stop();
            UnityEngine.Debug.Log(total);
            UnityEngine.Debug.Log($" {stopwatch.ElapsedMilliseconds} milliseconds");
        }
    }

    // only insufficient material for now 
    /*public void CheckForcedDraw()
    {
        if (board.claimInsufficientMaterial)
        {
            isGameOn = false;
            
        }
    } */

    public void SetPromotionSprite(ChessMan piece, int flag, bool isWhite)
    {
        if (isWhite)
        {
            switch (flag)
            {
                case 6: piece.GetComponent<SpriteRenderer>().sprite = piece.white_knight; break;
                case 7: piece.GetComponent<SpriteRenderer>().sprite = piece.white_bishop; break;
                case 8: piece.GetComponent<SpriteRenderer>().sprite = piece.white_rook; break;
                case 9: piece.GetComponent<SpriteRenderer>().sprite = piece.white_queen; break;
            }
        }
        else
        {
            switch (flag)
            {
                case 6: piece.GetComponent<SpriteRenderer>().sprite = piece.black_knight; break;
                case 7: piece.GetComponent<SpriteRenderer>().sprite = piece.black_bishop; break;
                case 8: piece.GetComponent<SpriteRenderer>().sprite = piece.black_rook; break;
                case 9: piece.GetComponent<SpriteRenderer>().sprite = piece.black_queen; break;
            }
        }
    }

    public void ExecuteMoveVisually(Move move)
    {
        int startsquare = move.data & 0x003F;
        int stopsquare = (move.data >> 6) & 0x003F;

        int startrank = startsquare / 8;
        int startfile = 7 - (startsquare % 8);

        int stoprank = stopsquare / 8;
        int stopfile = 7 - (stopsquare % 8);
        
        int flag = (move.data >> 12) & 0x003F;

        if (pieces[stoprank, stopfile] != null) // capture except enpessant
        {
            if (flag == board.captureFlag) // if it is a regular capture
            {
                Vector3 position = pieces[stoprank, stopfile].transform.position;
                Destroy(pieces[stoprank, stopfile].gameObject);
                
                pieces[startrank, startfile].transform.position = position;
                pieces[stoprank, stopfile] = pieces[startrank, startfile];
                pieces[startrank, startfile] = null;
            }

            else // it can only be a promotion
            {
                Vector3 position = pieces[stoprank, stopfile].transform.position;
                Destroy(pieces[stoprank, stopfile].gameObject);

                pieces[startrank, startfile].transform.position = position;
                pieces[stoprank, stopfile] = pieces[startrank, startfile];
                pieces[startrank, startfile] = null;

                SetPromotionSprite(pieces[stoprank, stopfile], flag, board.isWhite);
            }
        }

        else
        {
            if ((flag == board.regularFlag) || (flag == board.doublePawnMoveFlag))
            {
                Vector3 position = new Vector3(-3.5f + stopfile, -3.5f + stoprank, -1); // calculated position

                pieces[startrank, startfile].transform.position = position;
                pieces[stoprank, stopfile] = pieces[startrank, startfile];
                pieces[startrank, startfile] = null;
            }

            else if (flag == board.castlingFlag) 
            {
                Vector3 position = new Vector3(-3.5f + stopfile, -3.5f + stoprank, -1); // calculated position

                pieces[startrank, startfile].transform.position = position;
                pieces[stoprank, stopfile] = pieces[startrank, startfile];
                pieces[startrank, startfile] = null;
        
                if (startfile > stopfile) // kingside
                {
                    Vector3 rookFinalPosition = new Vector3(-3.5f + stopfile + 1, -3.5f + stoprank, -1); // calculated position

                    pieces[stoprank, stopfile - 1].transform.position = rookFinalPosition;
                    pieces[stoprank, stopfile + 1] = pieces[stoprank, stopfile - 1];
                    pieces[stoprank, stopfile - 1] = null;                            
                }

                else // queenside
                {
                    Vector3 rookFinalPosition = new Vector3(-3.5f + stopfile - 1, -3.5f + stoprank, -1); // calculated position

                    pieces[stoprank, stopfile + 2].transform.position = rookFinalPosition;
                    pieces[stoprank, stopfile - 1] = pieces[stoprank, stopfile + 2];
                    pieces[stoprank, stopfile + 2] = null;                            
                }
                    
            }

            else if (flag == board.enpessantFlag) // enpessant
            {
                Vector3 position = new Vector3(-3.5f + stopfile, -3.5f + stoprank, -1);
                pieces[startrank, startfile].transform.position = position;
                pieces[stoprank, stopfile] = pieces[startrank, startfile];
                pieces[startrank, startfile] = null;

                if (board.isWhite)
                {
                    Destroy(pieces[stoprank - 1, stopfile].gameObject);
                    pieces[stoprank - 1, stopfile] = null; 
                }
                else
                {
                    Destroy(pieces[stoprank + 1, stopfile].gameObject);
                    pieces[stoprank + 1, stopfile] = null;
                }
            }

            else // it can only be a promotion
            {
                Vector3 position = new Vector3(-3.5f + stopfile, -3.5f + stoprank, -1);

                pieces[startrank, startfile].transform.position = position;
                pieces[stoprank, stopfile] = pieces[startrank, startfile];
                pieces[startrank, startfile] = null;

                SetPromotionSprite(pieces[stoprank, stopfile], flag, board.isWhite);
            }
            
        }
    }
    



    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.N)) {
            promotedTo = 1;
            UnityEngine.Debug.Log("Key has been clicked");
        }
        if (Input.GetKeyDown(KeyCode.B)) {
            promotedTo = 2;
            UnityEngine.Debug.Log("Key has been clicked");
        }
        if (Input.GetKeyDown(KeyCode.R)) {
            promotedTo = 3;
            UnityEngine.Debug.Log("Key has been clicked");
        }
        if (Input.GetKeyDown(KeyCode.Q)) {
            promotedTo = 4;
            UnityEngine.Debug.Log("Key has been clicked");
        }
        if (Input.GetKeyDown(KeyCode.D)) {
            if (board.claimThreeFold)
            {
                isGameOn = false;
                clock.StopClocks();
                controller.ShowGameResult("Draw by ThreeFold Repitition!");
            }

            if (board.halfMoveCounter >= 100)
            {
                isGameOn = false;
                clock.StopClocks();
                controller.ShowGameResult("Draw by Fifty Move Rule!");
            }
        }   
    }
}
