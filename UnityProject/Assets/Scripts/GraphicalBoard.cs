using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using ChessEngine;

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
    public int promotedTo;
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
        UnityEngine.Debug.Log("First");

    }
    public void OnMoveCompleted()
    {
        clock.SwitchTurn(board.isWhite);

        isGameOn = !board.GameOver()[0];           
        if (!isGameOn)
        {
            clock.StopClocks();
            if (!board.GameOver()[1])
            {
                controller.ShowGameResult("Draw!");

            }
            else
            {
                if (board.GameOver()[2])
                {
                    controller.ShowGameResult("White Wins!");   
                }

                else
                {
                    controller.ShowGameResult("Black Wins!");
                }
            }                
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

        whitePlayer = new BotPlayer(board, this, new BondFish1_4(7, board));
        blackPlayer = new BotPlayer(board, this, new BondFish1_1(4, board));

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

    // only insufficient material for now 
    /*public void CheckForcedDraw()
    {
        if (board.claimInsufficientMaterial)
        {
            isGameOn = false;
            
        }
    } */

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
                pieces[stoprank, stopfile].transform.position = new Vector3(10, 10, 1); // i have not figured how to delete the captured piece

                pieces[startrank, startfile].transform.position = position;
                pieces[stoprank, stopfile] = pieces[startrank, startfile];
                pieces[startrank, startfile] = null;
            }

            else // it can only be a promotion
            {
                Vector3 position = pieces[stoprank, stopfile].transform.position;
                pieces[stoprank, stopfile].transform.position = new Vector3(10, 10, 1); // i have not figured how to delete the captured piece

                pieces[startrank, startfile].transform.position = position;
                pieces[stoprank, stopfile] = pieces[startrank, startfile];
                pieces[startrank, startfile] = null;

                if (board.isWhite)
                {
                    switch (flag) // flag will be a promotion flag
                    {
                        case 6: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].white_knight; break;
                        case 7: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].white_bishop; break;
                        case 8: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].white_rook; break;
                        case 9: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].white_queen; break;
                    }
                }
                else
                {
                    switch (flag)
                    {
                        case 7: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].black_knight; break;
                        case 8: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].black_bishop; break;
                        case 9: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].black_rook; break;
                        case 10: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].black_queen; break;
                    }
                }
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
                    pieces[startrank, stopfile - 1] = null;                            
                }

                else // queenside
                {
                    Vector3 rookFinalPosition = new Vector3(-3.5f + stopfile - 1, -3.5f + stoprank, -1); // calculated position

                    pieces[stoprank, stopfile + 2].transform.position = rookFinalPosition;
                    pieces[stoprank, stopfile - 1] = pieces[stoprank, stopfile + 2];
                    pieces[startrank, stopfile + 2] = null;                            
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
                    pieces[stoprank - 1, stopfile].transform.position = new Vector3(10, 10, 1);
                    pieces[stoprank - 1, stopfile] = null; 
                }
                else
                {
                    pieces[stoprank + 1, stopfile].transform.position = new Vector3(10, 10, 1);
                    pieces[stoprank + 1, stopfile] = null;
                }
            }

            else // it can only be a promotion
            {
                Vector3 position = new Vector3(-3.5f + stopfile, -3.5f + stoprank, -1);

                pieces[startrank, startfile].transform.position = position;
                pieces[stoprank, stopfile] = pieces[startrank, startfile];
                pieces[startrank, startfile] = null;

                if (board.isWhite)
                {
                    switch (flag) // flag will be a promotion flag
                    {
                        case 6: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].white_knight; break;
                        case 7: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].white_bishop; break;
                        case 8: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].white_rook; break;
                        case 9: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].white_queen; break;
                    }
                }
                else
                {
                    switch (flag)
                    {
                        case 6: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].black_knight; break;
                        case 7: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].black_bishop; break;
                        case 8: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].black_rook; break;
                        case 9: pieces[stoprank, stopfile].GetComponent<SpriteRenderer>().sprite = pieces[stoprank, stopfile].black_queen; break;
                    }
                }
            }
            
        }
    }
    



    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.N)) {
            promotedTo = 1;
            Debug.Log("Key has been clicked");
        }
        if (Input.GetKeyDown(KeyCode.B)) {
            promotedTo = 2;
            Debug.Log("Key has been clicked");
        }
        if (Input.GetKeyDown(KeyCode.R)) {
            promotedTo = 3;
            Debug.Log("Key has been clicked");
        }
        if (Input.GetKeyDown(KeyCode.Q)) {
            promotedTo = 4;
            Debug.Log("Key has been clicked");
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
