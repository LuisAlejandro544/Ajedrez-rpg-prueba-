/*
 * Archivo: Scripts/Core/MoveGenerator.cs
 * Proposito:
 *   Generador completo de jugadas legales de ajedrez. Implementa reglas oficiales:
 *   movimientos de las 6 piezas, captura al paso, enroques corto y largo (verificando
 *   que el rey no este en jaque ni atraviese casillas atacadas), coronacion de peones,
 *   deteccion de jaque, jaque mate, ahogado y tablas. Incluye formateo en notacion algebraica.
 */

using System.Collections.Generic;

namespace AjedrezGodot.Core;

public static class MoveGenerator
{
    private static readonly (int dr, int dc)[] KnightOffsets =
    {
        (-2, -1), (-2, 1), (-1, -2), (-1, 2),
        (1, -2), (1, 2), (2, -1), (2, 1)
    };

    private static readonly (int dr, int dc)[] KingOffsets =
    {
        (-1, -1), (-1, 0), (-1, 1),
        (0, -1),           (0, 1),
        (1, -1),  (1, 0),  (1, 1)
    };

    private static readonly (int dr, int dc)[] BishopDirs =
    {
        (-1, -1), (-1, 1), (1, -1), (1, 1)
    };

    private static readonly (int dr, int dc)[] RookDirs =
    {
        (-1, 0), (1, 0), (0, -1), (0, 1)
    };

    private static readonly (int dr, int dc)[] QueenDirs =
    {
        (-1, -1), (-1, 0), (-1, 1),
        (0, -1),           (0, 1),
        (1, -1),  (1, 0),  (1, 1)
    };

    private static readonly PieceType[] PromotionOptions =
    {
        PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight
    };

    /// <summary>
    /// Genera todas las jugadas estrictamente legales para el jugador en turno.
    /// </summary>
    public static List<ChessMove> GenerateLegalMoves(ChessBoard board, bool capturesOnly = false)
    {
        var pseudo = GeneratePseudoLegalMoves(board, capturesOnly);
        var legal = new List<ChessMove>(pseudo.Count);
        PieceColor moverColor = board.SideToMove;

        foreach (ChessMove move in pseudo)
        {
            board.MakeMove(move);
            int kingSq = board.GetKingSquare(moverColor);
            bool inCheck = IsSquareAttacked(board, kingSq, SquareHelper.Opponent(moverColor));
            board.UndoMove();

            if (!inCheck)
            {
                legal.Add(move);
            }
        }

        return legal;
    }

    /// <summary>
    /// Indica si el bando especificado tiene actualmente a su rey bajo ataque.
    /// </summary>
    public static bool IsInCheck(ChessBoard board, PieceColor color)
    {
        int kingSq = board.GetKingSquare(color);
        if (kingSq < 0) return false;
        return IsSquareAttacked(board, kingSq, SquareHelper.Opponent(color));
    }

    /// <summary>
    /// Evalua el estado reglamentario actual de la partida (Jaque, Jaque Mate, Ahogado o Tablas).
    /// </summary>
    public static GameStatus EvaluateGameStatus(ChessBoard board, List<ChessMove>? precomputedLegalMoves = null)
    {
        var legalMoves = precomputedLegalMoves ?? GenerateLegalMoves(board);
        bool inCheck = IsInCheck(board, board.SideToMove);

        if (legalMoves.Count == 0)
        {
            return inCheck ? GameStatus.Checkmate : GameStatus.Stalemate;
        }

        if (board.HalfmoveClock >= 100)
        {
            return GameStatus.DrawFiftyMove;
        }

        if (board.IsThreefoldRepetition())
        {
            return GameStatus.DrawThreefoldRepetition;
        }

        if (board.HasInsufficientMaterial())
        {
            return GameStatus.DrawInsufficientMaterial;
        }

        return inCheck ? GameStatus.Check : GameStatus.Playing;
    }

