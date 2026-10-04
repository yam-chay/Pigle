using System;
using Piglings.Simulation;

// Prototype v2, PR F: the day phase's slice placement — what a drag-and-drop does (SliceDrag)
// and which slice a point is on (TowerLayout).
partial class P{
static void BarnChecks(){
 // --- SliceDrag: what a drop does — the tower is always whole ---
 Check(SliceDrag.Resolve(true,-1,2)==SliceDropKind.Replace,"drop: a tray slice on a slot → that slot becomes it");
 Check(SliceDrag.Resolve(false,0,2)==SliceDropKind.Swap,"drop: a tower slice on another slot → the two trade places");
 Check(SliceDrag.Resolve(false,1,1)==SliceDropKind.None,"drop: back on its own slot → nothing changes (it slides home)");
 Check(SliceDrag.Resolve(false,1,-1)==SliceDropKind.None && SliceDrag.Resolve(true,-1,-1)==SliceDropKind.None,"drop: off the tower → nothing changes, no gap");
 Check(!SliceDrag.IsDrag(0.05f,0.05f,0.1f) && SliceDrag.IsDrag(0.08f,0.08f,0.1f) && !SliceDrag.IsDrag(0f,0f,0f),"drag: a press becomes a drag only past the threshold");
 Check(Math.Abs(SliceDrag.Jiggle(0f,3f,4f))<1e-5f && Math.Abs(SliceDrag.Jiggle(1f/16f,3f,4f)-3f)<1e-4f && Math.Abs(SliceDrag.Jiggle(3f/16f,3f,4f)+3f)<1e-4f,
       "jiggle: swings ± the amplitude, at the frequency");

 // --- TowerLayout: TestNight's layout (slices from 3.0, every 1.6, centred on x 0) ---
 Check(TowerLayout.SliceBottom(0,3f,1.6f)==3f && Math.Abs(TowerLayout.SliceBottom(2,3f,1.6f)-6.2f)<1e-5f,"layout: slice i's bottom = 3 + i × 1.6");
 Check(TowerLayout.SliceAt(0f,3.01f,0f,2.6f,3f,1.6f,3)==0 && TowerLayout.SliceAt(1f,4.7f,0f,2.6f,3f,1.6f,3)==1 && TowerLayout.SliceAt(-2.5f,7.7f,0f,2.6f,3f,1.6f,3)==2,
       "layout: a point inside a slice → its index (bottom = 0)");
 Check(TowerLayout.SliceAt(0f,2.9f,0f,2.6f,3f,1.6f,3)==-1 && TowerLayout.SliceAt(0f,7.81f,0f,2.6f,3f,1.6f,3)==-1,"layout: below the first slice or above the last → none");
 Check(TowerLayout.SliceAt(2.7f,4f,0f,2.6f,3f,1.6f,3)==-1 && TowerLayout.SliceAt(-2.7f,4f,0f,2.6f,3f,1.6f,3)==-1,"layout: beside the tower → none");
 Check(TowerLayout.SliceAt(0f,4f,0f,2.6f,3f,1.6f,0)==-1 && TowerLayout.SliceAt(0f,4f,0f,2.6f,3f,0f,3)==-1,"layout: no slices (or no height) → none");
}
}
