MECHANICA SANDBOX 2D
Version: May 2025

DESCRIPTION
Mechanica Sandbox 2D is a Unity-based prototype for testing physics-based mechanical assemblies.
Users can spawn square or circle parts, drag them, and connect them using bolts or hinges.
The simulation supports freeze mode for precise placement and connection building.

GOAL
Provide a simple 2D environment to prototype part behavior, connection logic, and grid snapping
systems similar to those found in sandbox engineering games.

SCENE ARCHITECTURE
Key Object Types:

Parts (Squares or Circles)

Visualized with SpriteRenderers

Have Rigidbody2D and Collider2D components

Can be moved, scaled, and connected

Selected parts now show a border outline; the primary part has a main color, other group parts have a secondary color (both colors configurable in Spawner).

Connections (Bolts or Hinges)

Visual indicators placed at grid snap points

Backed by FixedJoint2D or HingeJoint2D components

Created only when exactly two parts overlap a snap point and no other connection exists there

Grid

Defined by GridSnapping.cs

Snapping interval is consistent across all spawn and placement actions

Camera

Controlled by CameraController.cs

Supports panning and zooming (right mouse drag to pan)

UI

Freeze indicator displays whether simulation is frozen

Cursor changes for different actions (default, dragging, scaling)

Mass adjustment sliders appear when right-clicking a part

CONTROL SUMMARY
Action	Input
Toggle Freeze	F
Spawn square	S (freeze mode only)
Spawn circle	C (freeze mode only)
Place bolt	B (freeze mode only)
Place hinge	H (freeze mode only)
Move parts	Drag with left mouse button
Scale parts	Drag near part edges
Rotate selection	R while dragging
Pan camera	Right-click drag on empty space
Select part/group	Left-click or right-click on a part
Adjust mass	Right-click a part (toggles slider)
Deselect	Left-click empty space

Selection behavior:

Clicking a part selects it and its connected group (main and secondary outlines).

Right-click selects a part/group and opens the mass slider.

Dragging with the right mouse button does not change selection.

Spawning a new part deselects any current selection and selects the new part automatically.

GRID SNAPPING LOGIC
All spawn and placement actions use GridSnapping.SnapPos to align positions to the nearest
defined grid interval.

Connections (bolts or hinges) must be placed at a grid snap point.

Snap points are calculated based on the current grid interval, ensuring consistency between
parts and connections.

JOINT PLACEMENT RULES
When the user attempts to place a connection (Bolt or Hinge):

The system snaps the mouse position to the nearest grid point.

It checks for parts overlapping that point using Physics2D.OverlapCircleAll.

Exactly two distinct Part objects must be detected at that point.

No AnchoredJoint2D may already exist at that point.

If all conditions are met, the connection is created.

Once placed:

A FixedJoint2D or HingeJoint2D is created on one part, connected to the other.

A visual sprite is added to represent the connection.

SCRIPT FILES
Script	Purpose
CameraController.cs	Handles camera movement and zoom controls
Part.cs	Defines the Part class; manages dragging, scaling, connection awareness, and selection outlines
GridSnapping.cs	Provides static methods for snapping positions to the grid
GridRenderer.cs	Renders the visible grid lines in the scene
Spawner.cs	Manages spawning of parts and connections, input handling, selection logic, and freeze control
README.txt	Developer documentation (this file)

DEVELOPMENT NOTES
The system assumes the grid interval will remain constant during play.

Connections use visual sprites sized independently of part scaling.

All connection anchors are computed relative to their parent parts for consistency.

KNOWN LIMITATIONS
Only two parts can be connected at a given snap point.

No support yet for deleting parts or connections.

All connections are assumed to be either Fixed or Hinge joints.

Joint breakage or advanced joint properties are not handled.

EXTENDING THE SYSTEM
To add new part types:

Create a new prefab or update Spawner.cs to spawn different shapes.

Ensure the new part type implements or inherits the Part behavior.

To add new connection types:

Extend the JointType enum.

Add placement and visual creation logic to Spawner.cs.

To allow more than two parts per connection:

Update the joint placement logic to accept more than two overlapping parts.

Design new rules for how such multi-part connections should behave.

To support part or connection deletion:

Add right-click or UI-based deletion controls.

Ensure joint cleanup logic is implemented to avoid dangling references.

To improve snapping flexibility:

Consider supporting variable grid sizes or local grid overrides for certain parts.

CREDITS
Mechanica Sandbox 2D system design and codebase by Thomas Wald and contributors.