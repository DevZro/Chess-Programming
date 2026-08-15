# Code review: flaws, and what to do about them

This is a review of the current state of the project, ordered roughly by how much damage each item does. Nothing here has been changed in the source — it is a list of findings.

Worth saying first: the parts that are hard are done well. Generating legal moves directly from pin and check analysis, rather than making every pseudo-legal move and testing it, is the harder and faster design, and the reasoning in the comments around `GetPinnedPieces` and the en passant pin case shows the edge cases were actually thought through rather than stumbled over. The engine series being versioned 1.0 through 1.4, one technique at a time, is a genuinely good way to build a search. Most of what follows is about correctness slips and structure, not about the ideas.

---

## 1. Correctness bugs

### 1.1 The half-move counter is written and read at different bit offsets

`Board.cs:281` packs it in:

```csharp
boardState |= (ulong) halfMoveCounter << 30;
```

`Board.cs:1598` reads it back out:

```csharp
halfMoveCounter = (int) ((currrentState >> 32) & 0xFFFF);
```

Written at bit 30, read from bit 32. Every `UndoMove` restores `halfMoveCounter` to roughly its true value divided by four, with the low two bits discarded. The fifty-move rule is therefore wrong after any undo — which means it is wrong throughout the entire search, since the search does nothing but make and undo moves.

**Fix.** Pick one offset and use it in both places. Bits 24–29 hold the en passant square, so bit 30 is the correct next free bit; change the read to `>> 30`. While you are there, note `& 0xFFFF` claims sixteen bits: at offset 30 that runs to bit 45, which is fine in a `ulong`, but a half-move counter never exceeds 100, so `& 0x7F` (seven bits) is enough and leaves room. Better still, write the layout down as named constants:

```csharp
const int HalfMoveShift = 30;
const int HalfMoveMask  = 0x7F;
```

Then the write is `(ulong)halfMoveCounter << HalfMoveShift` and the read is `(int)((state >> HalfMoveShift) & HalfMoveMask)`, and the two can no longer drift apart.

### 1.2 `oldCastlingRights = castlingRights` copies the reference, not the contents

In both `MakeMove` (`Board.cs:1442`) and `UndoMove` (`Board.cs:1587`):

```csharp
bool[] oldCastlingRights = new bool[4];
...
oldCastlingRights = castlingRights;
```

The freshly allocated array is thrown away and `oldCastlingRights` becomes a second name for the *same* array. So when the castling rights are then mutated, `oldCastlingRights` changes with them, and the comparison in `UpdateZobristAfterMove` (`Board.cs:321`) and `UpdateZobristAfterUndo` (`Board.cs:370`):

```csharp
if (oldCastlingRights[i] != castlingRights[i])
```

can never be true. **The castling keys are never XORed into the Zobrist hash.** Two positions identical except for castling rights hash the same. That corrupts threefold detection and would silently corrupt a transposition table the moment you add one — which is the natural next step after 1.4.

**Fix.** Copy the values:

```csharp
bool[] oldCastlingRights = (bool[])castlingRights.Clone();
```

Or, much better, stop using a `bool[4]` for this. Castling rights are four bits; make them a single `int` or a `[Flags]` enum:

```csharp
[Flags]
enum CastlingRights { None = 0, WhiteKing = 1, WhiteQueen = 2, BlackKing = 4, BlackQueen = 8 }
```

Then saving the old value is `var old = castlingRights;` — a value copy, impossible to get wrong — the packing into the history word is a plain shift with no loop, and the Zobrist update becomes a single XOR over the changed bits rather than a four-iteration comparison. This one change removes the bug class rather than the bug.

Note also that the comment above the array (`Board.cs`, and identically at `EngineTests/Program.cs:53`) documents the order as `[BKS, BQS, WKS, WQS]`, while `LoadFen` assigns `K→0, Q→1, k→2, q→3`, i.e. white first. The code is self-consistent; the comment is backwards. Fix the comment before it misleads you during a debugging session.

### 1.3 `GameOver()` reads the wrong element of `NumChecks`

`Board.cs:3386`:

```csharp
if ((NumChecks(isWhite)[1] != 0) && !claimInsufficientMaterial)
```

