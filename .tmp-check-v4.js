const fs = require('fs');
const path = require('path');
let file;
function walk(dir) {
  for (const name of fs.readdirSync(dir)) {
    const p = path.join(dir, name);
    if (fs.statSync(p).isDirectory()) walk(p);
    else if (name.endsWith('v4.md')) file = p;
  }
}
walk(process.cwd());
const text = fs.readFileSync(file, 'utf8');
for (const h of ['## 4. 耐力（气力）', '## 5. 出圈', '## 6. 可调配置', '`staminaCost`', '圆环不参与出圈']) {
  if (!text.includes(h)) throw new Error(`missing ${h}`);
}
console.log(file, 'OK');
