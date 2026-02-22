const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = 3456;
const SPRITES_DIR = path.resolve(__dirname, '..', 'Assets', 'Sprites');

class SeededRandom {
  constructor(seed = 12345) {
    this.seed = seed;
  }
  next() {
    this.seed = (this.seed * 1103515245 + 12345) & 0x7fffffff;
    return this.seed / 0x7fffffff;
  }
  range(min, max) { return min + this.next() * (max - min); }
  pick(arr) { return arr[Math.floor(this.next() * arr.length)]; }
}

const PALETTES = {
  grass: {
    bottom: ['#186a1e', '#1a7021', '#207627', '#217d28', '#1e5a18', '#165214'],
    top: ['#1e7a1e', '#228b22', '#2d9633', '#268029', '#1f7523', '#1b6920'],
    front: ['#2ea32e', '#32cd32', '#3cb443', '#4acc52', '#38b840', '#30a838'],
  },
  flower: {
    red: { petals: ['#e63946', '#d62839', '#c91831', '#ff4d5a'], center: '#ff9800' },
    pink: { petals: ['#ff69b4', '#ff1493', '#db7093', '#ffb6c1'], center: '#ffd700' },
    yellow: { petals: ['#ffd700', '#ffec00', '#f4d03f', '#ffdf00'], center: '#8b4513' },
    white: { petals: ['#ffffff', '#f8f8ff', '#f0f0f0', '#fafafa'], center: '#ffeb3b' },
    purple: { petals: ['#9b59b6', '#8e44ad', '#a569bd', '#7d3c98'], center: '#ffc107' },
  },
  tree: {
    trunk: ['#8b4513', '#a0522d', '#6b4423'],
    leaves: ['#228b22', '#2e8b57', '#32cd32', '#3cb371', '#006400'],
    autumn: ['#ff8c00', '#ffa500', '#ff4500', '#dc143c', '#b8860b'],
  },
};

function blade(x, baseY, tipY, width, sway) {
  const hw = width / 2;
  const tx = Math.max(0, Math.min(32, x + hw + sway));
  return `${x.toFixed(1)},${baseY} ${Math.min(32, x + width).toFixed(1)},${baseY} ${tx.toFixed(1)},${tipY.toFixed(1)}`;
}

