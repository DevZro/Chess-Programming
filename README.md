# Chess Programming

A chess engine written from scratch in C#, with a Unity front end for playing and watching games. The engine uses bitboards for board representation and generates fully legal moves directly (via pin and check analysis) rather than filtering pseudo-legal moves with make/unmake tests.

The repository also contains **BondFish**, a series of search engines (1.0 through 1.4) built up incrementally, so the effect of each search technique can be measured against the previous version.

## Requirements

- **Unity 2022.3.29f1** (see `UnityProject/ProjectSettings/ProjectVersion.txt`)
- **.NET 9 SDK** — only needed for the standalone perft harness in `EngineTests/`

## Quick start

### Playing in Unity

1. Open `UnityProject/` in Unity Hub.
2. Open the scene `Assets/Scenes/Game.unity`.
3. Press Play, then click the start button in the scene.

The matchup is chosen in code, in `GraphicalBoard.StartNewGame()`. As committed, it runs BondFish 1.4 at depth 7 as White against BondFish 1.1 at depth 4 as Black:

```csharp
whitePlayer = new BotPlayer(board, this, new BondFish1_4(7, board));
blackPlayer = new BotPlayer(board, this, new BondFish1_1(4, board));
```

Human players are wired the same way — swap in `new HumanPlayer(this, board)` for either side (both variants are present as commented-out lines in that method).

### Running perft

```bash
cd EngineTests
dotnet run
```

This walks perft depths 1 through 7 from the standard starting position and prints the node count and elapsed milliseconds for each depth. Perft is the primary correctness test for the move generator: the node counts at each depth are a known sequence, so any deviation localises a move generation bug.

Note that `EngineTests/` contains its **own copy** of the board class (`Chess.ChessBoard`), independent of the Unity `Board`. See [CODE-REVIEW.md](CODE-REVIEW.md) for why that matters.

## Controls

| Input | Action |
| --- | --- |
| Click a piece, then a square | Select and move |
| `N` / `B` / `R` / `Q` | Choose the promotion piece before completing a promoting move |
| `D` | Claim a draw when threefold repetition or the fifty-move rule is available |

Promotion defaults to whatever `promotedTo` was last set to, so press the promotion key before playing the move.

## Repository layout

```
EngineTests/                     standalone .NET 9 perft harness
  Program.cs                     contains the Chess.ChessBoard class
  Perft.cs                       contains Main and the perft driver
UnityProject/
  Assets/Scenes/Game.unity       the playable scene
  Assets/Scripts/
    Board.cs                     the engine: bitboards, move generation, make/unmake, Zobrist
    SharedTypes.cs               the packed Move struct
    Zobrist.cs                   Zobrist key tables
    GraphicalBoard.cs            view and match controller
    Tile.cs, ChessMan.cs         board squares and pieces, plus click input
    IPlayer.cs                   player abstraction
    HumanPlayer.cs, BotPlayer.cs the two player kinds
    Clock.cs                     game clock and increment
    GameStatusController.cs      start button and result banner
    Engines/
      IBotEngine.cs              engine abstraction
      RandomBotEngine.cs         picks a legal move at random
      BondFish1.0.cs ... 1.4.cs  the search engine series
```

## Board representation

The board is twelve `ulong` bitboards, one per piece type and colour, indexed as:

| Index | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Piece | P | N | B | R | Q | K | p | n | b | r | q | k |

Four derived bitboards (`whiteOccupied`, `blackOccupied`, `occupied`, `empty`) are recomputed after every move by `UpdateOccupiedAndEmpty()`.

Square indexing is mirrored relative to the usual layout: bit 0 is **h1** and bit 63 is **a8**, so

```
square = rank * 8 + (7 - file)
```

This falls out of how `LoadFen` walks a FEN string and sets `1UL << (63 - walk)`. It is internally consistent, but it means files run right to left, which is worth remembering when reading the shift directions in the attack generators.

### The Move struct

A move is a single `ushort` (`SharedTypes.cs`):

```
bits 0-5    start square
bits 6-11   destination square
bits 12-15  flag
```

Flags: `0` null, `1` regular, `2` capture, `3` castling, `4` double pawn push, `5` en passant, `6`–`9` promotion to knight / bishop / rook / queen.

### Make and unmake

`MakeMove` applies a move and pushes a packed `ulong` onto `boardStateHistory` describing everything needed to reverse it — the move itself, the captured piece index (`15` for none), the castling rights, the en passant square, and the half-move counter. `UndoMove` pops that word and restores the previous state. The stack means the search can descend and back out without cloning the board.

## Move generation

`GenerateMoves()` returns only legal moves. Rather than generating pseudo-legal moves and testing each one by making it and checking whether the king is attacked, it establishes the constraints up front:

