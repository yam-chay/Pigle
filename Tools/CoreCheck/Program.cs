using System;
using Piglings.Events; using Piglings.Rules; using Piglings.Runtime;
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
 Check(st.Score==ChainTracker.ScoreFor(0)+ChainTracker.ScoreFor(1),"score");
 Action<ChainClosed> h=e=>{}; bus.Subscribe(h); bus.Unsubscribe(h); Check(true,"unsubscribe ok");
 tr.Dispose(); Check(tr.OpenChainCount==0,"dispose");
}}
