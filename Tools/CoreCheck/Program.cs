using System;
using Piglings.Events; using Piglings.Rules; using Piglings.Runtime; using Piglings.Simulation;
class P{static void Check(bool c,string m){Console.WriteLine((c?"PASS ":"FAIL ")+m); if(!c) Environment.ExitCode=1;}
static void Main(){
 var bus=new EventBus(); var st=new NightState(); var ids=new IdAllocator(); var tr=new ChainTracker(bus,st); ChainClosed? closed=null;
 bus.Subscribe<ChainClosed>(e=>closed=e);
 var chain=new ChainId(ids.Next()); var stone=ids.Next(); var a=ids.Next(); var b=ids.Next();
 bus.Publish(new ThrowReleased(chain,stone));
 bus.Publish(new RobotLostGrip(a,chain,Attribution.FromThrowable(stone)));
 bus.Publish(new RobotLostGrip(b,chain,Attribution.FromRobotBall(a,0)));
 bus.Publish(new ThrowableRemoved(stone,chain)); bus.Publish(new RobotRemoved(a,chain,RemovalReason.HitGround));
 Check(!closed.HasValue,"chain open while b falls");
 bus.Publish(new RobotRemoved(b,chain,RemovalReason.HitGround));
 Check(closed.HasValue && closed.Value.RobotsDropped==2 && closed.Value.MaxDepth==1,"closed 2/depth1");
 Check(st.Score==40,$"score: 10x1 + 20x1.5 = 40 (got {st.Score})");
 Action<ChainClosed> h=e=>{}; bus.Subscribe(h); bus.Unsubscribe(h); Check(true,"unsubscribe ok");
 tr.Dispose(); Check(tr.OpenChainCount==0,"dispose");
 ScoreChecks();
 NightChecks();
 ThrowChecks();
}
// Scoring: value flows down the chain. A hitter (stone or ball) carries a value; the robot it knocks
// scores value x (1 + 0.5 x depth); the hitter grows +10 per hit; the robot then carries 10 + what it received.
static void ScoreChecks(){
 var bus=new EventBus(); var st=new NightState(); var ids=new IdAllocator(); var tr=new ChainTracker(bus,st);
 var hits=new System.Collections.Generic.List<RobotScored>(); ChainScored? scored=null; int scoreSeenAtClose=-1; bool closedFirst=false;
 bus.Subscribe<RobotScored>(e=>hits.Add(e));
 bus.Subscribe<ChainClosed>(e=>{ scoreSeenAtClose=st.Score; closedFirst=!scored.HasValue; });
 bus.Subscribe<ChainScored>(e=>scored=e);
 // Line: stone -> a -> b -> c
 var chain=new ChainId(ids.Next()); var stone=ids.Next(); var a=ids.Next(); var b=ids.Next(); var c=ids.Next();
 bus.Publish(new ThrowReleased(chain,stone));
 bus.Publish(new RobotLostGrip(a,chain,Attribution.FromThrowable(stone)));
 bus.Publish(new RobotLostGrip(b,chain,Attribution.FromRobotBall(a,0)));
 bus.Publish(new RobotLostGrip(c,chain,Attribution.FromRobotBall(b,1)));
 Check(hits.Count==3 && hits[0].Received==10 && hits[1].Received==20 && hits[2].Received==30,"line: received 10, 20, 30");
 Check(MathF.Abs(hits[1].Multiplier-1.5f)<1e-5f && MathF.Abs(hits[2].Multiplier-2f)<1e-5f,"multiplier by depth x1, x1.5, x2");
 Check(hits[0].Total==10 && hits[1].Total==30 && hits[2].Total==60,"line: 10, 20x1.5=30, 30x2=60");
 Check(hits[0].Carries==20 && hits[1].Carries==30 && hits[2].Carries==40,"carries = wolf 10 + what it received: 20, 30, 40");
 Check(st.Score==100 && !scored.HasValue,"paid immediately (100), no ChainScored while open");
 bus.Publish(new ThrowableRemoved(stone,chain));
 foreach(var r in new[]{a,b,c}) bus.Publish(new RobotRemoved(r,chain,RemovalReason.HitGround));
 Check(scored.HasValue && scored.Value.RobotsDropped==3 && scored.Value.MaxDepth==2 && scored.Value.Total==100,"ChainScored = sum 100");
 Check(closedFirst && scoreSeenAtClose==100,"ChainClosed before ChainScored, nothing added at close");
 // Stone hits three directly: it grows 10 -> 20 -> 30 (all x1)
 hits.Clear(); scored=null; int before=st.Score;
 var ch2=new ChainId(ids.Next()); var s2=ids.Next(); var w=new[]{ids.Next(),ids.Next(),ids.Next()};
 bus.Publish(new ThrowReleased(ch2,s2));
 foreach(var r in w) bus.Publish(new RobotLostGrip(r,ch2,Attribution.FromThrowable(s2)));
 Check(hits[0].Total==10 && hits[1].Total==20 && hits[2].Total==30,"stone multi-hit: 10, 20, 30");
 // ...then the 2nd robot's ball (carries 10 + 20 = 30) knocks two more: 30x1.5, then (grown) 40x1.5
 var d=ids.Next(); var e2=ids.Next();
 bus.Publish(new RobotLostGrip(d,ch2,Attribution.FromRobotBall(w[1],0)));
 bus.Publish(new RobotLostGrip(e2,ch2,Attribution.FromRobotBall(w[1],0)));
 Check(hits[3].Received==30 && hits[3].Total==45 && hits[4].Received==40 && hits[4].Total==60,"ball multi-hit: 30x1.5=45, 40x1.5=60");
 bus.Publish(new ThrowableRemoved(s2,ch2)); foreach(var r in new[]{w[0],w[1],w[2],d,e2}) bus.Publish(new RobotRemoved(r,ch2,RemovalReason.HitGround));
 Check(scored.HasValue && scored.Value.Total==165 && st.Score==before+165,"mixed chain: 10+20+30+45+60 = 165");
 // Values belong to one chain: a new throw starts at 10 again
 hits.Clear(); var ch3=new ChainId(ids.Next()); var s3=ids.Next(); var f=ids.Next();
 bus.Publish(new ThrowReleased(ch3,s3)); bus.Publish(new RobotLostGrip(f,ch3,Attribution.FromThrowable(s3)));
 Check(hits.Count==1 && hits[0].Received==10 && hits[0].Order==1,"new chain starts fresh at 10");
 bus.Publish(new ThrowableRemoved(s3,ch3)); bus.Publish(new RobotRemoved(f,ch3,RemovalReason.HitGround));
 // A miss still publishes ChainScored, with zeros
 scored=null; before=st.Score; var miss=new ChainId(ids.Next()); var s4=ids.Next();
 bus.Publish(new ThrowReleased(miss,s4)); bus.Publish(new ThrowableRemoved(s4,miss));
 Check(scored.HasValue && scored.Value.RobotsDropped==0 && scored.Value.Total==0 && st.Score==before,"miss -> ChainScored 0, score unchanged");
 tr.Dispose();
 // Each setting pulls its own lever: stone 20, no growth, wolf 5, x1 per depth
 { var t=Totals(new ScoreCurve(20,0,5,1f,false), ids, out var carries);
   Check(t[0]==20 && t[1]==20 && carries[0]==25 && t[2]==50,"separate levers: stone 20, 20 (no growth); wolf adds 5 -> 25x2 = 50"); }
 // Compounding switch (Yam's first rule) still available: line 10, 40, 150
 { var bus2=new EventBus(); var st2=new NightState(); var tr2=new ChainTracker(bus2,st2,new ScoreCurve(10,10,10,1f,true)); var totals=new System.Collections.Generic.List<int>();
   bus2.Subscribe<RobotScored>(x=>totals.Add(x.Total));
   var ch=new ChainId(ids.Next()); var st0=ids.Next(); var r1=ids.Next(); var r2=ids.Next(); var r3=ids.Next();
   bus2.Publish(new ThrowReleased(ch,st0));
   bus2.Publish(new RobotLostGrip(r1,ch,Attribution.FromThrowable(st0)));
   bus2.Publish(new RobotLostGrip(r2,ch,Attribution.FromRobotBall(r1,0)));
   bus2.Publish(new RobotLostGrip(r3,ch,Attribution.FromRobotBall(r2,1)));
   Check(totals.Count==3 && totals[0]==10 && totals[1]==40 && totals[2]==150,"carryScoredTotal on: line 10, 40, 150"); tr2.Dispose(); }
 // A very deep compounding line never wraps negative
 { var bus3=new EventBus(); var st3=new NightState(); var tr3=new ChainTracker(bus3,st3,new ScoreCurve(10,10,10,1f,true));
   var ch=new ChainId(ids.Next()); var s0=ids.Next(); bus3.Publish(new ThrowReleased(ch,s0));
   var prev=ids.Next(); bus3.Publish(new RobotLostGrip(prev,ch,Attribution.FromThrowable(s0)));
   for(int dd=0;dd<20;dd++){ var next=ids.Next(); bus3.Publish(new RobotLostGrip(next,ch,Attribution.FromRobotBall(prev,dd))); prev=next; }
   Check(st3.Score==int.MaxValue,"depth-20 compounding line saturates at int.MaxValue instead of going negative"); tr3.Dispose(); }
 // The curve on its own
 Check(new ScoreCurve(5,5,5,0.5f).RobotTotal(5,1)==8,"7.5 rounds to 8");
 Check(new ScoreCurve(10,10,10,-1f).Multiplier(3)==1f,"negative multiplierPerDepth clamped to 0");
}
// Stone hits x, then y; x's ball hits z. Returns the three totals, and what x carries.
static int[] Totals(ScoreCurve curve, IdAllocator ids, out int[] carries){
 var bus=new EventBus(); var st=new NightState(); var tr=new ChainTracker(bus,st,curve);
 var t=new System.Collections.Generic.List<int>(); var cs=new System.Collections.Generic.List<int>();
 bus.Subscribe<RobotScored>(e=>{ t.Add(e.Total); cs.Add(e.Carries); });
 var ch=new ChainId(ids.Next()); var s=ids.Next(); var x=ids.Next(); var y=ids.Next(); var z=ids.Next();
 bus.Publish(new ThrowReleased(ch,s));
 bus.Publish(new RobotLostGrip(x,ch,Attribution.FromThrowable(s)));
 bus.Publish(new RobotLostGrip(y,ch,Attribution.FromThrowable(s)));
 bus.Publish(new RobotLostGrip(z,ch,Attribution.FromRobotBall(x,0)));
 tr.Dispose(); carries=cs.ToArray(); return t.ToArray();
}
// NightReferee: phases Running -> ChoicePending -> Overtime -> Ended (or Leave), loss only before the target.
class Night{
 public EventBus Bus=new EventBus(); public NightState St=new NightState(); public IdAllocator Ids=new IdAllocator();
 public ChainTracker Tr; public NightReferee Ref;
 public System.Collections.Generic.List<NightEnded> Ends=new System.Collections.Generic.List<NightEnded>();
 public System.Collections.Generic.List<NightPhase> Phases=new System.Collections.Generic.List<NightPhase>();
 public System.Collections.Generic.List<NightBanked> Banked=new System.Collections.Generic.List<NightBanked>();
 public System.Collections.Generic.List<NightTargetReached> Targets=new System.Collections.Generic.List<NightTargetReached>();
 // Robots still climbing: what Simulation's RobotSpawner would sweep when the night ends.
 public System.Collections.Generic.List<GameId> Wall=new System.Collections.Generic.List<GameId>();
 public Night(NightGoal g, ScoreCurve curve=null){
  Tr=new ChainTracker(Bus,St,curve); Ref=new NightReferee(Bus,St,Tr,g);
  Bus.Subscribe<NightEnded>(e=>Ends.Add(e)); Bus.Subscribe<NightBanked>(e=>Banked.Add(e)); Bus.Subscribe<NightTargetReached>(e=>Targets.Add(e));
  // Stand-in for RobotSpawner: on Ended, every robot still on the wall is swept (synchronously, like the real one).
  Bus.Subscribe<NightPhaseChanged>(e=>{ Phases.Add(e.To); if(e.To==NightPhase.Ended) foreach(var r in Wall) Bus.Publish(new RobotSwept(r)); });
 }
 // Throw a stone that knocks n robots directly (10, 20, 30... by default). Returns what's still in flight.
 public (ChainId chain, GameId stone, GameId[] robots) Throw(int n){
  var c=new ChainId(Ids.Next()); var s=Ids.Next(); var r=new GameId[n];
  Bus.Publish(new ThrowReleased(c,s));
  for(int i=0;i<n;i++){ r[i]=Ids.Next(); Bus.Publish(new RobotLostGrip(r[i],c,Attribution.FromThrowable(s))); }
  return (c,s,r);
 }
 public void Settle((ChainId chain, GameId stone, GameId[] robots) t){
  Bus.Publish(new ThrowableRemoved(t.stone,t.chain));
  foreach(var r in t.robots) Bus.Publish(new RobotRemoved(r,t.chain,RemovalReason.HitGround));
 }
 public void Breach(){ Bus.Publish(new RobotRemoved(Ids.Next(),ChainId.None,RemovalReason.EnteredBarn)); }
 public void Danger(){ Bus.Publish(new RobotEnteredDangerZone(Ids.Next())); }
 public int BankedTo(MasteryDestination d){ int t=0; foreach(var b in Banked) if(b.Destination==d) t+=b.Amount*b.Multiplier; return t; }
 public bool AnyBankedTo(MasteryDestination d){ foreach(var b in Banked) if(b.Destination==d) return true; return false; }
}
static void NightChecks(){
 // --- Running -> ChoicePending ---
 { var n=new Night(new NightGoal(50,10,1,5));
   Check(n.St.Phase==NightPhase.Running && n.St.StonesLeft==10 && n.St.CanThrow,"night starts Running, all stones, can throw");
   var t=n.Throw(3);   // 10+20+30 = 60 >= 50
   Check(n.St.Phase==NightPhase.Running && !n.St.CanThrow && n.Targets.Count==1 && n.Targets[0].StonesLeft==9 && n.St.StonesAtTarget==9,
     "target reached mid-chain: throwing stops, NightTargetReached (9 stones), still Running while it falls");
   n.Settle(t);
   Check(n.St.Phase==NightPhase.ChoicePending && !n.St.CanThrow && n.St.ScoreAtChoice==60 && n.Ends.Count==0,
     "chain settles -> ChoicePending: paused, no throwing, not ended");
   n.Breach(); n.Breach(); n.Breach(); n.Breach(); n.Breach();
   Check(n.St.Phase==NightPhase.ChoicePending && n.St.StonesLeft==9 && n.Ends.Count==0,"ChoicePending can't be lost: breaches cost nothing"); }
 { var n=new Night(new NightGoal(50,10,1,5));
   var a=n.Throw(1); var b=n.Throw(3);   // b crosses the target while a is still in flight
   n.Settle(b);
   Check(n.St.Phase==NightPhase.Running,"another chain still in flight -> no choice yet (never cut a chain)");
   n.Settle(a);
   Check(n.St.Phase==NightPhase.ChoicePending && n.St.ScoreAtChoice==70,"last chain lands -> ChoicePending with its points (70)"); }
 { var n=new Night(new NightGoal(50,10,1,2));
   var t=n.Throw(3); n.Breach(); n.Breach(); n.Breach();
   Check(n.Ends.Count==0 && n.St.StonesLeft==9,"after the target (chain still falling) the night can't be lost: breaches don't end it or cost stones");
   n.Settle(t);
   Check(n.St.Phase==NightPhase.ChoicePending,"...and the choice still comes"); }
 { var n=new Night(new NightGoal(60,1,1,5));
   var t=n.Throw(3); n.Settle(t);
   Check(n.St.Phase==NightPhase.ChoicePending,"last stone's chain reaches the target -> ChoicePending, not Lost"); }
 // --- Leave ---
 { var n=new Night(new NightGoal(50,10,1,5));
   n.Ref.Stay(); n.Ref.Leave();
   Check(n.St.Phase==NightPhase.Running,"Stay/Leave are ignored outside ChoicePending");
   var t=n.Throw(3); n.Settle(t); n.Ref.Leave();
   Check(n.St.Phase==NightPhase.Ended && n.St.Choice==NightChoice.Leave && n.Ends.Count==1 && n.Ends[0].Result==NightResult.Won
     && n.Ends[0].Reason==NightEndReason.Left,"Leave -> Ended at once, Won, reason Left");
   Check(n.BankedTo(MasteryDestination.Barn)==10 && n.BankedTo(MasteryDestination.Weapon)==9 && n.St.BarnMastery==10 && n.St.WeaponMastery==9,
     "Leave banks: score above target (60-50=10) to the barn x1, leftover stones (9) to the weapon");
   n.Ref.Stay();
   Check(n.St.Phase==NightPhase.Ended && n.Ends.Count==1,"Ended is final: a late Stay changes nothing"); }
 // --- Stay -> Overtime ---
 { var n=new Night(new NightGoal(50,3,1,5));
   var t=n.Throw(3); n.Settle(t); n.Ref.Stay();
   Check(n.St.Phase==NightPhase.Overtime && n.St.CanThrow && n.St.StonesLeft==2 && n.St.Choice==NightChoice.Stay,"Stay -> Overtime: throwing resumes (2 stones)");
   var o=n.Throw(2);   // 10+20 = 30 overtime points
   Check(n.St.OvertimeScore==30 && n.St.Phase==NightPhase.Overtime,"overtime score counted separately (30)");
   n.Settle(o);
   n.Breach();   // (irrelevant here: covered below)
   Check(n.St.Phase==NightPhase.Ended && n.Ends[0].Reason==NightEndReason.DangerLine,"a breach during overtime ends it (danger line)");
 }
 { var n=new Night(new NightGoal(50,3,1,5));
   var t=n.Throw(3); n.Settle(t); n.Ref.Stay();
   var o1=n.Throw(1); n.Settle(o1);
   Check(n.St.Phase==NightPhase.Overtime,"overtime continues while stones remain");
   var o2=n.Throw(2);
   Check(n.St.Phase==NightPhase.Overtime && n.St.StonesLeft==0,"last stone thrown: overtime waits for its chain");
   n.Settle(o2);
   Check(n.St.Phase==NightPhase.Ended && n.Ends[0].Result==NightResult.Won && n.Ends[0].Reason==NightEndReason.OutOfStones,
     "overtime stones used up, last chain closed -> Ended, Won (no loss in overtime)");
   Check(n.St.OvertimeScore==40 && n.BankedTo(MasteryDestination.Barn)==10*1+40*2,"Stay banks: overshoot 10 x1 + overtime 40 x2 = 90 to the barn");
   Check(!n.AnyBankedTo(MasteryDestination.Weapon) && n.St.WeaponMastery==0,"no weapon mastery from overtime"); }
 { // THE rule that keeps Leave worth choosing: overtime ended by the danger line with stones unthrown -> still no weapon mastery.
   var n=new Night(new NightGoal(50,10,1,5));
   var t=n.Throw(3); n.Settle(t); n.Ref.Stay();
   var o=n.Throw(1);
   n.Danger();
   Check(n.St.Phase==NightPhase.Ended && n.Ends[0].Result==NightResult.Won && n.Ends[0].Reason==NightEndReason.DangerLine,
     "overtime: a robot enters the danger zone -> Ended, Won (no loss), even with a chain in flight");
   Check(n.St.StonesLeft==0 && !n.AnyBankedTo(MasteryDestination.Weapon),"danger-line end: the 8 unthrown stones are lost, weapon mastery 0");
   Check(n.BankedTo(MasteryDestination.Barn)==10+10*2,"everything earned is kept: overshoot 10 x1 + overtime 10 x2");
   n.Settle(o);
   Check(n.Ends.Count==1,"NightEnded fires once, even when the in-flight chain lands afterwards"); }
 { var n=new Night(new NightGoal(50,1,1,5));
   var t=n.Throw(3); n.Settle(t);
   Check(n.St.Phase==NightPhase.ChoicePending && n.St.StonesLeft==0,"choice appears even with 0 stones left");
   n.Ref.Stay();
   Check(n.St.Phase==NightPhase.Ended && n.Ends[0].Reason==NightEndReason.OutOfStones,"Stay with 0 stones -> overtime ends at once"); }
 // --- Danger line in Running = warning only ---
 { var n=new Night(new NightGoal(500,5,1,3));
   n.Danger();
   Check(n.St.Phase==NightPhase.Running && n.St.StonesLeft==5 && n.St.RobotsReachedTop==0 && n.Ends.Count==0,
     "Running: entering the danger zone is a warning only (no stone, no breach, no end)"); }
 // --- Losing (Running, below the target) ---
 { var n=new Night(new NightGoal(500,2,1,5));
   var a=n.Throw(0); var b=n.Throw(1);
   Check(n.St.StonesLeft==0 && !n.St.CanThrow,"all stones thrown -> can't throw");
   n.Settle(a);
   Check(n.Ends.Count==0,"out of stones but a chain still falling -> not over");
   n.Settle(b);
   Check(n.Ends.Count==1 && n.Ends[0].Result==NightResult.Lost && n.Ends[0].Reason==NightEndReason.OutOfStones,"last chain lands below target -> Lost, out of stones");
   Check(n.Banked.Count==0 && n.St.BarnMastery==0 && n.St.WeaponMastery==0,"a lost night banks nothing"); }
 { var n=new Night(new NightGoal(500,3,1,5));
   n.Breach();
   Check(n.St.RobotsReachedTop==1 && n.St.StonesLeft==2,"Running: a breach costs a stone");
   n.Breach(); n.Breach();
   Check(n.St.StonesLeft==0 && n.Ends.Count==1 && n.Ends[0].Reason==NightEndReason.OutOfStones,"breaches take the last stone with nothing in flight -> Lost"); }
 { var n=new Night(new NightGoal(500,30,1,3));
   var t=n.Throw(1); n.Breach(); n.Breach(); n.Breach();
   Check(n.Ends.Count==1 && n.Ends[0].Result==NightResult.Lost && n.Ends[0].Reason==NightEndReason.BarnBreached && n.Ends[0].Breaches==3,
     "3rd breach -> Lost, barn breached, even with a chain in flight");
   n.Breach(); n.Settle(t);
   Check(n.Ends.Count==1 && n.St.RobotsReachedTop==3,"NightEnded fires once; nothing counts after the end"); }
 { var n=new Night(new NightGoal(500,1,1,3));
   var t=n.Throw(1); n.Breach();
   Check(n.St.StonesLeft==0 && n.St.RobotsReachedTop==1 && n.Ends.Count==0,"breach at 0 stones: counted, stones stay 0, not over");
   n.Breach(); n.Breach();
   Check(n.Ends.Count==1 && n.Ends[0].Reason==NightEndReason.BarnBreached && n.Ends[0].StonesLeft==0,"3rd breach at 0 stones -> Lost, barn breached");
   n.Settle(t); }
 { var n=new Night(new NightGoal(500,1,3,0));
   n.Breach();
   Check(n.St.StonesLeft==0,"a breach costing 3 stones with 1 left -> 0, not -2"); }
 { var n=new Night(new NightGoal(500,5,0,0));
   for(int i=0;i<10;i++) n.Breach();
   Check(n.St.StonesLeft==5 && n.Ends.Count==0,"stonesLostPerBreach 0 + maxBreaches 0 -> breaches cost nothing"); }
 // --- The sweep: flat robot value, same function as chains ---
 { var n=new Night(new NightGoal(50,10,1,5));
   var t=n.Throw(3); n.Settle(t);
   for(int i=0;i<4;i++) n.Wall.Add(n.Ids.Next());
   n.Ref.Leave();
   Check(n.St.SweepScore==40 && n.St.Score==100 && n.Ends[0].Score==100,"sweep: 4 robots x robot value 10, flat (no order escalation, no depth) = 40, in the final score");
   Check(n.BankedTo(MasteryDestination.Barn)==50,"Leave path: the sweep counts as score above target (100-50) x1"); }
 { var curve=new ScoreCurve(10,10,7,0.5f,false);
   var n=new Night(new NightGoal(50,10,1,5),curve);
   var t=n.Throw(3); n.Settle(t);
   for(int i=0;i<3;i++) n.Wall.Add(n.Ids.Next());
   n.Ref.Leave();
   Check(n.St.SweepScore==3*curve.RobotValue() && curve.RobotValue()==7,"sweep uses the chain's robot-value function (wolfValue 7 -> 21)"); }
 { var n=new Night(new NightGoal(50,3,1,5));
   var t=n.Throw(3); n.Settle(t); n.Ref.Stay();
   for(int i=0;i<2;i++) n.Wall.Add(n.Ids.Next());
   n.Danger();
   Check(n.St.SweepScore==20 && n.St.OvertimeScore==20 && n.BankedTo(MasteryDestination.Barn)==10+20*2,
     "Stay path: the sweep is overtime score -> barn x2 (overshoot 10 + sweep 20x2)"); }
 { var n=new Night(new NightGoal(500,10,1,5));
   n.Bus.Publish(new RobotSwept(n.Ids.Next()));
   Check(n.St.Score==0 && n.St.SweepScore==0,"RobotSwept outside the end sweep scores nothing"); }
 { var n=new Night(new NightGoal(500,1,1,5));
   n.Wall.Add(n.Ids.Next());
   var t=n.Throw(0); n.Settle(t);
   Check(n.Ends[0].Result==NightResult.Lost && n.St.SweepScore==10 && n.Banked.Count==0,"a lost night is swept too, but banks nothing"); }
 // --- Phase order, as published ---
 { var n=new Night(new NightGoal(50,2,1,5));
   var t=n.Throw(3); n.Settle(t); n.Ref.Stay(); var o=n.Throw(1); n.Settle(o);
   Check(n.Phases.Count==3 && n.Phases[0]==NightPhase.ChoicePending && n.Phases[1]==NightPhase.Overtime && n.Phases[2]==NightPhase.Ended,
     "NightPhaseChanged: ChoicePending, Overtime, Ended — once each");
   Check(n.Ends[0].ThrowsUsed==2 && n.Ends[0].StonesLeft==0,"NightEnded carries throws used"); }
 Check(new NightGoal(0).TargetScore==1,"a target of 0 becomes 1 (otherwise you could never throw)");
}
// ThrowSolver: the arc must pass through the target, and the stepped physics flight must stay on that arc.
static void ThrowChecks(){
 const float v=7f, g=9.81f, dt=0.02f;
 var targets=new (float x,float y)[]{(2f,0f),(-2f,-1.5f),(3f,-3f),(1f,1f),(-0.5f,-4f),(2.5f,0.5f)};
 foreach(var (tx,ty) in targets){
  bool ok=ThrowSolver.TrySolve(tx,ty,v,g,out float vx,out float vy);
  float t=ThrowSolver.TimeToCross(vx,tx); ThrowSolver.OffsetAt(vx,vy,g,t,out float x,out float y);
  Check(ok && MathF.Abs(x-tx)<1e-3f && MathF.Abs(y-ty)<1e-3f,$"arc hits ({tx},{ty}) speed {MathF.Sqrt(vx*vx+vy*vy):0.00}");
 }
 { // Fixed-step flight (velocity first, then position — how 2D physics integrates) vs the ideal arc.
  ThrowSolver.TrySolve(3f,-2f,v,g,out float vx,out float vy);
  float worstComp=0f, worstRaw=0f;
  float pyC=0f, vyC=ThrowSolver.CompensateForFixedStep(vy,g,dt), pyR=0f, vyR=vy;
  for(int n=1;n<=60;n++){
   vyC-=g*dt; pyC+=vyC*dt; vyR-=g*dt; pyR+=vyR*dt;
   ThrowSolver.OffsetAt(vx,vy,g,n*dt,out _,out float iy);
   worstComp=MathF.Max(worstComp,MathF.Abs(pyC-iy)); worstRaw=MathF.Max(worstRaw,MathF.Abs(pyR-iy));
  }
  Check(worstComp<1e-3f,$"stepped flight on the arc with compensation (off by {worstComp:0.0000})");
  Check(worstRaw>0.05f,$"...and off it without (off by {worstRaw:0.000}) - compensation is needed");
 }
 { bool ok=ThrowSolver.TrySolve(20f,0f,v,g,out float vx,out float vy);
   Check(!ok && MathF.Abs(vx-vy)<1e-4f && vx>0f,"out of range -> false, 45 deg towards target"); }
 { bool ok=ThrowSolver.TrySolve(-20f,0f,v,g,out float vx,out float vy);
   Check(!ok && vx<0f && vy>0f,"out of range to the left keeps direction"); }
 { Check(!ThrowSolver.TrySolve(0f,10f,v,g,out _,out _),"straight up beyond reach -> false");
   Check(ThrowSolver.TrySolve(0f,-3f,v,g,out float vx,out float vy) && vy<0f && MathF.Abs(vx)<1e-4f,"straight down -> aims down"); }
 { bool ok=ThrowSolver.TrySolve(2f,-1f,v,0f,out float vx,out float vy);
   Check(ok && MathF.Abs(vx*-1f-vy*2f)<1e-4f,"no gravity -> straight line"); }
}}
