using System;
using Piglings.Events; using Piglings.Meta; using Piglings.Rules; using Piglings.Runtime;

// Peg effects (M8.5): what a peg does when a stone or a falling ball hits it. PR 1: the plumbing + Bouncy.
partial class P{
static bool Near(float a,float b)=>MathF.Abs(a-b)<1e-4f;

static void PegEffectChecks(){
 // Sockets: 0 = Bouncy L1, 1 = Bouncy L1, 2 = Bouncy L2, 3 = Plain, 4 = empty (a plain hold). Bouncy: +1 mult at level 1,
 // +2 at level 2 (level 3+ uses the last). The real default curve: base 10, wolves 10, plain holds +1.
 var bouncy=new PegType("peg_bouncy",3,true,PegEffect.Bouncy,new[]{1f,2f});
 var pegs=new PegSetup(new[]{(bouncy,5),(new PegType("peg_plain"),1)},1,5);
 var n=new Night(Hours(100000),new ScoreCurve(),pegs); var fx=new PegEffects(n.Bus,n.St,n.Tr,pegs,n.Ids);
 void Put(int socket,string id,int level){ n.St.Sockets[socket].PegId=id; n.St.Sockets[socket].Level=level; }
 Put(0,"peg_bouncy",1); Put(1,"peg_bouncy",1); Put(2,"peg_bouncy",2); Put(3,"peg_plain",1);
 var hits=new System.Collections.Generic.List<PegHit>(); var bounces=new System.Collections.Generic.List<PegBounced>();
 n.Bus.Subscribe<PegHit>(e=>hits.Add(e)); n.Bus.Subscribe<PegBounced>(e=>bounces.Add(e));
 float Mult(ChainId ch){ for(int i=n.Gains.Count-1;i>=0;i--) if(n.Gains[i].Chain.Id==ch.Id) return n.Gains[i].Mult; return 0f; }
 int Score(ChainId ch){ for(int i=n.Gains.Count-1;i>=0;i--) if(n.Gains[i].Chain.Id==ch.Id) return n.Gains[i].Score; return 0; }

 // stone → r1; r1's ball bounces off socket 0 (Bouncy L1): the CHAIN gets +1 mult.
 var c=new ChainId(n.Ids.Next()); var s=n.Ids.Next(); var r1=n.Ids.Next();
 n.Bus.Publish(new ThrowReleased(c,s,"stone"));
 n.Bus.Publish(new RobotLostGrip(r1,c,Attribution.FromThrowable(s)));
 var res=fx.Hit(0,PegHitter.Ball,r1,c);
 Check(res.Outcome==PegOutcome.Bounced && bounces.Count==1 && Near(bounces[0].MultBonus,1f) && Near(bounces[0].ChainMult,2f) && Near(Mult(c),2f),
       "a ball bounces off a level-1 Bouncy peg: + 1 mult to its chain (1 → 2)");
 Check(n.Gains[n.Gains.Count-1].Cause==ChainGainCause.Peg && n.Gains[n.Gains.Count-1].Socket==0 && n.Gains[n.Gains.Count-1].ScoreAdded==0,
       "the bonus is a Peg gain at its socket, mult only (no score)");
 // Once per peg per hitter
 var again=fx.Hit(0,PegHitter.Ball,r1,c);
 Check(again.Outcome==PegOutcome.None && bounces.Count==1 && hits.Count==2 && Near(Mult(c),2f),"the same ball on the same peg again: a hit (PegHit), no second bonus");
 // Stones trigger it too (M10.S), once per peg per stone
 var byStone=fx.Hit(0,PegHitter.Stone,s,c);
 Check(byStone.Outcome==PegOutcome.Bounced && Near(Mult(c),3f) && fx.Hit(0,PegHitter.Stone,s,c).Outcome==PegOutcome.None,
       "a stone on a Bouncy peg: + 1 mult too (3), once per stone");
 // Another peg, another bonus; level 2 = +2
 fx.Hit(1,PegHitter.Ball,r1,c); fx.Hit(2,PegHitter.Ball,r1,c);
 Check(Near(Mult(c),6f) && Near(bounces[bounces.Count-1].MultBonus,2f),"another level-1 peg +1, a level-2 peg +2: mult 6 — pegs add up");
 Check(Near(bouncy.MultBonusAt(3),2f) && Near(bouncy.MultBonusAt(0),1f),"a level past the list uses the last entry; below 1 uses the first");
 Check(Near(new PegType("x",3,true,PegEffect.Bouncy,new[]{-1f}).MultBonusAt(1),0f) && Near(new PegType("y").MultBonusAt(1),0f),"never a negative bonus; none = 0");
 // Plain peg: the fact (PegHit) + 1 score, once per hitter. Empty socket (a plain hold): + 1 score, no fact.
 int hitCount=hits.Count, score=Score(c);
 var plain=fx.Hit(3,PegHitter.Ball,r1,c);
 Check(plain.Outcome==PegOutcome.None && hits.Count==hitCount+1 && hits[hits.Count-1].Effect==PegEffect.Plain && Score(c)==score+1,"a plain peg: PegHit (Plain), + 1 score");
 var empty=fx.Hit(4,PegHitter.Ball,r1,c);
 Check(empty.Outcome==PegOutcome.None && hits.Count==hitCount+1 && Score(c)==score+2,"an empty socket (a plain hold): + 1 score, nothing published but the gain");
 fx.Hit(4,PegHitter.Ball,r1,c); fx.Hit(4,PegHitter.Stone,s,c);
 Check(Score(c)==score+3,"the same hold again by the same ball at once: nothing (cooldown); by the stone (another hitter): + 1");
 fx.Hit(4,PegHitter.Ball,r1,c,0.5f);
 Check(Score(c)==score+4,"the same ball on the same hold after the cooldown: + 1 again (every contact scores)");
 Check(fx.Hit(-1,PegHitter.Ball,r1,c).Outcome==PegOutcome.None && fx.Hit(99,PegHitter.Ball,r1,c).Outcome==PegOutcome.None,"a socket out of range: nothing");
 // A ball outside an open chain (the end-of-night sweep falls with ChainId.None) gets nothing
 int before=bounces.Count; var swept=n.Ids.Next();
 Check(fx.Hit(1,PegHitter.Ball,swept,ChainId.None).Outcome==PegOutcome.None && bounces.Count==before,"a ball with no open chain: no bonus");
 // The close: score × mult (the pegs' mult included)
 n.Bus.Publish(new ThrowableRemoved(s,c)); n.Bus.Publish(new RobotRemoved(r1,c,RemovalReason.HitGround));
 var closed=n.Chains.Find(e=>e.Chain.Id==c.Id);
 Check(closed.Score==24 && Near(closed.Mult,6f) && closed.Total==144,$"the chain: (10 base + 10 wolf + 4 hold contacts) × 6 = 144 (got {closed.Score} × {closed.Mult} = {closed.Total})");
 // Every peg has a SCORE value per level (M10.S): a Plain peg per contact, a special one per trigger; -1 / unset = the default.
 { var plainTen=new PegType("peg_plain10",3,true,PegEffect.Plain,scoreValues:new[]{10});
   var bouncy5=new PegType("peg_bouncy5",3,true,PegEffect.Bouncy,new[]{1f},scoreValues:new[]{5});
   var plainDefault=new PegType("peg_plain_d",3,true,PegEffect.Plain,scoreValues:new[]{-1});
   var ps=new PegSetup(new[]{(plainTen,1),(bouncy5,1),(plainDefault,1)},1,4);
   var m=new Night(Hours(100000),new ScoreCurve(),ps); var fxv=new PegEffects(m.Bus,m.St,m.Tr,ps,m.Ids);
   m.St.Sockets[0].PegId="peg_plain10"; m.St.Sockets[0].Level=1; m.St.Sockets[1].PegId="peg_bouncy5"; m.St.Sockets[1].Level=1;
   m.St.Sockets[2].PegId="peg_plain_d"; m.St.Sockets[2].Level=1;
   var cv=new ChainId(m.Ids.Next()); var sv=m.Ids.Next(); m.Bus.Publish(new ThrowReleased(cv,sv,"stone"));
   fxv.Hit(0,PegHitter.Stone,sv,cv);
   Check(m.Gains[m.Gains.Count-1].ScoreAdded==10,"a Plain peg with its own value (10) adds it per contact");
   fxv.Hit(2,PegHitter.Stone,sv,cv);
   Check(m.Gains[m.Gains.Count-1].ScoreAdded==1,"a Plain peg left at -1 adds the default plain-peg score (1)");
   fxv.Hit(1,PegHitter.Stone,sv,cv);
   Check(m.Gains[m.Gains.Count-1].ScoreAdded==5 && Near(m.Gains[m.Gains.Count-1].MultAdded,1f),"a special peg with a value adds it with its mult bonus when it triggers (+5, +1 mult)");
   Check(bouncy.ScoreValueAt(1)==-1 && new PegType("x").ScoreValueAt(2)==-1,"none set = -1 (the default)");
   // The depth on gains: a ball's own depth on the holds it touches; the stone 0.
   var b1=m.Ids.Next(); var b2=m.Ids.Next();
   m.Bus.Publish(new RobotLostGrip(b1,cv,Attribution.FromThrowable(sv)));
   m.Bus.Publish(new RobotLostGrip(b2,cv,Attribution.FromRobotBall(b1,0)));
   Check(m.Gains[m.Gains.Count-1].Cause==ChainGainCause.Wolf && m.Gains[m.Gains.Count-1].Depth==1,"a wolf's gain carries its depth (1)");
   fxv.Hit(3,PegHitter.Ball,b2,cv);
   Check(m.Gains[m.Gains.Count-1].Cause==ChainGainCause.PlainPeg && m.Gains[m.Gains.Count-1].Depth==1,"a plain hold touched by a depth-1 ball: depth 1");
   fxv.Hit(3,PegHitter.Stone,sv,cv);
   Check(m.Gains[m.Gains.Count-1].Depth==0,"...by the stone: depth 0");
   fxv.Dispose(); }
 // Night over: no more peg effects, no facts
 var n2=new Night(new NightGoal(new[]{1000},1,0),null,pegs); var fx2=new PegEffects(n2.Bus,n2.St,n2.Tr,pegs,n2.Ids);
 n2.St.Sockets[0].PegId="peg_bouncy"; n2.St.Sockets[0].Level=1;
 int hits2=0; n2.Bus.Subscribe<PegHit>(e=>hits2++);
 n2.Play(0);
 Check(n2.St.Ended && fx2.Hit(0,PegHitter.Ball,n2.Ids.Next(),ChainId.None).Outcome==PegOutcome.None && hits2==0,"once the night has ended: nothing");
 fx.Dispose(); fx2.Dispose();
}

static void SplitterChecks(){
 // Sockets: 0 = Splitter L1 (2 stones), 1 and 2 = Splitter L2 (3 stones), 3 = a Splitter with a +2 mult bonus,
 // 4 = a Splitter whose pieces don't count for mastery. Cap: 4 stones of one throw in flight. Real curve (base 10, wolves 10).
 var splitter=new PegType("peg_splitter",3,true,PegEffect.Splitter,null,new[]{2,3},4,true);
 var bonus=new PegType("peg_bonus",3,true,PegEffect.Splitter,new[]{2f},new[]{2},4,true);
 var uncounted=new PegType("peg_uncounted",3,true,PegEffect.Splitter,null,new[]{2},4,false);
 var pegs=new PegSetup(new[]{(splitter,5),(bonus,1),(uncounted,1)},1,5);
 var n=new Night(Hours(100000),new ScoreCurve(),pegs); var fx=new PegEffects(n.Bus,n.St,n.Tr,pegs,n.Ids); var tally=new MasteryTally(n.Bus,n.St);
 void Put(int socket,string id,int level){ n.St.Sockets[socket].PegId=id; n.St.Sockets[socket].Level=level; }
 Put(0,"peg_splitter",1); Put(1,"peg_splitter",2); Put(2,"peg_splitter",2); Put(3,"peg_bonus",1); Put(4,"peg_uncounted",1);
 var splits=new System.Collections.Generic.List<StoneSplit>(); n.Bus.Subscribe<StoneSplit>(e=>splits.Add(e));
 var hits=new System.Collections.Generic.List<PegHit>(); n.Bus.Subscribe<PegHit>(e=>hits.Add(e));
 // What the Simulation does with a Split result: launch that many pieces from the stone, each announcing itself.
 GameId[] Launch(ChainId chain,GameId parent,int socket,PegHitResult r){
  var made=new GameId[r.NewPieces];
  for(int i=0;i<made.Length;i++){ made[i]=n.Ids.Next(); n.Bus.Publish(new StonePieceLaunched(chain,made[i],parent,socket)); }
  return made;
 }

 // The stone knocks a robot first, then splits at a level-1 needle: 1 new piece.
 var c=new ChainId(n.Ids.Next()); var s=n.Ids.Next(); var r0=n.Ids.Next();
 n.Bus.Publish(new ThrowReleased(c,s,"stone"));
 n.Bus.Publish(new RobotLostGrip(r0,c,Attribution.FromThrowable(s)));
 int stonesLeft=n.St.StonesLeft, throwsUsed=n.St.ThrowsUsed;
 var res=fx.Hit(0,PegHitter.Stone,s,c);
 Check(res.Outcome==PegOutcome.Split && res.NewPieces==1 && splits.Count==1 && splits[0].NewPieces==1,"a level-1 needle splits the stone in 2 (1 new piece)");
 var p1=Launch(c,s,0,res)[0];
 Check(n.Tr.StonesInFlight(c)==2,"the piece joins the throw: 2 stones of this chain in flight");
 Check(n.St.StonesLeft==stonesLeft && n.St.ThrowsUsed==throwsUsed,"pieces come from the throw, not the pile: stones left and throws used unchanged");
 // A piece adds no stone base (it isn't a throw); its victims add like any (M10.S)
 int gainsBefore=n.Gains.Count; var r1=n.Ids.Next(); n.Bus.Publish(new RobotLostGrip(r1,c,Attribution.FromThrowable(p1)));
 Check(gainsBefore>0 && n.Gains.Count==gainsBefore+1 && n.Gains[n.Gains.Count-1].Cause==ChainGainCause.Wolf && n.Gains[n.Gains.Count-1].Score==30,
       "a split adds no score by itself; the piece's victim adds its wolf value: 10 base + 10 + 10 = 30");
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

 // A Splitter with a mult bonus adds it when it splits (M10.S: every special peg's level has one; 0 by default)
 var c2=new ChainId(n.Ids.Next()); var s2=n.Ids.Next(); n.Bus.Publish(new ThrowReleased(c2,s2,"stone"));
 Launch(c2,s2,3,fx.Hit(3,PegHitter.Stone,s2,c2));
 Check(n.Gains[n.Gains.Count-1].Cause==ChainGainCause.Peg && Near(n.Gains[n.Gains.Count-1].Mult,3f),"a split at a +2 Splitter: the chain's mult 1 → 3");
 Check(Near(splitter.MultBonusAt(1),0f),"a Splitter with no bonus set adds no mult");

 // Toggle off: the piece's hits don't count; the stone's own still do
 int before=Hits(n.St,"stone");
 var c3=new ChainId(n.Ids.Next()); var s3=n.Ids.Next(); n.Bus.Publish(new ThrowReleased(c3,s3,"stone"));
 var up=Launch(c3,s3,4,fx.Hit(4,PegHitter.Stone,s3,c3))[0];
 n.Bus.Publish(new RobotLostGrip(n.Ids.Next(),c3,Attribution.FromThrowable(up)));
 n.Bus.Publish(new RobotLostGrip(n.Ids.Next(),c3,Attribution.FromThrowable(s3)));
 Check(Hits(n.St,"stone")==before+1,"toggle off: a piece's hit doesn't count for the stone; the stone's own does");

 // Levels: past the list uses the last entry; an unfilled 0 = never splits
 Check(splitter.PiecesAt(3)==3 && splitter.PiecesAt(0)==2,"pieces by level: past the list uses the last, below 1 the first");
 var never=new PegType("x",3,true,PegEffect.Splitter,null,new[]{0},4,true);
 Check(never.PiecesAt(1)==1,"an unfilled (0) pieces entry = 1 stone = no split");
 // A stone outside an open chain can't split
 Check(fx.Hit(2,PegHitter.Stone,n.Ids.Next(),ChainId.None).Outcome==PegOutcome.None,"a stone with no open chain: no split");
 fx.Dispose(); tally.Dispose();
}

static void BombChecks(){
 // Sockets: 0, 1, 5, 6 = Bomb L1 (cooldown 5 s), 2 = Bomb L2 (cooldown 3 s), 3 = Splitter whose pieces count,
 // 4 = Splitter whose pieces don't.
 var bomb=new PegType("peg_bomb",3,true,PegEffect.Bomb,cooldowns:new[]{5f,3f});
 var counted=new PegType("peg_splitter",3,true,PegEffect.Splitter,null,new[]{2},4,true);
 var uncounted=new PegType("peg_uncounted",3,true,PegEffect.Splitter,null,new[]{2},4,false);
 var pegs=new PegSetup(new[]{(bomb,5),(counted,1),(uncounted,1)},1,7);
 var n=new Night(Hours(100000),new ScoreCurve(),pegs); var fx=new PegEffects(n.Bus,n.St,n.Tr,pegs,n.Ids); var tally=new MasteryTally(n.Bus,n.St);
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
 var last=n.Gains[n.Gains.Count-1];
 Check(last.Score==40 && Near(last.Mult,2f),$"the victims add like any wolf (10 base + 3 × 10 = 40) and reach depth 1: + 1 mult, once (got {last.Score} × {last.Mult})");
 var direct=n.Ids.Next(); n.Bus.Publish(new RobotLostGrip(direct,c,Attribution.FromThrowable(s)));
 Check(n.Gains[n.Gains.Count-1].Score==50 && Near(n.Gains[n.Gains.Count-1].Mult,2f),"the stone's own next victim (depth 0): + 10, no mult");
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
 float multBefore=n.Gains[n.Gains.Count-1].Mult;
 var w=Knock(c,byBall,2);
 Check(Near(n.Gains[n.Gains.Count-1].Mult,multBefore+1f),"its victims reach a new depth (2): + 1 mult");
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

 // --- Banking and the save: a lost night, stone-triggered bomb ---
 { var pegs3=new PegSetup(new[]{(bomb,1)},1,1);
   var k=new Night(new NightGoal(new[]{1000},1,0),null,pegs3); var fx3=new PegEffects(k.Bus,k.St,k.Tr,pegs3,k.Ids);
   var t3=new MasteryTally(k.Bus,k.St); var prog=new Progression(k.Bus,null);
   k.St.Sockets[0].PegId="peg_bomb"; k.St.Sockets[0].Level=1;
   var ch=new ChainId(k.Ids.Next()); var st=k.Ids.Next(); k.Bus.Publish(new ThrowReleased(ch,st,"stone"));
   var r=fx3.Hit(0,PegHitter.Stone,st,ch);
   var vs=new[]{k.Ids.Next(),k.Ids.Next()};
   foreach(var x in vs) k.Bus.Publish(new RobotLostGrip(x,ch,Attribution.FromPeg(r.Explosion,r.VictimDepth)));
   k.Settle((ch,st,vs));   // the one stone landed: out of stones
   Check(k.Ends.Count==1 && k.Banked.Exists(b=>b.Destination==MasteryDestination.Peg && b.Id=="peg_bomb" && b.Stat==MasteryStat.PegKnocks && b.Amount==2),
         "the night banks NightBanked(Peg, \"peg_bomb\", PegKnocks, 2) — on a lost night too");
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
