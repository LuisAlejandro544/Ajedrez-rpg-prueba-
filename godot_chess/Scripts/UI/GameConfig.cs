/*
 * Archivo: Scripts/UI/GameConfig.cs
 * Proposito:
 *   Singleton global (Autoload) que conserva la configuracion elegida por el jugador
 *   al navegar entre las distintas pantallas (Menu Principal -> Pantalla de Partida):
 *   bando seleccionado (Blancas, Negras o Aleatorio), nivel de dificultad de la CPU
 *   y preferencias visuales del tablero.
 */

using System;
using Godot;
using AjedrezGodot.Core;

namespace AjedrezGodot.UI;

public enum PlayerColorPreference
{
    White = 0,
    Black = 1,
    Random = 2
}

public partial class GameConfig : Node
{
    public static GameConfig Instance { get; private set; } = null!;

    public PlayerColorPreference ColorPreference { get; set; } = PlayerColorPreference.White;
    public AIDifficulty Difficulty { get; set; } = AIDifficulty.Medium;
    public bool ShowLegalHints { get; set; } = true;
    public bool ShowCoordinates { get; set; } = true;

    public PieceColor ResolvedPlayerColor { get; private set; } = PieceColor.White;

    public override void _EnterTree()
    {
        Instance = this;
    }

    /// <summary>
    /// Resuelve el color real del jugador humano para la nueva partida.
    /// </summary>
    public PieceColor ResolvePlayerColorForMatch()
    {
        ResolvedPlayerColor = ColorPreference switch
        {
            PlayerColorPreference.White => PieceColor.White,
            PlayerColorPreference.Black => PieceColor.Black,
            PlayerColorPreference.Random => Random.Shared.Next(2) == 0 ? PieceColor.White : PieceColor.Black,
            _ => PieceColor.White
        };
        return ResolvedPlayerColor;
    }

    public string GetDifficultyLabel() => Difficulty switch
    {
        AIDifficulty.Easy => "Facil",
        AIDifficulty.Medium => "Medio",
        AIDifficulty.Hard => "Dificil",
        _ => "Medio"
    };
}
