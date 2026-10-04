using System;
using Piglings.Meta; using Piglings.Simulation;

// Prototype v2, PR F: the day phase's slice placement — which slice comes next when one is clicked (CampaignPlan.CycleSlice)
// and which slice a point is on (TowerLayout).
partial class P{
static void BarnChecks(){
 // --- CycleSlice: forward, back, wrapping, unknown current, nothing available ---
 var all=new[]{"barn","wood","straw","brick"};
 Check(CampaignPlan.CycleSlice(all,"barn",1)=="wood" && CampaignPlan.CycleSlice(all,"wood",1)=="straw","cycle: click → the next slice in the campaign's list");
 Check(CampaignPlan.CycleSlice(all,"brick",1)=="barn","cycle: the last wraps round to the first");
 Check(CampaignPlan.CycleSlice(all,"barn",-1)=="brick" && CampaignPlan.CycleSlice(all,"straw",-1)=="wood","cycle: right-click → the previous, wrapping");
 Check(CampaignPlan.CycleSlice(all,"gold",1)=="barn" && CampaignPlan.CycleSlice(all,"gold",-1)=="brick" && CampaignPlan.CycleSlice(all,null,1)=="barn",
       "cycle: a slice not in the list starts at the first (or the last going back)");
 Check(CampaignPlan.CycleSlice(new string[0],"barn",1)==null && CampaignPlan.CycleSlice(null,"barn",1)==null,"cycle: nothing available → null (no change)");
 Check(CampaignPlan.CycleSlice(new[]{"barn"},"barn",1)=="barn","cycle: one slice → stays (no change)");
 Check(CampaignPlan.CycleSlice(new[]{"barn","","wood","barn",null},"barn",1)=="wood" && CampaignPlan.CycleSlice(new[]{"barn","","wood","barn"},"wood",1)=="barn",
       "cycle: empty ids and repeats are ignored");

 // --- TowerLayout: TestNight's layout (slices from 3.0, every 1.6, centred on x 0) ---
 Check(TowerLayout.SliceBottom(0,3f,1.6f)==3f && Math.Abs(TowerLayout.SliceBottom(2,3f,1.6f)-6.2f)<1e-5f,"layout: slice i's bottom = 3 + i × 1.6");
 Check(TowerLayout.SliceAt(0f,3.01f,0f,2.6f,3f,1.6f,3)==0 && TowerLayout.SliceAt(1f,4.7f,0f,2.6f,3f,1.6f,3)==1 && TowerLayout.SliceAt(-2.5f,7.7f,0f,2.6f,3f,1.6f,3)==2,
       "layout: a point inside a slice → its index (bottom = 0)");
 Check(TowerLayout.SliceAt(0f,2.9f,0f,2.6f,3f,1.6f,3)==-1 && TowerLayout.SliceAt(0f,7.81f,0f,2.6f,3f,1.6f,3)==-1,"layout: below the first slice or above the last → none");
 Check(TowerLayout.SliceAt(2.7f,4f,0f,2.6f,3f,1.6f,3)==-1 && TowerLayout.SliceAt(-2.7f,4f,0f,2.6f,3f,1.6f,3)==-1,"layout: beside the tower → none");
 Check(TowerLayout.SliceAt(0f,4f,0f,2.6f,3f,1.6f,0)==-1 && TowerLayout.SliceAt(0f,4f,0f,2.6f,3f,0f,3)==-1,"layout: no slices (or no height) → none");
}
}
