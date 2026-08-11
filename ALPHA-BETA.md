# Alpha-beta pruning

Notes on the search algorithm behind `BondFish1_2` and everything after it, worked
through in terms of this repository's own code rather than generic pseudocode.

The short answer to "what does alpha-beta do" is: it returns **exactly the same move
as plain minimax**, having looked at far fewer positions. It is not an approximation
and it does not trade strength for speed. Every position it skips is one whose value
provably cannot change the answer.

- [What alpha and beta actually are](#what-alpha-and-beta-actually-are)
- [The starting point: 1.1, no pruning](#the-starting-point-11-no-pruning)
- [The long variant: two mutually recursive functions](#the-long-variant-two-mutually-recursive-functions)
- [The short variant: negamax](#the-short-variant-negamax)
- [The line everyone copies without understanding](#the-line-everyone-copies-without-understanding)
- [Fail-hard vs fail-soft](#fail-hard-vs-fail-soft)
- [A cutoff, traced](#a-cutoff-traced)
- [Why move ordering multiplies the gain](#why-move-ordering-multiplies-the-gain)
- [The root node is a special case](#the-root-node-is-a-special-case)
- [A test you can run](#a-test-you-can-run)
- [Two known issues in this code](#two-known-issues-in-this-code)

## What alpha and beta actually are

Templates present them as two ints threaded through recursion. They are not. They are
a **contract with the caller** about which answers are worth computing.

At any node:

- **alpha** — a *floor*. "The side to move here has already guaranteed itself `alpha`
  somewhere else in the tree. Don't bother reporting anything worse; it won't be chosen."
- **beta** — a *ceiling*. "If this position turns out to be worth more than `beta` to
  the side to move, the opponent one level up will avoid this branch entirely. The exact
  value is then irrelevant."

`(alpha, beta)` is the window of scores that could still **change a decision**. Anything
outside it is information nobody will act on. Alpha-beta's entire trick is refusing to
compute answers nobody will act on.

## The starting point: 1.1, no pruning

`BondFish1_1.Search` is plain negamax. Every node visits every move, unconditionally:

```csharp
public int Search(int depth)
{
    Positions += 1;
    if ((depth == 0) || board.GameOver()[0])
    {
        return Evaluate();
    }
    var moves = board.GenerateMoves();

    int best_score = -1000000;
    foreach (Move move in moves)
    {
        board.MakeMove(move);
        int score = -Search(depth - 1);

        if (score > best_score)
        {
            best_score = score;
        }
        board.UndoMove();
    }
    return best_score;
}
```

Keep `best_score` in mind. In 1.2 it disappears, and understanding *where it went* is
most of understanding alpha-beta.

## The long variant: two mutually recursive functions

This is the original Knuth–Moore shape. Scores are **absolute** — positive always means
good for White — and there is not a single negation anywhere.

```csharp
// Returns the position's value, ALWAYS from White's point of view.
// White maximises; Black minimises.

int Maximise(int alpha, int beta, int depth)   // White to move
{
    if (depth == 0 || IsTerminal()) return EvaluateAbsolute();

    int best = int.MinValue;

    foreach (Move move in GenerateMoves())
    {
        MakeMove(move);
        int score = Minimise(alpha, beta, depth - 1);
        UndoMove();

        if (score > best) best = score;
        if (best > alpha) alpha = best;      // raise our floor

        // BETA CUTOFF. Black, above us, already has an option worth `beta` or
        // better for Black. We can reach at least `alpha`, and alpha >= beta means
        // Black would never allow this branch. The remaining moves can only push
        // this value higher, i.e. make it even more unacceptable to Black.
        if (alpha >= beta) break;
    }
    return best;
}

int Minimise(int alpha, int beta, int depth)   // Black to move
{
    if (depth == 0 || IsTerminal()) return EvaluateAbsolute();

    int best = int.MaxValue;

    foreach (Move move in GenerateMoves())
    {
        MakeMove(move);
        int score = Maximise(alpha, beta, depth - 1);
        UndoMove();

        if (score < best) best = score;
        if (best < beta) beta = best;        // lower our ceiling

        // ALPHA CUTOFF — the exact mirror image.
        if (beta <= alpha) break;
    }
    return best;
}
```

Root call: `Maximise(-INFINITY, +INFINITY, depth)`.

Four things are worth reading off this, because the short form hides all four:

1. **alpha and beta pass down unchanged.** No swapping, no negating.
2. **The maximiser only ever raises alpha; the minimiser only ever lowers beta.** Each
   side tightens its own end of the window and never touches the other's.
3. **The cutoff test is identical in both**: `alpha >= beta`. The window has collapsed
   to empty — there is no score left that could influence any decision above.
4. **`EvaluateAbsolute` ignores whose turn it is.** Positive is good for White, always.

## The short variant: negamax

The identity that collapses those two functions into one:

```
max(a, b) == -min(-a, -b)
```

To exploit it, evaluation must become **relative to the side to move** rather than
absolute. This project's `Evaluate()` already is:

```csharp
if (board.isWhite) { return (white_count - black_count); }
else               { return (black_count - white_count); }
```

Positive means good for whoever is on move. That is the precondition negamax requires.
Now "the opponent minimises" becomes "the opponent maximises, negated," and one function
suffices — which is `BondFish1_2.Search`, essentially verbatim:

```csharp
public int Search(int alpha, int beta, int depth)
{
    Positions += 1;
    if ((depth == 0) || board.GameOver()[0])
    {
        return Evaluate();
    }
    var moves = board.GenerateMoves();

    foreach (Move move in moves)
    {
        board.MakeMove(move);
        int score = -Search(-beta, -alpha, depth - 1);
        board.UndoMove();

        if (score >= beta)
        {
            return beta;          // fail-high: beta cutoff
        }
        if (score > alpha)
        {
            alpha = score;        // raise the floor
        }
    }
    return alpha;
}
```

Note what happened to `best_score` from 1.1: it is gone, merged into `alpha`. That is
legitimate for a maximiser — after the loop, `alpha` *is* the best score found. **Alpha
is 1.1's `best_score`, except shared with the caller so the caller can prune.** That
single sentence is the whole difference between the two versions.

## The line everyone copies without understanding

```csharp
int score = -Search(-beta, -alpha, depth - 1);
```

There are three negations and **each has a separate reason**:

| Negation | Why |
| --- | --- |
| The outer `-` on the result | The child returns its score relative to *its* mover — your opponent. Negating converts it to your perspective. |
| `alpha` and `beta` **swap positions** | Your floor is your opponent's ceiling. A line you have guaranteed worth `alpha` to you is a cap on what they can get. The roles invert, so the operands must trade places. |
| The `-` on each bound | The same perspective flip as the first, applied to the bounds themselves. |

So `(-beta, -alpha)` reads as: *the identical window, viewed from the other side of the
board.* And because `alpha < beta` always holds on entry, `-beta < -alpha` holds too —
the window stays well-formed all the way down.

Getting the swap wrong is the classic template bug. Passing `(-alpha, -beta)` inverts
the window, every comparison misfires, and the search still runs and still returns
plausible-looking moves — it just prunes lines it should have kept.

## Fail-hard vs fail-soft

The 1.2 code returns `beta` on a cutoff and `alpha` when nothing improves — **the
bounds, not the scores**. The return value is clamped into `[alpha, beta]`. You learn
"at least beta" or "at most alpha," never by how much. That is **fail-hard**.

**Fail-soft** keeps `best` separate and returns it:

```csharp
int bestScore = -1000000;
foreach (Move move in moves)
{
    board.MakeMove(move);
    int score = -Search(-beta, -alpha, depth - 1);
    board.UndoMove();

    if (score > bestScore) bestScore = score;   // track the truth
    if (score > alpha)     alpha = score;       // track the window
    if (alpha >= beta)     break;
}
return bestScore;        // may fall outside [alpha, beta]
```

Identical move choice, strictly more information. It matters as soon as a transposition
table is added, because you would be storing a real bound instead of just `beta`.

## A cutoff, traced

Depth 2, White to move, candidate moves **A** and **B**. All scores relative to the
mover, as negamax requires.

**Move A** → `-Search(-INF, +INF, 1)`, Black to move

- Reply A1 → leaf is +3 for White; White to move, so `Evaluate() = +3`; Black's score is `-3`
- Reply A2 → leaf is +5 for White; Black's score is `-5`
- Black maximises its own score, so the best is `-3`. The node returns `-3`.

Back at the root: `score_A = -(-3) = +3`, so `alpha = 3`.

**Move B** → `-Search(-beta, -alpha, 1)` = `-Search(-INF, -3, 1)`, Black to move, window
`alpha' = -INF`, `beta' = -3`

- Reply B1 → leaf is +1 for White; Black's score is `-1`
- `-1 >= beta'(-3)` → **cutoff.** Return immediately.
- **B2, B3, … are never generated or searched.**

Why that is sound: White already has A guaranteeing 3. Down move B, Black has found a
reply holding White to 1. Black will play at least that well, so B is worth *at most* 1
to White — already worse than 3. The unexamined replies could only make B worse still.
They cannot rescue it, so their values are irrelevant.

Note precisely what was proved: **B is bad enough to reject**, not B's exact value.
Interior nodes routinely return bounds rather than truth. That is not a defect — a bound
was all the parent needed.

The trace also shows fail-hard costing something real. It returned `-3` (the bound)
where fail-soft would have returned `-1`, telling the root "B is worth at most 1." Same
decision either way; more information in the second case.

## Why move ordering multiplies the gain

The savings come *entirely* from cutting off early, which requires searching good moves
first.

- **Perfect ordering** — best move first at every node — reduces the tree from roughly
  `b^d` nodes to about `b^(d/2)`. That is the square root of the minimax tree: a
  doubling of reachable depth for the same cost.
- **Worst ordering** produces zero cutoffs and searches exactly the same tree as plain
  minimax — i.e. 1.1, plus the overhead of passing two extra ints.

Same tree, same answer, wildly different cost. That is the entire reason `BondFish1_3`'s
MVV-LVA ordering exists, and it is why 1.3 is not a strength change so much as a depth
change.

## The root node is a special case

`GetBestMove` in 1.2 is the same loop with two deliberate differences:

```csharp
int alpha = -1000000;
int beta  =  1000000;

foreach (Move move in moves)
{
    board.MakeMove(move);
    int score = -Search(-beta, -alpha, Depth - 1);
    board.UndoMove();

    if (score > alpha)
    {
        alpha = score;
        best_move = move;
    }
}
```

- **No beta cutoff.** Correct, and necessary. There is no parent to prune on behalf of;
  `beta` stays at `+1000000` forever and the root window is effectively `(alpha, +INF)`,
  narrowing from below as better moves are found.
- **The move is recorded, not just the score.** Interior nodes only need a value; the
  root needs to know *which* move produced it.

Because the root window starts fully open, the score it computes is **exact**. Interior
nodes may return bounds; the root does not. That fact is what makes the next section a
valid test.

## A test you can run

At equal depth, **1.1, 1.2 and 1.3 must return the same root score.** All three search
the root with a full window, so all three compute the true value; only the node count
should differ, and it should drop sharply at each step. Log `Positions` and compare.

Any score disagreement means the pruning is unsound rather than merely slow — which is
exactly the bug class that is otherwise invisible, because an engine that prunes
incorrectly still plays legal, reasonable-looking chess.

The *move* may legitimately differ between versions when several moves tie for best,
since reordering changes which one is found first. The score may not.

## Two known issues in this code

Both are covered in [CODE-REVIEW.md](CODE-REVIEW.md); they are repeated here because
they sit directly on the search path.

**Mate scores are not ply-adjusted.** `Evaluate()` returns a flat `-100000` for
checkmate, so a mate in one and a mate in five score identically. The engine has no
reason to prefer the faster mate, and no reason to prefer the more distant loss. The
standard fix is to fold the distance from the root into the score — `-MATE + ply` — which
requires threading a `ply` counter down the recursion.

**`GameOver()` calls `GenerateMoves()` internally**, and `Search` then calls
`GenerateMoves()` again for the move loop. The full legal move list is therefore built
two or more times per node. Since move generation dominates the cost of a node, this is
a straight multiplier on the whole search — and it applies at every version, so it also
distorts the `Positions` comparison above.

**A robustness note on `GetBestMove`.** `best_move` is initialised to
`new Move(0, 0, 0)` and only overwritten when `score > alpha`. It is safe as written,
because mate scores are ±100000 while `alpha` starts at -1000000, so the first move
always clears the bar. But it is load-bearing on that gap: if mate scores ever grow past
the initial alpha, the engine returns the null move, and `BotPlayer`'s
`if (bestMove.data != 0)` guard means it silently plays nothing. Initialising
`best_move = moves[0]` before the loop removes the failure mode outright.