`NumChecks` returns `{count, attackerIndex}`. Index `[1]` is the attacker's **piece index**, not the number of checks. It happens to work in most positions, because when there is no check `index` is left at its initialiser `0` — but `0` is also the valid index of the white pawn bitboard. So a black king mated or stalemated by a white pawn yields `index == 0`, the condition reads false, and **a pawn mate is reported as a draw**. Pawn mates are rare in a game between two material-only engines but entirely reachable, and this will look like a mysterious wrong result rather than an obvious bug.

**Fix.** Use `[0]`, the count. Better, give `NumChecks` a proper return type — a small `readonly struct CheckInfo { public int Count; public int AttackerIndex; }` — so `checkInfo.Count` cannot be confused with `checkInfo.AttackerIndex` at a call site. Returning `int[2]` for two unrelated integers invites exactly this mistake, and it also heap-allocates on every call (see 3.3). Use a sentinel of `-1` for "no attacker" so that `0` stops doing double duty.

### 1.4 `ExecuteMoveVisually` uses `startrank` where `stoprank` is meant

In the castling branch of `GraphicalBoard.cs`:

```csharp
pieces[stoprank, stopfile + 1] = pieces[stoprank, stopfile - 1];
pieces[startrank, stopfile - 1] = null;      // startrank
```

and in the queenside branch:

```csharp
pieces[startrank, stopfile + 2] = null;      // startrank
```

For castling the king does not change rank, so `startrank == stoprank` and this is harmless *today*. It is a latent bug that will surface the moment anything else reuses that block, and it makes the code read as though it were wrong. Use `stoprank` in both.

### 1.5 The black promotion sprite switch is off by one

Still in `ExecuteMoveVisually`, the capture-promotion branch for black uses cases 7, 8, 9, 10:

```csharp
case 7: ... black_knight; break;
case 8: ... black_bishop; break;
case 9: ... black_rook; break;
case 10: ... black_queen; break;
```

while the non-capture promotion branch for black uses 6, 7, 8, 9 — and the flags are defined as knight `6`, bishop `7`, rook `8`, queen `9`. So a black pawn that promotes *with a capture* draws the wrong piece: a knight promotion renders nothing (case 6 is unhandled), a queen promotion renders a rook. The engine's internal state is correct, so the game continues correctly while the display lies — the worst kind of bug to debug from a screenshot.

**Fix.** Correct the cases to 6–9. Then delete the duplication: both branches, and both colours, run the same four-case switch. Extract one method:

```csharp
void SetPromotionSprite(ChessMan piece, int flag, bool isWhite)
```

and call it from both places. Four copies of one switch is four chances to make this mistake; you made it in one of them.

### 1.6 Zobrist keys have around 62 bits of entropy, not 64

`Zobrist.cs`:

```csharp
ulong high = (ulong)rnd.Next();
ulong low  = (ulong)rnd.Next();
return (high << 32) | low;
```

`System.Random.Next()` returns a non-negative `int`, so bit 31 of each half is always `0` — the result always has bit 63 and bit 31 clear. You lose two bits and, more importantly, every key shares that structure, which biases collisions in a way that is invisible until a transposition table starts returning wrong entries.

**Fix.** Fill the whole width:

```csharp
private static ulong RandomUlong(System.Random rnd)
{
    var buffer = new byte[8];
    rnd.NextBytes(buffer);
    return BitConverter.ToUInt64(buffer, 0);
}
```

Keep the fixed seed (`123456789`) — deterministic keys are genuinely useful for reproducing bugs, and that part was a good decision.

### 1.7 Quiescence search misses en passant and has no depth limit

`BondFish1_4.QuiescenceSearch` decides what is a capture by looking at the destination square:

```csharp
int targetPieceType = board.OccupyingPiece(stopsquare);
if (targetPieceType != -1) // capture
```

An en passant capture lands on an *empty* square, so it is skipped. The one capture whose evaluation swing you most need to see at a leaf is the one that is missed.

There is also no depth cap and no delta pruning, so in a position with a long forcing sequence the quiescence search can recurse a long way, which is part of why the node counts at depth 7 are what they are.

**Fix.** Test the flag, not the board:

```csharp
bool isCapture = flag == board.captureFlag
              || flag == board.enpessantFlag
              || board.OccupyingPiece(stopsquare) != -1;
```

