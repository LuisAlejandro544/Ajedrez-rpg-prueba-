/*
 * Archivo: Scripts/Core/ChessBoard.cs
 * Proposito:
 *   Gestiona el estado interno del tablero de 64 casillas, la posicion inicial estandar,
 *   la ejecucion y reversion de jugadas (MakeMove / UndoMove), el historial para detectar
 *   triple repeticion, la regla de 50 movimientos y el calculo de hash Zobrist determinista.
 */

using System;
using System.Collections.Generic;

namespace AjedrezGodot.Core;

public sealed class ChessBoard
{
    public const int BoardSize = 64;

    private readonly Piece[] _squares = new Piece[BoardSize];
    private readonly Stack<UndoState> _history = new();
    private readonly Dictionary<ulong, int> _positionCounts = new();

    public PieceColor SideToMove { get; private set; } = PieceColor.White;
    public CastlingRights Castling { get; private set; } = CastlingRights.All;
    public int EnPassantSquare { get; private set; } = -1;
    public int HalfmoveClock { get; private set; }
    public int FullmoveNumber { get; private set; } = 1;
    public ulong PositionHash { get; private set; }

    public int WhiteKingSquare { get; private set; } = -1;
    public int BlackKingSquare { get; private set; } = -1;

    public int PlyCount => _history.Count;

    // Tabla Zobrist determinista para identificar posiciones repetidas rapidamente.
    private static readonly ulong[,] ZobristPieces = new ulong[13, 64];
    private static readonly ulong[] ZobristCastling = new ulong[16];
    private static readonly ulong[] ZobristEnPassant = new ulong[8];
    private static readonly ulong ZobristBlackToMove;