    /// <summary>
    /// Determina si una casilla esta siendo atacada por alguna pieza de 'attackerColor'.
    /// </summary>
    public static bool IsSquareAttacked(ChessBoard board, int square, PieceColor attackerColor)
    {
        int row = SquareHelper.Row(square);
        int col = SquareHelper.Col(square);

        // 1. Ataques de peon
        // Un peon blanco ataca hacia arriba (dr = -1), asi que un atacante blanco esta en row + 1.
        int pawnSourceRow = attackerColor == PieceColor.White ? row + 1 : row - 1;
        if (pawnSourceRow >= 0 && pawnSourceRow < 8)
        {
            if (col - 1 >= 0)
            {
                Piece p = board.GetPiece(pawnSourceRow, col - 1);
                if (p.Color == attackerColor && p.Type == PieceType.Pawn) return true;
            }
            if (col + 1 < 8)
            {
                Piece p = board.GetPiece(pawnSourceRow, col + 1);
                if (p.Color == attackerColor && p.Type == PieceType.Pawn) return true;
            }
        }

        // 2. Ataques de caballo
        foreach (var (dr, dc) in KnightOffsets)
        {
            int nr = row + dr;
            int nc = col + dc;
            if (!SquareHelper.IsValid(nr, nc)) continue;
            Piece p = board.GetPiece(nr, nc);
            if (p.Color == attackerColor && p.Type == PieceType.Knight) return true;
        }

        // 3. Ataques de rey
        foreach (var (dr, dc) in KingOffsets)
        {
            int nr = row + dr;
            int nc = col + dc;
            if (!SquareHelper.IsValid(nr, nc)) continue;
            Piece p = board.GetPiece(nr, nc);
            if (p.Color == attackerColor && p.Type == PieceType.King) return true;
        }

        // 4. Ataques diagonales (Alfil o Dama)
        foreach (var (dr, dc) in BishopDirs)
        {
            int nr = row + dr;
            int nc = col + dc;
            while (SquareHelper.IsValid(nr, nc))
            {
                Piece p = board.GetPiece(nr, nc);
                if (!p.IsEmpty)
                {
                    if (p.Color == attackerColor && (p.Type == PieceType.Bishop || p.Type == PieceType.Queen))
                    {
                        return true;
                    }
                    break;
                }
                nr += dr;
                nc += dc;
            }
        }

        // 5. Ataques ortogonales (Torre o Dama)
        foreach (var (dr, dc) in RookDirs)
        {
            int nr = row + dr;
            int nc = col + dc;
            while (SquareHelper.IsValid(nr, nc))
            {
                Piece p = board.GetPiece(nr, nc);
                if (!p.IsEmpty)
                {
                    if (p.Color == attackerColor && (p.Type == PieceType.Rook || p.Type == PieceType.Queen))
                    {
                        return true;
                    }
                    break;
                }
                nr += dr;
                nc += dc;
            }
        }

        return false;
    }

    /// <summary>
    /// Convierte una jugada a notacion algebraica legible en espanol (ej. e4, Cf3, O-O, e8=D+).
    /// Debe llamarse ANTES de ejecutar board.MakeMove(move), o tras deshacerla.
    /// </summary>
    public static string FormatMoveNotation(ChessBoard board, ChessMove move)
    {
        if (move.Special == MoveSpecial.CastleKingSide) return "O-O";
        if (move.Special == MoveSpecial.CastleQueenSide) return "O-O-O";

        Piece moving = board.GetPiece(move.From);
        string pieceLetter = moving.Type switch
        {
            PieceType.King => "R",
            PieceType.Queen => "D",
            PieceType.Rook => "T",
            PieceType.Bishop => "A",
            PieceType.Knight => "C",
            _ => string.Empty
        };

        string fromAlg = SquareHelper.ToAlgebraic(move.From);
        string toAlg = SquareHelper.ToAlgebraic(move.To);
        string captureSign = move.IsCapture ? "x" : "";

        string text;
        if (moving.Type == PieceType.Pawn)
        {
            text = move.IsCapture ? $"{fromAlg[0]}x{toAlg}" : toAlg;
        }
        else
        {
            text = $"{pieceLetter}{captureSign}{toAlg}";
        }

        if (move.IsPromotion)
        {
            string promoLetter = move.PromotionPiece switch
            {
                PieceType.Queen => "D",
                PieceType.Rook => "T",
                PieceType.Bishop => "A",
                PieceType.Knight => "C",
                _ => "D"
            };
            text += $"={promoLetter}";
        }

        // Comprobar si la jugada provoca jaque o mate
        board.MakeMove(move);
        var nextMoves = GenerateLegalMoves(board);
        bool givesCheck = IsInCheck(board, board.SideToMove);
        if (givesCheck)
        {
            text += nextMoves.Count == 0 ? "#" : "+";
        }
        board.UndoMove();

        return text;
    }

