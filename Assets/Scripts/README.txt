# Mechanica 2D – Developer README

_Last updated: 2025-05-15_

## Overview

Mechanica 2D is a grid-based sandbox editor for creating mechanical contraptions from modular parts. The simulation runs in two distinct modes:

- **Design Mode (Freeze Mode)** – Build, select, move, rotate, scale, and connect parts.
- **Play Mode (Unfrozen)** – Activate physics, motors, and observe your contraption in action.

The editor emphasizes intuitive interaction and visual clarity, supporting advanced multi-part manipulation, joint logic, grid snapping, and more.

---

## Core Features

### Selection & Interaction

- **Click to select** parts. Click away to deselect. Shift-click to multi-select.
- **Drag-box marquee selection**:
  - Click+drag in blank space to draw a translucent selection box.
  - All parts with a collider under the box become selected.
  - Hold **Shift** to add to the existing selection without deselecting prior parts.
  - Dragging only becomes a marquee after 4 pixels of movement (avoids false positives).
  - Outlines never disappear while Shift-clicking or dragging empty space.

- **Outline highlights**:
  - Cyan outline: Primary selected part.
  - Magenta outlines: Other selected parts.
  - Hidden or obscured outlines show as dashed (for clarity behind parts).
  - Outlines update dynamically and respect sorting layers.

### Part Manipulation

- **Move** selected parts with the mouse (drag).
- **Rotate** with `R` in 45° increments:
  - Pressing `R` rotates the entire group around the **main selected part**.
  - Works whether dragging or not.
  - During rotation, the main selected part stays fixed in position; others orbit around it.
- **Scale** parts using control points on each side:
  - Triangles scale correctly from their visual centers (compensating for sprite pivot offset).
  - Hypotenuse scaling supports both X and Y directions at once.
  - Grid snapping ensures consistent scale increments (typically 0.5).

### Grid & Snapping

- Grid is always visible in design mode, fading out during simulation.
- All part movement and scaling are snapped to grid points unless overridden.
- Triangle pivot offset (.25, .25) is fully accounted for in snapping and scaling logic.

### Joint Connections

- Parts can be connected by clicking a **join** button:
  - You must first select **both parts** to be joined.
  - Then click where they overlap to create the connection.
  - If multiple selected parts overlap at the cursor, the topmost two are chosen.
- Joints are visualized with simple connectors.

### Motors

- Parts can be given motor behavior using the motor context menu.
- Options include angular speed, activation toggles, and joint targeting.
- Selection highlighting supports motor-linked parts.

### Context Menus & UI

- **Right-click** brings up a contextual menu for selected parts (e.g. delete, motorize, no-collision).
- Context menus are protected against background clicks — UI elements block part selection.
- Freeze Mode indicator + Auto Bring-to-Front toggle are shown in the Spawner’s UI.

---

## Components and Scripts

| Script                    | Purpose |
|--------------------------|---------|
| `Part.Core.cs`           | Main logic for each part (selection, state) |
| `Part.Dragging.cs`       | Handles dragging and movement |
| `Part.Input.cs`          | Handles input events for selection/interaction |
| `Part.GlobalRotate.cs`   | Rotates entire selection group |
| `Part.Outline.cs`        | Draws and manages outlines with layering logic |
| `Part.RightClickAndPan.cs` | Panning and right-click behavior |
| `Part.Selection.cs`      | Utility for group selection and deselection |
| `Part.ContextMenu.cs`    | Context menu logic and click handling |
| `Part.Connectivity.cs`   | Joint connection logic |
| `Part.NoCollisionChooser.cs` | Toggles part collision behavior |
| `CameraController.cs`    | Basic zoom and camera drag |
| `GridRenderer.cs`        | Draws grid during design mode |
| `GridSnapping.cs`        | Snap positioning and scale values |
| `JointVisual.cs`         | Visual representation of part joints |
| `Motor.cs`               | Logic and UI for adding motors |
| `SortingOrderManager.cs` | Ensures correct visual layering for parts and outlines |
| `Spawner.cs`             | Spawns new parts and handles UI toggles |
| `SelectionBox.cs`        | Implements marquee drag-box multi-selection |
| `README.txt`             | Internal documentation for devs |

---

## Known State

- All major interactions are functional and intuitive.
- Multi-selection is robust and supports group manipulation.
- Outline behavior is stable even with layered or obstructed parts.
- No known bugs in scaling, rotation, snapping, or selection.
- Optimized for sandbox contraption-building and YouTube demonstration.

---

## Future Ideas (optional)

- Duplicate parts or groups with `Ctrl+D`
- Multi-joint preview and edit
- Part naming or labeling
- Undo/Redo stack
- More part types (gears, sliders, pistons)
- Simulation toggles (gravity, friction)

---

## Notes for Developers

- Always test changes in **both design and play modes**.
- Avoid modifying selection state during `Update` – use `LateUpdate` if needed.
- All selection and input is disabled while a part is being interacted with (`Part.IsInteracting`).
- Grid snapping and outline rendering both rely on correct part pivot alignment.

---

## Credits

Created by [Thomas Wald]  
Built in Unity using C#  
2D mechanical design inspiration drawn from old Flash-era sandbox games and modern maker tools.
