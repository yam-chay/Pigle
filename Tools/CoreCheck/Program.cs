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
 Check(st.Score==45,$"score: (10+20) x1.5 = 45 (got {st.Score})");
 Action<ChainClosed> h=e=>{}; bus.Subscribe(h); bus.Unsubscribe(h); Check(true,"unsubscribe ok");
 tr.Dispose(); Check(tr.OpenChainCount==0,"dispose");
 ScoreChecks();
 ThrowChecks();
}
// Scoring: RobotScored per robot as it happens, ChainScored with the multiplied total at close.
static void ScoreChecks(){
 var bus=new EventBus(); var st=new NightState(); var ids=new IdAllocator(); var tr=new ChainTracker(bus,st);
 var robotPts=new System.Collections.Generic.List<int>(); ChainScored? scored=null; int scoreSeenAtClose=-1; bool closedFirst=false;
 bus.Subscribe<RobotScored>(e=>robotPts.Add(e.Points));
 bus.Subscribe<ChainClosed>(e=>{ scoreSeenAtClose=st.Score; closedFirst=!scored.HasValue; });
 bus.Subscribe<ChainScored>(e=>scored=e);
 // stone -> a -> b -> c : depths 0,1,2
 var chain=new ChainId(ids.Next()); var stone=ids.Next(); var a=ids.Next(); var b=ids.Next(); var c=ids.Next();
 bus.Publish(new ThrowReleased(chain,stone));
 bus.Publish(new RobotLostGrip(a,chain,Attribution.FromThrowable(stone)));
 bus.Publish(new RobotLostGrip(b,chain,Attribution.FromRobotBall(a,0)));
 bus.Publish(new RobotLostGrip(c,chain,Attribution.FromRobotBall(b,1)));
 Check(robotPts.Count==3 && robotPts[0]==10 && robotPts[1]==20 && robotPts[2]==30,"RobotScored +10 +20 +30 by depth");
 Check(st.Score==60 && !scored.HasValue,"robot points paid immediately, no ChainScored while open");
 bus.Publish(new ThrowableRemoved(stone,chain));
 foreach(var r in new[]{a,b,c}) bus.Publish(new RobotRemoved(r,chain,RemovalReason.HitGround));
 Check(scored.HasValue && scored.Value.RobotsDropped==3 && scored.Value.MaxDepth==2 && scored.Value.RobotPoints==60
   && MathF.Abs(scored.Value.Multiplier-2f)<1e-5f && scored.Value.Total==120,"ChainScored 3 robots, depth 2, 60 x2 = 120");
 Check(st.Score==120,"score includes the chain bonus");
 Check(closedFirst && scoreSeenAtClose==120,"ChainClosed before ChainScored, state already final");
 // a miss still publishes ChainScored, with zeros, and changes nothing
 scored=null; var miss=new ChainId(ids.Next()); var s2=ids.Next();
 bus.Publish(new ThrowReleased(miss,s2)); bus.Publish(new ThrowableRemoved(s2,miss));
 Check(scored.HasValue && scored.Value.RobotsDropped==0 && scored.Value.Total==0 && st.Score==120,"miss -> ChainScored 0, score unchanged");
 // three direct hits (breadth, depth 0) are worth far less than the depth-2 chain
 scored=null; var wide=new ChainId(ids.Next()); var s3=ids.Next(); var w=new[]{ids.Next(),ids.Next(),ids.Next()};
 bus.Publish(new ThrowReleased(wide,s3));
 foreach(var r in w) bus.Publish(new RobotLostGrip(r,wide,Attribution.FromThrowable(s3)));
 bus.Publish(new ThrowableRemoved(s3,wide)); foreach(var r in w) bus.Publish(new RobotRemoved(r,wide,RemovalReason.HitGround));
 Check(scored.HasValue && scored.Value.Total==30 && MathF.Abs(scored.Value.Multiplier-1f)<1e-5f,"3 direct hits = 30 x1 (depth beats breadth)");
 tr.Dispose();
 // the curve on its own
 var curve=new ScoreCurve();
 Check(MathF.Abs(curve.Multiplier(0)-1f)<1e-5f && MathF.Abs(curve.Multiplier(1)-1.5f)<1e-5f,"multiplier x1 at depth 0, x1.5 at depth 1");
 Check(MathF.Abs(curve.Multiplier(20)-4f)<1e-5f,"multiplier capped at 4");
 Check(new ScoreCurve(10,0,0.25f).ChainTotal(10,1)==13,"12.5 rounds to 13");
 Check(new ScoreCurve(5,5,0f,0.5f).Multiplier(3)==1f,"max below 1 is clamped to 1 (never shrinks a chain)");
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
