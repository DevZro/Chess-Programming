using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DarkSquare : MonoBehaviour
{
    public GraphicalBoard GraphicalBoardScript;
    public Board BoardScript;

    private void OnMouseDown()
    {   
         
        if (GraphicalBoardScript.Clicked) {
            GraphicalBoardScript.Clicked = false;
            int start = GraphicalBoardScript.selectedPiece.PieceIndex();
            int stop = SquareIndex();
            Move proposed_move = new Move(start, stop, BoardScript.regularFlag); // ignore special move for now
            
            if (BoardScript.GenerateMoves().Contains(proposed_move)) {
                GraphicalBoardScript.selectedPiece.transform.position = this.transform.position + new Vector3(0, 0, -1); // needs to be adjusted          
                GraphicalBoardScript.selectedPiece.name = stop.ToString();
                BoardScript.MakeMove(proposed_move);
            }
            // check for castling
            else if ((BoardScript.OccupyingPiece(start) % 6) == 5) { // if king
                proposed_move = new Move(start, stop, BoardScript.castlingFlag);
                if (BoardScript.GenerateMoves().Contains(proposed_move)) {
                    GraphicalBoardScript.selectedPiece.transform.position = this.transform.position + new Vector3(0, 0, -1); // needs to be adjusted
    
                    if (start > stop) // kingside
                    {
                        int rookSquare = stop - 1;
                        ChessMan Rook = GameObject.Find(rookSquare.ToString()).GetComponent<ChessMan>();
                        Debug.Log(Rook.transform.position);                        
                        Rook.transform.position = this.transform.position + new Vector3(-1, 0, -1); // bug attractive code
                        Rook.name = (rookSquare + 2).ToString();
                        Debug.Log(Rook.transform.position);
                    }

                    else
                    {
                        int rookSquare = stop + 2;
                        ChessMan Rook = GameObject.Find(rookSquare.ToString()).GetComponent<ChessMan>();
                        Debug.Log(Rook.transform.position);                        
                        Rook.transform.position = this.transform.position + new Vector3(1, 0, -1);
                        Rook.name = (rookSquare - 3).ToString();
                        Debug.Log(Rook.transform.position);                
                    }
                    
                    GraphicalBoardScript.selectedPiece.name = stop.ToString();
                    BoardScript.MakeMove(proposed_move);
                }
            }

            else if ((BoardScript.OccupyingPiece(start) % 6) == 0) // if pawn
            {
                proposed_move = new Move(start, stop, BoardScript.doublePawnMoveFlag);
                if (BoardScript.GenerateMoves().Contains(proposed_move)) {
                    GraphicalBoardScript.selectedPiece.transform.position = this.transform.position + new Vector3(0, 0, -1);
                    GraphicalBoardScript.selectedPiece.name = stop.ToString();
                    BoardScript.MakeMove(proposed_move); 
                }

                proposed_move = new Move(start, stop, BoardScript.enpessantFlag);
                if (BoardScript.GenerateMoves().Contains(proposed_move)) {
                    GraphicalBoardScript.selectedPiece.transform.position = this.transform.position + new Vector3(0, 0, -1);
                    if (BoardScript.isWhite) {
                        ChessMan enpessanted = GameObject.Find((stop - 8).ToString()).GetComponent<ChessMan>();
                        enpessanted.transform.position = new Vector3(10, 10, 1);
                        enpessanted.name = "deleted";
                    }
                    else {
                        ChessMan enpessanted = GameObject.Find((stop + 8).ToString()).GetComponent<ChessMan>();
                        enpessanted.transform.position = new Vector3(10, 10, 1);
                        enpessanted.name = "deleted";
                    }
                    GraphicalBoardScript.selectedPiece.name = stop.ToString();
                    BoardScript.MakeMove(proposed_move); 
                }

                proposed_move = new Move(start, stop, BoardScript.queenPromotionFlag); // check for queen promotion checks for all promotions i think
                if (BoardScript.GenerateMoves().Contains(proposed_move)) {

                    int newPiece = GraphicalBoardScript.promotedTo;
                    if (!(BoardScript.isWhite))
                    {
                        newPiece = newPiece + 6;
                    }
                    GraphicalBoardScript.selectedPiece.transform.position = this.transform.position + new Vector3(0, 0, -1);

                    switch (newPiece) {
                        case 7: GraphicalBoardScript.selectedPiece.GetComponent<SpriteRenderer>().sprite = GraphicalBoardScript.selectedPiece.black_knight; break;
                        case 8: GraphicalBoardScript.selectedPiece.GetComponent<SpriteRenderer>().sprite = GraphicalBoardScript.selectedPiece.black_bishop; break;
                        case 9: GraphicalBoardScript.selectedPiece.GetComponent<SpriteRenderer>().sprite = GraphicalBoardScript.selectedPiece.black_rook; break;
                        case 10: GraphicalBoardScript.selectedPiece.GetComponent<SpriteRenderer>().sprite = GraphicalBoardScript.selectedPiece.black_queen; break;

                        case 1: GraphicalBoardScript.selectedPiece.GetComponent<SpriteRenderer>().sprite = GraphicalBoardScript.selectedPiece.white_knight; break;
                        case 2: GraphicalBoardScript.selectedPiece.GetComponent<SpriteRenderer>().sprite = GraphicalBoardScript.selectedPiece.white_bishop; break;
                        case 3: GraphicalBoardScript.selectedPiece.GetComponent<SpriteRenderer>().sprite = GraphicalBoardScript.selectedPiece.white_rook; break;
                        case 4: GraphicalBoardScript.selectedPiece.GetComponent<SpriteRenderer>().sprite = GraphicalBoardScript.selectedPiece.white_queen; break;
                    }


                    GraphicalBoardScript.selectedPiece.name = stop.ToString();
                    BoardScript.MakeMove(proposed_move);
                }

            }
        }
        //Destroy(this);
        Debug.Log(BoardScript.occupied.ToString()); 
    }

    public int SquareIndex() {
        float x = this.transform.position.x;
        float y = this.transform.position.y;

        int rank = (int)(3.5f + y);
        int file = (int)(3.5f + x);

        int index = (rank * 8) + (7 - file);
        return index;

    }
    

    // Start is called before the first frame update
    void Start()
    {
       GraphicalBoardScript = GameObject.Find("Graphical Board").GetComponent<GraphicalBoard>();
       BoardScript = GameObject.Find("Board").GetComponent<Board>(); 
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
