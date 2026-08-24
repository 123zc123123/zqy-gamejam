"use strict";

const knobs = {
  tMin: 0,
  tMax: 0.55,
  dMin: 1.2,
  vRate: 23,
  theta: 45,
  m: 1,
  g: 18,
  mu: 0.5,
  rStand: 0.1,
  rMax: 1,
  muCtrlScale: 1.3,
  muSlipScale: 0.65,
  growPer: 0.16,
};

const SETTLE_SPEED = 0.06;
const ELASTIC = 1;
const FIXED_DT = 1 / 60;
const MOVE_SUBSTEPS = 6;

function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }
function hypot(x, z) { return Math.hypot(x, z); }
function gravity() { return Math.max(0.01, knobs.g); }
function tanTheta() { return Math.tan(knobs.theta * Math.PI / 180); }
function vMax() { return Math.max(0, knobs.vRate) * Math.max(0, knobs.tMax); }
function frictionAccel(muEff) {
  const mu = muEff == null ? knobs.mu : muEff;
  return Math.max(1e-4, mu * gravity());
}
function jumpSpeedMin() {
  const t = tanTheta();
  const denom = 2 * t + 1 / (2 * Math.max(1e-4, knobs.mu));
  return Math.sqrt(Math.max(0, knobs.dMin) * gravity() / Math.max(1e-6, denom));
}
function jumpRange(dvx) {
  const g = gravity();
  const t = tanTheta();
  const mu = Math.max(1e-4, knobs.mu);
  const air = 2 * dvx * dvx * t / g;
  const ground = (dvx * dvx) / (2 * mu * g);
  const height = (dvx * dvx * t * t) / (2 * g);
  const ty = 2 * dvx * t / g;
  const tg = dvx / (mu * g);
  return { air, ground, total: air + ground, height, ty, tg, T: ty + tg };
}
function resistOf(b) {
  const vmax = vMax();
  const V0 = knobs.rStand * vmax;
  const K = knobs.rMax;
  const v = clamp(hypot(b.vx, b.vz), 0, vmax);
  const t = vmax < 1e-6 ? 0 : v / vmax;
  return V0 * b.m + t * (K * b.m * vmax - V0 * b.m);
}
function hitTierFor(me, other, nx, nz) {
  const vMeN = me.vx * nx + me.vz * nz;
  const vOtN = other.vx * nx + other.vz * nz;
  if (Math.abs(vMeN) + 1e-9 >= Math.abs(vOtN)) return "ctrl";
  const dP = other.m * (-vOtN) - me.m * vMeN;
  if (dP <= resistOf(me)) return "base";
  return "slip";
}
function muForTier(tier) {
  if (tier === "ctrl") return knobs.mu * knobs.muCtrlScale;
  if (tier === "slip") return knobs.mu * knobs.muSlipScale;
  return knobs.mu;
}

function applyGroundFriction(b, dt) {
  const spd = hypot(b.vx, b.vz);
  if (spd < SETTLE_SPEED) { b.vx = 0; b.vz = 0; return; }
  const a = frictionAccel(b.slideMu);
  const next = spd - a * dt;
  if (next <= SETTLE_SPEED) { b.vx = 0; b.vz = 0; return; }
  const k = next / spd;
  b.vx *= k;
  b.vz *= k;
}

function stepMotion(b, dt) {
  if (b.airborne) {
    b.vy -= gravity() * dt;
    b.y += b.vy * dt;
    b.x += b.vx * dt;
    b.z += b.vz * dt;
    if (b.y <= 0 && b.vy <= 0) {
      b.y = 0;
      b.vy = 0;
      b.airborne = false;
      applyGroundFriction(b, dt * 0.25);
    }
  } else {
    applyGroundFriction(b, dt);
    b.x += b.vx * dt;
    b.z += b.vz * dt;
    b.y = 0;
    b.vy = 0;
  }
}

function bouncePair(a, b, nx, nz) {
  const invA = 1 / a.m;
  const invB = 1 / b.m;
  const inv = invA + invB;
  const rvx = b.vx - a.vx;
  const rvz = b.vz - a.vz;
  const vn = rvx * nx + rvz * nz;
  if (vn >= -1e-4) return { j: 0, tierA: null, tierB: null };
  const tierA = hitTierFor(a, b, nx, nz);
  const tierB = hitTierFor(b, a, -nx, -nz);
  const jImp = -(1 + ELASTIC) * vn / inv;
  a.vx -= (jImp * invA) * nx;
  a.vz -= (jImp * invA) * nz;
  b.vx += (jImp * invB) * nx;
  b.vz += (jImp * invB) * nz;
  a.airborne = false; a.y = 0; a.vy = 0; a.slideMu = muForTier(tierA); a.hitTier = tierA;
  b.airborne = false; b.y = 0; b.vy = 0; b.slideMu = muForTier(tierB); b.hitTier = tierB;
  return { j: jImp, tierA, tierB };
}

