(() => {
  "use strict";

  const canvas = document.getElementById("view");
  const ctx = canvas.getContext("2d");
  const hudLive = document.getElementById("hud-live");
  const hudClock = document.getElementById("hud-clock");
  const banner = document.getElementById("banner");
  const overlayStart = document.getElementById("start");
  const overlayCount = document.getElementById("countdown");
  const overlayResult = document.getElementById("result");
  const overlaySpectate = document.getElementById("spectate");
  const overlayNetWait = document.getElementById("net-wait");
  const overlayNetJoin = document.getElementById("net-join");
  const countNum = document.getElementById("count-num");
  const resultTitle = document.getElementById("result-title");
  const resultSub = document.getElementById("result-sub");
  const stickEl = document.getElementById("stick");
  const stickKnob = document.getElementById("stick-knob");
  const STICK_SIZE = 128;
  const STICK_VISUAL = 38;
  const STICK_FIXED = 220;
  const STICK_FIXED_VISUAL = 78;

  const PITCH = 16 * Math.PI / 180;
  const COS_P = Math.cos(PITCH);
  const SIN_P = Math.sin(PITCH);
  const ARENA_HW = 21.2;
  const ARENA_HD = 31.8;
  const ARENA_CORNER = 7.2;
  let CAM_SCALE = 7.7;
  let camCX = 195;
  let camCY = 355;
  const BUG_R = 1.8;
  const MOVE_SUBSTEPS = 6;
  const HEART_R = 0.9;
  const GROW_MAX = 6;
  const R = globalThis.DouQuquRules;
  const SETTLE_SPEED = 0.06;
  const FIXED_DT = 1 / 60;
  const ELASTIC = 1;
  let W = 390;
  let H = 844;

  const FACTORY = {
    tMin: 0,
    tMax: 0.55,
    staminaMax: 100,
    staminaCost: 20,
    staminaRegen: 12,
    staminaSlots: 5,
    dMin: 1.2,
    vRate: 23,
    theta: 45,
    m: 1,
    g: 18,
    mu: 0.5,
    rStand: 0.1,
    rMax: 1,
    rChargeScale: 0.5,
    muCtrlScale: 1.3,
    muSlipScale: 0.65,
    growPer: 0.16,
    bugR: 1.8,
    sizeScale: 1.3,
    sizeT: 6,
    shieldT: 8,
    chargeScale: 1.25,
    chargeBuffT: 5,
    itemR: 1.35,
    regTime: 90,
    otTime: 30,
    heartStart: 4,
    heartCap: 6,
    heartBatch: 6,
    itemBatch: 3,
    itemCap: 3,
    heartGap: 7,
    heartGapOt: 5,
    nestHP: 4,
    nestMass: 3.0,
    nestR: 2.4,
    nestEggN: 5,
    eggHatchT: 3,
    eggHatchGap: 0.28,
    eggHatchJitter: 0.15,
    eggScatterV: 8,
    eggR: 0.55,
    eggMass: 0.2,
    babyLifeT: 8,
    babyRScale: 0.40,
    babyMass: 0.35,
    babyA1Scale: 0.40,
    babyChargeT: 0.16,
    babyAtkCd: 0.8,
    babyCanLoot: 0,
    babyDMin: 0,
    nestCap: 1,
    nestFirstT: 25,
    nestGap: 12,
  };

  const DEFAULTS = {
    tMin: 0,
    tMax: 0.8,
    staminaMax: 100,
    staminaCost: 20,
    staminaRegen: 12,
    staminaSlots: 5,
    dMin: 7,
    vRate: 50,
    theta: 15,
    m: 1,
    g: 80,
    mu: 1.8,
    rStand: 0.4,
    rMax: 0.6,
    rChargeScale: 0.5,
    muCtrlScale: 1.5,
    muSlipScale: 0.3,
    growPer: 0.16,
    bugR: 1.8,
    sizeScale: 1.3,
    sizeT: 6,
    shieldT: 8,
    chargeScale: 1.25,
    chargeBuffT: 5,
    itemR: 1.35,
    regTime: 90,
    otTime: 30,
    heartStart: 4,
    heartCap: 6,
    heartBatch: 6,
    itemBatch: 3,
    itemCap: 3,
    heartGap: 7,
    heartGapOt: 5,
    nestHP: 4,
    nestMass: 3.0,
    nestR: 2.4,
    nestEggN: 5,
    eggHatchT: 3,
    eggHatchGap: 0.28,
    eggHatchJitter: 0.15,
    eggScatterV: 8,
    eggR: 0.55,
    eggMass: 0.2,
    babyLifeT: 8,
    babyRScale: 0.40,
    babyMass: 0.35,
    babyA1Scale: 0.40,
    babyChargeT: 0.16,
    babyAtkCd: 0.8,
    babyCanLoot: 0,
    babyDMin: 0,
    nestCap: 1,
    nestFirstT: 25,
    nestGap: 12,
  };
  const shipped = globalThis.DOU_QUQU_SHIPPED;
  if (shipped && shipped.knobs && typeof shipped.knobs === "object") {
    Object.assign(DEFAULTS, shipped.knobs);
  }

  const knobs = { ...DEFAULTS };
  const SETTINGS_KEY = "dou-ququ-knobs-v3";
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

  function isNestTest() {
    return !!(globalThis.DOU_QUQU_TEST && globalThis.DOU_QUQU_TEST.nestDummy);
  }

  let phase = "boot";
  let solo = false;
  let netRole = null;
  let mySeat = 0;
  let netCode = "";
  let netWs = null;
  let netPeerOk = false;
  let remoteInput = { holding: false, x: 0, z: 0, mag: 0 };
  let remoteV3 = null;
  let netSendAcc = 0;
  let netInputAcc = 0;
  let lastSnap = null;
  let prevSnap = null;
  let snapAt = 0;
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
  const lastAim = { x: 0, z: 1 };
  const INPUT_HINTS = {
    1: "点按召出摇杆 · 按住时间蓄力 · 推向要去的方向",
    2: "往后拉 · 反方向弹出 · 仍按住时间蓄力",
    3: "从圆心拉出 · 长度=加速时间 · 松手后跑完再跳",
  };
  const STICK_HINTS = {
    1: "点按屏幕召出摇杆 · 停稳后蓄力",
    2: "往后拉 · 反方向弹出 · 停稳后蓄力",
    3: "从圆心拉出 · 松手后跑完这段加速再跳",
  };
  let inputVersion = 1;
  const particles = [];
  const rings = [];
  const bugs = [];
  const hearts = [];
  const items = [];
  const nests = [];
  const eggs = [];
  const babies = [];
  let nextBabyId = 100;
  const BABY_PAL = {
    body: "#5c4a34",
    belly: "#d2b48a",
    head: "#3a2c20",
    outline: null,
    leg: "#1a120c",
    wing: "#c4a86a",
    eye: "#100806",
    stripe: "#3a3020",
    gleam: "#efe0b8",
  };
  const FACTION = {
    ally: {
      fill: [36, 170, 148],
      fillFull: [64, 224, 196],
      edge: [16, 56, 50],
      edgeFull: [210, 255, 240],
      arrow: [168, 255, 228],
      ring: [40, 204, 176],
      ringEdge: [8, 36, 32],
    },
    enemy: {
      fill: [204, 46, 122],
      fillFull: [244, 88, 164],
      edge: [64, 12, 36],
      edgeFull: [255, 198, 226],
      arrow: [255, 176, 214],
      ring: [228, 52, 132],
      ringEdge: [52, 8, 28],
    },
    neutral: {
      fill: [196, 124, 40],
      fillFull: [232, 176, 72],
      edge: [90, 48, 16],
      edgeFull: [255, 220, 140],
      arrow: [255, 228, 160],
      ring: [196, 165, 116],
      ringEdge: [40, 28, 16],
    },
  };
  let matchState = null;
  let matchT = 0;

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
    if (!Number.isFinite(x) || !Number.isFinite(z)) return { x: 0, z: 0, d: 0 };
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
      name: NAMES[id] || "小蟋蟀",
      pal: PALETTES[id] || BABY_PAL,
      isPlayer,
      remote: false,
      ai: personality,
      x, z, px: x, pz: z, y: 0, vy: 0,
      vx: 0, vz: 0,
      vInit: 0,
      dirX: -x, dirZ: -z,
      r: BUG_R,
      m: knobs.m,
      grow: 0,
      stamina: Math.max(0, Number(knobs.staminaMax) || 100),
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
      aimMag: 0,
      v3Goal: null,
      v3Preview: 0,
      v3Pending: null,
      holding: false,
      pendingCharge: false,
      inX: 0, inZ: 0,
      airborne: false,
      alive: true,
      outT: 0,
      spin: 0,
      squatFlash: 0,
      tremble: 0,
      buffSizeT: 0,
      buffShieldT: 0,
      buffChargeT: 0,
      rageSize: false,
      rageCharge: false,
    };
  }

  function refreshBody(b) {
    if (b.kind === "baby") R.refreshBabyBody(knobs, b);
    else R.refreshBody(knobs, b);
  }

  function syncRuleKnobs() {
    if (!matchState) return;
    Object.assign(matchState.knobs, knobs);
    matchState.knobs.bugR = BUG_R;
  }

  function heartBlocked(x, z, skip) {
    if (arenaSDF(x, z) > -1.4) return true;
    for (const h of hearts) {
      if (h === skip || !h.alive) continue;
      if (hypot(h.x - x, h.z - z) < 2.0) return true;
    }
    for (const b of bugs) {
      if (hypot(b.x - x, b.z - z) < 2.8) return true;
    }
    for (const n of nests) {
      if (n.alive && hypot(n.x - x, n.z - z) < 2.2) return true;
    }
    for (const e of eggs) {
      if (e.alive && hypot(e.x - x, e.z - z) < 1.8) return true;
    }
    return false;
  }

  function placeHeart(skip) {
    for (let n = 0; n < 80; n++) {
      const x = (rand() * 2 - 1) * ARENA_HW;
      const z = (rand() * 2 - 1) * ARENA_HD;
      if (!heartBlocked(x, z, skip)) return { x, z };
    }
    for (let n = 0; n < 40; n++) {
      const x = (rand() * 2 - 1) * ARENA_HW * 0.72;
      const z = (rand() * 2 - 1) * ARENA_HD * 0.72;
      if (arenaSDF(x, z) <= -1.0) return { x, z };
    }
    const ang = rand() * Math.PI * 2;
    return { x: Math.cos(ang) * 8, z: Math.sin(ang) * 8 };
  }

  function pushHeart(x, z) {
    hearts.push({
      x, z,
      r: HEART_R,
      alive: true,
      respawn: 0,
      phase: rand() * 6,
    });
  }

  function liveHearts() {
    let n = 0;
    for (const h of hearts) if (h.alive) n += 1;
    return n;
  }

  function liveItems() {
    let n = 0;
    for (const it of items) if (it.alive) n += 1;
    return n;
  }

  function spawnHearts() {
    hearts.length = 0;
    const cap = knobs.heartCap || 6;
    const n = Math.min(knobs.heartStart || 4, cap);
    for (let i = 0; i < n; i++) {
      const p = placeHeart(null);
      pushHeart(p.x, p.z);
    }
  }

  function spawnMatch() {
    particles.length = 0;
    rings.length = 0;
    bugs.length = 0;
    hearts.length = 0;
    items.length = 0;
    nests.length = 0;
    eggs.length = 0;
    babies.length = 0;
    nextBabyId = 100;
    matchState = R.createMatchState(knobs);
    matchState.knobs.bugR = BUG_R;
    matchT = 0;
    const pos = [
      { x: 0, z: -ARENA_HD * 0.38 },
      { x: 0, z: ARENA_HD * 0.38 },
    ];
    const pvp = isNetPvp();
    const brains = [
      null,
      isNestTest()
        ? { idle: true, name: "木桩" }
        : { chargeMul: 1.2, react: 0.28, lead: 0.22, name: "贪蓄" },
    ];
    const n = solo ? 1 : 2;
    for (let i = 0; i < n; i++) {
      const mine = i === mySeat;
      const brain = (!pvp && !mine) ? brains[i] : null;
      const b = makeBug(i, pos[i].x, pos[i].z, mine, brain);
      b.remote = pvp && netRole === "host" && !mine;
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
    if (isNestTest()) spawnHouse({ x: 0, z: 0 }, { silent: true });
    spawnHearts();
    phoneEl.classList.remove("rage");
    time = 0;
    hitstop = 0;
    camPunch.x = 0;
    camPunch.y = 0;
    liveHud();
  }

  function liveCount() { return bugs.filter((b) => b.alive).length; }
  function fmtClock(sec) {
    const s = Math.max(0, Math.ceil(sec));
    const m = Math.floor(s / 60);
    return m + ":" + String(s % 60).padStart(2, "0");
  }

  function liveHud() {
    if (netRole === "guest" && lastSnap) return;
    if (!hudLive) return;
    if (solo) {
      hudLive.textContent = "独自练习";
      if (hudClock) hudClock.textContent = matchT > 0 ? fmtClock(matchT) : "";
      return;
    }
    if (isNestTest()) {
      const me = playerBug();
      hudLive.textContent = me ? "控制 " + me.name + " · Tab 切换" : "巢穴测试";
      if (hudClock) hudClock.textContent = matchT > 0 ? fmtClock(matchT) : "";
      return;
    }
    if (!matchState || phase === "boot" || phase === "result") {
      hudLive.textContent = isNetPvp() ? "联机对战" : (liveCount() >= 2 ? "对战中" : "残局");
      if (hudClock) hudClock.textContent = "";
      return;
    }
    syncRuleKnobs();
    matchState.t = matchT;
    const clock = R.matchClock(matchState);
    if (clock.phase === "rage") {
      hudLive.textContent = isNetPvp() ? "联机 · 狂暴" : "狂暴";
      if (hudClock) hudClock.textContent = fmtClock(clock.untilHard);
    } else {
      hudLive.textContent = isNetPvp() ? "联机对战" : (liveCount() >= 2 ? "对战中" : "残局");
      if (hudClock) hudClock.textContent = fmtClock(clock.untilReg);
    }
    phoneEl.classList.toggle("rage", !!matchState.rage);
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

  function stepStamina(b, dt) {
    if (!b.alive || b.kind === "baby" || b.airborne || b.charging) return;
    const max = Math.max(0, Number(knobs.staminaMax) || 0);
    const regen = Math.max(0, Number(knobs.staminaRegen) || 0);
    b.stamina = Math.min(max, Math.max(0, (b.stamina == null ? max : b.stamina) + regen * dt));
  }

  function frictionAccel(muEff)
 {    const mu = muEff == null ? knobs.mu : muEff;
    return Math.max(1e-4, mu * gravity());
  }

  function jumpSpeedMin() {
    const t = tanTheta();
    const denom = 2 * t + 1 / (2 * Math.max(1e-4, knobs.mu));
    return Math.sqrt(Math.max(0, knobs.dMin) * gravity() / Math.max(1e-6, denom));
  }

  function chargeDeltaV(b) {
    if (b.kind === "baby") return R.babyChargeDeltaV(knobs, b);
    return R.chargeDeltaV(knobs, b);
  }

  function chargeTMax(b) {
    if (b.kind === "baby") return R.babyChargeStats(knobs).tMax;
    return R.effectiveCharge(knobs, b).tMax;
  }

  function jumpSpeedMinFor(b) {
    if (b.kind !== "baby") return jumpSpeedMin();
    const dMin = knobs.babyDMin || 0;
    if (dMin <= 0) return 0;
    const t = tanTheta();
    const denom = 2 * t + 1 / (2 * Math.max(1e-4, knobs.mu));
    return Math.sqrt(Math.max(0, dMin) * gravity() / Math.max(1e-6, denom));
  }

  function jumpDeltaV(b) {
    return Math.max(jumpSpeedMinFor(b), chargeDeltaV(b));
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
      b.vInit = 0;
      b.tumble = 0;
      b.hitTier = null;
      return;
    }
    const a = frictionAccel(b.slideMu);
    const next = spd - a * dt;
    if (next <= SETTLE_SPEED) {
      b.vx = 0;
      b.vz = 0;
      b.vInit = 0;
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

  function isNetPvp() {
    return netRole === "host" || netRole === "guest";
  }

  function isHumanControlled(b) {
    return !!(b && (b.isPlayer || b.remote));
  }

  function canStartCharge(b) {
    if (!b || b.kind === "baby") return true;
    return (b.stamina || 0) + 1e-6 >= Math.max(0, Number(knobs.staminaCost) || 0);
  }

  function beginCharge(b) {
    if (!canStartCharge(b)) return false;
    b.charging = true;
    b.chargeT = 0;
    b.pendingCharge = false;
  }

  function clearV3Lock(b) {
    b.v3Goal = null;
    b.v3Pending = null;
    b.v3Preview = 0;
  }

  function interrupt(b) {
    if (b.kind === "baby") {
      if (!b.charging) return;
      b.charging = false;
      b.chargeT = 0;
      b.pendingCharge = true;
      b.squatFlash = 1;
      return;
    }
    if (!b.charging && !b.v3Pending && b.v3Goal == null) return;
    b.pendingCharge = b.holding && inputVersion !== 3;
    b.charging = false;
    b.chargeT = 0;
    b.squatFlash = 1;
    clearV3Lock(b);
  }

  function doJump(b) {
    if (!b.alive) return;
    const d = norm(b.dirX, b.dirZ);
    if (d.d < 1e-5) return;
    if (b.kind !== "baby") {
      const cost = Math.max(0, Number(knobs.staminaCost) || 0);
      if ((b.stamina || 0) + 1e-6 < cost) return;
      b.stamina = Math.max(0, (b.stamina || 0) - cost);
    }
    const dvx = jumpDeltaV(b);
    const dvy = dvx * tanTheta();
    b.dirX = d.x;
    b.dirZ = d.z;
    b.vx = d.x * dvx;
    b.vz = d.z * dvx;
    b.vInit = dvx;
    b.vy = dvy;
    b.y = 0.02;
    b.airborne = true;
    b.slideMu = knobs.mu;
    b.hitTier = null;
    b.charging = false;
    b.chargeT = 0;
    b.pendingCharge = false;
    clearV3Lock(b);
    b.fxSquash = 0.35;
    b.tumble = 0;
    b.roll = 0;
    spawnDust(b.x, b.z, 14, 0.85);
    spawnDust(b.x, b.z, 6, 0.55, "spark");
    if (b.kind === "baby") b.atkCd = R.babyAttackCd(knobs);
  }

  function tryRelease(b) {
    if (!b.charging) return;
    if (b.v3Goal != null) return;
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

  function keyHeld() {
    return keys.has("KeyW") || keys.has("KeyS") || keys.has("KeyA") || keys.has("KeyD")
      || keys.has("ArrowUp") || keys.has("ArrowDown") || keys.has("ArrowLeft") || keys.has("ArrowRight");
  }

  function playerInput() {
    let sx = stick.x;
    let sy = stick.y;
    const usingKeys = keyHeld() && stick.pointerId == null;
    if (keys.has("KeyW") || keys.has("ArrowUp")) sy -= 1;
    if (keys.has("KeyS") || keys.has("ArrowDown")) sy += 1;
    if (keys.has("KeyA") || keys.has("ArrowLeft")) sx -= 1;
    if (keys.has("KeyD") || keys.has("ArrowRight")) sx += 1;
    const n = hypot(sx, sy);
    const held = stick.active || stick.pointerId != null || keyHeld();
    if (!held) return { holding: false, x: 0, z: 0, mag: 0 };
    if (n < 0.12) return { holding: true, x: 0, z: 0, mag: 0 };
    let x = sx / n;
    let z = -sy / n;
    if (inputVersion === 2) {
      x = -x;
      z = -z;
    }
    const mag = inputVersion === 3 && usingKeys ? 1 : Math.min(1, n);
    return { holding: true, x, z, mag };
  }

  function knockoutAim(me, t) {
    const lead = Number.isFinite(me.ai && me.ai.lead) ? me.ai.lead : 0;
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
    if (!b || !b.alive || b.isPlayer || !b.ai || phase !== "play") {
      if (b && b.ai && (b.isPlayer || b.ai.idle)) b.holding = false;
      return;
    }
    if (b.ai.idle) {
      b.holding = false;
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
      let itemAim = null;
      let itemD = 3.2;
      for (const it of items) {
        if (!it.alive) continue;
        if (matchState && matchState.rage && it.kind !== "shield") continue;
        const d = hypot(it.x - b.x, it.z - b.z);
        const bias = it.kind === "shield" ? 0.55 : 0;
        if (d - bias < itemD) { itemD = d - bias; itemAim = it; }
      }
      if (itemAim && Math.random() < 0.48) {
        const d = norm(itemAim.x - b.x, itemAim.z - b.z);
        b.inX = d.x;
        b.inZ = d.z;
        b.dirX = d.x || b.dirX;
        b.dirZ = d.z || b.dirZ;
        b.holding = true;
        b.ai.target = null;
        b.ai.releaseAt = clamp(0.16 + itemD * 0.07, knobs.tMin + 0.04, chargeTMax(b) * 0.7);
        return;
      }
      if (heartAim && Math.random() < 0.42) {
        const d = norm(heartAim.x - b.x, heartAim.z - b.z);
        b.inX = d.x;
        b.inZ = d.z;
        b.dirX = d.x || b.dirX;
        b.dirZ = d.z || b.dirZ;
        b.holding = true;
        b.ai.target = null;
        b.ai.releaseAt = clamp(0.18 + heartD * 0.08, knobs.tMin + 0.04, chargeTMax(b) * 0.7);
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
        const chargeMul = Number.isFinite(b.ai.chargeMul) ? b.ai.chargeMul : 1;
        let t = (0.32 + gap * 0.16) * chargeMul;
        if (target.charging) t = Math.min(t, 0.55 + Math.random() * 0.15);
        if (Math.random() < 0.12) t = chargeTMax(b) * (0.85 + Math.random() * 0.12);
        b.ai.releaseAt = clamp(t, knobs.tMin + 0.04, chargeTMax(b) * 0.98);
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
        const react = Number.isFinite(b.ai.react) ? b.ai.react : 0.28;
        b.ai.timer = react + Math.random() * 0.35;
      }
    }
  }

  function startV3Windup(b, mag, dirX, dirZ) {
    if (!b || !b.alive || !isHumanControlled(b)) return;
    if (b.v3Goal != null) return;
    if (mag < 0.12) return;
    const goal = mag * chargeTMax(b);
    if (goal < knobs.tMin - 1e-4) return;
    b.dirX = dirX;
    b.dirZ = dirZ;
    b.inX = dirX;
    b.inZ = dirZ;
    b.holding = false;
    b.v3Preview = 0;
    b.v3Pending = { mag, dirX, dirZ, goal };
    if (!isSettled(b)) return;
    b.v3Goal = goal;
    b.v3Pending = null;
    beginCharge(b);
  }

  function playerBug() {
    for (const b of bugs) {
      if (b.isPlayer && b.alive) return b;
    }
    return null;
  }

  function localBug() {
    for (const b of bugs) {
      if (b.isPlayer) return b;
    }
    return null;
  }

  function factionOf(b) {
    if (!b) return "neutral";
    const me = localBug();
    if (b.kind === "baby") {
      if (b.ownerId == null || b.ownerId < 0) return "neutral";
      if (me && b.ownerId === me.id) return "ally";
      return "enemy";
    }
    if (me) return b === me || b.id === me.id ? "ally" : "enemy";
    return b.isPlayer ? "ally" : "enemy";
  }

  function factionPaint(b) {
    return FACTION[factionOf(b)] || FACTION.neutral;
  }

  function rgbStr(c) {
    return c[0] + ", " + c[1] + ", " + c[2];
  }

  function switchNestControl() {
    if (!isNestTest()) return;
    if (phase !== "play" && phase !== "countdown") return;
    const live = [];
    for (const b of bugs) if (b.alive) live.push(b);
    if (live.length < 2) {
      flash("没有可切换的蟋蟀");
      return;
    }
    const cur = playerBug();
    let i = cur ? live.indexOf(cur) : 0;
    if (i < 0) i = 0;
    const next = live[(i + 1) % live.length];
    for (const b of bugs) {
      const take = b === next;
      b.isPlayer = take;
      if (take) {
        b.ai = null;
        continue;
      }
      b.ai = { idle: true, name: (b.ai && b.ai.name) || "木桩" };
      b.holding = false;
      interrupt(b);
      clearV3Lock(b);
    }
    clearStick();
    stealPlayFocus();
    flash("控制 " + next.name + " · Tab 再切");
    liveHud();
  }

  function v3Pull() {
    const n = hypot(stick.x, stick.y);
    if (n < 0.12) return null;
    return { x: stick.x / n, z: -stick.y / n, mag: Math.min(1, n) };
  }

  function commitV3FromStick() {
    if (inputVersion !== 3) return;
    const pull = v3Pull();
    if (!pull) return;
    if (netRole === "guest") {
      netSendInput({ v3: { mag: pull.mag, dirX: pull.x, dirZ: pull.z } });
      const b = playerBug();
      if (b) {
        b.charging = true;
        b.chargeT = 0;
        b.dirX = pull.x;
        b.dirZ = pull.z;
      }
      return;
    }
    startV3Windup(playerBug(), pull.mag, pull.x, pull.z);
  }

  function stepCharge(b, dt) {
    if (!b.alive) return;
    if (b.kind === "baby") {
      stepBabyCharge(b, dt);
      return;
    }
    if (isHumanControlled(b) && inputVersion === 3 && (b.v3Goal != null || b.v3Pending) && !isSettled(b)) {
      interrupt(b);
      return;
    }

    if (isHumanControlled(b) && inputVersion === 3) {
      if (b.v3Pending && isSettled(b) && b.v3Goal == null) {
        const p = b.v3Pending;
        b.v3Goal = p.goal;
        b.dirX = p.dirX;
        b.dirZ = p.dirZ;
        b.v3Pending = null;
        beginCharge(b);
      }
      if (b.v3Goal != null) {
        if (!isSettled(b)) {
          interrupt(b);
          return;
        }
        if (!b.charging) beginCharge(b);
        const cap = Math.min(b.v3Goal, chargeTMax(b));
        b.chargeT = Math.min(cap, b.chargeT + dt);
        if (b.chargeT >= cap - 1e-4) doJump(b);
        return;
      }
      if (b.charging) {
        b.charging = false;
        b.chargeT = 0;
      }
      return;
    }

    if (b.holding) {
      if (!isSettled(b)) {
        if (b.charging) interrupt(b);
        else b.pendingCharge = true;
        return;
      }
      if (!b.charging) beginCharge(b);
      b.pendingCharge = false;
      b.chargeT = Math.min(chargeTMax(b), b.chargeT + dt);
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
        spawnDust(b.x, b.z, 2, 0.22 + clamp(b.chargeT / Math.max(1e-6, chargeTMax(b)), 0, 1) * 0.25);
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
    b.vInit = 0;
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
    if (R.tryShieldSave(knobs, b, arenaSDF, arenaGradient)) {
      b.px = b.x;
      b.pz = b.z;
      spawnDust(b.x, b.z, 18, 0.9, "spark");
      spawnDust(b.x, b.z, 10, 0.55);
      flash(`${b.name} 罩碎`);
      return;
    }
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
    spawnDust(h.x, h.z, 10, 0.55);
  }

  function spawnHeartWave() {
    const n = R.heartWaveSize(matchState, liveHearts());
    for (let i = 0; i < n; i++) {
      const p = placeHeart(null);
      pushHeart(p.x, p.z);
    }
    if (n > 0) R.markHeartFilled(matchState);
  }

  function stepHeartEconomy() {
    if (!matchState || phase !== "play") return;
    syncRuleKnobs();
    matchState.t = matchT;
    if (R.shouldRefillHeart(matchState, liveHearts())) spawnHeartWave();
  }

  function stepHearts(dt) {
    for (const h of hearts) {
      h.phase += dt;
      if (!h.alive) continue;
      if (phase !== "play") continue;
      for (const b of lootActors()) {
        if (!b.alive) continue;
        if (hypot(b.x - h.x, b.z - h.z) < b.r + h.r) {
          eatHeart(b, h);
          refreshBody(b);
          break;
        }
      }
    }
    stepHeartEconomy();
  }

  function itemBlocked(x, z) {
    if (arenaSDF(x, z) > -2.4) return true;
    for (const b of bugs) {
      if (!b.alive) continue;
      if (hypot(b.x - x, b.z - z) < 3.0) return true;
    }
    for (const h of hearts) {
      if (!h.alive) continue;
      if (hypot(h.x - x, h.z - z) < 2.2) return true;
    }
    for (const it of items) {
      if (!it.alive) continue;
      if (hypot(it.x - x, it.z - z) < 2.0) return true;
    }
    for (const n of nests) {
      if (n.alive && hypot(n.x - x, n.z - z) < 2.2) return true;
    }
    for (const e of eggs) {
      if (e.alive && hypot(e.x - x, e.z - z) < 1.8) return true;
    }
    return false;
  }

  function placeItem() {
    const blockers = [];
    for (const b of bugs) {
      if (b.alive) blockers.push({ x: b.x, z: b.z, min: 3.0 });
    }
    for (const h of hearts) {
      if (h.alive) blockers.push({ x: h.x, z: h.z, min: 2.2 });
    }
    for (const it of items) {
      if (it.alive) blockers.push({ x: it.x, z: it.z, min: 2.0 });
    }
    for (const n of nests) {
      if (n.alive) blockers.push({ x: n.x, z: n.z, min: 2.2 });
    }
    for (const e of eggs) {
      if (e.alive) blockers.push({ x: e.x, z: e.z, min: 1.8 });
    }
    for (let n = 0; n < 8; n++) {
      const p = R.placePoint(rand, blockers, {
        sdf: arenaSDF,
        minEdge: 2.4,
        ringMin: 6,
        ringMax: 12,
        hw: ARENA_HW,
        hd: ARENA_HD,
      });
      if (!itemBlocked(p.x, p.z)) return p;
    }
    return R.placePoint(rand, blockers, {
      sdf: arenaSDF,
      minEdge: 2.4,
      ringMin: 6,
      ringMax: 12,
    });
  }

  function eatItem(b, it) {
    it.alive = false;
    R.applyItem(knobs, b, it.kind);
    refreshBody(b);
    const names = { size: "增大", shield: "护盾", charge: "蓄力" };
    flash(`${b.name} · ${names[it.kind] || it.kind}`);
    spawnDust(it.x, it.z, 12, 0.6, it.kind === "charge" ? "spark" : "sand");
  }

  function stepItems(dt) {
    if (!matchState || phase !== "play") return;
    syncRuleKnobs();
    matchState.t = matchT;
    const kinds = R.dueItemSpawns(matchState, rand, liveItems());
    for (const kind of kinds) {
      const p = placeItem();
      items.push({
        kind,
        x: p.x,
        z: p.z,
        r: knobs.itemR,
        alive: true,
        phase: rand() * 6,
      });
    }
    for (const it of items) {
      it.phase += dt;
      if (!it.alive) continue;
      for (const b of bugs) {
        if (!b.alive || b.kind === "baby") continue;
        if (hypot(b.x - it.x, b.z - it.z) < b.r + it.r) {
          eatItem(b, it);
          break;
        }
      }
    }
  }

  function lootActors() {
    const out = [];
    for (const b of bugs) if (b.alive) out.push(b);
    if (R.babyCanLoot(knobs)) {
      for (const b of babies) if (b.alive) out.push(b);
    }
    return out;
  }

  function movers() {
    const out = [];
    for (const b of bugs) if (b.alive) out.push(b);
    for (const b of babies) if (b.alive) out.push(b);
    return out;
  }

  function nestChainLive() {
    for (const n of nests) if (n.alive) return true;
    for (const e of eggs) if (e.alive) return true;
    for (const b of babies) if (b.alive) return true;
    return false;
  }

  function nestLiveCount() {
    return nestChainLive() ? 1 : 0;
  }

  function clampInArena(x, z, pad) {
    let cx = x;
    let cz = z;
    const p = pad == null ? 0.4 : pad;
    for (let i = 0; i < 48; i++) {
      const s = arenaSDF(cx, cz);
      if (s <= -p) break;
      const g = arenaGradient(cx, cz);
      const len = hypot(g.x, g.z) || 1;
      const step = Math.max(0.04, s + p);
      cx -= (g.x / len) * step;
      cz -= (g.z / len) * step;
    }
    return { x: cx, z: cz };
  }

  function nestBlocked(x, z) {
    if (arenaSDF(x, z) > -2.4) return true;
    for (const b of bugs) {
      if (b.alive && hypot(b.x - x, b.z - z) < 3.0) return true;
    }
    for (const h of hearts) {
      if (h.alive && hypot(h.x - x, h.z - z) < 1.6) return true;
    }
    for (const it of items) {
      if (it.alive && hypot(it.x - x, it.z - z) < 1.6) return true;
    }
    return false;
  }

  function placeNest() {
    const blockers = [];
    for (const b of bugs) {
      if (b.alive) blockers.push({ x: b.x, z: b.z, min: 3.0 });
    }
    for (const h of hearts) {
      if (h.alive) blockers.push({ x: h.x, z: h.z, min: 1.6 });
    }
    for (const it of items) {
      if (it.alive) blockers.push({ x: it.x, z: it.z, min: 1.6 });
    }
    for (let n = 0; n < 10; n++) {
      const p = R.placePoint(rand, blockers, {
        sdf: arenaSDF,
        minEdge: 2.4,
        ringMin: 6,
        ringMax: 12,
        hw: ARENA_HW,
        hd: ARENA_HD,
      });
      if (!nestBlocked(p.x, p.z)) return p;
    }
    return R.placePoint(rand, blockers, {
      sdf: arenaSDF,
      minEdge: 2.4,
      ringMin: 6,
      ringMax: 12,
    });
  }

  function spawnHouse(at, opts) {
    const p = at || placeNest();
    nests.push({
      x: p.x,
      z: p.z,
      r: knobs.nestR,
      m: knobs.nestMass,
      hp: knobs.nestHP,
      maxHp: knobs.nestHP,
      alive: true,
      touching: {},
    });
    if (!opts || !opts.silent) flash("小房子出现了");
    spawnDust(p.x, p.z, 12, 0.7);
  }

  function ownerName(id) {
    const b = bugs.find((x) => x.id === id);
    return b ? b.name : "无主";
  }

  function explodeNest(nest, ownerId) {
    nest.alive = false;
    nest.hp = 0;
    const pad = (knobs.eggR || 0.55) + 0.08;
    const laid = R.scatterEggs({
      n: knobs.nestEggN,
      pad,
      hw: ARENA_HW,
      hd: ARENA_HD,
      minSep: Math.max(0.7, (knobs.eggR || 0.55) * 2),
      speed: 0,
      rand,
      sdf: arenaSDF,
      clamp: (x, z, p) => clampInArena(x, z, p),
    });
    const hatches = R.eggHatchTimes(laid.length, knobs, rand);
    for (let i = 0; i < laid.length; i++) {
      const e = laid[i];
      const hatchMax = hatches[i];
      eggs.push({
        x: e.x,
        z: e.z,
        px: e.x,
        pz: e.z,
        vx: e.vx,
        vz: e.vz,
        r: knobs.eggR,
        m: knobs.eggMass,
        alive: true,
        hatchT: hatchMax,
        hatchMax,
        ownerId: ownerId == null ? -1 : ownerId,
        phase: rand() * 6,
      });
    }
    spawnDust(nest.x, nest.z, 22, 1.15, "spark");
    spawnRing(nest.x, nest.z, 1.4, 1.4, "rgba(210, 150, 70, 0.9)");
    if (ownerId == null || ownerId < 0) flash("房子碎了 · 无主的卵");
    else flash(`${ownerName(ownerId)} 砸开了房子`);
  }

  function makeBaby(egg) {
    const owner = bugs.find((b) => b.id === egg.ownerId);
    const b = makeBug(nextBabyId, egg.x, egg.z, false, null);
    nextBabyId += 1;
    b.kind = "baby";
    b.ownerId = egg.ownerId == null ? -1 : egg.ownerId;
    b.name = "小蟋蟀";
    b.pal = owner && owner.pal ? owner.pal : BABY_PAL;
    b.ai = null;
    b.lifeT = knobs.babyLifeT;
    b.atkCd = 0;
    b.vx = egg.vx * 0.25;
    b.vz = egg.vz * 0.25;
    b.vInit = hypot(b.vx, b.vz);
    refreshBody(b);
    return b;
  }

  function babyTarget(b) {
    const foes = [];
    for (const p of bugs) {
      if (!p.alive) continue;
      if (b.ownerId >= 0 && p.id === b.ownerId) continue;
      foes.push(p);
    }
    if (!foes.length) return null;
    let best = foes[0];
    let bestD = hypot(best.x - b.x, best.z - b.z);
    for (let i = 1; i < foes.length; i++) {
      const d = hypot(foes[i].x - b.x, foes[i].z - b.z);
      if (d < bestD) {
        best = foes[i];
        bestD = d;
      }
    }
    return best;
  }

  function stepBabyCharge(b, dt) {
    const enemy = babyTarget(b);
    if (!R.canBabyCharge(b)) {
      if (b.charging) interrupt(b);
      return;
    }
    if (!isSettled(b)) {
      if (b.charging) interrupt(b);
      return;
    }
    if (enemy) {
      const d = norm(enemy.x - b.x, enemy.z - b.z);
      if (d.d > 1e-5) {
        b.dirX = d.x;
        b.dirZ = d.z;
      }
    } else if (hypot(b.dirX, b.dirZ) < 1e-5) {
      const a = rand() * Math.PI * 2;
      b.dirX = Math.cos(a);
      b.dirZ = Math.sin(a);
    }
    if (!b.charging) beginCharge(b);
    b.chargeT = Math.min(chargeTMax(b), b.chargeT + dt);
    if (b.chargeT >= chargeTMax(b) - 1e-4) doJump(b);
  }

  function killBaby(b, reason) {
    if (!b.alive) return;
    b.alive = false;
    b.charging = false;
    b.airborne = true;
    spawnDust(b.x, b.z, 8, 0.55);
    if (reason) flash(reason);
  }

  function markBabyOut(b) {
    if (!b.alive) return;
    if (b.lifeT <= 0 || !inArena(b.x, b.z)) killBaby(b, null);
  }

  function collidePairs(list) {
    for (let i = 0; i < list.length; i++) {
      for (let j = i + 1; j < list.length; j++) {
        const a = list[i];
        const b = list[j];
        if (!a.alive || !b.alive) continue;
        if (a.kind === "baby" && b.id === a.ownerId) continue;
        if (b.kind === "baby" && a.id === b.ownerId) continue;
        if (a.kind === "baby" && b.kind === "baby") {
          resolveBabyOverlap(a, b);
          continue;
        }
        resolveMoverHit(a, b);
      }
    }
  }

  function resolveBabyOverlap(a, b) {
    const minD = a.r + b.r;
    let dx = b.x - a.x;
    let dz = b.z - a.z;
    let dist = hypot(dx, dz);
    let nx;
    let nz;
    if (dist < minD) {
      const hit = separatePair(a, b);
      if (!hit) return;
      nx = hit.nx;
      nz = hit.nz;
    } else {
      const pax = a.px - b.px;
      const paz = a.pz - b.pz;
      const vx = (a.x - a.px) - (b.x - b.px);
      const vz = (a.z - a.pz) - (b.z - b.pz);
      const aa = vx * vx + vz * vz;
      if (aa < 1e-10) return;
      const bb = 2 * (pax * vx + paz * vz);
      const cc = pax * pax + paz * paz - minD * minD;
      const disc = bb * bb - 4 * aa * cc;
      if (disc < 0) return;
      const tHit = (-bb - Math.sqrt(disc)) / (2 * aa);
      if (tHit < 0 || tHit > 1) return;
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
    const invA = 1 / a.m;
    const invB = 1 / b.m;
    const inv = invA + invB;
    const rvx = b.vx - a.vx;
    const rvz = b.vz - a.vz;
    const vn = rvx * nx + rvz * nz;
    if (vn >= -1e-4) return;
    const jImp = -(1 + ELASTIC) * vn / inv;
    a.vx -= (jImp * invA) * nx;
    a.vz -= (jImp * invA) * nz;
    b.vx += (jImp * invB) * nx;
    b.vz += (jImp * invB) * nz;
    a.vInit = hypot(a.vx, a.vz);
    b.vInit = hypot(b.vx, b.vz);
  }

  function resolveMoverHit(a, b) {
    const minD = a.r + b.r;
    let dx = b.x - a.x;
    let dz = b.z - a.z;
    let dist = hypot(dx, dz);
    let nx;
    let nz;
    if (dist < minD) {
      const hit = separatePair(a, b);
      if (!hit) return;
      nx = hit.nx;
      nz = hit.nz;
    } else {
      const pax = a.px - b.px;
      const paz = a.pz - b.pz;
      const vx = (a.x - a.px) - (b.x - b.px);
      const vz = (a.z - a.pz) - (b.z - b.pz);
      const aa = vx * vx + vz * vz;
      if (aa < 1e-10) return;
      const bb = 2 * (pax * vx + paz * vz);
      const cc = pax * pax + paz * paz - minD * minD;
      const disc = bb * bb - 4 * aa * cc;
      if (disc < 0) return;
      const tHit = (-bb - Math.sqrt(disc)) / (2 * aa);
      if (tHit < 0 || tHit > 1) return;
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

  function separateFromStatic(bug, sx, sz, sr) {
    let dx = bug.x - sx;
    let dz = bug.z - sz;
    let dist = hypot(dx, dz);
    const minD = bug.r + sr;
    if (dist >= minD) return null;
    if (dist < 1e-6) { dx = 1; dz = 0; dist = 1; }
    const nx = dx / dist;
    const nz = dz / dist;
    const overlap = minD - dist;
    bug.x += nx * overlap;
    bug.z += nz * overlap;
    return { nx: -nx, nz: -nz };
  }

  function bounceStatic(bug, nest, nx, nz) {
    const ox = nest.x;
    const oz = nest.z;
    const dummy = {
      id: -8,
      name: "房子",
      x: nest.x,
      z: nest.z,
      vx: 0,
      vz: 0,
      vInit: 0,
      m: nest.m,
      r: nest.r,
      charging: false,
      grow: 0,
      stamina: Math.max(0, Number(knobs.staminaMax) || 100),
      kind: "nest",
      dirX: nx,
      dirZ: nz,
      hitTier: null,
      fxHit: 0,
      fxSquash: 0,
      tumble: 0,
      spin: 0,
      roll: 0,
      lastHitId: -1,
    };
    const jImp = bouncePair(bug, dummy, nx, nz);
    nest.x = ox;
    nest.z = oz;
    return jImp;
  }

  function collideNests() {
    for (const nest of nests) {
      if (!nest.alive) continue;
      const hits = [];
      const nowTouch = {};
      for (const b of bugs) {
        if (!b.alive || b.kind === "baby") continue;
        const minD = b.r + nest.r;
        let dx = nest.x - b.x;
        let dz = nest.z - b.z;
        let dist = hypot(dx, dz);
        let nx;
        let nz;
        let overlapped = dist < minD;
        if (!overlapped) {
          const pax = b.px - nest.x;
          const paz = b.pz - nest.z;
          const vx = b.x - b.px;
          const vz = b.z - b.pz;
          const aa = vx * vx + vz * vz;
          if (aa >= 1e-10) {
            const bb = 2 * (pax * vx + paz * vz);
            const cc = pax * pax + paz * paz - minD * minD;
            const disc = bb * bb - 4 * aa * cc;
            if (disc >= 0) {
              const tHit = (-bb - Math.sqrt(disc)) / (2 * aa);
              if (tHit >= 0 && tHit <= 1) {
                b.x = b.px + (b.x - b.px) * tHit;
                b.z = b.pz + (b.z - b.pz) * tHit;
                dx = nest.x - b.x;
                dz = nest.z - b.z;
                dist = hypot(dx, dz) || 1;
                overlapped = true;
              }
            }
          }
        }
        if (!overlapped) continue;
        nowTouch[b.id] = true;
        const sep = separateFromStatic(b, nest.x, nest.z, nest.r);
        if (!sep) continue;
        nx = sep.nx;
        nz = sep.nz;
        const vn = (0 - b.vx) * nx + (0 - b.vz) * nz;
        bounceStatic(b, nest, nx, nz);
        nest.x = nest.x;
        nest.z = nest.z;
        if (R.isNewNestContact(!!nest.touching[b.id], vn)) {
          hits.push({ id: b.id, vn });
        }
      }
      nest.touching = nowTouch;
      if (!hits.length) continue;
      const res = R.resolveNestHits(nest.hp, hits);
      nest.hp = res.hp;
      if (res.exploded) explodeNest(nest, res.ownerId);
    }
  }

  function collideEggs() {
    for (const egg of eggs) {
      if (!egg.alive) continue;
      for (const b of bugs) {
        if (!b.alive || b.kind === "baby") continue;
        const minD = b.r + egg.r;
        let dx = egg.x - b.x;
        let dz = egg.z - b.z;
        let dist = hypot(dx, dz);
        if (dist >= minD) {
          const pax = b.px - egg.px;
          const paz = b.pz - egg.pz;
          const vx = (b.x - b.px) - (egg.x - egg.px);
          const vz = (b.z - b.pz) - (egg.z - egg.pz);
          const aa = vx * vx + vz * vz;
          if (aa < 1e-10) continue;
          const bb = 2 * (pax * vx + paz * vz);
          const cc = pax * pax + paz * paz - minD * minD;
          const disc = bb * bb - 4 * aa * cc;
          if (disc < 0) continue;
          const tHit = (-bb - Math.sqrt(disc)) / (2 * aa);
          if (tHit < 0 || tHit > 1) continue;
          b.x = b.px + (b.x - b.px) * tHit;
          b.z = b.pz + (b.z - b.pz) * tHit;
          egg.x = egg.px + (egg.x - egg.px) * tHit;
          egg.z = egg.pz + (egg.z - egg.pz) * tHit;
          dx = egg.x - b.x;
          dz = egg.z - b.z;
          dist = hypot(dx, dz) || 1;
        }
        if (dist < 1e-6) { dx = 1; dz = 0; dist = 1; }
        const nx = dx / dist;
        const nz = dz / dist;
        const dummy = {
          id: -9,
          x: egg.x,
          z: egg.z,
          vx: egg.vx,
          vz: egg.vz,
          vInit: hypot(egg.vx, egg.vz),
          m: egg.m,
          r: egg.r,
          charging: false,
          grow: 0,
      stamina: Math.max(0, Number(knobs.staminaMax) || 100),
          kind: "egg",
          dirX: nx,
          dirZ: nz,
          hitTier: null,
          fxHit: 0,
          fxSquash: 0,
          tumble: 0,
          spin: 0,
          roll: 0,
          lastHitId: -1,
        };
        separatePair(b, dummy);
        bouncePair(b, dummy, nx, nz);
        egg.x = dummy.x;
        egg.z = dummy.z;
        egg.vx = dummy.vx;
        egg.vz = dummy.vz;
        dummy.charging = false;
      }
    }
  }

  function stepEggs(dt) {
    const a = frictionAccel(knobs.mu);
    for (const egg of eggs) {
      if (!egg.alive) continue;
      egg.px = egg.x;
      egg.pz = egg.z;
      const spd = hypot(egg.vx, egg.vz);
      if (spd > 1e-5) {
        const drop = Math.min(spd, a * dt);
        egg.vx -= (egg.vx / spd) * drop;
        egg.vz -= (egg.vz / spd) * drop;
      } else {
        egg.vx = 0;
        egg.vz = 0;
      }
      egg.x += egg.vx * dt;
      egg.z += egg.vz * dt;
      egg.hatchT -= dt;
      if (!inArena(egg.x, egg.z)) {
        egg.alive = false;
        spawnDust(egg.x, egg.z, 6, 0.4);
        continue;
      }
      if (egg.hatchT <= 0) {
        egg.alive = false;
        const baby = makeBaby(egg);
        babies.push(baby);
        spawnDust(egg.x, egg.z, 10, 0.6, "spark");
      }
    }
  }

  function stepBabies(dt) {
    for (const b of babies) {
      if (!b.alive) continue;
      b.lifeT -= dt;
      R.tickBabyAtkCd(b, dt);
      if (b.lifeT <= 0) killBaby(b, null);
    }
  }

  function stepNests() {
    if (!matchState || phase !== "play") return;
    syncRuleKnobs();
    matchState.t = matchT;
    const live = nestChainLive();
    if (live) matchState.nestActive = true;
    else if (matchState.nestActive) {
      R.markNestCleared(matchState);
      matchState.nestActive = false;
    }
    if (R.shouldSpawnNest(matchState, nestLiveCount())) spawnHouse();
  }

  function stepMatchClock(dt) {
    if (phase !== "play") return;
    matchT += dt;
    if (!matchState) return;
    syncRuleKnobs();
    matchState.t = matchT;
    if (R.enterRage(matchState, bugs)) {
      flash("狂暴");
      phoneEl.classList.add("rage");
    }
    for (const b of bugs) {
      if (!b.alive) continue;
      R.tickBuffs(knobs, b, dt);
    }
    liveHud();
  }

  function motionInitSpeed(b) {
    if (typeof b.vInit === "number" && Number.isFinite(b.vInit)) return Math.max(0, b.vInit);
    return hypot(b.vx || 0, b.vz || 0);
  }

  function resistInitSpeed(b) {
    if (isSettled(b) && b.charging) {
      return Math.max(0, chargeDeltaV(b)) * Math.max(0, knobs.rChargeScale);
    }
    return motionInitSpeed(b);
  }

  function resistOf(b) {
    const vmax = vMax();
    const V0 = knobs.rStand * vmax;
    const K = knobs.rMax;
    const v = clamp(resistInitSpeed(b), 0, vmax);
    const t = vmax < 1e-6 ? 0 : v / vmax;
    return V0 * b.m + t * (K * b.m * vmax - V0 * b.m);
  }

  function initNormalSpeed(b, nx, nz) {
    const spd = hypot(b.vx || 0, b.vz || 0);
    if (spd < 1e-8) return 0;
    return motionInitSpeed(b) * ((b.vx * nx + b.vz * nz) / spd);
  }

  function hitTierFor(me, other, nx, nz) {
    const vMeN = me.vx * nx + me.vz * nz;
    const vOtN = other.vx * nx + other.vz * nz;
    if (Math.abs(vMeN) + 1e-9 >= Math.abs(vOtN)) return "ctrl";
    const iMeN = initNormalSpeed(me, nx, nz);
    const iOtN = initNormalSpeed(other, nx, nz);
    const dP = other.m * (-iOtN) - me.m * iMeN;
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
    a.vInit = hypot(a.vx, a.vz);
    b.vInit = hypot(b.vx, b.vz);
    faceVelocity(a);
    faceVelocity(b);
    interrupt(a);
    interrupt(b);
    if (a.kind !== "nest" && a.kind !== "egg" && b.kind !== "nest" && b.kind !== "egg") {
      a.lastHitId = R.hitCreditId(b);
      b.lastHitId = R.hitCreditId(a);
    }
    applyHitSlide(a, tierA, jImp);
    applyHitSlide(b, tierB, -jImp);
    a.hitNx = -nx;
    a.hitNz = -nz;
    b.hitNx = nx;
    b.hitNz = nz;
    playHitFx(a, nx, nz, jImp, tierA);
    playHitFx(b, -nx, -nz, jImp, tierB);
    if (a.kind !== "nest" && b.kind !== "nest" && a.kind !== "egg" && b.kind !== "egg") {
      flash(`${a.name} ${tierLabel(tierA)} · ${b.name} ${tierLabel(tierB)}`);
    }
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
    collidePairs(movers());
    collideNests();
    collideEggs();
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

  function finishMatch(title, sub) {
    awaitingQuit = false;
    overlaySpectate.classList.add("hidden");
    phase = "result";
    overlayResult.classList.remove("hidden");
    resultTitle.textContent = title;
    resultSub.textContent = sub;
    openTune();
    phoneEl.classList.remove("rage");
    liveHud();
    if (netRole === "host") netSendState();
  }

  function checkEnd() {
    if (phase !== "play" || solo) return;
    const alive = bugs.filter((b) => b.alive);
    if (alive.length <= 1) {
      if (alive.length === 1) {
        const w = alive[0];
        const mine = w.id === mySeat;
        finishMatch(mine ? "你留下了" : "对手留下", mine ? "这一撞能吹。" : "出圈即负。再读一次蓄力。");
      } else {
        finishMatch("罐空了", "几乎同时出界。");
      }
      return;
    }
    if (matchT + 1e-9 >= knobs.regTime + knobs.otTime) {
      const w = R.centerWinner(bugs);
      if (w.tie || !w.winner) finishMatch("平手", "一样靠近场心。");
      else {
        const mine = w.winner.id === mySeat;
        finishMatch(mine ? "你更靠场心" : "对手更靠场心", "加时结束，比谁离中心近。");
      }
    }
  }

  function applyHumanInput(b, inp) {
    if (!b || !b.alive || !inp) return;
    b.aimMag = inp.mag || 0;
    if (inp.v3) {
      startV3Windup(b, inp.v3.mag, inp.v3.dirX, inp.v3.dirZ);
      return;
    }
    if (inputVersion === 3) {
      if (b.v3Goal != null || b.v3Pending) {
        b.holding = false;
        b.v3Preview = 0;
      } else {
        b.holding = false;
        b.v3Preview = inp.holding ? inp.mag : 0;
        if (inp.holding && hypot(inp.x, inp.z) > 0.01) {
          b.inX = inp.x;
          b.inZ = inp.z;
          b.dirX = inp.x;
          b.dirZ = inp.z;
          lastAim.x = inp.x;
          lastAim.z = inp.z;
        }
      }
    } else {
      b.holding = !!inp.holding;
      if (inp.holding && hypot(inp.x, inp.z) > 0.01) {
        b.inX = inp.x;
        b.inZ = inp.z;
        b.dirX = inp.x;
        b.dirZ = inp.z;
      }
    }
  }

  function simulate(dt) {
    if (netRole === "guest") {
      pumpGuest(dt);
      return;
    }
    if (phase === "countdown") {
      countT -= dt;
      const n = Math.ceil(countT);
      countNum.textContent = n > 0 ? String(n) : "斗";
      if (countT <= 0) {
        overlayCount.classList.add("hidden");
        phase = "play";
        refreshStickMode();
        flash("随时可跳");
      }
    }

    if (phase === "play") {
      applyHumanInput(playerBug(), playerInput());
      if (netRole === "host") {
        const remote = bugs.find((b) => b.remote);
        const inp = {
          holding: remoteInput.holding,
          x: remoteInput.x,
          z: remoteInput.z,
          mag: remoteInput.mag,
        };
        if (remoteV3) {
          inp.v3 = remoteV3;
          remoteV3 = null;
        }
        applyHumanInput(remote, inp);
      }
      for (const b of bugs) updateAI(b, dt);
    }
    const sub = MOVE_SUBSTEPS;
    const sdt = dt / sub;
    for (let s = 0; s < sub; s++) {
      for (const b of bugs) stepMotion(b, sdt);
      for (const b of babies) stepMotion(b, sdt);
      collide();
    }
    for (const b of bugs) markOut(b);
    for (const b of babies) markBabyOut(b);
    if (phase === "play") {
      stepMatchClock(dt);
      stepHearts(dt);
      stepItems(dt);
      stepEggs(dt);
      stepBabies(dt);
      stepNests();
      for (const b of bugs) stepStamina(b, dt);
      for (const b of bugs) stepCharge(b, dt);
      for (const b of babies) stepCharge(b, dt);
    }
    stepParticles(dt);
    camPunch.x *= 0.84;
    camPunch.y *= 0.84;
    if (bannerT > 0) {
      bannerT -= dt;
      if (bannerT <= 0) banner.textContent = "";
    }
    checkEnd();
    if (netRole === "host") {
      netSendAcc += dt;
      if (netSendAcc >= 0.05 || phase === "result") {
        netSendAcc = 0;
        netSendState();
      }
    }
  }

  function drawJarBg() {
    if (jarTex) ctx.drawImage(jarTex, 0, 0, W, H);
    else {
      ctx.fillStyle = "#161a16";
      ctx.fillRect(0, 0, W, H);
    }
    const lamp = ctx.createRadialGradient(W * 0.5, H * 0.06, 8, W * 0.5, H * 0.2, Math.max(W, H) * 0.58);
    if (matchState && matchState.rage) {
      lamp.addColorStop(0, "rgba(232, 120, 72, 0.22)");
      lamp.addColorStop(0.42, "rgba(180, 70, 40, 0.08)");
      lamp.addColorStop(1, "rgba(0,0,0,0)");
    } else {
      lamp.addColorStop(0, "rgba(232, 210, 156, 0.14)");
      lamp.addColorStop(0.42, "rgba(160, 150, 110, 0.04)");
      lamp.addColorStop(1, "rgba(0,0,0,0)");
    }
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
    if (!b.alive) return;
    const preview = b.v3Preview >= 0.12 && b.v3Goal == null && !b.charging;
    if (!b.charging && !preview) return;
    const tMax = Math.max(1e-6, chargeTMax(b));
    const t = preview ? b.v3Preview * tMax : b.chargeT;
    const saved = b.chargeT;
    b.chargeT = t;
    const dvx = chargeDeltaV(b);
    b.chargeT = saved;
    if (dvx <= 0.01) return;
    const d = norm(b.dirX, b.dirZ);
    if (d.d < 1e-5) return;
    const range = jumpRange(dvx);
    const dist = range.total;
    if (dist <= 0.01) return;
    const full = clamp(t / tMax, 0, 1);
    const halfW = Math.max(0.35, b.r);
    const px = -d.z;
    const pz = d.x;
    const start = worldToScreen(b.x, b.z, 0);
    const tip = worldToScreen(b.x + d.x * dist, b.z + d.z * dist, 0);
    if (![start.x, start.y, tip.x, tip.y].every(Number.isFinite)) return;
    const paint = factionPaint(b);
    const peak = full >= 1 ? 0.5 : 0.22 + full * 0.2;
    const rgb = rgbStr(full >= 1 ? paint.fillFull : paint.fill);
    const edgeRgb = rgbStr(full >= 1 ? paint.edgeFull : paint.edge);
    const arrowRgb = rgbStr(paint.arrow);
    const segs = 18;

    function edgePt(side, t) {
      const along = dist * t;
      return worldToScreen(
        b.x + d.x * along + px * halfW * side,
        b.z + d.z * along + pz * halfW * side,
        0
      );
    }

    ctx.save();
    const grad = ctx.createLinearGradient(start.x, start.y, tip.x, tip.y);
    grad.addColorStop(0, `rgba(${rgb}, 0)`);
    grad.addColorStop(0.16, `rgba(${rgb}, ${peak})`);
    grad.addColorStop(0.84, `rgba(${rgb}, ${peak})`);
    grad.addColorStop(1, `rgba(${rgb}, 0)`);
    ctx.fillStyle = grad;
    ctx.beginPath();
    const l0 = edgePt(1, 0);
    const r0 = edgePt(-1, 0);
    const l1 = edgePt(1, 1);
    const r1 = edgePt(-1, 1);
    ctx.moveTo(l0.x, l0.y);
    ctx.lineTo(l1.x, l1.y);
    ctx.lineTo(r1.x, r1.y);
    ctx.lineTo(r0.x, r0.y);
    ctx.closePath();
    ctx.fill();

    ctx.lineCap = "butt";
    ctx.lineJoin = "miter";
    ctx.lineWidth = full >= 1 ? 2.4 : 2;
    for (let side = -1; side <= 1; side += 2) {
      for (let i = 0; i < segs; i++) {
        const t0 = i / segs;
        const t1 = (i + 1) / segs;
        const mid = (t0 + t1) * 0.5;
        const fade = mid < 0.16 ? mid / 0.16 : mid > 0.84 ? (1 - mid) / 0.16 : 1;
        if (fade <= 0.02) continue;
        const a = edgePt(side, t0);
        const c = edgePt(side, t1);
        ctx.beginPath();
        ctx.moveTo(a.x, a.y);
        ctx.lineTo(c.x, c.y);
        ctx.strokeStyle = `rgba(${edgeRgb}, ${0.2 + fade * 0.75})`;
        ctx.stroke();
      }
    }

    const arrowLen = Math.min(1.15, Math.max(0.55, halfW * 0.85));
    const arrowW = halfW * 0.42;
    const gap = Math.max(1.6, arrowLen * 2.1);
    const first = Math.min(dist * 0.22, gap);
    for (let along = first; along < dist - arrowLen * 1.2; along += gap) {
      const cx = b.x + d.x * along;
      const cz = b.z + d.z * along;
      const t = along / dist;
      const fade = t < 0.16 ? t / 0.16 : t > 0.84 ? (1 - t) / 0.16 : 1;
      const pTip = worldToScreen(cx + d.x * arrowLen, cz + d.z * arrowLen, 0);
      const pL = worldToScreen(cx - d.x * arrowLen * 0.35 + px * arrowW, cz - d.z * arrowLen * 0.35 + pz * arrowW, 0);
      const pR = worldToScreen(cx - d.x * arrowLen * 0.35 - px * arrowW, cz - d.z * arrowLen * 0.35 - pz * arrowW, 0);
      ctx.beginPath();
      ctx.moveTo(pTip.x, pTip.y);
      ctx.lineTo(pL.x, pL.y);
      ctx.lineTo(pR.x, pR.y);
      ctx.closePath();
      ctx.fillStyle = `rgba(${arrowRgb}, ${0.28 + fade * (full >= 1 ? 0.58 : 0.38)})`;
      ctx.fill();
    }
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
    ctx.restore();
    if (b.alive) drawGroundRing(b, p, hitPx);
  }

  function drawGroundRing(b, p, hitPx) {
    const fac = factionOf(b);
    const paint = FACTION[fac] || FACTION.neutral;
    const baby = b.kind === "baby";
    const cx = p.x;
    const cy = p.y + 6;
    let rx = baby ? Math.max(11, hitPx * 1.9) : hitPx * 1.42;
    let ry = baby ? Math.max(5, hitPx * 0.82 * COS_P) : hitPx * 0.62 * COS_P;
    let fillA = baby ? 0.34 : 0.22;
    let lineA = baby ? 0.96 : 0.94;
    if (phase === "countdown" && fac === "ally" && !baby) {
      const pulse = Math.abs(Math.sin(time * 9));
      const s = 1 + pulse * 0.12;
      rx *= s;
      ry *= s;
      fillA = 0.2 + pulse * 0.16;
      lineA = 0.78 + pulse * 0.2;
    }
    ctx.save();
    ctx.globalAlpha = fillA;
    ctx.fillStyle = "rgb(" + rgbStr(paint.ring) + ")";
    ctx.beginPath();
    ctx.ellipse(cx, cy, rx, ry, 0, 0, Math.PI * 2);
    ctx.fill();
    if (fac === "enemy" && !baby) {
      const dash = Math.max(6, rx * 0.42);
      ctx.setLineDash([dash, dash * 0.6]);
    } else {
      ctx.setLineDash([]);
    }
    ctx.globalAlpha = lineA;
    ctx.lineWidth = baby ? 3 : 3.6;
    ctx.strokeStyle = "rgba(" + rgbStr(paint.ringEdge) + ",0.92)";
    ctx.beginPath();
    ctx.ellipse(cx, cy, rx, ry, 0, 0, Math.PI * 2);
    ctx.stroke();
    ctx.lineWidth = baby ? 1.9 : 2.3;
    ctx.strokeStyle = "rgb(" + rgbStr(paint.ring) + ")";
    ctx.beginPath();
    ctx.ellipse(cx, cy, rx, ry, 0, 0, Math.PI * 2);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.restore();
  }

  function pickupScreenR(r) {
    return Math.max(3.2, r * CAM_SCALE * 0.95);
  }

  function mixRgb(a, b, t) {
    const k = clamp(t, 0, 1);
    return [
      Math.round(a[0] + (b[0] - a[0]) * k),
      Math.round(a[1] + (b[1] - a[1]) * k),
      Math.round(a[2] + (b[2] - a[2]) * k),
    ];
  }

  function drawTimeCapsule(cx, cy, fillN, rgb, bright) {
    const w = 28;
    const h = 6;
    const rad = h / 2;
    const x = Math.round(cx - w / 2);
    const y = Math.round(cy - h / 2);
    const n = clamp(fillN, 0, 1);
    const [r0, g0, b0] = rgb;
    const lift = bright ? 0.45 : 0;
    const r = Math.round(r0 + (255 - r0) * lift);
    const g = Math.round(g0 + (255 - g0) * lift);
    const b = Math.round(b0 + (255 - b0) * lift);
    ctx.save();
    ctx.beginPath();
    if (ctx.roundRect) ctx.roundRect(x, y, w, h, rad);
    else {
      ctx.moveTo(x + rad, y);
      ctx.arcTo(x + w, y, x + w, y + h, rad);
      ctx.arcTo(x + w, y + h, x, y + h, rad);
      ctx.arcTo(x, y + h, x, y, rad);
      ctx.arcTo(x, y, x + w, y, rad);
    }
    ctx.fillStyle = "rgba(18, 12, 8, 0.72)";
    ctx.fill();
    const fw = Math.max(0, (w - 2) * n);
    if (fw > 0.5) {
      ctx.beginPath();
      if (ctx.roundRect) ctx.roundRect(x + 1, y + 1, fw, h - 2, Math.max(1, rad - 1));
      else ctx.rect(x + 1, y + 1, fw, h - 2);
      ctx.fillStyle = `rgb(${r},${g},${b})`;
      ctx.fill();
    }
    ctx.restore();
  }

  function drawHeart(h) {
    if (!h.alive) return;
    const bob = 0.12 + Math.sin(time * 2.6 + h.phase) * 0.07;
    const gnd = worldToScreen(h.x, h.z, 0);
    const p = worldToScreen(h.x, h.z, bob);
    const rad = pickupScreenR(h.r || HEART_R);
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

  function drawNest(n) {
    if (!n.alive) return;
    const gnd = worldToScreen(n.x, n.z, 0);
    const hpN = clamp(n.hp / Math.max(1, n.maxHp), 0, 1);
    const s = n.r * CAM_SCALE;
    ctx.save();
    ctx.globalAlpha = 0.28;
    ctx.fillStyle = "#1a1008";
    ctx.beginPath();
    ctx.ellipse(gnd.x, gnd.y + 6, s * 1.05, s * 0.46 * COS_P, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
    ctx.save();
    ctx.translate(gnd.x, gnd.y);
    ctx.scale(1, COS_P);
    ctx.translate(0, -s * 0.35);
    const wall = hpN > 0.75 ? "#c4a06a" : hpN > 0.5 ? "#b89058" : hpN > 0.25 ? "#9a7048" : "#7a5438";
    ctx.fillStyle = wall;
    ctx.strokeStyle = "#4a3018";
    ctx.lineWidth = 1.4;
    ctx.beginPath();
    ctx.moveTo(-s * 0.85, s * 0.15);
    ctx.lineTo(-s * 0.85, -s * 0.35);
    ctx.lineTo(0, -s * 0.95);
    ctx.lineTo(s * 0.85, -s * 0.35);
    ctx.lineTo(s * 0.85, s * 0.15);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    ctx.fillStyle = hpN > 0.5 ? "#8a3a22" : "#5a2818";
    ctx.beginPath();
    ctx.moveTo(-s * 0.92, -s * 0.28);
    ctx.lineTo(0, -s * 1.08);
    ctx.lineTo(s * 0.92, -s * 0.28);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    ctx.fillStyle = "#3a2414";
    ctx.fillRect(-s * 0.18, -s * 0.08, s * 0.36, s * 0.22);
    if (hpN < 0.99) {
      ctx.strokeStyle = `rgba(40, 20, 10, ${0.35 + (1 - hpN) * 0.5})`;
      ctx.lineWidth = 1.2 + (1 - hpN);
      ctx.beginPath();
      ctx.moveTo(-s * 0.2, -s * 0.55);
      ctx.lineTo(-s * 0.05, -s * 0.1);
      ctx.lineTo(-s * 0.28, s * 0.08);
      if (hpN < 0.6) {
        ctx.moveTo(s * 0.12, -s * 0.7);
        ctx.lineTo(s * 0.32, -s * 0.15);
      }
      if (hpN < 0.35) {
        ctx.moveTo(s * 0.4, -s * 0.2);
        ctx.lineTo(s * 0.15, s * 0.12);
      }
      ctx.stroke();
    }
    ctx.restore();
  }

  function drawEgg(e) {
    if (!e.alive) return;
    const left = clamp(e.hatchT / Math.max(1e-6, e.hatchMax || knobs.eggHatchT), 0, 1);
    const bob = 0.06 + Math.sin(time * 3.1 + e.phase) * 0.03;
    const gnd = worldToScreen(e.x, e.z, 0);
    const p = worldToScreen(e.x, e.z, bob);
    const rad = pickupScreenR(e.r || knobs.eggR);
    ctx.save();
    ctx.globalAlpha = 0.2;
    ctx.fillStyle = "#1a1008";
    ctx.beginPath();
    ctx.ellipse(gnd.x, gnd.y + 3, rad * 0.85, rad * 0.38 * COS_P, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
    ctx.save();
    ctx.translate(p.x, p.y);
    ctx.rotate(-0.12);
    const shell = ctx.createRadialGradient(-rad * 0.2, -rad * 0.35, rad * 0.08, 0, 0, rad * 1.1);
    shell.addColorStop(0, "#fff6e0");
    shell.addColorStop(0.55, "#e8d2a4");
    shell.addColorStop(1, "#c4a06a");
    ctx.fillStyle = shell;
    ctx.strokeStyle = "#7a5a32";
    ctx.lineWidth = 1.1;
    ctx.beginPath();
    ctx.ellipse(0, 0, rad * 0.72, rad * 1.02, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    if (left < 0.55) {
      ctx.strokeStyle = `rgba(70, 40, 16, ${0.35 + (1 - left) * 0.5})`;
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(-rad * 0.1, -rad * 0.55);
      ctx.lineTo(rad * 0.05, -rad * 0.05);
      ctx.lineTo(-rad * 0.18, rad * 0.4);
      if (left < 0.28) {
        ctx.moveTo(rad * 0.22, -rad * 0.2);
        ctx.lineTo(rad * 0.02, rad * 0.35);
      }
      ctx.stroke();
    }
    ctx.restore();
    drawTimeCapsule(p.x, p.y - rad * 1.15 - 8, left, [232, 210, 164], left <= 0.25);
  }

  const SVG_SHIELD = {
    body: new Path2D("M32 5 L54 13.5 V33.5 C54 47.5 43 56.5 32 60.5 C21 56.5 10 47.5 10 33.5 V13.5 Z"),
    face: new Path2D("M32 10.5 L48.5 16.5 V33 C48.5 44.5 40.5 52 32 55 C23.5 52 15.5 44.5 15.5 33 V16.5 Z"),
    ridge: new Path2D("M32 10.5 V55 C40.5 52 48.5 44.5 48.5 33 V16.5 Z"),
    boss: new Path2D("M32 30 m-5.2 0 a5.2 5.2 0 1 0 10.4 0 a5.2 5.2 0 1 0 -10.4 0"),
    chevron: new Path2D("M23.5 21.5 L32 16.5 L40.5 21.5"),
  };
  const SVG_FIRE = {
    outer: new Path2D("M32 60 C18 60 13 47 15.5 37 C17.5 43 23 42.5 23 32 C23 19 30.5 11 32 4.5 C36.5 14 48 18.5 48 34 C54 28.5 54.5 40 50.5 48 C46.5 56 40 60 32 60 Z"),
    mid: new Path2D("M32 55.5 C23 55.5 20.5 46 22.5 40 C24.5 44.5 28.2 44 28.2 36 C28.2 26.5 32 20.5 33 16.5 C36 22.5 42 25 42 36 C46 32 46.2 40 44 46 C41 52 37 55.5 32 55.5 Z"),
    core: new Path2D("M32 49.5 C27.4 49.5 26.2 44 27.2 41 C28.2 43.4 30.2 43.2 30.2 38.8 C30.2 33.2 32.2 30.2 33 28 C35.2 32 38 33.2 38 38 C40.2 36 40.4 41 39.2 44.6 C37.8 48 35 49.5 32 49.5 Z"),
  };

  function withSvgView(g, rad, draw) {
    g.save();
    g.scale(rad * 2.05 / 64, rad * 2.05 / 64);
    g.translate(-32, -32);
    draw();
    g.restore();
  }

  function drawSvgShield(g, rad) {
    withSvgView(g, rad, () => {
      g.fillStyle = "#5e6c68";
      g.strokeStyle = "#24302c";
      g.lineWidth = 2.2;
      g.lineJoin = "round";
      g.fill(SVG_SHIELD.body);
      g.stroke(SVG_SHIELD.body);
      g.fillStyle = "#b4c2bc";
      g.fill(SVG_SHIELD.face);
      g.fillStyle = "#8e9e98";
      g.fill(SVG_SHIELD.ridge);
      g.fillStyle = "#e4ece8";
      g.strokeStyle = "#2c3834";
      g.lineWidth = 1.4;
      g.fill(SVG_SHIELD.boss);
      g.stroke(SVG_SHIELD.boss);
      g.strokeStyle = "#2c3834";
      g.lineWidth = 1.8;
      g.lineCap = "round";
      g.stroke(SVG_SHIELD.chevron);
    });
  }

  function drawSvgFire(g, rad, t) {
    const flicker = 1 + Math.sin(t * 11) * 0.05 + Math.sin(t * 17.3) * 0.03;
    g.save();
    g.scale(1, flicker);
    g.translate(0, (1 - flicker) * rad * 0.35);
    withSvgView(g, rad, () => {
      g.fillStyle = "#b51c12";
      g.fill(SVG_FIRE.outer);
      g.fillStyle = "#ef4e14";
      g.fill(SVG_FIRE.mid);
      g.fillStyle = "#ffc24a";
      g.fill(SVG_FIRE.core);
    });
    g.restore();
  }

  function drawItem(it) {
    if (!it.alive) return;
    const bob = 0.1 + Math.sin(time * 2.4 + it.phase) * 0.06;
    const gnd = worldToScreen(it.x, it.z, 0);
    const p = worldToScreen(it.x, it.z, bob);
    const rad = pickupScreenR(it.r || knobs.itemR);
    ctx.save();
    ctx.globalAlpha = 0.24;
    ctx.fillStyle = "#1a1008";
    ctx.beginPath();
    ctx.ellipse(gnd.x, gnd.y + 5, rad * 1.05, rad * 0.48 * COS_P, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
    ctx.save();
    ctx.translate(p.x, p.y);
    if (it.kind === "size") {
      const pulse = 1 + Math.sin(time * 3 + it.phase) * 0.08;
      ctx.scale((rad / 9) * pulse, (rad / 9) * pulse);
      ctx.fillStyle = "#c4a060";
      ctx.strokeStyle = "rgba(90, 56, 24, 0.85)";
      ctx.lineWidth = 1.1;
      ctx.beginPath();
      ctx.ellipse(0, 2, 9, 7, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
      ctx.fillStyle = "rgba(240, 220, 160, 0.35)";
      ctx.beginPath();
      ctx.ellipse(-3, -1, 3, 2, -0.4, 0, Math.PI * 2);
      ctx.fill();
    } else if (it.kind === "shield") {
      drawSvgShield(ctx, rad);
    } else {
      drawSvgFire(ctx, rad, time + it.phase);
    }
    ctx.restore();
  }

  function drawBugShield(b) {
    if (!b.alive || !R.shieldActive(b)) return;
    const p = worldToScreen(b.x, b.z, b.y + b.r * 0.15);
    const k = 1 / (1 + b.y * 0.4);
    const rx = b.r * CAM_SCALE * 1.35 * k;
    const ry = b.r * CAM_SCALE * 0.72 * COS_P * k;
    ctx.save();
    ctx.translate(p.x, p.y);
    ctx.strokeStyle = "rgba(186, 206, 200, 0.8)";
    ctx.fillStyle = "rgba(150, 170, 166, 0.16)";
    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.ellipse(0, -b.r * CAM_SCALE * 0.15, rx, ry, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.restore();
  }

  function drawStaminaRing(b, p, hitPx) {
    if (!b.alive || b.kind === "baby") return;
    const max = Math.max(1, Number(knobs.staminaMax) || 100);
    const ratio = clamp((b.stamina == null ? max : b.stamina) / max, 0, 1);
    const slots = Math.max(3, Math.min(8, Math.round(Number(knobs.staminaSlots) || 5)));
    const radius = hitPx * 1.52; const span = Math.PI * 2 / slots; const gap = 0.1;
    const color = ratio <= 0.2 ? "#e05a45" : ratio <= 0.4 ? "#e5b34f" : "#7bd5a4";
    ctx.save(); ctx.lineWidth = Math.max(1.5, hitPx * 0.075); ctx.lineCap = "round";
    for (let i = 0; i < slots; i++) {
      const start = -Math.PI / 2 + i * span + gap; const end = -Math.PI / 2 + (i + 1) * span - gap;
      ctx.strokeStyle = "rgba(20, 30, 28, 0.48)"; ctx.beginPath(); ctx.arc(p.x, p.y, radius, start, end); ctx.stroke();
      const fill = clamp(ratio * slots - i, 0, 1);
      if (fill > 0) { ctx.strokeStyle = color; ctx.beginPath(); ctx.arc(p.x, p.y, radius, start, start + (end - start) * fill); ctx.stroke(); }
    }
    ctx.restore();
  }

  function drawCricket(b) {
    const p = worldToScreen(b.x, b.z, b.y);
    const chargeN = b.charging
      ? clamp(b.chargeT / Math.max(1e-6, chargeTMax(b)), 0, 1)
      : (b.v3Preview >= 0.12 ? b.v3Preview * 0.45 : 0);
    const full = b.charging && b.chargeT >= chargeTMax(b) - 1e-4;
    const jumping = b.airborne && b.y > 0.01;
    const sliding = isMovingOnGround(b);
    const roll = b.roll || 0;
    const hitPose = b.hitTier;
    const buzzing = !jumping && !b.charging && (hitPose === "slip" || roll > 0.35);
    const hit = b.fxHit;
    const idle = !b.charging && !jumping && !sliding && !hitPose && b.alive;
    const squat = (b.charging || b.v3Preview >= 0.12) ? 1 - 0.32 * chargeN : 1;
    let stretch = jumping ? 1.1 + Math.min(0.12, b.y * 0.08) : squat;
    const shake = full ? Math.sin(time * 48) * (1.2 + chargeN)
      : hitPose === "slip" ? Math.sin(time * 110) * (1.4 + hit * 2.2) + Math.sin(time * 23) * 1.1
      : hitPose === "ctrl" && hit > 0 ? 0
      : (hit > 0 ? Math.sin(time * 62) * hit * 2.2 : 0);
    const screenAng = Math.atan2(-b.dirZ, b.dirX) + b.spin * (hitPose === "slip" ? 0.24 : 0.1);
    const pal = b.pal || BABY_PAL;
    const fade = b.alive ? 1 : Math.max(0, 1 - b.outT * 1.4);
    const tId = time * 5.2 + b.id * 1.7;
    const breath = idle ? 1 + Math.sin(tId * 0.5) * 0.025 : 1;
    const hitPx = b.r * CAM_SCALE;
    drawStaminaRing(b, p, hitPx);

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
    if (b.kind === "baby" && b.alive) {
      const lifeN = clamp(b.lifeT / Math.max(1e-6, knobs.babyLifeT), 0, 1);
      drawTimeCapsule(p.x, p.y - hitPx * 0.78 - 10, lifeN, mixRgb([232, 220, 196], [208, 56, 48], 1 - lifeN), false);
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
    const itemOrder = items.slice().sort((a, b) => b.z - a.z);
    for (const it of itemOrder) drawItem(it);
    const nestOrder = nests.slice().sort((a, b) => b.z - a.z);
    for (const n of nestOrder) drawNest(n);
    const eggOrder = eggs.slice().sort((a, b) => b.z - a.z);
    for (const e of eggOrder) drawEgg(e);
    const order = bugs.concat(babies).sort((a, b) => b.z - a.z);
    for (const b of order) drawShadow(b);
    for (const b of order) drawArc(b);
    drawParticles();
    for (const b of order) drawCricket(b);
    for (const b of order) drawBugShield(b);
    drawVignette();
  }

  function stickSize() {
    return inputVersion === 3 ? STICK_FIXED : STICK_SIZE;
  }

  function stickVisual() {
    return inputVersion === 3 ? STICK_FIXED_VISUAL : STICK_VISUAL;
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
    stickKnob.style.transform = `translate(${x * stickVisual()}px, ${y * stickVisual()}px)`;
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
    if (el.closest("button, input, textarea")) return true;
    const ov = el.closest(".overlay");
    if (ov && !ov.classList.contains("hidden")) return true;
    return false;
  }

  function layoutFixedStick() {
    const rect = phoneEl.getBoundingClientRect();
    const pad = stickSize() * 0.5 + 6;
    stick.originX = rect.width * 0.5;
    stick.originY = clamp(rect.height - pad - 8, pad, Math.max(pad, rect.height - pad));
    stickEl.style.left = stick.originX + "px";
    stickEl.style.top = stick.originY + "px";
  }

  function pointerOnDisc(e) {
    const rect = phoneEl.getBoundingClientRect();
    const cx = rect.left + stick.originX;
    const cy = rect.top + stick.originY;
    return hypot(e.clientX - cx, e.clientY - cy) <= stickSize() * 0.5 + 16;
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

  function refreshStickMode() {
    stickEl.classList.toggle("fixed", inputVersion === 3);
    phoneEl.classList.toggle("input-v3", inputVersion === 3);
    const hint = document.getElementById("stick-hint");
    if (hint) hint.textContent = STICK_HINTS[inputVersion] || STICK_HINTS[1];
    const showFixed = inputVersion === 3 && phase === "play" && !paused && !spectating && !awaitingQuit;
    if (showFixed) {
      layoutFixedStick();
      stickEl.classList.remove("hidden");
    } else if (inputVersion !== 3 && !stick.active && stick.pointerId == null) {
      stickEl.classList.add("hidden");
    }
  }

  function setStickFromEvent(e) {
    const rect = phoneEl.getBoundingClientRect();
    const radius = stickSize() * 0.5;
    const cx = rect.left + stick.originX;
    const cy = rect.top + stick.originY;
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
    if (inputVersion === 3 && phase === "play" && !paused && !spectating && !awaitingQuit) {
      layoutFixedStick();
      stickEl.classList.remove("hidden");
    } else {
      stickEl.classList.add("hidden");
    }
    updateStickVisual();
  }

  phoneEl.addEventListener("pointerdown", (e) => {
    if (e.pointerType === "mouse" && e.button !== 0) return;
    if (stick.pointerId != null) return;
    if (isStickBlocked(e.target)) return;
    if (!canSummonStick()) return;
    const rect = phoneEl.getBoundingClientRect();
    if (inputVersion === 3) {
      layoutFixedStick();
      if (!pointerOnDisc(e)) return;
      stickEl.classList.remove("hidden");
    } else {
      showStickAt(e.clientX - rect.left, e.clientY - rect.top);
    }
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
    if (e.type === "pointerup") commitV3FromStick();
    clearStick();
  }
  phoneEl.addEventListener("pointerup", onPointerEnd);
  phoneEl.addEventListener("pointercancel", onPointerEnd);
  window.addEventListener("pointerup", onPointerEnd);
  window.addEventListener("pointercancel", onPointerEnd);

  function stealPlayFocus() {
    const ae = document.activeElement;
    if (ae && ae !== document.body && ae !== canvas && ae.blur) ae.blur();
    if (!canvas) return;
    if (canvas.tabIndex < 0) canvas.tabIndex = 0;
    try { canvas.focus({ preventScroll: true }); }
    catch (_) { try { canvas.focus(); } catch (__) { /* ignore */ } }
  }

  function isNestTab(e) {
    return (e.code === "Tab" || e.key === "Tab") && isNestTest() && (phase === "play" || phase === "countdown");
  }

  window.addEventListener("keydown", (e) => {
    if (isNestTab(e)) {
      e.preventDefault();
      e.stopPropagation();
      if (e.stopImmediatePropagation) e.stopImmediatePropagation();
      if (!e.repeat) switchNestControl();
      return;
    }
    keys.add(e.code);
    if (inputVersion === 3) {
      const inp = playerInput();
      if (inp.mag >= 0.12) {
        lastAim.x = inp.x;
        lastAim.z = inp.z;
      }
    }
    if (e.code === "Space") {
      e.preventDefault();
      if (isNetPvp()) return;
      userPaused = !userPaused;
      syncPause();
      if (paused) clearStick();
      flash(paused ? "暂停" : "继续");
    }
    if (e.code === "KeyR") restart();
    if (/Arrow|Space/.test(e.code)) e.preventDefault();
    updateStickVisual();
  }, true);
  window.addEventListener("keyup", (e) => {
    if (isNestTab(e)) {
      e.preventDefault();
      e.stopPropagation();
      if (e.stopImmediatePropagation) e.stopImmediatePropagation();
      return;
    }
    const wasHeld = keyHeld();
    keys.delete(e.code);
    if (inputVersion === 3 && wasHeld && !keyHeld() && stick.pointerId == null) {
      if (netRole === "guest") {
        netSendInput({ v3: { mag: 1, dirX: lastAim.x, dirZ: lastAim.z } });
      } else {
        startV3Windup(playerBug(), 1, lastAim.x, lastAim.z);
      }
    }
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
    refreshStickMode();
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

  function syncInputUi() {
    document.querySelectorAll(".input-modes button").forEach((btn) => {
      btn.classList.toggle("active", Number(btn.dataset.input) === inputVersion);
    });
    const hint = document.getElementById("input-hint");
    if (hint) hint.textContent = INPUT_HINTS[inputVersion] || INPUT_HINTS[1];
    refreshStickMode();
  }

  function setInputVersion(n, opts) {
    n = Number(n);
    if (n !== 1 && n !== 2 && n !== 3) return;
    const prev = inputVersion;
    inputVersion = n;
    syncInputUi();
    if (opts && opts.silent) return;
    if (prev !== n) {
      for (const b of bugs) {
        if (isHumanControlled(b)) {
          b.charging = false;
          b.chargeT = 0;
          b.pendingCharge = false;
          clearV3Lock(b);
        }
      }
      clearStick();
    }
    if (!opts || opts.persist !== false) saveSettings();
  }

  function netUrl() {
    if (location.protocol === "file:") return null;
    const proto = location.protocol === "https:" ? "wss:" : "ws:";
    return proto + "//" + location.host + "/__ws";
  }

  function netSend(obj) {
    if (!netWs || netWs.readyState !== 1) return;
    try { netWs.send(JSON.stringify(obj)); } catch (_) { /* closed */ }
  }

  function hideNetOverlays() {
    if (overlayNetWait) overlayNetWait.classList.add("hidden");
    if (overlayNetJoin) overlayNetJoin.classList.add("hidden");
  }

  function setNetWaitStatus(text) {
    const el = document.getElementById("net-wait-status");
    if (el) el.textContent = text;
  }

  function setJoinStatus(text) {
    const el = document.getElementById("join-status");
    if (el) el.textContent = text;
  }

  function packBug(b) {
    return {
      id: b.id,
      name: b.name,
      pal: b.pal,
      x: b.x, z: b.z, y: b.y,
      vx: b.vx, vz: b.vz, vy: b.vy,
      dirX: b.dirX, dirZ: b.dirZ,
      r: b.r, m: b.m, grow: b.grow,
      alive: b.alive,
      stamina: b.stamina,
      airborne: b.airborne,
      charging: b.charging,
      chargeT: b.chargeT,
      holding: b.holding,
      pendingCharge: b.pendingCharge,
      aimMag: b.aimMag,
      v3Goal: b.v3Goal,
      v3Preview: b.v3Preview,
      v3Pending: b.v3Pending,
      inX: b.inX, inZ: b.inZ,
      buffSizeT: b.buffSizeT,
      buffShieldT: b.buffShieldT,
      buffChargeT: b.buffChargeT,
      rageSize: b.rageSize,
      rageCharge: b.rageCharge,
      squatFlash: b.squatFlash,
      fxHit: b.fxHit,
      fxSquash: b.fxSquash,
      hitTier: b.hitTier,
      roll: b.roll,
      tumble: b.tumble,
      spin: b.spin,
      kind: b.kind || "bug",
      ownerId: b.ownerId,
      lifeT: b.lifeT,
      atkCd: b.atkCd,
    };
  }

  function netSendState() {
    if (netRole !== "host") return;
    netSend({
      type: "state",
      phase,
      countT,
      matchT,
      paused,
      inputVersion,
      hudLive: hudLive ? hudLive.textContent : "",
      hudClock: hudClock ? hudClock.textContent : "",
      rage: !!(phoneEl && phoneEl.classList.contains("rage")),
      resultTitle: resultTitle ? resultTitle.textContent : "",
      resultSub: resultSub ? resultSub.textContent : "",
      bugs: bugs.map(packBug),
      hearts: hearts.map((h) => ({ x: h.x, z: h.z, r: h.r, alive: h.alive, phase: h.phase })),
      items: items.map((it) => ({ kind: it.kind, x: it.x, z: it.z, r: it.r, alive: it.alive, phase: it.phase })),
      nests: nests.map((n) => ({ x: n.x, z: n.z, r: n.r, hp: n.hp, maxHp: n.maxHp, alive: n.alive })),
      eggs: eggs.map((e) => ({
        x: e.x, z: e.z, vx: e.vx, vz: e.vz, r: e.r, m: e.m,
        alive: e.alive, hatchT: e.hatchT, hatchMax: e.hatchMax, ownerId: e.ownerId,
      })),
      babies: babies.map(packBug),
    });
  }

  function netSendInput(extra) {
    const inp = playerInput();
    const msg = {
      type: "input",
      holding: inp.holding,
      x: inp.x,
      z: inp.z,
      mag: inp.mag,
      ver: inputVersion,
    };
    if (extra && extra.v3) msg.v3 = extra.v3;
    netSend(msg);
  }

  function lerpNum(a, b, u) {
    if (!Number.isFinite(a)) return b;
    if (!Number.isFinite(b)) return a;
    return a + (b - a) * u;
  }

  function fillFromSnap(dst, src, prev, u, lerpPos) {
    Object.assign(dst, src);
    if (lerpPos && prev) {
      dst.x = lerpNum(prev.x, src.x, u);
      dst.z = lerpNum(prev.z, src.z, u);
      dst.y = lerpNum(prev.y, src.y, u);
    }
  }

  function syncSnapList(arr, snaps, prevs, u, make, lerpPos) {
    const list = snaps || [];
    const prevMap = new Map();
    if (prevs) {
      for (const p of prevs) {
        const key = p.id != null ? p.id : (p.x + "," + p.z);
        prevMap.set(key, p);
      }
    }
    while (arr.length < list.length) arr.push(make(arr.length));
    arr.length = list.length;
    for (let i = 0; i < list.length; i++) {
      const src = list[i];
      const key = src.id != null ? src.id : (src.x + "," + src.z);
      fillFromSnap(arr[i], src, prevMap.get(key), u, lerpPos);
    }
  }

  function applyNetOverlays(snap) {
    if (!snap) return;
    if (hudLive && snap.hudLive != null) hudLive.textContent = snap.hudLive;
    if (hudClock && snap.hudClock != null) hudClock.textContent = snap.hudClock;
    if (phoneEl) phoneEl.classList.toggle("rage", !!snap.rage);
    if (snap.phase === "countdown") {
      overlayStart.classList.add("hidden");
      hideNetOverlays();
      overlayResult.classList.add("hidden");
      overlayCount.classList.remove("hidden");
      const n = Math.ceil(snap.countT);
      if (countNum) countNum.textContent = n > 0 ? String(n) : "斗";
    } else if (snap.phase === "play") {
      overlayStart.classList.add("hidden");
      hideNetOverlays();
      overlayCount.classList.add("hidden");
      overlayResult.classList.add("hidden");
    } else if (snap.phase === "result") {
      overlayCount.classList.add("hidden");
      const firstResult = overlayResult.classList.contains("hidden");
      overlayResult.classList.remove("hidden");
      if (resultTitle) resultTitle.textContent = snap.resultTitle || "这一罐";
      if (resultSub) resultSub.textContent = snap.resultSub || "";
      if (firstResult) openTune();
    }
  }

  function applySnap(snap, prev) {
    if (!snap) return;
    const u = prev ? clamp((performance.now() - snapAt) / 50, 0, 1) : 1;
    const localHold = playerInput();
    phase = snap.phase;
    countT = snap.countT;
    matchT = snap.matchT;
    if (typeof snap.inputVersion === "number") setInputVersion(snap.inputVersion, { silent: true, persist: false });
    syncSnapList(bugs, snap.bugs, prev && prev.bugs, u, (i) => makeBug(i, 0, 0, i === mySeat, null), true);
    for (const b of bugs) {
      b.isPlayer = b.id === mySeat;
      b.remote = false;
      b.ai = null;
      refreshBody(b);
    }
    syncSnapList(hearts, snap.hearts, prev && prev.hearts, u, () => ({ x: 0, z: 0, r: HEART_R, alive: false, phase: 0 }), true);
    syncSnapList(items, snap.items, prev && prev.items, u, () => ({ kind: "shield", x: 0, z: 0, r: 0.7, alive: false, phase: 0 }), true);
    syncSnapList(nests, snap.nests, prev && prev.nests, u, () => ({ x: 0, z: 0, r: 1, hp: 0, maxHp: 1, alive: false, touching: {} }), true);
    syncSnapList(eggs, snap.eggs, prev && prev.eggs, u, () => ({ x: 0, z: 0, vx: 0, vz: 0, r: 0.5, m: 0.4, alive: false, hatchT: 1, hatchMax: 1, ownerId: -1 }), true);
    syncSnapList(babies, snap.babies, prev && prev.babies, u, (i) => {
      const b = makeBug(100 + i, 0, 0, false, null);
      b.kind = "baby";
      b.ownerId = -1;
      return b;
    }, true);
    for (const b of babies) {
      b.isPlayer = false;
      b.kind = "baby";
      if (!b.pal || !b.pal.body) {
        const owner = bugs.find((p) => p.id === b.ownerId);
        b.pal = (owner && owner.pal) || BABY_PAL;
      }
    }
    const me = playerBug();
    if (me && phase === "play" && inputVersion !== 3) {
      me.holding = localHold.holding;
      if (localHold.holding && hypot(localHold.x, localHold.z) > 0.01) {
        me.dirX = localHold.x;
        me.dirZ = localHold.z;
        me.inX = localHold.x;
        me.inZ = localHold.z;
      }
    }
    applyNetOverlays(snap);
  }

  function pumpGuest(dt) {
    netInputAcc += dt;
    if (netInputAcc >= 0.05) {
      netInputAcc = 0;
      netSendInput();
    }
    if (lastSnap) applySnap(lastSnap, prevSnap);
    if (bannerT > 0) {
      bannerT -= dt;
      if (bannerT <= 0 && banner) banner.textContent = "";
    }
  }

  function onNetMessage(msg) {
    if (!msg || typeof msg !== "object") return;
    if (msg.type === "created") {
      netRole = "host";
      mySeat = 0;
      netCode = String(msg.code || "");
      const codeEl = document.getElementById("net-code");
      if (codeEl) codeEl.textContent = netCode;
      overlayStart.classList.add("hidden");
      if (overlayNetJoin) overlayNetJoin.classList.add("hidden");
      if (overlayNetWait) overlayNetWait.classList.remove("hidden");
      setNetWaitStatus("等待对手加入…");
      return;
    }
    if (msg.type === "joined") {
      netRole = "guest";
      mySeat = 1;
      netCode = String(msg.code || "");
      setJoinStatus("已加入，等房主开罐…");
      return;
    }
    if (msg.type === "peer") {
      netPeerOk = true;
      setNetWaitStatus("对手已到，开罐");
      solo = false;
      beginMatch();
      netSend({
        type: "start",
        seat: 1,
        inputVersion,
        knobs: snapshotSettings(),
      });
      netSendState();
      return;
    }
    if (msg.type === "start") {
      if (msg.knobs) applySettings(msg.knobs, { persist: false });
      if (msg.inputVersion) setInputVersion(msg.inputVersion, { silent: true, persist: false });
      mySeat = 1;
      netRole = "guest";
      netPeerOk = true;
      solo = false;
      hideNetOverlays();
      beginMatch();
      flash("联机对打");
      return;
    }
    if (msg.type === "state") {
      prevSnap = lastSnap;
      lastSnap = msg;
      snapAt = performance.now();
      applySnap(lastSnap, prevSnap);
      return;
    }
    if (msg.type === "input") {
      remoteInput = {
        holding: !!msg.holding,
        x: Number(msg.x) || 0,
        z: Number(msg.z) || 0,
        mag: Number(msg.mag) || 0,
      };
      if (msg.v3) {
        remoteV3 = {
          mag: Number(msg.v3.mag) || 0,
          dirX: Number(msg.v3.dirX) || 0,
          dirZ: Number(msg.v3.dirZ) || 0,
        };
      }
      return;
    }
    if (msg.type === "peer-left") {
      netPeerOk = false;
      flash("对手离开");
      if (phase === "play" || phase === "countdown") {
        finishMatch("对手离开", "这一罐不算。");
      } else if (overlayNetWait && !overlayNetWait.classList.contains("hidden")) {
        setNetWaitStatus("对手离开了，继续等待或取消");
      }
      return;
    }
    if (msg.type === "error") {
      const text = msg.message === "full" ? "房间已满" : msg.message === "no room" ? "没有这个房号" : (msg.message || "联机失败");
      flash(text);
      setJoinStatus(text);
      if (netRole === "guest" && !netPeerOk) {
        /* stay on join */
      }
      return;
    }
  }

  function netDisconnect() {
    const ws = netWs;
    netWs = null;
    netPeerOk = false;
    netRole = null;
    mySeat = 0;
    netCode = "";
    lastSnap = null;
    prevSnap = null;
    remoteV3 = null;
    remoteInput = { holding: false, x: 0, z: 0, mag: 0 };
    if (ws) {
      try { ws.onclose = null; ws.onerror = null; ws.onmessage = null; ws.close(); } catch (_) { /* ignore */ }
    }
  }

  function netConnect(onOpen) {
    const url = netUrl();
    if (!url) {
      flash("联机请先运行 node save-server.js");
      return;
    }
    netDisconnect();
    let ws;
    try { ws = new WebSocket(url); }
    catch (err) {
      flash("联机失败");
      return;
    }
    netWs = ws;
    ws.onopen = () => { if (netWs === ws && onOpen) onOpen(); };
    ws.onmessage = (ev) => {
      try { onNetMessage(JSON.parse(String(ev.data || ""))); } catch (_) { /* ignore */ }
    };
    ws.onerror = () => {
      if (netWs === ws) flash("联机通道出错");
    };
    ws.onclose = () => {
      if (netWs !== ws) return;
      netWs = null;
      const was = netRole;
      netPeerOk = false;
      if (was && (phase === "play" || phase === "countdown")) {
        finishMatch("连接断开", "同一 Wi-Fi，用同一条地址。");
      } else if (was) {
        flash("连接断开");
      }
      netRole = was === "host" || was === "guest" ? was : null;
      if (phase !== "play" && phase !== "countdown" && phase !== "result") {
        netRole = null;
        mySeat = 0;
      }
    };
  }

  function beginHostPvp() {
    if (isNestTest()) return;
    netConnect(() => netSend({ type: "create" }));
  }

  function beginJoinPvp(code) {
    const digits = String(code || "").replace(/\D/g, "").slice(0, 4);
    if (digits.length !== 4) {
      setJoinStatus("请输入 4 位房号");
      return;
    }
    netConnect(() => netSend({ type: "join", code: digits }));
  }

  function cancelNet() {
    netDisconnect();
    hideNetOverlays();
    overlayStart.classList.remove("hidden");
    setJoinStatus("");
    phase = "ready";
  }

  function beginMatch() {
    overlayStart.classList.add("hidden");
    overlayResult.classList.add("hidden");
    overlaySpectate.classList.add("hidden");
    overlayCount.classList.add("hidden");
    hideNetOverlays();
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
      refreshStickMode();
      return;
    }
    if (isNestTest()) {
      phase = "play";
      countT = 0;
      flash("中间有房子 · 对面不会跳");
      syncPause();
      refreshStickMode();
      stealPlayFocus();
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
    if (netRole === "guest") {
      flash("等待房主再开");
      return;
    }
    if (netRole === "host") {
      if (!netPeerOk) {
        flash("对手已离开");
        return;
      }
      beginMatch();
      netSend({
        type: "start",
        seat: 1,
        inputVersion,
        knobs: snapshotSettings(),
      });
      netSendState();
      return;
    }
    beginMatch();
  }

  document.getElementById("btn-start").onclick = () => {
    netDisconnect();
    solo = false;
    beginMatch();
  };
  document.getElementById("btn-solo").onclick = () => {
    netDisconnect();
    solo = true;
    beginMatch();
  };
  const btnHost = document.getElementById("btn-host");
  if (btnHost) btnHost.onclick = () => beginHostPvp();
  const btnJoin = document.getElementById("btn-join");
  if (btnJoin) {
    btnJoin.onclick = () => {
      overlayStart.classList.add("hidden");
      if (overlayNetJoin) overlayNetJoin.classList.remove("hidden");
      setJoinStatus("");
      const inp = document.getElementById("join-code");
      if (inp) {
        inp.value = "";
        inp.focus();
      }
    };
  }
  const btnJoinGo = document.getElementById("btn-join-go");
  if (btnJoinGo) {
    btnJoinGo.onclick = () => {
      const inp = document.getElementById("join-code");
      beginJoinPvp(inp ? inp.value : "");
    };
  }
  const joinCodeEl = document.getElementById("join-code");
  if (joinCodeEl) {
    joinCodeEl.addEventListener("keydown", (e) => {
      if (e.code === "Enter" || e.key === "Enter") {
        e.preventDefault();
        beginJoinPvp(joinCodeEl.value);
      }
    });
  }
  const btnJoinCancel = document.getElementById("btn-join-cancel");
  if (btnJoinCancel) btnJoinCancel.onclick = () => cancelNet();
  const btnNetCancel = document.getElementById("btn-net-cancel");
  if (btnNetCancel) btnNetCancel.onclick = () => cancelNet();
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
    ["staminaMax", "k-staminaMax", "v-staminaMax", 0],
    ["staminaCost", "k-staminaCost", "v-staminaCost", 0],
    ["staminaRegen", "k-staminaRegen", "v-staminaRegen", 2],
    ["staminaSlots", "k-staminaSlots", "v-staminaSlots", 0],
    ["dMin", "k-dMin", "v-dMin", 2],
    ["vRate", "k-vRate", "v-vRate", 2],
    ["theta", "k-theta", "v-theta", 0],
    ["m", "k-m", "v-m", 2],
    ["g", "k-g", "v-g", 2],
    ["mu", "k-mu", "v-mu", 2],
    ["rStand", "k-rStand", "v-rStand", 2],
    ["rMax", "k-rMax", "v-rMax", 2],
    ["rChargeScale", "k-rChargeScale", "v-rChargeScale", 2],
    ["muCtrlScale", "k-muCtrlScale", "v-muCtrlScale", 2],
    ["muSlipScale", "k-muSlipScale", "v-muSlipScale", 2],
    ["growPer", "k-growPer", "v-growPer", 2],
    ["sizeScale", "k-sizeScale", "v-sizeScale", 2],
    ["chargeScale", "k-chargeScale", "v-chargeScale", 2],
    ["regTime", "k-regTime", "v-regTime", 0],
    ["otTime", "k-otTime", "v-otTime", 0],
    ["nestHP", "k-nestHP", "v-nestHP", 0],
    ["nestEggN", "k-nestEggN", "v-nestEggN", 0],
    ["eggHatchT", "k-eggHatchT", "v-eggHatchT", 1],
    ["eggHatchGap", "k-eggHatchGap", "v-eggHatchGap", 2],
    ["babyLifeT", "k-babyLifeT", "v-babyLifeT", 1],
    ["babyMass", "k-babyMass", "v-babyMass", 2],
    ["babyA1Scale", "k-babyA1Scale", "v-babyA1Scale", 2],
    ["babyChargeT", "k-babyChargeT", "v-babyChargeT", 2],
    ["babyAtkCd", "k-babyAtkCd", "v-babyAtkCd", 2],
  ];

  function snapshotSettings() {
    return {
      name: "dou-ququ-knobs",
      version: 3,
      knobs: { ...knobs },
      inputVersion,
      slowmo,
      showVel,
    };
  }

  function shippedFileText() {
    return "window.DOU_QUQU_SHIPPED = " + JSON.stringify(snapshotSettings(), null, 2) + ";\n";
  }

  const persistStatusEl = document.getElementById("persist-status");
  const FILE_FP_KEY = SETTINGS_KEY + "-file";
  let shippedHandle = null;
  let filePersistOk = false;
  let persistDirty = false;
  let persistTimer = 0;

  function settingsFingerprint(data) {
    const src = data && data.knobs && typeof data.knobs === "object" ? data.knobs : data || {};
    return Object.keys(FACTORY).map((k) => k + ":" + Number(src[k])).join("|");
  }

  function rememberWrittenFile(data) {
    try { localStorage.setItem(FILE_FP_KEY, settingsFingerprint(data)); }
    catch (_) { /* private mode */ }
  }

  function knobsMatchShipped() {
    const src = (globalThis.DOU_QUQU_SHIPPED && globalThis.DOU_QUQU_SHIPPED.knobs) || DEFAULTS;
    for (const key of Object.keys(FACTORY)) {
      if (Number(knobs[key]) !== Number(src[key])) return false;
    }
    return true;
  }

  function updatePersistStatus() {
    if (!persistStatusEl) return;
    if (filePersistOk && !persistDirty) {
      persistStatusEl.textContent = "已保存到 defaults.js · 别人打开这份文件就是这组数。";
    } else if (filePersistOk && persistDirty) {
      persistStatusEl.textContent = "有改动尚未保存。点右上角「保存」。";
    } else if (knobsMatchShipped()) {
      persistStatusEl.textContent = "当前就是文件里的数。再改请点「保存」。";
    } else {
      persistStatusEl.textContent = "只存在这台电脑。点右上角「保存」会直接覆盖 defaults.js。若没写上，先运行 node save-server.js。";
    }
  }

  function applySettings(data, opts) {
    if (!data || typeof data !== "object") return false;
    const src = data.knobs && typeof data.knobs === "object" ? data.knobs : data;
    for (const key of Object.keys(FACTORY)) {
      if (typeof src[key] === "number" && Number.isFinite(src[key])) knobs[key] = src[key];
    }
    if (data.inputVersion === 1 || data.inputVersion === 2 || data.inputVersion === 3) {
      inputVersion = data.inputVersion;
    }
    if (typeof data.slowmo === "boolean") slowmo = data.slowmo;
    if (typeof data.showVel === "boolean") showVel = data.showVel;
    if (typeof src.babyCanLoot === "number" && Number.isFinite(src.babyCanLoot)) knobs.babyCanLoot = src.babyCanLoot;
    document.getElementById("k-slow").checked = slowmo;
    document.getElementById("k-vel").checked = showVel;
    const lootEl = document.getElementById("k-babyCanLoot");
    if (lootEl) lootEl.checked = knobs.babyCanLoot > 0;
    syncKnobsToUi();
    syncInputUi();
    if (!opts || opts.persist !== false) saveSettings();
    else updatePersistStatus();
    return true;
  }

  function downloadText(name, text, type) {
    const blob = new Blob([text], { type: type || "text/plain" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = name;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  }

  async function writeShippedHandle() {
    if (!shippedHandle) return false;
    const writable = await shippedHandle.createWritable();
    await writable.write(shippedFileText());
    await writable.close();
    globalThis.DOU_QUQU_SHIPPED = snapshotSettings();
    Object.assign(DEFAULTS, knobs);
    rememberWrittenFile(globalThis.DOU_QUQU_SHIPPED);
    filePersistOk = true;
    persistDirty = false;
    return true;
  }

  function scheduleFileWrite() {
    persistDirty = true;
    if (!shippedHandle) {
      updatePersistStatus();
      return;
    }
    window.clearTimeout(persistTimer);
    persistTimer = window.setTimeout(() => {
      writeShippedHandle().then(updatePersistStatus).catch(() => {
        filePersistOk = false;
        updatePersistStatus();
      });
    }, 400);
  }

  function saveSettings() {
    try { localStorage.setItem(SETTINGS_KEY, JSON.stringify(snapshotSettings())); }
    catch (_) { /* private mode */ }
    scheduleFileWrite();
  }

  function loadSettings() {
    const shippedData = globalThis.DOU_QUQU_SHIPPED || { knobs: DEFAULTS, slowmo: false, showVel: false };
    applySettings(shippedData, { persist: false });
    const fileFp = settingsFingerprint(shippedData);
    let remembered = "";
    try { remembered = localStorage.getItem(FILE_FP_KEY) || ""; } catch (_) { /* private mode */ }
    try {
      const raw = localStorage.getItem(SETTINGS_KEY);
      if (raw && remembered === fileFp) applySettings(JSON.parse(raw), { persist: false });
    } catch (_) { /* ignore bad cache */ }
    persistDirty = !knobsMatchShipped();
    updatePersistStatus();
  }

  function markSaved() {
    globalThis.DOU_QUQU_SHIPPED = snapshotSettings();
    Object.assign(DEFAULTS, knobs);
    rememberWrittenFile(globalThis.DOU_QUQU_SHIPPED);
    filePersistOk = true;
    persistDirty = false;
    updatePersistStatus();
  }

  function saveEndpoints() {
    const urls = [];
    if (location.protocol !== "file:") urls.push(new URL("/__save", location.href).href);
    urls.push("http://127.0.0.1:8765/__save");
    return urls;
  }

  async function postShippedFile(text) {
    let denied = false;
    for (const url of saveEndpoints()) {
      try {
        const res = await fetch(url, {
          method: "POST",
          headers: { "Content-Type": "text/plain; charset=utf-8" },
          body: text,
        });
        if (res.ok) return { ok: true };
        if (res.status === 403) denied = true;
      } catch (_) { /* try next */ }
    }
    return { ok: false, denied };
  }

  async function saveToFile() {
    const text = shippedFileText();
    const result = await postShippedFile(text);
    if (result.ok) {
      markSaved();
      flash("已保存到 defaults.js");
      return;
    }
    filePersistOk = false;
    persistDirty = true;
    updatePersistStatus();
    if (result.denied) flash("保存只能在这台电脑上，请用本机地址打开");
    else flash("没写上 defaults.js。先在本目录运行 node save-server.js，再用它打开本页");
  }

  function exportSettings() {
    const text = JSON.stringify(snapshotSettings(), null, 2);
    downloadText("dou-ququ-settings.json", text, "application/json");
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
    Object.assign(knobs, FACTORY);
    slowmo = false;
    showVel = false;
    inputVersion = 1;
    document.getElementById("k-slow").checked = false;
    document.getElementById("k-vel").checked = false;
    const lootEl = document.getElementById("k-babyCanLoot");
    if (lootEl) lootEl.checked = knobs.babyCanLoot > 0;
    syncKnobsToUi();
    syncInputUi();
    saveSettings();
  };
  document.querySelectorAll(".input-modes button").forEach((btn) => {
    btn.addEventListener("click", () => setInputVersion(btn.dataset.input));
  });
  document.getElementById("btn-save").onclick = () => { saveToFile(); };
  document.getElementById("btn-save-file").onclick = () => { saveToFile(); };
  document.getElementById("k-slow").onchange = (e) => { slowmo = e.target.checked; saveSettings(); };
  document.getElementById("k-vel").onchange = (e) => { showVel = e.target.checked; saveSettings(); };
  document.getElementById("k-babyCanLoot").onchange = (e) => {
    knobs.babyCanLoot = e.target.checked ? 1 : 0;
    saveSettings();
  };
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
    try { render(); }
    catch (err) { console.warn("render skipped", err); }
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

  function isLocalHostName() {
    const h = location.hostname;
    return h === "127.0.0.1" || h === "localhost" || h === "[::1]" || h === "::1";
  }

  async function showLanHint() {
    const el = document.getElementById("lan-hint");
    if (!el || !isLocalHostName() || location.protocol === "file:") return;
    try {
      const res = await fetch(new URL("/__lan", location.href).href, { cache: "no-store" });
      if (!res.ok) return;
      const data = await res.json();
      const phones = ((data && data.urls) || []).filter((u) => u && u.label === "手机" && u.href);
      if (!phones.length) {
        el.textContent = "手机请与电脑同一 Wi-Fi。终端若没有「手机」地址，开热点或检查防火墙。";
        el.classList.remove("hidden");
        return;
      }
      el.textContent = "手机打开：" + phones.map((u) => u.href).join("  ");
      el.classList.remove("hidden");
    } catch (_) { /* server not this page */ }
  }

  function boot() {
    bakeSand();
    fitCanvas();
    loadSettings();
    spawnMatch();
    phase = "ready";
    showLanHint();
    syncKnobsToUi();
    if (isNestTest()) closeTune();
    else openTune();
    if (isNestTest()) {
      globalThis.__DQ_DEBUG__ = () => {
        const me = playerBug();
        return {
          phase,
          paused,
          userPaused,
          matchT,
          tuneOpen: isTuneOpen(),
          playerId: me ? me.id : null,
          playerName: me ? me.name : null,
          bugs: bugs.map((b) => ({
            id: b.id,
            name: b.name,
            isPlayer: !!b.isPlayer,
            hasAi: !!b.ai,
            aiIdle: !!(b.ai && b.ai.idle),
            charging: !!b.charging,
            holding: !!b.holding,
            finiteDir: Number.isFinite(b.dirX) && Number.isFinite(b.dirZ),
            x: b.x,
            z: b.z,
          })),
          nestHp: nests.filter((n) => n.alive).map((n) => n.hp),
          eggs: eggs.filter((e) => e.alive).length,
          babies: babies.filter((b) => b.alive).length,
        };
      };
    }
    updateStickVisual();
    window.addEventListener("resize", () => {
      fitCanvas();
      refreshStickMode();
    });
    document.addEventListener("touchmove", (e) => e.preventDefault(), { passive: false });
    requestAnimationFrame(loop);
  }

  boot();
})();
