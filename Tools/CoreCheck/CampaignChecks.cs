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
   n.Play(1);                 // hour 1: 10 points — not the threshold yet
   n.Breach();                // hour 1: a breach (takes a stone)
   n.Play(2);                 // hour 1: 10 + 20 = 30 → crosses 30; the round starts when it lands
   Check(n.St.Phase==NightPhase.PegPlacement,"setup: the first threshold's round");
   n.Ref.EndPlacement();      // hour 2
   n.Play(3);                 // hour 2: (10 + 20 + 30) × 1.5 = 90
   n.Breach();
   Check(n.St.Hours.Count==2 && n.St.Hours[0].Score==40 && n.St.Hours[1].Score==90,$"score per hour, by the hour the chain was thrown in (40, 90; got {n.St.Hours[0].Score}, {(n.St.Hours.Count>1?n.St.Hours[1].Score:-1)})");
   Check(n.St.Hours[0].Breaches==1 && n.St.Hours[1].Breaches==1,"breaches per hour (1, 1)");
   Check(n.St.BestThrowPoints==90 && n.St.BestThrowHour==2,"best throw tonight: 90 points, thrown in hour 2"); }

 // --- The hour's gap and a throw's quality ---
 { var g=new NightGoal(new[]{100,250,450});
   Check(g.HourGap(1)==100 && g.HourGap(2)==150 && g.HourGap(3)==200,"gap of hour n = T(n) − T(n−1): 100, 150, 200");
   Check(g.HourGap(0)==100 && g.HourGap(9)==200,"gaps outside the hours: the first / the last");
   Check(Near(g.ThrowQuality(150,2),1f) && Near(g.ThrowQuality(15,1),0.15f) && g.ThrowQuality(0,1)==0f && g.ThrowQuality(-5,1)==0f,
         "quality = points ÷ the gap of the hour it was thrown in (150 in hour 2 = 1.0; 15 in hour 1 = 0.15; 0 → 0)"); }

 // --- Peg triggers: per peg id, per merged level, banked and applied ---
 { var bouncy=new PegType("peg_bouncy",3,true,PegEffect.Bouncy,new[]{2f,3f});
   var splitter=new PegType("peg_splitter",3,true,PegEffect.Splitter,null,new[]{2,3},new[]{1f,1f},4,true);
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
   fx.Hit(0,PegHitter.Stone,s,c);                                  // a stone on Bouncy: no bonus, no trigger
   fx.Hit(2,PegHitter.Stone,s,c);                                  // a level-2 split
   fx.Hit(3,PegHitter.Stone,s,c);                                  // an explosion
   fx.Hit(3,PegHitter.Stone,s,c);                                  // spent: no trigger
   var trig=n.St.PegTriggers;
   Check(trig["peg_bouncy"].Count==2 && trig["peg_bouncy"][0]==1 && trig["peg_bouncy"][1]==1,"Bouncy triggers per level: L1 1 (once per ball), L2 1; a stone's hit isn't one");
   Check(trig["peg_splitter"].Count==2 && trig["peg_splitter"][0]==0 && trig["peg_splitter"][1]==1,"a split at level 2 counts at level 2");
   Check(trig["peg_bomb"].Count==1 && trig["peg_bomb"][0]==1,"an explosion counts once; a spent bomb's hit doesn't");
   n.Bus.Publish(new ThrowableRemoved(s,c)); n.Bus.Publish(new RobotRemoved(r,c,RemovalReason.HitGround));
   n.Breach();   // caught (one stone, already thrown)
   Check(n.Banked.Exists(b=>b.Destination==MasteryDestination.Peg && b.Id=="peg_bouncy" && b.Stat==MasteryStat.PegTriggers && b.Level==2 && b.Amount==1),
         "banked per level: NightBanked(Peg, peg_bouncy, PegTriggers, 1, level 2) — on a caught night too");
   Check(prog.Profile.Pegs["peg_bouncy"].TriggersAt(1)==1 && prog.Profile.Pegs["peg_bouncy"].TriggersAt(2)==1 && prog.Profile.Pegs["peg_splitter"].TriggersAt(2)==1,
         "the profile keeps triggers per level");
   fx.Dispose(); t.Dispose(); prog.Dispose(); }

 // --- Stones: start 10, +1 per threshold, evolve at 15 (refill 2), cap 20 ---
 { var th=new[]{10,20,30,40,50,60,70,80,90,100};
   var sp=new StoneProgression(th,10,15,20,1,2);
   var a=sp.For(0);
   Check(a.Stones==10 && !a.Evolved && a.Level==1 && a.Refill==1 && a.NextThreshold==10 && a.PreviousThreshold==0,"0 hits: 10 stones, level 1, refill 1, next +1 at 10");
   var b=sp.For(49);
   Check(b.Stones==14 && !b.Evolved && b.NextThreshold==50 && Near(b.Progress(49),0.9f),"49 hits: 14 stones, 0.9 of the way to the 15th");
   var e=sp.For(50);
   Check(e.Stones==15 && e.Evolved && e.Level==2 && e.Refill==2,"50 hits: the 15th stone — evolved: level 2, refill 2");
   var m=sp.For(100); var big=sp.For(999999);
   Check(m.Stones==20 && m.AtCap && m.NextThreshold==-1 && Near(m.Progress(100),1f) && big.Stones==20,"100 hits: 20 stones = the cap; more hits change nothing");
   var shortList=new StoneProgression(new[]{10},10,15,20).For(500);
   Check(shortList.Stones==11 && shortList.AtCap,"fewer thresholds than room: the list ends the progression (11, done)");
   var lowCap=new StoneProgression(th,10,15,12).For(500);
   Check(lowCap.Stones==12 && lowCap.AtCap && !lowCap.Evolved,"a lower cap wins over a longer list (12 stones, never evolves)"); }

 // --- Peg copies: 1 when unlocked, +1 per weighted-mastery threshold, max 4 ---
 { var pp=new PegProgression(new[]{5,15,30});
   var s0=pp.For(null);
   Check(s0.Copies==1 && s0.NextThreshold==5 && Near(s0.Mastery,0f),"no triggers: 1 copy, next at 5");
   var s1=pp.For(new[]{3,1});
   Check(Near(s1.Mastery,5f) && s1.Copies==2 && s1.NextThreshold==15 && s1.PreviousThreshold==5,"weight = level by default: 3×1 + 1×2 = 5 → 2 copies, next at 15");
   var s2=new PegProgression(new[]{5,15,30},new[]{1f,3f}).For(new[]{3,1});
   Check(Near(s2.Mastery,6f),"custom weights per level (1, 3): 3 + 3 = 6");
   var s3=pp.For(new[]{100});
   Check(s3.Copies==4 && s3.AtMax && Near(s3.Progress,1f),"max 4 copies, then the bar is full");
   Check(Near(pp.For(new[]{10}).Progress,0.5f),"progress from the last copy's threshold: 10 of 5 → 15 = 0.5"); }

 // --- The campaign: unlocks from dawns, the next night, the tower ---
 { var plan=new CampaignPlan(new[]{"night_01","night_02","night_03"},new[]{"peg_bomb","peg_splitter",null},new[]{"peg_bouncy"});
   var bus=new EventBus(); var prog=new Progression(bus,null); var p=prog.Profile;
   Check(plan.UnlockedPegs(p).Count==1 && plan.IsUnlocked(p,"peg_bouncy") && !plan.IsUnlocked(p,"peg_bomb"),"a new profile: Bouncy only");
   Check(plan.TryNextLocked(p,out var next,out int on) && next=="peg_bomb" && on==1,"next locked: Bomb, 'dawn on night 1'");
   Check(!plan.CanGoNext(p,0),"no dawn yet: no Next night");
   prog.RecordNightResult("night_01",dawn:false);
   Check(p.Dawns.Count==0,"a caught night records no dawn");
   prog.RecordNightResult("night_01",dawn:true);
   Check(plan.IsUnlocked(p,"peg_bomb") && plan.UnlockedPegs(p)[1]=="peg_bomb" && plan.CanGoNext(p,0),"dawn on night 1: Bomb unlocked, Next night offered");
   Check(plan.TryNextLocked(p,out next,out on) && next=="peg_splitter" && on==2,"next locked: Splitter, 'dawn on night 2'");
   prog.RecordNightResult("night_02",dawn:true); prog.RecordNightResult("night_03",dawn:true); prog.RecordNightResult("night_03",dawn:true);
   Check(!plan.TryNextLocked(p,out _,out _) && plan.UnlockedPegs(p).Count==3 && p.DawnsOn("night_03")==2,"everything unlocked; dawns counted per night");
   Check(!plan.CanGoNext(p,2),"the last night: no Next night");
   prog.SetCurrentNight(7); Check(plan.CurrentNight(p)==2,"a saved night past the campaign lands on the last night");
   prog.SetCurrentNight(-3); Check(p.CurrentNight==0,"a negative night index → 0");
   var authored=new[]{"a","b","c"};
   Check(ReferenceEquals(CampaignPlan.TowerFor(p,"night_02",authored),authored),"no saved tower: the night's own slices");
   prog.SetTower("night_02",new[]{"a","x","c"});
   Check(CampaignPlan.TowerFor(p,"night_02",authored)[1]=="x","a saved tower: the player's choice");
   Check(ReferenceEquals(CampaignPlan.TowerFor(p,"night_02",new[]{"a","b"}),null)==false && CampaignPlan.TowerFor(p,"night_02",new[]{"a","b"}).Count==2,
         "a saved tower with a different slice count (re-authored night): the night's own");
   prog.SetTower("night_02",null); Check(!p.Towers.ContainsKey("night_02"),"clearing a tower choice");
   prog.Dispose(); }

 // --- The save: new sections round-trip; bad ones are corrupt; old saves still load ---
 { var p=new PlayerProfile();
   p.Peg("peg_bouncy").Triggers.AddRange(new[]{9,2,0}); p.Peg("peg_bouncy").Knocks=0;
   p.CurrentNight=1; p.Dawns["night_01"]=3; p.Towers["night_02"]=new System.Collections.Generic.List<string>{"slice_barn","slice_wood","slice_barn"};
   var json=ProfileJson.Write(p);
   Check(ProfileJson.Read(json,out var back,out _)==ProfileReadResult.Ok && back.CurrentNight==1 && back.DawnsOn("night_01")==3
         && back.Pegs["peg_bouncy"].TriggersAt(2)==2 && back.Towers["night_02"][1]=="slice_wood" && ProfileJson.Write(back)==json,
         "round trip: night, dawns, triggers per level, towers");
   bool Bad(string body)=>ProfileJson.Read("{\"version\":1,"+body+"}",out _,out _)==ProfileReadResult.Corrupt;
   Check(Bad("\"pegs\":{\"peg_bomb\":{\"triggers\":[1,-2]}}") && Bad("\"pegs\":{\"peg_bomb\":{\"triggers\":3}}"),"bad triggers (negative, not a list) are corrupt");
   Check(Bad("\"campaign\":{\"night\":-1}") && Bad("\"campaign\":{\"dawns\":{\"night_01\":\"x\"}}") && Bad("\"campaign\":[]"),"a bad campaign section is corrupt");
   Check(Bad("\"towers\":{\"night_01\":[1]}") && Bad("\"towers\":{\"night_01\":\"a\"}") && Bad("\"towers\":{\"night_01\":[\"\"]}"),"a bad tower (not a list of ids) is corrupt");
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
partial class P{
static void ResetCampaignChecks(){
 var bus=new EventBus(); var prog=new Progression(bus,null); var p=prog.Profile;
 p.Weapon("stone").DirectHits=120; p.Peg("peg_bouncy").Triggers.Add(5);
 prog.SetCurrentNight(2); prog.RecordNightResult("night_01",true); prog.SetTower("night_02",new[]{"a","b","c"});
 prog.ResetCampaign();
 Check(p.CurrentNight==0 && p.Dawns.Count==0 && p.Towers.Count==0,"Reset campaign: night 1, no dawns, no tower choices");
 Check(p.DirectHits("stone")==120 && p.Pegs["peg_bouncy"].TriggersAt(1)==5,"...mastery kept (hits, peg triggers)");
 prog.Dispose();
}
}