1. `ComputeAttacks(!isWhite)` builds the bitboard of squares the opponent attacks. The definition is deliberately loose — friendly pieces count as attacked, pinned pieces still attack, and squares behind the king count — because the purpose is to determine where the king may not go.
2. `GetPinnedPieces(isWhite)` returns two bitboards: fully pinned pieces, and *partially* pinned pieces that may still move along the pin ray. `GenerateRay` supports it by projecting rays outward from the king to find sliders that could pin.
3. `NumChecks(isWhite)` returns the number of checking pieces and the attacker's piece index.

Generation then branches on the check count:

- **Double check** — only king moves are returned, immediately.
- **Single check** — moves are generated per piece type and then filtered by `PseudoLegalFilter` to those that capture the checker or block the check ray, and merged with the legal king moves.
- **No check** — all moves are generated, with castling appended.

Pinned pieces are handled separately from free ones, and partially pinned pieces are restricted by comparing the square difference against the king (`% 8`, `% 9`, `% 7`) to work out which direction the pin runs along.

One case resists this scheme: a pawn whose en passant capture would expose the king to a rook or queen along the fifth rank. Both captured and capturing pawn leave that rank at once, so a normal pin test does not see it. It is handled by an explicit ray walk toward the king in the en passant branch.

## Repetition, the fifty-move rule, and material

- **Zobrist hashing** — `Zobrist.cs` holds the key tables; the hash is updated incrementally by `UpdateZobristAfterMove` / `UpdateZobristAfterUndo` rather than recomputed. `ComputeFullZobristHash` exists for verification.
- **Threefold repetition** — `positionHistory` is a `Dictionary<ulong,int>` counting occurrences per hash; reaching three sets `claimThreeFold`.
- **Fifty-move rule** — tracked by `halfMoveCounter`.
- **Insufficient material** — `CheckForInsufficientMaterial` sets `claimInsufficientMaterial` for K v K, K+N v K, and K+B v K.

Repetition and the fifty-move rule are **claimable, not automatic**: press `D` to claim. Insufficient material is the exception and ends the game through `GameOver()`.

## The BondFish engine series

Every engine implements `IBotEngine`:

```csharp
public interface IBotEngine
{
    Move GetBestMove();
    string GetName();
}
```

| Engine | What it adds |
| --- | --- |
| `RandomBotEngine` | Picks uniformly at random from the legal moves. Baseline. |
| `BondFish1_0` | Material evaluation, one ply deep. |
| `BondFish1_1` | Negamax to a fixed depth. |
| `BondFish1_2` | Alpha-beta pruning. |
| `BondFish1_3` | MVV-LVA move ordering plus promotion ordering, so pruning cuts earlier. |
| `BondFish1_4` | Quiescence search at the leaves, to stop the evaluation firing mid-exchange. |

Evaluation is material only — pawn 100, knight 300, bishop 300, rook 500, queen 900 — returned from the perspective of the side to move, with checkmate scored at `-100000` and stalemate at `0`. Each engine takes its search depth as a constructor argument.

The step from 1.1 to 1.2 is the one that carries the most ideas. [ALPHA-BETA.md](ALPHA-BETA.md) works through it in detail: what `alpha` and `beta` mean, the long two-function form of the algorithm next to the compact negamax form used here, why the recursive call negates and swaps its bounds, fail-hard vs fail-soft, and a test that will catch unsound pruning.

## Architecture

The engine and the presentation layer are separated by two interfaces.

```
Board                  bitboards, legal move generation, make/unmake, hashing
  ^
  |  IBotEngine        RandomBotEngine, BondFish 1.0 - 1.4
  |  IPlayer           HumanPlayer, BotPlayer
  v
GraphicalBoard         instantiates tiles and pieces, animates moves, runs the match
  Tile, ChessMan       per-square and per-piece click handling
  Clock                15 minutes plus 10 second increment
  GameStatusController start button and result banner
```

`GraphicalBoard` drives the game loop. After a move it calls `OnMoveCompleted()`, which switches the clock, checks `GameOver()`, shows a result if the game has ended, and otherwise hands the turn to the other `IPlayer`.

`BotPlayer` runs the search on a background thread via `Task.Run` and yields in a coroutine until it finishes, so the clock and UI keep updating while the engine thinks.

Because search and evaluation only talk to `Board` and `IBotEngine`, a new engine is a single new file plus one line in `StartNewGame()`.

## Known limitations

- Engine matchups are chosen by editing `StartNewGame()`; there is no in-game selection.
- The time control is fixed in `Clock.cs` (15 + 10) and is not exposed in the inspector.
- Captured pieces are moved off-screen rather than destroyed.
- Threefold and fifty-move draws require a manual claim.
- The engine exists in two copies — the Unity `Board` and `Chess.ChessBoard` in `EngineTests/` — which must be kept in sync by hand.

A fuller list, with suggested fixes, is in [CODE-REVIEW.md](CODE-REVIEW.md).

