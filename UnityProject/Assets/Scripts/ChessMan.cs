using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ChessMan : MonoBehaviour
{
    public Sprite black_king, black_queen, black_rook, black_bishop, black_knight, black_pawn;
    public Sprite white_king, white_queen, white_rook, white_bishop, white_knight, white_pawn;
    public GraphicalBoard graphicalBoard;
    public Board board;
    public Clock clock;

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
        if (!graphicalBoard.currentPlayer.IsHuman) return;
        if (graphicalBoard.isGameOn)
        {
            
            int index = PieceIndex(); // stores index of selected piece
            int[] pos = new int[2];
            int startsquare;
            int stopsquare;
            int flag;
            bool correctColourclicked = (board.OccupyingPiece(index) < 6) == board.isWhite; // find if right colour was selected 

            if (correctColourclicked) {
                // did we click the right colour
                //Debug.Log("To Move");

                // we have to clear highlights in case a piece is already clicked
                // we have to changed clicked to true in case a piece isn't currently clicked
                for (int i = 0; i < 64; i++) 
                {
                    int file = 7 - (i % 8);
                    int rank = i / 8;
                    
                    graphicalBoard.tiles[rank, file].ClearHighlight();
                }
                graphicalBoard.clicked = true;
                graphicalBoard.selectedPiece = this;
                
                // for hihlighting squares
                // if a piece is clicked while no other are currently clicked and it is the correct colour
                // then the possible pieces should be highlighted
                foreach (Move move in board.GenerateMoves()) 
                {
                    startsquare = move.data & 0x003F;
                    stopsquare = (move.data >> 6) & 0x003F;
                    flag = (move.data >> 12) & 0x000F;

                    pos = Pos(stopsquare);
                    if (startsquare == index)
                    {
                        if (graphicalBoard.pieces[pos[0], pos[1]] != null)
                        {
                            graphicalBoard.tiles[pos[0], pos[1]].HighlightCapture();
                        }
                        else
                        {
                           graphicalBoard.tiles[pos[0], pos[1]].Highlight(); 
                        }
                    }   
                }
            }

            else
            {                
                if (graphicalBoard.clicked && !correctColourclicked) {
                    // capture block
                    // was there already a selected piece and was it a different colour from what we clicked 
                    int start = graphicalBoard.selectedPiece.PieceIndex();
                    int stop = PieceIndex();
                    

                    Move proposed_move = new Move(start, stop, board.captureFlag); // check if the non special version of the move can be played, if not search for special move

                    if (board.GenerateMoves().Contains(proposed_move)) {
                        //Debug.Log("Takes");
                        Vector3 position = this.transform.position;
                        this.transform.position = new Vector3(10, 10, 1); // i have not figured how to delete the captured piece

                        pos = Pos(start);
                        graphicalBoard.pieces[pos[0], pos[1]] = null;
                        graphicalBoard.selectedPiece.transform.position = position;

                        pos = Pos(stop);
                        graphicalBoard.pieces[pos[0], pos[1]] = graphicalBoard.selectedPiece;

                        //GameObject.Find(stop.ToString()).GetComponent<ChessMan>().name = "deleted";
                        //graphicalBoard.selectedPiece.name = stop.ToString();
                        board.MakeMove(proposed_move);
                    }
                    else if ((board.OccupyingPiece(start) % 6) == 0) // if pawn on the start square
                    { 
                        switch (graphicalBoard.promotedTo) 
                        {
                            case 1: proposed_move = new Move(start, stop, board.knightPromotionFlag);; break;
                            case 2: proposed_move = new Move(start, stop, board.bishopPromotionFlag);; break;
                            case 3: proposed_move = new Move(start, stop, board.rookPromotionFlag);; break;
                            case 4: proposed_move = new Move(start, stop, board.queenPromotionFlag);; break;
                        }
                        
                        if (board.GenerateMoves().Contains(proposed_move)) {
                            int newPiece = graphicalBoard.promotedTo;
                            if (!(board.isWhite))
                            {
                                newPiece = newPiece + 6;
                            }
                            Vector3 position = this.transform.position;
                            this.transform.position = new Vector3(10, 10, 1); // i have not figured how to delete the captured piece

                            pos = Pos(start);
                            graphicalBoard.pieces[pos[0], pos[1]] = null;
                            graphicalBoard.selectedPiece.transform.position = position;
                            //GameObject.Find(stop.ToString()).GetComponent<ChessMan>().name = "deleted";

                            pos = Pos(stop);
                            graphicalBoard.pieces[pos[0], pos[1]] = graphicalBoard.selectedPiece;

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


                            //graphicalBoard.selectedPiece.name = stop.ToString();
                            board.MakeMove(proposed_move);
                        }
                    }

                    for (int i = 0; i < 64; i++) 
                    {
                        int file = 7 - (i % 8);
                        int rank = i / 8;
                        
                        graphicalBoard.tiles[rank, file].ClearHighlight();
                    }
                    graphicalBoard.clicked = false;
                }

                
            }

            graphicalBoard.OnMoveCompleted();
        }
    }

    public int PieceIndex() {
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

    // Start is called before the first frame update

    void Start()
    {
       graphicalBoard = GameObject.Find("Graphical Board").GetComponent<GraphicalBoard>();
       board = GameObject.Find("Board").GetComponent<Board>(); 
       clock = GameObject.Find("Clock Canvas").GetComponent<Clock>();
    }
    
}
