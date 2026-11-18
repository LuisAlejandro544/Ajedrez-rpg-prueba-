/*
 * Archivo: Scripts/UI/GameScreen.cs
 * Proposito:
 *   Controlador principal de la pantalla de partida (GameScreen.tscn).
 *   Construye dinamicamente el tablero interactivo de 64 casillas, carga y cachea
 *   las 12 piezas vectoriales en formato .svg, gestiona la seleccion y validacion
 *   de jugadas legales, muestra los popups de coronacion y fin de partida, registra
 *   el historial algebraico y ejecuta el turno de la IA local en segundo plano.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using AjedrezGodot.Core;

namespace AjedrezGodot.UI;

public partial class GameScreen : Control
{
    private readonly ChessBoard _board = new();
    private readonly Dictionary<string, Texture2D> _pieceTextures = new();
    private readonly Button[] _squareButtons = new Button[64];
    private readonly TextureRect[] _pieceIcons = new TextureRect[64];
    private readonly Panel[] _moveHints = new Panel[64];
    private readonly Label[] _rankLabels = new Label[64];
    private readonly Label[] _fileLabels = new Label[64];
    private readonly int[] _displayToBoardSq = new int[64];

    private readonly List<string> _moveNotations = new();
    private readonly Stack<Piece> _capturedStack = new();

    private PieceColor _playerColor = PieceColor.White;
    private PieceColor _cpuColor = PieceColor.Black;
    private GameStatus _currentStatus = GameStatus.Playing;
    private List<ChessMove> _currentLegalMoves = new();

    private int _selectedSquare = -1;
    private int _lastMoveFrom = -1;
    private int _lastMoveTo = -1;
    private int _pendingPromoFrom = -1;
    private int _pendingPromoTo = -1;
    private bool _isCpuThinking;
    private CancellationTokenSource? _aiCts;

    // Nodos de la escena
    private GridContainer _boardGrid = null!;
    private Label _lblTurnStatus = null!;
    private Label _lblCpuName = null!;
    private Label _lblPlayerName = null!;
    private Label _lblCpuAdvantage = null!;
    private Label _lblPlayerAdvantage = null!;
    private Label _lblMoveHistory = null!;
    private HBoxContainer _cpuCapturedBox = null!;
    private HBoxContainer _playerCapturedBox = null!;
    private Button _btnMenu = null!;
    private Button _btnRestart = null!;
    private Button _btnUndo = null!;
    private Button _btnRules = null!;

    private PromotionPopup _promotionPopup = null!;
    private GameOverPopup _gameOverPopup = null!;
    private RulesPopup _rulesPopup = null!;

    // Paleta de estilos cacheados del tablero
    private StyleBoxFlat _lightNormal = null!;
    private StyleBoxFlat _darkNormal = null!;
    private StyleBoxFlat _selectedStyle = null!;
    private StyleBoxFlat _lastMoveLight = null!;
    private StyleBoxFlat _lastMoveDark = null!;
    private StyleBoxFlat _checkStyle = null!;
    private StyleBoxFlat _dotHintStyle = null!;
    private StyleBoxFlat _captureHintStyle = null!;

    public override void _Ready()
    {
        BindSceneNodes();
        CreateBoardStyles();
        PreloadSvgPieces();
        BuildBoardGrid();

        _btnMenu.Pressed += OnMenuPressed;
        _btnRestart.Pressed += StartNewMatch;
        _btnUndo.Pressed += OnUndoPressed;
        _btnRules.Pressed += () => _rulesPopup.ShowPopup();

        _promotionPopup.PieceChosen += OnPromotionPieceChosen;
        _gameOverPopup.PlayAgainRequested += StartNewMatch;
        _gameOverPopup.MainMenuRequested += OnMenuPressed;

        StartNewMatch();
    }

    public override void _ExitTree()
    {
        CancelAiSearch();
    }

    private void BindSceneNodes()
    {
        _boardGrid = GetNode<GridContainer>("%BoardGrid");
        _lblTurnStatus = GetNode<Label>("%LblTurnStatus");
        _lblCpuName = GetNode<Label>("%LblCpuName");
        _lblPlayerName = GetNode<Label>("%LblPlayerName");
        _lblCpuAdvantage = GetNode<Label>("%LblCpuAdvantage");
        _lblPlayerAdvantage = GetNode<Label>("%LblPlayerAdvantage");
        _lblMoveHistory = GetNode<Label>("%LblMoveHistory");
        _cpuCapturedBox = GetNode<HBoxContainer>("%CpuCapturedBox");
        _playerCapturedBox = GetNode<HBoxContainer>("%PlayerCapturedBox");

        _btnMenu = GetNode<Button>("%BtnMenu");
        _btnRestart = GetNode<Button>("%BtnRestart");
        _btnUndo = GetNode<Button>("%BtnUndo");
        _btnRules = GetNode<Button>("%BtnGameRules");

        _promotionPopup = GetNode<PromotionPopup>("%PromotionPopup");
        _gameOverPopup = GetNode<GameOverPopup>("%GameOverPopup");
        _rulesPopup = GetNode<RulesPopup>("%GameRulesPopup");
    }

    private void CreateBoardStyles()
    {
        static StyleBoxFlat MakeTileStyle(Color bg, Color? border = null, int borderWidth = 0)
        {
            var sb = new StyleBoxFlat
            {
                BgColor = bg,
                CornerRadiusTopLeft = 0,
                CornerRadiusTopRight = 0,
                CornerRadiusBottomLeft = 0,
                CornerRadiusBottomRight = 0
            };
            if (border.HasValue && borderWidth > 0)
            {
                sb.BorderColor = border.Value;
                sb.SetBorderWidthAll(borderWidth);
            }
            return sb;
        }

        _lightNormal = MakeTileStyle(new Color("#EDD9BE"));
        _darkNormal = MakeTileStyle(new Color("#A26E47"));
        _selectedStyle = MakeTileStyle(new Color("#E6C054"), new Color("#FFF4C2"), 3);
        _lastMoveLight = MakeTileStyle(new Color("#D8CE7E"));
        _lastMoveDark = MakeTileStyle(new Color("#B59D4B"));
        _checkStyle = MakeTileStyle(new Color("#D64545"), new Color("#FFB8B8"), 3);

        _dotHintStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.11f, 0.09f, 0.42f),
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16
        };

        _captureHintStyle = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0f),
            BorderColor = new Color(0.75f, 0.18f, 0.14f, 0.85f),
            CornerRadiusTopLeft = 28,
            CornerRadiusTopRight = 28,
            CornerRadiusBottomLeft = 28,
            CornerRadiusBottomRight = 28
        };
        _captureHintStyle.SetBorderWidthAll(5);
    }

    private void PreloadSvgPieces()
    {
        string[] names =
        {
            "w_king.svg", "w_queen.svg", "w_rook.svg", "w_bishop.svg", "w_knight.svg", "w_pawn.svg",
            "b_king.svg", "b_queen.svg", "b_rook.svg", "b_bishop.svg", "b_knight.svg", "b_pawn.svg"
        };

        foreach (string file in names)
        {
            Texture2D? tex = GD.Load<Texture2D>($"res://Assets/Pieces/{file}");
            if (tex != null)
            {
                _pieceTextures[file] = tex;
            }
        }
    }

    private void BuildBoardGrid()
    {
        foreach (Node child in _boardGrid.GetChildren())
        {
            child.QueueFree();
        }

        _boardGrid.Columns = 8;

        for (int displayIdx = 0; displayIdx < 64; displayIdx++)
        {
            int capturedDisplayIdx = displayIdx;
            var btn = new Button
            {
                FocusMode = FocusModeEnum.None,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(36, 36),
                Flat = false
            };
            btn.Pressed += () => OnDisplaySquarePressed(capturedDisplayIdx);

            // Textura vectorial SVG de la pieza
            var icon = new TextureRect
            {
                AnchorLeft = 0.08f,
                AnchorTop = 0.08f,
                AnchorRight = 0.92f,
                AnchorBottom = 0.92f,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            btn.AddChild(icon);

            // Indicador visual de jugada legal (punto o aro de captura)
            var hint = new Panel
            {
                Visible = false,
                MouseFilter = MouseFilterEnum.Ignore
            };
            btn.AddChild(hint);

            // Etiqueta de fila (1-8) en esquina superior izquierda
            var rankLbl = new Label
            {
                AnchorLeft = 0.06f,
                AnchorTop = 0.02f,
                MouseFilter = MouseFilterEnum.Ignore,
                Visible = false
            };
            rankLbl.AddThemeFontSizeOverride("font_size", 13);
            btn.AddChild(rankLbl);

            // Etiqueta de columna (a-h) en esquina inferior derecha
            var fileLbl = new Label
            {
                AnchorRight = 0.94f,
                AnchorBottom = 0.98f,
                GrowHorizontal = GrowDirection.Begin,
                GrowVertical = GrowDirection.Begin,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                MouseFilter = MouseFilterEnum.Ignore,
                Visible = false
            };
            fileLbl.AddThemeFontSizeOverride("font_size", 13);
            btn.AddChild(fileLbl);

            _boardGrid.AddChild(btn);
            _squareButtons[displayIdx] = btn;
            _pieceIcons[displayIdx] = icon;
            _moveHints[displayIdx] = hint;
            _rankLabels[displayIdx] = rankLbl;
            _fileLabels[displayIdx] = fileLbl;
        }
    }

    private void StartNewMatch()
    {
        CancelAiSearch();

        _board.SetupStandardPosition();
        _moveNotations.Clear();
        _capturedStack.Clear();

        _playerColor = GameConfig.Instance.ResolvedPlayerColor;
        _cpuColor = SquareHelper.Opponent(_playerColor);

        _selectedSquare = -1;
        _lastMoveFrom = -1;
        _lastMoveTo = -1;
        _pendingPromoFrom = -1;
        _pendingPromoTo = -1;
        _isCpuThinking = false;

        _promotionPopup.Visible = false;
        _gameOverPopup.Visible = false;

        string diffLabel = GameConfig.Instance.GetDifficultyLabel();
        _lblCpuName.Text = $"CPU ({diffLabel}) • {(_cpuColor == PieceColor.White ? "Blancas" : "Negras")}";
        _lblPlayerName.Text = $"Tu • {(_playerColor == PieceColor.White ? "Blancas" : "Negras")}";

        RebuildDisplayMapping();
        RefreshBoardState();

        if (_board.SideToMove == _cpuColor)
        {
            _ = RunCpuTurnAsync();
        }
    }

    private void RebuildDisplayMapping()
    {
        bool flipped = _playerColor == PieceColor.Black;
        bool showCoords = GameConfig.Instance.ShowCoordinates;

        for (int dispRow = 0; dispRow < 8; dispRow++)
        {
            for (int dispCol = 0; dispCol < 8; dispCol++)
            {
                int dispIdx = (dispRow * 8) + dispCol;
                int boardRow = flipped ? (7 - dispRow) : dispRow;
                int boardCol = flipped ? (7 - dispCol) : dispCol;
                int boardSq = SquareHelper.ToIndex(boardRow, boardCol);

                _displayToBoardSq[dispIdx] = boardSq;

                bool isLight = ((boardRow + boardCol) & 1) == 0;
                Color coordColor = isLight ? new Color("#7C5335") : new Color("#F2E2CC");

                Label rankLbl = _rankLabels[dispIdx];
                rankLbl.Visible = showCoords && dispCol == 0;
                rankLbl.Text = (8 - boardRow).ToString();
                rankLbl.AddThemeColorOverride("font_color", coordColor);

                Label fileLbl = _fileLabels[dispIdx];
                fileLbl.Visible = showCoords && dispRow == 7;
                fileLbl.Text = ((char)('a' + boardCol)).ToString();
                fileLbl.AddThemeColorOverride("font_color", coordColor);
            }
        }
    }

    private void OnDisplaySquarePressed(int displayIdx)
    {
        if (_isCpuThinking || IsMatchFinished() || _promotionPopup.Visible)
        {
            return;
        }
        if (_board.SideToMove != _playerColor)
        {
            return;
        }

        int clickedSq = _displayToBoardSq[displayIdx];
        Piece clickedPiece = _board.GetPiece(clickedSq);

        // Si ya habia una casilla seleccionada, verificar si el jugador pulsa un destino legal
        if (_selectedSquare >= 0)
        {
            if (clickedSq == _selectedSquare)
            {
                _selectedSquare = -1;
                RenderBoard();
                return;
            }

            List<ChessMove> matchingMoves = new();
            foreach (ChessMove m in _currentLegalMoves)
            {
                if (m.From == _selectedSquare && m.To == clickedSq)
                {
                    matchingMoves.Add(m);
                }
            }

            if (matchingMoves.Count > 0)
            {
                // Si es coronacion, abrimos el popup modal para elegir la pieza
                if (matchingMoves[0].IsPromotion)
                {
                    _pendingPromoFrom = _selectedSquare;
                    _pendingPromoTo = clickedSq;
                    _promotionPopup.OpenForColor(_playerColor);
                    return;
                }

                ExecuteMoveAndAdvance(matchingMoves[0]);
                return;
            }
        }

        // Seleccionar una pieza propia
        if (!clickedPiece.IsEmpty && clickedPiece.Color == _playerColor)
        {
            _selectedSquare = clickedSq;
        }
        else
        {
            _selectedSquare = -1;
        }

        RenderBoard();
    }

    private void OnPromotionPieceChosen(int pieceTypeValue)
    {
        PieceType chosen = (PieceType)pieceTypeValue;
        foreach (ChessMove m in _currentLegalMoves)
        {
            if (m.From == _pendingPromoFrom &&
                m.To == _pendingPromoTo &&
                m.IsPromotion &&
                m.PromotionPiece == chosen)
            {
                _pendingPromoFrom = -1;
                _pendingPromoTo = -1;
                ExecuteMoveAndAdvance(m);
                return;
            }
        }
    }

    private void ExecuteMoveAndAdvance(ChessMove move)
    {
        string notation = MoveGenerator.FormatMoveNotation(_board, move);
        _moveNotations.Add(notation);

        Piece captured = move.IsCapture
            ? (move.Special == MoveSpecial.EnPassant
                ? new Piece(PieceType.Pawn, SquareHelper.Opponent(_board.SideToMove))
                : _board.GetPiece(move.To))
            : Piece.Empty;
        _capturedStack.Push(captured);

        _board.MakeMove(move);
        _lastMoveFrom = move.From;
        _lastMoveTo = move.To;
        _selectedSquare = -1;

        RefreshBoardState();

        if (!IsMatchFinished() && _board.SideToMove == _cpuColor)
        {
            _ = RunCpuTurnAsync();
        }
    }

    /// <summary>
    /// Ejecuta la IA en un hilo secundario usando async/await para mantener la fluidez de la UI.
    /// </summary>
    private async Task RunCpuTurnAsync()
    {
        CancelAiSearch();
        _aiCts = new CancellationTokenSource();
        CancellationToken token = _aiCts.Token;

        _isCpuThinking = true;
        UpdateTurnAndActionUI();

        try
        {
            // Breve pausa visual para que el usuario aprecie su jugada antes de la respuesta
            await Task.Delay(240, token);
            ChessMove? aiMove = await ChessAI.ChooseBestMoveAsync(
                _board,
                GameConfig.Instance.Difficulty,
                token);

            if (!IsInsideTree() || token.IsCancellationRequested)
            {
                return;
            }

            _isCpuThinking = false;

            if (aiMove.HasValue)
            {
                ExecuteMoveAndAdvance(aiMove.Value);
            }
            else
            {
                RefreshBoardState();
            }
        }
        catch (OperationCanceledException)
        {
            _isCpuThinking = false;
        }
    }

    private void OnUndoPressed()
    {
        if (_isCpuThinking || _board.PlyCount == 0 || _promotionPopup.Visible)
        {
            return;
        }

        // Si es turno del jugador y hay al menos 2 jugadas (propia + CPU), deshacemos ambas
        // para volver exactamente al turno anterior del jugador.
        int pliesToUndo = 1;
        if (_board.SideToMove == _playerColor && _board.PlyCount >= 2)
        {
            pliesToUndo = 2;
        }
        else if (_board.SideToMove == _playerColor && _board.PlyCount == 1 && _cpuColor == PieceColor.White)
        {
            // No se puede deshacer la apertura unica de la CPU blanca sin dejarla mover de nuevo
            return;
        }

        for (int i = 0; i < pliesToUndo; i++)
        {
            if (_board.UndoMove())
            {
                if (_moveNotations.Count > 0) _moveNotations.RemoveAt(_moveNotations.Count - 1);
                if (_capturedStack.Count > 0) _capturedStack.Pop();
            }
        }

        _selectedSquare = -1;
        _lastMoveFrom = -1;
        _lastMoveTo = -1;
        _gameOverPopup.Visible = false;

        RefreshBoardState();
    }

    private void RefreshBoardState()
    {
        _currentLegalMoves = MoveGenerator.GenerateLegalMoves(_board);
        _currentStatus = MoveGenerator.EvaluateGameStatus(_board, _currentLegalMoves);

        RenderBoard();
        UpdateTurnAndActionUI();
        UpdateCapturedPiecesAndScore();
        UpdateMoveHistoryText();
        CheckAndShowGameOverPopup();
    }

    private void RenderBoard()
    {
        bool showHints = GameConfig.Instance.ShowLegalHints;
        int checkedKingSq = (_currentStatus == GameStatus.Check || _currentStatus == GameStatus.Checkmate)
            ? _board.GetKingSquare(_board.SideToMove)
            : -1;

        var legalDestinations = new HashSet<int>();
        if (showHints && _selectedSquare >= 0)
        {
            foreach (ChessMove m in _currentLegalMoves)
            {
                if (m.From == _selectedSquare)
                {
                    legalDestinations.Add(m.To);
                }
            }
        }

        for (int dispIdx = 0; dispIdx < 64; dispIdx++)
        {
            int boardSq = _displayToBoardSq[dispIdx];
            int row = SquareHelper.Row(boardSq);
            int col = SquareHelper.Col(boardSq);
            bool isLight = ((row + col) & 1) == 0;

            Button btn = _squareButtons[dispIdx];
            StyleBoxFlat tileStyle = isLight ? _lightNormal : _darkNormal;

            if (boardSq == _lastMoveFrom || boardSq == _lastMoveTo)
            {
                tileStyle = isLight ? _lastMoveLight : _lastMoveDark;
            }
            if (boardSq == _selectedSquare)
            {
                tileStyle = _selectedStyle;
            }
            if (boardSq == checkedKingSq)
            {
                tileStyle = _checkStyle;
            }

            btn.AddThemeStyleboxOverride("normal", tileStyle);
            btn.AddThemeStyleboxOverride("hover", tileStyle);
            btn.AddThemeStyleboxOverride("pressed", tileStyle);

            Piece p = _board.GetPiece(boardSq);
            TextureRect icon = _pieceIcons[dispIdx];
            if (p.IsEmpty)
            {
                icon.Texture = null;
            }
            else
            {
                _pieceTextures.TryGetValue(p.GetAssetName(), out Texture2D? tex);
                icon.Texture = tex;
            }

            Panel hint = _moveHints[dispIdx];
            if (legalDestinations.Contains(boardSq))
            {
                bool isCapture = !p.IsEmpty || boardSq == _board.EnPassantSquare;
                if (isCapture)
                {
                    hint.AnchorLeft = 0.08f;
                    hint.AnchorTop = 0.08f;
                    hint.AnchorRight = 0.92f;
                    hint.AnchorBottom = 0.92f;
                    hint.AddThemeStyleboxOverride("panel", _captureHintStyle);
                }
                else
                {
                    hint.AnchorLeft = 0.36f;
                    hint.AnchorTop = 0.36f;
                    hint.AnchorRight = 0.64f;
                    hint.AnchorBottom = 0.64f;
                    hint.AddThemeStyleboxOverride("panel", _dotHintStyle);
                }
                hint.Visible = true;
            }
            else
            {
                hint.Visible = false;
            }
        }
    }

    private void UpdateTurnAndActionUI()
    {
        if (_isCpuThinking)
        {
            _lblTurnStatus.Text = "CPU pensando...";
        }
        else
        {
            _lblTurnStatus.Text = _currentStatus switch
            {
                GameStatus.Checkmate => "¡Jaque Mate!",
                GameStatus.Stalemate => "Tablas por Ahogado",
                GameStatus.DrawFiftyMove => "Tablas (Regla 50 jugadas)",
                GameStatus.DrawInsufficientMaterial => "Tablas (Material insuficiente)",
                GameStatus.DrawThreefoldRepetition => "Tablas (Triple repeticion)",
                GameStatus.Check => _board.SideToMove == _playerColor ? "¡Estas en Jaque!" : "CPU en Jaque",
                _ => _board.SideToMove == _playerColor ? "Tu turno" : "Turno de CPU"
            };
        }

        bool canUndo = !_isCpuThinking &&
                       ((_board.SideToMove == _playerColor && _board.PlyCount >= 2) ||
                        (IsMatchFinished() && _board.PlyCount >= 1));
        _btnUndo.Disabled = !canUndo;
    }

    private void UpdateCapturedPiecesAndScore()
    {
        foreach (Node child in _cpuCapturedBox.GetChildren()) child.QueueFree();
        foreach (Node child in _playerCapturedBox.GetChildren()) child.QueueFree();

        int whiteMaterial = 0;
        int blackMaterial = 0;

        static int MaterialValue(PieceType type) => type switch
        {
            PieceType.Pawn => 1,
            PieceType.Knight => 3,
            PieceType.Bishop => 3,
            PieceType.Rook => 5,
            PieceType.Queen => 9,
            _ => 0
        };

        for (int sq = 0; sq < 64; sq++)
        {
            Piece p = _board.GetPiece(sq);
            if (p.IsEmpty) continue;
            int val = MaterialValue(p.Type);
            if (p.Color == PieceColor.White) whiteMaterial += val;
            else blackMaterial += val;
        }

        int diff = _playerColor == PieceColor.White
            ? whiteMaterial - blackMaterial
            : blackMaterial - whiteMaterial;

        _lblPlayerAdvantage.Text = diff > 0 ? $"+{diff}" : string.Empty;
        _lblCpuAdvantage.Text = diff < 0 ? $"+{-diff}" : string.Empty;

        // Mostrar mini iconos SVG de piezas capturadas
        var capturedList = new List<Piece>(_capturedStack);
        capturedList.Reverse();

        foreach (Piece cap in capturedList)
        {
            if (cap.IsEmpty) continue;
            if (!_pieceTextures.TryGetValue(cap.GetAssetName(), out Texture2D? tex)) continue;

            var miniIcon = new TextureRect
            {
                Texture = tex,
                CustomMinimumSize = new Vector2(22, 22),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };

            if (cap.Color == _cpuColor)
            {
                _playerCapturedBox.AddChild(miniIcon);
            }
            else
            {
                _cpuCapturedBox.AddChild(miniIcon);
            }
        }
    }

    private void UpdateMoveHistoryText()
    {
        if (_moveNotations.Count == 0)
        {
            _lblMoveHistory.Text = "Sin jugadas todavia. Toca una pieza para comenzar.";
            return;
        }

        var parts = new List<string>();
        for (int i = 0; i < _moveNotations.Count; i += 2)
        {
            int moveNum = (i / 2) + 1;
            string whiteMove = _moveNotations[i];
            string blackMove = (i + 1 < _moveNotations.Count) ? _moveNotations[i + 1] : "";
            parts.Add(string.IsNullOrEmpty(blackMove)
                ? $"{moveNum}. {whiteMove}"
                : $"{moveNum}. {whiteMove} {blackMove}");
        }

        _lblMoveHistory.Text = string.Join("   ", parts);
    }

    private void CheckAndShowGameOverPopup()
    {
        if (!IsMatchFinished()) return;

        string title;
        string reason;

        if (_currentStatus == GameStatus.Checkmate)
        {
            bool playerWon = _board.SideToMove != _playerColor;
            title = playerWon ? "¡Victoria!" : "Derrota";
            reason = playerWon
                ? "Has ganado la partida por jaque mate."
                : "La CPU ha ganado por jaque mate.";
        }
        else
        {
            title = "Tablas";
            reason = _currentStatus switch
            {
                GameStatus.Stalemate => "Empate por rey ahogado (sin jugadas legales).",
                GameStatus.DrawFiftyMove => "Empate por la regla de los 50 movimientos.",
                GameStatus.DrawInsufficientMaterial => "Empate por material insuficiente para dar mate.",
                GameStatus.DrawThreefoldRepetition => "Empate por triple repeticion de posicion.",
                _ => "La partida ha finalizado en empate."
            };
        }

        _gameOverPopup.ShowResult(title, reason, _board.PlyCount);
    }

    private bool IsMatchFinished() =>
        _currentStatus != GameStatus.Playing && _currentStatus != GameStatus.Check;

    private void OnMenuPressed()
    {
        CancelAiSearch();
        GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
    }

    private void CancelAiSearch()
    {
        if (_aiCts != null)
        {
            _aiCts.Cancel();
            _aiCts.Dispose();
            _aiCts = null;
        }
    }
}
