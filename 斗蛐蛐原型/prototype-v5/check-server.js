"use strict";

const fs = require("fs");
const http = require("http");
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
      const addr = server.address();
      resolve(addr && addr.port);
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

function request(opts) {
  const timeoutMs = opts.timeoutMs || 4000;
  return new Promise((resolve, reject) => {
    const req = http.request({
      hostname: opts.hostname || "127.0.0.1",
      port: opts.port,
      path: opts.path || "/",
      method: opts.method || "GET",
      headers: opts.headers || {},
    }, (res) => {
      const chunks = [];
      res.on("data", (c) => chunks.push(c));
      res.on("end", () => {
        resolve({
          status: res.statusCode,
          headers: res.headers,
          body: Buffer.concat(chunks).toString("utf8"),
        });
      });
    });
    req.setTimeout(timeoutMs, () => {
      req.destroy();
      reject(new Error("timeout " + timeoutMs));
    });
    req.on("error", reject);
    if (opts.body != null) req.write(opts.body);
    req.end();
  });
}

function seedRoot(dir) {
  fs.writeFileSync(path.join(dir, "index.html"), "<!doctype html><title>dq-index</title>INDEX_MARK", "utf8");
  fs.writeFileSync(path.join(dir, "test-nest.html"), "<!doctype html><title>dq-nest</title>NEST_MARK", "utf8");
  fs.writeFileSync(path.join(dir, "game.js"), "/* GAME_MARK */", "utf8");
  fs.writeFileSync(path.join(dir, "defaults.js"), "/* OLD_DEFAULTS */\n", "utf8");
}

const VALID_SAVE = "globalThis.DOU_QUQU_SHIPPED = { knobs: {} };\n";

