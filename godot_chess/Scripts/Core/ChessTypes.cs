/*
 * Archivo: Scripts/Core/ChessTypes.cs
 * Proposito:
 *   Define los tipos de datos fundamentales, enumeraciones y estructuras inmutables
 *   que representan piezas, colores, derechos de enroque, jugadas y estado de la partida.
 *   Al no depender de nodos de Godot, este modulo puede ejecutarse en hilos secundarios
 *   para calcular jugadas de la IA sin bloquear la interfaz movil.
 */

using System;

namespace AjedrezGodot.Core;

/// <summary>
/// Tipo de pieza de ajedrez. None representa una casilla vacia.
/// </summary>
public enum PieceType : byte
{
    None = 0,
    Pawn = 1,
    Knight = 2,
    Bishop = 3,
    Rook = 4,
    Queen = 5,
    King = 6
}

/// <summary>
/// Bando de la pieza o jugador en turno.
/// </summary>
public enum PieceColor : byte
{
    None = 0,
    White = 1,
    Black = 2
}

/// <summary>
/// Banderas bit a bit para identificar los derechos de enroque disponibles.
/// </summary>
[Flags]
public enum CastlingRights : byte
{
    None = 0,
    WhiteKingSide = 1 << 0,
    WhiteQueenSide = 1 << 1,
    BlackKingSide = 1 << 2,
    BlackQueenSide = 1 << 3,
    All = WhiteKingSide | WhiteQueenSide | BlackKingSide | BlackQueenSide
}

/// <summary>
/// Tipo especial de movimiento para aplicar reglas especiales con exactitud.
/// </summary>
public enum MoveSpecial : byte
{
    Normal = 0,
    DoublePawnPush = 1,
    EnPassant = 2,
    CastleKingSide = 3,
    CastleQueenSide = 4,
    Promotion = 5
}

/// <summary>
/// Resultado o situacion actual de la partida de ajedrez.
/// </summary>
public enum GameStatus : byte
{
    Playing = 0,
    Check = 1,
    Checkmate = 2,
    Stalemate = 3,
    DrawFiftyMove = 4,
    DrawInsufficientMaterial = 5,
    DrawThreefoldRepetition = 6
}

/// <summary>
/// Niveles de dificultad de la IA local (CPU).
/// </summary>
public enum AIDifficulty : byte
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}

/// <summary>
/// Estructura ligera que representa una pieza en una casilla.
/// </summary>
public readonly struct Piece : IEquatable<Piece>
{
    public readonly PieceType Type;
    public readonly PieceColor Color;

    public static readonly Piece Empty = new(PieceType.None, PieceColor.None);

    public Piece(PieceType type, PieceColor color)
    {
        Type = type;
        Color = color;
    }

    public bool IsEmpty => Type == PieceType.None || Color == PieceColor.None;

    public string GetAssetName()
    {
        if (IsEmpty) return string.Empty;
        string prefix = Color == PieceColor.White ? "w_" : "b_";
        string suffix = Type switch
        {
            PieceType.King => "king",
            PieceType.Queen => "queen",
            PieceType.Rook => "rook",
            PieceType.Bishop => "bishop",
            PieceType.Knight => "knight",
            PieceType.Pawn => "pawn",
            _ => string.Empty
        };
        return $"{prefix}{suffix}.svg";
    }

    public bool Equals(Piece other) => Type == other.Type && Color == other.Color;
    public override bool Equals(object? obj) => obj is Piece other && Equals(other);
    public override int GetHashCode() => ((int)Type << 4) | (int)Color;
    public static bool operator ==(Piece left, Piece right) => left.Equals(right);
    public static bool operator !=(Piece left, Piece right) => !left.Equals(right);
}

/// <summary>
/// Representa un movimiento completo entre dos casillas (indices 0..63).
/// </summary>
public readonly struct ChessMove : IEquatable<ChessMove>
{
    public readonly int From;
    public readonly int To;
    public readonly MoveSpecial Special;
    public readonly PieceType PromotionPiece;
    public readonly Piece CapturedPiece;

    public ChessMove(
        int from,
        int to,
        MoveSpecial special = MoveSpecial.Normal,
        PieceType promotionPiece = PieceType.None,
        Piece capturedPiece = default)
    {
        From = from;
        To = to;
        Special = special;
        PromotionPiece = promotionPiece;
        CapturedPiece = capturedPiece;
    }

    public bool IsCapture => !CapturedPiece.IsEmpty || Special == MoveSpecial.EnPassant;
    public bool IsPromotion => Special == MoveSpecial.Promotion;

    public bool Equals(ChessMove other) =>
        From == other.From &&
        To == other.To &&
        Special == other.Special &&
        PromotionPiece == other.PromotionPiece;

    public override bool Equals(object? obj) => obj is ChessMove other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(From, To, (int)Special, (int)PromotionPiece);
    public static bool operator ==(ChessMove left, ChessMove right) => left.Equals(right);
    public static bool operator !=(ChessMove left, ChessMove right) => !left.Equals(right);
}

/// <summary>
/// Guarda la instantanea de estado necesaria para deshacer una jugada exactamente (UndoMove),
/// tanto durante la busqueda de la IA como cuando el usuario pulsa "Deshacer".
/// </summary>
public readonly struct UndoState
{
    public readonly ChessMove Move;
    public readonly Piece MovedPiece;
    public readonly Piece CapturedPiece;
    public readonly CastlingRights PreviousCastling;
    public readonly int PreviousEnPassantSquare;
    public readonly int PreviousHalfmoveClock;
    public readonly ulong PreviousPositionHash;

    public UndoState(
        ChessMove move,
        Piece movedPiece,
        Piece capturedPiece,
        CastlingRights previousCastling,
        int previousEnPassantSquare,
        int previousHalfmoveClock,
        ulong previousPositionHash)
    {
        Move = move;
        MovedPiece = movedPiece;
        CapturedPiece = capturedPiece;
        PreviousCastling = previousCastling;
        PreviousEnPassantSquare = previousEnPassantSquare;
        PreviousHalfmoveClock = previousHalfmoveClock;
        PreviousPositionHash = previousPositionHash;
    }
}

/// <summary>
/// Utilidades estaticas de coordenadas de tablero (0..63 <-> fila, columna y notacion algebraica).
/// </summary>
public static class SquareHelper
{
    public static int ToIndex(int row, int col) => (row * 8) + col;
    public static int Row(int index) => index >> 3;
    public static int Col(int index) => index & 7;
    public static bool IsValid(int row, int col) => row >= 0 && row < 8 && col >= 0 && col < 8;

    public static PieceColor Opponent(PieceColor color) =>
        color == PieceColor.White ? PieceColor.Black : PieceColor.White;

    public static string ToAlgebraic(int index)
    {
        if (index < 0 || index >= 64) return "-";
        char file = (char)('a' + Col(index));
        char rank = (char)('8' - Row(index));
        return $"{file}{rank}";
    }
}
