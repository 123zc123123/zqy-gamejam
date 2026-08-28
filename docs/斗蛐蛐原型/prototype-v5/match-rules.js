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
      staminaMax: 100,
      staminaCost: 20,
      staminaRegen: 12,
      staminaSlots: 5,
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
      nestHP: 4,
      nestMass: 3.0,
      nestR: 1.2,
      nestEggN: 5,
      eggHatchT: 3,
      eggHatchGap: 0.28,
      eggHatchJitter: 0.15,
      eggScatterV: 4.5,
      eggR: 0.28,
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
      lastNestClearAt: -1,
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

  function babyCanLoot(knobs) {
    return !!(knobs && knobs.babyCanLoot);
  }

  function refreshBabyBody(knobs, baby) {
    const bugR = knobs.bugR == null ? 1.8 : knobs.bugR;
    const scale = knobs.babyRScale == null ? 0.4 : knobs.babyRScale;
    const grow = 1 + (baby.grow || 0) * Math.max(0, knobs.growPer);
    baby.r = bugR * scale * grow;
    baby.m = Math.max(0.05, knobs.babyMass == null ? 0.35 : knobs.babyMass) * grow;
    return baby;
  }

  function babyChargeStats(knobs) {
    const a1 = Math.max(0, knobs.vRate) * Math.max(0, knobs.babyA1Scale == null ? 0.4 : knobs.babyA1Scale);
    const tMax = Math.max(0.02, knobs.babyChargeT == null ? 0.16 : knobs.babyChargeT);
    return { vRate: a1, tMax };
  }

  function babyChargeDeltaV(knobs, baby) {
    const e = babyChargeStats(knobs);
    return e.vRate * clamp(baby.chargeT || 0, 0, e.tMax);
  }

  function babyAttackCd(knobs) {
    return Math.max(0, knobs.babyAtkCd == null ? 0.8 : knobs.babyAtkCd);
  }

  function tickBabyAtkCd(baby, dt) {
    if (!baby) return 0;
    baby.atkCd = Math.max(0, (baby.atkCd || 0) - dt);
    return baby.atkCd;
  }

  function canBabyCharge(baby) {
    return !baby || !(baby.atkCd > 1e-6);
  }

  function hitCreditId(hitter) {
    if (!hitter) return -1;
    if (hitter.kind === "baby") {
      return hitter.ownerId == null ? -1 : hitter.ownerId;
    }
    return hitter.id;
  }

  function nestFieldCap(state) {
    return Math.max(1, state.knobs.nestCap == null ? 1 : state.knobs.nestCap);
  }

  function shouldSpawnNest(state, liveCount) {
    if (isRage(state)) return false;
    const first = state.knobs.nestFirstT == null ? 25 : state.knobs.nestFirstT;
    if (state.t + 1e-9 < first) return false;
    const live = liveCount == null ? 0 : liveCount;
    if (live >= nestFieldCap(state)) return false;
    if (state.lastNestClearAt < 0) return true;
    const gap = state.knobs.nestGap == null ? 12 : state.knobs.nestGap;
    return state.t - state.lastNestClearAt + 1e-9 >= gap;
  }

  function markNestCleared(state) {
    state.lastNestClearAt = state.t;
  }

  function isNewNestContact(wasTouching, vn) {
    return !wasTouching && vn < -1e-4;
  }

  function resolveNestHits(hp, hits) {
    const start = Math.max(0, hp | 0);
    if (!hits || !hits.length || start <= 0) {
      return { hp: start, ownerId: null, exploded: false };
    }
    const ranked = hits.slice().sort((a, b) => a.vn - b.vn || a.id - b.id);
    let remaining = start;
    let ownerId = null;
    let exploded = false;
    for (let i = 0; i < ranked.length; i++) {
      if (remaining <= 0) break;
      remaining -= 1;
      if (remaining === 0) {
        exploded = true;
        const vn = ranked[i].vn;
        const tied = ranked.filter((h) => Math.abs(h.vn - vn) <= 1e-9);
        ownerId = tied.length > 1 ? -1 : ranked[i].id;
        break;
      }
    }
    return { hp: remaining, ownerId, exploded };
  }

  function scatterEggs(spec) {
    const n = Math.max(0, spec.n | 0);
    const rand = spec.rand || Math.random;
    const speed = spec.speed == null ? 0 : spec.speed;
    const pad = spec.pad == null ? 0.36 : spec.pad;
    const hw = spec.hw == null ? 21.2 : spec.hw;
    const hd = spec.hd == null ? 31.8 : spec.hd;
    const minSep = spec.minSep == null ? 0 : spec.minSep;
    const out = [];
    for (let i = 0; i < n; i++) {
      let x = 0;
      let z = 0;
      let ok = false;
      for (let t = 0; t < 80; t++) {
        x = (rand() * 2 - 1) * hw;
        z = (rand() * 2 - 1) * hd;
        if (spec.sdf && spec.sdf(x, z) > -pad) continue;
        let packed = false;
        if (minSep > 0) {
          for (let j = 0; j < out.length; j++) {
            if (hypot(out[j].x - x, out[j].z - z) < minSep) {
              packed = true;
              break;
            }
          }
        }
        if (packed) continue;
        ok = true;
        break;
      }
      if (!ok && spec.clamp) {
        const p = spec.clamp(x, z, pad);
        x = p.x;
        z = p.z;
      }
      const ang = rand() * Math.PI * 2;
      out.push({
        x,
        z,
        vx: Math.cos(ang) * speed,
        vz: Math.sin(ang) * speed,
      });
    }
    return out;
  }

  function eggHatchTimes(n, knobs, rand) {
    const count = Math.max(0, n | 0);
    const base = knobs.eggHatchT == null ? 3 : knobs.eggHatchT;
    const gap = Math.max(0, knobs.eggHatchGap == null ? 0.28 : knobs.eggHatchGap);
    const jitter = Math.max(0, knobs.eggHatchJitter == null ? 0.15 : knobs.eggHatchJitter);
    const roll = rand || Math.random;
    const slots = [];
    for (let i = 0; i < count; i++) {
      slots.push(base + i * gap + roll() * jitter);
    }
    for (let i = count - 1; i > 0; i--) {
      const j = Math.floor(roll() * (i + 1)) % (i + 1);
      const tmp = slots[i];
      slots[i] = slots[j];
      slots[j] = tmp;
    }
    return slots;
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
    refreshBabyBody,
    babyCanLoot,
    babyChargeStats,
    babyChargeDeltaV,
    babyAttackCd,
    tickBabyAtkCd,
    canBabyCharge,
    hitCreditId,
    nestFieldCap,
    shouldSpawnNest,
    markNestCleared,
    isNewNestContact,
    resolveNestHits,
    scatterEggs,
    eggHatchTimes,
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
