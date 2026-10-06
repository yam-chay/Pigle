using System;
using System.Collections.Generic;
using Piglings.Events; using Piglings.Meta; using Piglings.Rules; using Piglings.Runtime; using Piglings.Simulation;

// M11.T3, the balance log: what BalanceTally gathers (chains, wall density, hours, miss share) and the CSV it becomes.
partial class P{
static void BalanceChecks(){
 { float now=0f; var n=new Night(new NightGoal(new[]{40,5000},10,0));
   var tally=new BalanceTally(n.Bus,n.St,()=>now);
   // Three robots climbing; one throw at t=2 knocks two of them (40 → hour 1 crosses), a miss at t=3.
   var a=n.Ids.Next(); var b=n.Ids.Next(); var c=n.Ids.Next();
   foreach(var r in new[]{a,b,c}) n.Bus.Publish(new RobotSpawned(r,"wolfbot"));
   now=2f; var hit=n.Throw(0);
   n.Bus.Publish(new RobotLostGrip(a,hit.chain,Attribution.FromThrowable(hit.stone)));
   n.Bus.Publish(new RobotLostGrip(b,hit.chain,Attribution.FromThrowable(hit.stone)));
   now=3f; var miss=n.Throw(0);
   Check(tally.AverageWall==2f,$"wall density at each throw: 3 climbing, then 1 → average 2 (got {tally.AverageWall})");
   n.Bus.Publish(new ThrowableRemoved(miss.stone,miss.chain));
   n.Bus.Publish(new ThrowableRemoved(hit.stone,hit.chain));
   n.Bus.Publish(new RobotRemoved(a,hit.chain,RemovalReason.HitGround));
   now=5f; n.Bus.Publish(new RobotRemoved(b,hit.chain,RemovalReason.HitGround));   // closes → 40 crosses hour 1 → round
   Check(tally.Chains.Count==2 && tally.Chains[0].Miss && !tally.Chains[1].Miss && tally.Chains[1].Wolves==2 && tally.Chains[1].Wall==3 && tally.Chains[1].ThrownAt==2f,
     "chains in closing order: the miss first (no wolf), then the hit (2 wolves, thrown at t=2 with 3 climbing)");
   now=6f; n.Ref.EndPlacement();
   now=9f; var late=n.Throw(1); n.Settle(late);
   var hours=tally.Hours();
   Check(hours.Count==2 && hours[0].Chains==2 && hours[0].Wolves==2 && hours[0].Total==40 && hours[1].Chains==1 && hours[1].Wolves==1,
     $"per hour by the hour thrown in: h1 2 chains / 2 wolves / 40; h2 1 chain (got {hours.Count} hours)");
   Check(Math.Abs(hours[0].Seconds-5f)<1e-4f,$"hour 1 lasts from the begin (0) to its round — which starts as the crossing chain closes (5): 5 s (got {hours[0].Seconds})");
   Check(hours[0].MissTotal==0 && hours[0].MissShare==0f,"the test curve gives a miss 0 points: miss share 0"); 
   tally.Dispose(); }
 // Miss share with the real curve (a miss keeps the stone's base).
 { var n=new Night(new NightGoal(new[]{5000},10,0),new ScoreCurve());
   var tally=new BalanceTally(n.Bus,n.St,()=>0f);
   var m1=n.Throw(0); n.Settle(m1); var m2=n.Throw(0); n.Settle(m2); var h=n.Throw(1); n.Settle(h);
   var hour=tally.Hours()[0];
   Check(hour.MissTotal==20 && hour.Total>20 && Math.Abs(hour.MissShare-20f/hour.Total)<1e-5f,
     $"two misses (base 10 each) and a hit: miss share = 20 / {hour.Total}");
   tally.Dispose(); }
 // --- The CSV ---
 { var ctx=new BalanceContext{When="2026-10-07 14:03:22",Run="r1-n2",Scenario="scenario A, hard",Profile="campaign",Night=2,NightId="night_02",ClimbMult=1.5f,SpawnMult=1f};
   var cols=BalanceCsv.Columns.Length;
   var chain=BalanceCsv.ChainRow(ctx,new ChainRecord{Index=3,Hour=2,ThrownAt=12.25f,Score=84,Mult=2.5f,HourMultiplier=1.5f,Total=315,Wolves=4,Depth=2,Wall=7,StonesLeft=5});
   Check(chain.Contains(",chain,2,3,12.25,84,2.5,1.5,315,4,2,0,7,5,") && chain.Contains("\"scenario A, hard\""),
     "a chain row: its fixed columns in order; a cell with a comma is quoted");
   Check(Split(chain).Count==cols,$"every row has every column ({cols})");
   var hourRow=BalanceCsv.HourRow(ctx,new HourRecord{Hour=1,Seconds=40f,Chains=5,Wolves=9,Total=200,MissTotal=50,Breaches=1});
   Check(hourRow.Contains(",hour,1,") && hourRow.Contains(",40,5,50,0.25,1,") && Split(hourRow).Count==cols,"an hour row: seconds, chains, miss total, miss share 0.25, breaches");
   var night=BalanceCsv.NightRow(ctx,new BalanceNightSummary{Result="Lost",Reason="OutOfStones",Seconds=95.5f,Score=900,Banked=600,Throws=14,Stolen=2,HoursReached=1,AverageWall=4.25f});
   Check(night.EndsWith(",Lost,OutOfStones,900,600,14,2,1,0,4.25") && Split(night).Count==cols,"the night row ends with its summary");
   Check(BalanceCsv.HeaderMatches(BalanceCsv.Header+"\r") && !BalanceCsv.HeaderMatches("when,run") && !BalanceCsv.HeaderMatches(null),
     "an old file with other columns is recognised (then moved aside)");
   Check(BalanceCsv.Escape("say \"hi\"")=="\"say \"\"hi\"\"\"" && BalanceCsv.Escape("plain")=="plain","quotes are doubled inside a quoted cell"); }
 // --- The save's origin (the scenario label) ---
 { var p=new PlayerProfile{Origin="scenario Scenario_Night3"};
   Check(ProfileJson.Read(ProfileJson.Write(p),out var back,out _)==ProfileReadResult.Ok && back.Origin=="scenario Scenario_Night3","the origin round-trips through the save");
   Check(!ProfileJson.Write(new PlayerProfile()).Contains("\"debug\""),"a normal-flow save has no debug section");
   Check(ProfileJson.Read("{\"version\":1,\"debug\":{\"origin\":5}}",out _,out _)==ProfileReadResult.Corrupt,"an origin that isn't text: corrupt"); }
}
// Splits a CSV line into cells (quotes respected).
static List<string> Split(string line){
 var cells=new List<string>(); var cur=new System.Text.StringBuilder(); bool q=false;
 for(int i=0;i<line.Length;i++){ char ch=line[i];
   if(q){ if(ch=='"'){ if(i+1<line.Length&&line[i+1]=='"'){cur.Append('"');i++;} else q=false; } else cur.Append(ch); }
   else if(ch=='"') q=true; else if(ch==','){cells.Add(cur.ToString());cur.Clear();} else cur.Append(ch); }
 cells.Add(cur.ToString()); return cells; }
}