Add a `qdepth` parameter capped at 6 to 8 ply, and consider delta pruning: skip a capture when `standPat + GetValue(victim) + margin < alpha`, since it cannot raise alpha even in the best case.

The deeper fix is to stop generating all moves and discarding most of them. `GenerateMoves()` is the single most expensive call in the program, and quiescence calls it at every leaf to keep maybe 10% of the result. A `GenerateMoves(capturesOnly: true)` overload that never builds the quiet moves would cut leaf cost substantially — probably the largest single speed win available to you.

### 1.8 Mate scores are not adjusted for distance

Every engine returns a flat `-100000` for checkmate. With no ply adjustment, a mate in one and a mate in five score identically, so the engine has no reason to prefer the faster mate and can shuffle pieces in a won position indefinitely — and in an endgame with a fifty-move counter that is also mishandled (1.1), it can throw away a win.

**Fix.** Return `-100000 + ply` (or `-(100000 - ply)`), threading the current ply through `Search`. Nearer mates then score higher, and the engine converts.

### 1.9 `Board` is searched from a background thread while the main thread reads it

> Background on threads, `Task`, coroutines and `MonoBehaviour`, and how this finding
> connects to section 2, is in [THREADING-AND-UNITY.md](THREADING-AND-UNITY.md).

`BotPlayer.MakeBotMove`:

```csharp
System.Threading.Tasks.Task.Run(() => {
    bestMove = engine.GetBestMove();
    calculationComplete = true;
});
while (!calculationComplete) { yield return null; }
```

Getting the search off the main thread so the clock keeps ticking was the right instinct, but the search mutates the *same* `Board` instance the main thread is using — every `MakeMove`/`UndoMove` inside the search writes the live bitboards. Meanwhile the coroutine yields each frame and Unity may read `board.isWhite`, and `Clock` and `GraphicalBoard` continue to run. Right now the main thread mostly happens not to touch the board mid-search, so it survives; it is a data race regardless, and it will eventually produce a corrupted board that reproduces once in fifty games.

Three further problems in the same block:

- `calculationComplete` is a plain `bool`, not `volatile` and not written through `Interlocked` or `Thread.MemoryBarrier`. The JIT is permitted to hoist the read out of the spin loop. In practice `yield return null` prevents that, but you are relying on an implementation detail for correctness.
- Exceptions inside `Task.Run` are captured in the `Task`, which nobody observes, so a crash in the engine shows up as a game that quietly stops — no exception, no message. This is the single worst thing about the current setup for debugging.
- `RandomBotEngine` calls `UnityEngine.Random.Range`, which is a main-thread-only API and throws when called off-thread. (`UnityEngine.Debug.Log`, which every engine calls, is one of the few Unity APIs that *is* thread-safe — those calls are noise on the hot path rather than a correctness problem.)

**Fix.** Give the search its own board. Add a copy constructor or `Board.Clone()`, hand the clone to the engine, and apply the returned move to the real board on the main thread. Once the search owns its data there is no race at all, and this is also a precondition for ever searching more than one position in parallel.

Then await the task properly rather than spinning on a flag:

```csharp
var task = System.Threading.Tasks.Task.Run(() => engine.GetBestMove());
while (!task.IsCompleted) yield return null;
if (task.IsFaulted) { Debug.LogException(task.Exception); yield break; }
Move bestMove = task.Result;
```

`task.Result` rethrows on the main thread, so engine bugs become visible stack traces. Replace `UnityEngine.Random` with `System.Random` in `RandomBotEngine`, and either drop the `Debug.Log` calls from the engines or accumulate the statistics into fields and log them from the main thread after the move returns.

### 1.10 Captured pieces are hidden, not destroyed

```csharp
pieces[stoprank, stopfile].transform.position = new Vector3(10, 10, 1); // i have not figured how to delete the captured piece
```

Every captured piece stays alive off-screen, stacked at the same coordinate. In a bot-versus-bot session that is a slow leak of `GameObject`s and `SpriteRenderer`s that are still being culled and drawn.

**Fix.** `Destroy(pieces[stoprank, stopfile].gameObject);` immediately after clearing the array slot. If you later want move-by-move undo in the UI, pool them instead: keep an inactive list and `SetActive(false)` rather than moving them, which is cheaper than an off-screen transform anyway because inactive objects are skipped entirely.

