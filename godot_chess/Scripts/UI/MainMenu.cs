/*
 * Archivo: Scripts/UI/MainMenu.cs
 * Proposito:
 *   Controlador de la pantalla principal de inicio (MainMenu.tscn).
 *   Permite al jugador elegir el color de sus piezas, seleccionar la dificultad
 *   de la IA local (CPU), activar ayudas visuales, abrir el popup de reglas
 *   y navegar hacia la pantalla del tablero de juego.
 */

using Godot;
using AjedrezGodot.Core;

namespace AjedrezGodot.UI;

public partial class MainMenu : Control
{
    private Button _btnWhite = null!;
    private Button _btnRandom = null!;
    private Button _btnBlack = null!;

    private Button _btnEasy = null!;
    private Button _btnMedium = null!;
    private Button _btnHard = null!;
    private Label _lblDifficultyDesc = null!;

    private CheckButton _chkHints = null!;
    private CheckButton _chkCoords = null!;

    private Button _btnPlay = null!;
    private Button _btnRules = null!;
    private RulesPopup _rulesPopup = null!;

    public override void _Ready()
    {
        _btnWhite = GetNode<Button>("%BtnWhite");
        _btnRandom = GetNode<Button>("%BtnRandom");
        _btnBlack = GetNode<Button>("%BtnBlack");

        _btnEasy = GetNode<Button>("%BtnEasy");
        _btnMedium = GetNode<Button>("%BtnMedium");
        _btnHard = GetNode<Button>("%BtnHard");
        _lblDifficultyDesc = GetNode<Label>("%LblDifficultyDesc");

        _chkHints = GetNode<CheckButton>("%ChkHints");
        _chkCoords = GetNode<CheckButton>("%ChkCoords");

        _btnPlay = GetNode<Button>("%BtnPlay");
        _btnRules = GetNode<Button>("%BtnRules");
        _rulesPopup = GetNode<RulesPopup>("%RulesPopup");

        _btnWhite.Pressed += () => SelectColor(PlayerColorPreference.White);
        _btnRandom.Pressed += () => SelectColor(PlayerColorPreference.Random);
        _btnBlack.Pressed += () => SelectColor(PlayerColorPreference.Black);

        _btnEasy.Pressed += () => SelectDifficulty(AIDifficulty.Easy);
        _btnMedium.Pressed += () => SelectDifficulty(AIDifficulty.Medium);
        _btnHard.Pressed += () => SelectDifficulty(AIDifficulty.Hard);

        _chkHints.Toggled += pressed => GameConfig.Instance.ShowLegalHints = pressed;
        _chkCoords.Toggled += pressed => GameConfig.Instance.ShowCoordinates = pressed;

        _btnPlay.Pressed += OnPlayPressed;
        _btnRules.Pressed += () => _rulesPopup.ShowPopup();

        SyncUiFromConfig();
    }

    private void SelectColor(PlayerColorPreference pref)
    {
        GameConfig.Instance.ColorPreference = pref;
        SyncUiFromConfig();
    }

    private void SelectDifficulty(AIDifficulty difficulty)
    {
        GameConfig.Instance.Difficulty = difficulty;
        SyncUiFromConfig();
    }

    private void SyncUiFromConfig()
    {
        var cfg = GameConfig.Instance;

        _btnWhite.ButtonPressed = cfg.ColorPreference == PlayerColorPreference.White;
        _btnRandom.ButtonPressed = cfg.ColorPreference == PlayerColorPreference.Random;
        _btnBlack.ButtonPressed = cfg.ColorPreference == PlayerColorPreference.Black;

        _btnEasy.ButtonPressed = cfg.Difficulty == AIDifficulty.Easy;
        _btnMedium.ButtonPressed = cfg.Difficulty == AIDifficulty.Medium;
        _btnHard.ButtonPressed = cfg.Difficulty == AIDifficulty.Hard;

        _lblDifficultyDesc.Text = cfg.Difficulty switch
        {
            AIDifficulty.Easy => "CPU relajada (profundidad 2): ideal para practicar aperturas.",
            AIDifficulty.Medium => "CPU equilibrada (profundidad 3): responde tacticas basicas.",
            AIDifficulty.Hard => "CPU estrategica (profundidad 4 + quietud): juego solido.",
            _ => string.Empty
        };

        _chkHints.ButtonPressed = cfg.ShowLegalHints;
        _chkCoords.ButtonPressed = cfg.ShowCoordinates;
    }

    private void OnPlayPressed()
    {
        GameConfig.Instance.ResolvePlayerColorForMatch();
        GetTree().ChangeSceneToFile("res://Scenes/GameScreen.tscn");
    }
}
