"use strict";

const crypto = require("crypto");
const { URL } = require("url");

const GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
const MAX_MSG = 256 * 1024;
const FORWARD = { input: 1, state: 1, start: 1, end: 1, hello: 1, ping: 1, rematch: 1 };

function acceptKey(key) {
  return crypto.createHash("sha1").update(String(key) + GUID).digest("base64");
}

function encodeFrame(opcode, payload) {
  const len = payload.length;
  let header;
  if (len < 126) {
    header = Buffer.alloc(2);
    header[0] = 0x80 | opcode;
    header[1] = len;
  } else if (len < 65536) {
    header = Buffer.alloc(4);
    header[0] = 0x80 | opcode;
    header[1] = 126;
    header.writeUInt16BE(len, 2);
  } else {
    header = Buffer.alloc(10);
    header[0] = 0x80 | opcode;
    header[1] = 127;
    header.writeBigUInt64BE(BigInt(len), 2);
  }
  return Buffer.concat([header, payload]);
}

function tryDecode(buf) {
  if (buf.length < 2) return null;
  const opcode = buf[0] & 0x0f;
  const masked = (buf[1] & 0x80) !== 0;
  let len = buf[1] & 0x7f;
  let offset = 2;
  if (len === 126) {
    if (buf.length < 4) return null;
    len = buf.readUInt16BE(2);
    offset = 4;
  } else if (len === 127) {
    if (buf.length < 10) return null;
    const big = buf.readBigUInt64BE(2);
    if (big > BigInt(MAX_MSG)) return { opcode: 8, payload: Buffer.alloc(0), rest: Buffer.alloc(0), overflow: true };
    len = Number(big);
    offset = 10;
  }
  if (len > MAX_MSG) return { opcode: 8, payload: Buffer.alloc(0), rest: Buffer.alloc(0), overflow: true };
  const maskLen = masked ? 4 : 0;
  if (buf.length < offset + maskLen + len) return null;
  let payload = buf.subarray(offset + maskLen, offset + maskLen + len);
  if (masked) {
    const mask = buf.subarray(offset, offset + 4);
    payload = Buffer.from(payload);
    for (let i = 0; i < payload.length; i++) payload[i] ^= mask[i & 3];
  }
  return { opcode, payload, rest: buf.subarray(offset + maskLen + len) };
}

function sendJson(client, obj) {
  if (!client || !client.socket || client.socket.destroyed) return;
  try {
    client.socket.write(encodeFrame(1, Buffer.from(JSON.stringify(obj), "utf8")));
  } catch (_) { /* closed */ }
}

function sendClose(socket, code) {
  if (!socket || socket.destroyed) return;
  const payload = Buffer.alloc(2);
  payload.writeUInt16BE(code || 1000, 0);
  try { socket.write(encodeFrame(8, payload)); } catch (_) { /* ignore */ }
  try { socket.end(); } catch (_) { /* ignore */ }
}

function makeCode(rooms) {
  for (let i = 0; i < 40; i++) {
    const c = String(1000 + crypto.randomInt(9000));
    if (!rooms.has(c)) return c;
  }
  return null;
}

function attachNetRelay(server) {
  const rooms = new Map();

  function dropClient(client, reason) {
    if (!client || client.dead) return;
    client.dead = true;
    const room = client.room;
    if (room) {
      if (room.host === client) {
        if (room.guest) sendJson(room.guest, { type: "peer-left", reason: reason || "host" });
        rooms.delete(room.code);
        if (room.guest) room.guest.room = null;
      } else if (room.guest === client) {
        room.guest = null;
        if (room.host) sendJson(room.host, { type: "peer-left", reason: reason || "guest" });
      }
      client.room = null;
    }
    try { client.socket.destroy(); } catch (_) { /* ignore */ }
  }

  function onMessage(client, raw) {
    let msg;
    try { msg = JSON.parse(raw); } catch (_) { return; }
    if (!msg || typeof msg !== "object") return;
    const type = msg.type;
    if (type === "create") {
      if (client.room) {
        sendJson(client, { type: "error", message: "already in room" });
        return;
      }
      const code = makeCode(rooms);
      if (!code) {
        sendJson(client, { type: "error", message: "no room" });
        return;
      }
      const room = { code, host: client, guest: null };
      rooms.set(code, room);
      client.room = room;
      client.role = "host";
      sendJson(client, { type: "created", code, seat: 0 });
      return;
    }
    if (type === "join") {
      const code = String(msg.code || "").replace(/\D/g, "").slice(0, 4);
      const room = rooms.get(code);
      if (!room || !room.host || room.host.dead) {
        sendJson(client, { type: "error", message: "no room" });
        return;
      }
      if (room.guest) {
        sendJson(client, { type: "error", message: "full" });
        return;
      }
      if (room.host === client) {
        sendJson(client, { type: "error", message: "already host" });
        return;
      }
      room.guest = client;
      client.room = room;
      client.role = "guest";
      sendJson(client, { type: "joined", code, seat: 1 });
      sendJson(room.host, { type: "peer", role: "guest", code });
      return;
    }
    if (type === "leave") {
      dropClient(client, "leave");
      return;
    }
    if (!FORWARD[type]) return;
    const room = client.room;
    if (!room) return;
    const peer = client.role === "host" ? room.guest : room.host;
    if (!peer) return;
    sendJson(peer, msg);
  }

  server.on("upgrade", (req, socket, head) => {
    let url;
    try { url = new URL(req.url || "/", "http://127.0.0.1"); } catch (_) {
      socket.destroy();
      return;
    }
    if (url.pathname !== "/__ws") {
      socket.destroy();
      return;
    }
    const key = req.headers["sec-websocket-key"];
    if (!key || String(req.headers.upgrade || "").toLowerCase() !== "websocket") {
      socket.destroy();
      return;
    }
    const res = [
      "HTTP/1.1 101 Switching Protocols",
      "Upgrade: websocket",
      "Connection: Upgrade",
      "Sec-WebSocket-Accept: " + acceptKey(key),
      "\r\n",
    ].join("\r\n");
    socket.write(res);
    const client = { socket, room: null, role: null, dead: false, buf: Buffer.alloc(0) };
    if (head && head.length) client.buf = Buffer.from(head);
    socket.on("data", (chunk) => {
      client.buf = Buffer.concat([client.buf, chunk]);
      if (client.buf.length > MAX_MSG * 2) {
        sendClose(socket, 1009);
        dropClient(client, "overflow");
        return;
      }
      while (true) {
        const frame = tryDecode(client.buf);
        if (!frame) break;
        client.buf = frame.rest;
        if (frame.overflow) {
          sendClose(socket, 1009);
          dropClient(client, "overflow");
          return;
        }
        if (frame.opcode === 8) {
          dropClient(client, "close");
          return;
        }
        if (frame.opcode === 9) {
          try { socket.write(encodeFrame(10, frame.payload)); } catch (_) { /* ignore */ }
          continue;
        }
        if (frame.opcode === 1) onMessage(client, frame.payload.toString("utf8"));
      }
    });
    socket.on("close", () => dropClient(client, "close"));
    socket.on("error", () => dropClient(client, "error"));
    socket.on("end", () => dropClient(client, "end"));
  });

  server._netRooms = rooms;
  return rooms;
}

module.exports = {
  attachNetRelay,
  acceptKey,
  encodeFrame,
  tryDecode,
};