---

## 2. The engine lives in two places

> Why `Board : MonoBehaviour` makes this duplication unavoidable, and what taking it off
> `MonoBehaviour` involves, is covered in
> [THREADING-AND-UNITY.md](THREADING-AND-UNITY.md).

`EngineTests/Program.cs` contains a complete `Chess.ChessBoard` — bitboards, `LoadFen`, all the attack generators, `GetPinnedPieces`, `GenerateRay`, `NumChecks`, `MakeMove`, `UndoMove`, `GenerateMoves` — roughly three thousand lines duplicating `UnityProject/Assets/Scripts/Board.cs`. The two have already diverged: the .NET copy uses `System.Numerics.BitOperations` and a `Stack<uint>` with `const` flags, while the Unity copy uses `Unity.Mathematics` and a `Stack<ulong>`, and has gained Zobrist hashing, the half-move counter, threefold detection and insufficient-material detection that the .NET copy never received.

This is the most consequential structural problem in the repository, because it undermines the thing it exists to provide. Perft on the .NET copy tells you the .NET copy is correct. It says nothing about the board that actually plays games — and every bug in section 1 lives in the Unity copy, where perft cannot reach it. Bug 1.1 in particular is exactly what a perft-with-undo test catches.

**Fix.** One engine, in one place, referenced by both front ends.

1. Take `Board` off `MonoBehaviour` and make it a plain C# class. Nothing in it needs the Unity lifecycle — there is no `Update`, and the static constructor already handles the knight table. Give it a real constructor taking a FEN, and change `GraphicalBoard` to hold `Board board = new Board(starting_position_fen);` instead of a serialized field.
2. Move the engine files (`Board.cs`, `SharedTypes.cs`, `Zobrist.cs`, `Engines/`) into a folder with an assembly definition (`ChessEngine.asmdef`) that references only `Unity.Mathematics`. Better, drop `Unity.Mathematics` — `BitboardUtils.TrailingZeroCount` compiles to the same `tzcnt` instruction and has no Unity dependency at all, which makes the assembly reusable as-is.
3. Have `EngineTests.csproj` compile those same files by link, instead of holding a copy:

```xml
<ItemGroup>
  <Compile Include="../UnityProject/Assets/Scripts/Engine/**/*.cs" />
</ItemGroup>
```

Then delete `Chess.ChessBoard`. Perft now tests the board that plays the games, and a fix can only be made once.

While you are in there: `EngineTests/Program.cs` contains the `ChessBoard` class and `EngineTests/Perft.cs` contains `Main`. The filenames are swapped relative to their contents. Rename them.

### Turn perft into an actual test suite

The current harness prints node counts to the console for you to eyeball against numbers you remember. Make the expectations explicit and machine-checked:

```csharp
(string fen, int depth, long expected)[] cases = {
    (STARTING_POSITION, 5, 4865609),
    ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 4, 4085603), // Kiwipete
    ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 6, 11030083),                          // en passant / pins
    ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w KQkq - 0 1", 5, 15833292),  // promotions
};
```

Those four positions between them exercise castling through attacked squares, the en passant pin case you wrote special-case code for, and promotion with capture — precisely the code most likely to be subtly wrong. Return a non-zero exit code on mismatch so it can gate a commit.

Add one more test that the current harness cannot express: **make/unmake symmetry**. Walk every move at depth 1, record the full board state (twelve bitboards, side, castling rights, en passant square, half-move counter, Zobrist hash), make the move, unmake it, and assert every field is byte-identical. Recurse to depth 4. That test alone catches bugs 1.1 and 1.2 immediately, and it is about twenty lines.

---

## 3. Redundant work

The search is the hot path, and the same expensive computation is repeated many times over.

### 3.1 `GameOver()` is called four times per completed move

`GraphicalBoard.OnMoveCompleted()`:

```csharp
isGameOn = !board.GameOver()[0];
if (!isGameOn)
{
    if (!board.GameOver()[1]) { ... }
    else { if (board.GameOver()[2]) { ... } }
}
```

Each `GameOver()` runs a full `GenerateMoves()` plus a full `NumChecks()`. Four calls, four identical full legal-move generations, per move, discarding three of them.

