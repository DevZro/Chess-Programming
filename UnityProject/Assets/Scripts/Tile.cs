using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile: MonoBehaviour
{
    public GraphicalBoard graphicalBoard;
    public Board board;
    public Clock clock;

    private SpriteRenderer tileRenderer;
    private Color originalColor;

    public Color highlightColor = new Color(0f, 1f, 0.5f, 0.6f);     // Green for normal moves
    public Color captureHighlightColor = new Color(1f, 0.2f, 0.2f, 0.7f); // Red for captures

    private void OnMouseDown()
    {  
        if (!graphicalBoard.isGameOn) return;
        if (!graphicalBoard.currentPlayer.IsHuman) return; 
        if (graphicalBoard.isGameOn)
        {
            if (graphicalBoard.clicked) 
            {
                for (int i = 0; i < 64; i++) 
                {
                    int file = 7 - (i % 8);
                    int rank = i / 8;
                    
                    graphicalBoard.tiles[rank, file].ClearHighlight();
                }
                graphicalBoard.clicked = false;

                int start = graphicalBoard.selectedPiece.PieceIndex();
                int stop = SquareIndex();

                int[] pos = new int[2];

                Move proposed_move = new Move(start, stop, board.regularFlag); // ignore special move for now
                
                if (board.GenerateMoves().Contains(proposed_move)) 
                {
                    pos = Pos(start);
                    graphicalBoard.pieces[pos[0], pos[1]] = null;
                    graphicalBoard.selectedPiece.transform.position = this.transform.position + new Vector3(0, 0, -1); // needs to be adjusted

                    pos = Pos(stop);
                    graphicalBoard.pieces[pos[0], pos[1]] = graphicalBoard.selectedPiece;         
                    //graphicalBoard.selectedPiece.name = stop.ToString();
                    board.MakeMove(proposed_move);
                }
                // check for castling
                else if ((board.OccupyingPiece(start) % 6) == 5) 
                { // if king
                    proposed_move = new Move(start, stop, board.castlingFlag);
                    if (board.GenerateMoves().Contains(proposed_move)) 
                    {
                        pos = Pos(start);
                        graphicalBoard.pieces[pos[0], pos[1]] = null;
                        graphicalBoard.selectedPiece.transform.position = this.transform.position + new Vector3(0, 0, -1); // needs to be adjusted
        
                        if (start > stop) // kingside
                        {
                            int rookSquare = stop - 1;
                            //ChessMan Rook = GameObject.Find(rookSquare.ToString()).GetComponent<ChessMan>();
                            
                            
                            pos = Pos(rookSquare);

                            ChessMan rook = graphicalBoard.pieces[pos[0], pos[1]];
                            graphicalBoard.pieces[pos[0], pos[1]] = null; // remove piece from original square

                            //Debug.Log(rook.transform.position);                        
                            rook.transform.position = this.transform.position + new Vector3(-1, 0, -1); // bug attractive code

                            pos = Pos(rookSquare + 2);
                            graphicalBoard.pieces[pos[0], pos[1]] = rook; // move piece to new square
                            //Rook.name = (rookSquare + 2).ToString();
                            //Debug.Log(Rook.transform.position);
                        }

                        else
                        {
                            int rookSquare = stop + 2;
                            //ChessMan Rook = GameObject.Find(rookSquare.ToString()).GetComponent<ChessMan>();

                            pos = Pos(rookSquare);

                            ChessMan rook = graphicalBoard.pieces[pos[0], pos[1]];
                            graphicalBoard.pieces[pos[0], pos[1]] = null; // remove piece from original square

                            //Debug.Log(rook.transform.position);                        
                            rook.transform.position = this.transform.position + new Vector3(1, 0, -1);

                            pos = Pos(rookSquare -3);
                            graphicalBoard.pieces[pos[0], pos[1]] = rook; // move piece to new square
                            //Rook.name = (rookSquare - 3).ToString();
                            //Debug.Log(Rook.transform.position);                
                        }
                        
                        pos = Pos(stop) ;
                        graphicalBoard.pieces[pos[0], pos[1]] = graphicalBoard.selectedPiece; 
                        //graphicalBoard.selectedPiece.name = stop.ToString();
                        board.MakeMove(proposed_move);
                    }
                }

                else if ((board.OccupyingPiece(start) % 6) == 0) // if pawn
                {
                    proposed_move = new Move(start, stop, board.doublePawnMoveFlag);
                    if (board.GenerateMoves().Contains(proposed_move)) 
                    {
                        pos = Pos(start);
                        graphicalBoard.pieces[pos[0], pos[1]] = null;
                        graphicalBoard.selectedPiece.transform.position = this.transform.position + new Vector3(0, 0, -1);

                        pos = Pos(stop) ;
                        graphicalBoard.pieces[pos[0], pos[1]] = graphicalBoard.selectedPiece;
                        //graphicalBoard.selectedPiece.name = stop.ToString();
                        board.MakeMove(proposed_move);
                    }

                    proposed_move = new Move(start, stop, board.enpessantFlag);
                    if (board.GenerateMoves().Contains(proposed_move)) 
                    {
                        pos = Pos(start);
                        graphicalBoard.pieces[pos[0], pos[1]] = null;
                        graphicalBoard.selectedPiece.transform.position = this.transform.position + new Vector3(0, 0, -1);

                        if (board.isWhite) 
                        {
                            //ChessMan enpessanted = GameObject.Find((stop - 8).ToString()).GetComponent<ChessMan>();

                            pos = Pos(stop - 8);
                            ChessMan enpessanted = graphicalBoard.pieces[pos[0], pos[1]];
                            graphicalBoard.pieces[pos[0], pos[1]] = null;

                            enpessanted.transform.position = new Vector3(10, 10, 1);
                            //enpessanted.name = "deleted";
                        }
                        else {
                            //ChessMan enpessanted = GameObject.Find((stop + 8).ToString()).GetComponent<ChessMan>();
                            
                            pos = Pos(stop + 8);
                            ChessMan enpessanted = graphicalBoard.pieces[pos[0], pos[1]];
                            graphicalBoard.pieces[pos[0], pos[1]] = null;
                            
                            enpessanted.transform.position = new Vector3(10, 10, 1);
                            //enpessanted.name = "deleted";
                        }
                        pos = Pos(stop) ;
                        graphicalBoard.pieces[pos[0], pos[1]] = graphicalBoard.selectedPiece;
                        //graphicalBoard.selectedPiece.name = stop.ToString();
                        board.MakeMove(proposed_move);
                    }

                    switch (graphicalBoard.promotedTo) 
                    {
                        case 1: proposed_move = new Move(start, stop, board.knightPromotionFlag);; break;
                        case 2: proposed_move = new Move(start, stop, board.bishopPromotionFlag);; break;
                        case 3: proposed_move = new Move(start, stop, board.rookPromotionFlag);; break;
                        case 4: proposed_move = new Move(start, stop, board.queenPromotionFlag);; break;
                    }

                    if (board.GenerateMoves().Contains(proposed_move)) 
                    {

                        int newPiece = graphicalBoard.promotedTo;
                        if (!(board.isWhite))
                        {
                            newPiece = newPiece + 6;
                        }

                        pos = Pos(start);
                        graphicalBoard.pieces[pos[0], pos[1]] = null;
                        graphicalBoard.selectedPiece.transform.position = this.transform.position + new Vector3(0, 0, -1);

                        switch (newPiece) {
                            case 7: graphicalBoard.selectedPiece.GetComponent<SpriteRenderer>().sprite = graphicalBoard.selectedPiece.black_knight; break;
                            case 8: graphicalBoard.selectedPiece.GetComponent<SpriteRenderer>().sprite = graphicalBoard.selectedPiece.black_bishop; break;
                            case 9: graphicalBoard.selectedPiece.GetComponent<SpriteRenderer>().sprite = graphicalBoard.selectedPiece.black_rook; break;
                            case 10: graphicalBoard.selectedPiece.GetComponent<SpriteRenderer>().sprite = graphicalBoard.selectedPiece.black_queen; break;

                            case 1: graphicalBoard.selectedPiece.GetComponent<SpriteRenderer>().sprite = graphicalBoard.selectedPiece.white_knight; break;
                            case 2: graphicalBoard.selectedPiece.GetComponent<SpriteRenderer>().sprite = graphicalBoard.selectedPiece.white_bishop; break;
                            case 3: graphicalBoard.selectedPiece.GetComponent<SpriteRenderer>().sprite = graphicalBoard.selectedPiece.white_rook; break;
                            case 4: graphicalBoard.selectedPiece.GetComponent<SpriteRenderer>().sprite = graphicalBoard.selectedPiece.white_queen; break;
                        }

                        pos = Pos(stop);
                        graphicalBoard.pieces[pos[0], pos[1]] = graphicalBoard.selectedPiece;
                        //graphicalBoard.selectedPiece.name = stop.ToString();
                        board.MakeMove(proposed_move);
                    }

                }
            }
            graphicalBoard.OnMoveCompleted(); 
        }
    }

    public int SquareIndex() 
    {
        float x = this.transform.position.x;
        float y = this.transform.position.y;

        int rank = (int)(3.5f + y);
        int file = (int)(3.5f + x);

        int index = (rank * 8) + (7 - file);
        return index;

    }

    public int[] Pos(int index) 
    {
        int[] pos = new int[2];

        pos[1] = 7 - (index % 8);
        pos[0] = index / 8; 

        return pos;
    }

    private void InitializeRenderer()
    {
        tileRenderer = GetComponent<SpriteRenderer>();

        originalColor = tileRenderer.color;
    }

    public void Highlight()
    {    
        tileRenderer.color = highlightColor;
        //Debug.Log("Active");
    }

    public void HighlightCapture()
    {   
        tileRenderer.color = captureHighlightColor;

    }

    public void ClearHighlight()
    {
        tileRenderer.color = originalColor;
    }
    
    private void Awake()
    {
        InitializeRenderer();
    }


    // Start is called before the first frame update
    void Start()
    {
        graphicalBoard = GameObject.Find("Graphical Board").GetComponent<GraphicalBoard>();
        board = graphicalBoard.board;
        clock = GameObject.Find("Clock Canvas").GetComponent<Clock>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
