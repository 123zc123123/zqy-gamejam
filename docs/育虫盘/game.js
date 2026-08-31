(() => {
  "use strict";

  const COLS = 3;
  const ROWS = 5;
  const SIZE = COLS * ROWS;
  const CAGE_SLOTS = 5;
  const START_EGGS = 16;
  const CLAIM_EGGS = 8;
  const STORAGE_KEY = "zqy-hatchery-v3";
  const DRAG_PX = 8;

  const STAGE = { EGG: 0, LARVA: 1, NYMPH: 2, ADULT: 3 };
  const STAGE_NAME = ["虫卵", "幼虫", "中虫", "成虫"];
  const NEXT_NAME = ["幼虫", "中虫", "成虫"];

  const QUALITY = [
    { id: "common", name: "凡品", weight: 55, color: "#d4cbb0", power: 0 },
    { id: "good", name: "良品", weight: 28, color: "#7ec98a", power: 1 },
    { id: "fine", name: "精品", weight: 13, color: "#6eb4e8", power: 2 },
    { id: "peak", name: "极品", weight: 4, color: "#f0c14b", power: 3 },
  ];

  const TRAITS = [
    { id: "brave", name: "好斗", hint: "出手更狠，撞后自己也晃。（占位）", mods: { power: 18, size: 0, charge: 6, resist: -10 } },
    { id: "steady", name: "沉稳", hint: "不容易被掀翻，出手偏慢。（占位）", mods: { power: 4, size: 6, charge: -8, resist: 16 } },
    { id: "swift", name: "灵巧", hint: "蓄得快、体型小，冲击一般。（占位）", mods: { power: -6, size: -8, charge: 16, resist: 0 } },
    { id: "tough", name: "耐久", hint: "皮实抗撞，蓄力偏钝。（占位）", mods: { power: 2, size: 10, charge: -6, resist: 14 } },
  ];

  const STATS = [
    { id: "power", name: "力量" },
    { id: "size", name: "体型" },
    { id: "charge", name: "蓄力" },
    { id: "resist", name: "抗击" },
  ];

  const STAGE_BASE = {
    0: { power: 8, size: 10, charge: 6, resist: 6 },
    1: { power: 22, size: 24, charge: 20, resist: 18 },
    2: { power: 40, size: 42, charge: 38, resist: 36 },
    3: { power: 58, size: 56, charge: 54, resist: 52 },
  };

  const QUALITY_MULT = { common: 1, good: 1.18, fine: 1.4, peak: 1.7 };

  const HATCH_KICKER = { common: "破茧", good: "良种", fine: "锋芒", peak: "神虫" };

  const ADULT_MERGE = {
    common: { next: "good", success: 0.72, failEggs: 3 },
    good: { next: "fine", success: 0.48, failEggs: 5 },
    fine: { next: "peak", success: 0.25, failEggs: 8 },
  };

  const SELL = {
    larva: 3,
    nymph: 8,
    common: 20,
    good: 50,
    fine: 140,
    peak: 420,
  };

  const phoneEl = document.getElementById("phone");
  const boardEl = document.getElementById("board");
  const eggBtn = document.getElementById("btn-egg");
  const eggCountEl = document.getElementById("egg-count");
  const eggArtEl = document.getElementById("egg-art");
  const cageListEl = document.getElementById("cage-list");
  const cageCountEl = document.getElementById("cage-count");
  const hintEl = document.getElementById("hint");
  const toastEl = document.getElementById("toast");
  const ghostEl = document.getElementById("ghost");
  const revealEl = document.getElementById("reveal");
  const revealArt = document.getElementById("reveal-art");
  const revealQuality = document.getElementById("reveal-quality");
  const revealTrait = document.getElementById("reveal-trait");
  const revealSub = document.getElementById("reveal-sub");
  const revealKicker = document.getElementById("reveal-kicker");
  const fxCanvas = document.getElementById("fx-canvas");
  const moneyEl = document.getElementById("money");
  const sheetEl = document.getElementById("sheet");
  const sheetArt = document.getElementById("sheet-art");
  const sheetTitle = document.getElementById("sheet-title");
  const sheetTags = document.getElementById("sheet-tags");
  const sheetBlurb = document.getElementById("sheet-blurb");
  const sheetStats = document.getElementById("sheet-stats");
  const sheetPrice = document.getElementById("sheet-price");
  const sheetCageBtn = document.getElementById("sheet-cage");
  const sheetSellBtn = document.getElementById("sheet-sell");
  const appEl = document.getElementById("app");

  let uid = 1;
  let toastTimer = 0;
  let fxRaf = 0;

  const state = {
    eggs: START_EGGS,
    money: 0,
    cells: Array(SIZE).fill(null),
    cage: Array(CAGE_SLOTS).fill(null),
    selected: -1,
  };

  const ui = {
    dragging: false,
    from: -1,
    cageFrom: -1,
    pointerId: null,
    startX: 0,
    startY: 0,
    moved: false,
    hover: -1,
    pendingAdult: null,
    inspect: null,
    hatchTimer: 0,
  };

  function svgEgg(fill = "#e8d9b0") {
    return `<svg viewBox="0 0 64 64" aria-hidden="true">
      <ellipse cx="32" cy="34" rx="15" ry="20" fill="${fill}"/>
      <ellipse cx="32" cy="34" rx="15" ry="20" fill="none" stroke="#5a4a32" stroke-width="1.4"/>
      <ellipse cx="27" cy="26" rx="6" ry="8" fill="rgba(255,255,255,0.32)"/>
      <circle cx="40" cy="38" r="2.1" fill="#b08950" opacity="0.55"/>
      <circle cx="28" cy="42" r="1.6" fill="#b08950" opacity="0.4"/>
      <circle cx="36" cy="46" r="1.2" fill="#b08950" opacity="0.35"/>
    </svg>`;
  }

  function svgLarva() {
    return `<svg viewBox="0 0 64 64" aria-hidden="true">
      <path d="M12 36c0-8 8-14 16-14 6 0 8 4 14 4 8 0 12 6 12 12 0 8-8 14-20 14-14 0-22-6-22-16z" fill="#c8b48a" stroke="#4a3c28" stroke-width="1.4"/>
      <path d="M22 30c2 0 3 2 3 4M30 28c2 0 3 2 3 4M38 30c2 0 3 2 3 4" fill="none" stroke="#6a5840" stroke-width="1.2" stroke-linecap="round"/>
      <circle cx="16" cy="34" r="3.2" fill="#5a4030"/>
      <circle cx="15.2" cy="33.2" r="1" fill="#eee"/>
      <path d="M12 32c-3-4-2-8 1-9" fill="none" stroke="#4a3c28" stroke-width="1.2" stroke-linecap="round"/>
    </svg>`;
  }

  function svgNymph() {
    return `<svg viewBox="0 0 64 64" aria-hidden="true">
      <path d="M18 22c-6-8-4-14 2-12" fill="none" stroke="#c4a574" stroke-width="1.3" stroke-linecap="round"/>
      <path d="M24 22c-4-9-1-14 4-11" fill="none" stroke="#c4a574" stroke-width="1.3" stroke-linecap="round"/>
      <ellipse cx="28" cy="30" rx="8" ry="7" fill="#6b8f4e" stroke="#2c3c20" stroke-width="1.3"/>
      <ellipse cx="40" cy="36" rx="12" ry="8" fill="#7a9a58" stroke="#2c3c20" stroke-width="1.3"/>
      <path d="M48 34c8 2 10 8 8 14M50 38c6 4 6 10 2 14" fill="none" stroke="#2c3c20" stroke-width="1.5" stroke-linecap="round"/>
      <path d="M20 36c-8 2-12 8-10 14M22 40c-8 4-8 10-4 14" fill="none" stroke="#2c3c20" stroke-width="1.4" stroke-linecap="round"/>
      <path d="M32 42c-2 6-8 10-12 10M38 44c2 6 0 10-2 12" fill="none" stroke="#2c3c20" stroke-width="1.3" stroke-linecap="round"/>
      <circle cx="24" cy="28" r="1.2" fill="#1a2014"/>
    </svg>`;
  }

  function svgAdult(color = "#c4a574") {
    const dark = shade(color, -0.45);
    return `<svg viewBox="0 0 64 64" aria-hidden="true">
      <path d="M16 18c-8-10-6-16 2-13" fill="none" stroke="${color}" stroke-width="1.4" stroke-linecap="round"/>
      <path d="M22 18c-5-12 0-16 6-12" fill="none" stroke="${color}" stroke-width="1.4" stroke-linecap="round"/>
      <ellipse cx="26" cy="28" rx="8" ry="7" fill="${color}" stroke="${dark}" stroke-width="1.3"/>
      <path d="M32 28c10-8 22-4 24 6 2 10-6 16-18 14-8-1-14-6-14-12 0-4 4-7 8-8z" fill="${color}" stroke="${dark}" stroke-width="1.3"/>
      <path d="M34 26c8-4 16-2 18 4" fill="${shade(color, 0.18)}" stroke="${dark}" stroke-width="0.8"/>
      <path d="M50 34c10 4 12 12 8 18M52 38c8 6 8 12 2 16" fill="none" stroke="${dark}" stroke-width="1.7" stroke-linecap="round"/>
      <path d="M18 34c-10 2-14 10-12 16M20 38c-10 6-10 12-4 16" fill="none" stroke="${dark}" stroke-width="1.5" stroke-linecap="round"/>
      <path d="M30 42c-4 8-12 12-16 12M40 44c4 8 2 12-2 14M46 40c6 4 8 10 6 14" fill="none" stroke="${dark}" stroke-width="1.3" stroke-linecap="round"/>
      <circle cx="22" cy="26" r="1.3" fill="#1a140c"/>
    </svg>`;
  }

  function svgCageEmpty() {
    return `<svg viewBox="0 0 64 64" aria-hidden="true">
      <rect x="13" y="12" width="38" height="42" rx="7" fill="none" stroke="#c4a574" stroke-width="2"/>
      <path d="M13 23h38M13 33h38M13 43h38M25 12v42M39 12v42" fill="none" stroke="#c4a574" stroke-width="1.5"/>
    </svg>`;
  }

  function svgTrait(id, color = "#efe6d2") {
    if (id === "brave") {
      return `<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M8 1.5c1.6 3.2 2.2 5.2 2.2 7.4-1.3.5-3.1.5-4.4 0C5.8 6.7 6.4 4.7 8 1.5z" fill="${color}"/><path d="M5.2 10.2c.5 2 1.6 3.4 2.8 4.2 1.2-.8 2.3-2.2 2.8-4.2" fill="none" stroke="${color}" stroke-width="1.2" stroke-linecap="round"/></svg>`;
    }
    if (id === "steady") {
      return `<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M8 2 13.2 5v3.8c0 2.8-2.1 4.5-5.2 5.7C4.9 13.3 2.8 11.6 2.8 8.8V5z" fill="none" stroke="${color}" stroke-width="1.3"/><path d="M8 5.2v5.6" stroke="${color}" stroke-width="1.2" stroke-linecap="round"/></svg>`;
    }
    if (id === "swift") {
      return `<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M1.8 8.2c4.2-1 7.2-4.2 10.4-6.4-1 4.2-.8 7.2.4 10.4-3.4-1.4-6.6-2.8-10.8-4z" fill="${color}"/></svg>`;
    }
    return `<svg viewBox="0 0 16 16" aria-hidden="true"><circle cx="8" cy="8" r="5" fill="none" stroke="${color}" stroke-width="1.4"/><circle cx="8" cy="8" r="2" fill="${color}"/></svg>`;
  }

  function shade(hex, amt) {
    const n = hex.replace("#", "");
    const r = Math.round(parseInt(n.slice(0, 2), 16) * (1 + amt));
    const g = Math.round(parseInt(n.slice(2, 4), 16) * (1 + amt));
    const b = Math.round(parseInt(n.slice(4, 6), 16) * (1 + amt));
    const clamp = (v) => Math.max(0, Math.min(255, v));
    return `rgb(${clamp(r)},${clamp(g)},${clamp(b)})`;
  }

  function hexToRgb(hex) {
    const n = hex.replace("#", "");
    return {
      r: parseInt(n.slice(0, 2), 16),
      g: parseInt(n.slice(2, 4), 16),
      b: parseInt(n.slice(4, 6), 16),
    };
  }

  function artFor(piece) {
    if (!piece) return "";
    if (piece.stage === STAGE.EGG) return svgEgg();
    if (piece.stage === STAGE.LARVA) return svgLarva();
    if (piece.stage === STAGE.NYMPH) return svgNymph();
    const q = qualityById(piece.quality);
    return svgAdult(q.color);
  }

  function qualityById(id) {
    return QUALITY.find((q) => q.id === id) || QUALITY[0];
  }

  function traitById(id) {
    return TRAITS.find((t) => t.id === id) || TRAITS[0];
  }

  function rollQuality() {
    const total = QUALITY.reduce((s, q) => s + q.weight, 0);
    let r = Math.random() * total;
    for (const q of QUALITY) {
      r -= q.weight;
      if (r <= 0) return q;
    }
    return QUALITY[0];
  }

  function rollTrait() {
    return TRAITS[Math.floor(Math.random() * TRAITS.length)];
  }

  function makePiece(stage, extra = {}) {
    return { id: uid++, stage, quality: null, trait: null, ...extra };
  }

  function firstEmpty() {
    return state.cells.findIndex((c) => !c);
  }

  function firstCageEmpty() {
    return state.cage.findIndex((c) => !c);
  }

  function cageCount() {
    return state.cage.reduce((n, p) => n + (p ? 1 : 0), 0);
  }

  function sellPrice(piece) {
    if (!piece) return 0;
    if (piece.stage === STAGE.LARVA) return SELL.larva;
    if (piece.stage === STAGE.NYMPH) return SELL.nymph;
    if (piece.stage === STAGE.ADULT) return SELL[piece.quality] || 0;
    return 0;
  }

  function pieceLabel(piece) {
    if (!piece) return "";
    if (piece.stage !== STAGE.ADULT) return STAGE_NAME[piece.stage];
    return `${qualityById(piece.quality).name} · ${traitById(piece.trait).name}`;
  }

  function clamp(n, a, b) {
    return Math.max(a, Math.min(b, n));
  }

  function combatStats(piece) {
    const base = { ...STAGE_BASE[piece.stage] };
    if (piece.stage !== STAGE.ADULT) return base;
    const q = QUALITY_MULT[piece.quality] || 1;
    const t = traitById(piece.trait);
    const out = {};
    STATS.forEach((s) => {
      out[s.id] = Math.round(clamp((base[s.id] + (t.mods[s.id] || 0)) * q, 5, 99));
    });
    return out;
  }

  function canMerge(a, b) {
    if (!a || !b || a.stage !== b.stage) return false;
    if (a.stage < STAGE.ADULT) return true;
    if (a.quality !== b.quality) return false;
    return a.quality !== "peak";
  }

  function mergeBlockReason(a, b) {
    if (!a || !b) return "";
    if (a.stage !== b.stage) return "只能和相同阶段合成";
    if (a.stage === STAGE.ADULT) {
      if (a.quality === "peak" || b.quality === "peak") return "极品已是最高，不能再合成";
      if (a.quality !== b.quality) return "成虫只能同品质合成";
    }
    return "不能合成";
  }

  function toast(msg) {
    toastEl.textContent = msg;
    toastEl.classList.remove("hidden");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toastEl.classList.add("hidden"), 1600);
  }

  function save() {
    try {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({ eggs: state.eggs, money: state.money, cells: state.cells, cage: state.cage, uid })
      );
    } catch (_) { /* ignore quota */ }
  }

  function load() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return;
      const data = JSON.parse(raw);
      if (typeof data.eggs === "number") state.eggs = data.eggs;
      if (typeof data.money === "number") state.money = data.money;
      if (Array.isArray(data.cells) && data.cells.length === SIZE) state.cells = data.cells;
      if (Array.isArray(data.cage)) {
        const next = Array(CAGE_SLOTS).fill(null);
        data.cage.slice(0, CAGE_SLOTS).forEach((p, i) => { next[i] = p || null; });
        state.cage = next;
      }
      if (typeof data.uid === "number") uid = Math.max(data.uid, 1);
    } catch (_) { /* ignore */ }
  }

  function setHint() {
    const hasPiece = state.cells.some(Boolean);
    const hasAdult = state.cells.some((p) => p && p.stage === STAGE.ADULT);
    if (cageCount() >= CAGE_SLOTS && hasAdult) {
      hintEl.textContent = "虫笼满了。可把笼里的成虫点回棋盘，或卖掉腾空。";
    } else if (state.eggs <= 0 && !hasPiece) {
      hintEl.textContent = "虫卵用完了。点「再领一窝」，或把低品质成虫合成（失败也会化卵）。";
    } else if (!hasPiece) {
      hintEl.textContent = "点左下角虫卵放到空格。两只相同的拖到一起就能合成。";
    } else if (hasAdult) {
      hintEl.textContent = "点虫子看详情。两只同品质成虫拖到一起可赌升一阶。";
    } else {
      hintEl.textContent = "卵→幼虫→中虫→成虫。点虫子看数值，可出售换钱。";
    }
  }

  function renderMoney() {
    moneyEl.textContent = String(state.money);
  }

  function renderCage() {
    cageListEl.querySelectorAll(".cage-slot").forEach((slot, i) => {
      const p = state.cage[i];
      slot.className = "cage-slot";
      if (!p) {
        slot.classList.add("empty");
        slot.title = "空笼";
        slot.innerHTML = svgCageEmpty();
        return;
      }
      const q = qualityById(p.quality);
      const t = traitById(p.trait);
      slot.classList.add("filled", `q-${q.id}`);
      if (ui.cageFrom === i && ui.dragging) slot.classList.add("dragging");
      slot.title = `${q.name} · ${t.name}（点一下放回棋盘）`;
      slot.innerHTML = `<span class="gem" style="background:${q.color}"></span>${artFor(p)}<span class="mark">${svgTrait(t.id, q.color)}</span>`;
    });
    cageCountEl.textContent = `${cageCount()} / ${CAGE_SLOTS}`;
  }

  function render() {
    eggArtEl.innerHTML = svgEgg();
    const n = state.eggs;
    eggCountEl.textContent = String(n);
    eggCountEl.classList.toggle("hidden", n <= 0);
    eggBtn.classList.toggle("empty", n <= 0);

    const selected = state.selected;
    const selPiece = selected >= 0 ? state.cells[selected] : null;

    boardEl.querySelectorAll(".cell").forEach((cell, i) => {
      const piece = state.cells[i];
      const matchable =
        selPiece && piece && i !== selected && canMerge(selPiece, piece);

      cell.className = "cell";
      if (!piece) cell.classList.add("empty");
      if (i === selected) cell.classList.add("selected");
      if (matchable) cell.classList.add("matchable");
      if (i === ui.from && ui.dragging) cell.classList.add("dragging");
      if (ui.dragging && i === ui.hover) {
        const src = state.cells[ui.from];
        const dst = state.cells[i];
        if (!dst) cell.classList.add("drop-ok");
        else if (src && dst && i !== ui.from && canMerge(src, dst)) {
          cell.classList.add("drop-merge");
        }
      }
      if (piece && piece.stage === STAGE.ADULT && piece.quality === "peak") {
        cell.classList.add("q-peak");
      }

      if (!piece) {
        cell.innerHTML = "";
        return;
      }

      const q = piece.quality ? qualityById(piece.quality) : null;
      const t = piece.trait ? traitById(piece.trait) : null;
      const tag = q
        ? `<div class="piece-tag"><span class="q" style="color:${q.color}">${q.name}</span><br><span class="t">${t.name}</span></div>`
        : `<div class="piece-tag">${STAGE_NAME[piece.stage]}</div>`;

      cell.innerHTML = `<div class="piece stage-${piece.stage}" data-id="${piece.id}">${artFor(piece)}${tag}</div>`;
    });

    renderCage();
    renderMoney();
    setHint();
  }

  function pulsePiece(index) {
    const el = boardEl.children[index]?.querySelector(".piece");
    if (!el) return;
    el.classList.remove("pop", "merge-in");
    void el.offsetWidth;
    el.classList.add("merge-in");
  }

  function burstCell(index) {
    const cell = boardEl.children[index];
    if (!cell) return;
    cell.classList.remove("hatch-burst");
    void cell.offsetWidth;
    cell.classList.add("hatch-burst");
  }

  function stopFX() {
    cancelAnimationFrame(fxRaf);
    fxRaf = 0;
    const ctx = fxCanvas.getContext("2d");
    if (ctx) ctx.clearRect(0, 0, fxCanvas.width, fxCanvas.height);
  }

  function burstFX(hex, power) {
    stopFX();
    const c = fxCanvas;
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    c.width = Math.floor(window.innerWidth * dpr);
    c.height = Math.floor(window.innerHeight * dpr);
    const ctx = c.getContext("2d");
    const cx = c.width / 2;
    const cy = c.height * 0.44;
    const count = 28 + power * 58;
    const rgb = hexToRgb(hex);
    const particles = [];

    for (let i = 0; i < count; i++) {
      const a = Math.random() * Math.PI * 2;
      const s = (1.8 + Math.random() * (5.5 + power * 8)) * dpr;
      particles.push({
        x: cx,
        y: cy,
        vx: Math.cos(a) * s,
        vy: Math.sin(a) * s - (1.4 + power * 0.8) * dpr,
        life: 1,
        decay: 0.008 + Math.random() * 0.016,
        r: (1.1 + Math.random() * (2.8 + power)) * dpr,
        spark: Math.random() < 0.22 + power * 0.08,
        rot: Math.random() * Math.PI,
        vr: (Math.random() - 0.5) * 0.28,
      });
    }

    if (power >= 2) {
      for (let i = 0; i < 10 + power * 8; i++) {
        const a = (Math.PI * 2 * i) / 18;
        particles.push({
          x: cx,
          y: cy,
          vx: Math.cos(a) * (7 + power * 2) * dpr,
          vy: Math.sin(a) * (7 + power * 2) * dpr,
          life: 1,
          decay: 0.012,
          r: 3.2 * dpr,
          spark: true,
          rot: a,
          vr: 0.2,
        });
      }
    }

    function tick() {
      ctx.clearRect(0, 0, c.width, c.height);
      ctx.globalCompositeOperation = "lighter";
      let alive = false;
      for (const p of particles) {
        p.life -= p.decay;
        if (p.life <= 0) continue;
        alive = true;
        p.x += p.vx;
        p.y += p.vy;
        p.vy += 0.06 * dpr;
        p.vx *= 0.986;
        p.rot += p.vr;
        const a = Math.max(0, p.life);
        ctx.globalAlpha = a;
        ctx.fillStyle = `rgba(${rgb.r},${rgb.g},${rgb.b},${0.28 + a * 0.72})`;
        if (p.spark) {
          ctx.save();
          ctx.translate(p.x, p.y);
          ctx.rotate(p.rot);
          ctx.fillRect(-p.r * 3.4, -p.r * 0.32, p.r * 6.8, p.r * 0.64);
          ctx.restore();
        } else {
          ctx.beginPath();
          ctx.arc(p.x, p.y, p.r * (0.55 + a), 0, Math.PI * 2);
          ctx.fill();
        }
      }
      ctx.globalAlpha = 1;
      if (alive) fxRaf = requestAnimationFrame(tick);
    }
    tick();
  }

  function placeEgg() {
    if (state.eggs <= 0) {
      toast("没有虫卵了");
      return;
    }
    const i = firstEmpty();
    if (i < 0) {
      toast("棋盘满了，先合成或把成虫收入虫笼");
      return;
    }
    state.eggs -= 1;
    state.cells[i] = makePiece(STAGE.EGG);
    state.selected = i;
    render();
    const el = boardEl.children[i]?.querySelector(".piece");
    if (el) el.classList.add("pop");
    save();
  }

  function mergeAt(from, to) {
    const a = state.cells[from];
    const b = state.cells[to];
    if (!canMerge(a, b)) return false;

    state.cells[from] = null;
    state.selected = to;

    if (a.stage === STAGE.ADULT) {
      const recipe = ADULT_MERGE[a.quality];
      if (Math.random() < recipe.success) {
        const nextQ = qualityById(recipe.next);
        const traitId = Math.random() < 0.5 ? a.trait : b.trait;
        const adult = makePiece(STAGE.ADULT, { quality: nextQ.id, trait: traitId });
        state.cells[to] = adult;
        render();
        burstCell(to);
        openReveal(adult, {
          kicker: "进阶",
          sub: `由两只${qualityById(a.quality).name}合成。性格继承父母之一。`,
        });
      } else {
        state.cells[to] = null;
        state.selected = -1;
        state.eggs += recipe.failEggs;
        render();
        const cell = boardEl.children[to];
        if (cell) {
          cell.classList.remove("fail-burst");
          void cell.offsetWidth;
          cell.classList.add("fail-burst");
        }
        phoneEl.classList.remove("broke");
        void phoneEl.offsetWidth;
        phoneEl.classList.add("broke");
        toast(`合成失败，化为 ${recipe.failEggs} 枚虫卵`);
        save();
      }
      return true;
    }

    const next = a.stage + 1;
    if (next === STAGE.ADULT) {
      const q = rollQuality();
      const t = rollTrait();
      const adult = makePiece(STAGE.ADULT, { quality: q.id, trait: t.id });
      state.cells[to] = adult;
      render();
      burstCell(to);
      openReveal(adult, {
        kicker: "破茧",
        sub: `${t.hint}。品质与性格分开掷，互不影响。`,
      });
    } else {
      state.cells[to] = makePiece(next);
      render();
      pulsePiece(to);
      toast(`合成 ${NEXT_NAME[a.stage]}`);
      save();
    }
    return true;
  }

  function moveTo(from, to) {
    if (from === to) return;
    if (state.cells[to]) return;
    state.cells[to] = state.cells[from];
    state.cells[from] = null;
    state.selected = to;
    render();
    save();
  }

  function collect(index) {
    const p = state.cells[index];
    if (!p || p.stage !== STAGE.ADULT) return;
    const slot = firstCageEmpty();
    if (slot < 0) {
      toast("虫笼满了，最多五只");
      return;
    }
    state.cells[index] = null;
    if (state.selected === index) state.selected = -1;
    state.cage[slot] = p;
    const q = qualityById(p.quality);
    const t = traitById(p.trait);
    render();
    toast(`收入虫笼 · ${q.name} · ${t.name}`);
    save();
  }

  function sellSelected() {
    sellInspect();
  }

  function inspectPiece() {
    const info = ui.inspect;
    if (!info) return null;
    if (info.where === "board") return state.cells[info.index] || null;
    return state.cage[info.index] || null;
  }

  function openSheet(where, index) {
    const piece = where === "board" ? state.cells[index] : state.cage[index];
    if (!piece) return;
    ui.inspect = { where, index };
    state.selected = where === "board" ? index : -1;

    const q = piece.quality ? qualityById(piece.quality) : null;
    const t = piece.trait ? traitById(piece.trait) : null;
    const stats = combatStats(piece);
    const price = sellPrice(piece);

    sheetArt.innerHTML = artFor(piece);
    sheetTitle.textContent = piece.stage === STAGE.ADULT && q ? q.name : STAGE_NAME[piece.stage];
    if (q) sheetTitle.style.color = q.color;
    else sheetTitle.style.color = "";

    const tags = [`<span>${STAGE_NAME[piece.stage]}</span>`];
    if (q) tags.push(`<span style="color:${q.color};border-color:${q.color}88">${q.name}</span>`);
    if (t) tags.push(`<span>${t.name}</span>`);
    sheetTags.innerHTML = tags.join("");

    if (piece.stage === STAGE.EGG) {
      sheetBlurb.textContent = "还没孵化。两枚卵合成一只幼虫。";
    } else if (piece.stage === STAGE.ADULT && t) {
      sheetBlurb.textContent = t.hint;
    } else {
      sheetBlurb.textContent = "还没定品。两只中虫合成才会出品质和性格。";
    }

    sheetStats.innerHTML = STATS.map((s) => {
      const v = stats[s.id];
      return `<li><span>${s.name}</span><span class="stat-bar"><i data-w="${v}" style="background:${q ? q.color : "#c4a574"}"></i></span><span>${v}</span></li>`;
    }).join("");
    requestAnimationFrame(() => {
      sheetStats.querySelectorAll("i").forEach((el) => {
        el.style.width = `${el.dataset.w}%`;
      });
    });

    sheetPrice.textContent = price > 0 ? `售价 ${price} 钱` : "虫卵不出售";

    const canCage = piece.stage === STAGE.ADULT;
    const inCage = where === "cage";
    sheetCageBtn.textContent = inCage ? "放回棋盘" : "入笼";
    sheetCageBtn.disabled = !canCage;
    sheetCageBtn.classList.toggle("ghost", !canCage);
    sheetSellBtn.disabled = price <= 0;
    sheetSellBtn.classList.toggle("ghost", price <= 0);

    sheetEl.classList.remove("hidden");
  }

  function closeSheet() {
    sheetEl.classList.add("hidden");
    ui.inspect = null;
  }

  function sellInspect() {
    const p = inspectPiece();
    const price = sellPrice(p);
    if (!p || price <= 0) {
      toast("虫卵不能卖");
      return;
    }
    const info = ui.inspect;
    if (info.where === "board") {
      state.cells[info.index] = null;
      if (state.selected === info.index) state.selected = -1;
    } else {
      state.cage[info.index] = null;
    }
    state.money += price;
    closeSheet();
    render();
    toast(`售出${pieceLabel(p)} · +${price} 钱`);
    save();
  }

  function cageInspect() {
    const info = ui.inspect;
    const p = inspectPiece();
    if (!info || !p || p.stage !== STAGE.ADULT) return;
    closeSheet();
    if (info.where === "cage") takeOutCage(info.index, firstEmpty());
    else collect(info.index);
  }

  function takeOutCage(slot, dest) {
    const p = state.cage[slot];
    if (!p) return;
    const i = dest >= 0 ? dest : firstEmpty();
    if (i < 0) {
      toast("棋盘满了，先合成、卖掉或挪开");
      return;
    }
    if (state.cells[i]) return;
    state.cage[slot] = null;
    state.cells[i] = p;
    state.selected = i;
    ui.cageFrom = -1;
    render();
    boardEl.children[i]?.querySelector(".piece")?.classList.add("pop");
    toast("已放回棋盘");
    save();
  }

  function shakeScreen(level) {
    const names = ["shake-sm", "shake-md", "shake-lg", "shake-xl"];
    appEl.classList.remove(...names);
    if (level < 1) return;
    const cls = names[Math.min(level, 4) - 1];
    void appEl.offsetWidth;
    appEl.classList.add(cls);
  }

  function openReveal(adult, extra = {}) {
    const q = qualityById(adult.quality);
    const t = traitById(adult.trait);
    ui.pendingAdult = adult;
    closeSheet();
    revealKicker.textContent = extra.kicker || HATCH_KICKER[q.id] || "破茧";
    revealArt.innerHTML = artFor(adult);
    revealQuality.textContent = q.name;
    revealQuality.style.color = q.color;
    revealTrait.textContent = t.name;
    revealSub.textContent = extra.sub || `${t.hint} 品质越高，基础数值乘得越多。`;
    clearTimeout(ui.hatchTimer);
    revealEl.className = `q-${q.id}`;
    void revealEl.offsetWidth;

    const fire = () => {
      revealEl.classList.remove("hold");
      revealEl.classList.add("show");
      burstFX(q.color, q.power);
      if (q.power >= 1) shakeScreen(q.power + 1);
    };

    if (q.power >= 2) {
      revealEl.classList.add("hold");
      shakeScreen(q.power);
      ui.hatchTimer = setTimeout(fire, q.power >= 3 ? 420 : 180);
    } else {
      fire();
    }
    save();
  }

  function closeReveal() {
    clearTimeout(ui.hatchTimer);
    stopFX();
    revealEl.className = "hidden";
    ui.pendingAdult = null;
    const idx = state.cells.findIndex((p) => p && p.stage === STAGE.ADULT);
    if (idx >= 0) pulsePiece(idx);
  }

  function cagePending() {
    const adult = ui.pendingAdult;
    if (firstCageEmpty() < 0) {
      toast("虫笼满了，最多五只");
      closeReveal();
      return;
    }
    closeReveal();
    if (!adult) return;
    const idx = state.cells.findIndex((p) => p && p.id === adult.id);
    if (idx >= 0) collect(idx);
  }

  function cellIndexFromPoint(x, y) {
    const el = document.elementFromPoint(x, y);
    if (!el) return -1;
    const cell = el.closest?.(".cell");
    if (!cell || !boardEl.contains(cell)) return -1;
    return Number(cell.dataset.index);
  }

  function onBoardDown(e) {
    if (e.button != null && e.button !== 0) return;
    e.preventDefault();

    const cell = e.target.closest?.(".cell");
    if (!cell) return;
    const i = Number(cell.dataset.index);
    const piece = state.cells[i];

    if (!piece) {
      if (ui.cageFrom >= 0 && state.cage[ui.cageFrom]) {
        takeOutCage(ui.cageFrom, i);
        return;
      }
      if (state.selected >= 0 && state.cells[state.selected]) {
        moveTo(state.selected, i);
        return;
      }
      if (state.eggs > 0) placeEggOn(i);
      else toast("没有虫卵了");
      return;
    }

    ui.from = i;
    ui.pointerId = e.pointerId;
    ui.startX = e.clientX;
    ui.startY = e.clientY;
    ui.moved = false;
    ui.dragging = false;
    ui.hover = i;
    cell.setPointerCapture?.(e.pointerId);
  }

  function placeEggOn(i) {
    if (state.cells[i] || state.eggs <= 0) return;
    state.eggs -= 1;
    state.cells[i] = makePiece(STAGE.EGG);
    state.selected = i;
    render();
    boardEl.children[i]?.querySelector(".piece")?.classList.add("pop");
    save();
  }

  function onBoardMove(e) {
    if (ui.from < 0) return;
    const dx = e.clientX - ui.startX;
    const dy = e.clientY - ui.startY;
    if (!ui.dragging && dx * dx + dy * dy > DRAG_PX * DRAG_PX) {
      ui.dragging = true;
      ui.moved = true;
      ghostEl.innerHTML = artFor(state.cells[ui.from]);
      ghostEl.style.left = `${e.clientX}px`;
      ghostEl.style.top = `${e.clientY}px`;
      ghostEl.classList.remove("hidden");
      render();
    }
    if (!ui.dragging) return;
    ghostEl.style.left = `${e.clientX}px`;
    ghostEl.style.top = `${e.clientY}px`;
    const hover = cellIndexFromPoint(e.clientX, e.clientY);
    if (hover !== ui.hover) {
      ui.hover = hover;
      render();
    }
  }

  function endDrag(e) {
    const from = ui.from;
    const wasDrag = ui.dragging;
    const x = e.clientX;
    const y = e.clientY;
    ghostEl.classList.add("hidden");
    ui.dragging = false;
    ui.from = -1;
    ui.hover = -1;
    ui.pointerId = null;

    if (from < 0) return;

    if (wasDrag) {
      const to = cellIndexFromPoint(x, y);
      const src = state.cells[from];
      const dst = to >= 0 ? state.cells[to] : null;
      if (to >= 0 && to !== from) {
        if (canMerge(src, dst)) mergeAt(from, to);
        else if (!dst) moveTo(from, to);
        else {
          state.selected = from;
          render();
          toast(mergeBlockReason(src, dst));
        }
      } else {
        state.selected = from;
        render();
      }
      return;
    }

    const piece = state.cells[from];
    if (!piece) {
      render();
      return;
    }
    openSheet("board", from);
  }

  function resetAll() {
    stopFX();
    state.eggs = START_EGGS;
    state.money = 0;
    state.cells = Array(SIZE).fill(null);
    state.cage = Array(CAGE_SLOTS).fill(null);
    state.selected = -1;
    ui.cageFrom = -1;
    uid = 1;
    render();
    save();
    toast("已重置");
  }

  function buildBoard() {
    boardEl.innerHTML = "";
    for (let i = 0; i < SIZE; i++) {
      const cell = document.createElement("div");
      cell.className = "cell empty";
      cell.dataset.index = String(i);
      cell.setAttribute("role", "gridcell");
      boardEl.appendChild(cell);
    }
  }

  function buildCage() {
    cageListEl.innerHTML = "";
    for (let i = 0; i < CAGE_SLOTS; i++) {
      const slot = document.createElement("div");
      slot.className = "cage-slot empty";
      slot.dataset.cage = String(i);
      cageListEl.appendChild(slot);
    }
  }

  function onCageDown(e) {
    if (e.button != null && e.button !== 0) return;
    const slotEl = e.target.closest?.(".cage-slot");
    if (!slotEl || !slotEl.classList.contains("filled")) return;
    const i = Number(slotEl.dataset.cage);
    if (!state.cage[i]) return;
    e.preventDefault();
    ui.cageFrom = i;
    ui.from = -1;
    ui.pointerId = e.pointerId;
    ui.startX = e.clientX;
    ui.startY = e.clientY;
    ui.moved = false;
    ui.dragging = false;
    slotEl.setPointerCapture?.(e.pointerId);
  }

  function onCageMove(e) {
    if (ui.cageFrom < 0) return;
    const dx = e.clientX - ui.startX;
    const dy = e.clientY - ui.startY;
    if (!ui.dragging && dx * dx + dy * dy > DRAG_PX * DRAG_PX) {
      ui.dragging = true;
      ghostEl.innerHTML = artFor(state.cage[ui.cageFrom]);
      ghostEl.style.left = `${e.clientX}px`;
      ghostEl.style.top = `${e.clientY}px`;
      ghostEl.classList.remove("hidden");
      render();
    }
    if (!ui.dragging) return;
    ghostEl.style.left = `${e.clientX}px`;
    ghostEl.style.top = `${e.clientY}px`;
    const hover = cellIndexFromPoint(e.clientX, e.clientY);
    if (hover !== ui.hover) {
      ui.hover = hover;
      render();
    }
  }

  function onCageUp(e) {
    const from = ui.cageFrom;
    const wasDrag = ui.dragging;
    const x = e.clientX;
    const y = e.clientY;
    ghostEl.classList.add("hidden");
    ui.dragging = false;
    ui.hover = -1;
    ui.pointerId = null;

    if (from < 0) return;

    if (wasDrag) {
      const to = cellIndexFromPoint(x, y);
      ui.cageFrom = -1;
      if (to >= 0 && !state.cells[to]) takeOutCage(from, to);
      else if (to >= 0 && state.cells[to]) {
        render();
        toast("拖到空格才能放回棋盘");
      } else render();
      return;
    }

    ui.cageFrom = -1;
    openSheet("cage", from);
  }

  buildBoard();
  buildCage();
  eggArtEl.innerHTML = svgEgg();
  load();
  render();

  eggBtn.addEventListener("click", (e) => {
    e.preventDefault();
    placeEgg();
  });

  document.getElementById("btn-claim").addEventListener("click", () => {
    state.eggs += CLAIM_EGGS;
    render();
    save();
    toast(`领到 ${CLAIM_EGGS} 枚虫卵`);
  });

  document.getElementById("btn-reset").addEventListener("click", resetAll);

  document.getElementById("btn-decor").addEventListener("click", () => {
    toast("外围装饰店还没开，钱先攒着");
  });
  document.getElementById("wallet").addEventListener("click", () => {
    toast("钱用来买装饰，店还没开");
  });

  document.getElementById("sheet-close").addEventListener("click", closeSheet);
  sheetEl.addEventListener("click", (e) => {
    if (e.target === sheetEl) closeSheet();
  });
  sheetCageBtn.addEventListener("click", (e) => {
    e.preventDefault();
    cageInspect();
  });
  sheetSellBtn.addEventListener("click", (e) => {
    e.preventDefault();
    sellInspect();
  });

  document.getElementById("btn-reveal").addEventListener("click", closeReveal);
  document.getElementById("btn-cage").addEventListener("click", cagePending);
  revealEl.addEventListener("click", (e) => {
    if (e.target === revealEl) closeReveal();
  });

  boardEl.addEventListener("pointerdown", onBoardDown);
  boardEl.addEventListener("pointermove", onBoardMove);
  boardEl.addEventListener("pointerup", endDrag);
  boardEl.addEventListener("pointercancel", endDrag);

  cageListEl.addEventListener("pointerdown", onCageDown);
  cageListEl.addEventListener("pointermove", onCageMove);
  cageListEl.addEventListener("pointerup", onCageUp);
  cageListEl.addEventListener("pointercancel", onCageUp);

  window.addEventListener("keydown", (e) => {
    if (e.key === "Escape") {
      if (!revealEl.classList.contains("hidden") && ui.pendingAdult) closeReveal();
      else if (!sheetEl.classList.contains("hidden")) closeSheet();
      else {
        state.selected = -1;
        ui.cageFrom = -1;
        render();
      }
    }
  });
})();