    private static List<ChessMove> GeneratePseudoLegalMoves(ChessBoard board, bool capturesOnly)
    {
        var moves = new List<ChessMove>(48);
        PieceColor side = board.SideToMove;

        for (int sq = 0; sq < ChessBoard.BoardSize; sq++)
        {
            Piece piece = board.GetPiece(sq);
            if (piece.IsEmpty || piece.Color != side) continue;

            int row = SquareHelper.Row(sq);
            int col = SquareHelper.Col(sq);

            switch (piece.Type)
            {
                case PieceType.Pawn:
                    AddPawnMoves(board, sq, row, col, side, capturesOnly, moves);
                    break;
                case PieceType.Knight:
                    AddStepMoves(board, sq, row, col, side, KnightOffsets, capturesOnly, moves);
                    break;
                case PieceType.Bishop:
                    AddSlidingMoves(board, sq, row, col, side, BishopDirs, capturesOnly, moves);
                    break;
                case PieceType.Rook:
                    AddSlidingMoves(board, sq, row, col, side, RookDirs, capturesOnly, moves);
                    break;
                case PieceType.Queen:
                    AddSlidingMoves(board, sq, row, col, side, QueenDirs, capturesOnly, moves);
                    break;
                case PieceType.King:
                    AddStepMoves(board, sq, row, col, side, KingOffsets, capturesOnly, moves);
                    if (!capturesOnly)
                    {
                        AddCastlingMoves(board, side, moves);
                    }
                    break;
            }
        }

        return moves;
    }

    private static void AddPawnMoves(
        ChessBoard board,
        int from,
        int row,
        int col,
        PieceColor side,
        bool capturesOnly,
        List<ChessMove> moves)
    {
        int dir = side == PieceColor.White ? -1 : 1;
        int startRow = side == PieceColor.White ? 6 : 1;
        int promoRow = side == PieceColor.White ? 0 : 7;

        int oneStepRow = row + dir;
        if (oneStepRow >= 0 && oneStepRow < 8)
        {
            // Avances sin captura
            if (!capturesOnly)
            {
                int oneStepSq = SquareHelper.ToIndex(oneStepRow, col);
                if (board.GetPiece(oneStepSq).IsEmpty)
                {
                    if (oneStepRow == promoRow)
                    {
                        foreach (PieceType promo in PromotionOptions)
                        {
                            moves.Add(new ChessMove(from, oneStepSq, MoveSpecial.Promotion, promo));
                        }
                    }
                    else
                    {
                        moves.Add(new ChessMove(from, oneStepSq));
                        if (row == startRow)
                        {
                            int twoStepSq = SquareHelper.ToIndex(row + (2 * dir), col);
                            if (board.GetPiece(twoStepSq).IsEmpty)
                            {
                                moves.Add(new ChessMove(from, twoStepSq, MoveSpecial.DoublePawnPush));
                            }
                        }
                    }
                }
            }

            // Capturas diagonales normales y al paso
            int[] captureCols = { col - 1, col + 1 };
            foreach (int nc in captureCols)
            {
                if (nc < 0 || nc >= 8) continue;
                int targetSq = SquareHelper.ToIndex(oneStepRow, nc);
                Piece targetPiece = board.GetPiece(targetSq);

                if (!targetPiece.IsEmpty && targetPiece.Color != side)
                {
                    if (oneStepRow == promoRow)
                    {
                        foreach (PieceType promo in PromotionOptions)
                        {
                            moves.Add(new ChessMove(from, targetSq, MoveSpecial.Promotion, promo, targetPiece));
                        }
                    }
                    else
                    {
                        moves.Add(new ChessMove(from, targetSq, MoveSpecial.Normal, PieceType.None, targetPiece));
                    }
                }
                else if (targetSq == board.EnPassantSquare)
                {
                    Piece epCaptured = new(PieceType.Pawn, SquareHelper.Opponent(side));
                    moves.Add(new ChessMove(from, targetSq, MoveSpecial.EnPassant, PieceType.None, epCaptured));
                }
            }
        }
    }