function simulateJump(dvx) {
  const dvy = dvx * tanTheta();
  const b = {
    x: 0, z: 0, y: 0.02, vx: dvx, vz: 0, vy: dvy,
    airborne: true, slideMu: knobs.mu, m: 1,
  };
  const dt = FIXED_DT / MOVE_SUBSTEPS;
  let t = 0;
  let tLand = null;
  let xLand = null;
  let yMax = 0;
  while (t < 20) {
    yMax = Math.max(yMax, b.y);
    const wasAir = b.airborne;
    stepMotion(b, dt);
    t += dt;
    if (wasAir && !b.airborne && tLand == null) {
      tLand = t;
      xLand = b.x;
    }
    if (!b.airborne && hypot(b.vx, b.vz) < SETTLE_SPEED) break;
  }
  return { t, x: b.x, tLand, xLand, yMax };
}

let failed = 0;
function approx(name, got, expected, tol) {
  const ok = Math.abs(got - expected) <= tol;
  if (!ok) {
    failed += 1;
    console.error(`FAIL ${name}: got ${got}, expected ${expected} ± ${tol}`);
  } else {
    console.log(`ok   ${name}: ${got.toFixed(4)} ≈ ${expected}`);
  }
}
function eq(name, got, expected) {
  if (got !== expected) {
    failed += 1;
    console.error(`FAIL ${name}: got ${got}, expected ${expected}`);
  } else {
    console.log(`ok   ${name}: ${got}`);
  }
}

const vmax = vMax();
approx("vmax", vmax, 12.65, 1e-9);

const t = tanTheta();
approx("tanθ", t, 1, 1e-6);

const vmin = jumpSpeedMin();
approx("vmin from dMin", vmin, Math.sqrt(1.2 * 18 / 3), 1e-6);

const full = jumpRange(vmax);
approx("ty", full.ty, 2 * 12.65 / 18, 1e-6);
approx("t地", full.tg, 12.65 / (0.5 * 18), 1e-6);
approx("k", full.ty / full.T, 0.5, 1e-6);
approx("D空", full.air, 2 * 12.65 * 12.65 / 18, 0.05);
approx("D地", full.ground, 12.65 * 12.65 / (2 * 0.5 * 18), 0.05);
approx("D", full.total, 26.7, 0.15);
approx("H", full.height, 4.45, 0.05);
approx("T", full.T, 2.81, 0.02);

const sim = simulateJump(vmax);
approx("sim D", sim.x, full.total, 0.35);
approx("sim T", sim.t, full.T, 0.08);
approx("sim D空", sim.xLand, full.air, 0.35);
approx("sim ty", sim.tLand, full.ty, 0.06);
approx("sim H", sim.yMax, full.height, 0.15);

const short = jumpRange(vmin);
approx("short D", short.total, 1.2, 0.02);
const simShort = simulateJump(vmin);
approx("sim short D", simShort.x, 1.2, 0.12);

const A = { m: 1, vx: vmax, vz: 0, y: 1, airborne: true, vy: 1 };
const B = { m: 1, vx: 0, vz: 0, y: 0, airborne: false, vy: 0 };
eq("standing defender slip", hitTierFor(B, A, -1, 0), "slip");
eq("attacker ctrl vs standing", hitTierFor(A, B, 1, 0), "ctrl");
eq("fat does not wall a faster A", hitTierFor({ m: 1, vx: vmax, vz: 0 }, { m: 8, vx: 0, vz: 0 }, 1, 0), "ctrl");

const half = { m: 1, vx: vmax * 0.5, vz: 0 };
const fullB = { m: 1, vx: -vmax, vz: 0 };
eq("half speed vs full head-on → default", hitTierFor(half, fullB, 1, 0), "base");

const light = { m: 1, vx: 11, vz: 0 };
const heavy = { m: 2, vx: -12, vz: 0 };
eq("light slightly slower vs 2x mass → slip", hitTierFor(light, heavy, 1, 0), "slip");

