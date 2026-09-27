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
 Check(st.Score==50,$"score: 10x1 + 20x2 = 50 (got {st.Score})");
 Action<ChainClosed> h=e=>{}; bus.Subscribe(h); bus.Unsubscribe(h); Check(true,"unsubscribe ok");
 tr.Dispose(); Check(tr.OpenChainCount==0,"dispose");
 ScoreChecks();
 ThrowChecks();
}
// Scoring: value flows down the chain. A hitter (stone or ball) carries a value; the robot it knocks
// scores value x (1 + depth); the hitter grows +10 per hit; the robot then carries 10 + what it scored.
static void ScoreChecks(){
 var bus=new EventBus(); var st=new NightState(); var ids=new IdAllocator(); var tr=new ChainTracker(bus,st);
 var hits=new System.Collections.Generic.List<RobotScored>(); ChainScored? scored=null; int scoreSeenAtClose=-1; bool closedFirst=false;
 bus.Subscribe<RobotScored>(e=>hits.Add(e));
 bus.Subscribe<ChainClosed>(e=>{ scoreSeenAtClose=st.Score; closedFirst=!scored.HasValue; });
 bus.Subscribe<ChainScored>(e=>scored=e);
 // Line: stone -> a -> b -> c  (Yam's example: 10, 20x2, 50x3)
 var chain=new ChainId(ids.Next()); var stone=ids.Next(); var a=ids.Next(); var b=ids.Next(); var c=ids.Next();
 bus.Publish(new ThrowReleased(chain,stone));
 bus.Publish(new RobotLostGrip(a,chain,Attribution.FromThrowable(stone)));
 bus.Publish(new RobotLostGrip(b,chain,Attribution.FromRobotBall(a,0)));
 bus.Publish(new RobotLostGrip(c,chain,Attribution.FromRobotBall(b,1)));
 Check(hits.Count==3 && hits[0].Received==10 && hits[1].Received==20 && hits[2].Received==50,"line: received 10, 20, 50");
 Check(MathF.Abs(hits[1].Multiplier-2f)<1e-5f && MathF.Abs(hits[2].Multiplier-3f)<1e-5f,"multiplier by depth x1, x2, x3");
 Check(hits[0].Total==10 && hits[1].Total==40 && hits[2].Total==150,"line: 10, 20x2=40, 50x3=150");
 Check(hits[0].Carries==20 && hits[1].Carries==50 && hits[2].Carries==160,"carries = 10 + what it scored: 20, 50, 160");
 Check(st.Score==200 && !scored.HasValue,"paid immediately (200), no ChainScored while open");
 bus.Publish(new ThrowableRemoved(stone,chain));
 foreach(var r in new[]{a,b,c}) bus.Publish(new RobotRemoved(r,chain,RemovalReason.HitGround));
 Check(scored.HasValue && scored.Value.RobotsDropped==3 && scored.Value.MaxDepth==2 && scored.Value.Total==200,"ChainScored = sum 200");
 Check(closedFirst && scoreSeenAtClose==200,"ChainClosed before ChainScored, nothing added at close");
 // Stone hits three directly: it grows 10 -> 20 -> 30 (all x1)
 hits.Clear(); scored=null; int before=st.Score;
 var ch2=new ChainId(ids.Next()); var s2=ids.Next(); var w=new[]{ids.Next(),ids.Next(),ids.Next()};
 bus.Publish(new ThrowReleased(ch2,s2));
 foreach(var r in w) bus.Publish(new RobotLostGrip(r,ch2,Attribution.FromThrowable(s2)));
 Check(hits[0].Total==10 && hits[1].Total==20 && hits[2].Total==30,"stone multi-hit: 10, 20, 30");
 // ...then the 2nd robot's ball (carries 10 + 20 = 30) knocks two more: 30x2, then (grown) 40x2
 var d=ids.Next(); var e2=ids.Next();
 bus.Publish(new RobotLostGrip(d,ch2,Attribution.FromRobotBall(w[1],0)));
 bus.Publish(new RobotLostGrip(e2,ch2,Attribution.FromRobotBall(w[1],0)));
 Check(hits[3].Received==30 && hits[3].Total==60 && hits[4].Received==40 && hits[4].Total==80,"ball multi-hit: 30x2=60, 40x2=80");
 bus.Publish(new ThrowableRemoved(s2,ch2)); foreach(var r in new[]{w[0],w[1],w[2],d,e2}) bus.Publish(new RobotRemoved(r,ch2,RemovalReason.HitGround));
 Check(scored.HasValue && scored.Value.Total==200 && st.Score==before+200,"mixed chain: 10+20+30+60+80 = 200");
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
 // Gentle mode: carry what it RECEIVED + 10 -> line 10, 40, 90
 { var bus2=new EventBus(); var st2=new NightState(); var tr2=new ChainTracker(bus2,st2,new ScoreCurve(10,1f,false)); var totals=new System.Collections.Generic.List<int>();
   bus2.Subscribe<RobotScored>(x=>totals.Add(x.Total));
   var ch=new ChainId(ids.Next()); var st0=ids.Next(); var r1=ids.Next(); var r2=ids.Next(); var r3=ids.Next();
   bus2.Publish(new ThrowReleased(ch,st0));
   bus2.Publish(new RobotLostGrip(r1,ch,Attribution.FromThrowable(st0)));
   bus2.Publish(new RobotLostGrip(r2,ch,Attribution.FromRobotBall(r1,0)));
   bus2.Publish(new RobotLostGrip(r3,ch,Attribution.FromRobotBall(r2,1)));
   Check(totals.Count==3 && totals[0]==10 && totals[1]==40 && totals[2]==90,"carryScoredTotal off: line 10, 40, 90"); tr2.Dispose(); }
 // A very deep line never wraps negative
 { var bus3=new EventBus(); var st3=new NightState(); var tr3=new ChainTracker(bus3,st3);
   var ch=new ChainId(ids.Next()); var s0=ids.Next(); bus3.Publish(new ThrowReleased(ch,s0));
   var prev=ids.Next(); bus3.Publish(new RobotLostGrip(prev,ch,Attribution.FromThrowable(s0)));
   for(int dd=0;dd<20;dd++){ var next=ids.Next(); bus3.Publish(new RobotLostGrip(next,ch,Attribution.FromRobotBall(prev,dd))); prev=next; }
   Check(st3.Score==int.MaxValue,"depth-20 line saturates at int.MaxValue instead of going negative"); tr3.Dispose(); }
 // The curve on its own
 Check(new ScoreCurve(5,0.5f).RobotTotal(5,1)==8,"7.5 rounds to 8");
 Check(new ScoreCurve(10,-1f).Multiplier(3)==1f,"negative multiplierPerDepth clamped to 0");
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
