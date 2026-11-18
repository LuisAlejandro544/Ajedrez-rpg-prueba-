/**
 * @license
 * SPDX-License-Identifier: Apache-2.0
 */

/**
 * Componente principal mínimo ("Hello World") solicitado para que
 * el entorno web compile sin errores mientras el proyecto principal
 * de ajedrez reside en la carpeta /godot_chess (Godot 4.7 + C#).
 */
export default function App() {
  return (
    <main className="min-h-screen flex items-center justify-center bg-neutral-950 text-neutral-100 p-6">
      <div className="text-center space-y-2">
        <h1 className="text-4xl font-semibold tracking-tight">Hello World</h1>
        <p className="text-sm text-neutral-400">
          Proyecto Godot 4.7 (C#) disponible en <code className="text-neutral-200">/godot_chess</code>
        </p>
      </div>
    </main>
  );
}
