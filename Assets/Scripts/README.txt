Mechanica sandbox 2D — Developer Notes
Last updated: 2025-05-07

Key Controls

S — Spawn square part

C — Spawn circle part

B — Create bolt between two parts (when frozen)

H — Create hinge between two parts (when frozen)

M — Create motorized hinge ("motor") between two parts (when frozen)

F — Toggle freeze / unfreeze

Current Features

Parts

Parts can be square or circle.

Parts can be dragged, rotated, and scaled when frozen.

Right-click a part to open its context menu:

Adjust mass.

Duplicate.

Rip (disconnect from group).

Delete.

Joints

Bolt (fixed connection).

Hinge (free rotation).

Motor (powered hinge with controllable speed and direction).

Joints are placed between two parts and maintain their relative positions.

Motor joints are selected and edited independently from parts:

Right-click motor to open its menu.

Adjust speed (0–360 degrees/sec).

Toggle clockwise/counter-clockwise direction.

Selection & Grouping

Clicking a part or motor highlights it.

Selecting a part highlights its entire connected group.

Duplicating or ripping affects the whole group or individual parts respectively.

Motors and parts have their own selection and context menu systems.

Physics

Motors apply infinite torque — object weight does not affect them.

Physics joints auto-anchor at creation points and respect parent part scaling.

Parts and joints use the global grid snapping system for placement.

Recent Changes (2025-05-07)

Implemented motor joints (Spawner.cs, Motor.cs, Part.cs updated).

Motors have their own selection, outline, and context menu.

Motors can be created by pressing M (instead of H).

Motors now properly select connected parts when right-clicked.

Fixed minor bug where invalid dash character in code caused connection point detection to fail.

Known limitations / Future TODOs

Motor torque is fixed at infinite and cannot yet be customized.

Scaling non-uniformly may affect some joint visuals if not carefully maintained.

Grouping logic may need future refactoring for more complex multi-joint systems.