const equalA = { m: 1, vx: vmax, vz: 0 };
const equalB = { m: 1, vx: -vmax, vz: 0 };
eq("equal head-on A ctrl", hitTierFor(equalA, equalB, 1, 0), "ctrl");
eq("equal head-on B ctrl", hitTierFor(equalB, equalA, -1, 0), "ctrl");

const standA = { m: 1, vx: vmax, vz: 0, airborne: true, y: 2, vy: 3, slideMu: knobs.mu };
const standB = { m: 1, vx: 0, vz: 0, airborne: false, y: 0, vy: 0, slideMu: knobs.mu };
const hit = bouncePair(standA, standB, 1, 0);
eq("post-hit A almost stop", Math.abs(standA.vx) < 1e-6, true);
approx("post-hit B takes vmax", standB.vx, vmax, 1e-6);
eq("post-hit both grounded", standA.airborne === false && standB.airborne === false && standA.y === 0, true);
eq("tiers after standing hit", `${hit.tierA}/${hit.tierB}`, "ctrl/slip");
approx("slip μ", standB.slideMu, 0.5 * 0.65, 1e-9);
approx("ctrl μ", standA.slideMu, 0.5 * 1.3, 1e-9);
const slipD = (vmax * vmax) / (2 * standB.slideMu * knobs.g);
approx("standing slip distance", slipD, 13.7, 0.1);

const slowA = { m: 1, vx: 0.5, vz: 0 };
const slowB = { m: 1, vx: 0, vz: 0 };
eq("slow poke vs standing still slower → still ctrl for faster", hitTierFor(slowA, slowB, 1, 0), "ctrl");
const pokeDef = hitTierFor(slowB, slowA, -1, 0);
eq("standing vs slow poke: not ctrl", pokeDef === "base" || pokeDef === "slip", true);

const grazeA = { m: 1, vx: vmax, vz: 0 };
const grazeB = { m: 1, vx: 0, vz: 0 };
eq("side graze: A still faster on tiny normal", hitTierFor(grazeA, grazeB, 0, 1) === "ctrl" || hitTierFor(grazeA, grazeB, 0.02, 0.9996) === "ctrl", true);

const R0 = resistOf({ m: 1, vx: 0, vz: 0 });
approx("R stand", R0, 0.1 * vmax, 1e-6);
const Rmax = resistOf({ m: 1, vx: vmax, vz: 0 });
approx("R full", Rmax, 1 * vmax, 1e-6);
const Rhalf = resistOf({ m: 1, vx: vmax * 0.5, vz: 0 });
approx("R half linear", Rhalf, 0.1 * vmax + 0.5 * (vmax - 0.1 * vmax), 1e-6);

const grow = 1 + 6 * 0.16;
approx("full grow scale", grow, 1.96, 1e-9);

function kOf(mu, thetaDeg) {
  const tt = Math.tan(thetaDeg * Math.PI / 180);
  return (2 * mu * tt) / (2 * mu * tt + 1);
}
approx("k default", kOf(0.5, 45), 0.5, 1e-9);
approx("k large μ → near 1", kOf(1.8, 45), 2 * 1.8 / (2 * 1.8 + 1), 1e-9);
approx("k small μ → near 0", kOf(0.08, 45), 2 * 0.08 / (2 * 0.08 + 1), 1e-9);

function separatePair(a, b) {
  let dx = b.x - a.x;
  let dz = b.z - a.z;
  let dist = hypot(dx, dz);
  const minD = a.r + b.r;
  if (dist >= minD) return false;
  if (dist < 1e-6) { dx = 1; dz = 0; dist = 1; }
  const nx = dx / dist;
  const nz = dz / dist;
  const overlap = minD - dist;
  const invA = 1 / a.m;
  const invB = 1 / b.m;
  const inv = invA + invB;
  a.x -= nx * overlap * (invA / inv);
  a.z -= nz * overlap * (invA / inv);
  b.x += nx * overlap * (invB / inv);
  b.z += nz * overlap * (invB / inv);
  return { nx, nz };
}

const lightBug = { x: 0, z: 0, r: 0.9, m: 1 };
const heavyBug = { x: 1.4, z: 0, r: 0.9, m: 3 };
const sep = separatePair(lightBug, heavyBug);
approx("mass split: light moves more", 0 - lightBug.x, (heavyBug.x - 1.4) * 3, 1e-6);
eq("separate n", sep && Math.abs(sep.nx - 1) < 1e-9, true);