    private static void AddStepMoves(
        ChessBoard board,
        int from,
        int row,
        int col,
        PieceColor side,
        (int dr, int dc)[] offsets,
        bool capturesOnly,
        List<ChessMove> moves)
    {
        foreach (var (dr, dc) in offsets)
        {
            int nr = row + dr;
            int nc = col + dc;
            if (!SquareHelper.IsValid(nr, nc)) continue;

            int to = SquareHelper.ToIndex(nr, nc);
            Piece target = board.GetPiece(to);

            if (target.IsEmpty)
            {
                if (!capturesOnly)
                {
                    moves.Add(new ChessMove(from, to));
                }
            }
            else if (target.Color != side)
            {
                moves.Add(new ChessMove(from, to, MoveSpecial.Normal, PieceType.None, target));
            }
        }
    }

    private static void AddSlidingMoves(
        ChessBoard board,
        int from,
        int row,
        int col,
        PieceColor side,
        (int dr, int dc)[] dirs,
        bool capturesOnly,
        List<ChessMove> moves)
    {
        foreach (var (dr, dc) in dirs)
        {
            int nr = row + dr;
            int nc = col + dc;
            while (SquareHelper.IsValid(nr, nc))
            {
                int to = SquareHelper.ToIndex(nr, nc);
                Piece target = board.GetPiece(to);

                if (target.IsEmpty)
                {
                    if (!capturesOnly)
                    {
                        moves.Add(new ChessMove(from, to));
                    }
                }
                else
                {
                    if (target.Color != side)
                    {
                        moves.Add(new ChessMove(from, to, MoveSpecial.Normal, PieceType.None, target));
                    }
                    break;
                }

                nr += dr;
                nc += dc;
            }
        }
    }

    private static void AddCastlingMoves(ChessBoard board, PieceColor side, List<ChessMove> moves)
    {
        PieceColor opponent = SquareHelper.Opponent(side);
        int row = side == PieceColor.White ? 7 : 0;
        int kingSq = SquareHelper.ToIndex(row, 4);

        if (board.GetKingSquare(side) != kingSq) return;
        if (IsSquareAttacked(board, kingSq, opponent)) return;

        // Enroque corto (flanco de rey)
        CastlingRights kingSideFlag = side == PieceColor.White
            ? CastlingRights.WhiteKingSide
            : CastlingRights.BlackKingSide;
        if ((board.Castling & kingSideFlag) != 0)
        {
            int fSq = SquareHelper.ToIndex(row, 5);
            int gSq = SquareHelper.ToIndex(row, 6);
            int rookSq = SquareHelper.ToIndex(row, 7);
            Piece rook = board.GetPiece(rookSq);

            if (rook.Type == PieceType.Rook && rook.Color == side &&
                board.GetPiece(fSq).IsEmpty && board.GetPiece(gSq).IsEmpty &&
                !IsSquareAttacked(board, fSq, opponent) &&
                !IsSquareAttacked(board, gSq, opponent))
            {
                moves.Add(new ChessMove(kingSq, gSq, MoveSpecial.CastleKingSide));
            }
        }

        // Enroque largo (flanco de dama)
        CastlingRights queenSideFlag = side == PieceColor.White
            ? CastlingRights.WhiteQueenSide
            : CastlingRights.BlackQueenSide;
        if ((board.Castling & queenSideFlag) != 0)
        {
            int bSq = SquareHelper.ToIndex(row, 1);
            int cSq = SquareHelper.ToIndex(row, 2);
            int dSq = SquareHelper.ToIndex(row, 3);
            int rookSq = SquareHelper.ToIndex(row, 0);
            Piece rook = board.GetPiece(rookSq);

            if (rook.Type == PieceType.Rook && rook.Color == side &&
                board.GetPiece(bSq).IsEmpty &&
                board.GetPiece(cSq).IsEmpty &&
                board.GetPiece(dSq).IsEmpty &&
                !IsSquareAttacked(board, dSq, opponent) &&
                !IsSquareAttacked(board, cSq, opponent))
            {
                moves.Add(new ChessMove(kingSq, cSq, MoveSpecial.CastleQueenSide));
            }
        }
    }
}
