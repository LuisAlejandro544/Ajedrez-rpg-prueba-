/*
 * Archivo: Scripts/UI/RulesPopup.cs
 * Proposito:
 *   Popup informativo reutilizable tanto en el Menu Principal como durante la partida.
 *   Resume las reglas especiales del ajedrez implementadas en el motor (enroque,
 *   captura al paso, coronacion, jaque mate y tipos de tablas).
 */

using Godot;

namespace AjedrezGodot.UI;

public partial class RulesPopup : Control
{
    private Button _btnClose = null!;

    public override void _Ready()
    {
        _btnClose = GetNode<Button>("%BtnCloseRules");
        _btnClose.Pressed += () => Visible = false;
        Visible = false;
    }

    public void ShowPopup()
    {
        Visible = true;
    }
}
