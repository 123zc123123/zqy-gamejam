"use strict";

const R = require("./match-rules.js");

let failed = 0;
function eq(name, got, expected) {
  if (got !== expected) {
    failed += 1;
    console.error(`FAIL ${name}: got ${JSON.stringify(got)}, expected ${JSON.stringify(expected)}`);
  } else {
    console.log(`ok   ${name}`);
  }
}
function approx(name, got, expected, tol) {
  if (Math.abs(got - expected) > tol) {
    failed += 1;
    console.error(`FAIL ${name}: got ${got}, expected ${expected} ± ${tol}`);
  } else {
    console.log(`ok   ${name}: ${got}`);
  }
}

const knobs = R.mergeKnobs();

{
  const vmax = R.panelVMax(knobs);
  approx("panel v_max", vmax, 12.65, 1e-9);
  const bug = { chargeT: knobs.tMax, rageCharge: false, buffChargeT: 0 };
  const off = R.effectiveCharge(knobs, bug);
  approx("idle tMax", off.tMax, 0.55, 1e-9);
  approx("idle vRate*tMax", off.vRate * off.tMax, 12.65, 1e-9);
  bug.buffChargeT = 5;
  const on = R.effectiveCharge(knobs, bug);
  approx("buff tMax", on.tMax, 0.44, 1e-9);
  approx("buff v_max still 12.65", on.vRate * on.tMax, 12.65, 1e-9);
  bug.chargeT = on.tMax;
  approx("buff full Δv", R.chargeDeltaV(knobs, bug), 12.65, 1e-9);
}

{
  const bug = { grow: 0, rageSize: false, buffSizeT: 0, m: 1 };
  R.refreshBody(knobs, bug);
  approx("base r", bug.r, 1.8, 1e-9);
  approx("base m", bug.m, 1, 1e-9);
  R.applyItem(knobs, bug, "size");
  approx("size r", bug.r, 1.8 * 1.3, 1e-9);
  approx("size m", bug.m, 1.3, 1e-9);
  eq("size timer", bug.buffSizeT, 6);
  R.tickBuffs(knobs, bug, 6.01);
  eq("size expired", bug.buffSizeT, 0);
  approx("r back to layer", bug.r, 1.8, 1e-9);
}

{
  const bug = { grow: 6, rageSize: false, buffSizeT: 0 };
  R.refreshBody(knobs, bug);
  const g = 1 + 6 * 0.16;
  approx("grown r", bug.r, 1.8 * g, 1e-6);
  R.applyItem(knobs, bug, "size");
  approx("grown+size r", bug.r, 1.8 * g * 1.3, 1e-6);
  R.tickBuffs(knobs, bug, 6.01);
  approx("expire keeps grow layer", bug.r, 1.8 * g, 1e-6);
  eq("grow unchanged", bug.grow, 6);
}

{
  const bug = { x: 30, z: 0, r: 0.9, vx: 4, vz: 0, buffShieldT: 8 };
  const sdf = (x) => Math.hypot(x, 0) - 10;
  const grad = () => ({ x: 1, z: 0 });
  const saved = R.tryShieldSave(knobs, bug, (x, z) => sdf(x, z), grad);
  eq("shield saves", saved, true);
  eq("shield consumed", bug.buffShieldT, 0);
  eq("vx unchanged", bug.vx, 4);
  eq("inside after save", sdf(bug.x, bug.z) <= -(bug.r + 0.08) + 0.05, true);
  const again = R.tryShieldSave(knobs, bug, (x, z) => Math.hypot(x, z) - 10, grad);
  eq("no second save", again, false);
}

