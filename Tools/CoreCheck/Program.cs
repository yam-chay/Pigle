using System;
using Piglings.Events; using Piglings.Rules; using Piglings.Runtime; using Piglings.Simulation;
partial class P{static void Check(bool c,string m){Console.WriteLine((c?"PASS ":"FAIL ")+m); if(!c) Environment.ExitCode=1;}
static void Main(){
 var bus=new EventBus(); var st=new NightState(); var ids=new IdAllocator(); var tr=new ChainTracker(bus,st); ChainClosed? closed=null;
 bus.Subscribe<ChainClosed>(e=>closed=e);
 var chain=new ChainId(ids.Next()); var stone=ids.Next(); var a=ids.Next(); var b=ids.Next();
 bus.Publish(new ThrowReleased(chain,stone,"stone"));
 bus.Publish(new RobotLostGrip(a,chain,Attribution.FromThrowable(stone)));
 bus.Publish(new RobotLostGrip(b,chain,Attribution.FromRobotBall(a,0)));
 bus.Publish(new ThrowableRemoved(stone,chain)); bus.Publish(new RobotRemoved(a,chain,RemovalReason.HitGround));
 Check(!closed.HasValue,"chain open while b falls");
 bus.Publish(new RobotRemoved(b,chain,RemovalReason.HitGround));
 Check(closed.HasValue && closed.Value.RobotsDropped==2 && closed.Value.MaxDepth==1,"closed 2/depth1");
 Check(st.Score==60,$"score: (base 10 + 2 wolves × 10) × mult 2 (depth 1 reached) = 60 (got {st.Score})");
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
 MasteryChecks();
 SaveChecks();
 PegEffectChecks();
 SplitterChecks();
 BombChecks();
 CampaignChecks();
 FlowChecks();
 BarnChecks();
 PostRunChecks();
 DebugChecks();
 BalanceChecks();
}
// Scoring (M10.S): each chain keeps SCORE (stone base + wolves + plain holds) and MULT (1 + one per new depth + special
// pegs); the result = SCORE × MULT × the hour, rounded once. The raw score reaches the night's score as it comes; the
// remainder at the close.
static void ScoreChecks(){
 var bus=new EventBus(); var st=new NightState(); var ids=new IdAllocator(); var tr=new ChainTracker(bus,st);   // base 10, wolves 10
 var gains=new System.Collections.Generic.List<ChainGained>(); ChainScored? scored=null; int scoreSeenAtClose=-1; bool closedFirst=false;
 bus.Subscribe<ChainGained>(e=>gains.Add(e));
 bus.Subscribe<ChainClosed>(e=>{ scoreSeenAtClose=st.Score; closedFirst=!scored.HasValue; });
 bus.Subscribe<ChainScored>(e=>scored=e);
 // Line: stone -> a -> b -> c (depths 0, 1, 2)
 var chain=new ChainId(ids.Next()); var stone=ids.Next(); var a=ids.Next(); var b=ids.Next(); var c=ids.Next();
 bus.Publish(new ThrowReleased(chain,stone,"stone"));
 Check(gains.Count==1 && gains[0].Cause==ChainGainCause.Stone && gains[0].ScoreAdded==10 && gains[0].Score==10 && gains[0].Mult==1f && st.Score==10,
   "the throw: + the stone's base (10) to the chain AND the night's score at once (raw); mult starts at 1");
 bus.Publish(new RobotLostGrip(a,chain,Attribution.FromThrowable(stone)));
 Check(gains[1].Cause==ChainGainCause.Wolf && gains[1].ScoreAdded==10 && gains[1].MultAdded==0f && gains[1].Score==20 && st.Score==20,
   "the stone's own victim (depth 0): + its wolf value (10), no mult");
 bus.Publish(new RobotLostGrip(b,chain,Attribution.FromRobotBall(a,0)));
 bus.Publish(new RobotLostGrip(c,chain,Attribution.FromRobotBall(b,1)));
 Check(gains[2].MultAdded==1f && gains[3].MultAdded==1f && gains[3].Score==40 && gains[3].Mult==3f && st.Score==40,
   "each NEW depth adds +1 mult (depth 1, depth 2): score 40, mult 3 — the night has only the raw 40 so far");
 Check(!scored.HasValue,"no ChainScored while the chain is open");
 bus.Publish(new ThrowableRemoved(stone,chain));
 foreach(var r in new[]{a,b,c}) bus.Publish(new RobotRemoved(r,chain,RemovalReason.HitGround));
 Check(scored.HasValue && scored.Value.Total==120 && scored.Value.Score==40 && scored.Value.Mult==3f && scored.Value.Remainder==80 && st.Score==120,
   "the close: 40 × 3 × 1 = 120; the remainder (80) lands as one jump");
 Check(closedFirst && scoreSeenAtClose==120,"the remainder is in the score by ChainClosed; ChainClosed comes before ChainScored");
 // Wide, not deep: a stone knocks 3, the 2nd one's ball knocks 2 (all depth 0 / 1): one level of depth = +1 mult, once.
 gains.Clear(); scored=null; int before=st.Score;
 var ch2=new ChainId(ids.Next()); var s2=ids.Next(); var w=new[]{ids.Next(),ids.Next(),ids.Next()};
 bus.Publish(new ThrowReleased(ch2,s2,"stone"));
 foreach(var r in w) bus.Publish(new RobotLostGrip(r,ch2,Attribution.FromThrowable(s2)));
 var d=ids.Next(); var e2=ids.Next();
 bus.Publish(new RobotLostGrip(d,ch2,Attribution.FromRobotBall(w[1],0)));
 bus.Publish(new RobotLostGrip(e2,ch2,Attribution.FromRobotBall(w[1],0)));
 Check(gains[gains.Count-1].Score==60 && gains[gains.Count-1].Mult==2f,"wide: 5 wolves add 50 score, but depth 1 only once (+1): score 60, mult 2");
 bus.Publish(new ThrowableRemoved(s2,ch2)); foreach(var r in new[]{w[0],w[1],w[2],d,e2}) bus.Publish(new RobotRemoved(r,ch2,RemovalReason.HitGround));
 Check(scored.HasValue && scored.Value.Total==120 && st.Score==before+120,"wide chain: 60 × 2 = 120");
 // A miss still has the stone's base (Yam's spec: + base when thrown).
 scored=null; before=st.Score; var miss=new ChainId(ids.Next()); var s4=ids.Next();
 bus.Publish(new ThrowReleased(miss,s4,"stone")); bus.Publish(new ThrowableRemoved(s4,miss));
 Check(scored.HasValue && scored.Value.RobotsDropped==0 && scored.Value.Total==10 && st.Score==before+10,"a miss -> ChainScored 10 (the base × 1)");
 // Plain holds: + 1 every contact, the same hitter on the same hold at most once per cooldown (0.2 s by default);
 // a closed chain (or none) adds nothing.
 { gains.Clear(); var ch=new ChainId(ids.Next()); var s=ids.Next(); var r=ids.Next();
   bus.Publish(new ThrowReleased(ch,s,"stone"));
   Check(tr.TouchPlain(ch,4,s,1f) && !tr.TouchPlain(ch,4,s,1.1f) && tr.TouchPlain(ch,5,s,1.1f),
     "plain hold: +1; the same stone on the same hold 0.1 s later: nothing (cooldown); another hold: +1");
   Check(tr.TouchPlain(ch,4,s,1.25f) && tr.TouchPlain(ch,4,s,1.5f),"...and every contact after the cooldown scores again (not once per hold)");
   bus.Publish(new RobotLostGrip(r,ch,Attribution.FromThrowable(s)));
   Check(tr.TouchPlain(ch,4,r,1.5f),"a ball on the same hold at the same moment is another hitter: its own cooldown, +1");
   Check(gains[gains.Count-1].Cause==ChainGainCause.PlainPeg && gains[gains.Count-1].Socket==4 && gains[gains.Count-1].Score==10+4+10+1,
     "plain-hold gains carry the socket; the chain's score: 10 base + 5 contacts + 10 wolf = 25");
   bus.Publish(new ThrowableRemoved(s,ch)); bus.Publish(new RobotRemoved(r,ch,RemovalReason.HitGround));
   Check(!tr.TouchPlain(ch,7,s,9f) && !tr.TouchPlain(ChainId.None,7,s,9f),"a closed chain (or none — the sweep) gains nothing"); }
 Check(new ScoreCurve(10,10,1,1f,0.5f,-1f).PlainHoldCooldown==0f && new ScoreCurve().PlainHoldCooldown==0.2f,"the cooldown: 0.2 s by default, never negative");
 // A special peg's mult bonus.
 { var ch=new ChainId(ids.Next()); var s=ids.Next(); bus.Publish(new ThrowReleased(ch,s,"stone"));
   Check(tr.AddPegGain(ch,3,s,0,2f)==3f && tr.AddPegGain(ch,3,s,0,0f)==3f,"a peg bonus adds to the mult (1 + 2 = 3); 0 adds nothing");
   bus.Publish(new ThrowableRemoved(s,ch)); Check(scored.Value.Total==30,"base 10 × mult 3 = 30"); }
 tr.Dispose();
 // The hour applies at the close, after score × mult; rounded once.
 { var curve=new ScoreCurve(15,10,1,1f,0.5f);
   Check(curve.Result(25,3f,1.5f)==113 && curve.Result(25,1f,1f)==25 && curve.Result(7,1f,1.5f)==11,"result: 25 × 3 × 1.5 = 112.5 -> 113; 7 × 1.5 = 10.5 -> 11 (rounded once)");
   Check(curve.Result(10,0f,0f)==10,"a mult / hour below 1 never shrinks the score");
   Check(Math.Abs(curve.Multiplier(2)-3f)<1e-5f && curve.HourMultiplier(2)==2f,"the DEPTH card's mult at depth 2: 1 + 2 = 3; hour 3 ×2"); }
 Check(new ScoreCurve(10,10,1,-1f,-1f).Multiplier(3)==1f && new ScoreCurve(-5).StoneBase==0,"negative settings clamped to 0");
 Check(new ScoreCurve(10,10,1,1f).Result(int.MaxValue,5f,3f)==int.MaxValue,"a huge result saturates at int.MaxValue instead of going negative");
}
// NightReferee: the hours until dawn. Running(hour) -> PegPlacement -> Running(hour+1) ... -> Ended (dawn), or out of stones.
class Night{
 public EventBus Bus=new EventBus(); public NightState St=new NightState(); public IdAllocator Ids=new IdAllocator();
 public ChainTracker Tr; public NightReferee Ref;
 public System.Collections.Generic.List<NightEnded> Ends=new System.Collections.Generic.List<NightEnded>();
 public System.Collections.Generic.List<NightPhaseChanged> Phases=new System.Collections.Generic.List<NightPhaseChanged>();
 public System.Collections.Generic.List<NightBanked> Banked=new System.Collections.Generic.List<NightBanked>();
 public System.Collections.Generic.List<HourReached> Hours=new System.Collections.Generic.List<HourReached>();
 public System.Collections.Generic.List<DawnReached> Dawns=new System.Collections.Generic.List<DawnReached>();
 public System.Collections.Generic.List<StonesChanged> Stones=new System.Collections.Generic.List<StonesChanged>();
 public System.Collections.Generic.List<ChainGained> Gains=new System.Collections.Generic.List<ChainGained>();
 public System.Collections.Generic.List<ChainScored> Chains=new System.Collections.Generic.List<ChainScored>();
 public System.Collections.Generic.List<ThrowReleased> Throws=new System.Collections.Generic.List<ThrowReleased>();
 public System.Collections.Generic.List<PegPlaced> Placed=new System.Collections.Generic.List<PegPlaced>();
 public System.Collections.Generic.List<PegMerged> Merged=new System.Collections.Generic.List<PegMerged>();
 // Robots still climbing: what Simulation's RobotSpawner would sweep when the night ends.
 public System.Collections.Generic.List<GameId> Wall=new System.Collections.Generic.List<GameId>();
 // The test curve (M10.S): no stone base, wolves 20 — so a throw knocking n robots directly is worth 20n (Throw(3) = 60,
 // as the old checks' numbers), and only depth / pegs / the hour add mult. Pass a curve to test the real defaults.
 public static readonly ScoreCurve TestCurve=new ScoreCurve(0,20,1,1f,0.5f);
 public Night(NightGoal g, ScoreCurve curve=null, PegSetup pegs=null){
  Tr=new ChainTracker(Bus,St,curve??TestCurve); Ref=new NightReferee(Bus,St,Tr,g,pegs);
  Bus.Subscribe<NightEnded>(e=>Ends.Add(e)); Bus.Subscribe<NightBanked>(e=>Banked.Add(e)); Bus.Subscribe<ChainGained>(e=>Gains.Add(e));
  Bus.Subscribe<HourReached>(e=>Hours.Add(e)); Bus.Subscribe<DawnReached>(e=>Dawns.Add(e)); Bus.Subscribe<StonesChanged>(e=>Stones.Add(e));
  Bus.Subscribe<ChainScored>(e=>Chains.Add(e)); Bus.Subscribe<ThrowReleased>(e=>Throws.Add(e));
  Bus.Subscribe<PegPlaced>(e=>Placed.Add(e)); Bus.Subscribe<PegMerged>(e=>Merged.Add(e));
  // Stand-in for RobotSpawner: on Ended, every robot still on the wall is swept (synchronously, like the real one).
  Bus.Subscribe<NightPhaseChanged>(e=>{ Phases.Add(e); if(e.To==NightPhase.Ended) foreach(var r in Wall) Bus.Publish(new RobotSwept(r)); });
 }
 // Throw a stone that knocks n robots directly (10, 20, 30... by default). Returns what's still in flight.
 public (ChainId chain, GameId stone, GameId[] robots) Throw(int n){
  var c=new ChainId(Ids.Next()); var s=Ids.Next(); var r=new GameId[n];
  Bus.Publish(new ThrowReleased(c,s,"stone"));
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
  Bus.Publish(new ThrowReleased(c,s,"stone"));
  for(int i=0;i<n;i++){ r[i]=Ids.Next(); Bus.Publish(new RobotLostGrip(r[i],c,i==0?Attribution.FromThrowable(s):Attribution.FromRobotBall(r[i-1],i-1))); }
  return (c,s,r);
 }
 public GameId Spawn(){ var r=Ids.Next(); Bus.Publish(new RobotSpawned(r,"wolfbot")); return r; }
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
 { var c=new ScoreCurve(10,10,1,1f,0.5f);
   Check(c.HourMultiplier(0)==1f && c.HourMultiplier(1)==1.5f && c.HourMultiplier(2)==2f && c.HourMultiplier(4)==3f,"hour multiplier: x1, x1.5, x2 ... x3 in hour 5 (step 0.5)");
   Check(c.Result(25,1.5f,1.5f)==56 && c.Result(15,1.5f,1f)==23,"rounded once: 25 x1.5 x1.5 = 56.25 -> 56; 22.5 -> 23");
   Check(new ScoreCurve(10,10,1,1f,-1f).HourMultiplier(3)==1f,"a negative step is clamped to 0"); }
 // --- Running -> threshold -> PegPlacement -> Running(next hour) ---
 { var n=new Night(Hours(50,500,1000));
   Check(n.St.Phase==NightPhase.Running && n.St.Hour==1 && n.St.CanThrow && n.Ref.NextThreshold==50,"night starts Running, hour 1, can throw, next threshold 50");
   var t=n.Throw(3);   // 10+20+30 = 60 >= 50
   Check(n.Hours.Count==0 && n.St.Hour==1 && n.St.ThresholdsReached==1 && n.Ref.NextThreshold==500,
     "crossing 50: still hour 1 (no HourReached yet), next threshold already 500");
   Check(n.St.Phase==NightPhase.Running && n.St.CanThrow && n.St.WallMoving && n.St.PendingPegRounds==1,
     "after the crossing play goes on: still Running, can throw, the wall moves, a round is waiting");
   n.Settle(t);
   Check(n.St.Phase==NightPhase.PegPlacement && n.Phases[0].From==NightPhase.Running && n.Phases[0].To==NightPhase.PegPlacement && !n.St.CanThrow,
     "chain settles -> PegPlacement (no throwing)");
   Check(n.Hours.Count==1 && n.Hours[0].Hour==2 && n.Hours[0].Multiplier==1.5f && n.Hours[0].Threshold==50 && n.St.Hour==2,
     "the new hour starts in the freeze: HourReached(hour 2, x1.5, threshold 50) when the round starts");
   n.Ref.EndPlacement();
   Check(n.St.Phase==NightPhase.Running && n.St.CanThrow && n.Ref.NextThreshold==500,"round over -> Running in hour 2, can throw, next threshold 500"); }
 { // Play goes on after a crossing; the only freeze is the round. Chains thrown after the crossing don't hold it off.
   var n=new Night(Hours(50,1000));
   Check(n.St.WallMoving,"hour 1: the wall moves");
   var t=n.Throw(3);                  // crosses 50
   var late=n.Throw(2);               // thrown after the crossing: hour 2 already
   Check(n.Gains[n.Gains.Count-1].Hour==1 && n.Gains[n.Gains.Count-1].HourMultiplier==1f,"a throw between the crossing and the freeze still scores at the old hour (×1)");
   n.Settle(t);
   Check(n.St.Phase==NightPhase.PegPlacement && !n.St.WallMoving && !n.St.CanThrow,
     "the crossing's chain lands -> the round starts, though a later chain is still falling (it doesn't hold the round off)");
   int before=n.St.Score; n.Bus.Publish(new RobotLostGrip(n.Ids.Next(),late.chain,Attribution.FromRobotBall(late.robots[0],0)));
   Check(n.St.Score>before && n.St.Phase==NightPhase.PegPlacement,"the late chain keeps scoring during the round");
   n.Settle(late);
   Check(n.St.Phase==NightPhase.PegPlacement,"...and its landing doesn't end the round");
   n.Ref.EndPlacement();
   Check(n.St.Phase==NightPhase.Running && n.St.WallMoving && n.St.CanThrow,"round over: Running, the wall moves"); }
 { // A late chain that crosses the NEXT threshold: its round waits for it, even across the current round.
   var n=new Night(Hours(50,90,1000));
   var t=n.Throw(3);                  // 60: crosses 50
   var late=n.Throw(2);               // +45 = 105: crosses 90 too (two rounds pending)
   Check(n.St.PendingPegRounds==2,"two rounds pending");
   n.Settle(t);
   Check(n.St.Phase==NightPhase.PegPlacement && n.St.PendingPegRounds==1,"first round starts (only the 50-crossing chain had to land)");
   n.Ref.EndPlacement();
   Check(n.St.Phase==NightPhase.Running,"second round waits: the chain that crossed 90 is still falling");
   n.Settle(late);
   Check(n.St.Phase==NightPhase.PegPlacement && n.St.PendingPegRounds==0,"it lands -> second round");
   n.Ref.EndPlacement(); Check(n.St.Phase==NightPhase.Running,"then Running"); }
 { // Dawn: throwing stops (the night is won), the night ends once everything has landed.
   var n=new Night(Hours(50));
   var t=n.Throw(3); var u=n.Throw(0);
   Check(n.St.Dawn && !n.St.CanThrow && n.Ends.Count==0,"dawn: no more throwing, waits for the chains");
   n.Settle(t);
   Check(n.Ends.Count==0,"...all of them (another is still in flight)");
   n.Settle(u);
   Check(n.Ends.Count==1 && n.Ends[0].Reason==NightEndReason.Dawn,"last one lands -> won at dawn"); }
 { // Two chains in flight: the round waits for both.
   var n=new Night(Hours(50,500)); var a=n.Throw(1); var b=n.Throw(3);
   n.Settle(b);
   Check(n.St.Phase==NightPhase.Running,"one chain still open -> no round yet");
   n.Settle(a);
   Check(n.St.Phase==NightPhase.PegPlacement,"last chain settles -> PegPlacement"); }
 // --- The hour multiplier: fixed at the throw, applied once at the close (after score × mult) ---
 { var curve=new ScoreCurve(15,10,1,1f,0.5f);
   var n=new Night(Hours(50,100000),curve);
   var r1=n.ThrowLine(3); n.Settle(r1);
   Check(n.Chains[0].Total==135 && n.Chains[0].HourMultiplier==1f && n.Chains[0].Hour==1,"hour 1 line: (15 + 30) × 3 × 1 = 135");
   n.Ref.EndPlacement();
   var r2=n.ThrowLine(3);
   Check(n.Gains[n.Gains.Count-1].Hour==2 && n.Gains[n.Gains.Count-1].HourMultiplier==1.5f && n.Gains[n.Gains.Count-1].Score==45,
     "hour 2: gains are raw (score 45, no hour yet) and say the chain's hour (2, ×1.5)");
   n.Settle(r2);
   Check(n.Chains[1].Total==203 && n.Chains[1].Remainder==203-45,"hour 2 line: 45 × 3 × 1.5 = 202.5 -> 203; the remainder 158 at the close");
   Check(n.Chains[0].Hour==1 && n.Chains[1].Hour==2,"ChainScored.Hour: 1, then 2"); }
 // --- Raw then the remainder: the bar moves as the chain falls, the mult lands at the close (M10.S) ---
 { var n=new Night(Hours(50,5000));
   var t=n.ThrowLine(2);              // raw 40 (2 wolves × 20), mult 2 (depth 1)
   Check(n.St.Score==40 && n.St.ThresholdsReached==0,"raw: the night has 40 while the chain falls — below 50");
   n.Settle(t);
   Check(n.St.Score==80 && n.Chains[0].Remainder==40 && n.St.ThresholdsReached==1 && n.St.Phase==NightPhase.PegPlacement,
     "the close: the remainder (40) crosses 50 — and the chain is closed, so its round starts at once");
   Check(n.St.Hours[0].Score==80,"the hour's score = raw + remainder (80)"); }
 { var n=new Night(Hours(30,5000));
   var other=n.Throw(0); var t=n.ThrowLine(2);   // another chain in the air; then raw 40 crosses 30 mid-flight
   Check(n.St.ThresholdsReached==1 && n.St.Phase==NightPhase.Running,"raw crossing mid-flight: counted at once; the round waits");
   n.Settle(t);
   Check(n.St.Phase==NightPhase.Running,"...for every chain open at the crossing (the other is still flying)");
   n.Settle(other);
   Check(n.St.Phase==NightPhase.PegPlacement,"both landed: the round"); }
 // --- One chain crossing several thresholds: one round each, back to back ---
 { var n=new Night(Hours(20,40,1000));
   var t=n.Throw(3);   // 10, 30, 60: crosses 20 and 40
   Check(n.Hours.Count==0 && n.St.PendingPegRounds==2 && n.St.Hour==1,"one chain crosses 20 and 40: two rounds waiting, still hour 1");
   n.Settle(t);
   Check(n.St.Phase==NightPhase.PegPlacement && n.St.PendingPegRounds==1 && n.St.Hour==2 && n.Hours.Count==1,"first round: hour 2 starts");
   n.Ref.EndPlacement();
   Check(n.St.Phase==NightPhase.PegPlacement && n.St.PendingPegRounds==0 && n.Phases[n.Phases.Count-1].From==NightPhase.PegPlacement
     && n.St.Hour==3 && n.Hours[1].Hour==3 && n.Hours[1].Multiplier==2f && n.Hours[1].Threshold==40,
     "second round right after (PegPlacement -> PegPlacement): hour 3, x2, threshold 40");
   n.Ref.EndPlacement();
   Check(n.St.Phase==NightPhase.Running && n.St.CanThrow && n.St.Hour==3,"then Running, hour 3"); }
 // --- Stone refill per threshold (not at dawn) ---
 { var n=new Night(new NightGoal(new[]{50,200,400},5,2));
   var t=n.Throw(3); n.Settle(t);
   Check(n.St.StonesLeft==6 && n.Stones[n.Stones.Count-1].Cause==StoneChange.Added && n.Stones[n.Stones.Count-1].Delta==2,
     "placement round: +2 stones (StonesChanged Added), 4 -> 6");
   var refills=0; foreach(var c in n.Stones) if(c.Cause==StoneChange.Added) refills++;
   n.Ref.EndPlacement(); n.Play(5); n.Ref.EndPlacement();   // hour 2: 5 × 20 = 100, ×1.5 = 150 -> 210, threshold 200
   n.Play(5);   // hour 3: 100 ×2 = 200 -> 410, dawn
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
   Check(n.Ends.Count==1 && n.St.SweepScore==40 && n.Ends[0].Score==100 && n.Ends[0].BankedScore==100 && n.BankedTo(MasteryDestination.Barn)==100,
     "dawn: the sweep scores flat (2 × the wolf value 20) and the full live score is banked (60 + 40 = 100)");
   Check(!n.AnyWeapon(),"nothing banks to the weapon any more"); }
 // --- Losing (M10.E): out of stones — none left, nothing in flight, no round waiting to refill — any hour before dawn ---
 { var n=new Night(new NightGoal(new[]{500},2,0));
   var a=n.Throw(0); var b=n.Throw(1);
   Check(n.St.StonesLeft==0 && !n.St.CanThrow && n.Ends.Count==0,"all stones thrown -> can't throw, but two chains still fly: not lost yet");
   n.Settle(a);
   Check(n.Ends.Count==0 && n.St.Phase==NightPhase.Running && !n.Ref.OutOfStones,"one chain landed, the other still flies: not lost");
   n.Settle(b);
   Check(n.Ends.Count==1 && n.Ends[0].Result==NightResult.Lost && n.Ends[0].Reason==NightEndReason.OutOfStones,
     "the last chain lands with the pile empty -> Lost, out of stones — at once, no breach needed"); }
 { var n=new Night(new NightGoal(new[]{50,100,5000},3,0));
   n.Play(3); n.Ref.EndPlacement();   // 60: threshold 50
   n.Play(3); n.Ref.EndPlacement();   // 150 (90 in hour 2): threshold 100
   n.Wall.Add(n.Ids.Next());
   n.Play(1);                         // 150 + 20 ×2 = 190; 0 stones left, it lands
   Check(n.Ends.Count==1 && n.Ends[0].Result==NightResult.Lost && n.Ends[0].Score==190 && n.Ends[0].BankedScore==100 && n.BankedTo(MasteryDestination.Barn)==100,
     "out of stones in hour 3 at 190: banks the last threshold reached (100), not the live score");
   Check(n.St.SweepScore==0 && n.St.Score==190,"a lost night's sweep scores nothing"); }
 { var n=new Night(new NightGoal(new[]{500},1,0)); n.Play(1);
   Check(n.Ends[0].BankedScore==0 && n.Banked.Count==0,"out of stones before the first threshold: banks nothing"); }
 { // A pending round's refill comes first: the chain that crossed with the last stone lands -> the round, not the loss.
   var n=new Night(new NightGoal(new[]{50,5000},1,2));
   var t=n.Throw(3);   // crosses 50 with the last stone; 0 stones, round pending
   n.Settle(t);
   Check(n.Ends.Count==0 && n.St.Phase==NightPhase.PegPlacement && n.St.StonesLeft==2,"0 stones + a pending round: the round starts and refills (0 -> 2), no loss");
   n.Ref.EndPlacement();
   Check(n.Ends.Count==0 && n.St.CanThrow,"after the round: stones to throw, the night goes on"); }
 { // A round with no refill can't save you: the loss comes as it ends, banked at the threshold it crossed.
   var n=new Night(new NightGoal(new[]{50,5000},1,0));
   n.Play(3);
   Check(n.Ends.Count==0 && n.St.Phase==NightPhase.PegPlacement && n.St.StonesLeft==0,"0 stones, the crossing chain landed: the round first (no loss inside it)");
   n.Ref.EndPlacement();
   Check(n.Ends.Count==1 && n.Ends[0].Reason==NightEndReason.OutOfStones && n.Ends[0].BankedScore==50,
     "the round ends with an empty pile -> out of stones, banks that threshold (50)"); }
 { // A round waiting on a chain thrown with the last stone: a breach on the empty pile meanwhile changes nothing.
   var n=new Night(new NightGoal(new[]{50,5000},1,0));
   var t=n.Throw(3); n.Breach();
   Check(n.Ends.Count==0 && n.St.RobotsReachedTop==1 && n.St.StonesStolen==0,"a breach on an empty pile while a chain flies: counted as a breach, takes nothing, ends nothing");
   n.Settle(t);
   Check(n.St.Phase==NightPhase.PegPlacement && n.Ends.Count==0,"the chain lands: its round, still no loss"); }
 { var n=new Night(new NightGoal(new[]{500},3,0));
   var thief=n.Spawn(); n.Breach(thief);
   Check(n.St.StonesLeft==2 && n.Ends.Count==0 && n.Stones[0].Cause==StoneChange.Stolen && n.Stones[0].Robot==thief && n.St.StonesStolen==1,
     "breach with stones left: the robot takes the top stone (stolen 1), no loss");
   n.Breach();
   Check(n.St.StonesLeft==1 && n.Ends.Count==0,"a second theft: 1 left");
   n.Breach();
   Check(n.St.StonesLeft==0 && n.Ends.Count==1 && n.Ends[0].Reason==NightEndReason.OutOfStones && n.Ends[0].Breaches==3 && n.St.StonesStolen==3,
     "the 3rd theft takes the last stone with nothing in flight -> out of stones at once (3 stolen)");
   n.Breach();
   Check(n.Ends.Count==1 && n.St.RobotsReachedTop==3,"NightEnded fires once; nothing counts after the end"); }
 { // A theft of the last stone while a chain still flies: not lost until it lands.
   var n=new Night(new NightGoal(new[]{500},2,0));
   var t=n.Throw(1); n.Breach();
   Check(n.St.StonesLeft==0 && n.Ends.Count==0,"the last stone stolen while a chain flies: not lost yet");
   n.Settle(t);
   Check(n.Ends.Count==1 && n.Ends[0].Reason==NightEndReason.OutOfStones,"...lost when it lands"); }
 { // Dawn with an empty pile is still dawn.
   var n=new Night(new NightGoal(new[]{50},1,0)); n.Play(3);
   Check(n.Ends.Count==1 && n.Ends[0].Reason==NightEndReason.Dawn && n.Ends[0].Result==NightResult.Won,"the last stone reaches dawn: won, not out of stones"); }
 // --- Breaches count when they start (M7.3), and never during a placement round ---
 { var n=new Night(new NightGoal(new[]{500},3,0));
   var thief=n.Spawn(); n.BreachStart(thief);
   Check(n.Stones.Count==1 && n.Stones[0].Cause==StoneChange.Stolen && n.St.StonesLeft==2 && n.St.RobotsReachedTop==1,"one Stolen at Breaching entry (3 -> 2)");
   n.BreachEnd(thief);
   Check(n.Stones.Count==1 && n.St.RobotsReachedTop==1,"...the removal at the end of the sequence counts nothing"); }
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
