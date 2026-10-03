using System;
using System.IO;
using Piglings.Events; using Piglings.Meta; using Piglings.Rules; using Piglings.Runtime; using Piglings.Simulation;

// Weapon mastery v1 + the save (M9.1): counting, banking, the level rule, the save format and the save file.
partial class P{
static int Hits(NightState st,string weapon){ st.WeaponHits.TryGetValue(weapon,out int n); return n; }
static int Count(System.Collections.Generic.SortedDictionary<string,int> d,string id){ d.TryGetValue(id,out int n); return n; }
static GameId Spawned(Night n,string type){ var r=n.Ids.Next(); n.Bus.Publish(new RobotSpawned(r,type)); return r; }

static void MasteryChecks(){
 // --- Counting (MasteryTally) ---
 { var n=new Night(Hours(100000)); var t=new MasteryTally(n.Bus,n.St);
   n.Throw(3);
   Check(Hits(n.St,"stone")==3,"one throw knocks 3 robots directly: 3 weapon hits");
   // stone -> a1 (type a) ; a1's ball -> b1 (type b) ; b1's ball -> a2 (type a)
   var c=new ChainId(n.Ids.Next()); var s=n.Ids.Next(); var a1=Spawned(n,"a"); var b1=Spawned(n,"b"); var a2=Spawned(n,"a");
   n.Bus.Publish(new ThrowReleased(c,s,"stone"));
   n.Bus.Publish(new RobotLostGrip(a1,c,Attribution.FromThrowable(s)));
   n.Bus.Publish(new RobotLostGrip(b1,c,Attribution.FromRobotBall(a1,0)));
   n.Bus.Publish(new RobotLostGrip(a2,c,Attribution.FromRobotBall(b1,1)));
   Check(Hits(n.St,"stone")==4,"ball knocks never count for the weapon (3 + 1 direct = 4)");
   Check(Count(n.St.BallKnocks,"a")==1 && Count(n.St.BallKnocks,"b")==1,"ball knocks per knocker type: a 1 (knocked b1), b 1 (knocked a2)");
   Check(Count(n.St.KnockedByBall,"b")==1 && Count(n.St.KnockedByBall,"a")==1,"knocked-by-ball per knocked type: b 1, a 1 (a1 was hit by the stone, not a ball)");
   // A second weapon counts on its own
   var c2=new ChainId(n.Ids.Next()); var s2=n.Ids.Next(); n.Bus.Publish(new ThrowReleased(c2,s2,"brick"));
   n.Bus.Publish(new RobotLostGrip(n.Ids.Next(),c2,Attribution.FromThrowable(s2)));
   Check(Hits(n.St,"brick")==1 && Hits(n.St,"stone")==4,"each weapon id counts separately");
   // Things we can't name aren't counted: a weapon id we never heard of, a null id, a robot with no RobotSpawned
   n.Bus.Publish(new RobotLostGrip(n.Ids.Next(),c2,Attribution.FromThrowable(n.Ids.Next())));
   var c3=new ChainId(n.Ids.Next()); var s3=n.Ids.Next(); n.Bus.Publish(new ThrowReleased(c3,s3,null));
   n.Bus.Publish(new RobotLostGrip(n.Ids.Next(),c3,Attribution.FromThrowable(s3)));
   Check(n.St.WeaponHits.Count==2 && Hits(n.St,"stone")==4 && Hits(n.St,"brick")==1,"unknown thrower / null weapon id: not counted");
   t.AddWeaponHits("stone",10);
   Check(Hits(n.St,"stone")==14,"Add 10 hits (debug) goes into tonight's tally");
   t.Dispose(); }
 // --- The sweep never counts; nothing counts once the night is banked ---
 { var n=new Night(Hours(10)); var t=new MasteryTally(n.Bus,n.St);
   n.Wall.Add(Spawned(n,"a")); n.Wall.Add(Spawned(n,"a"));
   n.Play(1);   // 10 points = dawn; the night ends (and sweeps the wall) once it settles
   Check(n.St.Ended && Hits(n.St,"stone")==1 && n.St.KnockedByBall.Count==0,"the end-of-night sweep (RobotSwept) isn't a hit or a knock");
   var c=new ChainId(n.Ids.Next()); var s=n.Ids.Next(); n.Bus.Publish(new ThrowReleased(c,s,"stone"));
   n.Bus.Publish(new RobotLostGrip(n.Ids.Next(),c,Attribution.FromThrowable(s)));
   t.AddWeaponHits("stone",10);
   Check(Hits(n.St,"stone")==1,"after the night ended (already banked): no more counting, Add 10 hits ignored");
   t.Dispose(); }

 // --- Banking (NightReferee → NightBanked → Progression) on both outcomes ---
 { // Dawn: one throw, two direct hits (10 + 20 = 30 ≥ 30 = dawn) and one ball knock
   var n=new Night(Hours(30)); var t=new MasteryTally(n.Bus,n.St); var prog=new Progression(n.Bus,null);
   int hitsAtEnd=-1; n.Bus.Subscribe<NightEnded>(e=>hitsAtEnd=prog.Profile.DirectHits("stone"));
   var c=new ChainId(n.Ids.Next()); var s=n.Ids.Next(); var r1=Spawned(n,"wolf"); var r2=Spawned(n,"wolf"); var r3=Spawned(n,"wolf");
   n.Bus.Publish(new ThrowReleased(c,s,"stone"));
   n.Bus.Publish(new RobotLostGrip(r1,c,Attribution.FromThrowable(s)));
   n.Bus.Publish(new RobotLostGrip(r2,c,Attribution.FromThrowable(s)));
   n.Bus.Publish(new RobotLostGrip(r3,c,Attribution.FromRobotBall(r1,0)));
   n.Settle((c,s,new[]{r1,r2,r3}));
   Check(n.Ends.Count==1 && n.Ends[0].Result==NightResult.Won,"dawn night ends");
   var w=n.Banked.Find(b=>b.Destination==MasteryDestination.Weapon);
   Check(w.Id=="stone" && w.Stat==MasteryStat.DirectHits && w.Amount==2 && w.Multiplier==1,"dawn banks NightBanked(Weapon, \"stone\", DirectHits, 2)");
   Check(n.Banked.Exists(b=>b.Destination==MasteryDestination.Lineage && b.Id=="wolf" && b.Stat==MasteryStat.BallKnocks && b.Amount==1)
      && n.Banked.Exists(b=>b.Destination==MasteryDestination.Lineage && b.Id=="wolf" && b.Stat==MasteryStat.KnockedByBall && b.Amount==1),
      "dawn banks both lineage stats (BallKnocks 1, KnockedByBall 1) under the robot type");
   Check(n.Banked.Exists(b=>b.Destination==MasteryDestination.Barn && b.Id==null && b.Stat==MasteryStat.Score),"the barn still banks the score (Id null)");
   Check(hitsAtEnd==2,"by NightEnded the profile already holds the night (2 hits) — the save point");
   var rec=prog.Profile.Robots["wolf"];
   Check(rec.BallKnocks==1 && rec.KnockedByBall==1,"the profile keeps both lineage stats");
   t.Dispose(); prog.Dispose(); }
 { // Caught: one stone, it knocks two, then a breach on the empty pile. Score 30 < 1000: banks no score, but the hits.
   var n=new Night(new NightGoal(new[]{1000},1,0)); var t=new MasteryTally(n.Bus,n.St); var prog=new Progression(n.Bus,null);
   n.Play(2); n.Breach();
   Check(n.Ends.Count==1 && n.Ends[0].Reason==NightEndReason.Caught,"caught night ends");
   Check(n.BankedTo(MasteryDestination.Barn)==0 && n.BankedTo(MasteryDestination.Weapon)==2,"caught: no score banked, but the 2 weapon hits are (mastery from use)");
   Check(prog.Profile.DirectHits("stone")==2,"caught: the profile gets the hits");
   t.Dispose(); prog.Dispose(); }
 { // A night with no hits banks nothing to any weapon
   var n=new Night(new NightGoal(new[]{1000},1,0)); var t=new MasteryTally(n.Bus,n.St);
   n.Play(0); n.Breach();
   Check(n.Ends.Count==1 && !n.Banked.Exists(b=>b.Destination==MasteryDestination.Weapon),"0 hits: no Weapon NightBanked");
   t.Dispose(); }

 // --- Progression on its own ---
 { var bus=new EventBus(); var prog=new Progression(bus,null);
   bus.Publish(new NightBanked(MasteryDestination.Weapon,"stone",MasteryStat.DirectHits,12,1));
   bus.Publish(new NightBanked(MasteryDestination.Weapon,"stone",MasteryStat.DirectHits,5,2));
   bus.Publish(new NightBanked(MasteryDestination.Barn,null,MasteryStat.Score,900,1));
   bus.Publish(new NightBanked(MasteryDestination.Weapon,"",MasteryStat.DirectHits,7,1));
   Check(prog.Profile.DirectHits("stone")==22 && prog.Profile.Weapons.Count==1 && prog.Profile.Robots.Count==0,
         "hits add up across nights (12 + 5x2 = 22); the barn score and an empty id aren't kept");
   bus.Publish(new NightBanked(MasteryDestination.Weapon,"stone",MasteryStat.DirectHits,int.MaxValue,1));
   Check(prog.Profile.DirectHits("stone")==int.MaxValue,"hits saturate at int.MaxValue, never wrap negative");
   prog.Reset();
   Check(prog.Profile.DirectHits("stone")==0 && prog.Profile.Weapons.Count==0,"Reset progress empties the profile");
   prog.Dispose();
   bus.Publish(new NightBanked(MasteryDestination.Weapon,"stone",MasteryStat.DirectHits,3,1));
   Check(prog.Profile.DirectHits("stone")==0,"after Dispose it no longer listens"); }

 // --- The level rule: derived from total hits, never stored ---
 { var one=new[]{50}; var two=new[]{50,150};
   Check(MasteryLevels.LevelFor(0,one)==1 && MasteryLevels.LevelFor(49,one)==1 && MasteryLevels.LevelFor(50,one)==2,"[50]: 0 → 1, 49 → 1, 50 → 2");
   Check(MasteryLevels.LevelFor(149,two)==2 && MasteryLevels.LevelFor(150,two)==3 && MasteryLevels.LevelFor(99999,two)==3,"[50, 150] are cumulative: 149 → 2, 150 → 3, past the top stays 3");
   Check(MasteryLevels.LevelFor(500,new int[0])==1 && MasteryLevels.LevelFor(500,null)==1,"no thresholds: always level 1");
   Check(MasteryLevels.LevelFor(120,new[]{50})==2 && MasteryLevels.LevelFor(120,new[]{50,100})==3,"retuning thresholds re-derives the level from the same saved hits (120: [50] → 2, [50,100] → 3)");
   Check(MasteryLevels.NextThreshold(0,one)==50 && MasteryLevels.NextThreshold(60,two)==150 && MasteryLevels.NextThreshold(50,one)==-1,"next threshold: 50, 150, -1 at the top");
   Check(MasteryLevels.Problem(two)==null && MasteryLevels.Problem(new[]{0})!=null && MasteryLevels.Problem(new[]{50,50})!=null && MasteryLevels.Problem(new[]{50,40})!=null,
         "Problem(): fine for [50,150]; flags a 0, a repeat, a drop"); }
}

static void SaveChecks(){
 // --- The format (ProfileJson) ---
 var p=new PlayerProfile();
 p.Weapon("stone").DirectHits=137; p.Robot("wolfbot_basic").BallKnocks=412; p.Robot("wolfbot_basic").KnockedByBall=300;
 p.Weapon("odd \"id\" ñ \\ /").DirectHits=int.MaxValue;
 var text=ProfileJson.Write(p);
 { var r=ProfileJson.Read(text,out var back,out var why);
   Check(r==ProfileReadResult.Ok && back.DirectHits("stone")==137 && back.Robots["wolfbot_basic"].BallKnocks==412
         && back.Robots["wolfbot_basic"].KnockedByBall==300 && back.DirectHits("odd \"id\" ñ \\ /")==int.MaxValue,"round trip: every count and id comes back (quotes, unicode, int.MaxValue)");
   Check(ProfileJson.Write(back)==text,"round trip is stable: writing it again gives the same text"); }
 Check(text.Contains("\"version\": 1"),"the file carries \"version\": 1");
 void Expect(string json,ProfileReadResult want,string what){
  var r=ProfileJson.Read(json,out var prof,out var why);
  Check(r==want && (r==ProfileReadResult.Ok)==(prof!=null) && (r==ProfileReadResult.Ok || why!=null),$"read {what} → {want}"+(r!=want?$" (got {r}: {why})":""));
 }
 Expect("{\"version\":1}",ProfileReadResult.Ok,"version only (nothing banked yet)");
 Expect("{\"version\":1,\"weapons\":{\"stone\":{}},\"robots\":null,\"feats\":[1,2],\"x\":\"y\"}",ProfileReadResult.Ok,"missing counts (0) and unknown fields (ignored)");
 Expect("{}",ProfileReadResult.UnknownVersion,"no version");
 Expect("{\"version\":2}",ProfileReadResult.UnknownVersion,"a newer version");
 Expect("{\"version\":0}",ProfileReadResult.UnknownVersion,"version 0");
 Expect("{\"version\":\"1\"}",ProfileReadResult.Corrupt,"a version that's a string");
 Expect("{\"version\":1.0}",ProfileReadResult.Corrupt,"a version that's a float");
 Expect("",ProfileReadResult.Corrupt,"an empty file");
 Expect("   \n",ProfileReadResult.Corrupt,"whitespace only");
 Expect("not json",ProfileReadResult.Corrupt,"not JSON");
 Expect("[1,2]",ProfileReadResult.Corrupt,"an array");
 Expect(text.Substring(0,text.Length/2),ProfileReadResult.Corrupt,"a half-written file");
 Expect("{\"version\":1} trailing",ProfileReadResult.Corrupt,"text after the object");
 Expect("{\"version\":1,\"weapons\":{\"stone\":{\"directHits\":-1}}}",ProfileReadResult.Corrupt,"a negative count");
 Expect("{\"version\":1,\"weapons\":{\"stone\":{\"directHits\":3.5}}}",ProfileReadResult.Corrupt,"a fractional count");
 Expect("{\"version\":1,\"weapons\":{\"stone\":{\"directHits\":\"3\"}}}",ProfileReadResult.Corrupt,"a count that's a string");
 Expect("{\"version\":1,\"weapons\":{\"stone\":{\"directHits\":2147483648}}}",ProfileReadResult.Corrupt,"a count past int.MaxValue");
 Expect("{\"version\":1,\"weapons\":{\"stone\":{\"directHits\":100000000000000000000000}}}",ProfileReadResult.Corrupt,"a count past long");
 Expect("{\"version\":1,\"weapons\":{\"stone\":5}}",ProfileReadResult.Corrupt,"an entry that isn't an object");
 Expect("{\"version\":1,\"weapons\":[]}",ProfileReadResult.Corrupt,"a section that isn't an object");
 Expect("{\"version\":1,\"weapons\":{\"\":{\"directHits\":1}}}",ProfileReadResult.Corrupt,"an empty id");
 Expect("{\"version\":1,\"weapons\":{\"stone\":{\"directHits\":1},\"stone\":{\"directHits\":2}}}",ProfileReadResult.Corrupt,"the same id twice");
 Expect("{\"version\":1,\"version\":1}",ProfileReadResult.Corrupt,"the same field twice");

 // --- The file (ProfileFile), on a real temp folder ---
 var dir=Path.Combine(Path.GetTempPath(),"piglings-corecheck-"+Guid.NewGuid().ToString("N"));
 try{
  int Corrupts()=>Directory.GetFiles(dir,"piglings_profile.corrupt-*").Length;
  var f=new ProfileFile(dir);
  var l=f.Load();
  Check(l.Source==ProfileSource.New && l.Profile.Weapons.Count==0 && l.SaveError==null && File.Exists(f.MainPath),"first launch: a new profile, written to disk right away");
  l.Profile.Weapon("stone").DirectHits=5;
  Check(f.Save(l.Profile)==null && !File.Exists(f.TempPath),"save: no error, no .tmp left behind");
  l.Profile.Weapon("stone").DirectHits=9; f.Save(l.Profile);
  { var again=new ProfileFile(dir).Load();
    Check(again.Source==ProfileSource.Main && again.Profile.DirectHits("stone")==9 && again.Problems.Count==0,"reload (app restart / Play Again): 9 hits from the save"); }
  { ProfileJson.Read(File.ReadAllText(f.PrevPath),out var prev,out _);
    Check(prev!=null && prev.DirectHits("stone")==5,".prev holds the save before the last one (5)"); }
  // A leftover .tmp (a crash mid-save) is ignored, and the next save just overwrites it
  File.WriteAllText(f.TempPath,"{ half");
  { var again=new ProfileFile(dir).Load();
    Check(again.Source==ProfileSource.Main && again.Profile.DirectHits("stone")==9 && Corrupts()==0,"a leftover .tmp is ignored"); }
  Check(f.Save(l.Profile)==null && !File.Exists(f.TempPath),"...and the next save overwrites it");
  // Corrupt save, good .prev → .prev loads (one night lost, not everything); the bad file is kept aside
  File.WriteAllText(f.MainPath,"garbage{");
  { var again=new ProfileFile(dir).Load();
    Check(again.Source==ProfileSource.Previous && again.Profile.DirectHits("stone")==9 && Corrupts()==1 && again.BackedUp.Count==1
          && File.ReadAllText(again.BackedUp[0])=="garbage{","corrupt save → backed up (byte for byte) and .prev loaded");
    var re=new ProfileFile(dir).Load();
    Check(re.Source==ProfileSource.Main && re.Profile.DirectHits("stone")==9 && Corrupts()==1,"...and the recovered profile was written back: the next launch loads it normally"); }
  // Both bad → fresh, both backed up; two backups in the same second get distinct names
  File.WriteAllText(f.MainPath,"bad main"); File.WriteAllText(f.PrevPath,"bad prev");
  { var again=new ProfileFile(dir).Load();
    Check(again.Source==ProfileSource.Fresh && again.Profile.Weapons.Count==0 && again.BackedUp.Count==2 && Corrupts()==3 && again.Problems.Count==2
          && !File.Exists(f.PrevPath),"save and .prev both bad → fresh profile, both backed up first, the bad .prev removed"); }
  File.WriteAllText(f.MainPath,"bad again");
  { new ProfileFile(dir).Load(); File.WriteAllText(f.MainPath,"bad again"); new ProfileFile(dir).Load();
    Check(Corrupts()==5,"repeated bad saves never overwrite an earlier backup (unique names)"); }
  // A save from a newer build: not read, not destroyed
  File.WriteAllText(f.MainPath,"{\"version\":7,\"weapons\":{\"stone\":{\"directHits\":400}}}"); File.Delete(f.PrevPath);
  { var again=new ProfileFile(dir).Load();
    Check(again.Source==ProfileSource.Fresh && again.BackedUp.Count==1 && File.ReadAllText(again.BackedUp[0]).Contains("400"),
          "unknown version (newer build) → backed up, fresh profile"); }
  // A save that can't be written reports an error instead of throwing
  var blocker=Path.Combine(dir,"not-a-folder"); File.WriteAllText(blocker,"x");
  Check(new ProfileFile(Path.Combine(blocker,"sub")).Save(p)!=null,"a save that can't be written returns the error, never throws");

  // --- End to end: two nights, one won, one caught; Play Again = a new session loading from disk ---
  Directory.Delete(dir,true);
  for(int night=0;night<2;night++){
   var file=new ProfileFile(dir); var load=file.Load();
   var n=new Night(night==0?Hours(10):new NightGoal(new[]{1000},1,0)); var t=new MasteryTally(n.Bus,n.St); var prog=new Progression(n.Bus,load.Profile);
   n.Bus.Subscribe<NightEnded>(e=>file.Save(prog.Profile));   // what NightSession does
   if(night==0) n.Play(3); else { n.Play(2); n.Breach(); }
   t.Dispose(); prog.Dispose();
  }
  Check(new ProfileFile(dir).Load().Profile.DirectHits("stone")==5,"end to end: won night (3 hits) + caught night (2) = 5 hits on disk");
 } finally { try{ Directory.Delete(dir,true); } catch(IOException){} }
}
}