function collideOnce(a, b) {
  const minD = a.r + b.r;
  let dx = b.x - a.x;
  let dz = b.z - a.z;
  let dist = hypot(dx, dz);
  let nx; let nz;
  if (dist < minD) {
    const hit = separatePair(a, b);
    if (!hit) return 0;
    nx = hit.nx; nz = hit.nz;
  } else {
    const pax = a.px - b.px;
    const paz = a.pz - b.pz;
    const vx = (a.x - a.px) - (b.x - b.px);
    const vz = (a.z - a.pz) - (b.z - b.pz);
    const aa = vx * vx + vz * vz;
    if (aa < 1e-10) return 0;
    const bb = 2 * (pax * vx + paz * vz);
    const cc = pax * pax + paz * paz - minD * minD;
    const disc = bb * bb - 4 * aa * cc;
    if (disc < 0) return 0;
    const tHit = (-bb - Math.sqrt(disc)) / (2 * aa);
    if (tHit < 0 || tHit > 1) return 0;
    a.x = a.px + (a.x - a.px) * tHit;
    a.z = a.pz + (a.z - a.pz) * tHit;
    b.x = b.px + (b.x - b.px) * tHit;
    b.z = b.pz + (b.z - b.pz) * tHit;
    dx = b.x - a.x; dz = b.z - a.z;
    dist = hypot(dx, dz) || 1;
    nx = dx / dist; nz = dz / dist;
    separatePair(a, b);
  }
  return bouncePair(a, b, nx, nz);
}

const runner = {
  x: -3, z: 0, px: -3, pz: 0, y: 1.2, vy: 4, vx: vmax, vz: 0,
  r: 0.9, m: 1, airborne: true, slideMu: knobs.mu, hitTier: null,
};
const idle = {
  x: 0, z: 0, px: 0, pz: 0, y: 0, vy: 0, vx: 0, vz: 0,
  r: 0.9, m: 1, airborne: false, slideMu: knobs.mu, hitTier: null,
};
const dt = FIXED_DT / MOVE_SUBSTEPS;
let hitResult = null;
for (let i = 0; i < 400; i++) {
  runner.px = runner.x; runner.pz = runner.z;
  idle.px = idle.x; idle.pz = idle.z;
  stepMotion(runner, dt);
  stepMotion(idle, dt);
  hitResult = collideOnce(runner, idle);
  if (hitResult && hitResult.j) break;
}
eq("moving air-vs-stand did collide", !!(hitResult && hitResult.j), true);
eq("air hit snaps attacker down", runner.airborne === false && runner.y === 0, true);
eq("air hit: attacker ctrl / idle slip", `${hitResult.tierA}/${hitResult.tierB}`, "ctrl/slip");
approx("idle received near vmax", idle.vx, vmax, 0.3);

const settled = !runner.airborne && hypot(runner.vx, runner.vz) < SETTLE_SPEED;
eq("controller can settle immediately after equal-mass stand hit", settled, true);

const fs = require("fs");
const path = require("path");
const html = fs.readFileSync(path.join(__dirname, "index.html"), "utf8");
const js = fs.readFileSync(path.join(__dirname, "game.js"), "utf8");
const knobIds = [...js.matchAll(/\["(\w+)", "k-(\w+)", "v-(\w+)"/g)];
eq("knob map parsed", knobIds.length >= 13, true);
for (const [, key, kid, vid] of knobIds) {
  eq(`html has k-${kid}`, html.includes(`id="k-${kid}"`), true);
  eq(`html has v-${vid}`, html.includes(`id="v-${vid}"`), true);
  eq(`defaults has ${key}`, js.includes(`${key}:`), true);
}
eq("html dropped jumpT", html.includes("k-jumpT"), false);
eq("html dropped hitT", html.includes("k-hitT"), false);
eq("settings key v2", js.includes("dou-ququ-knobs-v2"), true);
eq("drawArc preview uses chargeDeltaV", /function drawArc\(b\) \{[\s\S]{0,500}chargeDeltaV\(b\)/.test(js), true);
eq("charge waits until settled", /function stepCharge\(b, dt\) \{[\s\S]{0,250}!isSettled\(b\)/.test(js), true);
eq("own-slide does not plant to charge", !/function plant\(b\)/.test(js) && !/function canStartCharge\(b\)/.test(js), true);

if (failed) {
  console.error(`\n${failed} failed`);
  process.exit(1);
}
console.log("\nall checks passed");
