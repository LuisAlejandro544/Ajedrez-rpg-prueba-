/*
 * Archivo: Scripts/UI/GameOverPopup.cs
 * Proposito:
 *   Popup modal de fin de partida. Presenta el resultado final (Victoria, Derrota o Tablas),
 *   el motivo exacto segun las reglas (Jaque mate, Ahogado, 50 jugadas, Material insuficiente
 *   o Triple repeticion) y ofrece botones para revancha inmediata o volver al menu principal.
 */

using Godot;

namespace AjedrezGodot.UI;

public partial class GameOverPopup : Control
{
    [Signal]
    public delegate void PlayAgainRequestedEventHandler();

    [Signal]
    public delegate void MainMenuRequestedEventHandler();

    private Label _lblTitle = null!;
    private Label _lblReason = null!;
    private Label _lblStats = null!;
    private Button _btnRematch = null!;
    private Button _btnMenu = null!;

    public override void _Ready()
    {
        _lblTitle = GetNode<Label>("%LblGameOverTitle");
        _lblReason = GetNode<Label>("%LblGameOverReason");
        _lblStats = GetNode<Label>("%LblGameOverStats");
        _btnRematch = GetNode<Button>("%BtnRematch");
        _btnMenu = GetNode<Button>("%BtnGameOverMenu");

        _btnRematch.Pressed += () =>
        {
            Visible = false;
            EmitSignal(SignalName.PlayAgainRequested);
        };

        _btnMenu.Pressed += () =>
        {
            Visible = false;
            EmitSignal(SignalName.MainMenuRequested);
        };

        Visible = false;
    }

    public void ShowResult(string title, string reason, int totalPlies)
    {
        _lblTitle.Text = title;
        _lblReason.Text = reason;
        int fullMoves = (totalPlies + 1) / 2;
        _lblStats.Text = $"Jugadas realizadas: {fullMoves}";
        Visible = true;
    }
}
