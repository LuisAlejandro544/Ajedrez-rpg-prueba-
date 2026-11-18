/*
 * Archivo: Scripts/UI/PromotionPopup.cs
 * Proposito:
 *   Popup modal que aparece cuando un peon del jugador alcanza la octava fila.
 *   Muestra las 4 opciones reglamentarias de coronacion (Dama, Torre, Alfil, Caballo)
 *   usando sus iconos vectoriales SVG y notifica la eleccion mediante una senal de Godot.
 */

using Godot;
using AjedrezGodot.Core;

namespace AjedrezGodot.UI;

public partial class PromotionPopup : Control
{
    [Signal]
    public delegate void PieceChosenEventHandler(int pieceTypeValue);

    private Button _btnQueen = null!;
    private Button _btnRook = null!;
    private Button _btnBishop = null!;
    private Button _btnKnight = null!;

    private TextureRect _icoQueen = null!;
    private TextureRect _icoRook = null!;
    private TextureRect _icoBishop = null!;
    private TextureRect _icoKnight = null!;

    public override void _Ready()
    {
        _btnQueen = GetNode<Button>("%BtnPromoQueen");
        _btnRook = GetNode<Button>("%BtnPromoRook");
        _btnBishop = GetNode<Button>("%BtnPromoBishop");
        _btnKnight = GetNode<Button>("%BtnPromoKnight");

        _icoQueen = GetNode<TextureRect>("%IcoPromoQueen");
        _icoRook = GetNode<TextureRect>("%IcoPromoRook");
        _icoBishop = GetNode<TextureRect>("%IcoPromoBishop");
        _icoKnight = GetNode<TextureRect>("%IcoPromoKnight");

        _btnQueen.Pressed += () => Choose(PieceType.Queen);
        _btnRook.Pressed += () => Choose(PieceType.Rook);
        _btnBishop.Pressed += () => Choose(PieceType.Bishop);
        _btnKnight.Pressed += () => Choose(PieceType.Knight);

        Visible = false;
    }

    public void OpenForColor(PieceColor color)
    {
        string prefix = color == PieceColor.White ? "w_" : "b_";
        _icoQueen.Texture = GD.Load<Texture2D>($"res://Assets/Pieces/{prefix}queen.svg");
        _icoRook.Texture = GD.Load<Texture2D>($"res://Assets/Pieces/{prefix}rook.svg");
        _icoBishop.Texture = GD.Load<Texture2D>($"res://Assets/Pieces/{prefix}bishop.svg");
        _icoKnight.Texture = GD.Load<Texture2D>($"res://Assets/Pieces/{prefix}knight.svg");
        Visible = true;
    }

    private void Choose(PieceType type)
    {
        Visible = false;
        EmitSignal(SignalName.PieceChosen, (int)type);
    }
}
