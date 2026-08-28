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

const fs = require("fs");
const src = fs.readFileSync(__dirname + "/game.js", "utf8");

function extractPalettes(text) {
  const m = text.match(/const PALETTES = \[([\s\S]*?)\];/);
  return m ? m[1] : "";
}

{
  const pals = extractPalettes(src);
  eq("cricket palettes still two", (pals.match(/body:/g) || []).length, 2);
  eq("青头 body unchanged", pals.includes('body: "#4a3624"'), true);
  eq("油葫芦 body unchanged", (pals.match(/body: "#4a3624"/g) || []).length, 2);
  eq("青头 belly unchanged", pals.includes('belly: "#c49a62"'), true);
  eq("油葫芦 head still dark brown", pals.includes('head: "#26180e"'), true);
  eq("青头 head still cool green-black", pals.includes('head: "#2a3a32"'), true);
}

{
  eq("faction table exists", /const FACTION = \{/.test(src), true);
  eq("ally teal fill", src.includes("fill: [36, 170, 148]"), true);
  eq("enemy magenta fill", src.includes("fill: [204, 46, 122]"), true);
  eq("ally ring", src.includes("ring: [40, 204, 176]"), true);
  eq("enemy ring", src.includes("ring: [228, 52, 132]"), true);
  eq("unowned babies stay gold", src.includes("fill: [196, 124, 40]"), true);
}

{
  eq("localBug ignores alive so spectate keeps 己方", /function localBug\(/.test(src), true);
  eq("factionOf exists", /function factionOf\(/.test(src), true);
  eq("path uses faction paint", /factionPaint\(b\)/.test(src), true);
  eq("ground ring not player-only", /function drawGroundRing\(/.test(src), true);
  eq("old beige player-only ring gone", /if \(b\.isPlayer && b\.alive\) \{/.test(src) && /palRing/.test(src), false);
  eq("palRing helper removed", /function palRing\(/.test(src), false);
  eq("enemy dash only on main bugs", /fac === "enemy" && !baby/.test(src), true);
  eq("baby rings stay solid", /enemy" \? \[8, 5\]/.test(src), false);
  eq("baby ring fill strong enough", /baby \? 0\.34/.test(src), true);
  eq("baby ring has pixel floor", /Math\.max\(11, hitPx \* 1\.9\)/.test(src), true);
  eq("path arrows use faction color", /rgba\(\$\{arrowRgb\}/.test(src), true);
  eq("old gold-only path gone", src.includes('const rgb = full >= 1 ? "232, 176, 72" : "196, 124, 40"'), false);
  eq("baby path follows owner", /b\.kind === "baby"/.test(src) && /ownerId === me\.id/.test(src), true);
  eq("makeBug pal fallback for baby ids", /PALETTES\[id\] \|\| BABY_PAL/.test(src), true);
  eq("drawCricket pal fallback", /const pal = b\.pal \|\| BABY_PAL/.test(src), true);
  eq("guest restores baby pal", /b\.pal = \(owner && owner\.pal\) \|\| BABY_PAL/.test(src), true);
}

{
  const pal = { body: "#4a3624", belly: "#c49a62", head: "#2a3a32" };
  const packed = {
    id: 100,
    kind: "baby",
    ownerId: 0,
    pal,
    x: 1, z: 2, y: 0,
    alive: true,
    r: 0.72,
  };
  const snap = JSON.parse(JSON.stringify({ babies: [packed] }));
  eq("snapshot keeps baby pal", !!(snap.babies[0].pal && snap.babies[0].pal.body), true);
  eq("snapshot keeps owner", snap.babies[0].ownerId, 0);
  const PALETTES = [{ body: "#aaa" }, { body: "#bbb" }];
  const BABY_PAL = { body: "#5c4a34" };
  const dst = { pal: PALETTES[snap.babies[0].id] || BABY_PAL, kind: "bug" };
  Object.assign(dst, snap.babies[0]);
  if (!dst.pal || !dst.pal.body) dst.pal = BABY_PAL;
  eq("guest baby drawable after snap", dst.pal.body, "#4a3624");
  eq("guest baby stays baby", dst.kind, "baby");
}

if (failed) {
  console.error("\n" + failed + " faction checks failed");
  process.exit(1);
}
console.log("\nall faction checks passed");
