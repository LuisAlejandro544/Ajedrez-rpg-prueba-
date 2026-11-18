# Ajedrez Tactico (Godot 4.7.2 + C#)

Proyecto completo de ajedrez clasico para **Godot Engine 4.7.2 (.NET / C#)** con piezas vectoriales en formato `.svg` e Inteligencia Artificial local (CPU).

## Requisitos
- **Godot Engine 4.7.2 .NET** (version con soporte C#).
- **.NET 8 SDK** instalado (compatible con exportacion movil Android 32/64 bits y escritorio).

## Como abrir y ejecutar
1. Abre **Godot Engine 4.7.2 (.NET)**.
2. Importa el archivo `godot_chess/project.godot`.
3. Pulsa **Compilar (Build)** en la esquina superior derecha para compilar el proyecto C# (`AjedrezGodot.csproj`).
4. Ejecuta la escena principal (`res://Scenes/MainMenu.tscn`) con **F5**.

## Estructura modular del proyecto
- `Assets/Pieces/*.svg`: 12 piezas vectoriales (Blancas y Negras: Rey, Dama, Torre, Alfil, Caballo y Peon).
- `Scripts/Core/`: Logica pura desacoplada de Godot UI:
  - `ChessTypes.cs`: Estructuras ligeras (`Piece`, `ChessMove`, `UndoState`) y utilidades de casillas.
  - `ChessBoard.cs`: Estado de 64 casillas, `MakeMove` / `UndoMove`, hash Zobrist y deteccion de tablas.
  - `MoveGenerator.cs`: Reglas completas (movimientos legales, jaque, mate, ahogado, enroque corto/largo, captura al paso, coronacion y notacion algebraica).
  - `ChessAI.cs`: Motor CPU local con Negamax, poda Alfa-Beta, ordenamiento MVV-LVA, busqueda de quietud y ejecucion asincrona en segundo plano (`Task.Run`).
- `Scripts/UI/` y `Scenes/`:
  - `GameConfig.cs`: Autoload global para preferencias de partida.
  - `MainMenu.tscn` / `MainMenu.cs`: Seleccion de bando, dificultad de CPU, opciones y navegacion.
  - `GameScreen.tscn` / `GameScreen.cs`: Tablero interactivo 8x8 con rotacion automatica, indicadores de jugadas legales, historial algebraico, piezas capturadas y boton deshacer.
  - `PromotionPopup.tscn`, `GameOverPopup.tscn`, `RulesPopup.tscn`: Popups modales dedicados.

## Compilar APK Debug con GitHub Actions (sin PC)
El repositorio incluye el flujo manual `.github/workflows/build-android-debug.yml`:
1. Ve a la pestana **Actions** en GitHub.
2. Selecciona **Compilar APK Debug (Godot C#)**.
3. Pulsa **Run workflow** (solo se activa manualmente; nunca por commits).
4. Al terminar, descarga el artefacto `ajedrez-tactico-debug-apk` compatible con Android de 32 y 64 bits.

