using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ChessMan : MonoBehaviour
{
    public Sprite black_king, black_queen, black_rook, black_bishop, black_knight, black_pawn;
    public Sprite white_king, white_queen, white_rook, white_bishop, white_knight, white_pawn;
    public GraphicalBoard GraphicalBoardScript;
    public Board BoardScript;

    public void Activate (int pieceType)
    {
        switch (pieceType) {
            case 6 : this.GetComponent<SpriteRenderer>().sprite = black_pawn; this.name = "Black Pawn"; break;
            case 7 : this.GetComponent<SpriteRenderer>().sprite = black_knight; this.name = "Black Knight"; break;
            case 8 : this.GetComponent<SpriteRenderer>().sprite = black_bishop; this.name = "Black Bishop"; break;
            case 9 : this.GetComponent<SpriteRenderer>().sprite = black_rook; this.name = "Black Rook"; break;
            case 10 : this.GetComponent<SpriteRenderer>().sprite = black_queen; this.name = "Black Queen"; break;
            case 11 : this.GetComponent<SpriteRenderer>().sprite = black_king; this.name = "Black King"; break;

            case 0 : this.GetComponent<SpriteRenderer>().sprite = white_pawn; this.name = "White Pawn"; break;
            case 1 : this.GetComponent<SpriteRenderer>().sprite = white_knight; this.name = "White Knight"; break;
            case 2 : this.GetComponent<SpriteRenderer>().sprite = white_bishop; this.name = "White Bishop"; break;
            case 3 : this.GetComponent<SpriteRenderer>().sprite = white_rook; this.name = "White Rook"; break;
            case 4 : this.GetComponent<SpriteRenderer>().sprite = white_queen; this.name = "White Queen"; break;
            case 5 : this.GetComponent<SpriteRenderer>().sprite = white_king; this.name = "White King"; break;
        }
    }

    private void OnMouseDown()
    {
        GraphicalBoardScript = GameObject.Find("Graphical Board").GetComponent<GraphicalBoard>();
        BoardScript = GameObject.Find("Board").GetComponent<Board>(); 

        int index = PieceIndex(); // stores index of selected piece
        bool correctColourClicked = (BoardScript.OccupyingPiece(index) < 6) == BoardScript.isWhite; // find if right colour was selected 

        if (!GraphicalBoardScript.Clicked && correctColourClicked) {
            // were there no other clicked pieces and did we click the right colour as well 
            Debug.Log("To Move");
            GraphicalBoardScript.Clicked = true;
            GraphicalBoardScript.selectedPiece = this;
        }

        else
        {

            if (GraphicalBoardScript.Clicked && !correctColourClicked) {
                // capture block
                // was there already a selected piece and was it a different colour from what we clicked 
                int start = GraphicalBoardScript.selectedPiece.PieceIndex();
                int stop = PieceIndex();
                Move proposed_move = new Move(start, stop, BoardScript.captureFlag); // check if the non special version of the move can be played, if not search for special move

                if (BoardScript.GenerateMoves().Contains(proposed_move)) {
                    Debug.Log("Takes");
                    Vector3 position = this.transform.position;
                    this.transform.position = new Vector3(10, 10, 1); // i have not figured how to delete the captured piece
                    GraphicalBoardScript.selectedPiece.transform.position = position;
                    GameObject.Find(stop.ToString()).GetComponent<ChessMan>().name = "deleted";
                    GraphicalBoardScript.selectedPiece.name = stop.ToString();
                    BoardScript.MakeMove(proposed_move); 
                }
                else if ((BoardScript.OccupyingPiece(start) % 6) == 0) // if pawn on the start square
                { 
                    proposed_move = new Move(start, stop, BoardScript.queenPromotionFlag);// check for queen promotion checks for all promotions i think
                    if (BoardScript.GenerateMoves().Contains(proposed_move)) {
                        int newPiece = GraphicalBoardScript.promotedTo;
                        if (!(BoardScript.isWhite))
                        {
                            newPiece = newPiece + 6;
                        }
                        Vector3 position = this.transform.position;
                        this.transform.position = new Vector3(10, 10, 1); // i have not figured how to delete the captured piece
                        GraphicalBoardScript.selectedPiece.transform.position = position;
                        GameObject.Find(stop.ToString()).GetComponent<ChessMan>().name = "deleted";

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

            GraphicalBoardScript.Clicked = false;
        }
        Debug.Log(BoardScript.occupied.ToString()); 
    }

    public int PieceIndex() {
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
       
    }
    
}
