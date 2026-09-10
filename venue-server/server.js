"use strict";

const http = require("http");
const dgram = require("dgram");
const fs = require("fs");
const os = require("os");
const path = require("path");

const PORT = Number(process.env.VENUE_PORT || 8766);
const DISCOVERY_PORT = Number(process.env.VENUE_DISCOVERY_PORT || 28779);
const DATA_FILE = path.join(__dirname, "players.json");

function loadDb() {
  try {
    const raw = fs.readFileSync(DATA_FILE, "utf8");
    const parsed = JSON.parse(raw);
    if (parsed && Array.isArray(parsed.players)) return parsed;
  } catch (_) {}
  return { players: [] };
}

function saveDb(db) {
  fs.writeFileSync(DATA_FILE, JSON.stringify(db, null, 2), "utf8");
}

function lanIPv4() {
  const out = [];
  const ifs = os.networkInterfaces();
  for (const name of Object.keys(ifs)) {
    for (const info of ifs[name] || []) {
      if (!info || info.internal) continue;
      if (info.family !== "IPv4" && info.family !== 4) continue;
      if (!info.address || info.address.indexOf("169.254.") === 0) continue;
      if (out.indexOf(info.address) < 0) out.push(info.address);
    }
  }
  return out;
}

function sendJson(res, code, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(code, {
    "Content-Type": "application/json; charset=utf-8",
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, POST, PUT, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
    "Cache-Control": "no-store",
  });
  res.end(body);
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on("data", (c) => chunks.push(c));
    req.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    req.on("error", reject);
  });
}

function normalizeName(raw) {
  return String(raw || "").trim().slice(0, 20);
}

function findPlayer(db, name) {
  const key = name.toLowerCase();
  return db.players.find((p) => p && String(p.playerName || "").toLowerCase() === key) || null;
}

function starterPlayer(name) {
  const now = Date.now() * 10000;
  return {
    playerId: "venue-" + Date.now().toString(36),
    playerName: name,
    updatedAtUtcTicks: now,
    score: 0,
    gold: 100,
    eggs: 24,
    economyReady: true,
    crickets: [],
    backpack: [],
  };
}

let db = loadDb();

const server = http.createServer(async (req, res) => {
  if (req.method === "OPTIONS") {
    sendJson(res, 204, {});
    return;
  }
  const url = new URL(req.url || "/", "http://127.0.0.1");
  try {
    if (req.method === "GET" && url.pathname === "/health") {
      sendJson(res, 200, { ok: true, players: db.players.length });
      return;
    }
    if (req.method === "GET" && url.pathname === "/ranking") {
      const rows = db.players
        .filter((p) => p && p.playerName)
        .map((p, i) => ({ i, playerName: p.playerName, score: Number(p.score) || 0 }));
      rows.sort((a, b) => b.score - a.score || a.i - b.i);
      sendJson(res, 200, { players: rows.map((r) => ({ playerName: r.playerName, score: r.score })) });
      return;
    }
    if (req.method === "POST" && url.pathname === "/login") {
      const body = JSON.parse((await readBody(req)) || "{}");
      const name = normalizeName(body.playerName);
      if (!name) {
        sendJson(res, 400, { error: "请输入玩家名称" });
        return;
      }
      let player = findPlayer(db, name);
      if (!player) {
        player = starterPlayer(name);
        db.players.push(player);
        saveDb(db);
      }
      sendJson(res, 200, { player: player });
      return;
    }
    if (req.method === "PUT" && url.pathname === "/player") {
      const player = JSON.parse((await readBody(req)) || "{}");
      const name = normalizeName(player.playerName);
      if (!name) {
        sendJson(res, 400, { error: "缺少玩家名称" });
        return;
      }
      const existing = findPlayer(db, name);
      player.playerName = existing ? existing.playerName : name;
      if (existing) {
        const idx = db.players.indexOf(existing);
        db.players[idx] = player;
      } else db.players.push(player);
      saveDb(db);
      sendJson(res, 200, { ok: true });
      return;
    }
    sendJson(res, 404, { error: "not found" });
  } catch (err) {
    sendJson(res, 500, { error: String(err && err.message ? err.message : err) });
  }
});

server.listen(PORT, "0.0.0.0", () => {
  const ips = lanIPv4();
  process.stdout.write("斗蛐蛐展会账本已启动\n");
  process.stdout.write("  本机    http://127.0.0.1:" + PORT + "/\n");
  if (!ips.length) process.stdout.write("  没找到局域网 IP：开热点或连同一 Wi-Fi，防火墙放行 Node\n");
  for (let i = 0; i < ips.length; i++)
    process.stdout.write("  手机    http://" + ips[i] + ":" + PORT + "/\n");
  process.stdout.write("手机和笔记本连同一 Wi-Fi。游戏会自动发现本机。Ctrl+C 结束。\n");
});

const udp = dgram.createSocket("udp4");
udp.bind(0, () => {
  udp.setBroadcast(true);
  setInterval(() => {
    const ips = lanIPv4();
    const url = "http://" + (ips[0] || "127.0.0.1") + ":" + PORT;
    const msg = Buffer.from("VENUE|" + url);
    try {
      udp.send(msg, 0, msg.length, DISCOVERY_PORT, "255.255.255.255");
    } catch (_) {}
  }, 1000);
});