{
  const seq = [];
  const rolls = [0, 0, 0, 0, 0, 0, 0, 0];
  let i = 0;
  const rand = () => rolls[i++] || 0;
  const state = R.createMatchState(knobs);
  state.t = 19.9;
  eq("no item before 20", R.dueItemSpawns(state, rand).length, 0);
  state.t = 20;
  const a = R.dueItemSpawns(state, rand);
  eq("first wave count", a.length, 3);
  seq.push(a[0]);
  state.t = 42;
  const b = R.dueItemSpawns(state, rand);
  eq("second wave count", b.length, 3);
  seq.push(b[0]);
  eq("second batch added not replaced", a.length + b.length, 6);
  const mid = [];
  for (const t of [60, 74, 85]) {
    state.t = t;
    const wave = R.dueItemSpawns(state, rand);
    eq("mid wave " + t, wave.length, 3);
    mid.push(wave[0]);
    eq("mid not forced shield " + t, wave[0] === "size" || wave[0] === "shield" || wave[0] === "charge", true);
  }
  const late = [];
  for (const t of [94, 102, 110]) {
    state.t = t;
    const wave = R.dueItemSpawns(state, rand);
    eq("ot wave " + t, wave.length, 3);
    late.push.apply(late, wave);
  }
  eq("ot only shield", late.every((k) => k === "shield"), true);
  eq("next index done", state.nextItemIndex, 8);
  state.t = 200;
  eq("no replace extra", R.dueItemSpawns(state, rand).length, 0);
}

{
  const state = R.createMatchState(knobs);
  state.t = 10;
  eq("probe no heart", R.shouldRefillHeart(state), false);
  state.t = 20;
  eq("open burst", R.heartBurstFill(state), true);
  eq("open refill", R.shouldRefillHeart(state), true);
  R.markHeartFilled(state);
  eq("burst once marked no gap skip if still last<0? marked", state.lastHeartAt, 20);
  eq("after mark need gap", R.shouldRefillHeart(state), false);
  state.t = 27;
  eq("after 7s refill", R.shouldRefillHeart(state), true);
  state.t = 90;
  state.lastHeartAt = 88;
  eq("rage 2s not enough", R.shouldRefillHeart(state), false);
  state.t = 93;
  eq("rage 5s enough", R.shouldRefillHeart(state), true);
  eq("full field still adds batch", R.shouldRefillHeart(state), true);
  eq("heart wave size", R.heartWaveSize(state), 6);
}

{
  const state = R.createMatchState(knobs);
  const bugs = [
    { alive: true, grow: 2, rageSize: false, buffSizeT: 3, buffChargeT: 2, r: 0.9, m: 1 },
    { alive: true, grow: 0, rageSize: false, buffSizeT: 0, buffChargeT: 0, r: 0.9, m: 1 },
  ];
  R.refreshBody(knobs, bugs[0]);
  state.t = 90;
  eq("enter rage", R.enterRage(state, bugs), true);
  eq("rage flag", state.rage, true);
  eq("both rage size", bugs.every((b) => b.rageSize && b.rageCharge), true);
  approx("no stack size", bugs[0].r, 1.8 * (1 + 2 * 0.16) * 1.3, 1e-6);
  eq("personal size timer cleared", bugs[0].buffSizeT, 0);
  eq("second enter no-op", R.enterRage(state, bugs), false);
}

{
  const a = { alive: true, x: 1, z: 0, grow: 1 };
  const b = { alive: true, x: 3, z: 0, grow: 6 };
  const w = R.centerWinner([a, b]);
  eq("nearer wins", w.winner, a);
  const c = { alive: true, x: 1, z: 0, grow: 4 };
  const w2 = R.centerWinner([a, c]);
  eq("same dist more grow", w2.winner, c);
  const w3 = R.centerWinner([
    { alive: true, x: 2, z: 0, grow: 1 },
    { alive: true, x: -2, z: 0, grow: 1 },
  ]);
  eq("true tie", w3.tie, true);
}

{
  const clock = R.matchClock(R.createMatchState({}));
  eq("start probe", clock.phase, "probe");
  const s = R.createMatchState({});
  s.t = 90;
  eq("rage phase", R.matchClock(s).phase, "rage");
  s.t = 120;
  eq("over phase", R.matchClock(s).phase, "over");
}

if (failed) {
  console.error(`\n${failed} failed`);
  process.exit(1);
}
console.log("\nall item rules passed");