    static ChessBoard()
    {
        ulong seed = 0x9E3779B97F4A7C15UL;
        static ulong NextRand(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        for (int p = 0; p < 13; p++)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                ZobristPieces[p, sq] = NextRand(ref seed);
            }
        }
        for (int c = 0; c < 16; c++)
        {
            ZobristCastling[c] = NextRand(ref seed);
        }
        for (int f = 0; f < 8; f++)
        {
            ZobristEnPassant[f] = NextRand(ref seed);
        }
        ZobristBlackToMove = NextRand(ref seed);
    }

    public ChessBoard()
    {
        SetupStandardPosition();
    }

    private ChessBoard(bool skipSetup)
    {
        if (!skipSetup)
        {
            SetupStandardPosition();
        }
    }

    /// <summary>
    /// Crea una copia independiente del tablero actual (ideal para ejecutar la IA en otro hilo).
    /// </summary>
    public ChessBoard Clone()
    {
        var copy = new ChessBoard(skipSetup: true);
        Array.Copy(_squares, copy._squares, BoardSize);
        copy.SideToMove = SideToMove;
        copy.Castling = Castling;
        copy.EnPassantSquare = EnPassantSquare;
        copy.HalfmoveClock = HalfmoveClock;
        copy.FullmoveNumber = FullmoveNumber;
        copy.PositionHash = PositionHash;
        copy.WhiteKingSquare = WhiteKingSquare;
        copy.BlackKingSquare = BlackKingSquare;

        foreach (var kv in _positionCounts)
        {
            copy._positionCounts[kv.Key] = kv.Value;
        }

        // Copiamos el historial en orden correcto.
        var arr = _history.ToArray();
        for (int i = arr.Length - 1; i >= 0; i--)
        {
            copy._history.Push(arr[i]);
        }

        return copy;
    }

    public Piece GetPiece(int index) => _squares[index];

    public Piece GetPiece(int row, int col) => _squares[(row * 8) + col];

    public int GetKingSquare(PieceColor color) =>
        color == PieceColor.White ? WhiteKingSquare : BlackKingSquare;

    /// <summary>
    /// Reinicia el tablero con la formacion clasica de ajedrez.
    /// </summary>
    public void SetupStandardPosition()
    {
        Array.Fill(_squares, Piece.Empty);
        _history.Clear();
        _positionCounts.Clear();

        PieceType[] backRank =
        {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
        };

        for (int col = 0; col < 8; col++)
        {
            _squares[SquareHelper.ToIndex(0, col)] = new Piece(backRank[col], PieceColor.Black);
            _squares[SquareHelper.ToIndex(1, col)] = new Piece(PieceType.Pawn, PieceColor.Black);
            _squares[SquareHelper.ToIndex(6, col)] = new Piece(PieceType.Pawn, PieceColor.White);
            _squares[SquareHelper.ToIndex(7, col)] = new Piece(backRank[col], PieceColor.White);
        }

        BlackKingSquare = SquareHelper.ToIndex(0, 4);
        WhiteKingSquare = SquareHelper.ToIndex(7, 4);
        SideToMove = PieceColor.White;
        Castling = CastlingRights.All;
        EnPassantSquare = -1;
        HalfmoveClock = 0;
        FullmoveNumber = 1;

        PositionHash = ComputeFullHash();
        _positionCounts[PositionHash] = 1;
    }

    /// <summary>
    /// Aplica una jugada al tablero actualizando derechos de enroque, captura al paso,
    /// promocion, contadores de tablas y hash de posicion.
    /// </summary>
    public void MakeMove(ChessMove move)
    {
        Piece moving = _squares[move.From];
        Piece target = _squares[move.To];

        var undo = new UndoState(
            move,
            moving,
            target,
            Castling,
            EnPassantSquare,
            HalfmoveClock,
            PositionHash);
        _history.Push(undo);

        // Actualizar casilla origen
        _squares[move.From] = Piece.Empty;

        // Manejar captura al paso (En Passant)
        if (move.Special == MoveSpecial.EnPassant)
        {
            int capturedPawnRow = SquareHelper.Row(move.From);
            int capturedPawnCol = SquareHelper.Col(move.To);
            _squares[SquareHelper.ToIndex(capturedPawnRow, capturedPawnCol)] = Piece.Empty;
        }

        // Colocar pieza en destino (promocion o pieza original)
        Piece placed = move.Special == MoveSpecial.Promotion
            ? new Piece(move.PromotionPiece, moving.Color)
            : moving;
        _squares[move.To] = placed;

        // Actualizar posicion cacheada del Rey
        if (moving.Type == PieceType.King)
        {
            if (moving.Color == PieceColor.White) WhiteKingSquare = move.To;
            else BlackKingSquare = move.To;
        }

        // Mover la torre si es enroque
        if (move.Special == MoveSpecial.CastleKingSide)
        {
            int row = SquareHelper.Row(move.From);
            int rookFrom = SquareHelper.ToIndex(row, 7);
            int rookTo = SquareHelper.ToIndex(row, 5);
            _squares[rookTo] = _squares[rookFrom];
            _squares[rookFrom] = Piece.Empty;
        }
        else if (move.Special == MoveSpecial.CastleQueenSide)
        {
            int row = SquareHelper.Row(move.From);
            int rookFrom = SquareHelper.ToIndex(row, 0);
            int rookTo = SquareHelper.ToIndex(row, 3);
            _squares[rookTo] = _squares[rookFrom];
            _squares[rookFrom] = Piece.Empty;
        }

        // Actualizar derechos de enroque si rey o torre se mueven o son capturados
        Castling = UpdateCastlingRights(Castling, move.From, move.To);

        // Actualizar casilla de captura al paso
        if (move.Special == MoveSpecial.DoublePawnPush)
        {
            int midRow = (SquareHelper.Row(move.From) + SquareHelper.Row(move.To)) / 2;
            EnPassantSquare = SquareHelper.ToIndex(midRow, SquareHelper.Col(move.From));
        }
        else
        {
            EnPassantSquare = -1;
        }

        // Reloj de 50 jugadas: se reinicia si mueve peon o hay captura
        if (moving.Type == PieceType.Pawn || !target.IsEmpty || move.Special == MoveSpecial.EnPassant)
        {
            HalfmoveClock = 0;
        }
        else
        {
            HalfmoveClock++;
        }

        if (SideToMove == PieceColor.Black)
        {
            FullmoveNumber++;
        }

        SideToMove = SquareHelper.Opponent(SideToMove);
        PositionHash = ComputeFullHash();

        _positionCounts.TryGetValue(PositionHash, out int count);
        _positionCounts[PositionHash] = count + 1;
    }

    /// <summary>
    /// Revierte exactamente la ultima jugada realizada, restaurando tablero y banderas.
    /// </summary>
    public bool UndoMove()
    {
        if (_history.Count == 0) return false;

        if (_positionCounts.TryGetValue(PositionHash, out int currentCount))
        {
            if (currentCount <= 1) _positionCounts.Remove(PositionHash);
            else _positionCounts[PositionHash] = currentCount - 1;
        }

        UndoState undo = _history.Pop();
        ChessMove move = undo.Move;

        SideToMove = SquareHelper.Opponent(SideToMove);
        if (SideToMove == PieceColor.Black)
        {
            FullmoveNumber--;
        }

        Castling = undo.PreviousCastling;
        EnPassantSquare = undo.PreviousEnPassantSquare;
        HalfmoveClock = undo.PreviousHalfmoveClock;
        PositionHash = undo.PreviousPositionHash;

        // Restaurar pieza movida a origen y captura a destino
        _squares[move.From] = undo.MovedPiece;
        _squares[move.To] = undo.CapturedPiece;

        if (undo.MovedPiece.Type == PieceType.King)
        {
            if (undo.MovedPiece.Color == PieceColor.White) WhiteKingSquare = move.From;
            else BlackKingSquare = move.From;
        }

        // Restaurar peon capturado al paso
        if (move.Special == MoveSpecial.EnPassant)
        {
            int capturedRow = SquareHelper.Row(move.From);
            int capturedCol = SquareHelper.Col(move.To);
            _squares[SquareHelper.ToIndex(capturedRow, capturedCol)] =
                new Piece(PieceType.Pawn, SquareHelper.Opponent(undo.MovedPiece.Color));
        }

        // Devolver torre si fue enroque
        if (move.Special == MoveSpecial.CastleKingSide)
        {
            int row = SquareHelper.Row(move.From);
            int rookFrom = SquareHelper.ToIndex(row, 7);
            int rookTo = SquareHelper.ToIndex(row, 5);
            _squares[rookFrom] = _squares[rookTo];
            _squares[rookTo] = Piece.Empty;
        }
        else if (move.Special == MoveSpecial.CastleQueenSide)
        {
            int row = SquareHelper.Row(move.From);
            int rookFrom = SquareHelper.ToIndex(row, 0);
            int rookTo = SquareHelper.ToIndex(row, 3);
            _squares[rookFrom] = _squares[rookTo];
            _squares[rookTo] = Piece.Empty;
        }

        return true;
    }

    /// <summary>
    /// Indica si la posicion actual ya se ha repetido 3 o mas veces.
    /// </summary>
    public bool IsThreefoldRepetition()
    {
        return _positionCounts.TryGetValue(PositionHash, out int count) && count >= 3;
    }

    /// <summary>
    /// Verifica si queda suficiente material en el tablero para dar jaque mate.
    /// </summary>
    public bool HasInsufficientMaterial()
    {
        int whiteKnights = 0, blackKnights = 0;
        int whiteBishops = 0, blackBishops = 0;
        int whiteBishopColor = -1, blackBishopColor = -1;

        for (int sq = 0; sq < BoardSize; sq++)
        {
            Piece p = _squares[sq];
            if (p.IsEmpty || p.Type == PieceType.King) continue;

            if (p.Type == PieceType.Pawn || p.Type == PieceType.Rook || p.Type == PieceType.Queen)
            {
                return false;
            }

            if (p.Type == PieceType.Knight)
            {
                if (p.Color == PieceColor.White) whiteKnights++;
                else blackKnights++;
            }
            else if (p.Type == PieceType.Bishop)
            {
                int squareColor = (SquareHelper.Row(sq) + SquareHelper.Col(sq)) & 1;
                if (p.Color == PieceColor.White)
                {
                    whiteBishops++;
                    whiteBishopColor = squareColor;
                }
                else
                {
                    blackBishops++;
                    blackBishopColor = squareColor;
                }
            }
        }

        int totalMinor = whiteKnights + blackKnights + whiteBishops + blackBishops;
        if (totalMinor == 0) return true; // Rey vs Rey
        if (totalMinor == 1) return true; // Rey + Caballo/Alfil vs Rey

        // Rey + Alfil vs Rey + Alfil en casillas del mismo color
        if (whiteKnights == 0 && blackKnights == 0 &&
            whiteBishops == 1 && blackBishops == 1 &&
            whiteBishopColor == blackBishopColor)
        {
            return true;
        }

        return false;
    }

    private static CastlingRights UpdateCastlingRights(CastlingRights rights, int from, int to)
    {
        // Si mueve o es capturada una torre o rey en su casilla inicial, revocar permiso
        if (from == 60 || to == 60) rights &= ~(CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide);
        if (from == 4 || to == 4) rights &= ~(CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide);
        if (from == 63 || to == 63) rights &= ~CastlingRights.WhiteKingSide;
        if (from == 56 || to == 56) rights &= ~CastlingRights.WhiteQueenSide;
        if (from == 7 || to == 7) rights &= ~CastlingRights.BlackKingSide;
        if (from == 0 || to == 0) rights &= ~CastlingRights.BlackQueenSide;
        return rights;
    }

    private ulong ComputeFullHash()
    {
        ulong hash = 0;
        for (int sq = 0; sq < BoardSize; sq++)
        {
            Piece p = _squares[sq];
            if (!p.IsEmpty)
            {
                int pieceIdx = (int)p.Type + (p.Color == PieceColor.Black ? 6 : 0);
                hash ^= ZobristPieces[pieceIdx, sq];
            }
        }

        hash ^= ZobristCastling[(int)Castling];
        if (EnPassantSquare >= 0)
        {
            hash ^= ZobristEnPassant[SquareHelper.Col(EnPassantSquare)];
        }
        if (SideToMove == PieceColor.Black)
        {
            hash ^= ZobristBlackToMove;
        }
        return hash;
    }
}
