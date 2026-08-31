"use strict";

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

const DEAD = 0.12;
function magFromCenter(dxPx, dyPx, radius) {
  const d = Math.hypot(dxPx, dyPx) / radius;
  return Math.min(1, d);
}
function v3CommitT(mag, tMax, tMin) {
  if (mag < DEAD) return null;
  const t = mag * tMax;
  if (t < tMin - 1e-4) return null;
  return t;
}
function v3WindupDone(chargeT, goal) {
  return goal != null && chargeT >= goal - 1e-4;
}

{
  approx("at center mag 0", magFromCenter(0, 0, 110), 0, 1e-9);
  approx("half radius", magFromCenter(0, -55, 110), 0.5, 1e-9);
  approx("beyond rim clamps", magFromCenter(0, -400, 110), 1, 1e-9);
}

{
  const tMax = 0.8;
  eq("deadzone no commit", v3CommitT(0.05, tMax, 0), null);
  approx("half pull = half tMax", v3CommitT(0.5, tMax, 0), 0.4, 1e-9);
  eq("below tMin cancel", v3CommitT(0.2, tMax, 0.3), null);
  eq("release does not jump immediately", v3WindupDone(0, 0.4), false);
  eq("after t seconds jump", v3WindupDone(0.4, 0.4), true);
}

{
  const fs = require("fs");
  const src = fs.readFileSync(__dirname + "/game.js", "utf8");
  const css = fs.readFileSync(__dirname + "/style.css", "utf8");
  eq("v3 locks a time goal", /v3Goal/.test(src), true);
  eq("v3 measures from disc center not press", /stick\.pressX/.test(src), false);
  eq("v3 disc is enlarged", /STICK_FIXED\s*=\s*220/.test(src) && /#stick\.fixed/.test(css), true);
}

if (failed) {
  console.error("\n" + failed + " input checks failed");
  process.exit(1);
}
console.log("\nall input checks passed");
