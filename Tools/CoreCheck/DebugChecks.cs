using System;
using System.Collections.Generic;
using Piglings.Events; using Piglings.Meta; using Piglings.Rules; using Piglings.Runtime;

// M11.T2, the F1 debug panel: save edits written as the smallest cause (ProfileEdits), and the board edit (SetSocket).
partial class P{
static void DebugChecks(){
 // --- Stones: thresholds 10, 20, 30…; start 10, max 25 ---
 { var sp=new StoneProgression(new[]{10,20,30,40,50,60,70,80,90,100,110,120,130,140,150,160},10,25);
   Check(ProfileEdits.HitsForStones(sp,10)==0 && ProfileEdits.HitsForStones(sp,5)==0 && ProfileEdits.HitsForStones(sp,12)==20,
     "stones → hits: the start (or less) = 0; 12 stones = the 2nd threshold (20)");
   Check(ProfileEdits.HitsForStones(sp,99)==150,"past the cap: the hits of the cap (25 stones = the 15th threshold, 150)");
   foreach(int n in new[]{10,11,14,15,20,25}) Check(sp.For(ProfileEdits.HitsForStones(sp,n)).Stones==n,$"round trip: {n} stones → hits → {n} stones");
   var p=new PlayerProfile(); ProfileEdits.SetStones(p,"stone",sp,15);
   Check(p.DirectHits("stone")==50 && sp.For(p.DirectHits("stone")).Level==2,"set 15 stones: 50 hits, level 2 (the default evolutions)");
   ProfileEdits.SetStones(p,"stone",sp,10);
   Check(p.DirectHits("stone")==0 && sp.For(0).Level==1,"reverting: back to 10 stones = 0 hits, level 1");
   Check(ProfileEdits.StonesForLevel(sp,1)==10 && ProfileEdits.StonesForLevel(sp,3)==20 && ProfileEdits.StonesForLevel(sp,9)==25,
     "an evolution level's stones: lv1 10, lv3 20, past the list = the last (25)"); }
 // --- Peg copies: thresholds 10, 30, 60; start 1; level 1 weight 1 (and a weight of 2) ---
 { var pp=new PegProgression(new[]{10,30,60},null,1,8,3);
   Check(ProfileEdits.TriggersForCopies(pp,1)==0 && ProfileEdits.TriggersForCopies(pp,3)==30 && ProfileEdits.TriggersForCopies(pp,9)==60,
     "copies → triggers: 1 (the start) = 0; 3 = 30; more than the thresholds give = the last (60)");
   var p=new PlayerProfile(); p.Peg("peg_bouncy").Triggers.AddRange(new[]{5,40});
   ProfileEdits.SetPegCopies(p,"peg_bouncy",pp,2);
   Check(p.Pegs["peg_bouncy"].Triggers.Count==1 && pp.For(p.Pegs["peg_bouncy"].Triggers).Copies==2,"set 2 copies: the old triggers replaced, 2 copies derived");
   ProfileEdits.SetPegCopies(p,"peg_bouncy",pp,1);
   Check(p.Pegs["peg_bouncy"].Triggers.Count==0 && pp.For(p.Pegs["peg_bouncy"].Triggers).Copies==1,"back to 1 copy: no triggers");
   var heavy=new PegProgression(new[]{10,30},new[]{2f},1,8,3);
   Check(ProfileEdits.TriggersForCopies(heavy,2)==5 && ProfileEdits.TriggersForCopies(heavy,3)==15,"a level-1 weight of 2: half the triggers (rounded up)"); }
 // --- Nights won / current night ---
 { var plan=new CampaignPlan(new[]{"n1","n2","n3"},new[]{"peg_splitter",null,"peg_bomb"},new[]{"peg_bouncy"});
   var p=new PlayerProfile(); p.Dawns["elsewhere"]=4;
   ProfileEdits.SetNightsWon(p,plan,1);
   Check(p.DawnsOn("n1")==1 && p.DawnsOn("n2")==0 && plan.IsUnlocked(p,"peg_splitter") && !plan.IsUnlocked(p,"peg_bomb") && ProfileEdits.NightsWon(p,plan)==1,
     "1 night won: night 1's dawn, its unlock (Splitter) follows; Bomb still locked");
   ProfileEdits.SetNightsWon(p,plan,3); Check(ProfileEdits.NightsWon(p,plan)==3 && plan.IsUnlocked(p,"peg_bomb"),"all 3 won: Bomb unlocked");
   ProfileEdits.SetNightsWon(p,plan,0); Check(ProfileEdits.NightsWon(p,plan)==0 && !plan.IsUnlocked(p,"peg_splitter") && p.DawnsOn("elsewhere")==4,
     "none won: unlocks gone; a dawn outside the campaign is left alone");
   ProfileEdits.SetCurrentNight(p,plan,7); Check(p.CurrentNight==2,"the current night is kept inside the campaign (7 → the last, index 2)");
   ProfileEdits.SetCurrentNight(p,plan,-3); Check(p.CurrentNight==0,"…and never below the first"); }
 // --- End the night now: NightReferee.DebugEnd ---
 { var n=new Night(new NightGoal(new[]{50,500},10,0));
   n.Play(1);
   Check(n.Ref.DebugEnd(false) && n.Ends.Count==1 && n.Ends[0].Result==NightResult.Lost && n.Ends[0].Reason==NightEndReason.OutOfStones && n.St.Ended,
     "Lose night: ended at once, out of stones, NightEnded published");
   Check(!n.Ref.DebugEnd(true) && n.Ends.Count==1,"an ended night can't be ended again"); }
 { var n=new Night(new NightGoal(new[]{50,500},10,0));
   Check(n.Ref.DebugEnd(true) && n.Dawns.Count==1 && n.Ends[0].Result==NightResult.Won && n.St.ThresholdsReached==2 && n.Ends[0].HoursReached==2,
     "Win night: a dawn — every hour reached, DawnReached, then Won");
   var dusk=new Night(new NightGoal(new[]{50},10,0)); dusk.St.Phase=NightPhase.Dusk;
   Check(!dusk.Ref.DebugEnd(false),"not before the night begins (Dusk)"); }
 // --- Board edit: NightReferee.SetSocket ---
 { var pegs=new PegSetup(new[]{(new PegType("peg_bouncy",3,true,PegEffect.Bouncy),2)},1,4);
   var n=new Night(new NightGoal(new[]{50},10,0),null,pegs);
   var sets=new List<PegSocketSet>(); n.Bus.Subscribe<PegSocketSet>(e=>sets.Add(e));
   Check(n.Ref.SetSocket(1,"peg_bouncy",2) && n.St.Sockets[1].PegId=="peg_bouncy" && n.St.Sockets[1].Level==2 && sets.Count==1 && sets[0].Level==2,
     "set a socket while Running: the peg at level 2, PegSocketSet published");
   Check(n.Ref.SetSocket(1,"peg_bouncy",9) && n.St.Sockets[1].Level==3,"the level is clamped to the type's max (3)");
   Check(!n.Ref.SetSocket(2,"peg_unknown",1) && n.St.Sockets[2].IsEmpty && !n.Ref.SetSocket(9,"peg_bouncy",1),"an unknown type or socket: refused");
   Check(n.Ref.SetSocket(1,null,0) && n.St.Sockets[1].IsEmpty && n.St.Sockets[1].Level==0 && sets[sets.Count-1].PegId==null,"emptied: PegSocketSet with no peg");
   Check(n.Shelf("peg_bouncy")==2 && n.Placed.Count==0,"no shelf used, no PegPlaced"); }
}
}
