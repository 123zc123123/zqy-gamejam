(() => {
  "use strict";

  const ITEM_KINDS = ["size", "shield", "charge"];

  function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }
  function hypot(x, z) { return Math.hypot(x, z); }

  function defaultItemKnobs() {
    return {
      regTime: 90,
      otTime: 30,
      sizeScale: 1.30,
      sizeT: 6,
      shieldT: 8,
      chargeScale: 1.25,
      chargeBuffT: 5,
      itemR: 1.35,
      itemTimes: [20, 42, 60, 74, 85, 94, 102, 110],
      heartStart: 4,
      heartCap: 6,
      heartBatch: 6,
      itemBatch: 3,
      itemCap: 3,
      heartGap: 7,
      heartGapOt: 5,
      heartOpenAt: 20,
      vRate: 23,
      tMax: 0.55,
      m: 1,
      growPer: 0.16,
      bugR: 1.8,
      itemMinEdge: 2.4,
      itemMinBug: 3.0,
      itemMinHeart: 1.6,
      itemMinItem: 2.0,
      itemRingMin: 6,
      itemRingMax: 12,
      heartMinEdge: 1.4,
      heartMinBug: 1.3,
      heartMinHeart: 1.25,
      shieldPad: 0.08,
    };
  }

  function mergeKnobs(knobs) {
    return Object.assign(defaultItemKnobs(), knobs || {});
  }

  function createMatchState(knobs) {
    return {
      knobs: mergeKnobs(knobs),
      t: 0,
      rage: false,
      nextItemIndex: 0,
      lastItemKind: null,
      lastHeartAt: -1e9,
    };
  }

  function hardStop(knobs) {
    return knobs.regTime + knobs.otTime;
  }

  function isRage(state) {
    return state.t >= state.knobs.regTime && state.t < hardStop(state.knobs);
  }

  function matchClock(state) {
    const reg = state.knobs.regTime;
    const end = hardStop(state.knobs);
    const t = state.t;
    let phase = "probe";
    if (t >= end) phase = "over";
    else if (t >= reg) phase = "rage";
    else if (t >= 70) phase = "close";
    else if (t >= state.knobs.heartOpenAt) phase = "open";
    return {
      phase,
      t,
      untilReg: Math.max(0, reg - t),
      untilHard: Math.max(0, end - t),
    };
  }

  function sizeActive(bug) {
    return !!(bug.rageSize || (bug.buffSizeT > 0));
  }

  function chargeActive(bug) {
    return !!(bug.rageCharge || (bug.buffChargeT > 0));
  }

  function shieldActive(bug) {
    return bug.buffShieldT > 0;
  }

  function effectiveCharge(knobs, bug) {
    const s = chargeActive(bug) ? knobs.chargeScale : 1;
    return {
      vRate: knobs.vRate * s,
      tMax: knobs.tMax / Math.max(1e-6, s),
      scale: s,
    };
  }

  function panelVMax(knobs) {
    return Math.max(0, knobs.vRate) * Math.max(0, knobs.tMax);
  }

  function chargeDeltaV(knobs, bug) {
    const e = effectiveCharge(knobs, bug);
    return e.vRate * clamp(bug.chargeT || 0, 0, e.tMax);
  }

  function refreshBody(knobs, bug) {
    const g = 1 + (bug.grow || 0) * Math.max(0, knobs.growPer);
    const s = sizeActive(bug) ? knobs.sizeScale : 1;
    const bugR = knobs.bugR == null ? 1.8 : knobs.bugR;
    bug.r = bugR * g * s;
    bug.m = Math.max(0.08, knobs.m) * g * s;
    return bug;
  }

  function pickItemKind(state, rand) {
    if (isRage(state)) {
      state.lastItemKind = "shield";
      return "shield";
    }
    const pool = ITEM_KINDS.filter((k) => k !== state.lastItemKind);
    const roll = rand ? rand() : Math.random();
    const kind = pool[Math.floor(roll * pool.length) % pool.length];
    state.lastItemKind = kind;
    return kind;
  }

  function itemFieldCap(state) {
    return Math.max(0, state.knobs.itemCap == null ? 3 : state.knobs.itemCap);
  }

  function dueItemSpawns(state, rand, fieldCount) {
    const times = state.knobs.itemTimes;
    const batch = Math.max(1, state.knobs.itemBatch || 1);
    const cap = itemFieldCap(state);
    const live = fieldCount == null ? 0 : fieldCount;
    const out = [];
    let placed = 0;
    while (state.nextItemIndex < times.length && state.t + 1e-9 >= times[state.nextItemIndex]) {
      const room = cap - live - placed;
      if (room <= 0) break;
      const n = Math.min(batch, room);
      for (let i = 0; i < n; i++) out.push(pickItemKind(state, rand));
      placed += n;
      state.nextItemIndex += 1;
    }
    return out;
  }

  function heartBurstFill(state) {
    return state.lastHeartAt < 0 && state.t + 1e-9 >= state.knobs.heartOpenAt;
  }

  function heartFieldCap(state) {
    return Math.max(0, state.knobs.heartCap == null ? 6 : state.knobs.heartCap);
  }

  function heartWaveSize(state, fieldCount) {
    const cap = heartFieldCap(state);
    const live = fieldCount == null ? 0 : fieldCount;
    return Math.max(0, cap - live) > 0 ? 1 : 0;
  }

  function shouldRefillHeart(state, fieldCount) {
    if (state.t + 1e-9 < state.knobs.heartOpenAt) return false;
    if (heartWaveSize(state, fieldCount) <= 0) return false;
    if (heartBurstFill(state)) return true;
    const gap = isRage(state) ? state.knobs.heartGapOt : state.knobs.heartGap;
    return state.t - state.lastHeartAt + 1e-9 >= gap;
  }

  function markHeartFilled(state) {
    state.lastHeartAt = state.t;
  }

  function placePoint(rand, blockers, spec) {
    const nTry = spec.tries || 80;
    for (let n = 0; n < nTry; n++) {
      const ang = rand() * Math.PI * 2;
      const u = rand();
      let x;
      let z;
      if (spec.ringMin != null && spec.ringMax != null) {
        const rad = spec.ringMin + u * (spec.ringMax - spec.ringMin);
        x = Math.cos(ang) * rad;
        z = Math.sin(ang) * rad;
      } else {
        x = (rand() * 2 - 1) * (spec.hw || 21.2);
        z = (rand() * 2 - 1) * (spec.hd || 31.8);
      }
      if (spec.sdf && spec.sdf(x, z) > -spec.minEdge) continue;
      let blocked = false;
      for (const b of blockers) {
        if (hypot(b.x - x, b.z - z) < b.min) { blocked = true; break; }
      }
      if (!blocked) return { x, z };
    }
    const ang = rand() * Math.PI * 2;
    const rad = spec.ringMin != null ? (spec.ringMin + spec.ringMax) * 0.5 : 8;
    return { x: Math.cos(ang) * rad, z: Math.sin(ang) * rad };
  }

  function applyItem(knobs, bug, kind) {
    if (kind === "size") {
      if (!bug.rageSize) bug.buffSizeT = knobs.sizeT;
      refreshBody(knobs, bug);
      return;
    }
    if (kind === "shield") {
      bug.buffShieldT = knobs.shieldT;
      return;
    }
    if (kind === "charge") {
      if (!bug.rageCharge) bug.buffChargeT = knobs.chargeBuffT;
    }
  }

  function tickBuffs(knobs, bug, dt) {
    let body = false;
    if (!bug.rageSize && bug.buffSizeT > 0) {
      bug.buffSizeT -= dt;
      if (bug.buffSizeT <= 0) {
        bug.buffSizeT = 0;
        body = true;
      }
    }
    if (bug.buffShieldT > 0) {
      bug.buffShieldT -= dt;
      if (bug.buffShieldT <= 0) bug.buffShieldT = 0;
    }
    if (!bug.rageCharge && bug.buffChargeT > 0) {
      bug.buffChargeT -= dt;
      if (bug.buffChargeT <= 0) bug.buffChargeT = 0;
    }
    if (body) refreshBody(knobs, bug);
  }

  function enterRage(state, bugs) {
    if (state.rage) return false;
    if (!isRage(state)) return false;
    state.rage = true;
    for (const bug of bugs) {
      if (!bug.alive) continue;
      bug.rageSize = true;
      bug.rageCharge = true;
      bug.buffSizeT = 0;
      bug.buffChargeT = 0;
      refreshBody(state.knobs, bug);
    }
    return true;
  }

  function tryShieldSave(knobs, bug, sdf, grad) {
    if (sdf(bug.x, bug.z) <= 0) return false;
    if (!shieldActive(bug)) return false;
    bug.buffShieldT = 0;
    const pad = (bug.r || 0) + (knobs.shieldPad == null ? 0.08 : knobs.shieldPad);
    for (let i = 0; i < 48; i++) {
      const s = sdf(bug.x, bug.z);
      if (s <= -pad) break;
      const g = grad(bug.x, bug.z);
      const len = hypot(g.x, g.z) || 1;
      const step = Math.max(0.04, s + pad);
      bug.x -= (g.x / len) * step;
      bug.z -= (g.z / len) * step;
    }
    return true;
  }

  function centerWinner(bugs) {
    const alive = bugs.filter((b) => b.alive);
    if (alive.length === 0) return { tie: true, winner: null };
    const scored = alive.map((b) => ({
      bug: b,
      d: hypot(b.x || 0, b.z || 0),
      grow: b.grow || 0,
    }));
    scored.sort((a, b) => a.d - b.d || b.grow - a.grow);
    if (scored.length >= 2) {
      const a = scored[0];
      const b = scored[1];
      if (Math.abs(a.d - b.d) <= 1e-6 && a.grow === b.grow) {
        return { tie: true, winner: null };
      }
    }
    return { tie: false, winner: scored[0].bug };
  }

  const api = {
    ITEM_KINDS,
    defaultItemKnobs,
    mergeKnobs,
    createMatchState,
    hardStop,
    isRage,
    matchClock,
    sizeActive,
    chargeActive,
    shieldActive,
    effectiveCharge,
    panelVMax,
    chargeDeltaV,
    refreshBody,
    pickItemKind,
    itemFieldCap,
    dueItemSpawns,
    heartBurstFill,
    heartFieldCap,
    heartWaveSize,
    shouldRefillHeart,
    markHeartFilled,
    placePoint,
    applyItem,
    tickBuffs,
    enterRage,
    tryShieldSave,
    centerWinner,
  };

  if (typeof module !== "undefined" && module.exports) module.exports = api;
  if (typeof globalThis !== "undefined") globalThis.DouQuquRules = api;
})();
