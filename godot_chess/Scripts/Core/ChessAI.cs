/*
 * Archivo: Scripts/Core/ChessAI.cs
 * Proposito:
 *   Motor de Inteligencia Artificial local (CPU) completamente autonomo sin dependencias externas.
 *   Utiliza Negamax con poda Alfa-Beta, ordenamiento de capturas MVV-LVA, tablas posicionales
 *   por tipo de pieza (Piece-Square Tables) y busqueda de quietud (Quiescence Search) para evitar
 *   errores tacticos graves. Se ejecuta asincronamente en segundo plano para no congelar la app.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AjedrezGodot.Core;

public static class ChessAI
{
    private const int Infinity = 1_000_000;
    private const int CheckmateScore = 100_000;

    // Valores materiales clasicos en centipeones
    private static readonly int[] PieceValues =
    {
        0,    // None
        100,  // Pawn
        320,  // Knight
        330,  // Bishop
        500,  // Rook
        900,  // Queen
        20000 // King
    };

    // Tablas posicionales orientadas desde la perspectiva de las Blancas (fila 0 arriba, fila 7 abajo).
    private static readonly int[] PawnTable =
    {
         0,  0,  0,  0,  0,  0,  0,  0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 30, 30, 20, 10, 10,
         5,  5, 10, 27, 27, 10,  5,  5,
         0,  0,  0, 24, 24,  0,  0,  0,
         5, -5,-10,  0,  0,-10, -5,  5,
         5, 10, 10,-22,-22, 10, 10,  5,
         0,  0,  0,  0,  0,  0,  0,  0
    };

    private static readonly int[] KnightTable =
    {
        -50,-40,-30,-30,-30,-30,-40,-50,
        -40,-20,  0,  0,  0,  0,-20,-40,
        -30,  0, 12, 16, 16, 12,  0,-30,
        -30,  5, 16, 20, 20, 16,  5,-30,
        -30,  0, 16, 20, 20, 16,  0,-30,
        -30,  5, 12, 16, 16, 12,  5,-30,
        -40,-20,  0,  5,  5,  0,-20,-40,
        -50,-40,-30,-30,-30,-30,-40,-50
    };

    private static readonly int[] BishopTable =
    {
        -20,-10,-10,-10,-10,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  8, 12, 12,  8,  0,-10,
        -10,  6,  6, 12, 12,  6,  6,-10,
        -10,  0, 12, 12, 12, 12,  0,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10,  6,  0,  0,  0,  0,  6,-10,
        -20,-10,-10,-10,-10,-10,-10,-20
    };

    private static readonly int[] RookTable =
    {
         0,  0,  0,  0,  0,  0,  0,  0,
         6, 12, 12, 12, 12, 12, 12,  6,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
         0,  0,  0,  6,  6,  0,  0,  0
    };

    private static readonly int[] QueenTable =
    {
        -20,-10,-10, -5, -5,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5,  5,  5,  5,  0,-10,
         -5,  0,  5,  5,  5,  5,  0, -5,
          0,  0,  5,  5,  5,  5,  0, -5,
        -10,  5,  5,  5,  5,  5,  0,-10,
        -10,  0,  5,  0,  0,  0,  0,-10,
        -20,-10,-10, -5, -5,-10,-10,-20
    };

    private static readonly int[] KingMidTable =
    {
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -20,-30,-30,-40,-40,-30,-30,-20,
        -10,-20,-20,-20,-20,-20,-20,-10,
         20, 20,  0,  0,  0,  0, 20, 20,
         20, 30, 10,  0,  0, 10, 30, 20
    };

    /// <summary>
    /// Calcula asincronamente la mejor jugada para la CPU sin bloquear el hilo principal de Godot.
    /// </summary>
    public static Task<ChessMove?> ChooseBestMoveAsync(
        ChessBoard currentBoard,
        AIDifficulty difficulty,
        CancellationToken cancellationToken = default)
    {
        ChessBoard searchBoard = currentBoard.Clone();
        return Task.Run(() => ChooseBestMove(searchBoard, difficulty, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Selecciona la mejor jugada de manera sincrona sobre un tablero dedicado.
    /// </summary>
    public static ChessMove? ChooseBestMove(
        ChessBoard board,
        AIDifficulty difficulty,
        CancellationToken cancellationToken = default)
    {
        List<ChessMove> legalMoves = MoveGenerator.GenerateLegalMoves(board);
        if (legalMoves.Count == 0) return null;
        if (legalMoves.Count == 1) return legalMoves[0];

        int depth = difficulty switch
        {
            AIDifficulty.Easy => 2,
            AIDifficulty.Medium => 3,
            AIDifficulty.Hard => 4,
            _ => 3
        };

        OrderMoves(board, legalMoves);

        var scoredMoves = new List<(ChessMove move, int score)>(legalMoves.Count);
        int alpha = -Infinity;
        int beta = Infinity;

        foreach (ChessMove move in legalMoves)
        {
            cancellationToken.ThrowIfCancellationRequested();

            board.MakeMove(move);
            int score = -Negamax(board, depth - 1, -beta, -alpha, 1, difficulty == AIDifficulty.Hard, cancellationToken);
            board.UndoMove();

            scoredMoves.Add((move, score));
            if (score > alpha)
            {
                alpha = score;
            }
        }

        // Ordenar de mayor a menor puntuacion
        scoredMoves.Sort((a, b) => b.score.CompareTo(a.score));

        // En dificultad Facil permitimos variedad humana controlada dentro de un margen pequeno
        if (difficulty == AIDifficulty.Easy && scoredMoves.Count > 1)
        {
            int bestScore = scoredMoves[0].score;
            var candidates = new List<ChessMove>();
            foreach (var item in scoredMoves)
            {
                if (bestScore - item.score <= 90)
                {
                    candidates.Add(item.move);
                }
            }
            return candidates[Random.Shared.Next(candidates.Count)];
        }

        // Entre jugadas empatadas exactamente al mejor valor, elegir aleatoriamente para partidas variadas
        int topScore = scoredMoves[0].score;
        var bestTies = new List<ChessMove>();
        foreach (var item in scoredMoves)
        {
            if (item.score == topScore) bestTies.Add(item.move);
            else break;
        }

        return bestTies[Random.Shared.Next(bestTies.Count)];
    }

    private static int Negamax(
        ChessBoard board,
        int depth,
        int alpha,
        int beta,
        int plyFromRoot,
        bool useQuiescence,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (board.HalfmoveClock >= 100 || board.IsThreefoldRepetition() || board.HasInsufficientMaterial())
        {
            return 0;
        }

        List<ChessMove> legalMoves = MoveGenerator.GenerateLegalMoves(board);
        if (legalMoves.Count == 0)
        {
            if (MoveGenerator.IsInCheck(board, board.SideToMove))
            {
                // Preferimos mates mas cortos y retrasar mates en contra
                return -(CheckmateScore - plyFromRoot);
            }
            return 0; // Ahogado
        }

        if (depth <= 0)
        {
            return useQuiescence
                ? QuiescenceSearch(board, alpha, beta, 0, cancellationToken)
                : EvaluatePosition(board);
        }

        OrderMoves(board, legalMoves);

        int bestScore = -Infinity;
        foreach (ChessMove move in legalMoves)
        {
            board.MakeMove(move);
            int score = -Negamax(board, depth - 1, -beta, -alpha, plyFromRoot + 1, useQuiescence, cancellationToken);
            board.UndoMove();

            if (score > bestScore) bestScore = score;
            if (score > alpha) alpha = score;
            if (alpha >= beta) break; // Poda Beta
        }

        return bestScore;
    }

    private static int QuiescenceSearch(
        ChessBoard board,
        int alpha,
        int beta,
        int qDepth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int standPat = EvaluatePosition(board);
        if (standPat >= beta) return beta;
        if (alpha < standPat) alpha = standPat;
        if (qDepth >= 4) return standPat;

        List<ChessMove> captures = MoveGenerator.GenerateLegalMoves(board, capturesOnly: true);
        OrderMoves(board, captures);

        foreach (ChessMove move in captures)
        {
            board.MakeMove(move);
            int score = -QuiescenceSearch(board, -beta, -alpha, qDepth + 1, cancellationToken);
            board.UndoMove();

            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }

        return alpha;
    }

    /// <summary>
    /// Evalua la posicion estatica desde la perspectiva del jugador en turno (Negamax).
    /// </summary>
    private static int EvaluatePosition(ChessBoard board)
    {
        int whiteScore = 0;
        int blackScore = 0;

        for (int sq = 0; sq < ChessBoard.BoardSize; sq++)
        {
            Piece p = board.GetPiece(sq);
            if (p.IsEmpty) continue;

            int row = SquareHelper.Row(sq);
            int col = SquareHelper.Col(sq);
            int tableSq = p.Color == PieceColor.White ? sq : ((7 - row) * 8) + col;

            int positional = p.Type switch
            {
                PieceType.Pawn => PawnTable[tableSq],
                PieceType.Knight => KnightTable[tableSq],
                PieceType.Bishop => BishopTable[tableSq],
                PieceType.Rook => RookTable[tableSq],
                PieceType.Queen => QueenTable[tableSq],
                PieceType.King => KingMidTable[tableSq],
                _ => 0
            };

            int total = PieceValues[(int)p.Type] + positional;
            if (p.Color == PieceColor.White) whiteScore += total;
            else blackScore += total;
        }

        int eval = whiteScore - blackScore;
        return board.SideToMove == PieceColor.White ? eval : -eval;
    }

    /// <summary>
    /// Ordena jugadas priorizando capturas valiosas con piezas baratas (MVV-LVA) y promociones,
    /// aumentando drásticamente la eficiencia de la poda Alfa-Beta.
    /// </summary>
    private static void OrderMoves(ChessBoard board, List<ChessMove> moves)
    {
        moves.Sort((a, b) => ScoreMoveHeuristic(board, b).CompareTo(ScoreMoveHeuristic(board, a)));
    }

    private static int ScoreMoveHeuristic(ChessBoard board, ChessMove move)
    {
        int score = 0;
        Piece mover = board.GetPiece(move.From);

        if (move.IsCapture)
        {
            Piece victim = move.Special == MoveSpecial.EnPassant
                ? new Piece(PieceType.Pawn, SquareHelper.Opponent(mover.Color))
                : move.CapturedPiece;

            score += (PieceValues[(int)victim.Type] * 10) - PieceValues[(int)mover.Type];
        }

        if (move.IsPromotion)
        {
            score += PieceValues[(int)move.PromotionPiece];
        }

        if (move.Special == MoveSpecial.CastleKingSide || move.Special == MoveSpecial.CastleQueenSide)
        {
            score += 60;
        }

        return score;
    }
}
