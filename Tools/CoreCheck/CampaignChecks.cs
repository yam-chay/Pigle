using System;
using System.IO;
using Piglings.Events; using Piglings.Meta; using Piglings.Rules; using Piglings.Runtime; using Piglings.Simulation;

// Prototype v2, PR A: the pre-night phase, per-hour stats, throw quality, peg triggers, and the campaign's derived
// progression (stones, peg copies, unlocks) + the save's new sections and separate profiles.
partial class P{
static void CampaignChecks(){
 // --- Dusk: the night waits until Begin ---
 { var bus=new EventBus(); var st=new NightState(); var tr=new ChainTracker(bus,st);
   var phases=new System.Collections.Generic.List<NightPhaseChanged>(); bus.Subscribe<NightPhaseChanged>(e=>phases.Add(e));
   var stones=new System.Collections.Generic.List<StonesChanged>(); bus.Subscribe<StonesChanged>(e=>stones.Add(e));
   var rf=new NightReferee(bus,st,tr,new NightGoal(new[]{100},5,0),null,startImmediately:false);
   Check(st.Phase==NightPhase.Dusk && !st.CanThrow && !st.WallMoving,"campaign start: Dusk — no throwing, the wall doesn't move");
   bus.Publish(new RobotBreached(new GameId(999)));
   Check(st.StonesLeft==5 && stones.Count==0 && !st.Ended,"a breach in Dusk (shouldn't happen) takes nothing and catches no one");
   rf.Begin();
   Check(st.Phase==NightPhase.Running && st.CanThrow && st.WallMoving && phases.Count==1 && phases[0].From==NightPhase.Dusk && phases[0].To==NightPhase.Running,
         "Begin(): Dusk → Running (NightPhaseChanged), the pig may throw");
   rf.Begin();
   Check(phases.Count==1,"Begin() again: nothing (only from Dusk)");
   var rf2=new NightReferee(new EventBus(),new NightState(),new ChainTracker(new EventBus(),new NightState()));
   rf2.Begin(); rf.Dispose(); rf2.Dispose(); tr.Dispose(); }
 { var st=new NightState(); new NightReferee(new EventBus(),st,new ChainTracker(new EventBus(),st));
   Check(st.Phase==NightPhase.Running,"Night.unity (default): Running from the start, as before"); }

 // --- Per-hour stats and the best throw ---
 { var n=new Night(Hours(30,1000));
   n.Play(1);                 // hour 1: 20 points — not the threshold yet
   n.Breach();                // hour 1: a breach (takes a stone)
   n.Play(2);                 // hour 1: + 40 = 60 → crosses 30; the round starts when it lands
   Check(n.St.Phase==NightPhase.PegPlacement,"setup: the first threshold's round");
   n.Ref.EndPlacement();      // hour 2
   n.Play(3);                 // hour 2: 60 × 1.5 = 90 (60 raw as it falls, the remainder 30 at the close)
   n.Breach();
   Check(n.St.Hours.Count==2 && n.St.Hours[0].Score==60 && n.St.Hours[1].Score==90,$"score per hour, by the hour the chain was thrown in, raw + remainder (60, 90; got {n.St.Hours[0].Score}, {(n.St.Hours.Count>1?n.St.Hours[1].Score:-1)})");
   Check(n.St.Hours[0].Breaches==1 && n.St.Hours[1].Breaches==1,"breaches per hour (1, 1)");
   Check(n.St.BestThrowPoints==90 && n.St.BestThrowHour==2,"best throw tonight: 90 points, thrown in hour 2"); }

 // --- The hour's gap and a throw's quality ---
 { var g=new NightGoal(new[]{100,250,450});
   Check(g.HourGap(1)==100 && g.HourGap(2)==150 && g.HourGap(3)==200,"gap of hour n = T(n) − T(n−1): 100, 150, 200");
   Check(g.HourGap(0)==100 && g.HourGap(9)==200,"gaps outside the hours: the first / the last");
   Check(Near(g.ThrowQuality(150,2),1f) && Near(g.ThrowQuality(15,1),0.15f) && g.ThrowQuality(0,1)==0f && g.ThrowQuality(-5,1)==0f,
         "quality = points ÷ the gap of the hour it was thrown in (150 in hour 2 = 1.0; 15 in hour 1 = 0.15; 0 → 0)"); }

 // --- Peg triggers: per peg id, per merged level, banked and applied ---
 { var bouncy=new PegType("peg_bouncy",3,true,PegEffect.Bouncy,new[]{1f,2f});
   var splitter=new PegType("peg_splitter",3,true,PegEffect.Splitter,null,new[]{2,3},4,true);
   var bomb=new PegType("peg_bomb",3,true,PegEffect.Bomb,cooldowns:new[]{5f});
   var pegs=new PegSetup(new[]{(bouncy,2),(splitter,2),(bomb,1)},1,4);
   var n=new Night(new NightGoal(new[]{100000},1,0),null,pegs); var fx=new PegEffects(n.Bus,n.St,n.Tr,pegs,n.Ids);
   var t=new MasteryTally(n.Bus,n.St); var prog=new Progression(n.Bus,null);
   void Put(int s,string id,int l){ n.St.Sockets[s].PegId=id; n.St.Sockets[s].Level=l; }
   Put(0,"peg_bouncy",1); Put(1,"peg_bouncy",2); Put(2,"peg_splitter",2); Put(3,"peg_bomb",1);
   var c=new ChainId(n.Ids.Next()); var s=n.Ids.Next(); n.Bus.Publish(new ThrowReleased(c,s,"stone"));
   var r=n.Ids.Next(); n.Bus.Publish(new RobotLostGrip(r,c,Attribution.FromThrowable(s)));
   fx.Hit(0,PegHitter.Ball,r,c); fx.Hit(0,PegHitter.Ball,r,c);   // once per peg per ball: 1 trigger
   fx.Hit(1,PegHitter.Ball,r,c);                                   // a level-2 Bouncy
   fx.Hit(0,PegHitter.Stone,s,c);                                  // a stone on Bouncy: a trigger too (M10.S)
   fx.Hit(2,PegHitter.Stone,s,c);                                  // a level-2 split
   fx.Hit(3,PegHitter.Stone,s,c);                                  // an explosion
   fx.Hit(3,PegHitter.Stone,s,c);                                  // spent: no trigger
   var trig=n.St.PegTriggers;
   Check(trig["peg_bouncy"].Count==2 && trig["peg_bouncy"][0]==2 && trig["peg_bouncy"][1]==1,"Bouncy triggers per level: L1 2 (once per ball + once for the stone), L2 1");
   Check(trig["peg_splitter"].Count==2 && trig["peg_splitter"][0]==0 && trig["peg_splitter"][1]==1,"a split at level 2 counts at level 2");
   Check(trig["peg_bomb"].Count==1 && trig["peg_bomb"][0]==1,"an explosion counts once; a spent bomb's hit doesn't");
   n.Bus.Publish(new ThrowableRemoved(s,c)); n.Bus.Publish(new RobotRemoved(r,c,RemovalReason.HitGround));
   Check(n.Banked.Exists(b=>b.Destination==MasteryDestination.Peg && b.Id=="peg_bouncy" && b.Stat==MasteryStat.PegTriggers && b.Level==2 && b.Amount==1),
         "banked per level: NightBanked(Peg, peg_bouncy, PegTriggers, 1, level 2) — on a lost night too");
   Check(prog.Profile.Pegs["peg_bouncy"].TriggersAt(1)==2 && prog.Profile.Pegs["peg_bouncy"].TriggersAt(2)==1 && prog.Profile.Pegs["peg_splitter"].TriggersAt(2)==1,
         "the profile keeps triggers per level");
   fx.Dispose(); t.Dispose(); prog.Dispose(); }

 // --- Stones: start 10, +1 per Levels entry, the last entry = the cap (25); evolution and refill from the evolutions, keyed by stone level (R2) ---
 { var th=new[]{10,20,30,40,50,60,70,80,90,100,110,120,130,140,150};
   var sp=new StoneProgression(th,10);   // the default evolutions: at levels 1 / 6 / 11 / 16 = 10 / 15 / 20 / 25 stones, +1…+4
   Check(sp.MaxStones==25 && sp.MaxStoneLevel==16,"15 Levels entries from 10 stones: the cap is 25 stones, stone level 16 (no separate max)");
   var a=sp.For(0);
   Check(a.StoneLevel==1 && sp.StonesAtLevel(6)==15,"0 hits: stone level 1; level 6 = 15 stones");
   Check(a.Stones==10 && a.Level==1 && a.Refill==1 && a.NextThreshold==10 && a.PreviousThreshold==0,"0 hits: 10 stones, level 1, refill 1, next +1 at 10");
   Check(a.NextEvolutionStones==15 && a.NextEvolutionLevel==2,"0 hits: the next evolution is level 2 at 15 stones");
   var b=sp.For(49);
   Check(b.Stones==14 && b.Level==1 && b.NextThreshold==50 && Near(b.Progress(49),0.9f),"49 hits: 14 stones, still level 1, 0.9 of the way to the 15th");
   var e=sp.For(50);
   Check(e.Stones==15 && e.Level==2 && e.Refill==2 && e.NextEvolutionStones==20,"50 hits: the 15th stone — level 2, refill 2; level 3 at 20");
   var l3=sp.For(100); var l4=sp.For(150);
   Check(l3.Stones==20 && l3.Level==3 && l3.Refill==3,"100 hits: 20 stones — level 3, refill 3");
   Check(l4.Stones==25 && l4.StoneLevel==16 && l4.Level==4 && l4.Refill==4 && l4.AtCap && l4.NextEvolutionStones==-1 && l4.NextEvolutionLevel==0,
         "150 hits: 25 stones = the cap — level 4, refill 4, no evolution left");
   Check(sp.For(999999).Stones==25,"more hits change nothing past the cap");
   var shortList=new StoneProgression(new[]{10},10).For(500);
   Check(shortList.Stones==11 && shortList.AtCap,"fewer thresholds than room: the list ends the progression (11, done)");
   var lowCap=new StoneProgression(new[]{10,20},10).For(500);
   Check(lowCap.Stones==12 && lowCap.AtCap && lowCap.Level==1 && lowCap.NextEvolutionStones==-1,
         "a short Levels list caps early (12 stones: evolution 1 for good, the next evolution — level 6 — out of reach)");
   var custom=new StoneProgression(th,5,new[]{new StoneEvolution(1,1),new StoneEvolution(4,3)});
   Check(custom.For(0).Level==1 && custom.For(30).StoneLevel==4 && custom.For(30).Stones==8 && custom.For(30).Level==2 && custom.For(30).Refill==3,
         "a custom list: evolution 1 at level 1 (+1), evolution 2 at level 4 = 8 stones from a start of 5 (+3)");
   var none=new StoneProgression(th,10,new StoneEvolution[0]).For(500);
   Check(none.Level==1 && none.Refill==1,"an empty list: one level, refill 1");
   Check(StoneProgression.Problem(StoneProgression.DefaultEvolutions)==null &&
         StoneProgression.Problem(new[]{new StoneEvolution(6,1),new StoneEvolution(6,2)})!=null,"evolutions must rise (the same At Level twice = a problem)"); }

 // --- Peg copies: 1 when unlocked, +1 per weighted-mastery threshold, up to Max Copies (8) ---
 { var pp=new PegProgression(new[]{5,15,30});
   var s0=pp.For(null);
   Check(s0.Copies==1 && s0.NextThreshold==5 && Near(s0.Mastery,0f),"no triggers: 1 copy, next at 5");
   var s1=pp.For(new[]{3,1});
   Check(Near(s1.Mastery,5f) && s1.Copies==2 && s1.NextThreshold==15 && s1.PreviousThreshold==5,"weight = level by default: 3×1 + 1×2 = 5 → 2 copies, next at 15");
   var s2=new PegProgression(new[]{5,15,30},new[]{1f,3f}).For(new[]{3,1});
   Check(Near(s2.Mastery,6f),"custom weights per level (1, 3): 3 + 3 = 6");
   var s3=pp.For(new[]{100});
   Check(s3.Copies==4 && s3.AtMax && Near(s3.Progress,1f),"the thresholds list ends at 4 copies: then the bar is full");
   Check(Near(pp.For(new[]{10}).Progress,0.5f),"progress from the last copy's threshold: 10 of 5 → 15 = 0.5");
   var ten=new PegProgression(new[]{1,2,3,4,5,6,7,8,9,10,11});
   Check(ten.For(new[]{100}).Copies==10 && ten.For(new[]{100}).AtMax,"no 4-copy cap any more: max 10 by default (a longer list stops there)");
   Check(new PegProgression(new[]{1,2,3,4,5,6,7,8,9},null,1,6).For(new[]{100}).Copies==6,"Max Copies is per type (6 here)"); }

 // --- Follow-ups (stage 2): one per N copies (3 by default); placing the type gives that many more of it in a row ---
 { var pp=new PegProgression(new[]{1,2,3,4,5,6,7,8,9});   // max 10 copies, a follow-up every 3
   Check(pp.For(new[]{1}).Copies==2 && pp.For(new[]{1}).FollowUps==0 && pp.For(new[]{1}).NextFollowUpAt==3,"2 copies: no follow-up yet, the first at 3");
   Check(pp.For(new[]{2}).FollowUps==1 && pp.For(new[]{2}).NextFollowUpAt==6,"3 copies: 1 follow-up, the next at 6");
   Check(pp.For(new[]{5}).FollowUps==2 && pp.For(new[]{8}).FollowUps==3,"6 copies: 2 follow-ups · 9 copies: 3");
   Check(pp.For(new[]{100}).Copies==10 && pp.For(new[]{100}).FollowUps==3 && pp.For(new[]{100}).NextFollowUpAt==-1,
         "10 copies (the max): 3 follow-ups — 4 in a row — and none left to earn (12 is past the max)");
   Check(new PegProgression(new[]{1,2,3,4,5,6,7,8,9},null,1,10,4).For(new[]{100}).FollowUps==2,"every 4 copies instead: 10 copies → 2");
   Check(new PegProgression(new[]{1,2,3,4,5,6,7,8,9},null,1,10,0).For(new[]{100}).FollowUps==0,"Follow-Up Every 0 = never"); }

 { PegSetup Setup(int sockets,int throws,params (PegType type,int count)[] shelf)=>new PegSetup(new System.Collections.Generic.List<(PegType,int)>(shelf),throws,sockets);
   // In a row: how many pegs of `id` one round lets you place, starting with it (the base throw + its chain).
   int InARow(int followUps,int copies){
    var n=new Night(Hours(50,5000),null,Setup(20,1,(new PegType("t",3,true,followUps:followUps),copies)));
    n.Play(3); int placed=0;
    for(int s=0;s<20 && n.St.Phase==NightPhase.PegPlacement;s++) if(n.Ref.PlacePeg(s,"t")) placed++;
    return placed; }
   Check(InARow(0,2)==1,"2 copies (no follow-up): 1 peg");
   Check(InARow(1,3)==2,"3 copies (1 follow-up): 2 in a row");
   Check(InARow(2,6)==3,"6 copies (2 follow-ups): 3 in a row");
   Check(InARow(3,10)==4,"10 copies (3 follow-ups): 4 in a row");
   Check(InARow(3,2)==2,"a chain ends when that type runs out: 3 follow-ups but 2 pegs → 2");

   var bouncy=new PegType("bouncy",3,true,followUps:2); var bomb=new PegType("bomb",3,true);
   var n=new Night(Hours(50,100,5000),null,Setup(10,1,(bouncy,8),(bomb,2)));
   var granted=new System.Collections.Generic.List<PegFollowUpGranted>(); n.Bus.Subscribe<PegFollowUpGranted>(e=>granted.Add(e));
   n.Play(3);
   Check(n.Ref.PlacePeg(0,"bouncy") && n.St.PegFollowUp=="bouncy" && n.St.PegFollowUpsLeft==1 && granted.Count==1 && granted[0].Remaining==1,
         "Bouncy with 2 follow-ups: the first follow-up (1 more to come)");
   Check(!n.Ref.IsValidTarget(1,"bomb") && !n.Ref.PlacePeg(1,"bomb") && n.Shelf("bomb")==2,"during the chain another type is refused (nothing used)");
   Check(n.Ref.PlacePeg(1,"bouncy") && n.St.PegFollowUp=="bouncy" && n.St.PegFollowUpsLeft==0 && granted.Count==2 && granted[1].Remaining==0,"the second follow-up");
   Check(n.Ref.PlacePeg(2,"bouncy") && n.St.Phase==NightPhase.Running && n.St.PegFollowUp==null,"the chain done: 3 Bouncy, the round ends");
   n.Play(3);
   Check(n.St.Phase==NightPhase.PegPlacement && n.Ref.PlacePeg(3,"bomb") && n.St.Phase==NightPhase.Running && granted.Count==2,
         "next round, a Bomb first (no follow-ups): 1 throw, no chain");

   var two=new Night(Hours(50,100,5000),null,Setup(10,2,(new PegType("a",3,true,followUps:1),5),(new PegType("b",3,true,followUps:1),5)));
   two.Play(3);
   Check(two.Ref.PlacePeg(0,"a") && two.Ref.PlacePeg(1,"a") && two.St.PegFollowUp==null && two.St.PegThrowsLeft==1,"2 base throws: A + its follow-up, then 1 base throw left (any type)");
   Check(two.Ref.PlacePeg(2,"b") && two.St.Phase==NightPhase.Running,"once per round: B's follow-ups don't start a second chain");
   two.Play(3);
   Check(two.Ref.PlacePeg(3,"b") && two.St.PegFollowUp=="b","a new round: a chain again");

   var full=new Night(Hours(50,5000),null,Setup(1,1,(new PegType("solo",1,false,followUps:3),4)));
   full.Play(3);
   Check(full.Ref.PlacePeg(0,"solo") && full.St.Phase==NightPhase.Running,"no follow-up when it couldn't go anywhere (no socket left): the round doesn't stall"); }

 // --- The campaign: unlocks from dawns, the next night, the tower ---
 { var plan=new CampaignPlan(new[]{"night_01","night_02","night_03"},new[]{"peg_bomb","peg_splitter",null},new[]{"peg_bouncy"});
   var bus=new EventBus(); var prog=new Progression(bus,null); var p=prog.Profile;
   Check(plan.UnlockedPegs(p).Count==1 && plan.IsUnlocked(p,"peg_bouncy") && !plan.IsUnlocked(p,"peg_bomb"),"a new profile: Bouncy only");
   Check(plan.TryNextLocked(p,out var next,out int on) && next=="peg_bomb" && on==1,"next locked: Bomb, 'dawn on night 1'");
   Check(!plan.CanGoNext(p,0),"no dawn yet: no Next night");
   prog.RecordNightResult("night_01",dawn:false);
   Check(p.Dawns.Count==0,"a lost night records no dawn");
   prog.RecordNightResult("night_01",dawn:true);
   Check(plan.IsUnlocked(p,"peg_bomb") && plan.UnlockedPegs(p)[1]=="peg_bomb" && plan.CanGoNext(p,0),"dawn on night 1: Bomb unlocked, Next night offered");
   Check(plan.TryNextLocked(p,out next,out on) && next=="peg_splitter" && on==2,"next locked: Splitter, 'dawn on night 2'");
   prog.RecordNightResult("night_02",dawn:true); prog.RecordNightResult("night_03",dawn:true); prog.RecordNightResult("night_03",dawn:true);
   Check(!plan.TryNextLocked(p,out _,out _) && plan.UnlockedPegs(p).Count==3 && p.DawnsOn("night_03")==2,"everything unlocked; dawns counted per night");
   Check(!plan.CanGoNext(p,2),"the last night: no Next night");
   prog.SetCurrentNight(7); Check(plan.CurrentNight(p)==2,"a saved night past the campaign lands on the last night");
   prog.SetCurrentNight(-3); Check(p.CurrentNight==0,"a negative night index → 0");
   prog.Dispose(); }

 // --- Slices (R3): owned from the start or unlocked by a dawn; ONE tower for the campaign, carried over between nights ---
 { var plan=new CampaignPlan(new[]{"n1","n2","n3"},null,null,new[]{"barn","wood"},
                             new System.Collections.Generic.List<System.Collections.Generic.IReadOnlyList<string>>{new[]{"straw"},new string[0],new[]{"brick","straw"}});
   var bus=new EventBus(); var prog=new Progression(bus,null); var p=prog.Profile;
   Check(plan.IsSliceUnlocked(p,"barn") && plan.IsSliceUnlocked(p,"wood") && !plan.IsSliceUnlocked(p,"straw") && !plan.IsSliceUnlocked(p,"brick"),
         "starting slices are owned; the others wait for their dawn");
   Check(string.Join(",",plan.AllSlices())=="barn,wood,straw,brick" && plan.SliceUnlockNightOf("straw")==1 && plan.SliceUnlockNightOf("brick")==3
         && plan.SliceUnlockNightOf("barn")==0,"every slice in campaign order (starting first, a repeat once); straw by night 1, brick by night 3");
   Check(string.Join(",",plan.TowerFor(p,3))=="barn,barn,barn","nothing built yet: every slot gets the first owned slice");
   prog.SetTower(new[]{"wood","barn"});
   Check(string.Join(",",plan.TowerFor(p,3))=="wood,barn,barn","a taller night keeps what was built and fills the new slot on top");
   prog.SetTower(new[]{"barn","wood","wood"}); prog.SetTower(new[]{"wood"});
   Check(string.Join(",",p.Tower)=="wood,wood,wood" && string.Join(",",plan.TowerFor(p,2))=="wood,wood",
         "a shorter night overwrites only its bottom slots: the higher ones are kept for the next taller night");
   p.Tower[1]="straw";
   Check(plan.TowerFor(p,2)[1]=="barn","a saved slice that isn't owned (yet / any more): the first owned one");
   prog.RecordNightResult("n1",dawn:true);
   Check(plan.IsSliceUnlocked(p,"straw") && plan.TowerFor(p,2)[1]=="straw" && string.Join(",",plan.UnlockedSlices(p))=="barn,wood,straw",
         "a dawn on night 1 unlocks straw: the saved slot comes back, and it's owned (in campaign order)");
   Check(new CampaignPlan(new[]{"n1"}).TowerFor(p,3).Count==0,"no owned slice at all: nothing to build (NightSession says so)");
   prog.Dispose(); }

 // --- The save: new sections round-trip; bad ones are corrupt; old saves still load ---
 { var p=new PlayerProfile();
   p.Peg("peg_bouncy").Triggers.AddRange(new[]{9,2,0}); p.Peg("peg_bouncy").Knocks=0;
   p.CurrentNight=1; p.Dawns["night_01"]=3; p.Tower.AddRange(new[]{"slice_barn","slice_wood","slice_barn"});
   var json=ProfileJson.Write(p);
   Check(ProfileJson.Read(json,out var back,out _)==ProfileReadResult.Ok && back.CurrentNight==1 && back.DawnsOn("night_01")==3
         && back.Pegs["peg_bouncy"].TriggersAt(2)==2 && back.Tower[1]=="slice_wood" && ProfileJson.Write(back)==json,
         "round trip: night, dawns, triggers per level, the tower");
   Check(ProfileJson.Read("{\"version\":1,\"towers\":{\"night_02\":[\"slice_wood\"]}}",out var oldTowers,out _)==ProfileReadResult.Ok && oldTowers.Tower.Count==0,
         "an older save's per-night towers: ignored (no tower choice yet), still a good save");
   bool Bad(string body)=>ProfileJson.Read("{\"version\":1,"+body+"}",out _,out _)==ProfileReadResult.Corrupt;
   Check(Bad("\"pegs\":{\"peg_bomb\":{\"triggers\":[1,-2]}}") && Bad("\"pegs\":{\"peg_bomb\":{\"triggers\":3}}"),"bad triggers (negative, not a list) are corrupt");
   Check(Bad("\"campaign\":{\"night\":-1}") && Bad("\"campaign\":{\"dawns\":{\"night_01\":\"x\"}}") && Bad("\"campaign\":[]"),"a bad campaign section is corrupt");
   Check(Bad("\"tower\":[1]") && Bad("\"tower\":\"a\"") && Bad("\"tower\":[\"\"]") && Bad("\"tower\":{}"),"a bad tower (not a list of ids) is corrupt");
   Check(ProfileJson.Read("{\"version\":1,\"weapons\":{\"stone\":{\"directHits\":4}}}",out var old,out _)==ProfileReadResult.Ok && old.CurrentNight==0 && old.Dawns.Count==0,
         "a save from before the campaign still loads (night 0, no dawns)"); }

 // --- Separate profiles: separate files ---
 { var dir=Path.Combine(Path.GetTempPath(),"piglings-profiles-"+Guid.NewGuid().ToString("N"));
   try{
    var dev=new ProfileFile(dir,"dev"); var camp=new ProfileFile(dir,"campaign");
    var d=dev.Load().Profile; d.Weapon("stone").DirectHits=7; dev.Save(d);
    var c=camp.Load().Profile;
    Check(dev.MainPath.EndsWith("piglings_dev.json") && camp.MainPath.EndsWith("piglings_campaign.json") && c.DirectHits("stone")==0,
          "profiles 'dev' and 'campaign' are separate files: testing in dev never touches the campaign");
    Check(new ProfileFile(dir,"../evil").ProfileName==ProfileFile.DefaultProfile && new ProfileFile(dir,"").ProfileName==ProfileFile.DefaultProfile,
          "a profile name that isn't letters/digits/-/_ (or none) → the default");
   } finally { try{ Directory.Delete(dir,true); } catch(IOException){} } }
}
}
