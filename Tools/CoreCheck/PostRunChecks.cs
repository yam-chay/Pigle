using System;
using System.Collections.Generic;
using Piglings.Definitions; using Piglings.Events; using Piglings.Meta; using Piglings.Rules; using Piglings.Runtime; using Piglings.Simulation;

// Prototype v2, PR E: what the post-run screen reads — the night's facts (best throw per hour, the biggest chain, stones
// stolen), wolves dropped per robot type, the all-time records and their NEW flags, the "only what moved" progress rows,
// which buttons show, and the hour colours by night progress.
partial class P{
static void PostRunChecks(){
 // --- The stone's base by evolution level (M10.S) ---
 { var sp=new StoneProgression(new[]{10,20,30},10,25);
   Check(sp.BaseScoreFor(1)==10 && sp.BaseScoreFor(2)==20 && sp.BaseScoreFor(3)==40 && sp.BaseScoreFor(4)==80 && sp.BaseScoreFor(9)==80 && sp.BaseScoreFor(0)==10,
     "the stone's base by level: 10 / 20 / 40 / 80 (past the list → the last)");
   var custom=new StoneProgression(new int[0],10,25,new[]{new StoneEvolution(10,1,5),new StoneEvolution(12,2,-3)});
   Check(custom.BaseScoreFor(1)==5 && custom.BaseScoreFor(2)==0,"a level's own base; never negative"); }
 // --- The evolution track bar (StoneProgression.TrackPosition): evenly spaced slots 10 / 15 / 20 / 25 ---
 { var sp=new StoneProgression(new[]{10,20,30,40,50},10,25);
   Check(sp.TrackPosition(10)==0f && Math.Abs(sp.TrackPosition(15)-1f/3f)<1e-5f && Math.Abs(sp.TrackPosition(20)-2f/3f)<1e-5f && sp.TrackPosition(25)==1f,
     "the track: 10 → 0, 15 → ⅓, 20 → ⅔, 25 → 1 (on the slots)");
   Check(Math.Abs(sp.TrackPosition(12)-0.4f/3f)<1e-5f && sp.TrackPosition(5)==0f && sp.TrackPosition(40)==1f,"between slots by the stones; clamped below / past");
   var row=PostRunProgress.StoneRow(15,22,sp);   // 11 → 12 stones: no evolution
   Check(row.EvolutionBar.After>row.EvolutionBar.Before && !row.EvolvedTonight,"a stone earned short of the next evolution: the bar grows, no READY");
   var jump=PostRunProgress.StoneRow(45,55,sp);  // 14 → 15 stones: level 1 → 2
   Check(jump.EvolvedTonight && Math.Abs(jump.EvolutionBar.After-1f/3f)<1e-5f,"reaching 15 stones tonight: READY, the bar ends on the 15 slot");
   // Smooth: hits toward the next stone move it too (thresholds 10, 20, 30…: 25 hits = 12 stones + 50% of the 13th).
   Check(Math.Abs(sp.StonesWithProgress(25)-12.5f)<1e-5f && sp.StonesWithProgress(10)==11f,"stones with progress: 12.5 at 25 hits; 11 right on a threshold");
   var creep=PostRunProgress.StoneRow(21,25,sp);   // 12 stones both: 10% → 50% toward the 13th
   Check(creep.EvolutionBar.After>creep.EvolutionBar.Before && Math.Abs(creep.EvolutionBar.Before-(2.1f/5f)/3f)<1e-4f,
     "no stone earned, but hits gained: the bar still grows (12.1 → 12.5 stones on the track)");
   var capped=new StoneProgression(new[]{1},10,11);
   Check(capped.StonesWithProgress(500)==11f,"at the cap: just the count"); }
 // --- The night's facts ---
 { var n=new Night(new NightGoal(new[]{50,5000},10,0));
   n.Play(1);                        // hour 1: 20
   var big=n.Throw(3); n.Settle(big); // hour 1: 60 (crosses 50) -> round
   n.Ref.EndPlacement();
   n.Play(1);                        // hour 2: 20 × 1.5 = 30
   Check(n.St.Hours.Count==2 && n.St.Hours[0].BestThrow==60 && n.St.Hours[1].BestThrow==30,
     $"best throw per hour, by the hour it was thrown in (h1 60, h2 30; got {n.St.Hours[0].BestThrow}, {(n.St.Hours.Count>1?n.St.Hours[1].BestThrow:-1)})");
   n.Play(0);
   Check(n.St.Hours[1].BestThrow==30,"a miss (0 with the test curve's base 0) doesn't touch the hour's best"); }
 { var n=new Night(new NightGoal(new[]{5000},10,0));
   n.Settle(n.Throw(4));          // 4 robots, depth 0
   n.Settle(n.ThrowLine(3));      // 3 robots, depth 2
   Check(n.St.BiggestChainRobots==4 && n.St.BiggestChainDepth==0,"biggest chain: the most robots, with ITS depth (4 · depth 0)");
   Check(n.St.LongestChain==4 && n.St.DeepestChain==2,"...while longest / deepest are separate maxima (4, depth 2)");
   n.Settle(n.ThrowLine(4));      // 4 robots, depth 3: a tie on robots goes to the deeper one
   Check(n.St.BiggestChainRobots==4 && n.St.BiggestChainDepth==3,"biggest chain: a tie on robots goes to the deeper chain (4 · depth 3)"); }

 // --- Wolves dropped: every robot knocked off the wall, any cause, the sweep too (Yam), per robot type ---
 { var n=new Night(new NightGoal(new[]{30},10,0)); var t=new MasteryTally(n.Bus,n.St); var prog=new Progression(n.Bus,null);
   GameId Spawn(string type){ var r=n.Ids.Next(); n.Bus.Publish(new RobotSpawned(r,type)); return r; }
   var a=Spawn("wolf"); var b=Spawn("wolf"); var c=Spawn("fox"); var d=Spawn("wolf");
   var chain=new ChainId(n.Ids.Next()); var stone=n.Ids.Next();
   n.Bus.Publish(new ThrowReleased(chain,stone,"stone"));
   n.Bus.Publish(new RobotLostGrip(a,chain,Attribution.FromThrowable(stone)));   // direct
   n.Bus.Publish(new RobotLostGrip(b,chain,Attribution.FromRobotBall(a,0)));     // by a ball
   n.Bus.Publish(new RobotLostGrip(c,chain,Attribution.FromPeg(n.Ids.Next(),1))); // by a bomb we never heard of: still dropped
   Check(n.St.Dropped["wolf"]==2 && n.St.Dropped["fox"]==1,"dropped: a direct hit, a ball knock and a bomb knock all count, per type");
   n.Bus.Publish(new RobotSwept(d));
   Check(n.St.Dropped["wolf"]==2,"a RobotSwept outside the end (not the sweep) doesn't count");
   n.Wall.Add(d);   // still on the wall when the night ends: the sweep takes it
   n.Bus.Publish(new ThrowableRemoved(stone,chain));
   foreach(var r in new[]{a,b,c}) n.Bus.Publish(new RobotRemoved(r,chain,RemovalReason.HitGround));   // 10+20x1.5+... crosses 30: dawn
   Check(n.Ends.Count==1 && n.St.Dropped["wolf"]==3,"dawn: the end-of-night sweep's robots count as dropped (wolf 3)");
   Check(n.St.Swept.Count==1 && n.St.Swept["wolf"]==1,"...and the sweep's share is counted apart (wolf: 1 swept; fox: none)");
   Check(n.Banked.Exists(e=>e.Destination==MasteryDestination.Lineage && e.Id=="wolf" && e.Stat==MasteryStat.Dropped && e.Amount==3)
      && n.Banked.Exists(e=>e.Destination==MasteryDestination.Lineage && e.Id=="fox" && e.Stat==MasteryStat.Dropped && e.Amount==1),
      "banked as NightBanked(Lineage, type, Dropped)");
   Check(n.Banked.Exists(e=>e.Destination==MasteryDestination.Lineage && e.Id=="wolf" && e.Stat==MasteryStat.Swept && e.Amount==1)
      && !n.Banked.Exists(e=>e.Id=="fox" && e.Stat==MasteryStat.Swept),"the sweep's share banks as NightBanked(Lineage, type, Swept), only when non-zero");
   Check(prog.Profile.Robots["wolf"].Swept==1 && prog.Profile.Robots["wolf"].KnockedInPlay==2 && prog.Profile.Robots["fox"].KnockedInPlay==1,
      "the profile keeps swept per type: wolf 3 dropped = 2 knocked in play + 1 swept");
   Check(prog.Profile.Robots["wolf"].Dropped==3 && prog.Profile.Robots["fox"].Dropped==1 && prog.Profile.TotalDropped()==4,
      "the profile keeps dropped per type; the all-time total adds them (4)");
   n.Bus.Publish(new RobotSwept(Spawn("wolf")));
   n.Bus.Publish(new RobotLostGrip(Spawn("wolf"),chain,Attribution.FromThrowable(stone)));
   Check(n.St.Dropped["wolf"]==3 && n.St.Swept["wolf"]==1,"after the night is banked nothing counts (a late sweep or knock)");
   t.Dispose(); prog.Dispose(); }
 { var n=new Night(new NightGoal(new[]{5000},1,0)); var t=new MasteryTally(n.Bus,n.St);
   var w=n.Ids.Next(); n.Bus.Publish(new RobotSpawned(w,"wolf")); n.Wall.Add(w);
   n.Play(0);   // out of stones: the loss sweeps too
   Check(n.Ends.Count==1 && n.St.Dropped["wolf"]==1 && n.St.Swept["wolf"]==1,"a lost night's sweep counts as dropped (and swept) too (it scores nothing)");
   t.Dispose(); }

 // --- The all-time records ---
 { var bus=new EventBus(); var prog=new Progression(bus,null);
   var first=prog.RecordNight(620,9,2,4100);
   Check(first.BestThrow && first.LongestChain && first.DeepestChain && first.BestNightScore && first.Any,"the first night sets every non-zero record (all NEW)");
   var second=prog.RecordNight(620,10,1,0);
   Check(!second.BestThrow && second.LongestChain && !second.DeepestChain && !second.BestNightScore,"NEW only when beaten: a tie (620) isn't, 10 > 9 is, lower ones aren't");
   var r=prog.Profile.Records;
   Check(r.BestThrow==620 && r.LongestChain==10 && r.DeepestChain==2 && r.BestNightScore==4100,"records keep the best of each (620, 10, 2, 4100)");
   Check(!prog.RecordNight(0,0,0,0).Any,"an empty night breaks nothing");
   prog.Dispose(); }

 // --- The save: dropped, records, startInNight (additive, v1) ---
 { var p=new PlayerProfile(); p.Robot("wolf").Dropped=950; p.Robot("wolf").Swept=210; p.Records.BestThrow=620; p.Records.LongestChain=9; p.Records.DeepestChain=3;
   p.Records.BestNightScore=4100; p.StartInNight=true;
   var text=ProfileJson.Write(p);
   var ok=ProfileJson.Read(text,out var back,out var problem)==ProfileReadResult.Ok;
   Check(ok && back.Robots["wolf"].Dropped==950 && back.Robots["wolf"].Swept==210 && back.Records.BestThrow==620 && back.Records.LongestChain==9 && back.Records.DeepestChain==3
         && back.Records.BestNightScore==4100 && back.StartInNight,"save round trip: dropped, swept, the four records and startInNight come back");
   ok=ProfileJson.Read("{\"version\":1,\"robots\":{\"wolf\":{\"ballKnocks\":3}},\"campaign\":{\"night\":1}}",out var old,out problem)==ProfileReadResult.Ok;
   Check(ok && old.Robots["wolf"].Dropped==0 && old.Robots["wolf"].Swept==0 && !old.Records.Any && !old.StartInNight,"an older save (no dropped / records / startInNight) reads as 0 / none / false");
   Check(ProfileJson.Read("{\"version\":1,\"records\":{\"bestThrow\":-5}}",out _,out problem)==ProfileReadResult.Corrupt,"a negative record is corrupt");
   Check(ProfileJson.Read("{\"version\":1,\"records\":[]}",out _,out problem)==ProfileReadResult.Corrupt,"records that aren't an object are corrupt");
   Check(ProfileJson.Read("{\"version\":1,\"campaign\":{\"startInNight\":1}}",out _,out problem)==ProfileReadResult.Corrupt,"startInNight that isn't true/false is corrupt");
   var copy=ProfileJson.Copy(p); copy.Robot("wolf").Dropped=1; copy.Records.BestThrow=1; copy.Peg("peg_bouncy").Triggers.Add(5);
   Check(p.Robots["wolf"].Dropped==950 && p.Records.BestThrow==620 && !p.Pegs.ContainsKey("peg_bouncy"),"Copy is separate: changing the copy leaves the original alone"); }
 { var bus=new EventBus(); var prog=new Progression(bus,null); prog.SetStartInNight(true); prog.SetCurrentNight(2); prog.ResetCampaign();
   Check(!prog.Profile.StartInNight && prog.Profile.CurrentNight==0,"Reset campaign also clears a pending fast Retry"); prog.Dispose(); }

 // --- CampaignPlan: which night unlocks a type ---
 { var plan=new CampaignPlan(new[]{"n1","n2","n3"},new[]{"peg_bomb","peg_splitter",null},new[]{"peg_bouncy"});
   Check(plan.UnlockNightOf("peg_bomb")==1 && plan.UnlockNightOf("peg_splitter")==2 && plan.UnlockNightOf("peg_bouncy")==0 && plan.UnlockNightOf("x")==0,
     "UnlockNightOf: bomb by night 1, splitter by night 2; a starting / unknown type: 0"); }

 // --- The PROGRESS rows: only what moved ---
 { var plan=new CampaignPlan(new[]{"n1","n2","n3"},new[]{"peg_bomb","peg_splitter",null},new[]{"peg_bouncy"});
   var types=new[]{"peg_bouncy","peg_bomb","peg_splitter"};
   Func<string,PegProgression> rule=id=>id=="peg_unknown"?null:new PegProgression(new[]{4,10,20},null,1,10,3);
   var stones=new StoneProgression(new[]{10,20,30,40},10,25);
   var before=new PlayerProfile(); before.Weapon("stone").DirectHits=15; before.Peg("peg_bouncy").Triggers.Add(2);
   before.Robot("wolf").Dropped=100; before.Records.BestThrow=500;
   // Tonight: 7 stone hits (15 -> 22: a stone earned), Bouncy fired twice (2 -> 4: a copy earned), the dawn on night 1 unlocked the
   // Bomb (never fired), 12 wolves dropped, a new best throw.
   var after=ProfileJson.Copy(before); after.Weapon("stone").DirectHits=22; after.Peg("peg_bouncy").Triggers[0]=4;
   after.Dawns["n1"]=1; after.Robot("wolf").Dropped=106; after.Robot("fox").Dropped=6; after.Records.BestThrow=620; after.Records.DeepestChain=2;
   var rep=PostRunProgress.Build(before,after,"stone",stones,plan,types,rule);
   var s=rep.Stone;
   Check(s.HitsGained==7 && s.Before.Stones==11 && s.After.Stones==12 && s.Bar.Ready && s.Bar.After==1f && Math.Abs(s.Bar.Before-0.5f)<1e-5f,
     $"stone row: +7 hits, 11 -> 12 stones, the bar from 50% (15 of 10→20) to full, READY (got before {s.Bar.Before})");
   Check(rep.Pegs.Count==2 && rep.Pegs[0].PegId=="peg_bouncy" && rep.Pegs[1].PegId=="peg_bomb","peg rows: only the type that fired (Bouncy) and the one unlocked tonight (Bomb), campaign order; Splitter (locked) has none");
   var bouncy=rep.Pegs[0];
   Check(!bouncy.UnlockedTonight && bouncy.TriggersTonight==2 && bouncy.CopiesBefore==1 && bouncy.CopiesAfter==2 && bouncy.Bar.Ready && Math.Abs(bouncy.Bar.Before-0.5f)<1e-5f,
     "Bouncy: fired 2×, copies 1 -> 2, the bar from 50% (2 of 4) to full, READY");
   var bomb=rep.Pegs[1];
   Check(bomb.UnlockedTonight && bomb.UnlockedByNight==1 && bomb.CopiesBefore==0 && bomb.CopiesAfter==1 && !bomb.Bar.Ready && bomb.Bar.After==0f,
     "Bomb: NEW (unlocked by dawn on night 1), copies 0 -> 1, an empty bar, not READY");
   Check(bouncy.After.NextFollowUpAt==3 && bouncy.InARowAtNext==2,"the next follow-up step: at 3 copies, 2 in a row");
   Check(rep.WolvesTonight==12 && rep.WolvesTotal==112,"wolves: +12 tonight (any type), 112 all-time");
   Check(rep.Records.New.BestThrow && rep.Records.New.DeepestChain && !rep.Records.New.LongestChain && !rep.Records.New.BestNightScore
         && rep.Records.Before.BestThrow==500 && rep.Records.After.BestThrow==620,"records: NEW where tonight beat the record from before tonight (best throw, deepest chain)");
   // A night where nothing moved: no peg rows, the stone's bar doesn't grow, nothing READY.
   var same=PostRunProgress.Build(before,ProfileJson.Copy(before),"stone",stones,plan,types,rule);
   Check(same.Pegs.Count==0 && same.Stone.HitsGained==0 && same.Stone.Bar.Gain==0f && !same.Stone.Bar.Ready && same.WolvesTonight==0 && !same.Records.New.Any,
     "nothing moved: no peg rows, no gain, no READY, no NEW");
   // A partial gain: the bar grows but isn't complete.
   var partial=ProfileJson.Copy(before); partial.Weapon("stone").DirectHits=18; partial.Peg("peg_bouncy").Triggers[0]=3;
   var pr=PostRunProgress.Build(before,partial,"stone",stones,plan,types,rule);
   Check(Math.Abs(pr.Stone.Bar.After-0.8f)<1e-5f && !pr.Stone.Bar.Ready && pr.Pegs.Count==1 && Math.Abs(pr.Pegs[0].Bar.After-0.75f)<1e-5f && !pr.Pegs[0].Bar.Ready,
     "a partial gain: stone 50% -> 80%, Bouncy 50% -> 75%, neither READY");
   // [3] and [3, 0] are the same triggers: a merged level with no triggers doesn't count as "moved".
   var padded=ProfileJson.Copy(before); padded.Peg("peg_bouncy").Triggers.Add(0);
   Check(PostRunProgress.Build(before,padded,"stone",stones,plan,types,rule).Pegs.Count==0,"triggers [2] vs [2, 0]: nothing moved, no row");
   Check(PostRunProgress.Build(before,after,"stone",null,null,types,rule).Stone==null && PostRunProgress.Build(before,after,"stone",null,null,types,rule).Pegs.Count==0,
     "no stone rule / no campaign plan: no stone row, no peg rows (Night.unity)"); }
 // --- Records rows: how close tonight came (M10.S) ---
 { Check(Math.Abs(RecordsReport.Closeness(310,620)-0.5f)<1e-5f && RecordsReport.Closeness(620,620)==1f && RecordsReport.Closeness(700,620)==1f,
     "records bar: tonight ÷ the record (310 of 620 = half); tied or broken = full");
   Check(RecordsReport.Closeness(0,620)==0f && RecordsReport.Closeness(5,0)==1f && RecordsReport.Closeness(0,0)==0f,"nothing tonight = empty; a first record = full");
   var before=new PlayerProfile(); before.Records.BestThrow=620; var after=ProfileJson.Copy(before); after.Records.LongestChain=9;
   var tonight=new NightRecords{ BestThrow=310, LongestChain=9 };
   var rep=PostRunProgress.Build(before,after,"stone",null,null,null,null,tonight);
   Check(rep.Records.Tonight.BestThrow==310 && rep.Records.Tonight.LongestChain==9 && rep.Records.New.LongestChain && !rep.Records.New.BestThrow,
     "the report carries tonight's values next to the records (and NEW where beaten)");
   Check(PostRunProgress.Build(before,after,"stone",null,null,null,null).Records.Tonight.BestThrow==0,"no tonight given: zeros"); }
 { var bar=new ProgressBar(0.7f,0.4f,false);
   Check(bar.Before==0.7f && bar.After==0.7f && bar.Gain==0f,"a bar never runs backwards (after below before → before)");
   var ready=new ProgressBar(0.3f,0.2f,true);
   Check(ready.After==1f && Math.Abs(ready.Gain-0.7f)<1e-5f,"READY = full"); }

 // --- The buttons: "To the barn" and "Next night" never together ---
 { // End to end on a 3-night campaign: the inputs NightFlow feeds PostRunChoices (a dawn, CampaignPlan.CanGoNext).
   var plan=new CampaignPlan(new[]{"n1","n2","n3"}); var prof=new PlayerProfile(); prof.Dawns["n2"]=1; prof.Dawns["n3"]=1;
   PostRunChoices.For(true,plan.CanGoNext(prof,1),out var mid,out var midSecond);
   PostRunChoices.For(true,plan.CanGoNext(prof,2),out var last,out var lastSecond);
   Check(mid==PostRunAction.NextNight && midSecond==PostRunAction.Retry,"campaign: a dawn on night 2 of 3 → Next night + Retry");
   Check(last==PostRunAction.ToBarn && lastSecond==PostRunAction.Retry,"campaign: a dawn on the LAST night (3 of 3) → To the barn + Retry, no Next night"); }
 { PostRunChoices.For(false,false,out var p1,out var s1); PostRunChoices.For(false,true,out var p2,out var s2);
   PostRunChoices.For(true,true,out var p3,out var s3); PostRunChoices.For(true,false,out var p4,out var s4);
   Check(p1==PostRunAction.Retry && s1==PostRunAction.ToBarn && p2==PostRunAction.Retry && s2==PostRunAction.ToBarn,"a loss: Retry (primary) + To the barn");
   Check(p3==PostRunAction.NextNight && s3==PostRunAction.Retry,"a dawn: Next night (primary) + Retry");
   Check(p4==PostRunAction.ToBarn && s4==PostRunAction.Retry,"a dawn on the last night: To the barn (primary) + Retry");
   bool never=true;
   foreach(var dawn in new[]{false,true}) foreach(var next in new[]{false,true}){
     PostRunChoices.For(dawn,next,out var a,out var b);
     if((a==PostRunAction.ToBarn && b==PostRunAction.NextNight) || (a==PostRunAction.NextNight && b==PostRunAction.ToBarn) || a==b) never=false; }
   Check(never,"never To the barn and Next night together, never the same button twice"); }

 // --- The hour dots: exactly one per hour + one for dawn (the bug: 7 + the reached hours again) ---
 { var lost=HourDots.For(7,2,false); int lit=0; foreach(var d in lost) if(d.Lit) lit++;
   Check(lost.Count==8 && lit==2 && lost[0].Lit && lost[1].Lit && !lost[2].Lit && lost[7].IsDawn && !lost[7].Lit && lost[7].Hour==8,
     $"dots (7 hours, reached 2, no dawn): 8 dots, hours 1-2 lit, the dawn dot dim (got {lost.Count} dots, {lit} lit)");
   var won=HourDots.For(7,7,true); bool all=true; foreach(var d in won) all&=d.Lit;
   Check(won.Count==8 && all && won[7].IsDawn && !won[6].IsDawn,"dots (7 hours, dawn): 8 dots, all lit, the last is dawn's (gold)");
   Check(HourDots.For(7,99,false).Count==8 && HourDots.For(7,-1,false)[0].Lit==false && HourDots.For(0,0,false).Count==2,
     "dots: reached clamped; never more than hours + 1"); }
 // --- Hour colours by night progress: the palette over the hours + dawn (dawn = the last colour, post_run_v5) ---
 Check(PaletteSampling.Position(1,6,7)==0f && PaletteSampling.Position(6,6,7)==5f && PaletteSampling.Position(7,6,7)==6f && PaletteSampling.Dawn(7)==6f,
   "a 6-hour night on 7 colours: one each (hour 1 = the first, hour 6 = the 6th), dawn = the 7th (the gold)");
 Check(PaletteSampling.Position(1,5,7)==0f && Math.Abs(PaletteSampling.Position(5,5,7)-4.8f)<1e-5f && PaletteSampling.Position(6,5,7)==6f,
   "a 5-hour night on 7 colours: hour 5 blends just short of the gold (4.8), dawn is the gold");
 Check(PaletteSampling.Position(0,5,7)==0f && PaletteSampling.Position(9,5,7)==6f && PaletteSampling.Position(1,1,7)==0f && PaletteSampling.Position(3,5,1)==0f,
   "clamped: before hour 1 / past dawn / a one-hour night's hour 1 / one colour");
 Check(PaletteSampling.Position(1,1,7)==0f && PaletteSampling.Position(2,1,7)==6f,"a one-hour night: hour 1 the first colour, dawn the last");
 Check(PaletteSampling.Position(7,7,8)==6f && PaletteSampling.Dawn(8)==7f && PaletteSampling.Position(99,7,8)==7f,
   "8 colours, 7 hours: hour 7 = the 7th, dawn = the 8th (gold); past dawn is clamped to the last, never wrapped");
}
}