function genGrass(frame, frames) {
  const sway = Math.sin((frame / frames) * Math.PI * 2) * 1.5;
  const b = [];
  for (let i = 0; i < 16; i++) {
    const tipY = 10 + (i % 5) * 2 + Math.sin(i * 1.3) * 2;
    b.push(`<polygon points="${blade(i*2,32,tipY,2,sway+Math.sin(i*0.7)*0.5)}" fill="${PALETTES.grass.bottom[i%6]}"/>`);
  }
  for (let i = 0; i < 8; i++) {
    const tipY = Math.abs((i % 4) * 1.5) + Math.sin(i * 1.1);
    b.push(`<polygon points="${blade(i*4,20,tipY,2,sway+Math.sin(i*0.9)*0.7)}" fill="${PALETTES.grass.top[i%6]}"/>`);
  }
  for (let i = 0; i < 12; i++) {
    const tipY = 8 + (i % 4) * 2 + Math.sin(i * 1.5) * 2;
    b.push(`<polygon points="${blade(i*2.7+0.5,32,tipY,2,sway+Math.sin(i*1.2)*0.6)}" fill="${PALETTES.grass.front[i%6]}"/>`);
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32"><rect x="0" y="28" width="32" height="4" fill="#1a7021"/>${b.join('')}</svg>`;
}

function genFlower(color, frame, frames) {
  const p = PALETTES.flower[color] || PALETTES.flower.red;
  const sway = Math.sin((frame / frames) * Math.PI * 2) * 0.3;
  const e = [`<ellipse cx="16" cy="30" rx="6" ry="2" fill="rgba(0,0,0,0.15)"/>`];
  e.push(`<path d="M16,32 Q14,24 16,16" stroke="#228b22" stroke-width="2" fill="none"/>`);
  e.push(`<ellipse cx="20" cy="24" rx="4" ry="2" fill="#32cd32" transform="rotate(-20 20 24)"/>`);
  for (let i = 0; i < 5; i++) {
    const a = (i / 5) * Math.PI * 2 - Math.PI / 2;
    const px = 16 + Math.cos(a) * 7 + sway, py = 16 + Math.sin(a) * 7;
    e.push(`<ellipse cx="${px.toFixed(1)}" cy="${py.toFixed(1)}" rx="4" ry="5" fill="${p.petals[i%4]}" transform="rotate(${(a*180/Math.PI+90).toFixed(0)} ${px.toFixed(1)} ${py.toFixed(1)})"/>`);
  }
  e.push(`<circle cx="${16+sway}" cy="16" r="3" fill="${p.center}"/>`);
  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">${e.join('')}</svg>`;
}

function genTree(autumn, frame, frames) {
  const colors = autumn ? PALETTES.tree.autumn : PALETTES.tree.leaves;
  const sway = Math.sin((frame / frames) * Math.PI * 2) * 0.3;
  const e = [`<ellipse cx="16" cy="31" rx="10" ry="3" fill="rgba(0,0,0,0.15)"/>`];
  e.push(`<rect x="13" y="20" width="6" height="12" fill="#8b4513"/>`);
  e.push(`<rect x="13" y="20" width="2" height="12" fill="#a0522d"/>`);
  [[16,10,6],[12,8,5],[8,6,4],[5,4,3]].forEach((l, i) => {
    const s = sway * (i + 1) * 0.3;
    e.push(`<ellipse cx="${16+s}" cy="${l[0]}" rx="${l[1]}" ry="${l[2]}" fill="${colors[i%5]}"/>`);
  });
  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">${e.join('')}</svg>`;
}

const GENERATORS = {
  grass: { fn: genGrass, frames: 5 },
  flower: { fn: (f, n, c) => genFlower(c, f, n), frames: 5, variants: ['red', 'pink', 'yellow', 'white', 'purple'] },
  'tree-green': { fn: (f, n) => genTree(false, f, n), frames: 6 },
  'tree-autumn': { fn: (f, n) => genTree(true, f, n), frames: 6 },
};

function generate(type, variant, seed) {
  const g = GENERATORS[type];
  const frames = [];
  for (let i = 0; i < g.frames; i++) {
    frames.push(g.fn(i, g.frames, variant));
  }
  return frames;
}

const HTML = `<!DOCTYPE html>
<html>
<head>
  <title>Sprite Generator</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: system-ui, sans-serif; background: #1a1a2e; color: #eee; min-height: 100vh; }
    .container { max-width: 1200px; margin: 0 auto; padding: 20px; }
    h1 { margin-bottom: 20px; color: #4cc9f0; }
    .tabs { display: flex; gap: 10px; margin-bottom: 20px; }
    .tab { padding: 10px 20px; background: #16213e; border: none; color: #aaa; cursor: pointer; border-radius: 6px; }
    .tab.active { background: #4cc9f0; color: #1a1a2e; }
    .panel { display: none; }
    .panel.active { display: block; }
    .controls { background: #16213e; padding: 20px; border-radius: 8px; margin-bottom: 20px; }
    .row { display: flex; gap: 15px; align-items: center; flex-wrap: wrap; }
    label { color: #888; font-size: 12px; display: block; margin-bottom: 4px; }
    select, input[type="number"] { padding: 8px 12px; border-radius: 4px; border: 1px solid #333; background: #0f0f23; color: #eee; }
    button { padding: 10px 20px; background: #4cc9f0; border: none; border-radius: 6px; cursor: pointer; font-weight: bold; color: #1a1a2e; }
    button:hover { background: #7dd8f5; }
    button.secondary { background: #333; color: #eee; }
    button.secondary:hover { background: #444; }
    .preview { display: flex; gap: 10px; flex-wrap: wrap; margin-top: 20px; }
    .preview-frame { background: #2a2a4a; padding: 10px; border-radius: 6px; text-align: center; }
    .preview-frame svg { display: block; image-rendering: pixelated; }
    .preview-frame span { font-size: 11px; color: #666; margin-top: 5px; display: block; }
    .gallery { display: grid; grid-template-columns: repeat(auto-fill, minmax(100px, 1fr)); gap: 15px; }
    .gallery-item { background: #16213e; padding: 10px; border-radius: 6px; text-align: center; cursor: pointer; }
    .gallery-item:hover { background: #1f2b4a; }
    .gallery-item img { width: 64px; height: 64px; image-rendering: pixelated; }
    .gallery-item span { font-size: 10px; color: #888; display: block; margin-top: 5px; word-break: break-all; }
    .empty { color: #666; text-align: center; padding: 40px; }
    .status { color: #4cc9f0; font-size: 12px; margin-top: 10px; }
  </style>
</head>
<body>
  <div class="container">
    <h1>Sprite Generator</h1>
    <div class="tabs">
      <button class="tab active" data-tab="generate">Generate</button>
      <button class="tab" data-tab="gallery">Gallery</button>
    </div>
    <div id="generate" class="panel active">
      <div class="controls">
        <div class="row">
          <div>
            <label>Type</label>
            <select id="type">
              <option value="grass">Grass</option>
              <option value="flower">Flower</option>
              <option value="tree-green">Tree (Green)</option>
              <option value="tree-autumn">Tree (Autumn)</option>
            </select>
          </div>
          <div id="variantWrap" style="display:none">
            <label>Color</label>
            <select id="variant">
              <option value="red">Red</option>
              <option value="pink">Pink</option>
              <option value="yellow">Yellow</option>
              <option value="white">White</option>
              <option value="purple">Purple</option>
            </select>
          </div>
          <div>
            <label>Seed</label>
            <input type="number" id="seed" value="12345">
          </div>
          <button onclick="preview()">Preview</button>
          <button class="secondary" onclick="save()">Save All</button>
        </div>
        <div class="status" id="status"></div>
      </div>
      <div class="preview" id="preview"></div>
    </div>
    <div id="gallery" class="panel">
      <div class="controls">
        <button onclick="loadGallery()">Refresh</button>
        <button class="secondary" onclick="clearAll()">Clear All</button>
      </div>
      <div class="gallery" id="galleryGrid"></div>
    </div>
  </div>
  <script>
    const $ = id => document.getElementById(id);
    document.querySelectorAll('.tab').forEach(t => t.onclick = () => {
      document.querySelectorAll('.tab').forEach(x => x.classList.remove('active'));
      document.querySelectorAll('.panel').forEach(x => x.classList.remove('active'));
      t.classList.add('active');
      $(t.dataset.tab).classList.add('active');
      if (t.dataset.tab === 'gallery') loadGallery();
    });
    $('type').onchange = () => {
      $('variantWrap').style.display = $('type').value === 'flower' ? 'block' : 'none';
    };
    async function preview() {
      const type = $('type').value;
      const variant = $('variant').value;
      const seed = $('seed').value;
      const res = await fetch('/api/preview?type=' + type + '&variant=' + variant + '&seed=' + seed);
      const data = await res.json();
      $('preview').innerHTML = data.frames.map((svg, i) => 
        '<div class="preview-frame">' + svg + '<span>Frame ' + i + '</span></div>'
      ).join('');
      $('status').textContent = 'Generated ' + data.frames.length + ' frames';
    }
    async function save() {
      const type = $('type').value;
      const variant = $('variant').value;
      const seed = $('seed').value;
      const res = await fetch('/api/save?type=' + type + '&variant=' + variant + '&seed=' + seed);
      const data = await res.json();
      $('status').textContent = data.message;
    }
    async function loadGallery() {
      const res = await fetch('/api/gallery');
      const files = await res.json();
      const grid = $('galleryGrid');
      if (files.length === 0) {
        grid.innerHTML = '<div class="empty">No sprites yet. Generate some!</div>';
        return;
      }
      grid.innerHTML = files.map(f => 
        '<div class="gallery-item" onclick="download(\\'' + f + '\\')">' +
        '<img src="/sprites/' + f + '" alt="' + f + '">' +
        '<span>' + f + '</span></div>'
      ).join('');
    }
    function download(f) {
      const a = document.createElement('a');
      a.href = '/sprites/' + f;
      a.download = f;
      a.click();
    }
    async function clearAll() {
      if (!confirm('Delete all sprites?')) return;
      await fetch('/api/clear', { method: 'POST' });
      loadGallery();
    }
    preview();
  </script>
</body>
</html>`;

const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  
  if (url.pathname === '/') {
    res.writeHead(200, { 'Content-Type': 'text/html' });
    res.end(HTML);
    return;
  }
  
  if (url.pathname === '/api/preview') {
    const type = url.searchParams.get('type') || 'grass';
    const variant = url.searchParams.get('variant') || 'red';
    const frames = generate(type, variant, 12345);
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ frames }));
    return;
  }
  
  if (url.pathname === '/api/save') {
    const type = url.searchParams.get('type') || 'grass';
    const variant = url.searchParams.get('variant') || 'red';
    const g = GENERATORS[type];
    const base = type === 'flower' ? 'tile_flower_' + variant : 'tile_' + type.replace('-', '_');
    
    if (!fs.existsSync(SPRITES_DIR)) fs.mkdirSync(SPRITES_DIR, { recursive: true });
    
    let count = 0;
    for (let i = 0; i < g.frames; i++) {
      const svg = g.fn(i, g.frames, variant);
      const name = i === 0 ? base + '.svg' : base + '_' + (i - 1) + '.svg';
      fs.writeFileSync(path.join(SPRITES_DIR, name), svg);
      count++;
    }
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ message: 'Saved ' + count + ' files' }));
    return;
  }
  
  if (url.pathname === '/api/gallery') {
    let files = [];
    if (fs.existsSync(SPRITES_DIR)) {
      files = fs.readdirSync(SPRITES_DIR).filter(f => f.endsWith('.svg')).sort();
    }
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(files));
    return;
  }
  
  if (url.pathname === '/api/clear' && req.method === 'POST') {
    if (fs.existsSync(SPRITES_DIR)) {
      fs.readdirSync(SPRITES_DIR).filter(f => f.endsWith('.svg')).forEach(f => {
        fs.unlinkSync(path.join(SPRITES_DIR, f));
      });
    }
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ ok: true }));
    return;
  }
  
  if (url.pathname.startsWith('/sprites/')) {
    const file = path.join(SPRITES_DIR, url.pathname.replace('/sprites/', ''));
    if (fs.existsSync(file)) {
      res.writeHead(200, { 'Content-Type': 'image/svg+xml' });
      res.end(fs.readFileSync(file));
      return;
    }
    res.writeHead(404);
    res.end('Not found');
    return;
  }
  
  res.writeHead(404);
  res.end('Not found');
});

server.listen(PORT, () => {
  console.log('Sprite Generator UI running at http://localhost:' + PORT);
  console.log('Press Ctrl+C to stop');
});
