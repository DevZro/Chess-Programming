using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class GraphicalBoard : MonoBehaviour
{
    
    public const string starting_position_fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    //public const string starting_position_fen = "rnbqkbnr/pppppp1p/8/6p1/5P2/8/PPPPP1PP/RNBQKBNR w KQkq - 0 2";
    public Board BoardScript;
    public GameObject ChessPiece;
    public GameObject tile;
    public ChessMan selectedPiece;
    public bool Clicked = false;
    public int promotedTo;
    public ChessMan current;


    // Start is called before the first frame update
    void Start()
    {
        BoardScript = GameObject.Find("Board").GetComponent<Board>();
        
        BoardScript.LoadFen(starting_position_fen);
        BoardScript.UpdateOccupiedAndEmpty();

        Color lightCol = new Color (192/255f, 164/255f, 132/255f);
        Color darkCol = new Color (92/255f, 64/255f, 51/255f);


        for (int file = 0; file < 8; file ++) {
            for (int rank = 0; rank < 8; rank ++){
                bool isLightSquare = (file + rank) % 2 != 0;

                var position = new Vector3 (-3.5f + file, -3.5f + rank, 0);

                if (isLightSquare) 
                {
                    tile.GetComponent<SpriteRenderer>().color = lightCol;
                    Instantiate(tile, position, Quaternion.identity);
                }
                else 
                {
                    tile.GetComponent<SpriteRenderer>().color = darkCol;
                    Instantiate(tile, position, Quaternion.identity);
                }
            }
        }

        SetBoard(); 

        Debug.Log(Perft(7));

        /* Debug.Log(BoardScript.whiteOccupied + " White");
        Debug.Log(BoardScript.blackOccupied + " Black");

        Move move = BoardScript.GenerateMoves()[15];

        BoardScript.MakeMove(move);
        //total = Perft(1);
        Debug.Log(BoardScript.whiteOccupied + " White_ " + move.data.ToString());
        Debug.Log(BoardScript.blackOccupied + " Black_ " + move.data.ToString());
        BoardScript.UndoMove();
        Debug.Log(BoardScript.whiteOccupied + " White " + move.data.ToString());
        Debug.Log(BoardScript.blackOccupied + " Black " + move.data.ToString());

        /*foreach (Move move in BoardScript.GenerateMoves())
        {
            BoardScript.MakeMove(move);
            //total = Perft(1);
            Debug.Log(BoardScript.whiteOccupied + " White_ " + move.data.ToString());
            Debug.Log(BoardScript.blackOccupied + " Black_ " + move.data.ToString());
            BoardScript.UndoMove();
            Debug.Log(BoardScript.whiteOccupied + " White " + move.data.ToString());
            Debug.Log(BoardScript.blackOccupied + " Black " + move.data.ToString());
        } 

           

        /*foreach (Move move in BoardScript.GenerateMoves())
        {
            Debug.Log(move.data.ToString() + " Data");
            BoardScript.MakeMove(move);
            total = Perft(1);
            Debug.Log(BoardScript.boardStateHistory.Peek().ToString() + " Made Move");
            BoardScript.UndoMove();
            Debug.Log(move.data.ToString() + " Undid Move " + BoardScript.boardStateHistory.Count);
            Debug.Log(total.ToString() + " Total " + move.data.ToString());
        }
        Debug.Log(BoardScript.whiteOccupied + " White");
        Debug.Log(BoardScript.blackOccupied + " Black");*/


    }

    public void SetBoard() {
        ChessMan ChessManScript = ChessPiece.GetComponent<ChessMan>();
        for (int i = 0; i < 64; i++) {
            int file = 7 - (i % 8);
            int rank = i / 8;
            int piece = BoardScript.OccupyingPiece(i);
            if (piece != -1) {
                ChessManScript.Activate(piece);
                current = (Instantiate (ChessPiece, new Vector3(-3.5f + file, -3.5f + rank, -1), Quaternion.identity)).GetComponent<ChessMan>();
                current.name = i.ToString();
            }
        }

        promotedTo = 4; 
    }

    public long Perft(int depth)
    {
        long total = 0;

        if (depth == 1)
        {
            total = BoardScript.GenerateMoves().Count;
        }
        else
        {
            foreach (Move move in BoardScript.GenerateMoves())
            {
                BoardScript.MakeMove(move);
                total += Perft(depth - 1);
                BoardScript.UndoMove();
            }
        }
        return total;
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
        
    }
}
