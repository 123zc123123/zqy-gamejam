(() => {
  "use strict";

  const canvas = document.getElementById("view");
  const ctx = canvas.getContext("2d");
  const hudLive = document.getElementById("hud-live");
  const banner = document.getElementById("banner");
  const overlayStart = document.getElementById("start");
  const overlayCount = document.getElementById("countdown");
  const overlayResult = document.getElementById("result");
  const overlaySpectate = document.getElementById("spectate");
  const countNum = document.getElementById("count-num");
  const resultTitle = document.getElementById("result-title");
  const resultSub = document.getElementById("result-sub");
  const stickEl = document.getElementById("stick");
  const stickKnob = document.getElementById("stick-knob");
  const STICK_SIZE = 128;
  const STICK_VISUAL = 38;

  const PITCH = 16 * Math.PI / 180;
  const COS_P = Math.cos(PITCH);
  const SIN_P = Math.sin(PITCH);
  const ARENA_HW = 21.2;
  const ARENA_HD = 31.8;
  const ARENA_CORNER = 7.2;
  let CAM_SCALE = 7.7;
  let camCX = 195;
  let camCY = 355;
  const BUG_R = 0.9;
  const MOVE_SUBSTEPS = 6;
  const HEART_R = 0.3;
  const HEART_COUNT = 18;
  const GROW_MAX = 6;
  const HEART_RESPAWN = 5.5;
  const SETTLE_SPEED = 0.06;
  const FIXED_DT = 1 / 60;
  const ELASTIC = 1;
  let W = 390;
  let H = 844;

  const DEFAULTS = {
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

  const knobs = { ...DEFAULTS };
  const SETTINGS_KEY = "dou-ququ-knobs-v2";
  const NAMES = ["青头", "油葫芦"];
  const PALETTES = [
    {
      body: "#4a3624",
      belly: "#c49a62",
      head: "#2a3a32",
      outline: null,
      leg: "#1a120c",
      wing: "#a88850",
      eye: "#100806",
      stripe: "#2c342c",
      gleam: "#e8d4a8",
      mark: "#8a2a22",
      ring: "rgba(198,186,140,0.82)",
      headSheen: "rgba(132,164,148,0.38)",
    },
    {
      body: "#4a3624",
      belly: "#c49a62",
      head: "#26180e",
      outline: null,
      leg: "#1a120c",
      wing: "#a88850",
      eye: "#100806",
      stripe: "#322010",
      gleam: "#e8d4a8",
    },
  ];

  let phase = "boot";
  let solo = false;
  let paused = false;
  let userPaused = false;
  let awaitingQuit = false;
  let spectating = false;
  const tuneEl = document.getElementById("tune");
  const pauseTag = document.getElementById("pause-tag");
  const phoneEl = document.getElementById("phone");
  let slowmo = false;
  let showVel = false;
  let acc = 0;
  let lastTs = 0;
  let time = 0;
  let countT = 0;
  let hitstop = 0;
  let camPunch = { x: 0, y: 0 };
  let bannerT = 0;
  let sandTex = null;
  let jarTex = null;
  let lipCracks = [];
  let rng = 1;

  const keys = new Set();
  const stick = { x: 0, y: 0, active: false, pointerId: null, originX: 0, originY: 0 };
  const particles = [];
  const rings = [];
  const bugs = [];
  const hearts = [];

  function rand() {
    rng = (rng * 16807) % 2147483647;
    return (rng - 1) / 2147483646;
  }
  function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }
  function hypot(x, z) { return Math.hypot(x, z); }
  function arenaSDF(x, z) {
    const r = ARENA_CORNER;
    const qx = Math.abs(x) - (ARENA_HW - r);
    const qz = Math.abs(z) - (ARENA_HD - r);
    return Math.hypot(Math.max(qx, 0), Math.max(qz, 0)) + Math.min(Math.max(qx, qz), 0) - r;
  }
  function inArena(x, z) { return arenaSDF(x, z) <= 0; }
  function arenaGradient(x, z) {
    const e = 0.08;
    return norm(arenaSDF(x + e, z) - arenaSDF(x - e, z), arenaSDF(x, z + e) - arenaSDF(x, z - e));
  }
  function pathRoundRect(g, x, y, w, h, r) {
    const rr = Math.min(r, w * 0.5, h * 0.5);
    g.moveTo(x + rr, y);
    g.arcTo(x + w, y, x + w, y + h, rr);
    g.arcTo(x + w, y + h, x, y + h, rr);
    g.arcTo(x, y + h, x, y, rr);
    g.arcTo(x, y, x + w, y, rr);
    g.closePath();
  }
  function norm(x, z) {
    const d = hypot(x, z);
    if (d < 1e-6) return { x: 0, z: 0, d: 0 };
    return { x: x / d, z: z / d, d };
  }

  function worldToScreen(x, z, y) {
    const scale = CAM_SCALE;
    const cx = camCX + camPunch.x;
    const cy = camCY + camPunch.y;
    return {
      x: cx + x * scale,
      y: cy + (-z * COS_P - y * SIN_P) * scale,
      s: scale,
    };
  }

  function layoutCamera() {
    const padT = 64;
    const padB = 56;
    const padX = 4;
    const availW = Math.max(80, W - padX * 2);
    const availH = Math.max(80, H - padT - padB);
    CAM_SCALE = Math.min(availW / (2 * ARENA_HW), availH / (2 * ARENA_HD * COS_P));
    camCX = W * 0.5;
    camCY = padT + availH * 0.5;
  }

  function bakeSand() {
    const c = document.createElement("canvas");
    c.width = 768;
    c.height = 768;
    const g = c.getContext("2d");
    const grd = g.createRadialGradient(384, 330, 24, 384, 384, 430);
    grd.addColorStop(0, "#d6ceb6");
    grd.addColorStop(0.38, "#c2b99c");
    grd.addColorStop(0.72, "#9e977c");
    grd.addColorStop(1, "#6f6c58");
    g.fillStyle = grd;
    g.fillRect(0, 0, 768, 768);
    for (let i = 0; i < 4600; i++) {
      const x = Math.random() * 768;
      const y = Math.random() * 768;
      const a = 0.03 + Math.random() * 0.09;
      g.fillStyle = Math.random() > 0.5 ? `rgba(78,76,58,${a})` : `rgba(236,230,210,${a})`;
      g.fillRect(x, y, 1 + Math.random() * 2, 1 + Math.random() * 2);
    }
    for (let i = 0; i < 28; i++) {
      g.fillStyle = `rgba(90, 86, 68, ${0.04 + Math.random() * 0.06})`;
      g.beginPath();
      g.ellipse(Math.random() * 768, Math.random() * 768, 8 + Math.random() * 22, 4 + Math.random() * 10, Math.random() * Math.PI, 0, Math.PI * 2);
      g.fill();
    }
    sandTex = c;

    const j = document.createElement("canvas");
    j.width = 768;
    j.height = 768;
    const jg = j.getContext("2d");
    const jgGrad = jg.createRadialGradient(384, 200, 30, 384, 430, 540);
    jgGrad.addColorStop(0, "#3e453c");
    jgGrad.addColorStop(0.4, "#2a302a");
    jgGrad.addColorStop(1, "#121612");
    jg.fillStyle = jgGrad;
    jg.fillRect(0, 0, 768, 768);
    jg.strokeStyle = "rgba(186, 196, 168, 0.07)";
    jg.lineWidth = 1;
    for (let i = 0; i < 110; i++) {
      jg.beginPath();
      let x0 = Math.random() * 768;
      let y0 = Math.random() * 768;
      jg.moveTo(x0, y0);
      const segs = 2 + (Math.random() * 4 | 0);
      for (let k = 0; k < segs; k++) {
        x0 += (Math.random() - 0.5) * 64;
        y0 += 8 + Math.random() * 46;
        jg.lineTo(x0, y0);
      }
      jg.stroke();
    }
    for (let i = 0; i < 18; i++) {
      jg.strokeStyle = `rgba(210, 196, 150, ${0.025 + Math.random() * 0.04})`;
      jg.beginPath();
      const y = 40 + i * 42 + Math.random() * 10;
      jg.moveTo(0, y);
      jg.quadraticCurveTo(384, y + (Math.random() - 0.5) * 16, 768, y);
      jg.stroke();
    }
    jarTex = j;
    lipCracks = [];
    for (let i = 0; i < 56; i++) {
      lipCracks.push({
        u: Math.random(),
        v: Math.random(),
        dx: (Math.random() - 0.5) * 34,
        dy: 6 + Math.random() * 26,
      });
    }
  }

  function makeBug(id, x, z, isPlayer, personality) {
    return {
      id,
      name: NAMES[id],
      pal: PALETTES[id],
      isPlayer,
      ai: personality,
      x, z, px: x, pz: z, y: 0, vy: 0,
      vx: 0, vz: 0,
      dirX: -x, dirZ: -z,
      r: BUG_R,
      m: knobs.m,
      grow: 0,
      lastHitId: -1,
      slideMu: knobs.mu,
      hitTier: null,
      fxHit: 0,
      fxSquash: 0,
      hitNx: 1,
      hitNz: 0,
      roll: 0,
      trail: [],
      chargeEmit: 0,
      slideEmit: 0,
      tumble: 0,
      charging: false,
      chargeT: 0,
      holding: false,
      pendingCharge: false,
      inX: 0, inZ: 0,
      airborne: false,
      alive: true,
      outT: 0,
      spin: 0,
      squatFlash: 0,
      tremble: 0,
    };
  }

  function refreshBody(b) {
    const g = 1 + b.grow * Math.max(0, knobs.growPer);
    b.r = BUG_R * g;
    b.m = Math.max(0.08, knobs.m) * g;
  }

  function heartBlocked(x, z, skip) {
    if (arenaSDF(x, z) > -1.4) return true;
    for (const h of hearts) {
      if (h === skip || !h.alive) continue;
      if (hypot(h.x - x, h.z - z) < 1.25) return true;
    }
    for (const b of bugs) {
      if (hypot(b.x - x, b.z - z) < 1.3) return true;
    }
    return false;
  }

  function placeHeart(skip) {
    for (let n = 0; n < 50; n++) {
      const x = (rand() * 2 - 1) * ARENA_HW;
      const z = (rand() * 2 - 1) * ARENA_HD;
      if (!heartBlocked(x, z, skip)) return { x, z };
    }
    return { x: 0, z: 0 };
  }

  function spawnHearts() {
    hearts.length = 0;
    for (let i = 0; i < HEART_COUNT; i++) {
      const p = placeHeart(null);
      hearts.push({
        x: p.x,
        z: p.z,
        r: HEART_R,
        alive: true,
        respawn: 0,
        phase: rand() * 6,
      });
    }
  }

  function spawnMatch() {
    particles.length = 0;
    rings.length = 0;
    bugs.length = 0;
    hearts.length = 0;
    const pos = [
      { x: 0, z: -ARENA_HD * 0.38 },
      { x: 0, z: ARENA_HD * 0.38 },
    ];
    const brains = [
      null,
      { chargeMul: 1.2, react: 0.28, lead: 0.22, name: "贪蓄" },
    ];
    const n = solo ? 1 : 2;
    for (let i = 0; i < n; i++) {
      const b = makeBug(i, pos[i].x, pos[i].z, i === 0, brains[i]);
      const d = norm(-pos[i].x, -pos[i].z);
      b.dirX = d.x || 0;
      b.dirZ = d.z || 1;
      if (b.ai) {
        b.ai.timer = 0.25 + Math.random() * 0.9;
        b.ai.releaseAt = 0.6;
        b.ai.target = null;
      }
      bugs.push(b);
      refreshBody(b);
    }
    spawnHearts();
    time = 0;
    hitstop = 0;
    camPunch.x = 0;
    camPunch.y = 0;
    liveHud();
  }

  function liveCount() { return bugs.filter((b) => b.alive).length; }
  function liveHud() {
    hudLive.textContent = solo ? "独自练习" : (liveCount() >= 2 ? "对战中" : "残局");
  }
  function flash(msg) { banner.textContent = msg; bannerT = 1.6; }

  function gravity() {
    return Math.max(0.01, knobs.g);
  }

  function tanTheta() {
    return Math.tan(knobs.theta * Math.PI / 180);
  }

  function vMax() {
    return Math.max(0, knobs.vRate) * Math.max(0, knobs.tMax);
  }

  function isSettled(b) {
    return !b.airborne && b.y <= 0 && hypot(b.vx, b.vz) < SETTLE_SPEED;
  }

  function isMovingOnGround(b) {
    return b.alive && !b.airborne && hypot(b.vx, b.vz) >= SETTLE_SPEED;
  }

  function frictionAccel(muEff) {
    const mu = muEff == null ? knobs.mu : muEff;
    return Math.max(1e-4, mu * gravity());
  }

  function jumpSpeedMin() {
    const t = tanTheta();
    const denom = 2 * t + 1 / (2 * Math.max(1e-4, knobs.mu));
    return Math.sqrt(Math.max(0, knobs.dMin) * gravity() / Math.max(1e-6, denom));
  }

  function chargeDeltaV(b) {
    return Math.max(0, knobs.vRate) * clamp(b.chargeT, 0, knobs.tMax);
  }

  function jumpDeltaV(b) {
    return Math.max(jumpSpeedMin(), chargeDeltaV(b));
  }

  function jumpRange(dvx) {
    const g = gravity();
    const t = tanTheta();
    const mu = Math.max(1e-4, knobs.mu);
    const air = 2 * dvx * dvx * t / g;
    const ground = (dvx * dvx) / (2 * mu * g);
    const height = (dvx * dvx * t * t) / (2 * g);
    const ty = 2 * dvx * t / g;
    return { air, ground, total: air + ground, height, ty };
  }

  function jumpDist(b) {
    return jumpRange(jumpDeltaV(b)).total;
  }

  function applyGroundFriction(b, dt) {
    const spd = hypot(b.vx, b.vz);
    if (spd < SETTLE_SPEED) {
      b.vx = 0;
      b.vz = 0;
      b.tumble = 0;
      b.hitTier = null;
      return;
    }
    const a = frictionAccel(b.slideMu);
    const next = spd - a * dt;
    if (next <= SETTLE_SPEED) {
      b.vx = 0;
      b.vz = 0;
      b.tumble = 0;
      b.hitTier = null;
      return;
    }
    const k = next / spd;
    b.vx *= k;
    b.vz *= k;
  }

  function stickToGround(b) {
    b.y = 0;
    b.vy = 0;
    b.airborne = false;
  }

  function faceVelocity(b) {
    const n = norm(b.vx, b.vz);
    if (n.d < 0.12) return;
    b.dirX = n.x;
    b.dirZ = n.z;
  }

  function beginCharge(b) {
    b.charging = true;
    b.chargeT = 0;
    b.pendingCharge = false;
  }

  function interrupt(b) {
    if (!b.charging) return;
    b.pendingCharge = b.holding;
    b.charging = false;
    b.chargeT = 0;
    b.squatFlash = 1;
  }

  function doJump(b) {
    if (!b.alive) return;
    const d = norm(b.dirX, b.dirZ);
    if (d.d < 1e-5) return;
    const dvx = jumpDeltaV(b);
    const dvy = dvx * tanTheta();
    b.dirX = d.x;
    b.dirZ = d.z;
    b.vx = d.x * dvx;
    b.vz = d.z * dvx;
    b.vy = dvy;
    b.y = 0.02;
    b.airborne = true;
    b.slideMu = knobs.mu;
    b.hitTier = null;
    b.charging = false;
    b.chargeT = 0;
    b.pendingCharge = false;
    b.fxSquash = 0.35;
    b.tumble = 0;
    b.roll = 0;
    spawnDust(b.x, b.z, 14, 0.85);
    spawnDust(b.x, b.z, 6, 0.55, "spark");
  }

  function tryRelease(b) {
    if (!b.charging) return;
    if (b.chargeT < knobs.tMin - 1e-4) {
      b.charging = false;
      b.chargeT = 0;
      return;
    }
    doJump(b);
  }

  function hitSpraySpec(tier, burst) {
    if (!tier) {
      return burst
        ? { n: 20, spread: 0.55, power: 0.8, life: 0.36, spark: 0.1, size: 0.9, ring: 0.8, origin: 0.14, tint: "#c4b89a", sparkTint: "#e8d8a8", ringTint: "rgba(196, 184, 154, 0.8)" }
        : { n: 2, spread: 0.45, power: 0.28, life: 0.28, spark: 0.05, size: 0.85, origin: 0.08, tint: "#c4b89a", sparkTint: "#e8d8a8" };
    }
    if (tier === "ctrl") {
      return burst
        ? { n: 32, spread: 0.1, power: 0.55, life: 0.26, spark: 0.12, size: 0.55, ring: 0.45, origin: 0.06, tint: "#b8c4c8", sparkTint: "#eef4f8", ringTint: "rgba(198, 210, 216, 0.9)" }
        : { n: 2, spread: 0.08, power: 0.22, life: 0.2, spark: 0.04, size: 0.5, origin: 0.04, tint: "#b8c4c8", sparkTint: "#eef4f8" };
    }
    if (tier === "slip") {
      return burst
        ? { n: 78, spread: 1, power: 2.7, life: 0.9, spark: 0.5, size: 2.6, ring: 1.85, origin: 0.55, tint: "#c44a28", sparkTint: "#ff9a48", ringTint: "rgba(210, 96, 48, 0.9)" }
        : { n: 5, spread: 1, power: 0.95, life: 0.55, spark: 0.28, size: 2.1, origin: 0.28, tint: "#c44a28", sparkTint: "#ff9a48" };
    }
    return burst
      ? { n: 46, spread: 0.62, power: 1.35, life: 0.5, spark: 0.28, size: 1.25, ring: 1, origin: 0.2, tint: "#c4a66a", sparkTint: "#ffe08a", ringTint: "rgba(216, 176, 96, 0.88)" }
      : { n: 3, spread: 0.58, power: 0.42, life: 0.34, spark: 0.12, size: 1.15, origin: 0.12, tint: "#c4a66a", sparkTint: "#ffe08a" };
  }

  function spawnDirectedDust(x, z, dx, dz, n, power) {
    spawnHitSpray(x, z, dx, dz, { n, spread: 0.28, power, life: 0.38, spark: 0.08, size: 1, origin: 0.12, tint: "#c4b89a", sparkTint: "#ffe08a" });
  }

  function spawnHitSpray(x, z, dx, dz, spec) {
    const d = norm(dx, dz);
    const bx = d.d < 1e-5 ? 1 : d.x;
    const bz = d.d < 1e-5 ? 0 : d.z;
    const n = spec.n | 0;
    const origin = spec.origin == null ? 0.18 : spec.origin;
    for (let i = 0; i < n; i++) {
      const spark = Math.random() < spec.spark;
      const randA = Math.random() * Math.PI * 2;
      const k = clamp(spec.spread, 0, 1);
      let px = bx * (1 - k) + Math.cos(randA) * k;
      let pz = bz * (1 - k) + Math.sin(randA) * k;
      const dn = hypot(px, pz) || 1;
      px /= dn;
      pz /= dn;
      const s = (0.3 + Math.random()) * spec.power * (spark ? 1.55 : 1);
      const life = spec.life * (0.55 + Math.random() * 0.55);
      particles.push({
        kind: spark ? "spark" : "sand",
        x: x + (Math.random() - 0.5) * origin,
        z: z + (Math.random() - 0.5) * origin,
        y: spark ? 0.1 : 0.02,
        vx: px * s,
        vz: pz * s,
        vy: (spark ? 2.1 : 0.9) + Math.random() * (spark ? 2.6 : 1.8),
        life,
        max: Math.max(life, spec.life),
        r: spec.size * (spark ? 0.7 : 1) * (0.7 + Math.random() * 0.5),
        tint: spark ? spec.sparkTint : spec.tint,
      });
    }
  }

  function spawnDust(x, z, n, power, kind) {
    const type = kind || "sand";
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2;
      const s = (0.35 + Math.random()) * power;
      particles.push({
        kind: type,
        x, z, y: type === "spark" ? 0.12 : 0.02,
        vx: Math.cos(a) * s * (type === "spark" ? 1.6 : 1),
        vz: Math.sin(a) * s * (type === "spark" ? 1.6 : 1),
        vy: (type === "spark" ? 2.4 : 1.1) + Math.random() * 2.4,
        life: type === "spark" ? 0.22 + Math.random() * 0.18 : 0.35 + Math.random() * 0.35,
        max: type === "spark" ? 0.4 : 0.7,
        r: type === "spark" ? 0.03 + Math.random() * 0.03 : 0.04 + Math.random() * 0.05,
      });
    }
  }

  function spawnRing(x, z, power, scale, tint) {
    const s = scale == null ? 1 : scale;
    rings.push({
      x, z,
      style: "dust",
      tint: tint || "rgba(216, 181, 106, 0.88)",
      life: 0.3 * s,
      max: 0.3 * s,
      r0: 0.1,
      r1: 0.7 * s + Math.min(1.8, power * 0.12),
    });
  }

  function playerInput() {
    let sx = stick.x;
    let sy = stick.y;
    if (keys.has("KeyW") || keys.has("ArrowUp")) sy -= 1;
    if (keys.has("KeyS") || keys.has("ArrowDown")) sy += 1;
    if (keys.has("KeyA") || keys.has("ArrowLeft")) sx -= 1;
    if (keys.has("KeyD") || keys.has("ArrowRight")) sx += 1;
    const n = hypot(sx, sy);
    const held = stick.active || stick.pointerId != null
      || keys.has("KeyW") || keys.has("KeyS") || keys.has("KeyA") || keys.has("KeyD")
      || keys.has("ArrowUp") || keys.has("ArrowDown") || keys.has("ArrowLeft") || keys.has("ArrowRight");
    if (n >= 0.12) return { holding: true, x: sx / n, z: -sy / n };
    if (held) return { holding: true, x: 0, z: 0 };
    return { holding: false, x: 0, z: 0 };
  }

  function knockoutAim(me, t) {
    const lead = me.ai.lead;
    const px = t.x + t.vx * lead;
    const pz = t.z + t.vz * lead;
    const outward = arenaGradient(px, pz);
    const hx = px + outward.x * 0.4;
    const hz = pz + outward.z * 0.4;
    return { x: hx - me.x, z: hz - me.z };
  }

  function scoreTarget(me, t) {
    const d = hypot(t.x - me.x, t.z - me.z);
    let s = clamp(1.6 + arenaSDF(t.x, t.z) * 0.55, 0, 2.6);
    if (t.charging) s += 1.7;
    s += Math.max(0, 2.4 - d);
    if (t.isPlayer) s += 0.4;
    if (arenaSDF(me.x, me.z) > -2.4 && arenaSDF(t.x, t.z) < arenaSDF(me.x, me.z)) s -= 0.8;
    return s + Math.random() * 0.25;
  }

  function pickTarget(me) {
    let best = null;
    let bestS = -1e9;
    for (const t of bugs) {
      if (t === me || !t.alive) continue;
      const s = scoreTarget(me, t);
      if (s > bestS) { bestS = s; best = t; }
    }
    return best;
  }

  function updateAI(b, dt) {
    if (!b.ai || !b.alive || phase !== "play") {
      if (b.ai) b.holding = false;
      return;
    }
    b.ai.timer -= dt;
    const edge = arenaSDF(b.x, b.z);
    const out = arenaGradient(b.x, b.z);
    const vr = b.vx * out.x + b.vz * out.z;

    if (!b.holding && edge > -2.3 && vr > 0.3) {
      const inward = norm(-out.x, -out.z);
      b.inX = inward.x;
      b.inZ = inward.z;
      b.dirX = inward.x;
      b.dirZ = inward.z;
      b.holding = true;
      b.ai.releaseAt = knobs.tMin + 0.16 + Math.random() * 0.18;
      b.ai.target = null;
      b.ai.timer = 0;
      return;
    }

    if (!b.holding && b.ai.timer <= 0) {
      let heartAim = null;
      let heartD = 2.5;
      if (b.grow < GROW_MAX) {
        for (const h of hearts) {
          if (!h.alive) continue;
          const d = hypot(h.x - b.x, h.z - b.z);
          if (d < heartD) { heartD = d; heartAim = h; }
        }
      }
      if (heartAim && Math.random() < 0.42) {
        const d = norm(heartAim.x - b.x, heartAim.z - b.z);
        b.inX = d.x;
        b.inZ = d.z;
        b.dirX = d.x || b.dirX;
        b.dirZ = d.z || b.dirZ;
        b.holding = true;
        b.ai.target = null;
        b.ai.releaseAt = clamp(0.18 + heartD * 0.08, knobs.tMin + 0.04, knobs.tMax * 0.7);
        return;
      }
      const target = pickTarget(b);
      if (target) {
        const aim = knockoutAim(b, target);
        const d = norm(aim.x, aim.z);
        b.inX = d.x;
        b.inZ = d.z;
        b.dirX = d.x || b.dirX;
        b.dirZ = d.z || b.dirZ;
        b.holding = true;
        b.ai.target = target;
        const gap = hypot(target.x - b.x, target.z - b.z);
        let t = (0.32 + gap * 0.16) * b.ai.chargeMul;
        if (target.charging) t = Math.min(t, 0.55 + Math.random() * 0.15);
        if (Math.random() < 0.12) t = knobs.tMax * (0.85 + Math.random() * 0.12);
        b.ai.releaseAt = clamp(t, knobs.tMin + 0.04, knobs.tMax * 0.98);
      } else {
        b.ai.timer = 0.2 + Math.random() * 0.5;
      }
    }

    if (b.holding) {
      const t = b.ai.target;
      if (t && t.alive) {
        const aim = knockoutAim(b, t);
        const d = norm(aim.x, aim.z);
        b.inX = b.inX * 0.82 + d.x * 0.18;
        b.inZ = b.inZ * 0.82 + d.z * 0.18;
        const nn = norm(b.inX, b.inZ);
        b.inX = nn.x;
        b.inZ = nn.z;
        b.dirX = nn.x;
        b.dirZ = nn.z;
      }
      if (b.chargeT >= b.ai.releaseAt) {
        b.holding = false;
        b.ai.timer = b.ai.react + Math.random() * 0.35;
      }
    }
  }

  function stepCharge(b, dt) {
    if (!b.alive) return;

    if (b.holding) {
      if (!isSettled(b)) {
        if (b.charging) interrupt(b);
        else b.pendingCharge = true;
        return;
      }
      if (!b.charging) beginCharge(b);
      b.pendingCharge = false;
      b.chargeT = Math.min(knobs.tMax, b.chargeT + dt);
    } else if (b.charging) {
      b.pendingCharge = false;
      tryRelease(b);
    } else {
      b.pendingCharge = false;
    }
  }

  function stepMotion(b, dt) {
    if (!b.alive) {
      b.outT += dt;
      b.y -= dt * 2.4;
      return;
    }
    refreshBody(b);
    b.px = b.x;
    b.pz = b.z;
    b.squatFlash = Math.max(0, b.squatFlash - dt * 3.2);
    const hitFade = b.hitTier === "slip" ? 1.6 : b.hitTier === "ctrl" ? 8.5 : 3.2;
    b.fxHit = Math.max(0, b.fxHit - dt * hitFade);
    b.fxSquash = Math.max(0, b.fxSquash - dt * (b.hitTier === "ctrl" ? 8.5 : b.hitTier === "slip" ? 3.2 : 5.4));
    if (isSettled(b)) b.roll = Math.max(0, (b.roll || 0) - dt * 7);
    if (b.charging && isSettled(b)) {
      b.chargeEmit += dt;
      if (b.chargeEmit > 0.07) {
        b.chargeEmit = 0;
        spawnDust(b.x, b.z, 2, 0.22 + clamp(b.chargeT / knobs.tMax, 0, 1) * 0.25);
      }
    }
    if (isMovingOnGround(b)) {
      b.slideEmit += dt;
      const dusty = b.hitTier === "slip" ? 0.02 : b.hitTier === "ctrl" ? 0.048 : 0.034;
      if (b.slideEmit > dusty) {
        b.slideEmit = 0;
        spawnHitSpray(b.x, b.z, -b.dirX, -b.dirZ, hitSpraySpec(b.hitTier, false));
      }
    }
    for (let i = b.trail.length - 1; i >= 0; i--) {
      b.trail[i].life -= dt;
      if (b.trail[i].life <= 0) b.trail.splice(i, 1);
    }

    if (b.airborne) {
      b.vy -= gravity() * dt;
      b.y += b.vy * dt;
      b.x += b.vx * dt;
      b.z += b.vz * dt;
      if (b.y <= 0 && b.vy <= 0) {
        stickToGround(b);
        spawnDust(b.x, b.z, 4, 0.25);
        applyGroundFriction(b, dt * 0.25);
      }
    } else {
      applyGroundFriction(b, dt);
      b.x += b.vx * dt;
      b.z += b.vz * dt;
      b.y = 0;
      b.vy = 0;
    }

    b.spin *= Math.pow(0.12, dt);
    if (isSettled(b)) b.lastHitId = -1;
    markOut(b);
  }

  function addGrow(b, reason) {
    if (!b.alive) return;
    if (b.grow >= GROW_MAX) {
      flash(`${b.name} 已经很大了`);
      return;
    }
    b.grow += 1;
    refreshBody(b);
    flash(`${reason} · 体型 ${b.grow}`);
  }

  function pullBackSolo(b) {
    const x = 0;
    const z = -ARENA_HD * 0.38;
    b.x = x;
    b.z = z;
    b.px = x;
    b.pz = z;
    b.y = 0;
    b.vy = 0;
    b.vx = 0;
    b.vz = 0;
    b.airborne = false;
    b.charging = false;
    b.chargeT = 0;
    b.pendingCharge = b.holding;
    b.tumble = 0;
    b.hitTier = null;
    b.roll = 0;
    b.slideMu = knobs.mu;
    b.spin = 0;
    const d = norm(-x, -z);
    b.dirX = d.x || 0;
    b.dirZ = d.z || 1;
    spawnDust(x, z, 10, 0.7);
    flash("出圈 · 拉回");
  }

  function markOut(b) {
    if (!b.alive) return;
    if (inArena(b.x, b.z)) return;
    if (solo && b.isPlayer) {
      pullBackSolo(b);
      return;
    }
    const killer = bugs.find((k) => k.id === b.lastHitId && k.alive && k !== b);
    b.alive = false;
    b.charging = false;
    b.holding = false;
    b.pendingCharge = false;
    b.airborne = true;
    spawnDust(b.x, b.z, 16, 1.1);
    if (killer) addGrow(killer, `${killer.name} 击杀 ${b.name}`);
    else flash(`${b.name} 出圈`);
    liveHud();
    if (b.isPlayer && phase === "play" && liveCount() > 1) offerQuit();
  }

  function eatHeart(b, h) {
    addGrow(b, `${b.name} 吃到饲料`);
    h.alive = false;
    h.respawn = HEART_RESPAWN;
    spawnDust(h.x, h.z, 10, 0.55);
  }

  function stepHearts(dt) {
    for (const h of hearts) {
      h.phase += dt;
      if (!h.alive) {
        h.respawn -= dt;
        if (h.respawn <= 0) {
          const p = placeHeart(h);
          h.x = p.x;
          h.z = p.z;
          h.alive = true;
        }
        continue;
      }
      if (phase !== "play") continue;
      for (const b of bugs) {
        if (!b.alive) continue;
        if (hypot(b.x - h.x, b.z - h.z) < b.r + h.r) {
          eatHeart(b, h);
          break;
        }
      }
    }
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

  function tierLabel(tier) {
    if (tier === "ctrl") return "可控";
    if (tier === "slip") return "失衡";
    return "默认";
  }

  function applyHitSlide(b, tier, jImp) {
    stickToGround(b);
    b.hitTier = tier;
    b.slideMu = muForTier(tier);
    const sign = jImp > 0 ? 1 : -1;
    if (tier === "ctrl") {
      b.tumble = 0;
      b.spin = 0;
      b.fxHit = 1;
      b.fxSquash = 1.05;
      b.roll = 0;
    } else if (tier === "slip") {
      b.tumble = 1;
      b.spin += sign * 9.2;
      b.fxHit = 1;
      b.fxSquash = 1.35;
      b.roll = 1;
    } else {
      b.tumble = 0.7;
      b.spin += sign * 2.8;
      b.fxHit = 1;
      b.fxSquash = 1.25;
      b.roll = 0;
    }
  }

  function tierHitStop(tier, jImp) {
    const mag = Math.abs(jImp);
    if (tier === "ctrl") return 0.022 + Math.min(0.02, mag * 0.006);
    if (tier === "slip") return 0.1 + Math.min(0.07, mag * 0.018);
    return 0.04 + Math.min(0.04, mag * 0.012);
  }

  function playHitFx(b, nx, nz, jImp, tier) {
    const pwr = Math.min(2.6, Math.abs(jImp) * 0.24);
    const spec = hitSpraySpec(tier, true);
    spec.power += pwr * (tier === "slip" ? 0.35 : 0.16);
    spawnHitSpray(b.x, b.z, -nx, -nz, spec);
    spawnRing(b.x, b.z, pwr, spec.ring, spec.ringTint);
  }

  function bouncePair(a, b, nx, nz) {
    const invA = 1 / a.m;
    const invB = 1 / b.m;
    const inv = invA + invB;
    const rvx = b.vx - a.vx;
    const rvz = b.vz - a.vz;
    const vn = rvx * nx + rvz * nz;
    if (vn >= -1e-4) return 0;
    const tierA = hitTierFor(a, b, nx, nz);
    const tierB = hitTierFor(b, a, -nx, -nz);
    const jImp = -(1 + ELASTIC) * vn / inv;
    a.vx -= (jImp * invA) * nx;
    a.vz -= (jImp * invA) * nz;
    b.vx += (jImp * invB) * nx;
    b.vz += (jImp * invB) * nz;
    faceVelocity(a);
    faceVelocity(b);
    interrupt(a);
    interrupt(b);
    a.lastHitId = b.id;
    b.lastHitId = a.id;
    applyHitSlide(a, tierA, jImp);
    applyHitSlide(b, tierB, -jImp);
    a.hitNx = -nx;
    a.hitNz = -nz;
    b.hitNx = nx;
    b.hitNz = nz;
    playHitFx(a, nx, nz, jImp, tierA);
    playHitFx(b, -nx, -nz, jImp, tierB);
    flash(`${a.name} ${tierLabel(tierA)} · ${b.name} ${tierLabel(tierB)}`);
    return jImp;
  }

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

  function collide() {
    for (let i = 0; i < bugs.length; i++) {
      for (let j = i + 1; j < bugs.length; j++) {
        const a = bugs[i];
        const b = bugs[j];
        if (!a.alive || !b.alive) continue;
        const minD = a.r + b.r;
        let dx = b.x - a.x;
        let dz = b.z - a.z;
        let dist = hypot(dx, dz);
        let nx;
        let nz;
        if (dist < minD) {
          const hit = separatePair(a, b);
          if (!hit) continue;
          nx = hit.nx;
          nz = hit.nz;
        } else {
          const pax = a.px - b.px;
          const paz = a.pz - b.pz;
          const vx = (a.x - a.px) - (b.x - b.px);
          const vz = (a.z - a.pz) - (b.z - b.pz);
          const aa = vx * vx + vz * vz;
          if (aa < 1e-10) continue;
          const bb = 2 * (pax * vx + paz * vz);
          const cc = pax * pax + paz * paz - minD * minD;
          const disc = bb * bb - 4 * aa * cc;
          if (disc < 0) continue;
          const tHit = (-bb - Math.sqrt(disc)) / (2 * aa);
          if (tHit < 0 || tHit > 1) continue;
          a.x = a.px + (a.x - a.px) * tHit;
          a.z = a.pz + (a.z - a.pz) * tHit;
          b.x = b.px + (b.x - b.px) * tHit;
          b.z = b.pz + (b.z - b.pz) * tHit;
          dx = b.x - a.x;
          dz = b.z - a.z;
          dist = hypot(dx, dz) || 1;
          nx = dx / dist;
          nz = dz / dist;
          separatePair(a, b);
        }
        const jImp = bouncePair(a, b, nx, nz);
        if (jImp) {
          const punchMul = (a.hitTier === "slip" || b.hitTier === "slip") ? 3.3
            : (a.hitTier === "ctrl" && b.hitTier === "ctrl") ? 0.7 : 1.9;
          const punch = clamp(Math.abs(jImp) * punchMul, 0, 14);
          camPunch.x += nx * punch;
          camPunch.y += -nz * punch * 0.4;
          hitstop = Math.max(hitstop, Math.max(tierHitStop(a.hitTier, jImp), tierHitStop(b.hitTier, jImp)));
        }
      }
    }
  }

  function stepParticles(dt) {
    for (let i = particles.length - 1; i >= 0; i--) {
      const p = particles[i];
      p.life -= dt;
      p.vy -= (p.kind === "spark" ? 10 : 18) * dt;
      p.x += p.vx * dt;
      p.z += p.vz * dt;
      p.y = Math.max(0, p.y + p.vy * dt);
      if (p.y === 0) { p.vx *= 0.88; p.vz *= 0.88; }
      if (p.life <= 0) particles.splice(i, 1);
    }
    for (let i = rings.length - 1; i >= 0; i--) {
      rings[i].life -= dt;
      if (rings[i].life <= 0) rings.splice(i, 1);
    }
  }

  function checkEnd() {
    if (phase !== "play" || solo) return;
    const alive = bugs.filter((b) => b.alive);
    if (alive.length <= 1) {
      awaitingQuit = false;
      overlaySpectate.classList.add("hidden");
      phase = "result";
      overlayResult.classList.remove("hidden");
      openTune();
      if (alive.length === 1) {
        const w = alive[0];
        resultTitle.textContent = w.isPlayer ? "你留下了" : "对手留下";
        resultSub.textContent = w.isPlayer ? "这一撞能吹。" : "出圈即负。再读一次蓄力。";
      } else {
        resultTitle.textContent = "罐空了";
        resultSub.textContent = "几乎同时出界。";
      }
    }
  }

  function simulate(dt) {
    if (phase === "countdown") {
      countT -= dt;
      const n = Math.ceil(countT);
      countNum.textContent = n > 0 ? String(n) : "斗";
      if (countT <= 0) {
        overlayCount.classList.add("hidden");
        phase = "play";
        flash("随时可跳");
      }
    }

    if (phase === "play") {
      for (const b of bugs) {
        if (!b.isPlayer) continue;
        const inp = playerInput();
        b.holding = inp.holding;
        if (inp.holding && hypot(inp.x, inp.z) > 0.01) {
          b.inX = inp.x;
          b.inZ = inp.z;
          b.dirX = inp.x;
          b.dirZ = inp.z;
        }
      }
      for (const b of bugs) updateAI(b, dt);
    }
    const sub = MOVE_SUBSTEPS;
    const sdt = dt / sub;
    for (let s = 0; s < sub; s++) {
      for (const b of bugs) stepMotion(b, sdt);
      collide();
    }
    for (const b of bugs) markOut(b);
    stepHearts(dt);
    if (phase === "play") {
      for (const b of bugs) stepCharge(b, dt);
    }
    stepParticles(dt);
    camPunch.x *= 0.84;
    camPunch.y *= 0.84;
    if (bannerT > 0) {
      bannerT -= dt;
      if (bannerT <= 0) banner.textContent = "";
    }
    checkEnd();
  }

  function drawJarBg() {
    if (jarTex) ctx.drawImage(jarTex, 0, 0, W, H);
    else {
      ctx.fillStyle = "#161a16";
      ctx.fillRect(0, 0, W, H);
    }
    const lamp = ctx.createRadialGradient(W * 0.5, H * 0.06, 8, W * 0.5, H * 0.2, Math.max(W, H) * 0.58);
    lamp.addColorStop(0, "rgba(232, 210, 156, 0.14)");
    lamp.addColorStop(0.42, "rgba(160, 150, 110, 0.04)");
    lamp.addColorStop(1, "rgba(0,0,0,0)");
    ctx.fillStyle = lamp;
    ctx.fillRect(0, 0, W, H);
    ctx.fillStyle = "rgba(138, 36, 28, 0.78)";
    ctx.beginPath();
    ctx.ellipse(W * 0.86, H * 0.135, 17, 17, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = "rgba(214, 186, 128, 0.45)";
    ctx.lineWidth = 1;
    ctx.stroke();
    ctx.fillStyle = "rgba(244, 226, 186, 0.72)";
    ctx.font = "11px 'Songti SC','STSong','Noto Serif SC',serif";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText("鬥", W * 0.86, H * 0.138);
    ctx.textBaseline = "alphabetic";
  }

  function drawArena() {
    const c = worldToScreen(0, 0, 0);
    const s = c.s;
    const w = ARENA_HW * 2 * s;
    const h = ARENA_HD * 2 * s;
    const rr = ARENA_CORNER * s;
    const lip = Math.max(18, s * 1.4);
    ctx.save();
    ctx.translate(c.x, c.y);
    ctx.scale(1, COS_P);

    ctx.beginPath();
    pathRoundRect(ctx, -ARENA_HW * s - lip, -ARENA_HD * s - lip, w + lip * 2, h + lip * 2, rr + lip * 0.55);
    const cer = ctx.createLinearGradient(0, -h * 0.5 - lip, 0, h * 0.5 + lip);
    cer.addColorStop(0, "#8f978c");
    cer.addColorStop(0.4, "#6e786e");
    cer.addColorStop(1, "#4a534c");
    ctx.fillStyle = cer;
    ctx.fill();

    ctx.save();
    ctx.beginPath();
    pathRoundRect(ctx, -ARENA_HW * s - lip, -ARENA_HD * s - lip, w + lip * 2, h + lip * 2, rr + lip * 0.55);
    pathRoundRect(ctx, -ARENA_HW * s, -ARENA_HD * s, w, h, rr);
    ctx.clip("evenodd");
    ctx.strokeStyle = "rgba(226, 228, 210, 0.16)";
    ctx.lineWidth = 1;
    const spanW = w + lip * 2;
    const spanH = h + lip * 2;
    for (const ck of lipCracks) {
      ctx.beginPath();
      const x0 = -spanW * 0.5 + ck.u * spanW;
      const y0 = -spanH * 0.5 + ck.v * spanH;
      ctx.moveTo(x0, y0);
      ctx.lineTo(x0 + ck.dx, y0 + ck.dy);
      ctx.stroke();
    }
    ctx.restore();

    ctx.beginPath();
    pathRoundRect(ctx, -ARENA_HW * s - lip, -ARENA_HD * s - lip, w + lip * 2, h + lip * 2, rr + lip * 0.55);
    ctx.lineWidth = 2.4;
    ctx.strokeStyle = "#2f3732";
    ctx.stroke();
    ctx.lineWidth = 1;
    ctx.strokeStyle = "rgba(210, 196, 150, 0.32)";
    ctx.stroke();

    ctx.beginPath();
    pathRoundRect(ctx, -ARENA_HW * s, -ARENA_HD * s, w, h, rr);
    ctx.fillStyle = "#b8af94";
    ctx.fill();
    if (sandTex) {
      ctx.save();
      ctx.clip();
      ctx.scale(1, 1 / COS_P);
      ctx.globalAlpha = 0.9;
      ctx.drawImage(sandTex, -ARENA_HW * s, -ARENA_HD * s * COS_P, w, h * COS_P);
      ctx.restore();
    }
    const light = ctx.createRadialGradient(0, -h * 0.18, 10, 0, 0, Math.max(w, h) * 0.55);
    light.addColorStop(0, "rgba(236, 214, 160, 0.1)");
    light.addColorStop(1, "rgba(0,0,0,0)");
    ctx.fillStyle = light;
    ctx.fill();
    const shade = ctx.createRadialGradient(0, 0, Math.min(w, h) * 0.22, 0, 0, Math.max(w, h) * 0.62);
    shade.addColorStop(0, "rgba(0,0,0,0)");
    shade.addColorStop(1, "rgba(40, 44, 36, 0.2)");
    ctx.fillStyle = shade;
    ctx.fill();

    ctx.beginPath();
    pathRoundRect(ctx, -ARENA_HW * s, -ARENA_HD * s, w, h, rr);
    ctx.lineWidth = 5;
    ctx.strokeStyle = "#5c6560";
    ctx.stroke();
    ctx.lineWidth = 1.35;
    ctx.strokeStyle = "rgba(214, 196, 150, 0.52)";
    ctx.beginPath();
    pathRoundRect(ctx, -ARENA_HW * s + 3, -ARENA_HD * s + 3, w - 6, h - 6, Math.max(3, rr - 3));
    ctx.stroke();
    ctx.lineWidth = 1;
    ctx.strokeStyle = "rgba(36, 42, 38, 0.32)";
    ctx.beginPath();
    pathRoundRect(ctx, -ARENA_HW * s + 7, -ARENA_HD * s + 7, w - 14, h - 14, Math.max(2, rr - 6));
    ctx.stroke();
    ctx.restore();
  }

  function sampleJumpPoint(b, d, dist, y) {
    return worldToScreen(b.x + d.x * dist, b.z + d.z * dist, y);
  }

  function drawArc(b) {
    if (!b.charging || !b.alive) return;
    const dvx = chargeDeltaV(b);
    if (dvx <= 0.01) return;
    const d = norm(b.dirX, b.dirZ);
    if (d.d < 1e-5) return;
    const range = jumpRange(dvx);
    const dist = range.total;
    if (dist <= 0.01) return;
    const full = clamp(b.chargeT / knobs.tMax, 0, 1);
    const g = gravity();
    const dvy = dvx * tanTheta();
    const ty = range.ty;
    const airSteps = 18;
    const gndSteps = 8;

    function airPt(i) {
      const t = (i / airSteps) * ty;
      const along = dvx * t;
      const y = Math.max(0, dvy * t - 0.5 * g * t * t);
      return sampleJumpPoint(b, d, along, y);
    }
    function gndPt(i) {
      const along = range.air + (range.ground * i) / gndSteps;
      return sampleJumpPoint(b, d, along, 0);
    }

    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    ctx.beginPath();
    for (let i = 0; i <= airSteps; i++) {
      const p = airPt(i);
      if (i === 0) ctx.moveTo(p.x, p.y);
      else ctx.lineTo(p.x, p.y);
    }
    for (let i = 1; i <= gndSteps; i++) {
      const p = gndPt(i);
      ctx.lineTo(p.x, p.y);
    }
    ctx.strokeStyle = full >= 1 ? "rgba(40, 22, 10, 0.28)" : "rgba(40, 22, 10, 0.18)";
    ctx.lineWidth = 7;
    ctx.stroke();

    ctx.beginPath();
    for (let i = 0; i <= airSteps; i++) {
      const p = airPt(i);
      if (i === 0) ctx.moveTo(p.x, p.y);
      else ctx.lineTo(p.x, p.y);
    }
    ctx.strokeStyle = full >= 1 ? "rgba(240, 196, 96, 0.95)" : `rgba(196, 132, 48, ${0.5 + full * 0.4})`;
    ctx.lineWidth = 2.6;
    ctx.stroke();

    ctx.beginPath();
    const land = gndPt(0);
    ctx.moveTo(land.x, land.y);
    for (let i = 1; i <= gndSteps; i++) {
      const p = gndPt(i);
      ctx.lineTo(p.x, p.y);
    }
    ctx.strokeStyle = full >= 1 ? "rgba(232, 188, 96, 0.8)" : `rgba(196, 132, 48, ${0.35 + full * 0.3})`;
    ctx.lineWidth = 2.2;
    ctx.setLineDash([5, 4]);
    ctx.stroke();
    ctx.setLineDash([]);

    const landMark = sampleJumpPoint(b, d, range.air, 0);
    ctx.fillStyle = full >= 1 ? "rgba(240, 196, 96, 0.9)" : "rgba(138, 74, 24, 0.7)";
    ctx.beginPath();
    ctx.arc(landMark.x, landMark.y, 2.4, 0, Math.PI * 2);
    ctx.fill();

    const tip = sampleJumpPoint(b, d, dist, 0);
    const back = sampleJumpPoint(b, d, dist * 0.86, 0);
    const ang = Math.atan2(tip.y - back.y, tip.x - back.x);
    ctx.save();
    ctx.translate(tip.x, tip.y);
    ctx.rotate(ang);
    ctx.fillStyle = full >= 1 ? "#f0c45e" : "#8a4a18";
    ctx.beginPath();
    ctx.moveTo(7, 0);
    ctx.lineTo(-4, 5);
    ctx.lineTo(-4, -5);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
  }

  function drawShadow(b) {
    const p = worldToScreen(b.x, b.z, 0);
    const k = b.alive ? 1 / (1 + b.y * 0.55) : Math.max(0, 1 - b.outT);
    const hitPx = b.r * CAM_SCALE;
    const rollK = 1 + (b.roll || 0) * 0.38;
    ctx.save();
    ctx.globalAlpha = 0.22 * k;
    ctx.fillStyle = "#1a1008";
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 6 + b.y * CAM_SCALE * 0.15, hitPx * 0.72 * k * rollK, hitPx * 0.32 * k * COS_P * rollK, 0, 0, Math.PI * 2);
    ctx.fill();
    if (b.isPlayer && b.alive) {
      ctx.globalAlpha = 0.62 * k;
      ctx.strokeStyle = palRing(b);
      ctx.lineWidth = 1.25;
      ctx.beginPath();
      ctx.ellipse(p.x, p.y + 6, hitPx * 1.18, hitPx * 0.5 * COS_P, 0, 0, Math.PI * 2);
      ctx.stroke();
    }
    ctx.restore();
  }

  function palRing(b) {
    return (b.pal && b.pal.ring) || "rgba(196,165,116,0.8)";
  }

  function drawHeart(h) {
    if (!h.alive) return;
    const bob = 0.12 + Math.sin(time * 2.6 + h.phase) * 0.07;
    const gnd = worldToScreen(h.x, h.z, 0);
    const p = worldToScreen(h.x, h.z, bob);
    const rad = Math.max(2.6, HEART_R * CAM_SCALE * 0.95);
    ctx.save();
    ctx.globalAlpha = 0.22;
    ctx.fillStyle = "#1a1008";
    ctx.beginPath();
    ctx.ellipse(gnd.x, gnd.y + 3, rad * 0.95, rad * 0.42 * COS_P, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
    const ball = ctx.createRadialGradient(-rad * 0.28, -rad * 0.32, rad * 0.08, 0, 0, rad);
    ball.addColorStop(0, "#ffe9a0");
    ball.addColorStop(0.45, "#f0c44a");
    ball.addColorStop(1, "#c48a18");
    ctx.save();
    ctx.translate(p.x, p.y);
    ctx.beginPath();
    ctx.arc(0, 0, rad, 0, Math.PI * 2);
    ctx.fillStyle = ball;
    ctx.fill();
    ctx.fillStyle = "rgba(255, 250, 220, 0.55)";
    ctx.beginPath();
    ctx.ellipse(-rad * 0.28, -rad * 0.3, rad * 0.28, rad * 0.18, -0.4, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  function drawCricket(b) {
    const p = worldToScreen(b.x, b.z, b.y);
    const chargeN = b.charging ? clamp(b.chargeT / knobs.tMax, 0, 1) : 0;
    const full = b.charging && b.chargeT >= knobs.tMax;
    const jumping = b.airborne && b.y > 0.01;
    const sliding = isMovingOnGround(b);
    const roll = b.roll || 0;
    const hitPose = b.hitTier;
    const buzzing = !jumping && !b.charging && (hitPose === "slip" || roll > 0.35);
    const hit = b.fxHit;
    const idle = !b.charging && !jumping && !sliding && !hitPose && b.alive;
    const squat = b.charging ? 1 - 0.32 * chargeN : 1;
    let stretch = jumping ? 1.1 + Math.min(0.12, b.y * 0.08) : squat;
    const shake = full ? Math.sin(time * 48) * (1.2 + chargeN)
      : hitPose === "slip" ? Math.sin(time * 110) * (1.4 + hit * 2.2) + Math.sin(time * 23) * 1.1
      : hitPose === "ctrl" && hit > 0 ? 0
      : (hit > 0 ? Math.sin(time * 62) * hit * 2.2 : 0);
    const screenAng = Math.atan2(-b.dirZ, b.dirX) + b.spin * (hitPose === "slip" ? 0.24 : 0.1);
    const pal = b.pal;
    const fade = b.alive ? 1 : Math.max(0, 1 - b.outT * 1.4);
    const tId = time * 5.2 + b.id * 1.7;
    const breath = idle ? 1 + Math.sin(tId * 0.5) * 0.025 : 1;
    const hitPx = b.r * CAM_SCALE;

    if (jumping) {
      for (let i = 3; i >= 1; i--) {
        const back = i * 0.07;
        const gp = worldToScreen(b.x - b.vx * back, b.z - b.vz * back, Math.max(0, b.y));
        ctx.save();
        ctx.globalAlpha = fade * (0.07 + (3 - i) * 0.05);
        ctx.fillStyle = pal.wing;
        ctx.beginPath();
        ctx.ellipse(gp.x, gp.y, hitPx * 0.5, hitPx * 0.22 * COS_P, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      }
    }


    ctx.save();
    ctx.globalAlpha = fade;
    ctx.translate(p.x + shake, p.y);
    ctx.scale(1, 0.78);
    ctx.rotate(screenAng);
    if (roll > 0.01) {
      ctx.rotate(roll * Math.PI);
      ctx.scale(1, 1 - roll * 0.22);
    }
    let coil = b.charging ? 0.3 + chargeN * 0.7 : 0;
    let kick = jumping ? 1 : (sliding ? 0.28 : 0);
    let ant = idle ? Math.sin(tId) * 1.6 : (b.charging ? -2.2 - chargeN * 2 : jumping ? 3.2 : Math.sin(tId * 1.3) * 1.2);
    if (!b.charging && !jumping) {
      if (hitPose === "ctrl") {
        coil = 0.28;
        kick = -0.82;
        ant = 0.2;
        stretch = 0.78;
      } else if (hitPose === "base") {
        coil = -0.12;
        kick = 0.72;
        ant = Math.sin(time * 18 + b.id) * 3.1;
        stretch = 1.06 + Math.sin(hit * Math.PI) * 0.16;
      } else if (hitPose === "slip" || roll > 0.25) {
        coil = -0.34;
        kick = 0.5 + Math.sin(time * 32 + b.id) * 0.95;
        ant = Math.sin(time * 28 + b.id) * 5.5;
        stretch = 1.16;
      }
    }
    const squash = b.fxSquash;
    const squashX = hitPose === "ctrl" ? 0.42 : hitPose === "slip" ? 0.12 : 0.2;
    const squashY = hitPose === "ctrl" ? 0.08 : hitPose === "slip" ? 0.3 : 0.26;
    ctx.scale(1 + squash * squashX, 1 - squash * squashY);
    ctx.scale((hitPx / 13) * breath, (hitPx / 13) * stretch);

    ctx.lineCap = "round";
    ctx.lineJoin = "round";

    for (const s of [-1, 1]) {
      ctx.strokeStyle = pal.head;
      ctx.lineWidth = 0.7;
      ctx.beginPath();
      ctx.moveTo(-11.6, s * 1.1);
      ctx.quadraticCurveTo(-14.5, s * 2.4, -16.8, s * 3.6);
      ctx.stroke();
    }

    ctx.fillStyle = pal.leg;
    ctx.strokeStyle = pal.leg;
    for (const s of [-1, 1]) {
      const hipX = 1.4;
      const hipY = s * 2.6 * (1 + roll * 0.7);
      const kneeX = -7.8 - coil * 2.2 + kick * 1.8;
      const kneeY = s * (7.6 - coil * 2.4 + kick * 0.4);
      const footX = -15.2 - kick * 2.4 + coil * 3.2;
      const footY = s * (5.4 - coil * 1.2 + kick * 0.6);
      ctx.lineCap = "round";
      ctx.lineWidth = 4.0;
      ctx.beginPath();
      ctx.moveTo(hipX, hipY);
      ctx.lineTo(kneeX, kneeY);
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(hipX, hipY, 1.35, 0, Math.PI * 2);
      ctx.arc(kneeX, kneeY, 1.85, 0, Math.PI * 2);
      ctx.fill();
      ctx.lineWidth = 1.7;
      ctx.beginPath();
      ctx.moveTo(kneeX, kneeY);
      ctx.lineTo(footX, footY);
      ctx.stroke();
      ctx.lineWidth = 0.65;
      for (let k = 0; k < 3; k++) {
        const u = 0.28 + k * 0.2;
        const sx = kneeX + (footX - kneeX) * u;
        const sy = kneeY + (footY - kneeY) * u;
        ctx.beginPath();
        ctx.moveTo(sx, sy);
        ctx.lineTo(sx - 1.2, sy + s * 1.8);
        ctx.stroke();
      }
      ctx.beginPath();
      ctx.arc(footX, footY, 0.85, 0, Math.PI * 2);
      ctx.fill();
      ctx.lineWidth = 1.2;
      ctx.beginPath();
      ctx.moveTo(5.4, s * 2.4);
      ctx.quadraticCurveTo(8.2, s * (5.2 - coil), 10.6, s * (4.4 - coil * 0.7));
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(3.4, s * 2.8);
      ctx.lineTo(6.0, s * 5.8);
      ctx.stroke();
    }

    ctx.fillStyle = pal.body;
    ctx.beginPath();
    ctx.ellipse(-3.4, 0, 9.6, 4.15, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = pal.belly;
    ctx.beginPath();
    ctx.ellipse(-3.6, 0, 6.4, 2.15, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = pal.stripe;
    ctx.lineWidth = 0.65;
    ctx.globalAlpha = fade * 0.55;
    for (let i = 0; i < 5; i++) {
      ctx.beginPath();
      ctx.ellipse(-7.2 + i * 1.85, 0, 5.8 - i * 0.35, 3.55 - i * 0.18, 0, 0.2, Math.PI - 0.2);
      ctx.stroke();
    }
    ctx.globalAlpha = fade;

    ctx.fillStyle = pal.stripe;
    ctx.beginPath();
    pathRoundRect(ctx, 1.6, -3.5, 6.4, 7, 1.6);
    ctx.fill();
    ctx.fillStyle = pal.body;
    ctx.beginPath();
    pathRoundRect(ctx, 2.1, -2.7, 5.4, 5.4, 1.2);
    ctx.fill();
    if (pal.mark) {
      ctx.fillStyle = pal.mark;
      ctx.beginPath();
      ctx.arc(4.7, 0, 0.68, 0, Math.PI * 2);
      ctx.fill();
    }

    ctx.fillStyle = pal.head;
    ctx.beginPath();
    ctx.ellipse(10.4, 0, 3.6, 3.35, 0, 0, Math.PI * 2);
    ctx.fill();
    if (pal.headSheen) {
      ctx.fillStyle = pal.headSheen;
      ctx.beginPath();
      ctx.ellipse(10.0, -0.9, 2.05, 1.2, -0.28, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.fillStyle = pal.eye;
    ctx.beginPath();
    ctx.ellipse(11.5, -1.55, 1.15, 1.35, 0.25, 0, Math.PI * 2);
    ctx.ellipse(11.5, 1.55, 1.15, 1.35, -0.25, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = pal.gleam;
    ctx.beginPath();
    ctx.arc(11.75, -1.85, 0.38, 0, Math.PI * 2);
    ctx.arc(11.75, 1.25, 0.38, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = pal.wing;
    ctx.globalAlpha = fade * 0.9;
    ctx.beginPath();
    ctx.ellipse(-2.8, -1.35, 8.4, 1.55, -0.08, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.ellipse(-2.8, 1.35, 8.4, 1.55, 0.08, 0, Math.PI * 2);
    ctx.fill();
    ctx.globalAlpha = fade;

    if (buzzing) {
      const copies = 6;
      for (let i = 0; i < copies; i++) {
        const buzz = Math.sin(time * 130 + b.id * 13 + i * 0.7) * 0.18;
        for (const s of [-1, 1]) {
          ctx.save();
          ctx.translate(2.6, s * 1.15);
          ctx.rotate(s * (0.95 + buzz));
          ctx.globalAlpha = fade * (0.55 - i * 0.08);
          ctx.fillStyle = pal.wing;
          ctx.beginPath();
          ctx.moveTo(0, 0);
          ctx.quadraticCurveTo(6, s * 2.2, 17, s * 0.8);
          ctx.quadraticCurveTo(10, s * 6.5, 0.4, s * 1.8);
          ctx.closePath();
          ctx.fill();
          ctx.strokeStyle = pal.gleam;
          ctx.lineWidth = 0.55;
          ctx.beginPath();
          ctx.moveTo(0.6, 0);
          ctx.lineTo(14.5, s * 0.6);
          ctx.stroke();
          ctx.restore();
        }
      }
      ctx.globalAlpha = fade;
    }

    ctx.strokeStyle = pal.head;
    ctx.lineWidth = 0.95;
    ctx.beginPath();
    ctx.moveTo(13.2, -1.5);
    ctx.quadraticCurveTo(20, -6 + ant * 0.25, 29, -4.2 + ant);
    ctx.moveTo(13.2, 1.5);
    ctx.quadraticCurveTo(20, 6 - ant * 0.25, 29, 4.2 - ant);
    ctx.stroke();

    if (b.charging) {
      ctx.fillStyle = `rgba(40, 22, 10, ${0.14 + chargeN * 0.22})`;
      ctx.beginPath();
      ctx.ellipse(-2, 10, 9 + chargeN * 4, 2.8, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = `rgba(232, 176, 72, ${0.25 + chargeN * 0.55})`;
      ctx.lineWidth = 1.1 + chargeN;
      ctx.beginPath();
      ctx.ellipse(-1.2, 0, 13.2 + chargeN * 1.6, 6.6 + chargeN, 0, 0, Math.PI * 2);
      ctx.stroke();
    }
    if (full) {
      ctx.strokeStyle = `rgba(255, 220, 120, ${0.55 + Math.sin(time * 18) * 0.25})`;
      ctx.lineWidth = 1.8;
      ctx.beginPath();
      ctx.ellipse(-1.2, 0, 14.6, 7.4, 0, 0, Math.PI * 2);
      ctx.stroke();
    }
    if (pal.outline) {
      ctx.strokeStyle = pal.outline;
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.ellipse(-1.4, 0, 13.6, 5.8, 0, 0, Math.PI * 2);
      ctx.stroke();
    }
    if (hit > 0) {
      const glow = hitPose === "slip" ? 0.5 : hitPose === "ctrl" ? 0.32 : 0.4;
      ctx.fillStyle = `rgba(255, 236, 200, ${hit * glow})`;
      ctx.beginPath();
      ctx.ellipse(-1, 0, 14, 7, 0, 0, Math.PI * 2);
      ctx.fill();
    }
    if (b.squatFlash > 0) {
      ctx.strokeStyle = `rgba(240,220,180,${b.squatFlash})`;
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.ellipse(-1, 0, 15, 7.6, 0, 0, Math.PI * 2);
      ctx.stroke();
    }
    ctx.restore();

    if (showVel && b.alive) {
      const q = worldToScreen(b.x + b.vx * 0.18, b.z + b.vz * 0.18, b.y);
      ctx.strokeStyle = "rgba(255,220,120,0.7)";
      ctx.beginPath();
      ctx.moveTo(p.x, p.y);
      ctx.lineTo(q.x, q.y);
      ctx.stroke();
    }
  }

  function drawParticles() {
    for (const rg of rings) {
      const u = 1 - clamp(rg.life / rg.max, 0, 1);
      const rad = rg.r0 + (rg.r1 - rg.r0) * u;
      const p = worldToScreen(rg.x, rg.z, 0);
      ctx.save();
      ctx.globalAlpha = (1 - u) * 0.82;
      ctx.strokeStyle = u < 0.22 ? "#fff6d8" : (rg.tint || "#d8b56a");
      ctx.lineWidth = 3 * (1 - u) + 0.6;
      ctx.beginPath();
      ctx.ellipse(p.x, p.y, rad * CAM_SCALE, rad * CAM_SCALE * COS_P, 0, 0, Math.PI * 2);
      ctx.stroke();
      ctx.restore();
    }
    for (const p of particles) {
      const a = clamp(p.life / p.max, 0, 1);
      const s = worldToScreen(p.x, p.z, p.y);
      const rad = Math.max(0.7, (p.r || 1) * CAM_SCALE * 0.22) * (0.7 + a * 0.5);
      ctx.globalAlpha = a;
      if (p.kind === "spark") {
        ctx.fillStyle = p.tint || "#ffe08a";
        ctx.beginPath();
        ctx.arc(s.x, s.y, rad * 1.15, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = "#fff6d0";
        ctx.beginPath();
        ctx.arc(s.x, s.y, rad * 0.4, 0, Math.PI * 2);
        ctx.fill();
      } else {
        ctx.fillStyle = p.tint || "#c4b89a";
        ctx.beginPath();
        ctx.arc(s.x, s.y, rad, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.globalAlpha = 1;
    }
  }

  function drawVignette() {
    const g = ctx.createRadialGradient(camCX, camCY, Math.min(W, H) * 0.2, camCX, camCY, Math.max(W, H) * 0.72);
    g.addColorStop(0, "rgba(0,0,0,0)");
    g.addColorStop(0.75, "rgba(0,0,0,0)");
    g.addColorStop(1, "rgba(12, 16, 14, 0.42)");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, W, H);
  }

  function render() {
    ctx.clearRect(0, 0, W, H);
    drawJarBg();
    drawArena();
    const heartOrder = hearts.slice().sort((a, b) => b.z - a.z);
    for (const h of heartOrder) drawHeart(h);
    const order = bugs.slice().sort((a, b) => b.z - a.z);
    for (const b of order) drawShadow(b);
    for (const b of order) drawArc(b);
    drawParticles();
    for (const b of order) drawCricket(b);
    drawVignette();
  }

  function updateStickVisual() {
    let x = stick.x;
    let y = stick.y;
    if (!stick.active) {
      if (keys.has("KeyW") || keys.has("ArrowUp")) y -= 1;
      if (keys.has("KeyS") || keys.has("ArrowDown")) y += 1;
      if (keys.has("KeyA") || keys.has("ArrowLeft")) x -= 1;
      if (keys.has("KeyD") || keys.has("ArrowRight")) x += 1;
      const d = hypot(x, y);
      if (d > 1) { x /= d; y /= d; }
    }
    stickKnob.style.transform = `translate(${x * STICK_VISUAL}px, ${y * STICK_VISUAL}px)`;
  }

  function canSummonStick() {
    if (phase !== "play") return false;
    if (paused || spectating || awaitingQuit) return false;
    if (isTuneOpen()) return false;
    return true;
  }

  function isStickBlocked(el) {
    if (!el || !el.closest) return false;
    if (el.closest("#tune, #btn-tune")) return true;
    if (el.closest("button")) return true;
    const ov = el.closest(".overlay");
    if (ov && !ov.classList.contains("hidden")) return true;
    return false;
  }

  function showStickAt(x, y) {
    const rect = phoneEl.getBoundingClientRect();
    const pad = STICK_SIZE * 0.5 + 6;
    stick.originX = clamp(x, pad, Math.max(pad, rect.width - pad));
    stick.originY = clamp(y, pad, Math.max(pad, rect.height - pad));
    stickEl.style.left = stick.originX + "px";
    stickEl.style.top = stick.originY + "px";
    stickEl.classList.remove("hidden");
  }

  function setStickFromEvent(e) {
    const rect = phoneEl.getBoundingClientRect();
    const cx = rect.left + stick.originX;
    const cy = rect.top + stick.originY;
    const radius = STICK_SIZE * 0.5;
    let x = (e.clientX - cx) / radius;
    let y = (e.clientY - cy) / radius;
    const d = hypot(x, y);
    if (d > 1) { x /= d; y /= d; }
    stick.x = x;
    stick.y = y;
    stick.active = true;
    updateStickVisual();
  }

  function clearStick() {
    if (stick.pointerId != null && phoneEl.releasePointerCapture) {
      try { phoneEl.releasePointerCapture(stick.pointerId); } catch (_) { /* already released */ }
    }
    stick.x = 0;
    stick.y = 0;
    stick.active = false;
    stick.pointerId = null;
    stickEl.classList.add("hidden");
    updateStickVisual();
  }

  phoneEl.addEventListener("pointerdown", (e) => {
    if (e.pointerType === "mouse" && e.button !== 0) return;
    if (stick.pointerId != null) return;
    if (isStickBlocked(e.target)) return;
    if (!canSummonStick()) return;
    const rect = phoneEl.getBoundingClientRect();
    showStickAt(e.clientX - rect.left, e.clientY - rect.top);
    stick.pointerId = e.pointerId;
    try { phoneEl.setPointerCapture(e.pointerId); } catch (_) { /* ignore */ }
    setStickFromEvent(e);
    e.preventDefault();
  });
  phoneEl.addEventListener("pointermove", (e) => {
    if (stick.pointerId !== e.pointerId) return;
    setStickFromEvent(e);
  });
  function onPointerEnd(e) {
    if (stick.pointerId !== e.pointerId) return;
    clearStick();
  }
  phoneEl.addEventListener("pointerup", onPointerEnd);
  phoneEl.addEventListener("pointercancel", onPointerEnd);
  window.addEventListener("pointerup", onPointerEnd);
  window.addEventListener("pointercancel", onPointerEnd);

  window.addEventListener("keydown", (e) => {
    keys.add(e.code);
    if (e.code === "Space") {
      e.preventDefault();
      userPaused = !userPaused;
      syncPause();
      if (paused) clearStick();
      flash(paused ? "暂停" : "继续");
    }
    if (e.code === "KeyR") restart();
    if (/Arrow|Space/.test(e.code)) e.preventDefault();
    updateStickVisual();
  });
  window.addEventListener("keyup", (e) => {
    keys.delete(e.code);
    updateStickVisual();
  });

  function isTuneOpen() { return !tuneEl.classList.contains("hidden"); }

  function syncPause() {
    const inMatch = phase === "play" || phase === "countdown";
    paused = userPaused || awaitingQuit || (isTuneOpen() && inMatch);
    pauseTag.classList.toggle("hidden", !paused || awaitingQuit);
    pauseTag.textContent = isTuneOpen() && inMatch ? "暂停 · 调参中" : "暂停";
    phoneEl.classList.toggle("tuning", isTuneOpen());
    phoneEl.classList.toggle("spectating", spectating || awaitingQuit);
  }

  function offerQuit() {
    awaitingQuit = true;
    overlaySpectate.classList.remove("hidden");
    clearStick();
    keys.clear();
    updateStickVisual();
    syncPause();
  }

  function continueWatch() {
    awaitingQuit = false;
    spectating = true;
    overlaySpectate.classList.add("hidden");
    userPaused = false;
    syncPause();
  }

  function quitMatch() {
    awaitingQuit = false;
    spectating = false;
    overlaySpectate.classList.add("hidden");
    userPaused = false;
    phase = "result";
    resultTitle.textContent = "你出圈了";
    resultSub.textContent = "提前离开这一罐。";
    overlayResult.classList.remove("hidden");
    openTune();
    syncPause();
  }

  function openTune() {
    tuneEl.classList.remove("hidden");
    clearStick();
    keys.clear();
    updateStickVisual();
    syncPause();
  }

  function closeTune() {
    tuneEl.classList.add("hidden");
    syncPause();
  }

  function beginMatch() {
    overlayStart.classList.add("hidden");
    overlayResult.classList.add("hidden");
    overlaySpectate.classList.add("hidden");
    overlayCount.classList.add("hidden");
    userPaused = false;
    awaitingQuit = false;
    spectating = false;
    closeTune();
    spawnMatch();
    if (solo) {
      phase = "play";
      countT = 0;
      flash("独自练习");
      syncPause();
      return;
    }
    phase = "countdown";
    countT = 3;
    countNum.textContent = "3";
    overlayCount.classList.remove("hidden");
    syncPause();
  }

  function restart() {
    overlayResult.classList.add("hidden");
    beginMatch();
  }

  document.getElementById("btn-start").onclick = () => {
    solo = false;
    beginMatch();
  };
  document.getElementById("btn-solo").onclick = () => {
    solo = true;
    beginMatch();
  };
  document.getElementById("btn-watch").onclick = continueWatch;
  document.getElementById("btn-quit").onclick = quitMatch;
  document.getElementById("btn-again").onclick = restart;
  document.getElementById("btn-reset").onclick = restart;
  document.getElementById("btn-tune").onclick = () => {
    if (isTuneOpen()) closeTune();
    else openTune();
  };
  document.getElementById("btn-tune-close").onclick = closeTune;
  const knobMap = [
    ["tMin", "k-tMin", "v-tMin", 2],
    ["tMax", "k-tMax", "v-tMax", 2],
    ["dMin", "k-dMin", "v-dMin", 2],
    ["vRate", "k-vRate", "v-vRate", 2],
    ["theta", "k-theta", "v-theta", 0],
    ["m", "k-m", "v-m", 2],
    ["g", "k-g", "v-g", 2],
    ["mu", "k-mu", "v-mu", 2],
    ["rStand", "k-rStand", "v-rStand", 2],
    ["rMax", "k-rMax", "v-rMax", 2],
    ["muCtrlScale", "k-muCtrlScale", "v-muCtrlScale", 2],
    ["muSlipScale", "k-muSlipScale", "v-muSlipScale", 2],
    ["growPer", "k-growPer", "v-growPer", 2],
  ];

  function snapshotSettings() {
    return {
      name: "dou-ququ-knobs",
      version: 2,
      knobs: { ...knobs },
      slowmo,
      showVel,
    };
  }

  function applySettings(data) {
    if (!data || typeof data !== "object") return false;
    const src = data.knobs && typeof data.knobs === "object" ? data.knobs : data;
    for (const key of Object.keys(DEFAULTS)) {
      if (typeof src[key] === "number" && Number.isFinite(src[key])) knobs[key] = src[key];
    }
    if (typeof data.slowmo === "boolean") slowmo = data.slowmo;
    if (typeof data.showVel === "boolean") showVel = data.showVel;
    document.getElementById("k-slow").checked = slowmo;
    document.getElementById("k-vel").checked = showVel;
    syncKnobsToUi();
    saveSettings();
    return true;
  }

  function saveSettings() {
    try { localStorage.setItem(SETTINGS_KEY, JSON.stringify(snapshotSettings())); }
    catch (_) { /* private mode */ }
  }

  function loadSettings() {
    try {
      const raw = localStorage.getItem(SETTINGS_KEY);
      if (!raw) return;
      applySettings(JSON.parse(raw));
    } catch (_) { /* ignore bad cache */ }
  }

  function exportSettings() {
    const text = JSON.stringify(snapshotSettings(), null, 2);
    const blob = new Blob([text], { type: "application/json" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = "dou-ququ-settings.json";
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(() => {});
    }
    flash("设定已导出");
  }

  function importSettingsFile(file) {
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      try {
        if (applySettings(JSON.parse(String(reader.result)))) flash("设定已导入");
        else flash("导入失败");
      } catch (_) { flash("导入失败"); }
    };
    reader.readAsText(file);
  }

  function syncKnobsToUi() {
    for (const [key, id, vid, digits] of knobMap) {
      const el = document.getElementById(id);
      el.value = String(knobs[key]);
      document.getElementById(vid).textContent = Number(knobs[key]).toFixed(digits);
    }
  }
  for (const [key, id, vid, digits] of knobMap) {
    document.getElementById(id).addEventListener("input", (e) => {
      knobs[key] = Number(e.target.value);
      document.getElementById(vid).textContent = knobs[key].toFixed(digits);
      saveSettings();
    });
  }

  document.getElementById("btn-defaults").onclick = () => {
    Object.assign(knobs, DEFAULTS);
    slowmo = false;
    showVel = false;
    document.getElementById("k-slow").checked = false;
    document.getElementById("k-vel").checked = false;
    syncKnobsToUi();
    saveSettings();
  };
  document.getElementById("k-slow").onchange = (e) => { slowmo = e.target.checked; saveSettings(); };
  document.getElementById("k-vel").onchange = (e) => { showVel = e.target.checked; saveSettings(); };
  document.getElementById("btn-export").onclick = exportSettings;
  document.getElementById("btn-import").onclick = () => document.getElementById("file-import").click();
  document.getElementById("file-import").onchange = (e) => {
    const file = e.target.files && e.target.files[0];
    importSettingsFile(file);
    e.target.value = "";
  };

  function loop(ts) {
    if (!lastTs) lastTs = ts;
    let dt = Math.min(0.05, (ts - lastTs) / 1000);
    lastTs = ts;
    if (slowmo) dt *= 0.5;
    if (!paused) {
      if (hitstop > 0) hitstop -= dt;
      else {
        acc += dt;
        while (acc >= FIXED_DT) {
          simulate(FIXED_DT);
          time += FIXED_DT;
          acc -= FIXED_DT;
        }
      }
    }
    render();
    requestAnimationFrame(loop);
  }

  function fitCanvas() {
    const dpr = Math.min(2.5, window.devicePixelRatio || 1);
    const rect = canvas.getBoundingClientRect();
    W = Math.max(1, Math.floor(rect.width));
    H = Math.max(1, Math.floor(rect.height));
    canvas.width = Math.round(W * dpr);
    canvas.height = Math.round(H * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    layoutCamera();
  }

  function boot() {
    bakeSand();
    fitCanvas();
    loadSettings();
    spawnMatch();
    phase = "ready";
    syncKnobsToUi();
    openTune();
    updateStickVisual();
    window.addEventListener("resize", fitCanvas);
    document.addEventListener("touchmove", (e) => e.preventDefault(), { passive: false });
    requestAnimationFrame(loop);
  }

  boot();
})();