**Fix.** Call it once: `bool[] result = board.GameOver();`. And return something self-describing instead of `bool[3]` where the caller must remember that `[2]` means "white wins" — an enum is clearer and cannot be indexed wrongly:

```csharp
enum GameResult { Ongoing, WhiteWins, BlackWins, Draw }
```

That also removes the `bool[3]` allocation.

### 3.2 `GameOver()` is called at every search node, duplicating move generation

`BondFish1_4.Search`:

```csharp
if ((depth == 0) || board.GameOver()[0])
    return QuiescenceSearch(alpha, beta);
var moves = board.GenerateMoves();
```

`GameOver()` internally calls `GenerateMoves()`. Then `Search` calls `GenerateMoves()` again. Then `QuiescenceSearch` calls `Evaluate()`, which calls `GameOver()` — a third generation. **At each interior node the legal move list is built at least twice, and at each leaf three times.** Since move generation dominates the profile, you are paying two to three times what the search actually needs.

**Fix.** Generate once and derive everything from the result. The standard shape:

```csharp
var moves = board.GenerateMoves();
if (moves.Count == 0)
    return board.NumChecks(board.isWhite)[0] > 0 ? -100000 + ply : 0;  // mate or stalemate
if (depth == 0) return QuiescenceSearch(alpha, beta, ply);
```

Terminal detection falls out of `moves.Count == 0` for free, and `Evaluate()` reduces to counting material with no terminal check at all. Expect a large speedup for a small change — this is the highest-value performance fix in the project.

### 3.3 Allocation on the hot path

Per node, the current code allocates: a `List<Move>` and its internal array, a `bool[3]` from `GameOver()`, an `int[2]` from every `NumChecks()` call, a `(ulong, ulong)` tuple from `GetPinnedPieces` (a value type, so this one is free), and an `int[moves.Count]` for the scores. At the node counts a depth-7 search reaches, that is a great deal of garbage, and in Unity a GC spike is a visible frame hitch.

**Fix, in order of payoff.**

- Return the small fixed-size results by value: a `readonly struct CheckInfo` instead of `int[2]`, an enum instead of `bool[3]`. Free, and clearer at the call sites.
- Score moves into a reused buffer rather than a fresh `int[]` per node. Allocate `int[218]` once per engine (218 is the maximum legal move count in any chess position) and index into it.
- Longer term, replace `List<Move>` with a preallocated `Move[218]` per ply plus a count, so move generation allocates nothing at all. This is a larger change because `GenerateMoves` is written against `List<Move>.Add`, but it is the difference between a search that allocates per node and one that does not.

### 3.4 `Tile.OnMouseDown` calls `GenerateMoves()` up to five times per click

The click handler builds a candidate move and tests `board.GenerateMoves().Contains(proposed_move)`, then repeats for the castling, double-push, en passant and promotion variants — a fresh full legal-move generation for each, and the pawn branch does not `else if` between the cases, so a pawn click runs all of them. Human play is not performance-critical, so this costs nothing perceptible, but it is the same avoidable pattern as 3.1 and 3.2.

**Fix.** Generate once per click into a local, then test membership against that list. Better, generate once when a piece is *selected* and cache the legal destinations for that piece — you then get square highlighting almost for free, which is the single largest usability improvement available to the UI.

Two real bugs in the same handler while you are there: the queenside castling branch reuses `pos = Pos(rookSquare + 2);` from the kingside branch, which is the kingside offset; and promotion silently uses whatever `promotedTo` was last set to, defaulting to `0` (no promotion selected) if the player has not pressed a key. Default it to queen.

### 3.5 Selection sort, duplicated

Every engine from 1.3 onward sorts moves with an inline selection sort — an O(n²) pass — and the identical block appears **twice** in each engine, once in `Search` and once in `GetBestMove`.

Selection sort is arguably the right *choice* here, since alpha-beta usually cuts after a few moves and a full sort would be wasted work. But the implementation should exist once, and it should be incremental — swap the best remaining move into position `i` only when you are about to search position `i`, which is what the current nested loop does by accident. Extract it:

```csharp
static void SelectBestMove(List<Move> moves, int[] scores, int i)
```

Then `GetBestMove` and `Search` both call it, and there is one copy to fix when you add killer moves or history heuristics to the ordering.

---

## 4. Structure and duplication

