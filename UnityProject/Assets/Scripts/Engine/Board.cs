using System;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using Utils;

/* 
    The Move struct is a custom data type that represents the a particular move. It is a 16 bit unsigned integer.
    The least significant 6 bits store the start square of the piece, the next 6 bits stores the destination of the piece and the last 4 bits signify a
    special flag depending on the type of move. 
    The struct is designed to be lightweight as it will be a highly used data type, thus it is fairly small and has no methods.
    */


public class Board// All methods, struts and classes related to the Chess Operations are stored in the Chess namespace. 
{
    /* 
    This is the major class and data type that forms the backbone of the program.
    It represents the chessBoard and all its properties and functionality.
    */
        /* 
        The fields of the ChessBoard class are initialised
        */

        // The ChessBoard stores the current positions of all pieces using bitboards
        // All of these bitboards are to be updated and recalculated after every move is played
        public ulong[] bitboards = new ulong[12]; // an array of bitboards of all piece types
        public ulong occupied; // bitboard of all currently occupied square
        private ulong empty;  // bitboard of all empty squares but would probably go given it can be easily found as ~occupied
        public ulong whiteOccupied; // bitboard of all squares occupied by the white pieces
        public ulong blackOccupied; // bitboard of all squares occupied by the black pieces


        // look up table for all squares a knight can attack from every given position
        private static readonly ulong[] KnightAttackTable = new ulong[64]; 

        public bool isWhite;

        /* 
        A boolean array for storing all available castling rights in the other
        [BKS, BQS, WKS, WQS]
        The castlingRights stored are designed to remain false one changed from true thus only permanent changes count as a lost castling right.
        i.e. A check or an obstructing piece doesn't count as an unavailable castling right as it could return at a later move but a moved king or rook does. 
        */
        private bool[] castlingRights = new bool[4]; 


        // The Flags for creating move structs
        public int nullFlag = 0;
        public int regularFlag = 1;
        public int captureFlag = 2;
        public int castlingFlag = 3;
        public int doublePawnMoveFlag = 4;
        public int enpessantFlag = 5;
        
        public int knightPromotionFlag = 6;
        public int bishopPromotionFlag = 7;
        public int rookPromotionFlag = 8;
        public int queenPromotionFlag = 9;

        // for the 50 move rule
        // records how many halfmoves since a pawn move or capture
        public int halfMoveCounter;
        // records what move the game currently is on
        public int moveCounter;
        public int HalfMoveShift = 30; // the number of bits to shift to get to the halfMoveCounter in the ulong stored in boardStateHistory
        public ulong HalfMoveMask = 0x7F; // half-move counter never exceeds 100 so 7 bits is enough to store it, thus the mask is 0x7F

        public string STARTING_POSITION = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        
        /*
        Stores the value of the square that potentially could be an enpessant destination.
        Just like with the castlingRights, an enpessant need not be possible for it to store a value.
        */
        public int enpessantSquare;

        // Stack implemented to help undo moves,
        // doesn't store moves but instead ulongs that provide enough info undo a move
        // this is because previous moves by themselves are not enough to undo a move 
        // Stores 64 bit ulongs that stores, the previous move, any captured piece type, the enpessant and castling rights before the move was played...
        // and the number of halfmoves since a pawn move or capture for the 50 move rule 
        public Stack<ulong> boardStateHistory = new Stack<ulong>();

        public ulong currentZobristHash;
        private Dictionary<ulong, int> positionHistory = new Dictionary<ulong, int>();
        public bool claimThreeFold;
        public bool claimInsufficientMaterial;

        /*
        Currently, the only functionality that requires being in the static construtor is the precomputing of the KnightAttackTable
        */
        static Board()
        {
            for (int square = 0; square < 64; square++)
            {
                KnightAttackTable[square] = ComputeKnightAttacks(square); // fills the KnightAttackTable based on the results of the defined method
            }
        }

        public Board Clone()
        {
            var copy = (Board) MemberwiseClone();
            copy.bitboards          = (ulong[]) bitboards.Clone();
            copy.castlingRights     = (bool[])  castlingRights.Clone();
            copy.boardStateHistory  = new Stack<ulong>(boardStateHistory.Reverse());
            copy.positionHistory    = new Dictionary<ulong,int>(positionHistory);
            return copy;
        }


        /*
        Method for computing the bitboard of attacked sqaures of a Knight on a given square
        The method is static as it is intended to be used in the static constructor
        */
        private static ulong ComputeKnightAttacks(int square)
        {
            ulong attacks = 0;
            ulong knight = 1UL << square;

            // The proposed attacks are anded with a bitboard that zeros said attack if it involves edge of board wrap around shenanigans
            attacks |= (knight << 17) & ~0x0101010101010101UL;
            attacks |= (knight << 15) & ~0x8080808080808080UL;
            attacks |= (knight << 10) & ~0x0303030303030303UL;
            attacks |= (knight << 6) & ~0xC0C0C0C0C0C0C0C0UL;
            attacks |= (knight >> 6) & ~0x0303030303030303UL;
            attacks |= (knight >> 10) & ~0xC0C0C0C0C0C0C0C0UL;
            attacks |= (knight >> 15) & ~0x0101010101010101UL;
            attacks |= (knight >> 17) & ~0x8080808080808080UL;
            return attacks;
        }

        
        // initialise an object based on a fen string
        // update the state of the object
        public void LoadFen(string Fen)
        {
            Dictionary<char, int> pieceIndex = new Dictionary<char, int>() 
            {
                {'p', 0},
                {'n', 1},
                {'b', 2},
                {'r', 3},
                {'q', 4},
                {'k', 5}
            };
            string[] stateVariables = Fen.Split(' ');

            int walk = 0;
            foreach (char c in stateVariables[0])
            {
                if (c == '/')
                {
                    continue;
                }

                if (((int) c < 57) && ((int) c > 48)) // ie between 1 and 9
                {
                    walk += (int) c - 48; // if number, add that to walk
                    continue;
                }
                
                // update appropriate bitboard
                // upper case are white, while lower case are black and need a 6 offset
                // subtracting from 63 is important because of the orientation of FEN
                bitboards[(char.IsUpper(c)) ? pieceIndex[char.ToLower(c)] : pieceIndex[char.ToLower(c)] + 6] |= 1UL << (63 - walk); 
                walk += 1;
            }

            if (stateVariables[1] == "w") 
            {
                isWhite = true;
            }

            else 
            {
                isWhite = false;
            }

            if (stateVariables[2] != "-") // skip if dash
            {
                foreach (char c in stateVariables[2]) // update castling rights, default is all false so no need to restate
                {
                    if (c == 'K')
                    {
                        castlingRights[0] = true;
                    }
                    else if (c == 'Q')
                    {
                        castlingRights[1] = true;
                    }
                    else if (c == 'k')
                    {
                        castlingRights[2] = true;
                    }
                    else
                    {
                        castlingRights[3] = true;
                    }
                }
            }

            if (stateVariables[3] == "-") // enpessant
            {
                enpessantSquare = 0;
            }

            else
            {
                int file ;
                int rank ;

                char [] rankandfile = stateVariables[3].ToCharArray();

                file = 104 - ((int) rankandfile[0]) ;
                rank = (int) rankandfile[1] - 49;

                enpessantSquare = (rank * 8) + file;
            }

            halfMoveCounter = int.Parse(stateVariables[4]);
            moveCounter = int.Parse(stateVariables[5]);
            UpdateOccupiedAndEmpty();
            InitializeZobrist();

        }

        // updates the 4 occupied and empty bitboards
        public void UpdateOccupiedAndEmpty()
        {
            whiteOccupied = 0UL;
            blackOccupied = 0UL;

            for (int i = 0; i < 6; i++)
            {
                whiteOccupied |= bitboards[i];
                blackOccupied |= bitboards[i+6];
            }

            occupied = whiteOccupied | blackOccupied;
            empty = ~occupied;
        }