async function run() {
  const src = fs.readFileSync(path.join(__dirname, "save-server.js"), "utf8");
  const gameSrc = fs.readFileSync(path.join(__dirname, "game.js"), "utf8");
  const html = fs.readFileSync(path.join(__dirname, "index.html"), "utf8");
  const nestHtml = fs.readFileSync(path.join(__dirname, "test-nest.html"), "utf8");
  const how = fs.readFileSync(path.join(__dirname, "怎么玩.txt"), "utf8");

  ok("S1 default host 0.0.0.0", /DOU_QUQU_SAVE_HOST \|\| "0\.0\.0\.0"/.test(src));
  ok("S1 main listens HOST not 127 only", /server\.listen\(PORT, HOST/.test(src));
  ok("S1 require.main guard", /require\.main === module/.test(src));

  eq("isLoopback 127.0.0.1", S.isLoopback("127.0.0.1"), true);
  eq("isLoopback ::1", S.isLoopback("::1"), true);
  eq("isLoopback mapped", S.isLoopback("::ffff:127.0.0.1"), true);
  eq("isLoopback 127.1", S.isLoopback("127.0.0.2"), true);
  eq("isLoopback lan", S.isLoopback("192.168.1.20"), false);
  eq("isLoopback empty", S.isLoopback(""), false);
  eq("isLoopback link-local", S.isLoopback("169.254.1.1"), false);

  const loopUrls = S.collectListenUrls(8765, "127.0.0.1");
  eq("S2 only loopback count", loopUrls.length, 1);
  eq("S2 loopback href", loopUrls[0].href, "http://127.0.0.1:8765/");
  eq("S2 no phone", loopUrls.some((u) => u.label === "手机"), false);

  const openUrls = S.collectListenUrls(8765, "0.0.0.0");
  ok("S3 has loopback", openUrls.some((u) => u.href === "http://127.0.0.1:8765/"));
  ok("S3 no link-local", openUrls.every((u) => u.href.indexOf("169.254.") < 0));
  const lan = S.collectLanIPv4();
  ok("S3 lan list has no 127", lan.every((ip) => ip.indexOf("127.") !== 0 && ip !== "127.0.0.1"));
  ok("S3 lan list has no 169.254", lan.every((ip) => ip.indexOf("169.254.") < 0));
  if (lan.length) {
    ok("S3 has phone url", openUrls.some((u) => u.label === "手机" && u.href === "http://" + lan[0] + ":8765/"));
  } else {
    console.log("skip S3 phone url (no LAN IPv4 on this machine)");
  }

  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "dq-www-"));
  const leakDir = fs.mkdtempSync(path.join(os.tmpdir(), "dq-leak-"));
  const secret = path.join(leakDir, "secret.txt");
  fs.writeFileSync(secret, "SECRET_SHOULD_NOT_LEAK", "utf8");
  seedRoot(tmp);

  const parentEscape = S.safeFile(tmp, "/../" + path.basename(leakDir) + "/secret.txt");
  eq("S7 safeFile parent ok", parentEscape.ok, false);
  eq("S7 safeFile parent code", parentEscape.code, 403);
  const encoded = S.safeFile(tmp, "/%2e%2e/" + path.basename(leakDir) + "/secret.txt");
  eq("S7 safeFile %2e%2e ok", encoded.ok, false);
  eq("S7 safeFile %2e%2e code", encoded.code, 403);
  const nul = S.safeFile(tmp, "/index.html\0.js");
  eq("S7 null byte", nul.ok, false);

  const server = S.createServer({ root: tmp, host: "127.0.0.1" });
  const port = await listen(server, "127.0.0.1");

  const rootGet = await request({ port, path: "/" });
  eq("S4 GET / status", rootGet.status, 200);
  ok("S4 GET / html", rootGet.body.indexOf("INDEX_MARK") >= 0);
  ok("S4 no-store", String(rootGet.headers["cache-control"] || "").indexOf("no-store") >= 0);

  const nestGet = await request({ port, path: "/test-nest.html" });
  eq("S5 nest status", nestGet.status, 200);
  ok("S5 nest body", nestGet.body.indexOf("NEST_MARK") >= 0);
  const gameGet = await request({ port, path: "/game.js" });
  eq("S5 game.js", gameGet.status, 200);
  ok("S5 game body", gameGet.body.indexOf("GAME_MARK") >= 0);
  const defGet = await request({ port, path: "/defaults.js" });
  eq("S5 defaults.js", defGet.status, 200);

  const missing = await request({ port, path: "/no-such" });
  eq("S6 404", missing.status, 404);

  const trav = await request({ port, path: "/../" + path.basename(leakDir) + "/secret.txt" });
  ok("S7 http parent blocked", trav.status === 403 || trav.status === 404);
  ok("S7 http parent no secret", trav.body.indexOf("SECRET_SHOULD_NOT_LEAK") < 0);
  const travEnc = await request({ port, path: "/%2e%2e/" + path.basename(leakDir) + "/secret.txt" });
  ok("S7 http encoded blocked", travEnc.status === 403 || travEnc.status === 404);
  ok("S7 http encoded no secret", travEnc.body.indexOf("SECRET_SHOULD_NOT_LEAK") < 0);

  const saveOk = await request({
    port,
    path: "/__save",
    method: "POST",
    headers: { "Content-Type": "text/plain; charset=utf-8" },
    body: VALID_SAVE,
  });
  eq("S8 save status", saveOk.status, 200);
  ok("S8 wrote file", fs.readFileSync(path.join(tmp, "defaults.js"), "utf8").indexOf("DOU_QUQU_SHIPPED") >= 0);
  ok("S8 did not touch shipped defaults", fs.readFileSync(path.join(__dirname, "defaults.js"), "utf8").indexOf(VALID_SAVE) < 0);

  fs.writeFileSync(path.join(tmp, "defaults.js"), "/* OLD_DEFAULTS */\n", "utf8");
  const saveBad = await request({
    port,
    path: "/__save",
    method: "POST",
    headers: { "Content-Type": "text/plain; charset=utf-8" },
    body: "not a shipped file",
  });
  eq("S9 invalid 400", saveBad.status, 400);
  eq("S9 file unchanged", fs.readFileSync(path.join(tmp, "defaults.js"), "utf8"), "/* OLD_DEFAULTS */\n");

  const lanInfo = await request({ port, path: "/__lan" });
  eq("S11 __lan status", lanInfo.status, 200);
  let lanJson = null;
  try { lanJson = JSON.parse(lanInfo.body); } catch (_) { lanJson = null; }
  eq("S11 ok", !!(lanJson && lanJson.ok), true);
  ok("S11 urls array", Array.isArray(lanJson && lanJson.urls));
  eq("S11 loopback host urls", (lanJson.urls || []).some((u) => u.label === "手机"), false);

  const opt = await request({ port, path: "/__save", method: "OPTIONS" });
  eq("S12 OPTIONS", opt.status, 204);

  const put = await request({ port, path: "/", method: "PUT" });
  eq("S13 PUT 405", put.status, 405);

  const alias = await request({
    port,
    path: "/save",
    method: "POST",
    headers: { "Content-Type": "text/plain; charset=utf-8" },
    body: VALID_SAVE,
  });
  eq("POST /save alias", alias.status, 200);

  await close(server);

  const deniedServer = S.createServer({
    root: tmp,
    clientAddress: () => "192.168.1.50",
  });
  const deniedPort = await listen(deniedServer, "127.0.0.1");
  fs.writeFileSync(path.join(tmp, "defaults.js"), "/* OLD_DEFAULTS */\n", "utf8");
  const denied = await request({
    port: deniedPort,
    path: "/__save",
    method: "POST",
    headers: { "Content-Type": "text/plain; charset=utf-8" },
    body: VALID_SAVE,
  });
  eq("S10 remote save 403", denied.status, 403);
  ok("S10 error loopback only", denied.body.indexOf("loopback only") >= 0);
  eq("S10 file unchanged", fs.readFileSync(path.join(tmp, "defaults.js"), "utf8"), "/* OLD_DEFAULTS */\n");
  const stillGet = await request({ port: deniedPort, path: "/" });
  eq("S10 GET still 200", stillGet.status, 200);
  await close(deniedServer);

  const a = S.createServer({ root: tmp });
  const busyPort = await listen(a, "127.0.0.1");
  const b = S.createServer({ root: tmp });
  const busyErr = await new Promise((resolve) => {
    b.once("error", resolve);
    b.listen(busyPort, "127.0.0.1");
  });
  eq("S14 EADDRINUSE", busyErr && busyErr.code, "EADDRINUSE");
  await close(a);
  await close(b);

  ok("S15 index lan-hint", html.indexOf('id="lan-hint"') >= 0);
  ok("S15 nest lan-hint", nestHtml.indexOf('id="lan-hint"') >= 0);
  ok("S15 boot fetches __lan", /function showLanHint\(/.test(gameSrc) && gameSrc.indexOf('"/__lan"') >= 0);
  ok("S15 only localhost shows hint", /function isLocalHostName\(/.test(gameSrc) && /location\.hostname/.test(gameSrc));
  ok("S15 save relative first", /new URL\("\/__save", location\.href\)/.test(gameSrc));
  ok("S15 save loopback fallback", gameSrc.indexOf("http://127.0.0.1:8765/__save") >= 0);
  ok("S15 denied copy", gameSrc.indexOf("保存只能在这台电脑上") >= 0);
  ok("S15 how-to phones", how.indexOf("手机用「手机」地址") >= 0);
  const css = fs.readFileSync(path.join(__dirname, "style.css"), "utf8");
  ok("S15 lan-hint hidden css", css.indexOf("#lan-hint.hidden") >= 0);

  const s1 = S.createServer({ root: tmp });
  const s2 = S.createServer({ root: tmp });
  const p1 = await listen(s1, "127.0.0.1");
  const p2 = await listen(s2, "127.0.0.1");
  const pair = await Promise.all([
    request({ port: p1, path: "/" }),
    request({ port: p2, path: "/" }),
  ]);
  eq("S16 phone A", pair[0].status, 200);
  eq("S16 phone B", pair[1].status, 200);
  ok("S16 both see same static", pair[0].body.indexOf("INDEX_MARK") >= 0 && pair[1].body.indexOf("INDEX_MARK") >= 0);
  await close(s1);
  await close(s2);

  if (lan.length) {
    const lanServer = S.createServer({ root: tmp, host: "0.0.0.0" });
    let lanPort = null;
    try {
      lanPort = await listen(lanServer, "0.0.0.0");
      const viaLan = await request({
        hostname: lan[0],
        port: lanPort,
        path: "/",
        timeoutMs: 2500,
      });
      eq("LAN IPv4 GET /", viaLan.status, 200);
      ok("LAN IPv4 body", viaLan.body.indexOf("INDEX_MARK") >= 0);
      const viaLanInfo = await request({
        hostname: lan[0],
        port: lanPort,
        path: "/__lan",
        timeoutMs: 2500,
      });
      const info = JSON.parse(viaLanInfo.body);
      ok("LAN __lan has phone", (info.urls || []).some((u) => u.label === "手机"));
    } catch (err) {
      console.log("skip LAN IPv4 GET (" + lan[0] + "): " + (err && err.message || err));
    }
    await close(lanServer);
  } else {
    console.log("skip LAN IPv4 GET (no adapter)");
  }

  fs.rmSync(tmp, { recursive: true, force: true });
  fs.rmSync(leakDir, { recursive: true, force: true });
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