### 4.1 `GenerateMoves` is about 1,670 lines in one method

`Board.cs:1693` to roughly `3360`. It cannot be unit tested in pieces, cannot be read on a screen, and cannot be modified with any confidence that a change to the white pawn logic has not broken the black pawn logic — because the two are separate near-identical copies, as are the pinned and unpinned variants of each.

The structure is genuinely there in the code, marked by comments: king moves, then pawns by rank band and pin status, then knights, then sliders, then castling. Those comments are section headers for methods that were never extracted.

**Fix.** Extract along the lines already drawn, into private methods taking the shared state (`pinnedPieces`, `partiallyPinnedPieces`, `checkIndex`, `attackedSquares`) as parameters:

```csharp
private void GenerateKingMoves(List<Move> moves, ulong attackedSquares, ...);
private void GeneratePawnMoves(List<Move> moves, ulong pinned, ulong partiallyPinned, ...);
private void GenerateKnightMoves(List<Move> moves, ulong pinned, ...);
private void GenerateSliderMoves(List<Move> moves, int index, ulong pinned, ...);
private void GenerateCastlingMoves(List<Move> moves, ulong attackedSquares);
```

Then collapse the colour duplication. Pawn movement differs between colours only in shift direction and in which ranks are the promotion and double-push ranks. Parameterise it:

```csharp
int forward       = isWhite ? 8 : -8;
ulong startRank   = isWhite ? 0x000000000000FF00UL : 0x00FF000000000000UL;
ulong promoteRank = isWhite ? 0x00FF000000000000UL : 0x000000000000FF00UL;
```

One implementation, driven by three values, replaces two mirrored blocks. Do this *after* the perft-plus-symmetry suite from section 2 is in place — that is exactly the refactor that needs a test to lean on, and with it the refactor is safe and mechanical.

### 4.2 `GetPinnedPieces` and `GenerateRay` repeat the same direction logic eight times

`GenerateRay` has eight `case` blocks that differ only in the shift amount and the wrap mask. `GetPinnedPieces` runs three near-identical while loops (bishop, rook, queen) whose bodies branch on `% 9`, `% 8`, `% 7`, `% 1` to recover a direction. Together that is around 500 lines expressing about 40 lines of logic.

The `%` arithmetic is also fragile in a way the comments acknowledge — there is a note about 63 being divisible by both 9 and 7, and about needing `(square / 8) != (kingSquare / 8)` to exclude horizontal pins from the diagonal case. Those are real edge cases being handled by careful reasoning about modular arithmetic that has to be re-derived every time the code is read.

**Fix.** Table the directions and loop:

```csharp
static readonly int[] DirOffsets   = { 9, 7, -7, -9, 8, 1, -1, -8 };
static readonly ulong[] DirWrapMask = { ~FileA, ~FileH, ~FileA, ~FileH, Full, ~FileA, ~FileH, Full };
```

One ray-walking loop parameterised by index replaces all eight cases, and the bishop/rook/queen distinction becomes a start and end index into the table — which is already how `GenerateRay` selects its `loop_start` and `loop_end`, so the idea is present, just not carried through.

Then replace the `%` direction arithmetic entirely with a precomputed table, filled once in the static constructor:

```csharp
static readonly ulong[,] Between = new ulong[64, 64];  // squares strictly between a and b, 0 if not aligned
```

`GetPinnedPieces` then reduces to: for each potential pinner, `ulong between = Between[kingSquare, pinnerSquare]; if (between == 0) continue;` and count the friendly pieces on it. The whole `% 9` / `% 7` / rank-comparison edge case family disappears, along with the inner loops that rebuild `inBetweenBitboard` shift by shift on every call. It is 4 KB of memory, computed once, and it is both faster and shorter than what it replaces.

This is also the natural stepping stone toward magic bitboards, if you go there — `Between` and a `Line` table are the same infrastructure.

### 4.3 `Evaluate()` is copy-pasted verbatim across four engines

`BondFish1_1` through `1_4` each contain the same `Evaluate()` — the same `GameOver()` terminal check, the same five `PieceCount(...) * value` lines, the same perspective flip — and each declares its own `Pawn = 100 ... King = 10000` fields and its own `PieceValues` array initialisation in its constructor. When you add pawn structure or piece-square tables, you will write it four times or, more likely, write it once and leave the other three inconsistent, which quietly invalidates every comparison between versions.

