using System;
using Piglings.Events; using Piglings.Rules; using Piglings.Runtime;

// Peg effects (M8.5): what a peg does when a stone or a falling ball hits it. PR 1: the plumbing + Bouncy.
partial class P{
static bool Near(float a,float b)=>MathF.Abs(a-b)<1e-4f;

static void PegEffectChecks(){
 // Sockets: 0 = Bouncy L1, 1 = Bouncy L1, 2 = Bouncy L2, 3 = Plain, 4 = empty. Bouncy levels: ×2, ×3 (level 3+ uses the last).
 var bouncy=new PegType("peg_bouncy",3,true,PegEffect.Bouncy,new[]{2f,3f});
 var pegs=new PegSetup(new[]{(bouncy,5),(new PegType("peg_plain"),1)},1,5);
 var n=new Night(Hours(100000),null,pegs); var fx=new PegEffects(n.Bus,n.St,n.Tr,pegs);
 void Put(int socket,string id,int level){ n.St.Sockets[socket].PegId=id; n.St.Sockets[socket].Level=level; }
 Put(0,"peg_bouncy",1); Put(1,"peg_bouncy",1); Put(2,"peg_bouncy",2); Put(3,"peg_plain",1);
 var hits=new System.Collections.Generic.List<PegHit>(); var bounces=new System.Collections.Generic.List<PegBounced>();
 n.Bus.Subscribe<PegHit>(e=>hits.Add(e)); n.Bus.Subscribe<PegBounced>(e=>bounces.Add(e));

 // stone → r1 (10). r1's ball knocks rA (no bonus yet), bounces off socket 0, knocks rB (×2 from then on).
 var c=new ChainId(n.Ids.Next()); var s=n.Ids.Next(); var r1=n.Ids.Next(); var rA=n.Ids.Next(); var rB=n.Ids.Next();
 n.Bus.Publish(new ThrowReleased(c,s,"stone"));
 n.Bus.Publish(new RobotLostGrip(r1,c,Attribution.FromThrowable(s)));
 n.Bus.Publish(new RobotLostGrip(rA,c,Attribution.FromRobotBall(r1,0)));
 var res=fx.Hit(0,PegHitter.Ball,r1,c);
 n.Bus.Publish(new RobotLostGrip(rB,c,Attribution.FromRobotBall(r1,0)));
 var sA=n.Scored.Find(e=>e.Robot==rA); var sB=n.Scored.Find(e=>e.Robot==rB);
 Check(res.Outcome==PegOutcome.Bounced && bounces.Count==1 && Near(bounces[0].Multiplier,2f) && Near(bounces[0].BallMultiplier,2f),
       "a falling ball bounces off a level-1 Bouncy peg: PegBounced ×2");
 Check(Near(sA.PegMultiplier,1f) && sA.Total==30,"a victim knocked BEFORE the bounce gets no bonus (20 ×1.5 = 30)");
 // r1 carries 20, grown to 30 after rA: rB = 30 × 1.5 (depth 1) × 2 (peg) = 90
 Check(Near(sB.PegMultiplier,2f) && sB.Total==90,$"a victim knocked AFTER the bounce scores ×2: 30 ×1.5 ×2 = 90 (got {sB.Total})");
 Check(sB.Carries==40,$"...but carries only its plain value: 10 + 30 = 40, the peg isn't carried on (got {sB.Carries})");
 // Once per peg per ball
 var again=fx.Hit(0,PegHitter.Ball,r1,c);
 Check(again.Outcome==PegOutcome.None && bounces.Count==1 && hits.Count==2,"the same ball on the same peg again: a hit (PegHit), no second bonus");
 // A second level-1 Bouncy peg adds its extra part: ×2 then ×2 = ×3 (not ×4)
 fx.Hit(1,PegHitter.Ball,r1,c);
 Check(bounces.Count==2 && Near(bounces[1].BallMultiplier,3f),"two level-1 Bouncy pegs stack on the extra part: ×3, not ×4");
 var rC=n.Ids.Next(); n.Bus.Publish(new RobotLostGrip(rC,c,Attribution.FromRobotBall(r1,0)));
 Check(Near(n.Scored.Find(e=>e.Robot==rC).PegMultiplier,3f),"its next victim scores with ×3");
 // The bonus belongs to that ball only: rB's own ball doesn't inherit it
 var rD=n.Ids.Next(); n.Bus.Publish(new RobotLostGrip(rD,c,Attribution.FromRobotBall(rB,1)));
 Check(Near(n.Scored.Find(e=>e.Robot==rD).PegMultiplier,1f),"a victim's own ball starts without the bonus (it was never carried)");
 // Level 2 = ×3; a level past the list uses the last entry
 var r2=n.Ids.Next(); n.Bus.Publish(new RobotLostGrip(r2,c,Attribution.FromThrowable(s)));
 fx.Hit(2,PegHitter.Ball,r2,c);
 Check(Near(bounces[bounces.Count-1].Multiplier,3f),"a level-2 Bouncy peg gives ×3");
 Check(Near(bouncy.ScoreMultiplierAt(3),3f) && Near(bouncy.ScoreMultiplierAt(0),2f),"a level past the list uses the last entry; below 1 uses the first");
 Check(Near(new PegType("x",3,true,PegEffect.Bouncy,new[]{0f}).ScoreMultiplierAt(1),1f),"an unfilled (0) multiplier = no bonus, never ×0");
 // Stones bounce off physically but get no bonus
 int before=bounces.Count;
 var stoneHit=fx.Hit(0,PegHitter.Stone,s,c);
 var r3=n.Ids.Next(); n.Bus.Publish(new RobotLostGrip(r3,c,Attribution.FromThrowable(s)));
 Check(stoneHit.Outcome==PegOutcome.None && bounces.Count==before && hits[hits.Count-1].Hitter==PegHitter.Stone
       && Near(n.Scored.Find(e=>e.Robot==r3).PegMultiplier,1f),"a stone on a Bouncy peg: PegHit, no bonus, its victims score ×1");
 // Plain peg: the fact only. Empty socket: nothing at all.
 int hitCount=hits.Count;
 var plain=fx.Hit(3,PegHitter.Ball,r1,c);
 Check(plain.Outcome==PegOutcome.None && hits.Count==hitCount+1 && hits[hits.Count-1].Effect==PegEffect.Plain,"a plain peg: PegHit (Plain), no effect");
 var empty=fx.Hit(4,PegHitter.Ball,r1,c);
 Check(empty.Outcome==PegOutcome.None && hits.Count==hitCount+1,"an empty socket: nothing published");
 Check(fx.Hit(-1,PegHitter.Ball,r1,c).Outcome==PegOutcome.None && fx.Hit(99,PegHitter.Ball,r1,c).Outcome==PegOutcome.None,"a socket out of range: nothing");
 // A ball outside an open chain (the end-of-night sweep falls with ChainId.None) gets nothing
 var swept=n.Ids.Next();
 Check(fx.Hit(0,PegHitter.Ball,swept,ChainId.None).Outcome==PegOutcome.None && bounces.Count==before,"a ball with no open chain: no bonus");
 // The chain's total includes the bonus
 n.Bus.Publish(new ThrowableRemoved(s,c));
 foreach(var r in new[]{r1,rA,rB,rC,rD,r2,r3}) n.Bus.Publish(new RobotRemoved(r,c,RemovalReason.HitGround));
 int sum=0; foreach(var e in n.Scored) if(e.Chain.Id==c.Id) sum+=e.Total;
 var closed=n.Chains.Find(e=>e.Chain.Id==c.Id);
 Check(closed.Total==sum && sum>0,$"ChainScored = the sum of the robots' bonused totals ({sum})");
 // The hour and the peg multiply together, rounded once
 Check(new ScoreCurve().RobotTotal(20,1,1.5f,2f)==90 && new ScoreCurve().RobotTotal(5,1,1.5f,1.5f)==17,"hour × peg multiply together, rounded once (20 ×1.5 ×1.5 ×2 = 90; 5 ×1.5³ = 16.875 → 17)");
 // Night over: no more peg effects, no facts
 var n2=new Night(new NightGoal(new[]{1000},1,0),null,pegs); var fx2=new PegEffects(n2.Bus,n2.St,n2.Tr,pegs);
 n2.St.Sockets[0].PegId="peg_bouncy"; n2.St.Sockets[0].Level=1;
 int hits2=0; n2.Bus.Subscribe<PegHit>(e=>hits2++);
 n2.Play(0); n2.Breach();
 Check(n2.St.Ended && fx2.Hit(0,PegHitter.Ball,n2.Ids.Next(),ChainId.None).Outcome==PegOutcome.None && hits2==0,"once the night has ended: nothing");
 fx.Dispose(); fx2.Dispose();
}
}
