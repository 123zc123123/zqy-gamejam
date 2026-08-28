"use strict";

const fs = require("fs");
const os = require("os");
const path = require("path");
const S = require("./save-server.js");

let failed = 0;
function eq(name, got, expected) {
  if (got !== expected) {
    failed += 1;
    console.error(`FAIL ${name}: got ${JSON.stringify(got)}, expected ${JSON.stringify(expected)}`);
  } else {
    console.log(`ok   ${name}`);
  }
}
function ok(name, cond) {
  eq(name, !!cond, true);
}

function listen(server, host) {
  return new Promise((resolve, reject) => {
    const onErr = (err) => reject(err);
    server.once("error", onErr);
    server.listen(0, host || "127.0.0.1", () => {
      server.removeListener("error", onErr);
      resolve(server.address().port);
    });
  });
}

function close(server) {
  return new Promise((resolve) => {
    if (!server || !server.listening) {
      resolve();
      return;
    }
    server.close(() => resolve());
  });
}

function collector(ws) {
  const q = [];
  const waiters = [];
  ws.addEventListener("message", (ev) => {
    let msg;
    try { msg = JSON.parse(String(ev.data || "")); } catch (_) { return; }
    if (waiters.length) waiters.shift()(msg);
    else q.push(msg);
  });
  return {
    next(ms) {
      if (q.length) return Promise.resolve(q.shift());
      return new Promise((resolve, reject) => {
        const t = setTimeout(() => reject(new Error("msg timeout")), ms || 3000);
        waiters.push((m) => { clearTimeout(t); resolve(m); });
      });
    },
  };
}

function openWs(port) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket("ws://127.0.0.1:" + port + "/__ws");
    const timer = setTimeout(() => {
      try { ws.close(); } catch (_) { /* ignore */ }
      reject(new Error("open timeout"));
    }, 3000);
    ws.addEventListener("open", () => {
      clearTimeout(timer);
      resolve(ws);
    });
    ws.addEventListener("error", () => {
      clearTimeout(timer);
      reject(new Error("ws error"));
    });
  });
}

function seedRoot(dir) {
  fs.writeFileSync(path.join(dir, "index.html"), "<!doctype html>INDEX", "utf8");
}

async function run() {
  if (typeof WebSocket !== "function") {
    throw new Error("Node WebSocket missing");
  }
  const src = fs.readFileSync(path.join(__dirname, "save-server.js"), "utf8");
  const game = fs.readFileSync(path.join(__dirname, "game.js"), "utf8");
  const html = fs.readFileSync(path.join(__dirname, "index.html"), "utf8");
  const how = fs.readFileSync(path.join(__dirname, "怎么玩.txt"), "utf8");

  ok("relay required", src.indexOf('require("./net-relay.js")') >= 0);
  ok("html host button", html.indexOf('id="btn-host"') >= 0);
  ok("html join button", html.indexOf('id="btn-join"') >= 0);
  ok("html net-code", html.indexOf('id="net-code"') >= 0);
  ok("game isNetPvp", game.indexOf("function isNetPvp") >= 0);
  ok("game remote input", game.indexOf("b.remote") >= 0 && game.indexOf("remoteV3") >= 0);
  ok("game guest pump", game.indexOf("function pumpGuest") >= 0);
  ok("how-to pvp", how.indexOf("开房对打") >= 0);

  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "dq-net-"));
  seedRoot(tmp);
  const server = S.createServer({ root: tmp, host: "127.0.0.1" });
  const port = await listen(server, "127.0.0.1");

  const host = await openWs(port);
  const hostQ = collector(host);
  host.send(JSON.stringify({ type: "create" }));
  const created = await hostQ.next();
  eq("N1 created type", created.type, "created");
  eq("N1 seat 0", created.seat, 0);
  ok("N1 4-digit code", /^\d{4}$/.test(String(created.code)));
  const code = created.code;

  const guest = await openWs(port);
  const guestQ = collector(guest);
  guest.send(JSON.stringify({ type: "join", code }));
  const joined = await guestQ.next();
  eq("N2 joined", joined.type, "joined");
  eq("N2 seat 1", joined.seat, 1);
  const peer = await hostQ.next();
  eq("N2 host sees guest", peer.type, "peer");

  host.send(JSON.stringify({ type: "state", phase: "play", hello: 1 }));
  const state = await guestQ.next();
  eq("N3 state relayed", state.type, "state");
  eq("N3 payload", state.hello, 1);

  guest.send(JSON.stringify({ type: "input", holding: true, x: 0.5, z: -1, mag: 0.8 }));
  const inp = await hostQ.next();
  eq("N4 input relayed", inp.type, "input");
  eq("N4 holding", inp.holding, true);
  eq("N4 mag", inp.mag, 0.8);

  const extra = await openWs(port);
  const extraQ = collector(extra);
  extra.send(JSON.stringify({ type: "join", code }));
  const full = await extraQ.next();
  eq("N5 third is full", full.type, "error");
  eq("N5 full msg", full.message, "full");
  extra.close();

  const miss = await openWs(port);
  const missQ = collector(miss);
  miss.send(JSON.stringify({ type: "join", code: "0000" }));
  const noRoom = await missQ.next();
  eq("N6 missing room", noRoom.type, "error");
  eq("N6 no room", noRoom.message, "no room");
  miss.close();

  const hostB = await openWs(port);
  const hostBQ = collector(hostB);
  hostB.send(JSON.stringify({ type: "create" }));
  const createdB = await hostBQ.next();
  ok("N7 second room", createdB.code !== code);
  hostB.send(JSON.stringify({ type: "state", room: "B" }));
  const leak = await Promise.race([
    guestQ.next(400).then((m) => m).catch(() => null),
  ]);
  eq("N7 rooms isolated", leak, null);
  hostB.close();

  guest.close();
  const left = await hostQ.next();
  eq("N8 guest left", left.type, "peer-left");

  host.close();

  const host2 = await openWs(port);
  const host2Q = collector(host2);
  host2.send(JSON.stringify({ type: "create" }));
  const c2 = await host2Q.next();
  const guest2 = await openWs(port);
  const guest2Q = collector(guest2);
  guest2.send(JSON.stringify({ type: "join", code: c2.code }));
  await guest2Q.next();
  await host2Q.next();
  host2.close();
  const hostGone = await guest2Q.next();
  eq("N9 host left", hostGone.type, "peer-left");
  guest2.close();

  await close(server);
  fs.rmSync(tmp, { recursive: true, force: true });
}

run().then(() => {
  if (failed) {
    console.error("\n" + failed + " failed");
    process.exit(1);
  }
  console.log("\nall checks passed");
}).catch((err) => {
  console.error(err);
  process.exit(1);
});