**Fix.** An abstract base class holding what does not vary:

```csharp
public abstract class BondFishBase : IBotEngine
{
    protected static readonly int[] PieceValues = { 100, 300, 300, 500, 900, 10000 };
    protected readonly Board board;
    protected readonly int Depth;
    protected int Positions;

    protected virtual int Evaluate() { ... }
    protected int GetValue(int pieceType) => PieceValues[pieceType % 6];
    protected int ScoreMove(Move move) { ... }
    public abstract Move GetBestMove();
    public abstract string GetName();
}
```

Each version then contains only what makes it that version: 1.2 adds alpha-beta to `Search`, 1.3 overrides `ScoreMove`, 1.4 adds `QuiescenceSearch`. The diff between two versions becomes the actual difference between two versions — which is the whole point of keeping the series.

Keeping the old versions runnable for comparison is a good idea and worth preserving. Sharing the parts that are not under comparison is what makes the comparison meaningful.

### 4.4 `HumanPlayer` does nothing, and input lives in the view

`IPlayer` exists to abstract over human and bot turns, but `HumanPlayer.OnTurnStarted()` and `OnGameStarted()` are empty. The actual input handling is in `ChessMan.OnMouseDown` and `Tile.OnMouseDown`, which reach back into `GraphicalBoard` and `Board` directly. The abstraction is therefore only half-built: `BotPlayer` drives its own turn, `HumanPlayer` does not.

The visible consequence is that `GraphicalBoard.whiteIsHuman` and `blackIsHuman` are set in `Start()` and then never read — `StartNewGame()` hardcodes two bots and the human lines are commented out. The fields look like configuration and are dead.

**Fix.** Give `HumanPlayer` the input responsibility. `OnTurnStarted()` enables input and computes the legal move list once; the click handlers report a selected square to the current `IPlayer` rather than to the board; `HumanPlayer` validates against its cached list and calls back into `GraphicalBoard` the same way `BotPlayer` does. `Tile` and `ChessMan` go back to being view objects that report clicks, which is all they should be.

Then make `StartNewGame` read its configuration instead of hardcoding it:

```csharp
[SerializeField] bool whiteIsHuman;
[SerializeField] bool blackIsHuman;
[SerializeField] int whiteDepth = 5;
[SerializeField] int blackDepth = 5;
```

with a small factory choosing the engine. Comparing 1.3 against 1.4 then takes no recompile, which matters when the entire point of the version series is running them against each other.

### 4.5 `GameObject.Find` for dependency wiring

`ChessMan` and `Tile` locate their collaborators by name at runtime:

```csharp
GameObject.Find("Graphical Board")
GameObject.Find("Board")
GameObject.Find("Clock Canvas")
```

`GameObject.Find` walks the entire active scene hierarchy, matches on a string, and returns `null` for inactive objects. Renaming an object in the editor breaks the game with a `NullReferenceException` at click time rather than an error at build time.

**Fix.** Since `GraphicalBoard` already instantiates every tile and piece, inject at creation:

```csharp
newTile.Initialise(this, board, clock);
```

No string lookups, no scene walk, no silent breakage on rename, and the dependencies of `Tile` become visible in its signature.

### 4.6 Move flags are instance fields rather than constants

In the Unity `Board` the flags are instance `int` fields:

```csharp
public int nullFlag = 0;
public int regularFlag = 1;
...
```

Which means every `Board` carries ten redundant `int`s, they are publicly mutable, and callers must hold a `Board` reference to name a flag — hence `board.captureFlag` and `flag >= 6` scattered through the engines. `BondFish1_4.ScoreMove` even comments on the fragility:

```csharp
if (flag >= 6) // Prone to bug but represents capture flags.
```

Note also that the comment says "capture flags" where it means promotion flags. The `EngineTests` copy got this right with `const int`, so the Unity version is a regression.

**Fix.** An enum, which makes the magic-number comparisons expressible:

```csharp
public enum MoveFlag : byte
{
    Null, Regular, Capture, Castling, DoublePawnMove, EnPassant,
    KnightPromotion, BishopPromotion, RookPromotion, QueenPromotion
}
```

