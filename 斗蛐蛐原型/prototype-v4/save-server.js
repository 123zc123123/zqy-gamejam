"use strict";

const http = require("http");
const fs = require("fs");
const os = require("os");
const path = require("path");

const { attachNetRelay } = require("./net-relay.js");

const ROOT = path.resolve(__dirname);
const PORT = Number(process.env.DOU_QUQU_SAVE_PORT || 8765);
const HOST = process.env.DOU_QUQU_SAVE_HOST || "0.0.0.0";
const TYPES = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".txt": "text/plain; charset=utf-8",
  ".png": "image/png",
  ".jpg": "image/jpeg",
};

function send(res, code, body, type) {
  res.writeHead(code, {
    "Content-Type": type || "text/plain; charset=utf-8",
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
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

function isLoopback(addr) {
  if (!addr) return false;
  const a = String(addr).replace(/^::ffff:/i, "").replace(/^\[|\]$/g, "");
  if (a === "::1" || a === "localhost") return true;
  if (a === "127.0.0.1") return true;
  return /^127\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(a);
}

function isLinkLocalIPv4(addr) {
  return /^169\.254\./.test(addr);
}

function isLoopbackHost(host) {
  const h = String(host || "").replace(/^\[|\]$/g, "");
  return h === "127.0.0.1" || h === "::1" || h === "localhost";
}

function collectLanIPv4() {
  const out = [];
  const ifs = os.networkInterfaces();
  for (const name of Object.keys(ifs)) {
    for (const info of ifs[name] || []) {
      if (!info || info.internal) continue;
      const family = info.family;
      if (family !== "IPv4" && family !== 4) continue;
      const ip = info.address;
      if (!ip || isLoopback(ip) || isLinkLocalIPv4(ip)) continue;
      if (out.indexOf(ip) < 0) out.push(ip);
    }
  }
  return out;
}

function collectListenUrls(port, host) {
  const p = Number(port);
  const urls = [{ label: "本机", href: "http://127.0.0.1:" + p + "/" }];
  if (isLoopbackHost(host)) return urls;
  for (const ip of collectLanIPv4()) {
    urls.push({ label: "手机", href: "http://" + ip + ":" + p + "/" });
  }
  return urls;
}

function safeFile(root, relPath) {
  let rel = relPath || "/";
  try {
    rel = decodeURIComponent(rel);
  } catch (_) {
    return { ok: false, code: 400, body: "bad path" };
  }
  rel = rel.replace(/\\/g, "/");
  if (rel === "/") rel = "/index.html";
  if (rel.indexOf("\0") >= 0) return { ok: false, code: 400, body: "bad path" };
  const file = path.resolve(root, "." + rel);
  const rootResolved = path.resolve(root);
  const relToRoot = path.relative(rootResolved, file);
  if (!relToRoot || relToRoot.startsWith("..") || path.isAbsolute(relToRoot)) {
    return { ok: false, code: 403, body: "forbidden" };
  }
  return { ok: true, file };
}

function createServer(options) {
  const opts = options || {};
  const root = path.resolve(opts.root || ROOT);
  const getClientAddress = typeof opts.clientAddress === "function"
    ? opts.clientAddress
    : function (req) { return (req.socket && req.socket.remoteAddress) || ""; };

  const server = http.createServer(async (req, res) => {
    let url;
    try {
      url = new URL(req.url || "/", "http://127.0.0.1");
    } catch (_) {
      send(res, 400, "bad url");
      return;
    }
    if (req.method === "OPTIONS") {
      send(res, 204, "");
      return;
    }
    if (req.method === "GET" && url.pathname === "/__lan") {
      const addr = server.address();
      const port = addr && addr.port ? addr.port : Number(opts.port || PORT);
      const host = (addr && addr.address) || opts.host || HOST;
      const urls = collectListenUrls(port, host);
      send(res, 200, JSON.stringify({
        ok: true,
        port,
        host,
        urls,
      }), "application/json; charset=utf-8");
      return;
    }
    if (req.method === "POST" && (url.pathname === "/__save" || url.pathname === "/save")) {
      if (!isLoopback(getClientAddress(req))) {
        send(res, 403, JSON.stringify({ ok: false, error: "loopback only" }), "application/json; charset=utf-8");
        return;
      }
      try {
        const body = await readBody(req);
        if (!body || body.indexOf("DOU_QUQU_SHIPPED") < 0) {
          send(res, 400, JSON.stringify({ ok: false, error: "invalid payload" }), "application/json; charset=utf-8");
          return;
        }
        fs.writeFileSync(path.join(root, "defaults.js"), body, "utf8");
        send(res, 200, JSON.stringify({ ok: true, file: "defaults.js" }), "application/json; charset=utf-8");
      } catch (err) {
        send(res, 500, JSON.stringify({ ok: false, error: String(err && err.message || err) }), "application/json; charset=utf-8");
      }
      return;
    }
    if (req.method !== "GET") {
      send(res, 405, "method not allowed");
      return;
    }
    const mapped = safeFile(root, url.pathname || "/");
    if (!mapped.ok) {
      send(res, mapped.code, mapped.body);
      return;
    }
    fs.readFile(mapped.file, (err, data) => {
      if (err) {
        send(res, 404, "not found");
        return;
      }
      send(res, 200, data, TYPES[path.extname(mapped.file)] || "application/octet-stream");
    });
  });
  attachNetRelay(server);
  return server;
}

function printBanner(port, host) {
  const urls = collectListenUrls(port, host);
  const phones = urls.filter((u) => u.label === "手机");
  process.stdout.write("save-server\n");
  for (const u of urls) {
    const tag = (u.label + "    ").slice(0, 4);
    process.stdout.write("  " + tag + "  " + u.href + "\n");
  }
  if (!isLoopbackHost(host) && phones.length === 0) {
    process.stdout.write("  手机    （没找到局域网地址。同一 Wi-Fi，或开手机热点；防火墙放行 Node）\n");
  } else if (phones.length) {
    const base = phones[0].href.replace(/\/$/, "");
    process.stdout.write("  巢穴    " + base + "/test-nest.html\n");
  }
  process.stdout.write("  电脑用本机地址打开；手机用「手机」地址，竖屏。保存只在这台电脑生效。\n");
  process.stdout.write("  联机    两人打开同一地址。一人点「开房对打」，另一人点「加入」输入房号。\n");
}

if (require.main === module) {
  const server = createServer({ root: ROOT, host: HOST, port: PORT });
  server.on("error", (err) => {
    const msg = err && err.code === "EADDRINUSE"
      ? "端口被占用: " + HOST + ":" + PORT
      : String((err && err.message) || err);
    process.stderr.write(msg + "\n");
    process.exit(1);
  });
  server.listen(PORT, HOST, () => {
    printBanner(PORT, HOST);
  });
}

module.exports = {
  PORT,
  HOST,
  ROOT,
  TYPES,
  isLoopback,
  isLoopbackHost,
  collectLanIPv4,
  collectListenUrls,
  safeFile,
  createServer,
  printBanner,
  attachNetRelay,
};