        public int OccupyingPiece(int square)
        {
            ulong walk = 1UL << square; 
            /*if ((walk & occupied) == 0)
            {
                return -1;
            }*/
            
            for (int i = 0; i < 12; i++)
            {
                if ((bitboards[i] & walk) != 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /*
        Adds a ulong to the boardStateHistory stack
        structure of the ulong is this...
        least 16 significant bits store the data of the move made,
        next 4 bits represent the index of the piece type that was captured, if nothing was captured, it will default to all 1s
        This is an ugly fix but it is a result of the way indices storing has been implemented
        3 instead of 4 bits could have been used if the isWhite bool was used to figure out the colour of the index but 4 is even so 4
        the next 4 bits store the entire castlingrights bool array
        the next 6 bits store the location of the enpessant square
        the next 2 bits are currently unused
        Finally the last 32 bits are used to store the half move counter
        */
        private void UpdateBoardStateHistory(Move move, int j)
        {
            // Because it involves knowing the exact board state before the move was played,
             // this method must be called before any of the states are updated,
             // probably during the second loop

            ulong boardState = 0;
            boardState |= move.data;
            boardState |= (ulong)j << 16;
            for(int i = 0; i < 4; i++)
            {
                if (castlingRights[i])
                {
                    boardState |= (ulong)1 << (20 + i);
                }
            }
            boardState |= (ulong) enpessantSquare << 24;
            boardState |= (ulong) halfMoveCounter << HalfMoveShift;

            boardStateHistory.Push(boardState);
        }

        // initialises the Zobrist Hashing
        // should be called once when loading FEN
        // records initial position as occurred once
        // there may be need for flexibility with how many times the "initial position" occured or even adding a prior history
        // we can only load positions from FEN and since such info are not included in FENs it is not yet neccesary
        public void InitializeZobrist()
        {
            currentZobristHash = ComputeFullZobristHash();
            positionHistory[currentZobristHash] = 1;   
        }

        // updates zobrist after move is "made"
        // is called in the MakeMove function
        // called after updating castling rights and enpessant so needs access to previous castling rights and enpessant
        public void UpdateZobristAfterMove(Move move, int pieceIndex, int captureIndex, bool[] oldCastlingRights, int oldEnpessantSquare)
        {
            int startsquare = move.data & 0x003F;
            int stopsquare = (move.data >> 6) & 0x003F;

            // 1. Remove piece from old square
            currentZobristHash ^= Zobrist.pieceKeys[pieceIndex, startsquare];

            // 2. Put piece on new square
            currentZobristHash ^= Zobrist.pieceKeys[pieceIndex, stopsquare];

            // 3. If capture, remove captured piece
            if (captureIndex != 15) // since 15 = empty
                currentZobristHash ^= Zobrist.pieceKeys[captureIndex, stopsquare];

            // 4. Side to move changes
            currentZobristHash ^= Zobrist.sideKey;

            // 5. Castling rights changed? 
            for (int i = 0; i < 4; i++)
            {
                if (oldCastlingRights[i] != castlingRights[i])
                {
                    currentZobristHash ^= Zobrist.castlingKeys[i];
                }
            }

            // 6. En passant changed? 
            if (oldEnpessantSquare != 0) // 0 means no enpessant available
            {   int oldFile = oldEnpessantSquare % 8;
                currentZobristHash ^= Zobrist.enPassantKeys[oldFile];
            }

            if (enpessantSquare != 0)
            {
                int file = enpessantSquare % 8;
                currentZobristHash ^= Zobrist.enPassantKeys[file];
            }

            // Record the new position
            if (!positionHistory.ContainsKey(currentZobristHash))
                positionHistory[currentZobristHash] = 0;
            positionHistory[currentZobristHash]++;
        }

        public void UpdateZobristAfterUndo(int originalSquare, int currentSquare, int capturedPieceIndex, bool [] oldCastlingRights, int oldEnpessantSquare)
        {
            int piece = OccupyingPiece(originalSquare); // called AFTER bitboards have been updated so will show piece as being on "originalSquare" 

            // Remove current position from history
            positionHistory[currentZobristHash]--;
            if (positionHistory[currentZobristHash] == 0)
                positionHistory.Remove(currentZobristHash);

            // === 1. Remove piece from stop square (reverse of placement) ===
            currentZobristHash ^= Zobrist.pieceKeys[piece, currentSquare];

            // === 2. Put piece back on start square ===
            currentZobristHash ^= Zobrist.pieceKeys[piece, originalSquare];

            // === 3. Restore captured piece if there was one ===
            if (capturedPieceIndex != 15)
                currentZobristHash ^= Zobrist.pieceKeys[capturedPieceIndex, currentSquare];

            // === 4. Toggle side back (reverse the turn) ===
            currentZobristHash ^= Zobrist.sideKey;

            // === 5. Castling Rights  ===
            for (int i = 0; i < 4; i++)
            {
                if (oldCastlingRights[i] != castlingRights[i])
                {
                    currentZobristHash ^= Zobrist.castlingKeys[i];
                }
            }

            // === 6. En Passant ===
            if (oldEnpessantSquare != 0) // 0 means no enpessant available
            {   int oldFile = oldEnpessantSquare % 8;
                currentZobristHash ^= Zobrist.enPassantKeys[oldFile];
            }

            if (enpessantSquare != 0)
            {
                int file = enpessantSquare % 8;
                currentZobristHash ^= Zobrist.enPassantKeys[file];
            }

            
        }

        // Full hash (used only at start)
        private ulong ComputeFullZobristHash()
        {
            ulong hash = 0;

            // All pieces on board
            for (int sq = 0; sq < 64; sq++)
            {
                int piece = OccupyingPiece(sq);
                if (piece != -1)
                    hash ^= Zobrist.pieceKeys[piece, sq];
            }

            // Side to move
            if (isWhite) hash ^= Zobrist.sideKey;

            // Castling rights 
            for (int i = 0; i < 4; i++)
            {
                if (castlingRights[i] == true)
                {
                    hash ^= Zobrist.castlingKeys[i];
                }
            }

            // En passant 
            if (enpessantSquare != 0)
            {
                int file = enpessantSquare % 8;
                hash ^= Zobrist.enPassantKeys[file];
            }

            return hash;
        }

        // Call this after every move in your Board class
        private void CheckForThreefoldRepetition()
        {
            if (positionHistory[currentZobristHash] >= 3)
            {
                claimThreeFold = true;
                return;
            }
            claimThreeFold = false;
            
        }

        // checked after update occupied
        // current working insufficient material check
        // check only for K v K, K + B v K, K + N v K
        private void CheckForInsufficientMaterial()
        {
            if ((blackOccupied == bitboards[11]) && (whiteOccupied == bitboards[5])) // K v K
            {
                claimInsufficientMaterial = true;
            } 

            else if (((whiteOccupied & ~bitboards[5]) == bitboards[1]) && (blackOccupied == bitboards[11])) // K + nN v K
            {
                if (BitboardUtils.IsSingleBit(bitboards[1])) // n = 1
                {
                    claimInsufficientMaterial = true;
                }
            }

            else if (((blackOccupied & ~bitboards[11]) == bitboards[7]) && (whiteOccupied == bitboards[5])) //K v K + nN
            {
                if (BitboardUtils.IsSingleBit(bitboards[7])) // n = 1
                {
                    claimInsufficientMaterial = true;
                }
            }

            else if (((whiteOccupied & ~bitboards[5]) == bitboards[2]) && (blackOccupied == bitboards[11])) // K + nB v K
            {
                if (BitboardUtils.IsSingleBit(bitboards[2]))  // n = 1
                {
                    claimInsufficientMaterial = true;
                }
            }

            else if (((blackOccupied & ~bitboards[11]) == bitboards[8]) && (whiteOccupied == bitboards[5])) //K v K + nB
            {
                if (BitboardUtils.IsSingleBit(bitboards[8])) // n = 1
                {
                    claimInsufficientMaterial = true;
                }
            }
            else // ability to change the position back to normal incase such a position was seen during search.
            {
                claimInsufficientMaterial = false;
            }
        }

        
        /*
        This method computes the bitboard of squares attacked by the play whose turn it is to play
        A square counts as being attacked as long as there is no obstruction to the piece,
        therefore friendly pieces can be "attacked", pinned pieces can 'attack" and Kings can "attack" defended pieces.
        Key detail is that the pawns do not "attack" squares they move to if it is not a capture.
        This fairly strange definition of an attack will help in determining legal moves as kings cannot move to "atacked" squares.
        Finally there is the point of enpassent and castling.
        The squares involved in castling are attacked not because of the move but because the rook attacks those squares anyways.
        As for enpessant, the square of the opponent pawn is not considered "attacked" because although a piece can vanish from that square,
        it matters not for determining legal moves in relation to a King as a King can not occupy such a square. 

        */
        private ulong ComputeAttacks(bool isWhite) // Generate bitboard of sqaures attacked by a side
        {
            ulong attacks = 0;

            int bishopIndex = isWhite ? 2 : 8;
            int rookIndex = isWhite ? 3 : 9;
            int queenIndex = isWhite ? 4 : 10;


            // the responsibility of ensuring the right bitboard is seleted is passed unto the Generate Attack methods
            attacks |= GeneratePawnAttacks(isWhite);
            attacks |= GenerateKnightAttacks(isWhite);
            attacks |= GenerateBishopAttacks(bishopIndex);
            attacks |= GenerateRookAttacks(rookIndex);
            attacks |= GenerateQueenAttacks(queenIndex);
            attacks |= GenerateKingAttacks(isWhite);
            
            return attacks;
        }


        // Computes the squares attacked by the pawns of the side to play
        // vertical squares do not count as attacked as a capture can not occur on them
        private ulong GeneratePawnAttacks(bool isWhite)
        {
            ulong attacks = 0;
            int pawnIndex = isWhite ? 0 : 6;

            ulong pawns = bitboards[pawnIndex];

            if (isWhite)
            {
                attacks = ((pawns << 9) & ~0x0101010101010101UL) | ((pawns << 7) & ~0x8080808080808080UL);
            }
            else
            {
                attacks = ((pawns >> 7) & ~0x0101010101010101UL) | ((pawns >> 9) & ~0x8080808080808080UL);
            }
            return attacks;
        }


        // Computes the squares attacked by the knights of the side to play
        // because the KnightAttackTable can only singular knights at a time, a while loop is needed
        // there are usually only one or two knights on the board if any so the loop should be too computationally expensive
        private ulong GenerateKnightAttacks(bool isWhite)
        {
            ulong attacks = 0;
            int knightIndex = isWhite ? 1 : 7;
            int square = 0;

            ulong knights = bitboards[knightIndex];

            while (true)
            {
                square = BitboardUtils.TrailingZeroCount(knights); // the square of the first knight is found
                if (square == 64) // if the square is 64, it means there are no kights left
                {
                    break;
                }

                attacks |= KnightAttackTable[square]; // or the precomputed attack with the already found attacks
                knights = knights - (1UL << square);  // remove the current knight from the bitboard
            }

            return attacks;
        }

        
        // Computes the squares attacked by the Bishops of the side to play
        // only 1 loops is needed despite the loop required to calculate the moves
        // this is although unlike knights where moves do not require loops, all bishops can have moves computed at once
        // there are technically 2 loops but the final 2 loops can be conceptually thought of as a loop over all the possible squares the bishop could attack
        // the fundamental idea behind this function is to project squares in each diretion till they hit the edge of the board or after they hit another piece 
        // Importantly, since the method is used to calculate squares that the king cannot move to, squares behind a king can be "attacked" since a king in check cannot move there
        private ulong GenerateBishopAttacks(int index)
        {
            ulong bishops = bitboards[index];
            ulong attacks = 0;
            ulong target;
            int kingIndex = (index > 5) ? 5 : 11; //index of opposing king
            ulong king = bitboards[kingIndex];
            
            if (bishops == 0)
            {
                return 0; // if there are no bishops, then there no bishop attacks
            }

            for (int i = 0; i < 4; i++)
            {
                target = bishops;
                switch (i)
                {
                    
                    case 0 : while (true)
                    {
                        target = (target << 9) & ~0x0101010101010101UL; // target can potentially be 0 if edge shenanigans
                        attacks |= target; // add new target squares to cummulative attacks
                        target &= (empty | king); // remove target square that hit another piece, which is not the opposing king
                        if (target == 0) 
                        {
                            break; // exit loop if all target squares have been exhausted
                        }
                        //Importantly, the loop is only broken out of after the adding the target square even if it hits another piece                       
                   }
                    break;

                    case 1 : while (true)
                    {
                        target = (target << 7) & ~0x8080808080808080UL;
                        attacks |= target;
                        target &= (empty | king);
                        if (target == 0)
                        {
                            break;
                        }
                    }
                    break;

                    case 2 : while (true)
                    {
                        target = (target >> 7) & ~0x0101010101010101UL;
                        attacks |= target;
                        target &= (empty | king);
                        if (target == 0)
                        {
                            break;
                        }
                    }
                    break;

                    case 3 : while (true)
                    {
                        target = (target >> 9) & ~0x8080808080808080UL;
                        attacks |= target;
                        target &= (empty | king);
                        if (target == 0)
                        {
                            break;
                        }
                    }
                    break;
                } 
            }
            return attacks;
        }

        // Fubdamentally the same as the Generation method for Bishops
        private ulong GenerateRookAttacks(int index)
        {
            ulong rooks = bitboards[index];
            ulong attacks = 0;
            ulong target;
            int kingIndex = (index > 5) ? 5 : 11; //index of opposing king
            ulong king = bitboards[kingIndex];

            if (rooks == 0)
            {
                return 0; 
            }

            for (int i =0; i < 4; i++)
            {
                target = rooks;

                switch (i)
                {
                    
                    case 0 : while (true)
                    {
                        target = (target << 8); // hitting the edge is automatically handled by overflow
                        attacks |= target;
                        target &= (empty | king);
                        if (target == 0)
                        {
                            break;
                        }
                    }
                    break;

                    case 1 : while (true)
                    {
                        target = (target << 1) & ~0x0101010101010101UL;
                        attacks |= target;
                        target &= (empty | king);
                        if (target == 0)
                        {
                            break;
                        }
                    }
                    break;

                    case 2 : while (true)
                    {
                        target = (target >> 1) & ~0x8080808080808080UL;
                        attacks |= target;
                        target &= (empty | king);
                        if (target == 0)
                        {
                            break;
                        }
                    }
                    break;

                    case 3 : while (true)
                    {
                        target = (target >> 8);
                        attacks |= target;
                        target &= (empty | king);
                        if (target == 0)
                        {
                            break;
                        }
                    }
                    break;

                } 
            }
            return attacks;
        }


        // the Queen Attack Generation works by passing the queen index as the index for both bishops and rooks and oring the results
        // this method is the entire reason the bishop and rook methods have indices
        // interestingly, this method requires no index since the queen is the only piece that can move like a queen
        // THERE EXISTS ADVANCED BOT CODING THAT REQUIRES CALULATING THE KING'S MOVES LIKE A QUEEN SO IT MAY BE A CHANGE FURTHER INTO THE FUTURE TO
        // ADD BACK THE INDEX
        private ulong GenerateQueenAttacks(int index)
        {
            return GenerateBishopAttacks(index) | GenerateRookAttacks(index); 
        }

       // conceptually very simple
        private ulong GenerateKingAttacks(bool isWhite)
        {
            ulong attacks = 0;
            int kingIndex = isWhite ? 5 : 11;

            ulong king = bitboards[kingIndex];

            attacks |= (king << 9) & ~0x0101010101010101UL;
            attacks |=  king << 8 ;
            attacks |= (king << 7) & ~0x8080808080808080UL;
            attacks |= (king << 1) & ~0x0101010101010101UL;
            attacks |= (king >> 1) & ~0x8080808080808080UL;
            attacks |= (king >> 7) & ~0x0101010101010101UL;
            attacks |=  king >> 8;
            attacks |= (king >> 9) & ~0x8080808080808080UL;

            return attacks;
        }


        /*
        Returns a tuple of 2 ulongs. The first representing the bitboard of all pinned pieces 
        and the second representing the bitboards of all pieces that are pinned but can still move in a restricted manner
        The second bitboard is naturally a subset of the first.
        The point of this method is vital
        Calculating the legal moves in a position is complex and the natural solution which is to check if that move has a response that loses the king
        is painfully slow.
        The solution is thus to keep track of all pieces that are pinned in the position and limit their moves or outright stop them from moving.
        This is of course only one part of the entire solution but an important part nonetheless
        A final note is the realisation that this method does not actually find all pinned pieces.
        A special case of enpessant exists which is very tricky to parse and will thus be handled using a separate logic 
        */
        private (ulong, ulong) GetPinnedPieces(bool isWhite)
        {
            int kingIndex = isWhite ? 5 : 11; // the index of the king whose side it is to play
            int bishopIndex = isWhite ? 8 : 2; // the bishop, rooks and knights are of the opponent as they are the pieces doing the pinning
            int rookIndex = isWhite ? 9 : 3;
            int queenIndex = isWhite ? 10 : 4;

            ulong playingPieces = isWhite ? whiteOccupied : blackOccupied;  // this represents the bitboard of pieces whose turn it is and can thus be pinned

            ulong king = bitboards[kingIndex];
            int kingSquare = BitboardUtils.TrailingZeroCount(king);

            ulong pinningPieces; // the bitboard of all pieces of a certain piece type that could potentially create a pin 
            ulong pinRay; // the bitboard of all squares between the king and all "pinningPieces"
            
            ulong pinnedPieces = 0; 
            ulong partiallyPinnedPieces = 0;

            int square;
            int squareDifference;
            int inBetweenSquares;
            ulong inBetweenBitboard;

            // Bishops 
            pinRay = GenerateRay(bishopIndex);
            pinningPieces = pinRay & bitboards[bishopIndex];
            
            int pinnedSquare;

            while (pinningPieces != 0)
            {
                square = BitboardUtils.TrailingZeroCount(pinningPieces);
                squareDifference = square - kingSquare;
                inBetweenBitboard = 0; // this variable is used in an update manner throughout the method. Not zeroing it can cause carry over errors

                if (squareDifference > 0)
                {
                    // Checks what direction to move to get from the king to the pinningPiece
                    // We loop in this direction to find some potentially pinned Pieces
                    // the (squareDifference % 9) == 0 checks if the bishop is northwest of the king 
                    // There seems to be an edge case where 63 is divisible by 7 as well and thus could also pass for the north east direction
                    // this is not an issue because the board is not long or wide enough for a north difference to be 63
                    if ((squareDifference % 9) == 0) 
                    {
                        inBetweenSquares = (squareDifference / 9) - 1; // number of steps to take to get from the king to the pinning Piece
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king << (i + 1) * 9;
                        }
                    }
                    else 
                    {
                        inBetweenSquares = (squareDifference / 7) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king << (i + 1) * 7;
                        }  
                    }
                }
                else
                {
                    if ((squareDifference % 9) == 0)
                    {
                        inBetweenSquares = Math.Abs(squareDifference / 9) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king >> (i + 1) * 9;
                        }
                    }
                    else 
                    {
                        inBetweenSquares = Math.Abs(squareDifference / 7);
                        for (int i = 0; i < (inBetweenSquares - 1); i++)
                        {
                            inBetweenBitboard |= king >> (i + 1) * 7;
                        }  
                    }  
                }

                // ands the inBetweenBitboard and playingPieces to get the bitboard of pieces that can be pinned and are in the scope of the piece
                // a piece is generally only pinned if it is the only piece in the scope
                // so the trailing zeros is added to the leading zeros and the sum is check
                // if it equals 63, it could only mean there is one piece in the bitboard
                if (BitboardUtils.IsSingleBit(inBetweenBitboard & playingPieces)){
                    pinnedSquare = BitboardUtils.TrailingZeroCount(inBetweenBitboard & playingPieces); // the square of said pinned piece
                    pinnedPieces |= 1Ul << pinnedSquare;

                    // a Piece is considered partially pinned if it can move in a restricted manner
                    // it is possible for bishops, queens or pawns to be partially pinned by a bishop
                    // the bitboards of all pieces that could be partially pinned are ored together
                    // this bitboard is then anded with the current pinned piece, it is is a match, then it is partially pinned
                    // Pawns are special because if a bishop pins them partially, then can generally not move until a piece is in position for a capture
                    // the pawn bitboard is found by uses bishopIndex + 4
                    // the % 12 helps because the current Indices are those of the pinning pieces so added 6 mod 12 reverts it to that of the pinned pieces
                    if (((bitboards[(bishopIndex + 6) % 12] | bitboards[(queenIndex + 6) % 12] | bitboards[(bishopIndex + 4) % 12]) & (1Ul << pinnedSquare)) != 0)
                    {
                        partiallyPinnedPieces |= 1Ul << pinnedSquare ;
                    }


                }
                pinningPieces -= 1UL << square;
            }

            //Rooks
            pinRay = GenerateRay(rookIndex);
            pinningPieces = pinRay & bitboards[rookIndex];

            while (pinningPieces != 0)
            {
                square = BitboardUtils.TrailingZeroCount(pinningPieces);
                squareDifference = square - kingSquare;
                inBetweenBitboard = 0;

                if (squareDifference > 0)
                {
                    if ((squareDifference % 8) == 0)
                    {
                        inBetweenSquares = (squareDifference / 8) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king << (i + 1) * 8;
                        }
                    }
                    else 
                    {
                        inBetweenSquares = (squareDifference / 1) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king << (i + 1);
                        }  
                    }
                }
                else
                {
                    if ((squareDifference % 8) == 0)
                    {
                        inBetweenSquares = Math.Abs(squareDifference / 8) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king >> (i + 1) * 8;
                        }
                    }
                    else 
                    {
                        inBetweenSquares = Math.Abs(squareDifference / 1) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king >> (i + 1);
                        }  
                    }  
                }

                if (BitboardUtils.IsSingleBit(inBetweenBitboard & playingPieces))
                {
                    pinnedSquare = BitboardUtils.TrailingZeroCount(inBetweenBitboard & playingPieces);
                    pinnedPieces |= 1Ul << pinnedSquare;

                    // a Piece is considered partially pinned if it can move in a restricted manner
                    // it is possible for rooks, queens or pawns to be partially pinned by a rook
                    // the bitboards of all pieces that could be partially pinned are ored together
                    // this bitboard is then anded with the current pinned piece, it is is a match, then it is partially pinned
                    // Pawns are special because if a rook pins them partially VERTICALLY, then can generally move as until a piece is in position for a capture, in which they can not capture at all
                    // If pawns are pinned horizontally though, they can not move at all and are actually fully pinned
                    // They are still called partially pinned because it is easier to apologise
                    // the pawn bitboard is found by uses bishopIndex + 4
                    // the % 12 helps because the current Indices are those of the pinning pieces so added 6 mod 12 reverts it to that of the pinned pieces
                    if (((bitboards[(rookIndex + 6) % 12] | bitboards[(queenIndex + 6) % 12] | bitboards[(bishopIndex + 4) % 12]) & (1Ul << pinnedSquare)) != 0)
                    {
                        partiallyPinnedPieces |= 1Ul << pinnedSquare ;
                    }
                }
                pinningPieces-= 1UL << square;
            }

            // Queens
            pinRay = GenerateRay(queenIndex);
            pinningPieces = pinRay & bitboards[queenIndex];
            bool asBishop; // variable to differentiate if a queen pins as a bishop or as a rook

            while (pinningPieces != 0)
            {
                square = BitboardUtils.TrailingZeroCount(pinningPieces);
                squareDifference = square - kingSquare;
                inBetweenBitboard = 0; // this variable is used in an update manner throughout the method. Not zeroing it can cause carry over errors

                // 8 directions are checked for queens instead of 4
                // for the north east direction, the mod 7 must be 0 but the rank number must also be different
                // if not this exposes the edge case of horizontal pins
                // it might seem like a wise decision to remove the condition of ranks altogether as a square difference of 7 could never actually be north east pin
                // the problem with this is that the pinRay generated can be of that type
                if (squareDifference > 0) 
                {
                    if ((squareDifference % 9) == 0) 
                    {
                        asBishop = true;
                        inBetweenSquares = (squareDifference / 9) - 1; 
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king << (i + 1) * 9;
                        }
                    }
                    else if ((squareDifference % 8) == 0)
                    {
                        asBishop = false;
                        inBetweenSquares = (squareDifference / 8) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king << (i + 1) * 8;
                        }
                    }
                    else if (((squareDifference % 7) == 0) && ((square / 8) != (kingSquare / 8)))
                    {
                        asBishop = true;
                        inBetweenSquares = (squareDifference / 7) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king << (i + 1) * 7;
                        }  
                    }
                    else 
                    {
                        asBishop = false;
                        inBetweenSquares = (squareDifference / 1) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king << (i + 1);
                        }  
                    }
                }
                else
                {
                    if ((squareDifference % 9) == 0)
                    {
                        asBishop = true;
                        inBetweenSquares = Math.Abs(squareDifference / 9) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king >> (i + 1) * 9;
                        }
                    }
                    else if ((squareDifference % 8) == 0)
                    {
                        asBishop = false;
                        inBetweenSquares = Math.Abs(squareDifference / 8) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king >> (i + 1) * 8;
                        }
                    }
                    else if (((squareDifference % 7) == 0) && (square / 8) != (kingSquare / 8))
                    {
                        asBishop = true;
                        inBetweenSquares = Math.Abs(squareDifference / 7);
                        for (int i = 0; i < (inBetweenSquares - 1); i++)
                        {
                            inBetweenBitboard |= king >> (i + 1) * 7;
                        }  
                    }
                    else 
                    {
                        asBishop = false;
                        inBetweenSquares = Math.Abs(squareDifference / 1) - 1;
                        for (int i = 0; i < inBetweenSquares; i++)
                        {
                            inBetweenBitboard |= king >> (i + 1);
                        }  
                    }    
                }


                if (BitboardUtils.IsSingleBit(inBetweenBitboard & playingPieces))
                {
                    pinnedSquare = BitboardUtils.TrailingZeroCount(inBetweenBitboard & playingPieces); // the square of said pinned piece
                    pinnedPieces |= 1Ul << pinnedSquare;
                    
                    if (asBishop) // if the queen pins as a bishop, then queens, bishops and pawns should be partially pinned
                    {
                        if (((bitboards[(bishopIndex + 6) % 12] | bitboards[(queenIndex + 6) % 12] | bitboards[(bishopIndex + 4) % 12]) & (1Ul << pinnedSquare)) != 0)
                        {
                            partiallyPinnedPieces |= 1Ul << pinnedSquare;
                        }
                    }
                    else // if the queen does not pin as a bishop, then queens, rooks and pawns should be partially pinned
                    {
                        if (((bitboards[(rookIndex + 6) % 12] | bitboards[(queenIndex + 6) % 12] | bitboards[(bishopIndex + 4) % 12]) & (1Ul << pinnedSquare)) != 0)
                        {
                            partiallyPinnedPieces |= 1Ul << pinnedSquare;
                        }
                    }
                }
                pinningPieces -= 1UL << square;
            }
            return (pinnedPieces, partiallyPinnedPieces);
        }

        // Returns a bitboard of squares between the king and the sliding piece type (bishop, rooks, queens) specified which includes the pieces in question
        // The pieces are included because it is required for the GetPinnedPiees method
        // A ray is only returned if there are no pieces of the same colour between the slider and the opposing king
        // The condition of same colour being "opaque" stems from the fact that the GenerateRay method is a helper to the GetPinnedPieces Method
        // A piece can generally not pin another if a piece of the same colour obstructs its line of sight to the king
        // Since opposing pieces are transparent, a generated ray doesn't equate to a pinned piece as there could be more than one or even no opposing piece between the slider and king
        // the GenerateRay method feels inefficient as the full rays are being generated for the purpose of just determining if the sliders could pin or not
        // a similar computation is redone in the GetPinnedPieces method
        // The working principle is to project rays from the king and see which sliders it hits
        private ulong GenerateRay(int slider)
        {
            int kingIndex = (slider > 6) ? 5 : 11; // the kingIndex will be of the opposite colour to the sliding piece
            ulong king = bitboards[kingIndex];
            ulong beam; // imaginary light projected from king in a direction
            ulong ray; // top of beam
            ulong xray = 0; // xray is the collection of all accepted beams
            int loop_start;
            int loop_end;

            ulong slidingPieces = bitboards[slider];

            ulong semiOccupied = (slider > 6) ? blackOccupied : whiteOccupied;

            if ((slider % 2) == 0 ) // Bishop or Queen
            {
                loop_start = 0;
            }
            else // Rook
            {
                loop_start = 4;
            }
            if ((slider % 6) == 2 ) // Bishop
            {
                loop_end = 4;
            }
            else // Rook or Queen
            {
                loop_end = 8;
            }

            for (int i = loop_start; i < loop_end; i++)
            {
                ray = king;
                beam = 0; // beam is zeroed after each direction is analysed

                switch (i)
                {
                    case 0 : while (true)
                    {
                        ray = (ray << 9) & ~0x0101010101010101UL;
                        if (ray == 0) // if ray hits the edge, the beam is useless
                        {
                            break;
                        }

                        beam |= ray;

                        if (((ray & slidingPieces) != 0)) // if ray hits slider, the beam is complete and can be added to the xray
                        {
                            xray |= beam;
                            break;
                        }

                        if ((ray & semiOccupied) != 0) // if ray hits a same colour piece that is not the slider, beam is useless
                        {
                            break;
                        }
                    }
                    break;

                    case 1 : while (true)
                    {
                        ray = (ray << 7) & ~0x8080808080808080UL;
                        if (ray == 0)
                        {
                            break;
                        }

                        beam |= ray;

                        if (((ray & slidingPieces) != 0))
                        {
                            xray |= beam;
                            break;
                        }

                        if ((ray & semiOccupied) != 0)
                        {
                            break;
                        }
                    }
                    break;

                    case 2 : while (true)
                    {
                        ray = (ray >> 7) & ~0x0101010101010101UL;
                        if (ray == 0)
                        {
                            break;
                        }

                        beam |= ray;

                        if (((ray & slidingPieces) != 0))
                        {
                            xray |= beam;
                            break;
                        }

                        if ((ray & semiOccupied) != 0)
                        {
                            break;
                        }
                    }
                    break;

                    case 3 : while (true)
                    {
                        ray = (ray >> 9) & ~0x8080808080808080UL;
                        if (ray == 0)
                        {
                            break;
                        }

                        beam |= ray;

                        if (((ray & slidingPieces) != 0))
                        {
                            xray |= beam;
                            break;
                        }

                        if ((ray & semiOccupied) != 0)
                        {
                            break;
                        }
                    }
                    break;

                    case 4 : while (true)
                    {
                        ray = ray << 8 ;
                        if (ray == 0)
                        {
                            break;
                        }

                        beam |= ray;

                        if (((ray & slidingPieces) != 0))
                        {
                            xray |= beam;
                            break;
                        }

                        if ((ray & semiOccupied) != 0)
                        {
                            break;
                        }
                    }
                    break;

                    case 5 : while (true)
                    {
                        ray = (ray << 1) & ~0x0101010101010101UL;
                        if (ray == 0)
                        {
                            break;
                        }

                        beam |= ray;

                        if (((ray & slidingPieces) != 0))
                        {
                            xray |= beam;
                            break;
                        }

                        if ((ray & semiOccupied) != 0)
                        {
                            break;
                        }
                    }
                    break;

                    case 6 : while (true)
                    {
                        ray = (ray >> 1) & ~0x8080808080808080UL;
                        if (ray == 0)
                        {
                            break;
                        }

                        beam |= ray;

                        if (((ray & slidingPieces) != 0))
                        {
                            xray |= beam;
                            break;
                        }

                        if ((ray & semiOccupied) != 0)
                        {
                            break;
                        }
                    }
                    break;

                    case 7 : while (true)
                    {
                        ray = ray >> 8 ;
                        if (ray == 0)
                        {
                            break;
                        }

                        beam |= ray;

                        if (((ray & slidingPieces) != 0))
                        {
                            xray |= beam;
                            break;
                        }

                        if ((ray & semiOccupied) != 0)
                        {
                            break;
                        }
                    }
                    break;
                }
            }
            return xray;
        }

        // returns an array of the integer amount of pieces currently checking the king, and the index of said piece
        // proxy for an isCheck function
        // the results can generally be 0 (no check), 1 (single check), 2 (double check)
        // this is preferred over an isCheck because move generation for double checks are much more different and easier than single checks
        // hopefully the computational cost of calculating the number of pieces checking is outweighed by the computational benefit of being able to easily identify double checks
        public int[] NumChecks(bool isWhite)
        {
            int kingIndex = isWhite ? 5 : 11;
            ulong king = bitboards[kingIndex];

            int num = 0;
            int index = 0;

            ulong pieceAttacks;

            pieceAttacks = GeneratePawnAttacks(!isWhite);
            if ((pieceAttacks & king) != 0)
            {
                index = isWhite ? 6 : 0;
                num += 1;
            }

            pieceAttacks = GenerateKnightAttacks(!isWhite);
            if ((pieceAttacks & king) != 0)
            {
                index = isWhite ? 7 : 1;
                num += 1;
            }

            pieceAttacks = GenerateBishopAttacks(isWhite ? 8 : 2); // if white is to play then we want to see the attacks of the black pieces
            if ((pieceAttacks & king) != 0)
            {
                index = isWhite ? 8 : 2;
                num += 1;
            }

            pieceAttacks = GenerateRookAttacks(isWhite ? 9 : 3);
            if ((pieceAttacks & king) != 0)
            {
                index = isWhite ? 9 : 3;
                num += 1;
            }

            pieceAttacks = GenerateQueenAttacks(isWhite ? 10 : 4);
            if ((pieceAttacks & king) != 0)
            {
                index = isWhite ? 10 : 4;
                num += 1;
            }

            // Kings can not attack Kings

            int[] numCheck = new int[2] {num, index};
            return numCheck;
        }

        // returns true if the stop square of a proposed move is NOT in a list of target square
        // this is used to show that the move should be filtered of a move list
        // this is not a true pseudo-legal filter as it assumes pins and such have already been taken care of
        public bool PseudoLegalFilter(Move proposedMove, List<int> targetSquares, int checkIndex)
        {
            int finalSquare = (proposedMove.data >> 6) & 0b0000111111;
            // if enpessant square is not 0, and the pseudolegalfilter has been called,
            // that means a king has been checked by a double pawn move, provided a pawn checked the king
            // all enpessants that would be legal if it was not a check, remain legal
            if (enpessantSquare != 0)
            {
                // allow move if there is a possible enpessant, the king was checked by a pawn AND the move is an enpessant move 
                if(((proposedMove.data >> 12) == enpessantFlag) && ((checkIndex % 6) == 0))
                {
                    return false;
                }
            }
            return (!targetSquares.Contains(finalSquare));
            
        }

        // Method to make a move on the board
        // The method assumes the input move is legal and was generated by the GenerateMoves method
        public void MakeMove(Move move)
        {
            int startsquare = move.data & 0x003F; // isolate the startsquare
            int stopsquare = (move.data >> 6) & 0x003F; // isolate the stopsquare
            int flag = (move.data >> 12) & 0x000F;  // isolate the flag

            // needed for reseting the half move counter 
            // prone to bugs as initialised as 0 means we start as a pawn
            // but the value WILL update in the loop... I think.
            int pieceIndex = 0;  
            int captureIndex = 15; // for updateBoardStateHistory default is all 1s if no captures

            // loop through first 6 bitboards to find the correct piece if white to play, last 6 if black
            int startIndex = isWhite ? 0 : 6;
            int stopIndex = isWhite ? 6 : 12;

            ulong piece;

            bool[] oldCastlingRights = (bool[])castlingRights.Clone();


            int oldEnpessantSquare = enpessantSquare;

            for (int i = startIndex; i < stopIndex; i++)
            {
                piece = bitboards[i];
                if ((piece & (1UL << startsquare)) == 0)
                {
                    continue; // if it is not a match, look through the next bitboard
                }
                bitboards[i] &= ~(1UL << startsquare);
                bitboards[i] |= 1UL << stopsquare;

                pieceIndex = i;
               // If it is not a regular flag, castling flag, or double pawn push flag, it could be a capture


                if ((flag != doublePawnMoveFlag) && (flag != enpessantFlag) && (flag != castlingFlag) && (flag != regularFlag)) 
                {
                // set indices for other colour
                    startIndex = isWhite ? 6 : 0;
                    stopIndex = isWhite ? 12 : 6;

                    for (int j = startIndex ; j < stopIndex; j++)
                    {
                        piece = bitboards[j];
                        if ((piece & (1UL << stopsquare)) == 0)
                        {
                            continue; // if it is not a match, look through the next bitboard
                        }
                        bitboards[j] &= ~(1UL << stopsquare); // set the captured piece to 0

                        captureIndex = j; // set it such that the captured index will reflect the captured piece type
                        break;
                    }                    
                }

                // update board history using the captureIndex
                // default will be all 1s (15) if no captures
                // board state variables are changed after updatingboardstatehistory
                // this is needed to keep the previous position state before updating to new 
                UpdateBoardStateHistory(move, captureIndex);          

                UpdateCastlingRights(i, startsquare, stopsquare, captureIndex);

                if (flag == regularFlag)
                {
                    break;
                }

                if (flag == castlingFlag)
                {
                    // if the king castles kingside, then rook moves from behind where the king ends up, else from 2 in front of where he ends up
                    bitboards[isWhite ? 3 : 9] &= ~(1UL << (((stopsquare % 8) == 1) ? (stopsquare - 1) : (stopsquare + 2))); 

                    // if the king castles kingside, then the rook moves to one ahead of stop square, else one behind
                    bitboards[isWhite ? 3 : 9] |= 1UL << (((stopsquare % 8) == 1) ? (stopsquare + 1) : (stopsquare - 1));
                    break;
                }

                if (flag == enpessantFlag)
                {
                    // remove the captured pawn
                    bitboards[isWhite ? 6 : 0] &= ~(1UL << (isWhite ? (stopsquare - 8) : (stopsquare + 8)));
                    break;
                }

                if (flag == captureFlag) // if capture, our work is done
                {
                    break;
                }

                if (flag == doublePawnMoveFlag)
                {
                    enpessantSquare = startsquare + (isWhite ? 8 : -8); // could have added start and stop and divided by 2 but divisions are slow
                    break;
                }

                // Only promotions are left

                bitboards[isWhite ? 0 : 6] &= ~(1UL << stopsquare); // remove pawn so it can become a new piece

                if ( flag == knightPromotionFlag)
                {
                    bitboards[isWhite ? 1 : 7] |= 1UL << stopsquare;
                }
                else if ( flag == bishopPromotionFlag)
                {
                    bitboards[isWhite ? 2 : 8] |= 1UL << stopsquare;
                }
                else if ( flag == rookPromotionFlag)
                {
                    bitboards[isWhite ? 3 : 9] |= 1UL << stopsquare;
                }
                else if ( flag == queenPromotionFlag)
                {
                    bitboards[isWhite ? 4 : 10] |= 1UL << stopsquare;
                }

                break;
            }

            if (flag != doublePawnMoveFlag) // enpessants expire quickly
            {
                enpessantSquare = 0;
            }

            if ((captureIndex != 15) || ((pieceIndex % 6) == 0))
            {
                halfMoveCounter = 0;
            }
            else
            {
                halfMoveCounter += 1;
            }
             

            // if black just played increase moveCounter by 1
            if (!isWhite)
            {
                moveCounter += 1;
            }
            isWhite = !isWhite;
            UpdateOccupiedAndEmpty();
            UpdateZobristAfterMove(move, pieceIndex, captureIndex, oldCastlingRights, oldEnpessantSquare );
            CheckForThreefoldRepetition();
            CheckForInsufficientMaterial();
        }

        public void UndoMove()
        {
            ulong currrentState = boardStateHistory.Pop();

            ulong piece;

            // step 1 is to move whatever moved back to where it came from
            int originalSquare = (int) (currrentState & 0x003F);
            int currentSquare = (int) ((currrentState >> 6) & 0x003F);
            int flag  = (int) ((currrentState >> 12) & 0x000F);

            int startIndex = isWhite ? 6 : 0;
            int stopIndex = isWhite ? 12 : 6;

            // variable implemented for promotions
            // stores the variable of index of the piece on the currentSquare
            int pieceonCurrentSquareIndex = -1;

            int capturedPieceIndex = ((int) currrentState >> 16) & 0x000F;
            bool[] oldCastlingRights = (bool[])castlingRights.Clone();

            int oldEnpessantSquare;

            for (int i = startIndex; i < stopIndex; i++) // look for the piece type that made a move
            {
                piece = bitboards[i];
                if ((piece & (1UL << currentSquare)) == 0)
                {
                    continue; // if it is not a match, look through the next bitboard
                }
                bitboards[i] &= ~(1UL << currentSquare);
                bitboards[i] |= 1UL << originalSquare;

                pieceonCurrentSquareIndex = i;
            }

            if (capturedPieceIndex != 15)
            {
                bitboards[capturedPieceIndex] |= 1UL << currentSquare;
            }

            if (flag == castlingFlag) //if castling, move the rook back to where it belongs
            {
                // if the king castled kingside, then rook moves to behind where the king ends up, else to 2 in front of where he ends up
                bitboards[isWhite ? 9 : 3] |= 1UL << (((currentSquare % 8) == 1) ? (currentSquare - 1) : (currentSquare + 2)); 

                // if the king castles kingside, then the rook moves from one ahead of stop square, else one behind
                bitboards[isWhite ? 9 : 3] &= ~(1UL << (((currentSquare % 8) == 1) ? (currentSquare + 1) : (currentSquare - 1)));
            }

            if (flag == enpessantFlag)
            {
                bitboards[isWhite ? 0 : 6] |= 1UL << (isWhite ? (currentSquare + 8) : (currentSquare - 8));    
            }
            
            if ((flag == knightPromotionFlag) || (flag == bishopPromotionFlag) || (flag == rookPromotionFlag) || (flag == queenPromotionFlag))
            {
                bitboards[pieceonCurrentSquareIndex] &= ~(1UL << originalSquare); //  remove piece from original square
                bitboards[isWhite ? 6 : 0] |= 1UL << originalSquare;
            }

            for (int i = 0; i < 4; i++)
            {
                castlingRights[i] = ((((ulong)1 << (20 + i)) & (currrentState)) == 0) ? false : true;
            }

            oldEnpessantSquare = enpessantSquare;

            enpessantSquare = ((int) currrentState  >> 24) & 0x003F;

            halfMoveCounter = (int) ((currrentState >> HalfMoveShift) & HalfMoveMask);
            
            // if it was white to play afte the undo then the moveCounter reduces by 1
            if (isWhite)
            {
                moveCounter += 1;
            }
            isWhite = !isWhite;
            UpdateOccupiedAndEmpty();
            UpdateZobristAfterUndo(originalSquare, currentSquare, capturedPieceIndex, oldCastlingRights, oldEnpessantSquare);
            CheckForThreefoldRepetition();
            CheckForInsufficientMaterial();           
        }

        public void UpdateCastlingRights(int pieceIndex, int startsquare, int stopsquare, int captureIndex)
        {
            if ((pieceIndex % 6) == 5) // if the king moved
            {
                if (isWhite)
                {
                    castlingRights[0] = false;
                    castlingRights[1] = false;
                }
                else
                {
                    castlingRights[2] = false;
                    castlingRights[3] = false;
                }
            }

            if ((pieceIndex % 6) == 3) // if the rook moved
            {
                // if a rook moves from the bottom right corner then there is no longer WKS castling. It does not matter if the rook is white 
                // or not, cause if it is not white, then white must have already lost that right
                if (startsquare == 0)  
                {
                    castlingRights[0] = false;
                }
                else if (startsquare == 7)
                {                       
                    castlingRights[1] = false;
                }
                else if (startsquare == 56)
                {
                    castlingRights[2] = false;
                }
                else if (startsquare == 63)
                {
                    castlingRights[3] = false;
                }
            }

            if (((captureIndex % 6) == 3) && (captureIndex != 15)) // if the rook was taken
            {
                if (stopsquare == 0)  
                {
                    castlingRights[0] = false;
                }
                else if (stopsquare == 7)
                {                       
                    castlingRights[1] = false;
                }
                else if (stopsquare == 56)
                {
                    castlingRights[2] = false;
                }
                else if (stopsquare == 63)
                {
                    castlingRights[3] = false;
                }
            }
        }


        public static string ConvertMoveToString(Move move)
        {
            char startfile = Convert.ToChar(104 -(((int) move.data & 0b111111) % 8));
            char endfile = Convert.ToChar(104 -(((int) move.data >> 6) & 0b111111) % 8);

            int startrank = (((int) move.data & 0b111111) / 8) + 1;
            int endrank = ((((int) move.data >> 6) & 0b111111) / 8) + 1;

            return($"{Char.ToString(startfile)}{startrank}{Char.ToString(endfile)}{endrank}");
        }

        /*
        Generates a return a list of all legal moves in a given position
        The method works by considering separately the case of no checks, single and double checks
        2 lists are created, one for all legal king moves and the other for all legal moves
        The legal king moves list is then filled with all king moves (excluding castling) that do not walk into check or try to capture pieces of the same type 
        These moves are return as the move list if the king is in double check
        After that the rest of the legal moves are computed, piece type by piece type
        In doing this, the non pinned pieces are first addressed before the complicated partially pinned pieces are tackled
        After that a filter is done to only allow moves that capture the checking piece, blocks the king 
        This list is then merged with the king moves list and returned for the case of the king in check
        Finally, if the king is not in check, no filter is done, but checks are finally generated and added to the move list which is joined with the king move list and returned

        A key detail is the special case of a pinned pawn that is stopped from performing enpessant by a rook or queen
        It has not been implicitly taken care of yet and additional care is taken to account for this extremely rare edge case
        */
        public List<Move> GenerateMoves(bool capturesOnly = false)
        {
            int kingIndex = isWhite ? 5 : 11;
            ulong king = bitboards[kingIndex];
            int kingSquare = BitboardUtils.TrailingZeroCount(king);

            int pieceIndex; // index of the bitboard of the piece type in question
            ulong pieces; // bitboard of said piece

            ulong sameColour = isWhite ? whiteOccupied : blackOccupied; // bitboard of the colour of pieces to play
            ulong otherColour = isWhite ? blackOccupied : whiteOccupied; // bitboard of the opposing colour

            int currentpieceSquare; // square of the piece whose moves are being generated
            ulong pieceDestinations; // bitboard of all the squares said piece can end up on

            int finalSquare; // a proposed final square of the current piece

            ulong attackedSquares = ComputeAttacks(!isWhite); // useful variable to show the squares that the king can not approach
            var (pinnedPieces, partiallyPinnedPieces) = GetPinnedPieces(isWhite); // all pinned and partially pinned pieces

            List<Move> kingMoves = new List<Move>(); // list for storing legal king moves
            List<Move> moves = new List<Move>(); // list for storing legal moves

            int[] NumCheckOutput = new int[2];
            NumCheckOutput = NumChecks(isWhite); // number of checks and the index of the attacker (only relevant for single checks)

            int numCheck = NumCheckOutput[0]; // number of checks
            int checkIndex = NumCheckOutput[1]; // index of attacker

            int walk;

            // King
            pieceDestinations = GenerateKingAttacks(isWhite) & ~attackedSquares & otherColour; // squares that are not defended and have an opposing piece is a valid CAPTURE

            while (pieceDestinations != 0)
            {
                kingMoves.Add(new Move(kingSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                pieceDestinations -= 1UL << BitboardUtils.TrailingZeroCount(pieceDestinations);
            }
            
            if (!capturesOnly)
            {
                pieceDestinations = GenerateKingAttacks(isWhite) & ~attackedSquares & empty; // squares that are not defended and are empty is a valid move

                while (pieceDestinations != 0)  
                {
                    kingMoves.Add(new Move(kingSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                    pieceDestinations -= 1UL << BitboardUtils.TrailingZeroCount(pieceDestinations);
                }
            }
            
            // DOUBLE CHECK
            if (numCheck == 2) // A double check implies only king moves are legal
            {
                return kingMoves;
            }

            // PAWNS

            // Pawns moves are not invariant of colour so white pawns must be handle slightly different from black pawns
            if (isWhite)
            {
                pieceIndex = 0; 

                // Pawns on the 2nd rank that are not pinned
                pieces = bitboards[pieceIndex] & ~pinnedPieces & 0x000000000000FF00UL; 
                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                    if (!capturesOnly)
                    {
                        pieceDestinations = 1UL << currentpieceSquare ;
                        for (int i = 0; i < 2; i++) // add vertical moves
                        {
                            pieceDestinations <<= 8;
                            if ((pieceDestinations & empty) == 0)
                            {
                                break;
                            }
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), (i == 0) ? regularFlag : doublePawnMoveFlag));
                            // takes care of double pawn moves as well
                        }
                        pieceDestinations = (1UL << (currentpieceSquare + 9)) & otherColour & ~0x0101010101010101UL; // checks if a north west piece is up for capture
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                        }
                    }

                    pieceDestinations = (1UL << (currentpieceSquare + 7)) & otherColour & ~0x8080808080808080UL; // checks if a north west piece is up for capture
                    if (pieceDestinations != 0)
                    {
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                    }
                    pieces -= 1UL << currentpieceSquare;
                }

                // Pawns between the 3rd and 6th ranks that are not pinned
                pieces = bitboards[pieceIndex] & ~pinnedPieces & 0x0000FFFFFFFF0000UL;

                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                    if (!capturesOnly)
                    {
                        pieceDestinations = 1UL << (currentpieceSquare + 8) ;

                        if ((pieceDestinations & empty) != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                        }
                    }

                    pieceDestinations = (1UL << (currentpieceSquare + 9)) & otherColour & ~0x0101010101010101UL;
                    if (pieceDestinations != 0)
                    {
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                    }

                    pieceDestinations = (1UL << (currentpieceSquare + 7)) & otherColour & ~0x8080808080808080UL;
                    if (pieceDestinations != 0)
                    {
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                    }
                    pieces -= 1UL << currentpieceSquare;
                } 
                
                // Pawns on the 7th rank and are not pinned (can promote)
                pieces = bitboards[pieceIndex] & ~pinnedPieces & 0x00FF000000000000UL;

                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                    if (!capturesOnly)
                    {
                        pieceDestinations = 1UL << (currentpieceSquare + 8) ;

                        if ((pieceDestinations & empty) != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), knightPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), bishopPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), rookPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), queenPromotionFlag));
                        }
                    }

                    pieceDestinations = (1UL << (currentpieceSquare + 9)) & otherColour & ~0x0101010101010101UL;
                    if (pieceDestinations != 0)
                    {
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), knightPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), bishopPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), rookPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), queenPromotionFlag));
                    }

                    pieceDestinations = (1UL << (currentpieceSquare + 7)) & otherColour & ~0x8080808080808080UL;
                    if (pieceDestinations != 0)
                    {
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), knightPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), bishopPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), rookPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), queenPromotionFlag));
                    }
                    pieces -= 1UL << currentpieceSquare;
                }
               
                // Pawns on the 2nd rank that are partially pinned
                pieces = bitboards[pieceIndex] & partiallyPinnedPieces & 0x000000000000FF00UL;

                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                    // if the pinned pawn is vertically in front of or behind the king then if must be pinned by a rook or queen and thus can still move vertically
                    if (!capturesOnly)
                    {
                        if (((currentpieceSquare - kingSquare) % 8) == 0) 
                        {
                            pieceDestinations = 1UL << currentpieceSquare ;
                            for (int i = 0; i < 2; i++)
                            {
                                pieceDestinations <<= 8;
                                if ((pieceDestinations & empty) == 0)
                                {
                                    break;
                                }
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), (i == 0) ? regularFlag : doublePawnMoveFlag));
                            }
                        }
                    }

                    // if the pinned pawn is pinned diagonally in front of or behind the king then if must be pinned by a bishop or queen and thus can still move diagonally
                    if (((currentpieceSquare - kingSquare) % 9) == 0) // north west
                    {
                        pieceDestinations = (1UL << (currentpieceSquare + 9)) & otherColour & ~0x0101010101010101UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                        }
                    }

                    if (((currentpieceSquare - kingSquare) % 7) == 0) // no need to check if the pawn has a different rank from the king since it the diffrence was 7, it could not be pinned horizontally
                    {
                        pieceDestinations = (1UL << (currentpieceSquare + 7)) & otherColour & ~0x8080808080808080UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                        }
                    }
                    pieces -= 1UL << currentpieceSquare;
                    // Pawns that are horizontally pinned are computed as partially pinned but can actually never move
                }

                // Pawns on the 3rd to 6th rank that are partially pinned
                pieces = bitboards[pieceIndex] & partiallyPinnedPieces & 0x0000FFFFFFFF0000UL;

                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);

                    if (!capturesOnly)
                    {
                        if (((currentpieceSquare - kingSquare) % 8) == 0)
                        {
                            pieceDestinations = 1UL << (currentpieceSquare + 8) ;
                            if ((pieceDestinations & empty) != 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                    }

                    if (((currentpieceSquare - kingSquare) % 9) == 0)
                    {
                        pieceDestinations = (1UL << (currentpieceSquare + 9)) & otherColour & ~0x0101010101010101UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                        }
                    }

                    if (((currentpieceSquare - kingSquare) % 7) == 0)
                    {
                        pieceDestinations = (1UL << (currentpieceSquare + 7)) & otherColour & ~0x8080808080808080UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                        }
                    }
                    pieces -= 1UL << currentpieceSquare;
                } 

                // Pawns on the 7th rank that are partially pinned
                pieces = bitboards[pieceIndex] & partiallyPinnedPieces & 0x00FF000000000000UL;

                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                    
                    // Pawns that are pinned vertically cannot promote because the pinning piece must be in front of them
                    if (((currentpieceSquare - kingSquare) % 9) == 0)
                    {
                        pieceDestinations = (1UL << (currentpieceSquare + 9)) & otherColour & ~0x0101010101010101UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), knightPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), bishopPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), rookPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), queenPromotionFlag));
                        }
                    }

                    if (((currentpieceSquare - kingSquare) % 7) == 0)
                    {
                        pieceDestinations = (1UL << (currentpieceSquare + 7)) & otherColour & ~0x8080808080808080UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), knightPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), bishopPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), rookPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), queenPromotionFlag));
                        }
                    }
                    pieces -= 1UL << currentpieceSquare;
                } 

                // Handles the case of a possible en pessant for a pawn
                if ((enpessantSquare >= 40) & (enpessantSquare < 48))
                {
                    int offset = 7;
                    int edgecheck = 0;
                    bool enpessantPinned;
                    ulong heavyPiecesonfifth;
                    int currentHeavyPieceSquare;
                    int effectiveDirection;

                    for (int i = 0; i < 2; i++) // considers enpessant for both possible pawns that could capture
                    {
                        currentpieceSquare = enpessantSquare - offset;

                        //check for edge shenanigans and if there is actually a pawn on the "currentPieceSquare" 
                        if (((currentpieceSquare % 8) != edgecheck) && (((1UL << currentpieceSquare) & bitboards[pieceIndex]) != 0))
                        {
                            if (((1UL << currentpieceSquare) & pinnedPieces) == 0) // if the pawn is NOT pinned
                            {
                                // There is a special case of pins for enpessant that must be checked for
                                // If the pawn separates a rook/queen and the king and there are no pawns between them, then the pawn is pinned
                                // We find all the rooks and queens on the fifth rank and check if any of them have only the 2 specific pawns in question between them and the king
                                // If any of them does, en pessant is illegal
                                // Key detail is that enpessant could still be illegal if the if this is not the case
                                // We let through the cases where there are more than just those 2 pawns between the rook/queen and king,
                                // but filter out cases with only those 2 pawns or nothing in between
                                // The second case is not en pessant but it is fine to filter it out since it is a check
                                // and enpessant is illegal during checks of a king on the fifth rank anyways
                                
                                enpessantPinned = false;
                                heavyPiecesonfifth = (bitboards[9] | bitboards[10]) & 0x000000FF00000000UL; // finds the rooks and queens on the fifth rank
                                currentHeavyPieceSquare = BitboardUtils.TrailingZeroCount(heavyPiecesonfifth) % 64; // mod 64 to stop the 64 as the output of 0
                                

                                if ((kingSquare >= 32) && (kingSquare < 40)) // if king is on fifth rank
                                {
                                    while (currentHeavyPieceSquare != 0)
                                    {
                                        heavyPiecesonfifth -= 1UL << currentHeavyPieceSquare;
                                        effectiveDirection = (kingSquare > currentHeavyPieceSquare) ? 1 : -1; // direction from rook to king
                                        walk = currentHeavyPieceSquare + effectiveDirection;
                                        while (true)
                                        {
                                            if (walk == kingSquare) //if the ray got to the king successfully, then enpessant is illegal by pin or check
                                            {
                                                enpessantPinned = true;
                                                break;
                                            }

                                            // if the ray is obstructed by anything that are not the 2 pawns, then enpessant is possible
                                            if (((walk != currentpieceSquare) && (walk != (enpessantSquare - 8))) && (((1UL << walk) & occupied) != 0))
                                            {                                  
                                                break;
                                            }
                                            walk += effectiveDirection;;
                                        }

                                        currentHeavyPieceSquare = BitboardUtils.TrailingZeroCount(heavyPiecesonfifth) % 64;

                                        
                                    }

                                    if (!(enpessantPinned)) // if enpessant is possible then add move
                                    {
                                        moves.Add(new Move(currentpieceSquare, enpessantSquare, enpessantFlag));
                                    }
                                }
                                else // if king is not on the fifth rank then enpessant is possible
                                {
                                    moves.Add(new Move(currentpieceSquare, enpessantSquare, enpessantFlag));
                                }
                            }

                                    
                            else // if pawn is traditionally pinned
                            {
                                // it can still play enpessant if the capture is in direction of the king
                                if (((currentpieceSquare - kingSquare) % (enpessantSquare - currentpieceSquare)) == 0) 
                                {
                                    moves.Add(new Move(currentpieceSquare, enpessantSquare, enpessantFlag));
                                }
                            }
                        }
                        offset = 9;
                        edgecheck = 7; 
                        // change the paramters to allow for next direction
                    }
                }              
            }

            else
            {
                pieceIndex = 6; 

                // Pawns on the 7th rank that are not pinned
                pieces = bitboards[pieceIndex] & ~pinnedPieces & 0x00FF000000000000UL; 
                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                    if (!capturesOnly)
                    {
                        pieceDestinations = 1UL << currentpieceSquare ;
                        for (int i = 0; i < 2; i++) // add vertical moves
                        {
                            pieceDestinations >>= 8;
                            if ((pieceDestinations & empty) == 0)
                            {
                                break;
                            }
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), (i == 0) ? regularFlag : doublePawnMoveFlag));
                            // takes care of double pawn moves as well
                        }
                    }
                    pieceDestinations = (1UL << (currentpieceSquare - 7)) & otherColour & ~0x0101010101010101UL; // checks if a south west piece is up for capture
                    if (pieceDestinations != 0)
                    {
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                    }

                    pieceDestinations = (1UL << (currentpieceSquare - 9)) & otherColour & ~0x8080808080808080UL; // checks if a south west piece is up for capture
                    if (pieceDestinations != 0)
                    {
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                    }
                    pieces -= 1UL << currentpieceSquare;
                }

                // Pawns between the 6th and 3rd ranks that are not pinned
                pieces = bitboards[pieceIndex] & ~pinnedPieces & 0x0000FFFFFFFF0000UL;

                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                    if (!capturesOnly)
                    {
                        pieceDestinations = 1UL << (currentpieceSquare - 8) ;

                        if ((pieceDestinations & empty) != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                        }
                    }

                    pieceDestinations = (1UL << (currentpieceSquare - 7)) & otherColour & ~0x0101010101010101UL;
                    if (pieceDestinations != 0)
                    {
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                    }

                    pieceDestinations = (1UL << (currentpieceSquare - 9)) & otherColour & ~0x8080808080808080UL;
                    if (pieceDestinations != 0)
                    {
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                    }
                    pieces -= 1UL << currentpieceSquare;
                } 
                
                // Pawns on the 2nd rank and are not pinned (can promote)
                pieces = bitboards[pieceIndex] & ~pinnedPieces & 0x000000000000FF00UL;

                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                    if (!capturesOnly)
                    {
                        pieceDestinations = 1UL << (currentpieceSquare - 8) ;

                        if ((pieceDestinations & empty) != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), knightPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), bishopPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), rookPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), queenPromotionFlag));
                        }
                    }

                    pieceDestinations = (1UL << (currentpieceSquare - 7)) & otherColour & ~0x0101010101010101UL;
                    if (pieceDestinations != 0)
                    {
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), knightPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), bishopPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), rookPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), queenPromotionFlag));
                    }

                    pieceDestinations = (1UL << (currentpieceSquare - 9)) & otherColour & ~0x8080808080808080UL;
                    if (pieceDestinations != 0)
                    {
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), knightPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), bishopPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), rookPromotionFlag));
                        moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), queenPromotionFlag));
                    }
                    pieces -= 1UL << currentpieceSquare;
                }
               
                // Pawns on the 7th rank that are partially pinned
                pieces = bitboards[pieceIndex] & partiallyPinnedPieces & 0x00FF000000000000UL;

                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                    // if the pinned pawn is vertically in front of or behind the king then if must be pinned by a rook or queen and thus can still move vertically
                    if (!capturesOnly)
                    {
                        if (((currentpieceSquare - kingSquare) % 8) == 0) 
                        {
                            pieceDestinations = 1UL << currentpieceSquare ;
                            for (int i = 0; i < 2; i++)
                            {
                                pieceDestinations >>= 8;
                                if ((pieceDestinations & empty) == 0)
                                {
                                    break;
                                }
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), (i == 0) ? regularFlag : doublePawnMoveFlag));
                            }
                        }
                    }

                    // if the pinned pawn is pinned diagonally in front of or behind the king then if must be pinned by a bishop or queen and thus can still move diagonally
                    if (((currentpieceSquare - kingSquare) % 7) == 0) // south west
                    {
                        pieceDestinations = (1UL << (currentpieceSquare - 7)) & otherColour & ~0x0101010101010101UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                        }
                    }

                    if (((currentpieceSquare - kingSquare) % 9) == 0) 
                    {
                        pieceDestinations = (1UL << (currentpieceSquare - 9)) & otherColour & ~0x8080808080808080UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                        }
                    }
                    pieces -= 1UL << currentpieceSquare;
                    // Pawns that are horizontally pinned are computed as partially pinned but can actually never move
                }

                // Pawns on the 6th to 3rd rank that are partially pinned
                pieces = bitboards[pieceIndex] & partiallyPinnedPieces & 0x0000FFFFFFFF0000UL;

                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);

                    if (!capturesOnly)
                    {
                        if (((currentpieceSquare - kingSquare) % 8) == 0)
                        {
                            pieceDestinations = 1UL << (currentpieceSquare - 8) ;
                            if ((pieceDestinations & empty) != 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                    }

                    if (((currentpieceSquare - kingSquare) % 7) == 0)
                    {
                        pieceDestinations = (1UL << (currentpieceSquare - 7)) & otherColour & ~0x0101010101010101UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                        }
                    }

                    if (((currentpieceSquare - kingSquare) % 9) == 0)
                    {
                        pieceDestinations = (1UL << (currentpieceSquare - 9)) & otherColour & ~0x8080808080808080UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                        }
                    }
                    pieces -= 1UL << currentpieceSquare;
                } 

                // Pawns on the 2nd rank that are partially pinned
                pieces = bitboards[pieceIndex] & partiallyPinnedPieces & 0x000000000000FF00UL;

                while (pieces != 0)
                {
                    currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                    
                    // Pawns that are pinned vertically cannot promote because the pinning piece must be in front of them
                    if (((currentpieceSquare - kingSquare) % 7) == 0)
                    {
                        pieceDestinations = (1UL << (currentpieceSquare - 7)) & otherColour & ~0x0101010101010101UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), knightPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), bishopPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), rookPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), queenPromotionFlag));
                        }
                    }

                    if (((currentpieceSquare - kingSquare) % 9) == 0)
                    {
                        pieceDestinations = (1UL << (currentpieceSquare - 9)) & otherColour & ~0x8080808080808080UL;
                        if (pieceDestinations != 0)
                        {
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), knightPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), bishopPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), rookPromotionFlag));
                            moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), queenPromotionFlag));
                        }
                    }
                    pieces -= 1UL << currentpieceSquare;
                } 

                // Handles the case of a possible en pessant for a pawn
                if ((enpessantSquare >= 16) & (enpessantSquare < 24))
                {
                    int offset = 9;
                    int edgecheck = 0;
                    bool enpessantPinned;
                    ulong heavyPiecesonfourth;
                    int currentHeavyPieceSquare;
                    int effectiveDirection;

                    for (int i = 0; i < 2; i++) // considers enpessant for both possible pawns that could capture
                    {
                        currentpieceSquare = enpessantSquare + offset;

                        //check for edge shenanigans and if there is actually a pawn on the "currentPieceSquare" 
                        if (((currentpieceSquare % 8) != edgecheck) && (((1UL << currentpieceSquare) & bitboards[pieceIndex]) != 0))
                        {
                            if (((1UL << currentpieceSquare) & pinnedPieces) == 0) // if the pawn is NOT pinned
                            {
                                // There is a special case of pins for enpessant that must be checked for
                                // If the pawn separates a rook/queen and the king and there are no pawns between them, then the pawn is pinned
                                // We find all the rooks and queens on the fourth rank and check if any of them have only the 2 specific pawns in question between them and the king
                                // If any of them does, en pessant is illegal
                                // Key detail is that enpessant could still be illegal if the if this is not the case
                                // We let through the cases where there are more than just those 2 pawns between the rook/queen and king,
                                // but filter out cases with only those 2 pawns or nothing in between
                                // The second case is not en pessant but it is fine to filter it out since it is a check
                                // and enpessant is illegal during checks of a king on the fifth rank anyways
                                
                                enpessantPinned = false;
                                heavyPiecesonfourth = (bitboards[3] | bitboards[4]) & 0x00000000FF000000UL; // finds the rooks and queens on the fourth rank
                                currentHeavyPieceSquare = BitboardUtils.TrailingZeroCount(heavyPiecesonfourth) % 64; // mod 64 to stop the 64 as the output of 0

                                if (kingSquare >= 24 && (kingSquare < 32)) // if king is on fourth rank
                                {
                                    while (currentHeavyPieceSquare != 0)
                                    {
                                        heavyPiecesonfourth -= 1UL << currentHeavyPieceSquare;
                                        effectiveDirection = (kingSquare > currentHeavyPieceSquare) ? 1 : -1; // direction from rook to king
                                        walk = currentHeavyPieceSquare + effectiveDirection;
                                        while (true)
                                        {
                                            if (walk == kingSquare) //if the ray got to the king successfully, then enpessant is illegal by pin or check
                                            {
                                                enpessantPinned = true;
                                                break;
                                            }

                                            // if the ray is obstructed by anything that are not the 2 pawns, then enpessant is possible
                                            if (((walk != currentpieceSquare) && (walk != (enpessantSquare + 8))) && (((1UL << walk) & occupied) != 0))
                                            {                                 
                                                break;
                                            }
                                            walk += effectiveDirection;
                                        }
                                        currentHeavyPieceSquare = BitboardUtils.TrailingZeroCount(heavyPiecesonfourth) % 64;
                                        
                                    }
                                    if (!(enpessantPinned)) // if enpessant is possible then add move
                                    {
                                        moves.Add(new Move(currentpieceSquare, enpessantSquare, enpessantFlag));
                                    }
                                }
                                else // if king is not on the fourth rank then enpessant is possible
                                {
                                    moves.Add(new Move(currentpieceSquare, enpessantSquare, enpessantFlag));
                                }
                            }

                                    
                            else // if pawn is traditionally pinned
                            {
                                // it can still play enpessant if the capture is in direction of the king
                                if (((currentpieceSquare - kingSquare) % (enpessantSquare - currentpieceSquare)) == 0) 
                                {
                                    moves.Add(new Move(currentpieceSquare, enpessantSquare, enpessantFlag));
                                }
                            }
                        }
                        offset = 7;
                        edgecheck = 7; 
                        // change the paramters to allow for next direction
                    }
                }              
            }

            // Knights

            pieceIndex = isWhite ? 1 : 7;

            // unpinned knights
            pieces = bitboards[pieceIndex] & ~pinnedPieces;

            while (pieces != 0)
            {
                currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                pieceDestinations = KnightAttackTable[currentpieceSquare];

                // loop through all the possible final squares
                while (pieceDestinations!= 0)
                {
                    finalSquare = BitboardUtils.TrailingZeroCount(pieceDestinations);
                    if (((1UL << finalSquare) & sameColour) == 0) // If there is no same colour piece on the square
                    {
                        if (((1UL << finalSquare) & otherColour) == 0) // If there is not a piece of the opposing colour as well (empty), add a regular move
                        {
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, finalSquare, regularFlag));
                            }
                        }
                        else // if there is an opposing colour, add a capture
                        {
                            moves.Add(new Move(currentpieceSquare, finalSquare, captureFlag));
                        }
                    }
                    pieceDestinations -= 1UL << finalSquare;
                    
                }

                pieces -= 1UL << currentpieceSquare;
            }
            // There are no partially pinned Knights

            // Bishops
            pieceIndex = isWhite ? 2 : 8;

            // unpinned bishops
            pieces = bitboards[pieceIndex] & ~pinnedPieces;

            while (pieces != 0)
            {
                currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);

                for (int i = 0; i < 4; i++)
                {
                    pieceDestinations = 1UL << currentpieceSquare;

                    switch (i)
                    {
                        
                        case 0 : while (true)
                        {
                            pieceDestinations = (pieceDestinations << 9) & ~0x0101010101010101UL;
                            pieceDestinations &= ~sameColour; // and with negative same colour to make it 0 only when the square is the same colour
                            if (pieceDestinations == 0) // if same colour, 
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0) // if different colour, add capture
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            // if neither, add move
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 1 : while (true)
                        {
                            pieceDestinations = (pieceDestinations << 7) & ~0x8080808080808080UL;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 2 : while (true)
                        {
                            pieceDestinations = (pieceDestinations >> 7) & ~0x0101010101010101UL;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 3 : while (true)
                        {
                            pieceDestinations = (pieceDestinations >> 9) & ~0x8080808080808080UL;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                    } 
                }

                pieces -= 1UL << currentpieceSquare;
            }

            // partially pinned bishops
            pieces = bitboards[pieceIndex] & partiallyPinnedPieces;

            while (pieces != 0)
            {
                currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);

                if (((currentpieceSquare - kingSquare) % 9) == 0) // if pinned northwest, only northwest directions are possible
                {

                    for (int i = 0; i < 2; i++)
                    {
                        pieceDestinations = 1UL << currentpieceSquare;

                        switch (i)
                        {
                            
                            case 0 : while (true)
                            {
                                pieceDestinations = (pieceDestinations << 9) & ~0x0101010101010101UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                            case 1 : while (true)
                            {
                                pieceDestinations = (pieceDestinations >> 9) & ~0x8080808080808080UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                        } 
                    }

                }

                else // pinned northeast
                {
                    for (int i = 0; i < 2; i++)
                    {
                        pieceDestinations = 1UL << currentpieceSquare;

                        switch (i)
                        {

                            case 0 : while (true)
                            {
                                pieceDestinations = (pieceDestinations << 7) & ~0x8080808080808080UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                            case 1 : while (true)
                            {
                                pieceDestinations = (pieceDestinations >> 7) & ~0x0101010101010101UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;
                        }

                    }
                }    
                pieces -= 1UL << currentpieceSquare;          
            }
            
            // Rooks
            pieceIndex = isWhite ? 3 : 9;

            // unpinned rooks
            pieces = bitboards[pieceIndex] & ~pinnedPieces;

            while (pieces != 0)
            {
                currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                

                for (int i =0; i < 4; i++)
                {
                    pieceDestinations = 1UL << currentpieceSquare;

                    switch (i)
                    {
                        
                        case 0 : while (true)
                        {
                            pieceDestinations <<= 8;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 1 : while (true)
                        {
                            pieceDestinations = (pieceDestinations << 1) & ~0x0101010101010101UL;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 2 : while (true)
                        {
                            pieceDestinations = (pieceDestinations >> 1) & ~0x8080808080808080UL;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 3 : while (true)
                        {
                            pieceDestinations >>= 8;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                    } 
                }

                pieces -= 1UL << currentpieceSquare;
            }

            // partially pinned rooks
            pieces = bitboards[pieceIndex] & partiallyPinnedPieces;

            while (pieces != 0)
            {
                currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                
                if (((currentpieceSquare - kingSquare) % 8) == 0) 
                {

                    for (int i =0; i < 2; i++)
                    {
                        pieceDestinations = 1UL << currentpieceSquare;

                        switch (i)
                        {
                            
                            case 0 : while (true)
                            {
                                pieceDestinations <<= 8;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                            case 1 : while (true)
                            {
                                pieceDestinations >>= 8;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                        } 
                    }
                }

                else
                {
                    for (int i =0; i < 2; i++)
                    {
                        pieceDestinations = 1UL << currentpieceSquare;
                        
                        switch (i)
                        {

                            case 0 : while (true)
                            {
                                pieceDestinations = (pieceDestinations << 1) & ~0x0101010101010101UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                            case 1 : while (true)
                            {
                                pieceDestinations = (pieceDestinations >> 1) & ~0x8080808080808080UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;
                        }
                    }
                } 

                pieces -= 1UL << currentpieceSquare;             
            }

            // Queens
            pieceIndex = isWhite ? 4 : 10;

            // unpinned queens
            pieces = bitboards[pieceIndex] & ~pinnedPieces;

            while (pieces != 0)
            {
                currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);
                
                for (int i =0; i < 8; i++)
                {
                    pieceDestinations = 1UL << currentpieceSquare;

                    switch (i)
                    {
                        
                        case 0 : while (true)
                        {
                            pieceDestinations = (pieceDestinations << 9) & ~0x0101010101010101UL;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 1 : while (true)
                        {
                            pieceDestinations = (pieceDestinations << 7) & ~0x8080808080808080UL;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 2 : while (true)
                        {
                            pieceDestinations = (pieceDestinations >> 7) & ~0x0101010101010101UL;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 3 : while (true)
                        {
                            pieceDestinations = (pieceDestinations >> 9) & ~0x8080808080808080UL;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 4 : while (true)
                        {
                            pieceDestinations = pieceDestinations << 8;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 5 : while (true)
                        {
                            pieceDestinations = (pieceDestinations << 1) & ~0x0101010101010101UL;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 6 : while (true)
                        {
                            pieceDestinations = (pieceDestinations >> 1) & ~0x8080808080808080UL;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                        case 7 : while (true)
                        {
                            pieceDestinations = pieceDestinations >> 8;
                            pieceDestinations &= ~sameColour;
                            if (pieceDestinations == 0)
                            {
                                break;
                            }
                            if ((pieceDestinations & ~otherColour) == 0)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                break;
                            }
                            if (!capturesOnly)
                            {
                                moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                            }
                        }
                        break;

                    } 
                }

                pieces -= 1UL << currentpieceSquare;
            }

            // partially pinned queens
            pieces = bitboards[pieceIndex] & partiallyPinnedPieces;

            while (pieces != 0)
            {
                currentpieceSquare = BitboardUtils.TrailingZeroCount(pieces);

                if (((currentpieceSquare - kingSquare) % 9) == 0) 
                {

                    for (int i = 0; i < 2; i++)
                    {
                        pieceDestinations = 1UL << currentpieceSquare;

                        switch (i)
                        {
                            
                            case 0 : while (true)
                            {
                                pieceDestinations = (pieceDestinations << 9) & ~0x0101010101010101UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                            case 1 : while (true)
                            {
                                pieceDestinations = (pieceDestinations >> 9) & ~0x8080808080808080UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                        } 
                    }
                }

                else if (((currentpieceSquare - kingSquare) % 7) == 0)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        pieceDestinations = 1UL << currentpieceSquare;

                        switch (i)
                        {

                            case 0 : while (true)
                            {
                                pieceDestinations = (pieceDestinations << 7) & ~0x8080808080808080UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                            case 1 : while (true)
                            {
                                pieceDestinations = (pieceDestinations >> 7) & ~0x0101010101010101UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;
                        }
                    }
                }

                else if (((currentpieceSquare - kingSquare) % 8) == 0)
                {

                    for (int i = 0; i < 2; i++)
                    {
                        pieceDestinations = 1UL << currentpieceSquare;

                        switch (i)
                        {
                            
                            case 0 : while (true)
                            {
                                pieceDestinations = pieceDestinations << 8;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                            case 1 : while (true)
                            {
                                pieceDestinations = pieceDestinations >> 8;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                        } 
                    }
                }

                else
                {
                    for (int i =0; i < 2; i++)
                    {
                        pieceDestinations = 1UL << currentpieceSquare;
                        
                        switch (i)
                        {

                            case 0 : while (true)
                            {
                                pieceDestinations = (pieceDestinations << 1) & ~0x0101010101010101UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;

                            case 1 : while (true)
                            {
                                pieceDestinations = (pieceDestinations >> 1) & ~0x8080808080808080UL;
                                pieceDestinations &= ~sameColour;
                                if (pieceDestinations == 0)
                                {
                                    break;
                                }
                                if ((pieceDestinations & ~otherColour) == 0)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), captureFlag));
                                    break;
                                }
                                if (!capturesOnly)
                                {
                                    moves.Add(new Move(currentpieceSquare, BitboardUtils.TrailingZeroCount(pieceDestinations), regularFlag));
                                }
                            }
                            break;
                        }
                    }
                }   
                pieces -= 1UL << currentpieceSquare;            
            }

            if (numCheck == 1) // If there is a single check, then it is complex
            {
                int targetSquare; // represent the currently computed valid stopsquare of any piece (includes the attacking piece, or path for sliders)

                List<int> targetSquares = new List<int>(); // list of all target square
                

                if ((checkIndex % 6) == 0) // if a pawn is checking
                {
                    if (isWhite)
                    {
                        // there can be only one target square for knights and pawn, this is the approach to find said target
                        targetSquare =  kingSquare + 9;

                        //check if the potential target square has a pawn, and check if the target square has not engaged in edge shenanigans
                        // if all these are true, then the proposed target square is correct
                        // if they are not, the the other target square has to be correct since of them checks the king
                        if (!(((bitboards[checkIndex] & (1UL << targetSquare)) != 0) && ((targetSquare % 8) != 0)))
                        {
                            targetSquare = kingSquare + 7;
                        }
                    }
                    else
                    {
                        targetSquare =  kingSquare - 7;
                        if (!(((bitboards[checkIndex] &  (1UL << targetSquare)) != 0)  && ((targetSquare % 8) != 0)))
                        {
                            targetSquare = kingSquare - 9;
                        }
                    }
                    targetSquares.Add(targetSquare);
                }

                else if ((checkIndex % 6) == 1) // if a knight is checking
                {                        
                    ulong targetPiece = bitboards[checkIndex]; // all knights
                    int targetDifference = 0;

                    while(true)
                    {
                        targetSquare = BitboardUtils.TrailingZeroCount(targetPiece);
                        targetDifference = targetSquare - kingSquare;

                        if ((KnightAttackTable[targetSquare] & (1UL << kingSquare)) == 0) // if king is not attacked by this knight
                        {
                            targetPiece -= (1UL << targetSquare);   
                        }
                        else
                        {
                            break;
                        }
                        // since the king is checked by a knight, then the break must surely happen, leaving the last square as the true
                    }  
                    targetSquares.Add(targetSquare);  
                }

                else if ((checkIndex % 6) == 2) // if a bishop is checking
                {
                    ulong targetPiece = bitboards[checkIndex] & GenerateRay(checkIndex); 
                    int targetDifference = 0;
                    int proposedDirection;
                    
                    while(true)
                    {
                        walk = kingSquare;
                        targetSquare = BitboardUtils.TrailingZeroCount(targetPiece);
                        targetDifference = targetSquare - kingSquare;
                        if ((targetDifference % 9) == 0) // check 9  first to avoid the 0 to 63 edge case
                        {
                            proposedDirection =  (targetDifference > 0) ? 9 : -9;
                        }
                        else
                        {
                            proposedDirection = (targetDifference > 0) ? 7 : -7;
                        }

                        while (walk != targetSquare)
                        { 
                            walk += proposedDirection;
                            // if there is a piece of the king's colour between, then it isn't a check
                            // no need to check for different colour since GenerateRay has taken care of that
                            if (((1UL << walk) & sameColour) != 0) 
                            {
                                targetPiece -= (1UL << targetSquare);
                                break;
                            }
                        }

                        if (walk == targetSquare) // it was indeed a check
                        {
                            break; //end the while loop as we have found the right piece
                        }
                    }   
                    
                    walk = kingSquare;

                    while (walk != targetSquare)
                    { 
                        walk += proposedDirection;
                        targetSquares.Add(walk);
                    }
                    
                }

                else if ((checkIndex % 6) == 3) // if a rook is checking
                {
                    ulong targetPiece = bitboards[checkIndex] & GenerateRay(checkIndex); 
                    int targetDifference = 0;
                    int proposedDirection;
                    
                    while(true)
                    {
                        walk = kingSquare;
                        targetSquare = BitboardUtils.TrailingZeroCount(targetPiece);
                        targetDifference = targetSquare - kingSquare;
                        if ((targetDifference % 8) == 0) // check 9  first to avoid the 0 to 63 edge case
                        {
                            proposedDirection =  (targetDifference > 0) ? 8 : -8;
                        }
                        else
                        {
                            proposedDirection = (targetDifference > 0) ? 1 : -1;
                        }

                        while (walk != targetSquare)
                        { 
                            walk += proposedDirection;
                            // if there is a piece of the king's colour between, then it isn't a check
                            // no need to check for different colour since GenerateRay has taken care of that
                            if (((1UL << walk) & sameColour) != 0) 
                            {
                                targetPiece -= (1UL << targetSquare);
                                break;
                            }
                        }

                        if (walk == targetSquare) // it was indeed a check
                        {
                            break; //end the while loop as we have found the right piece
                        }
                    } 

                    walk = kingSquare;

                    while (walk != targetSquare)
                    {
                        walk += proposedDirection;
                        targetSquares.Add(walk);
                    }  
                }
                
                else if ((checkIndex % 6) == 4) // if queen is checking
                {
                    ulong targetPiece = bitboards[checkIndex] & GenerateRay(checkIndex); 
                    int targetDifference = 0;
                    int proposedDirection;
                    while(true)
                    {
                        walk = kingSquare;
                        targetSquare = BitboardUtils.TrailingZeroCount(targetPiece);
                        targetDifference = targetSquare - kingSquare;
                        if ((targetDifference % 9) == 0) // check 9  first to avoid the 0 to 63 edge case
                        {
                            proposedDirection =  (targetDifference > 0) ? 9 : -9;
                        }
                        else if ((targetDifference % 8) == 0) // avoid the 8*7 edge case by doing 8 before 7
                        {
                            proposedDirection =  (targetDifference > 0) ? 8 : -8;
                        }
                        else if (((targetDifference % 7) == 0) && ((targetSquare / 8) != (kingSquare / 8)))
                        {
                            proposedDirection = (targetDifference > 0) ? 7 : -7;
                        }
                        else
                        {
                            proposedDirection = (targetDifference > 0) ? 1 : -1;
                        }

                        while (walk != targetSquare)
                        { 
                            walk += proposedDirection;
                            // if there is a piece of the king's colour between, then it isn't a check
                            // no need to check for different colour since GenerateRay has taken care of that
                            if (((1UL << walk) & sameColour) != 0) 
                            {
                                targetPiece -= (1UL << targetSquare);
                                break;
                            }
                        }

                        if (walk == targetSquare) // it was indeed a check
                        {
                            break; //end the while loop as we have found the right piece
                        }
                    } 

                    walk = kingSquare;

                    while (walk != targetSquare)
                    {
                        walk += proposedDirection;
                        targetSquares.Add(walk);
                        
                    }
                }

                moves.RemoveAll(pseudolegalmove => PseudoLegalFilter(pseudolegalmove, targetSquares, checkIndex));
                moves.AddRange(kingMoves);
                return moves;
            }
            if (!capturesOnly)
            {
                if (isWhite)
                {
                    // checks for castling
                    // you can not castle out of check
                    // the ulong anded checks if the king destination or square beside is attacked
                    // the squares must also be empty
                    if ((castlingRights[0]) && ((attackedSquares & 0x0000000000000006UL) == 0) && ((occupied & 0x0000000000000006UL) == 0))
                    {
                        moves.Add(new Move(kingSquare, 1, castlingFlag));
                    }
                    if ((castlingRights[1]) && ((attackedSquares & 0x0000000000000030UL) == 0) && ((occupied & 0x0000000000000070UL) == 0))
                    {
                        moves.Add(new Move(kingSquare, 5, castlingFlag));
                    }
                }
                else
                {
                    if ((castlingRights[2]) && ((attackedSquares & 0x0600000000000000UL) == 0) && ((occupied & 0x0600000000000000UL) == 0))
                    {
                        moves.Add(new Move(kingSquare, 57, castlingFlag));
                    }
                    if ((castlingRights[3]) && ((attackedSquares & 0x3000000000000000UL) == 0) && ((occupied & 0x7000000000000000UL) == 0))
                    {
                        moves.Add(new Move(kingSquare, 61, castlingFlag));
                    }  
                }
            }

            moves.AddRange(kingMoves); 
            return moves;
        }  


        // Function for checking if game is over and who won
        // returns a GameResult enum value
        // the logic is still incomplete as it doesn't contain 50 move rule and three fold repitition

        // first returns true if game is over and false if the game is still on
        // second returns true if the game is won and false if it is a draw
        // third returns true if white wins and false if black wins
        
        public GameResult GameOver()
        {
            int moveCount = GenerateMoves().Count;
            int numChecks = NumChecks(isWhite)[0];
            if ((moveCount != 0) && !claimInsufficientMaterial)
            {
                return GameResult.Ongoing;
            }
            // the player that is to play is the only one that can be in check
            if ((moveCount == 0) && (numChecks != 0))
            {
                return isWhite ? GameResult.BlackWins : GameResult.WhiteWins;
            } 
            return GameResult.Draw;
        }

        public int PieceCount(int index)
        {
            int count = 0;
            ulong piece = bitboards[index];
            while (piece != 0UL)
            {
                piece = piece & ~(1UL << BitboardUtils.TrailingZeroCount(piece));
                count += 1;
            }

            return count;             
        } 
}