Then `flag >= 6` becomes `flag >= MoveFlag.KnightPromotion`, which is checkable by eye, and `Move.Flag` can be a property on the struct so the `(data >> 12) & 0x003F` decoding stops being copy-pasted at roughly forty call sites. Add `StartSquare` and `StopSquare` properties for the same reason — every one of those hand-decodes is a chance to write `0x000F` where you meant `0x003F`, and both masks already appear in the codebase for the same field.

---

## 5. Smaller things

**Debug logging left in shipped paths.** `CheckForInsufficientMaterial` contains `UnityEngine.Debug.Log(111111)` through `(555555)` as trace markers, and every engine logs its name and node count on every move. In a bot-versus-bot game that is hundreds of console lines per game, from a background thread. Remove the numeric markers; put the node counts behind a `[SerializeField] bool verboseLogging` flag.

**Spelling: "enpessant" should be "en passant."** It appears in field names, method names, flags and comments throughout. Purely cosmetic, but it is in the public API surface of `Board`, and it will not match anything you search for when reading chess programming references — which matters, because the en passant edge cases are exactly where you will need those references.

**`empty` is redundant.** It is maintained alongside `occupied` as `~occupied`, and the comment in `EngineTests/Program.cs:40` already notes it "would probably go given it can be easily found as ~occupied." That is correct — a `NOT` is a single instruction, cheaper than keeping a second field in sync. Drop it.

**`PieceCount` should use a popcount intrinsic.** It loops with `math.tzcnt`, clearing one bit per iteration — up to eight iterations for pawns. `math.countbits(bitboards[index])` is one instruction, and it is called ten times per evaluation, at every leaf of the search.

**`Perft` on a `MonoBehaviour`.** `GraphicalBoard.Perft` duplicates the perft driver in `EngineTests`. Once the engine is a plain library (section 2), delete the Unity copy.

**Unused `using` directives.** `Board.cs` imports `System.Runtime.ExceptionServices`, `System.Globalization`, `System.Diagnostics`, `System.Collections` and `Unity.VisualScripting`; `BondFish1.4.cs` imports `System.Net.Http.Headers`. Harmless, but `Unity.VisualScripting` is a real assembly reference that slows compilation for nothing.

**Version control.** There is one commit (`e139d2f initial commit`), and most of the current architecture — `BotPlayer`, `Clock`, `Engines/`, `GameStatusController`, `HumanPlayer`, `IPlayer`, `Tile`, `Zobrist` — is untracked. Everything since the initial commit is one `rm -rf` from gone, and you have no way to compare BondFish 1.3's behaviour before and after a change to shared code.

Commit the untracked files now. Then commit per meaningful change, and tag the engine versions (`git tag bondfish-1.4`) so a version can be checked out and re-run. Add a `.gitignore` for Unity — `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`, and `Recordings/` if those are large local captures. Untracked `.meta` files are worth attention too: Unity `.meta` files must be committed alongside their assets, or GUID references break for anyone else who clones the project.

---

## Suggested order of work

1. **Commit what exists** (section 5), so nothing below can lose work.
2. **Fix the three correctness bugs**: half-move offset (1.1), castling-rights reference copy (1.2), `NumChecks` index (1.3). Small, contained, and each one is currently corrupting search results.
3. **Unify the engine and build the test suite** (section 2): `Board` off `MonoBehaviour`, `EngineTests` compiling the real files, perft cases with expected counts, and the make/unmake symmetry test. This is what makes everything after it safe.
4. **Remove the redundant generation** (3.1, 3.2). Largest speed win for the least code, and the tests from step 3 confirm the node counts are unchanged.
5. **Give the search its own board** (1.9), removing the data race and making engine exceptions visible.
6. **Refactor the large methods** (4.1, 4.2), with the tests to lean on. The `Between` table is the highest-leverage single change here.
7. **Then the rest**: engine base class (4.3), the `IPlayer` split (4.4), the visual bugs (1.4, 1.5, 1.10), quiescence and mate scoring (1.7, 1.8), Zobrist entropy (1.6).

Steps 2 and 3 are the ones that change how the project develops from here rather than just fixing what is in front of you. Every bug in section 1 is a bug perft on the real board with a symmetry check would have surfaced on its own — that suite is the difference between finding these by reading and finding them automatically.
