"use strict";

const http = require("http");
const fs = require("fs");
const path = require("path");

const ROOT = path.resolve(__dirname);
const PORT = Number(process.env.DOU_QUQU_SAVE_PORT || 8765);
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

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url || "/", "http://127.0.0.1");
  if (req.method === "OPTIONS") {
    send(res, 204, "");
    return;
  }
  if (req.method === "POST" && (url.pathname === "/__save" || url.pathname === "/save")) {
    try {
      const body = await readBody(req);
      if (!body || body.indexOf("DOU_QUQU_SHIPPED") < 0) {
        send(res, 400, JSON.stringify({ ok: false, error: "invalid payload" }), "application/json; charset=utf-8");
        return;
      }
      fs.writeFileSync(path.join(ROOT, "defaults.js"), body, "utf8");
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
  let rel = decodeURIComponent(url.pathname || "/");
  if (rel === "/") rel = "/index.html";
  const file = path.resolve(ROOT, "." + rel.replace(/\\/g, "/"));
  if (file !== ROOT && !file.startsWith(ROOT + path.sep)) {
    send(res, 403, "forbidden");
    return;
  }
  fs.readFile(file, (err, data) => {
    if (err) {
      send(res, 404, "not found");
      return;
    }
    send(res, 200, data, TYPES[path.extname(file)] || "application/octet-stream");
  });
});

server.listen(PORT, "127.0.0.1", () => {
  process.stdout.write("save-server http://127.0.0.1:" + PORT + "/\n");
});
