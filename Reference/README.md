# Reference (not compiled — lives outside Assets/)
Read-only copies of CCTD code to port from. Never edit these; port the ideas into the Piglings layers.
- ThrowSolver.cs / ThrowController.cs / TrajectoryView.cs -> Simulation/ThrowController + Presentation/TrajectoryView
- SpiderController.cs -> already ported as Simulation/RobotController (state machine pattern)
- SpiderSpawner.cs -> Simulation/RobotSpawner (weighted spawn areas can be ported later)
