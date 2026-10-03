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
 PegChecks();
 NameClashChecks();
 ThrowChecks();
 BreachChecks();
 PileChecks();
 ComponentFileChecks();
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
// NightReferee: the hours until dawn. Running(hour) -> PegPlacement -> Running(hour+1) ... -> Ended (dawn), or caught.
class Night{
 public EventBus Bus=new EventBus(); public NightState St=new NightState(); public IdAllocator Ids=new IdAllocator();
 public ChainTracker Tr; public NightReferee Ref;
 public System.Collections.Generic.List<NightEnded> Ends=new System.Collections.Generic.List<NightEnded>();
 public System.Collections.Generic.List<NightPhaseChanged> Phases=new System.Collections.Generic.List<NightPhaseChanged>();
 public System.Collections.Generic.List<NightBanked> Banked=new System.Collections.Generic.List<NightBanked>();
 public System.Collections.Generic.List<HourReached> Hours=new System.Collections.Generic.List<HourReached>();
 public System.Collections.Generic.List<DawnReached> Dawns=new System.Collections.Generic.List<DawnReached>();
 public System.Collections.Generic.List<StonesChanged> Stones=new System.Collections.Generic.List<StonesChanged>();
 public System.Collections.Generic.List<RobotScored> Scored=new System.Collections.Generic.List<RobotScored>();
 public System.Collections.Generic.List<ChainScored> Chains=new System.Collections.Generic.List<ChainScored>();
 public System.Collections.Generic.List<ThrowReleased> Throws=new System.Collections.Generic.List<ThrowReleased>();
 public System.Collections.Generic.List<PegPlaced> Placed=new System.Collections.Generic.List<PegPlaced>();
 public System.Collections.Generic.List<PegMerged> Merged=new System.Collections.Generic.List<PegMerged>();
 // Robots still climbing: what Simulation's RobotSpawner would sweep when the night ends.
 public System.Collections.Generic.List<GameId> Wall=new System.Collections.Generic.List<GameId>();
 public Night(NightGoal g, ScoreCurve curve=null, PegSetup pegs=null){
  Tr=new ChainTracker(Bus,St,curve); Ref=new NightReferee(Bus,St,Tr,g,pegs);
  Bus.Subscribe<NightEnded>(e=>Ends.Add(e)); Bus.Subscribe<NightBanked>(e=>Banked.Add(e)); Bus.Subscribe<RobotScored>(e=>Scored.Add(e));
  Bus.Subscribe<HourReached>(e=>Hours.Add(e)); Bus.Subscribe<DawnReached>(e=>Dawns.Add(e)); Bus.Subscribe<StonesChanged>(e=>Stones.Add(e));
  Bus.Subscribe<ChainScored>(e=>Chains.Add(e)); Bus.Subscribe<ThrowReleased>(e=>Throws.Add(e));
  Bus.Subscribe<PegPlaced>(e=>Placed.Add(e)); Bus.Subscribe<PegMerged>(e=>Merged.Add(e));
  // Stand-in for RobotSpawner: on Ended, every robot still on the wall is swept (synchronously, like the real one).
  Bus.Subscribe<NightPhaseChanged>(e=>{ Phases.Add(e); if(e.To==NightPhase.Ended) foreach(var r in Wall) Bus.Publish(new RobotSwept(r)); });
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
 public void Play(int n){ Settle(Throw(n)); }
 // Throw a stone that starts a line: stone -> r1, r1's ball -> r2 (depth 1), r2's ball -> r3 (depth 2)...
 public (ChainId chain, GameId stone, GameId[] robots) ThrowLine(int n){
  var c=new ChainId(Ids.Next()); var s=Ids.Next(); var r=new GameId[n];
  Bus.Publish(new ThrowReleased(c,s));
  for(int i=0;i<n;i++){ r[i]=Ids.Next(); Bus.Publish(new RobotLostGrip(r[i],c,i==0?Attribution.FromThrowable(s):Attribution.FromRobotBall(r[i-1],i-1))); }
  return (c,s,r);
 }
 public GameId Spawn(){ var r=Ids.Next(); Bus.Publish(new RobotSpawned(r)); return r; }
 // A whole breach, like the real robot: it starts (counted here) and, BreachSeconds later, is removed (cleanup).
 public void Breach(){ Breach(Spawn()); }
 public void Breach(GameId r){ BreachStart(r); BreachEnd(r); }
 public void BreachStart(GameId r){ Bus.Publish(new RobotBreached(r)); }
 public void BreachEnd(GameId r){ Bus.Publish(new RobotRemoved(r,ChainId.None,RemovalReason.EnteredBarn)); }
 public void Danger(){ Bus.Publish(new RobotEnteredDangerZone(Spawn())); }
 public int BankedTo(MasteryDestination d){ int t=0; foreach(var b in Banked) if(b.Destination==d) t+=b.Amount*b.Multiplier; return t; }
 public int Shelf(string id)=>Ref.ShelfCount(id);
 public bool AnyWeapon(){ foreach(var b in Banked) if(b.Destination==MasteryDestination.Weapon) return true; return false; }
}
static NightGoal Hours(params int[] t)=>new NightGoal(t,10,0);
static PegSetup Pegs(int sockets, int throws=1, params (string id,int count)[] shelf){
 var l=new System.Collections.Generic.List<(PegType,int)>();
 foreach(var (id,count) in shelf) l.Add((new PegType(id,3,true),count));
 return new PegSetup(l,throws,sockets);
}
static void NightChecks(){
 // --- The thresholds themselves ---
 { var g=new NightGoal(new[]{0,500,400,1500});
   Check(g.Thresholds[0]==1 && g.Thresholds[1]==500 && g.Thresholds[2]==501 && g.Thresholds[3]==1500,"thresholds: at least 1 and strictly rising (0 -> 1, 400 after 500 -> 501)");
   Check(new NightGoal(null).ThresholdCount==1 && new NightGoal(new int[0]).ThresholdCount==1,"no thresholds -> one (the night must be winnable)");
   Check(g.ScoreAtThreshold(0)==0 && g.ScoreAtThreshold(2)==500 && g.ScoreAtThreshold(9)==1500,"score kept at k thresholds: 0 before the first, else the k-th"); }
 // --- Hour multiplier ---
 { var c=new ScoreCurve(10,10,10,0.5f,false,0.5f);
   Check(c.HourMultiplier(0)==1f && c.HourMultiplier(1)==1.5f && c.HourMultiplier(2)==2f && c.HourMultiplier(4)==3f,"hour multiplier: x1, x1.5, x2 ... x3 in hour 5 (step 0.5)");
   Check(c.RobotTotal(25,1,1.5f)==56 && c.RobotTotal(15,1,1f)==23,"rounded once: 25 x1.5 x1.5 = 56.25 -> 56; hour 1 unchanged (22.5 -> 23)");
   Check(new ScoreCurve(10,10,10,0.5f,false,-1f).HourMultiplier(3)==1f,"a negative step is clamped to 0"); }
 // --- Running -> threshold -> PegPlacement -> Running(next hour) ---
 { var n=new Night(Hours(50,500,1000));
   Check(n.St.Phase==NightPhase.Running && n.St.Hour==1 && n.St.CanThrow && n.Ref.NextThreshold==50,"night starts Running, hour 1, can throw, next threshold 50");
   var t=n.Throw(3);   // 10+20+30 = 60 >= 50
   Check(n.Hours.Count==1 && n.Hours[0].Hour==2 && n.Hours[0].Multiplier==1.5f && n.Hours[0].Threshold==50 && n.St.Hour==2,"crossing 50: HourReached(hour 2, x1.5, threshold 50)");
   Check(n.St.Phase==NightPhase.Running && !n.St.CanThrow && n.St.PendingPegRounds==1,"throwing stops at the crossing; still Running while the chain falls");
   n.Settle(t);
   Check(n.St.Phase==NightPhase.PegPlacement && n.Phases[0].From==NightPhase.Running && n.Phases[0].To==NightPhase.PegPlacement && !n.St.CanThrow,
     "chain settles -> PegPlacement (no throwing)");
   n.Ref.EndPlacement();
   Check(n.St.Phase==NightPhase.Running && n.St.CanThrow && n.Ref.NextThreshold==500,"round over -> Running in hour 2, can throw, next threshold 500"); }
 { // The wall freezes at the crossing, not at the round: no stretch where you can't throw but wolves climb.
   var n=new Night(Hours(50,200));
   Check(n.St.WallMoving,"hour 1: the wall moves");
   var t=n.Throw(3);
   Check(!n.St.WallMoving && n.St.Phase==NightPhase.Running && !n.St.CanThrow,"threshold crossed, chain still falling: the wall is already frozen (and no throwing)");
   n.Settle(t);
   Check(!n.St.WallMoving && n.St.Phase==NightPhase.PegPlacement,"placement round: still frozen");
   n.Ref.EndPlacement();
   Check(n.St.WallMoving,"round over: the wall moves again");
   var u=n.Throw(4);   // hour 2: 100 x1.5 = 150 -> 210, dawn
   Check(n.St.Dawn && !n.St.WallMoving,"dawn crossed: the wall freezes while the last chain lands");
   n.Settle(u); }
 { // Two chains in flight: the round waits for both.
   var n=new Night(Hours(50,500)); var a=n.Throw(1); var b=n.Throw(3);
   n.Settle(b);
   Check(n.St.Phase==NightPhase.Running,"one chain still open -> no round yet");
   n.Settle(a);
   Check(n.St.Phase==NightPhase.PegPlacement,"last chain settles -> PegPlacement"); }
 // --- Multiplier fixed at throw time, applies to what a robot scores, never to what it carries ---
 { var curve=new ScoreCurve(15,10,10,0.5f,false,0.5f);
   var n=new Night(Hours(50,100000),curve);
   var r1=n.ThrowLine(3); n.Settle(r1); var run=n.Scored.ToArray(); n.Scored.Clear();
   Check(run[0].Total==15 && run[1].Total==38 && run[2].Total==70 && run[0].HourMultiplier==1f && run[0].Hour==1,"hour 1 line: 15, 37.5->38, 70 (x1)");
   n.Ref.EndPlacement();
   var r2=n.ThrowLine(3); n.Settle(r2); var h2=n.Scored.ToArray();
   Check(h2[0].Hour==2 && h2[0].HourMultiplier==1.5f && h2[0].Total==23 && h2[1].Total==56 && h2[2].Total==105,"hour 2 line: 15x1.5=22.5->23, 25x1.5x1.5=56.25->56, 35x2x1.5=105");
   Check(h2[1].Received==run[1].Received && h2[2].Received==run[2].Received && h2[1].Carries==run[1].Carries,"what robots carry isn't multiplied by the hour (never compounds)");
   Check(n.Chains[0].Hour==1 && n.Chains[n.Chains.Count-1].Hour==2,"ChainScored.Hour: 1, then 2"); }
 { var curve=new ScoreCurve(10,10,10,1f,true,0.5f);   // compounding switch on: the hour still applies once
   var n=new Night(Hours(10,100000),curve);
   n.Play(1); n.Ref.EndPlacement(); n.Scored.Clear();
   var t=n.ThrowLine(3); n.Settle(t);
   Check(n.Scored[0].Total==15 && n.Scored[1].Total==60 && n.Scored[2].Total==225,"carryScoredTotal on, hour 2: 10x1.5, 40x1.5, 150x1.5 (the hour doesn't compound)"); }
 // --- One chain crossing several thresholds: one round each, back to back ---
 { var n=new Night(Hours(20,40,1000));
   var t=n.Throw(3);   // 10, 30, 60: crosses 20 and 40
   Check(n.Hours.Count==2 && n.Hours[0].Hour==2 && n.Hours[1].Hour==3 && n.St.PendingPegRounds==2,"one chain crosses 20 and 40: two HourReached, two rounds waiting");
   n.Settle(t);
   Check(n.St.Phase==NightPhase.PegPlacement && n.St.PendingPegRounds==1,"first round");
   n.Ref.EndPlacement();
   Check(n.St.Phase==NightPhase.PegPlacement && n.St.PendingPegRounds==0 && n.Phases[n.Phases.Count-1].From==NightPhase.PegPlacement,
     "second round right after (PegPlacement -> PegPlacement)");
   n.Ref.EndPlacement();
   Check(n.St.Phase==NightPhase.Running && n.St.CanThrow && n.St.Hour==3,"then Running, hour 3"); }
 // --- Stone refill per threshold (not at dawn) ---
 { var n=new Night(new NightGoal(new[]{50,200,400},5,2));
   var t=n.Throw(3); n.Settle(t);
   Check(n.St.StonesLeft==6 && n.Stones[n.Stones.Count-1].Cause==StoneChange.Added && n.Stones[n.Stones.Count-1].Delta==2,
     "placement round: +2 stones (StonesChanged Added), 4 -> 6");
   var refills=0; foreach(var c in n.Stones) if(c.Cause==StoneChange.Added) refills++;
   n.Ref.EndPlacement(); n.Play(4); n.Ref.EndPlacement();   // hour 2: (10+20+30+40) x1.5 = 150 -> 210, threshold 200
   n.Play(4);   // hour 3: 100 x2 = 200 -> 410, dawn
   int refillsAfter=0; foreach(var c in n.Stones) if(c.Cause==StoneChange.Added) refillsAfter++;
   Check(refills==1 && refillsAfter==2 && n.Ends.Count==1 && n.Ends[0].Reason==NightEndReason.Dawn,"refill each hour (2), none at dawn"); }
 { var n=new Night(new NightGoal(new[]{50,1000},5,0));
   n.Play(3);
   Check(n.St.Phase==NightPhase.PegPlacement && n.St.StonesLeft==4 && n.Stones.Count==1,"stonesPerThreshold 0: the round adds nothing"); }
 // --- Dawn ---
 { var n=new Night(Hours(50,120));
   n.Play(3); n.Ref.EndPlacement();
   var t=n.Throw(2);   // hour 2: 60 + 15 + 30 = 105 ... not yet
   Check(n.Dawns.Count==0,"below the last threshold: no dawn");
   var u=n.Throw(1);   // +15 = 120
   Check(n.Dawns.Count==1 && n.St.Dawn && !n.St.CanThrow && n.St.Phase==NightPhase.Running && n.Ends.Count==0,"last threshold crossed: DawnReached, throwing stops, the night waits for the chains");
   int stones=n.St.StonesLeft; n.Breach();
   Check(n.Ends.Count==0 && n.St.StonesLeft==stones,"after dawn a breach can't catch anyone or steal");
   n.Settle(t); n.Settle(u);
   Check(n.Ends.Count==1 && n.Ends[0].Result==NightResult.Won && n.Ends[0].Reason==NightEndReason.Dawn && n.Ends[0].HoursReached==2,"chains settle -> Ended, won at dawn, 2 thresholds");
   Check(n.Hours.Count==1,"no HourReached (and no placement round) for the last threshold");
   bool placementAfterDawn=false; foreach(var p in n.Phases) if(p.To==NightPhase.PegPlacement && n.Phases.IndexOf(p)>0) placementAfterDawn=true;
   Check(!placementAfterDawn,"no pause at dawn"); }
 { var n=new Night(Hours(50));
   n.Wall.Add(n.Ids.Next()); n.Wall.Add(n.Ids.Next());
   n.Play(3);
   Check(n.Ends.Count==1 && n.St.SweepScore==20 && n.Ends[0].Score==80 && n.Ends[0].BankedScore==80 && n.BankedTo(MasteryDestination.Barn)==80,
     "dawn: the sweep scores flat (2 x 10) and the full live score is banked (60 + 20 = 80)");
   Check(!n.AnyWeapon(),"nothing banks to the weapon any more"); }
 // --- Losing: a breach on an empty pile, any hour before dawn ---
 { var n=new Night(new NightGoal(new[]{500},2,0));
   var a=n.Throw(0); var b=n.Throw(1);
   Check(n.St.StonesLeft==0 && !n.St.CanThrow,"all stones thrown -> can't throw");
   n.Settle(a); n.Settle(b);
   Check(n.Ends.Count==0 && n.St.Phase==NightPhase.Running,"last stone thrown and landed -> NOT a loss, the night goes on");
   n.Breach();
   Check(n.Ends.Count==1 && n.Ends[0].Result==NightResult.Lost && n.Ends[0].Reason==NightEndReason.Caught,"then a robot breaches on the empty pile -> Lost, caught"); }
 { var n=new Night(new NightGoal(new[]{50,100,5000},3,0));
   n.Play(3); n.Ref.EndPlacement();   // 60: threshold 50
   n.Play(3); n.Ref.EndPlacement();   // 150 (90 in hour 2): threshold 100
   n.Wall.Add(n.Ids.Next());
   n.Play(1);                         // 150 + 10x2 = 170; 0 stones left
   n.Breach();
   Check(n.Ends.Count==1 && n.Ends[0].Result==NightResult.Lost && n.Ends[0].Score==170 && n.Ends[0].BankedScore==100 && n.BankedTo(MasteryDestination.Barn)==100,
     "caught in hour 3 at 170: banks the last threshold reached (100), not the live score");
   Check(n.St.SweepScore==0 && n.St.Score==170,"a lost night's sweep is visual only: nothing scored"); }
 { var n=new Night(new NightGoal(new[]{500},1,0)); n.Play(1); n.Breach();
   Check(n.Ends[0].BankedScore==0 && n.Banked.Count==0,"caught before the first threshold: banks nothing"); }
 { var n=new Night(new NightGoal(new[]{50,5000},1,0));
   var t=n.Throw(3);   // crosses 50 with the last stone; 0 stones, round pending
   n.Breach();
   Check(n.Ends.Count==1 && n.Ends[0].Reason==NightEndReason.Caught && n.Ends[0].BankedScore==50,
     "a threshold (not dawn) doesn't protect you: caught while its chain still falls, banks that threshold (50)"); }
 { var n=new Night(new NightGoal(new[]{500},3,0));
   var thief=n.Spawn(); n.Breach(thief);
   Check(n.St.StonesLeft==2 && n.Ends.Count==0 && n.Stones[0].Cause==StoneChange.Stolen && n.Stones[0].Robot==thief,"breach with stones left: the robot takes the top stone, no loss");
   n.Breach(); n.Breach();
   Check(n.St.StonesLeft==0 && n.Ends.Count==0 && n.St.RobotsReachedTop==3,"three breaches take all three stones — still not lost");
   n.Breach();
   Check(n.Ends.Count==1 && n.Ends[0].Breaches==4,"4th breach on the empty pile -> caught");
   n.Breach();
   Check(n.Ends.Count==1 && n.St.RobotsReachedTop==4,"NightEnded fires once; nothing counts after the end"); }
 // --- Breaches count when they start (M7.3), and never during a placement round ---
 { var n=new Night(new NightGoal(new[]{500},3,0));
   var thief=n.Spawn(); n.BreachStart(thief);
   Check(n.Stones.Count==1 && n.Stones[0].Cause==StoneChange.Stolen && n.St.StonesLeft==2 && n.St.RobotsReachedTop==1,"one Stolen at Breaching entry (3 -> 2)");
   n.BreachEnd(thief);
   Check(n.Stones.Count==1 && n.St.RobotsReachedTop==1,"...the removal at the end of the sequence counts nothing"); }
 { var n=new Night(new NightGoal(new[]{500},1,0)); n.Play(0);
   var r=n.Spawn(); n.BreachStart(r);
   Check(n.Ends.Count==1 && n.Ends[0].Reason==NightEndReason.Caught,"empty pile: Caught at Breaching entry");
   n.BreachEnd(r); Check(n.Ends.Count==1,"...the removal afterwards changes nothing"); }
 { var n=new Night(new NightGoal(new[]{60,5000},2,0));
   var thief=n.Spawn(); n.BreachStart(thief);   // 2 -> 1
   var t=n.Throw(3);                            // the last stone's chain crosses 60 while the thief is still breaching
   n.BreachEnd(thief);                          // leaves with the pile empty: not a second breach
   n.Settle(t);
   Check(n.St.StonesLeft==0 && n.St.RobotsReachedTop==1 && n.Ends.Count==0 && n.St.Phase==NightPhase.PegPlacement,
     "threshold crossed during another robot's breach (after it stole): no catch, the round starts"); }
 { var n=new Night(new NightGoal(new[]{50,5000},0,0)); n.Ref.AddStones(3); n.Play(3);
   Check(n.St.Phase==NightPhase.PegPlacement,"in a placement round");
   n.Breach();
   Check(n.Ends.Count==0 && n.St.StonesLeft==2,"a breach during placement (a sequence finishing) costs nothing"); }
 // --- The danger line is a warning only ---
 { var n=new Night(new NightGoal(new[]{500},5,0)); n.Danger();
   Check(n.St.Phase==NightPhase.Running && n.St.StonesLeft==5 && n.Ends.Count==0,"danger zone: a warning only (no stone, no breach, no end)"); }
 // --- Stones added mid-run ---
 { var n=new Night(new NightGoal(new[]{500},5,0));
   var t=n.Throw(1);
   Check(n.Stones[0].Cause==StoneChange.Thrown && n.Stones[0].Count==4,"a throw: StonesChanged Thrown, 5 -> 4");
   n.Ref.AddStones(3); n.Ref.AddStones(0);
   Check(n.St.StonesLeft==7 && n.Stones.Count==2 && n.Stones[1].Cause==StoneChange.Added && n.St.CanThrow,"AddStones(3): 4 -> 7; AddStones(0) is ignored");
   n.Settle(t); }
 { var n=new Night(new NightGoal(new[]{500},0,0)); n.Ref.AddStones(2);
   Check(n.St.StonesLeft==2 && n.St.CanThrow,"an empty pile refilled mid-run can throw again"); }
 { var n=new Night(new NightGoal(new[]{500},1,0)); n.Play(0); n.Breach(); n.Ref.AddStones(5);
   Check(n.St.StonesLeft==0,"AddStones after the night ended is ignored"); }
 { var n=new Night(new NightGoal(new[]{500},10,0));
   n.Bus.Publish(new RobotSwept(n.Ids.Next()));
   Check(n.St.Score==0 && n.St.SweepScore==0,"RobotSwept outside the end sweep scores nothing"); }
 Check(new NightGoal().Thresholds[0]>=1,"default night has a reachable threshold");
}
// Pegs: the shelf, the board, placing and merging, throws per round.
static void PegChecks(){
 { var s=new PegSetup(new System.Collections.Generic.List<(PegType,int)>{
     (new PegType("a"),2),(new PegType("b"),1),(new PegType("a"),3),(new PegType("c"),1),(new PegType("d"),1),(new PegType("e"),1),(new PegType("f"),4),(null,1),(new PegType("g"),0)},1,14);
   Check(s.Loadout.Count==5 && s.Loadout[0].count==5 && s.DroppedTypes==1,"shelf: up to 5 types (6th dropped), same id adds up (a: 2+3), nulls and 0 counts ignored"); }
 { var n=new Night(Hours(50,5000),null,Pegs(3,1,("bouncy",2),("plain",1)));
   Check(n.St.Sockets.Length==3 && n.St.Shelf.Count==2 && n.Shelf("bouncy")==2,"board = socket count (max pegs), shelf from the loadout in order");
   Check(!n.Ref.PlacePeg(0,"bouncy") && n.Shelf("bouncy")==2,"no placing while Running");
   n.Play(3);
   Check(n.St.Phase==NightPhase.PegPlacement && n.St.PegThrowsLeft==1 && n.Ref.CanPlaceAnyPeg,"round: 1 throw, something placeable");
   int throwsBefore=n.St.ThrowsUsed, stonesBefore=n.St.StonesLeft, chainsBefore=n.Chains.Count, releases=n.Throws.Count;
   Check(n.Ref.PlacePeg(1,"bouncy") && n.Placed.Count==1 && n.Placed[0].Socket==1 && n.Placed[0].PegId=="bouncy" && n.Placed[0].Level==1,
     "PlacePeg on an empty socket: PegPlaced(1, bouncy, level 1)");
   Check(n.Shelf("bouncy")==1 && n.St.Sockets[1].PegId=="bouncy" && n.St.Sockets[1].Level==1,"the shelf loses one, the board holds it");
   Check(n.St.ThrowsUsed==throwsBefore && n.St.StonesLeft==stonesBefore && n.Throws.Count==releases && n.Chains.Count==chainsBefore,
     "a peg throw costs no stone, counts as no throw and starts no chain");
   Check(n.St.Phase==NightPhase.Running && n.St.CanThrow,"the round's only throw used -> back to Running"); }
 { var n=new Night(Hours(50,100,5000),null,Pegs(2,2,("bouncy",5),("plain",1)));
   n.Play(3);
   Check(n.St.PegThrowsLeft==2,"pegThrowsPerThreshold 2 -> two throws this round");
   n.Ref.PlacePeg(0,"bouncy");
   Check(n.St.Phase==NightPhase.PegPlacement && n.St.PegThrowsLeft==1,"one throw left: still placing");
   Check(n.Ref.IsValidTarget(0,"bouncy") && !n.Ref.IsValidTarget(0,"plain") && n.Ref.IsValidTarget(1,"plain"),
     "valid targets: same type = merge; another type can't go on it; an empty socket takes anything");
   Check(!n.Ref.PlacePeg(0,"plain") && n.St.PegThrowsLeft==1 && n.Shelf("plain")==1,"refused (wrong type on a peg): nothing used");
   Check(n.Ref.PlacePeg(0,"bouncy") && n.Merged.Count==1 && n.Merged[0].Level==2 && n.St.Sockets[0].Level==2,"same type: PegMerged, level 2");
   Check(n.St.Phase==NightPhase.Running,"both throws used -> Running");
   n.Play(3);   // threshold 100
   n.Ref.PlacePeg(0,"bouncy");   // level 3 = max
   Check(n.St.Sockets[0].Level==3 && !n.Ref.IsValidTarget(0,"bouncy"),"max level 3: no more merging onto it");
   Check(!n.Ref.PlacePeg(0,"bouncy") && n.St.Sockets[0].Level==3,"a merge past the max level is refused"); }
 { var n=new Night(Hours(50,5000),null,new PegSetup(new System.Collections.Generic.List<(PegType,int)>{(new PegType("solid",3,false),3)},2,1));
   n.Play(3); n.Ref.PlacePeg(0,"solid");
   Check(!n.Ref.IsValidTarget(0,"solid") && !n.Ref.CanPlaceAnyPeg && n.St.PegThrowsLeft==1,"a non-mergeable type can't go on itself; board full -> nothing placeable (throw left)");
   n.Ref.EndPlacement();
   Check(n.St.Phase==NightPhase.Running && n.St.PegThrowsLeft==0,"EndPlacement (Simulation, after the refill pause): unused throws are lost"); }
 { var n=new Night(Hours(50,5000),null,Pegs(14,1));
   n.Play(3);
   Check(n.St.Phase==NightPhase.PegPlacement && !n.Ref.CanPlaceAnyPeg,"empty shelf: the round still happens (refill pause) but nothing is placeable");
   n.Ref.EndPlacement(); Check(n.St.Phase==NightPhase.Running,"...and ends when Simulation says so"); }
 { var n=new Night(Hours(50,5000),null,Pegs(2,1,("plain",1)));
   n.Play(3); n.Ref.PlacePeg(0,"plain");
   n.Ref.EndPlacement();
   Check(n.St.Phase==NightPhase.Running,"EndPlacement outside a round is ignored");
   Check(!n.Ref.PlacePeg(1,"plain") && !n.Ref.PlacePeg(-1,"plain") && !n.Ref.PlacePeg(5,"plain") && !n.Ref.IsValidTarget(0,"ghost"),
     "no placing outside a round, off the board, or an unknown type"); }
 { // Max pegs = sockets: 2 sockets, 5 pegs of a type that can't merge -> 2 placed, then nothing.
   var n=new Night(Hours(50,100,150,5000),null,new PegSetup(new System.Collections.Generic.List<(PegType,int)>{(new PegType("x",1,true),5)},1,2));
   for(int i=0;i<3;i++){ n.Play(3); if(n.St.Phase==NightPhase.PegPlacement && n.Ref.CanPlaceAnyPeg){ for(int s=0;s<2;s++) if(n.Ref.PlacePeg(s,"x")) break; } else n.Ref.EndPlacement(); }
   int onBoard=0; foreach(var s in n.St.Sockets) if(!s.IsEmpty) onBoard++;
   Check(onBoard==2 && n.Shelf("x")==3,"max pegs = sockets: 2 on the board, 3 left on the shelf"); }
}
// Name clashes: CoreCheck can't compile Simulation/Presentation, so it can't see "ambiguous reference" errors
// there. This scans every script instead: a type name declared in two of our namespaces (e.g. an enum in
// Events and a component in Simulation), or one that shadows a common UnityEngine type, breaks any file
// that uses both namespaces — and Presentation uses nearly all of them.
static void NameClashChecks(){
 var dir=new System.IO.DirectoryInfo(AppContext.BaseDirectory);
 while(dir!=null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName,"Assets"))) dir=dir.Parent;
 if(dir==null){ Check(false,"name clashes: couldn't find the Assets folder"); return; }
 var scripts=System.IO.Path.Combine(dir.FullName,"Assets","_Piglings","Scripts");
 var decl=new System.Text.RegularExpressions.Regex(@"^\s*(?:public|internal)\s+(?:(?:sealed|static|readonly|abstract|partial)\s+)*(?:class|struct|enum|interface)\s+([A-Za-z_]\w*)");
 var nsOf=new System.Text.RegularExpressions.Regex(@"^\s*namespace\s+([\w\.]+)");
 var where=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.HashSet<string>>();
 foreach(var f in System.IO.Directory.GetFiles(scripts,"*.cs",System.IO.SearchOption.AllDirectories)){
  string ns="(global)";
  foreach(var line in System.IO.File.ReadAllLines(f)){
   var m=nsOf.Match(line); if(m.Success){ ns=m.Groups[1].Value; continue; }
   var d=decl.Match(line); if(!d.Success) continue;
   var name=d.Groups[1].Value;
   if(!where.TryGetValue(name,out var set)) where[name]=set=new System.Collections.Generic.HashSet<string>();
   set.Add(ns);
  }
 }
 var clashes=new System.Collections.Generic.List<string>();
 foreach(var kv in where) if(kv.Value.Count>1) clashes.Add(kv.Key+" in "+string.Join(" + ",kv.Value));
 Check(where.Count>20 && clashes.Count==0,"no type name declared in two of our namespaces"+(clashes.Count>0?": "+string.Join("; ",clashes):""));
 var unity=new[]{"Random","Debug","Time","Object","Color","Vector2","Vector3","Physics2D","Animator","Camera","Input","Screen","GUI","Transform","Rigidbody2D","Collider2D","EntityId","Mathf","Application"};
 var shadow=new System.Collections.Generic.List<string>();
 foreach(var u in unity) if(where.ContainsKey(u)) shadow.Add(u);
 Check(shadow.Count==0,"no type shadows a common UnityEngine name"+(shadow.Count>0?": "+string.Join(", ",shadow):""));
}
// BreachTiming: the breach sequence happens in order for any BreachSeconds — on the perch, then the stone lands
// (the robot visibly holds it), then the jump; and the robot doesn't pop when the jump starts.
static void BreachChecks(){
 Check(0f<BreachTiming.ClimbEnds && BreachTiming.ClimbEnds<BreachTiming.StoneLands && BreachTiming.StoneLands<BreachTiming.JumpStarts && BreachTiming.JumpStarts<1f,
   "breach order: on the perch < stone lands < jump starts < removed");
 foreach(var secs in new[]{0.5f,1.5f,4f}){
  float landsAt=BreachTiming.StoneHopSeconds(secs), jumpAt=BreachTiming.JumpStarts*secs;
  Check(landsAt>0f && landsAt<jumpAt && BreachTiming.JumpProgress(BreachTiming.Phase(landsAt,secs))==0f,
    $"BreachSeconds {secs}: the stolen stone lands at {landsAt:0.00}s, before the jump at {jumpAt:0.00}s");
 }
 Check(BreachTiming.ClimbProgress(0f)==0f && BreachTiming.ClimbProgress(BreachTiming.ClimbEnds)==1f && BreachTiming.ClimbProgress(BreachTiming.JumpStarts)==1f,
   "climb: starts where it touched the line, on the perch by ClimbEnds, stays there");
 BreachTiming.JumpOffset(0f,1f,0.174f,out float dx0,out float dy0);
 BreachTiming.JumpOffset(1f,1f,0.174f,out float dx1,out float dy1);
 BreachTiming.JumpOffset(1f,-1f,0.174f,out float dxl,out _);
 Check(dx0==0f && dy0==0f && dx1>0f && dxl<0f && dy1<0f,"jump: no pop at the start, ends out to its side and below the perch");
 Check(BreachTiming.Phase(10f,1f)==1f && BreachTiming.Phase(0f,0f)==1f,"phase is clamped; a zero-length breach is over at once (never stuck)");
}
// PileLayout: the pyramid every mirrored pile uses (the stone pile's slots, unchanged by the M8.2 refactor).
static void PileChecks(){
 bool At(int i,int bottom,float ex,float ey){ PileLayout.Pyramid(i,bottom,1f,1f,out float x,out float y); return MathF.Abs(x-ex)<1e-5f && MathF.Abs(y-ey)<1e-5f; }
 Check(At(0,3,-1f,0f) && At(1,3,0f,0f) && At(2,3,1f,0f),"pyramid of 3: bottom row -1, 0, 1, centred");
 Check(At(3,3,-0.5f,1f) && At(4,3,0.5f,1f) && At(5,3,0f,2f),"rows above: one fewer each, centred (-0.5, 0.5 / 0)");
 Check(At(6,3,0f,3f) && At(7,3,0f,4f),"past the top: a single column keeps stacking");
 Check(At(0,1,0f,0f) && At(4,1,0f,4f),"bottomRow 1 = a stack");
 Check(At(-2,3,-1f,0f) && At(0,0,0f,0f),"a negative index or a 0-wide row can't break it");
 PileLayout.Pyramid(9,8,0.17f,0.14f,out float sx,out float sy);
 Check(MathF.Abs(sx-(-0.34f))<1e-4f && MathF.Abs(sy-0.14f)<1e-5f,"the stone pile (8 wide, 0.17 / 0.14): stone 10 sits at (-0.34, 0.14), as before (2nd of the 7-wide row)");
}
// Unity can only add a MonoBehaviour / ScriptableObject as a component or asset when it lives in a file of the
// same name (the old Zones.cs bug). Plain types (events, enums, structs) may share a file. CLAUDE.md rule.
static void ComponentFileChecks(){
 var dir=new System.IO.DirectoryInfo(AppContext.BaseDirectory);
 while(dir!=null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName,"Assets"))) dir=dir.Parent;
 if(dir==null){ Check(false,"component files: couldn't find the Assets folder"); return; }
 var root=System.IO.Path.Combine(dir.FullName,"Assets","_Piglings");
 var decl=new System.Text.RegularExpressions.Regex(@"^\s*(?:public|internal)?\s*(?:(?:sealed|abstract|partial)\s+)*class\s+([A-Za-z_]\w*)\s*:\s*(?:UnityEngine\.)?(MonoBehaviour|ScriptableObject)\b");
 var bad=new System.Collections.Generic.List<string>(); int found=0;
 foreach(var f in System.IO.Directory.GetFiles(root,"*.cs",System.IO.SearchOption.AllDirectories)){
  int inFile=0; var name=System.IO.Path.GetFileNameWithoutExtension(f);
  foreach(var line in System.IO.File.ReadAllLines(f)){
   var m=decl.Match(line); if(!m.Success) continue;
   found++; inFile++;
   if(m.Groups[1].Value!=name) bad.Add(m.Groups[1].Value+" in "+name+".cs");
  }
  if(inFile>1) bad.Add(name+".cs has "+inFile+" MonoBehaviours/ScriptableObjects");
 }
 Check(found>20 && bad.Count==0,$"every MonoBehaviour / ScriptableObject ({found}) is alone in a file of its own name"+(bad.Count>0?": "+string.Join("; ",bad):""));
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
