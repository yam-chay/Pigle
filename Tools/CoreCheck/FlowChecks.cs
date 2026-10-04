using System;
using Piglings.Events; using Piglings.Rules; using Piglings.Runtime; using Piglings.Simulation;

// Prototype v2, PR C: what the campaign flow relies on — "the sweep has landed" (SettleTracker) and the camera's
// maths (CameraFraming: the ease between frames, the Tower frame fitted to the built tower).
partial class P{
static void FlowChecks(){
 // --- SettleTracker: nothing off the wall, no chain open ---
 { var bus=new EventBus(); var st=new NightState(); var ids=new IdAllocator(); var tr=new ChainTracker(bus,st); var settle=new SettleTracker(bus,tr);
   Check(settle.Settled && settle.RobotsInMotion==0,"settle: an empty wall is settled");
   var chain=new ChainId(ids.Next()); var stone=ids.Next(); var a=ids.Next();
   bus.Publish(new ThrowReleased(chain,stone,"stone"));
   Check(!settle.Settled && settle.RobotsInMotion==0,"settle: a stone in flight (its chain is open) isn't settled");
   bus.Publish(new RobotLostGrip(a,chain,Attribution.FromThrowable(stone)));
   bus.Publish(new ThrowableRemoved(stone,chain));
   Check(!settle.Settled && settle.RobotsInMotion==1,"settle: the stone landed, its robot still falls — not settled");
   bus.Publish(new RobotRemoved(a,chain,RemovalReason.HitGround));
   Check(settle.Settled,"settle: the robot landed and the chain closed — settled");
   var swept=ids.Next(); bus.Publish(new RobotSwept(swept));
   Check(!settle.Settled && settle.RobotsInMotion==1,"settle: a swept robot (no chain) falling isn't settled");
   bus.Publish(new RobotRemoved(swept,ChainId.None,RemovalReason.TimedOut));
   Check(settle.Settled,"settle: ...until it's removed, whatever the reason (timed out)");
   var thief=ids.Next(); bus.Publish(new RobotBreached(thief));
   Check(!settle.Settled,"settle: a breaching robot's sequence still plays — not settled");
   bus.Publish(new RobotRemoved(thief,ChainId.None,RemovalReason.EnteredBarn));
   bus.Publish(new RobotRemoved(ids.Next(),ChainId.None,RemovalReason.HitGround));
   Check(settle.Settled && settle.RobotsInMotion==0,"settle: the thief left; removing a robot it never saw changes nothing");
   settle.Dispose(); bus.Publish(new RobotSwept(ids.Next()));
   Check(settle.RobotsInMotion==0,"settle: Dispose unsubscribes");
   tr.Dispose(); }

 // --- On a real night: caught with a chain still falling → ended, but not settled until it lands ---
 { var bus=new EventBus(); var st=new NightState(); var ids=new IdAllocator(); var tr=new ChainTracker(bus,st);
   var rf=new NightReferee(bus,st,tr,new NightGoal(new[]{1000},1,0)); var settle=new SettleTracker(bus,tr);
   var chain=new ChainId(ids.Next()); var stone=ids.Next(); var a=ids.Next(); var thief=ids.Next();
   bus.Publish(new ThrowReleased(chain,stone,"stone"));
   bus.Publish(new RobotLostGrip(a,chain,Attribution.FromThrowable(stone)));
   bus.Publish(new RobotBreached(thief));
   Check(st.Ended && st.StonesLeft==0,$"setup: a breach on an empty pile ends the night (ended {st.Ended}, stones {st.StonesLeft})");
   Check(!settle.Settled,"caught: ended, but the chain and the thief are still in play — the flow waits");
   bus.Publish(new ThrowableRemoved(stone,chain)); bus.Publish(new RobotRemoved(a,chain,RemovalReason.HitGround));
   Check(!settle.Settled,"caught: the chain closed, the thief's sequence still plays");
   bus.Publish(new RobotRemoved(thief,ChainId.None,RemovalReason.EnteredBarn));
   Check(settle.Settled,"caught: everything landed — settled, the camera may go down");
   settle.Dispose(); rf.Dispose(); tr.Dispose(); }

 // --- CameraFraming: the ease and the move ---
 Check(CameraFraming.Ease(0f)==0f && CameraFraming.Ease(1f)==1f && Math.Abs(CameraFraming.Ease(0.5f)-0.5f)<1e-6f,"ease: 0 → 0, ½ → ½, 1 → 1");
 Check(CameraFraming.Ease(-1f)==0f && CameraFraming.Ease(2f)==1f,"ease: clamped (a late frame never overshoots the target)");
 Check(CameraFraming.Ease(0.1f)<0.1f && CameraFraming.Ease(0.9f)>0.9f,"ease in-out: slow out of a frame, slow into the next");
 { float prev=-1f; bool rising=true; for(int i=0;i<=100;i++){ float e=CameraFraming.Ease(i/100f); if(e<prev) rising=false; prev=e; }
   Check(rising,"ease: never goes back"); }
 { var barn=new CameraPose(1.16f,1.15f); var night=new CameraPose(6f,3.5f);
   var mid=CameraFraming.Between(barn,night,0.5f); var end=CameraFraming.Between(barn,night,1.5f);
   Check(Math.Abs(mid.Y-3.58f)<1e-4f && Math.Abs(mid.Size-2.325f)<1e-4f,"between: halfway is halfway (y and size together)");
   Check(end.Y==6f && end.Size==3.5f && CameraFraming.Between(barn,night,0f).Y==1.16f,"between: ends exactly on the frames"); }

 // --- CameraFraming: the Tower frame ---
 { var t=CameraFraming.Tower(0f,10f,3f,0.5f,16f/9f);
   Check(Math.Abs(t.Y-5f)<1e-5f && Math.Abs(t.Size-5.5f)<1e-5f,$"tower: centred on the tower, half its height + margin (y 5, size 5.5; got {t})");
   var taller=CameraFraming.Tower(0f,13.2f,3f,0.5f,16f/9f);
   Check(taller.Size>t.Size && taller.Y>t.Y,"tower: one more slice → a bigger frame, higher up");
   var narrow=CameraFraming.Tower(0f,2f,3f,0.5f,0.5f);
   Check(Math.Abs(narrow.Size-7f)<1e-5f,$"tower: a narrow screen widens the frame until the barn fits across ((3 + 0.5) / 0.5 = 7; got {narrow.Size})");
   var flipped=CameraFraming.Tower(10f,0f,3f,0.5f,16f/9f);
   Check(flipped.Y==t.Y && flipped.Size==t.Size,"tower: top and bottom swapped → the same frame"); }

 // --- CameraFraming: the Night frame, fitted to the tower (M10.D), with the night's override ---
 // TestNight: slices from 3.0 every 1.6, roof 3.3 above the tower's top, bottom 2.5 → Yam's hand-tuned frames.
 { Func<int,CameraPose> night=n=>CameraFraming.Night(2.5f,3f+n*1.6f+3.3f);
   var n1=night(2); var n2=night(3); var n3=night(4);
   Check(Math.Abs(n1.Y-6f)<1e-4f && Math.Abs(n1.Size-3.5f)<1e-4f,$"night frame: 2 slices = Night 1's tuned frame (y 6, size 3.5; got {n1})");
   Check(Math.Abs(n2.Y-6.8f)<1e-4f && Math.Abs(n2.Size-4.3f)<1e-4f && Math.Abs(n3.Y-7.6f)<1e-4f && Math.Abs(n3.Size-5.1f)<1e-4f,
         $"night frame: 3 and 4 slices = the hand-tuned 6.8/4.3 and 7.6/5.1 (got {n2}; {n3})");
   Check(Math.Abs((n3.Y-n3.Size)-2.5f)<1e-4f && Math.Abs((n1.Y-n1.Size)-2.5f)<1e-4f,"night frame: the bottom never moves; a taller tower zooms out upward");
   var auto=CameraFraming.Override(n2,0f,0f); var both=CameraFraming.Override(n2,9f,4f); var sizeOnly=CameraFraming.Override(n2,0f,5f);
   Check(auto.Y==n2.Y && auto.Size==n2.Size,"override 0 / 0 = auto (the fitted frame)");
   Check(both.Y==9f && both.Size==4f && sizeOnly.Y==n2.Y && sizeOnly.Size==5f,"override: a value above 0 wins, each on its own"); }
}
}
