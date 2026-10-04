using System;
using Piglings.Events; using Piglings.Meta; using Piglings.Rules; using Piglings.Runtime;

// Peg effects (M8.5): what a peg does when a stone or a falling ball hits it. PR 1: the plumbing + Bouncy.
partial class P{
static bool Near(float a,float b)=>MathF.Abs(a-b)<1e-4f;

static void PegEffectChecks(){
 // Sockets: 0 = Bouncy L1, 1 = Bouncy L1, 2 = Bouncy L2, 3 = Plain, 4 = empty. Bouncy levels: ×2, ×3 (level 3+ uses the last).
 var bouncy=new PegType("peg_bouncy",3,true,PegEffect.Bouncy,new[]{2f,3f});
 var pegs=new PegSetup(new[]{(bouncy,5),(new PegType("peg_plain"),1)},1,5);
 var n=new Night(Hours(100000),null,pegs); var fx=new PegEffects(n.Bus,n.St,n.Tr,pegs,n.Ids);
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
 var n2=new Night(new NightGoal(new[]{1000},1,0),null,pegs); var fx2=new PegEffects(n2.Bus,n2.St,n2.Tr,pegs,n2.Ids);
 n2.St.Sockets[0].PegId="peg_bouncy"; n2.St.Sockets[0].Level=1;
 int hits2=0; n2.Bus.Subscribe<PegHit>(e=>hits2++);
 n2.Play(0); n2.Breach();
 Check(n2.St.Ended && fx2.Hit(0,PegHitter.Ball,n2.Ids.Next(),ChainId.None).Outcome==PegOutcome.None && hits2==0,"once the night has ended: nothing");
 fx.Dispose(); fx2.Dispose();
}

static void SplitterChecks(){
 // Sockets: 0 = Splitter L1 (2 stones), 1 and 2 = Splitter L2 (3 stones), 3 = a half-share Splitter L1,
 // 4 = a Splitter whose pieces don't count for mastery. Cap: 4 stones of one throw in flight.
 var splitter=new PegType("peg_splitter",3,true,PegEffect.Splitter,null,new[]{2,3},new[]{1f,1f},4,true);
 var halves=new PegType("peg_halves",3,true,PegEffect.Splitter,null,new[]{2},new[]{0.5f},4,true);
 var uncounted=new PegType("peg_uncounted",3,true,PegEffect.Splitter,null,new[]{2},new[]{1f},4,false);
 var pegs=new PegSetup(new[]{(splitter,5),(halves,1),(uncounted,1)},1,5);
 var n=new Night(Hours(100000),null,pegs); var fx=new PegEffects(n.Bus,n.St,n.Tr,pegs,n.Ids); var tally=new MasteryTally(n.Bus,n.St);
 void Put(int socket,string id,int level){ n.St.Sockets[socket].PegId=id; n.St.Sockets[socket].Level=level; }
 Put(0,"peg_splitter",1); Put(1,"peg_splitter",2); Put(2,"peg_splitter",2); Put(3,"peg_halves",1); Put(4,"peg_uncounted",1);
 var splits=new System.Collections.Generic.List<StoneSplit>(); n.Bus.Subscribe<StoneSplit>(e=>splits.Add(e));
 var hits=new System.Collections.Generic.List<PegHit>(); n.Bus.Subscribe<PegHit>(e=>hits.Add(e));
 // What the Simulation does with a Split result: launch that many pieces from the stone, each announcing itself.
 GameId[] Launch(ChainId chain,GameId parent,int socket,PegHitResult r){
  var made=new GameId[r.NewPieces];
  for(int i=0;i<made.Length;i++){ made[i]=n.Ids.Next(); n.Bus.Publish(new StonePieceLaunched(chain,made[i],parent,socket)); }
  return made;
 }

 // The stone knocks a robot first (its value grows 10 → 20), then splits at a level-1 needle: 1 new piece.
 var c=new ChainId(n.Ids.Next()); var s=n.Ids.Next(); var r0=n.Ids.Next();
 n.Bus.Publish(new ThrowReleased(c,s,"stone"));
 n.Bus.Publish(new RobotLostGrip(r0,c,Attribution.FromThrowable(s)));
 int stonesLeft=n.St.StonesLeft, throwsUsed=n.St.ThrowsUsed;
 var res=fx.Hit(0,PegHitter.Stone,s,c);
 Check(res.Outcome==PegOutcome.Split && res.NewPieces==1 && splits.Count==1 && splits[0].NewPieces==1,"a level-1 needle splits the stone in 2 (1 new piece)");
 var p1=Launch(c,s,0,res)[0];
 Check(n.Tr.StonesInFlight(c)==2,"the piece joins the throw: 2 stones of this chain in flight");
 Check(n.St.StonesLeft==stonesLeft && n.St.ThrowsUsed==throwsUsed,"pieces come from the throw, not the pile: stones left and throws used unchanged");
 // The piece carries the stone's CURRENT value (20, after its hit), share 1
 var r1=n.Ids.Next(); n.Bus.Publish(new RobotLostGrip(r1,c,Attribution.FromThrowable(p1)));
 Check(n.Scored.Find(e=>e.Robot==r1).Received==20,"a piece carries the stone's current value × 1 (20, after the stone's first hit)");
 // Once per needle: the stone again, and the piece born there
 Check(fx.Hit(0,PegHitter.Stone,s,c).Outcome==PegOutcome.None && fx.Hit(0,PegHitter.Stone,p1,c).Outcome==PegOutcome.None && splits.Count==1,
       "once per needle: the stone can't split there again, nor can a piece born there");
 // A piece can split at another needle: level 2 = 3 stones (2 new). 2 + 2 = 4 = the cap.
 var res2=fx.Hit(1,PegHitter.Stone,p1,c);
 Check(res2.Outcome==PegOutcome.Split && res2.NewPieces==2,"a piece splits at a different, level-2 needle: 3 stones (2 new)");
 var more=Launch(c,p1,1,res2);
 Check(n.Tr.StonesInFlight(c)==4,"4 stones of this throw in flight");
 // The cap: nothing more can split
 Check(fx.Hit(2,PegHitter.Stone,s,c).Outcome==PegOutcome.None && splits.Count==2,"at the cap (4 in flight), a needle doesn't split");
 // Robot balls never split
 int hitsBefore=hits.Count;
 Check(fx.Hit(2,PegHitter.Ball,r0,c).Outcome==PegOutcome.None && hits.Count==hitsBefore+1 && hits[hits.Count-1].Hitter==PegHitter.Ball,
       "a robot ball on a needle: a hit, no split");
 // Mastery: direct hits by pieces count as stone hits (toggle on): r0 (stone) + r1 (piece) = 2; one more by a piece-of-a-piece
 var r2=n.Ids.Next(); n.Bus.Publish(new RobotLostGrip(r2,c,Attribution.FromThrowable(more[0])));
 Check(Hits(n.St,"stone")==3,$"pieces' direct hits count for the stone with the toggle on (3, got {Hits(n.St,"stone")})");
 // The chain closes only once every piece is gone
 ChainScored? closed=null; n.Bus.Subscribe<ChainScored>(e=>{ if(e.Chain.Id==c.Id) closed=e; });
 n.Bus.Publish(new ThrowableRemoved(s,c)); n.Bus.Publish(new ThrowableRemoved(p1,c)); n.Bus.Publish(new ThrowableRemoved(more[0],c));
 foreach(var r in new[]{r0,r1,r2}) n.Bus.Publish(new RobotRemoved(r,c,RemovalReason.HitGround));
 Check(!closed.HasValue && n.Tr.StonesInFlight(c)==1,"one piece still flying: the chain stays open");
 n.Bus.Publish(new ThrowableRemoved(more[1],c));
 Check(closed.HasValue && closed.Value.RobotsDropped==3,"the last piece removed: the chain closes (3 robots)");

 // Value share 0.5: the stone AND its piece carry half
 var c2=new ChainId(n.Ids.Next()); var s2=n.Ids.Next(); n.Bus.Publish(new ThrowReleased(c2,s2,"stone"));
 var half=fx.Hit(3,PegHitter.Stone,s2,c2); var hp=Launch(c2,s2,3,half)[0];
 var ra=n.Ids.Next(); var rb=n.Ids.Next();
 n.Bus.Publish(new RobotLostGrip(ra,c2,Attribution.FromThrowable(s2))); n.Bus.Publish(new RobotLostGrip(rb,c2,Attribution.FromThrowable(hp)));
 Check(n.Scored.Find(e=>e.Robot==ra).Received==5 && n.Scored.Find(e=>e.Robot==rb).Received==5,"value share 0.5: the stone and its piece each carry 10 × 0.5 = 5");

 // Toggle off: the piece's hits don't count; the stone's own still do
 int before=Hits(n.St,"stone");
 var c3=new ChainId(n.Ids.Next()); var s3=n.Ids.Next(); n.Bus.Publish(new ThrowReleased(c3,s3,"stone"));
 var up=Launch(c3,s3,4,fx.Hit(4,PegHitter.Stone,s3,c3))[0];
 n.Bus.Publish(new RobotLostGrip(n.Ids.Next(),c3,Attribution.FromThrowable(up)));
 n.Bus.Publish(new RobotLostGrip(n.Ids.Next(),c3,Attribution.FromThrowable(s3)));
 Check(Hits(n.St,"stone")==before+1,"toggle off: a piece's hit doesn't count for the stone; the stone's own does");

 // Levels: past the list uses the last entry; an unfilled 0 = never splits
 Check(splitter.PiecesAt(3)==3 && splitter.PiecesAt(0)==2,"pieces by level: past the list uses the last, below 1 the first");
 var never=new PegType("x",3,true,PegEffect.Splitter,null,new[]{0},null,4,true);
 Check(never.PiecesAt(1)==1,"an unfilled (0) pieces entry = 1 stone = no split");
 // A stone outside an open chain can't split
 Check(fx.Hit(2,PegHitter.Stone,n.Ids.Next(),ChainId.None).Outcome==PegOutcome.None,"a stone with no open chain: no split");
 fx.Dispose(); tally.Dispose();
}

static void BombChecks(){
 // Sockets: 0, 1, 5, 6 = Bomb L1 (cooldown 5 s), 2 = Bomb L2 (cooldown 3 s), 3 = Splitter whose pieces count,
 // 4 = Splitter whose pieces don't.
 var bomb=new PegType("peg_bomb",3,true,PegEffect.Bomb,cooldowns:new[]{5f,3f});
 var counted=new PegType("peg_splitter",3,true,PegEffect.Splitter,null,new[]{2},new[]{1f},4,true);
 var uncounted=new PegType("peg_uncounted",3,true,PegEffect.Splitter,null,new[]{2},new[]{1f},4,false);
 var pegs=new PegSetup(new[]{(bomb,5),(counted,1),(uncounted,1)},1,7);
 var n=new Night(Hours(100000),null,pegs); var fx=new PegEffects(n.Bus,n.St,n.Tr,pegs,n.Ids); var tally=new MasteryTally(n.Bus,n.St);
 void Put(Night night,int socket,string id,int level){ night.St.Sockets[socket].PegId=id; night.St.Sockets[socket].Level=level; }
 Put(n,0,"peg_bomb",1); Put(n,1,"peg_bomb",1); Put(n,2,"peg_bomb",2); Put(n,3,"peg_splitter",1); Put(n,4,"peg_uncounted",1);
 Put(n,5,"peg_bomb",1); Put(n,6,"peg_bomb",1);
 var booms=new System.Collections.Generic.List<BombExploded>(); n.Bus.Subscribe<BombExploded>(e=>booms.Add(e));
 var recharged=new System.Collections.Generic.List<PegRecharged>(); n.Bus.Subscribe<PegRecharged>(e=>recharged.Add(e));
 // What the Simulation does with an Exploded result: knock the robots in range loose, attributed to the explosion.
 GameId[] Knock(ChainId chain,PegHitResult r,int count){
  var v=new GameId[count];
  for(int i=0;i<count;i++){ v[i]=n.Ids.Next(); n.Bus.Publish(new RobotLostGrip(v[i],chain,Attribution.FromPeg(r.Explosion,r.VictimDepth))); }
  return v;
 }
 GameId[] Split(ChainId chain,GameId stone,int socket){
  var r=fx.Hit(socket,PegHitter.Stone,stone,chain); var made=new GameId[r.NewPieces];
  for(int i=0;i<made.Length;i++){ made[i]=n.Ids.Next(); n.Bus.Publish(new StonePieceLaunched(chain,made[i],stone,socket)); }
  return made;
 }

 // --- A stone sets it off: victims 1 deep, scored through the explosion as its own hitter ---
 var c=new ChainId(n.Ids.Next()); var s=n.Ids.Next(); n.Bus.Publish(new ThrowReleased(c,s,"stone"));
 var res=fx.Hit(0,PegHitter.Stone,s,c);
 Check(res.Outcome==PegOutcome.Exploded && res.VictimDepth==1 && booms.Count==1 && booms[0].Trigger==PegHitter.Stone && booms[0].PegId=="peg_bomb",
       "a stone sets a charged bomb off: BombExploded, victims 1 deep");
 var v=Knock(c,res,3);
 var sc=new int[3]; for(int i=0;i<3;i++) sc[i]=n.Scored.Find(e=>e.Robot==v[i]).Received;
 Check(sc[0]==10 && sc[1]==20 && sc[2]==30,$"the explosion starts at the stone's value and grows per victim: 10, 20, 30 (got {sc[0]}, {sc[1]}, {sc[2]})");
 Check(n.Scored.Find(e=>e.Robot==v[0]).Total==15,"scored by the chain rules: 10 × 1.5 (depth 1) = 15");
 var direct=n.Ids.Next(); n.Bus.Publish(new RobotLostGrip(direct,c,Attribution.FromThrowable(s)));
 Check(n.Scored.Find(e=>e.Robot==direct).Received==10,"the trigger doesn't grow from the explosion's victims: the stone's next hit still gets 10");
 Check(Hits(n.St,"stone")==4,$"stone-triggered: its 3 victims count as stone hits (+1 direct = 4, got {Hits(n.St,"stone")})");
 Check(Count(n.St.PegKnocks,"peg_bomb")==3 && n.St.KnockedByBall.Count==0,"knocks recorded for the peg (3); none counted as ball knocks");
 // --- Spent: a plain hold, no charge used, until it recharges ---
 Check(n.St.Sockets[0].IsSpent && Near(n.St.Sockets[0].Recharge,5f),"spent after going off, for the level's cooldown (5 s)");
 int hitsBefore=0; n.Bus.Subscribe<PegHit>(e=>hitsBefore++);
 Check(fx.Hit(0,PegHitter.Stone,s,c).Outcome==PegOutcome.None && booms.Count==1 && hitsBefore==1,"a spent bomb: a hit (PegHit), no explosion");
 fx.Advance(4f);
 Check(n.St.Sockets[0].IsSpent && recharged.Count==0,"4 s of a 5 s cooldown: still spent");
 fx.Advance(1.5f);
 Check(!n.St.Sockets[0].IsSpent && recharged.Count==1 && recharged[0].Socket==0,"cooldown over: PegRecharged, charged again");
 Check(fx.Hit(0,PegHitter.Stone,s,c).Outcome==PegOutcome.Exploded,"...and it can go off again");
 var l2=fx.Hit(2,PegHitter.Stone,s,c);
 Check(l2.Outcome==PegOutcome.Exploded && Near(n.St.Sockets[2].Recharge,3f),"a level-2 bomb: the level-2 cooldown (3 s)");

 // --- A ball sets it off: victims the ball's depth + 1; nothing for the stone ---
 int stoneHits=Hits(n.St,"stone"), knocks=Count(n.St.PegKnocks,"peg_bomb");
 // v[0] (knocked loose by the explosion, 1 deep) is now a falling ball.
 var byBall=fx.Hit(1,PegHitter.Ball,v[0],c);
 Check(byBall.Outcome==PegOutcome.Exploded && byBall.VictimDepth==2 && booms[booms.Count-1].Trigger==PegHitter.Ball,
       "a ball 1 deep sets a bomb off: its victims are 2 deep");
 var w=Knock(c,byBall,2);
 Check(n.Scored.Find(e=>e.Robot==w[0]).Received==20,"the explosion starts at the ball's value (10 + the 10 it received = 20)");
 Check(Hits(n.St,"stone")==stoneHits && Count(n.St.PegKnocks,"peg_bomb")==knocks+2,"ball-triggered: no stone hits; the peg's knocks still recorded");
 // A ball that isn't one of the chain's robots can't set it off (nothing to score into), and keeps the charge
 Check(fx.Hit(5,PegHitter.Ball,n.Ids.Next(),c).Outcome==PegOutcome.None && !n.St.Sockets[5].IsSpent,"an unknown ball: no explosion, charge kept");
 Check(fx.Hit(5,PegHitter.Stone,n.Ids.Next(),ChainId.None).Outcome==PegOutcome.None && !n.St.Sockets[5].IsSpent,"no open chain: no explosion, charge kept");

 // --- A split piece sets it off: counts for the stone only under the Splitter's toggle ---
 var c2=new ChainId(n.Ids.Next()); var s2=n.Ids.Next(); n.Bus.Publish(new ThrowReleased(c2,s2,"stone"));
 var pc=Split(c2,s2,3)[0]; stoneHits=Hits(n.St,"stone");
 Knock(c2,fx.Hit(5,PegHitter.Stone,pc,c2),2);
 Check(Hits(n.St,"stone")==stoneHits+2,"a counted piece sets it off: its victims count as stone hits");
 var c3=new ChainId(n.Ids.Next()); var s3=n.Ids.Next(); n.Bus.Publish(new ThrowReleased(c3,s3,"stone"));
 var pu=Split(c3,s3,4)[0]; stoneHits=Hits(n.St,"stone"); knocks=Count(n.St.PegKnocks,"peg_bomb");
 Knock(c3,fx.Hit(6,PegHitter.Stone,pu,c3),2);
 Check(Hits(n.St,"stone")==stoneHits && Count(n.St.PegKnocks,"peg_bomb")==knocks+2,"a piece whose split doesn't count: no stone hits; peg knocks recorded");
 fx.Dispose(); tally.Dispose();

 // --- PegPlacement: a bomb is a plain hold (no charge used), and recharging pauses ---
 { var pegs2=new PegSetup(new[]{(bomb,2)},1,2);
   var m=new Night(Hours(10,100000),null,pegs2); var fx2=new PegEffects(m.Bus,m.St,m.Tr,pegs2,m.Ids);
   m.St.Sockets[0].PegId="peg_bomb"; m.St.Sockets[0].Level=1;
   m.St.Sockets[1].PegId="peg_bomb"; m.St.Sockets[1].Level=1; m.St.Sockets[1].Recharge=2f;
   var a=m.Throw(1);   // 10 points = the threshold: its round waits for this chain only
   var b=m.Throw(0);   // thrown after the crossing: still flying during the round
   m.Settle(a);
   Check(m.St.Phase==NightPhase.PegPlacement,"setup: placement round, with a chain thrown after the crossing still in flight");
   Check(fx2.Hit(0,PegHitter.Stone,b.stone,b.chain).Outcome==PegOutcome.None && !m.St.Sockets[0].IsSpent,
         "during PegPlacement a bomb hit is a plain hold: no explosion, the charge is kept");
   fx2.Advance(10f);
   Check(Near(m.St.Sockets[1].Recharge,2f),"recharging pauses during PegPlacement (2 s left after 10 s)");
   m.Ref.EndPlacement();
   fx2.Advance(2.5f);
   Check(m.St.Phase==NightPhase.Running && !m.St.Sockets[1].IsSpent,"back to Running: the recharge resumes and finishes");
   fx2.Dispose(); }

 // --- Banking and the save: caught night, stone-triggered bomb ---
 { var pegs3=new PegSetup(new[]{(bomb,1)},1,1);
   var k=new Night(new NightGoal(new[]{1000},1,0),null,pegs3); var fx3=new PegEffects(k.Bus,k.St,k.Tr,pegs3,k.Ids);
   var t3=new MasteryTally(k.Bus,k.St); var prog=new Progression(k.Bus,null);
   k.St.Sockets[0].PegId="peg_bomb"; k.St.Sockets[0].Level=1;
   var ch=new ChainId(k.Ids.Next()); var st=k.Ids.Next(); k.Bus.Publish(new ThrowReleased(ch,st,"stone"));
   var r=fx3.Hit(0,PegHitter.Stone,st,ch);
   var vs=new[]{k.Ids.Next(),k.Ids.Next()};
   foreach(var x in vs) k.Bus.Publish(new RobotLostGrip(x,ch,Attribution.FromPeg(r.Explosion,r.VictimDepth)));
   k.Settle((ch,st,vs)); k.Breach();
   Check(k.Ends.Count==1 && k.Banked.Exists(b=>b.Destination==MasteryDestination.Peg && b.Id=="peg_bomb" && b.Stat==MasteryStat.PegKnocks && b.Amount==2),
         "the night banks NightBanked(Peg, \"peg_bomb\", PegKnocks, 2) — on a caught night too");
   Check(prog.Profile.Pegs["peg_bomb"].Knocks==2 && prog.Profile.DirectHits("stone")==2,"the profile keeps the peg's knocks, and the stone's 2 hits from its bomb");
   var json=ProfileJson.Write(prog.Profile);
   Check(ProfileJson.Read(json,out var back,out _)==ProfileReadResult.Ok && back.Pegs["peg_bomb"].Knocks==2 && ProfileJson.Write(back)==json,
         "save round trip with the pegs section");
   Check(ProfileJson.Read("{\"version\":1,\"pegs\":{\"peg_bomb\":{\"knocks\":-3}}}",out _,out _)==ProfileReadResult.Corrupt
         && ProfileJson.Read("{\"version\":1,\"pegs\":[]}",out _,out _)==ProfileReadResult.Corrupt,"a bad pegs section is corrupt");
   Check(ProfileJson.Read("{\"version\":1,\"weapons\":{\"stone\":{\"directHits\":4}}}",out var old,out _)==ProfileReadResult.Ok && old.Pegs.Count==0,
         "a save from before pegs still loads (no pegs section = none yet)");
   // Ended: no more recharging
   k.St.Sockets[0].Recharge=2f; fx3.Advance(5f);
   Check(Near(k.St.Sockets[0].Recharge,2f),"once the night has ended, spent bombs stay spent (no more recharging)");
   fx3.Dispose(); t3.Dispose(); prog.Dispose(); }
}
}
