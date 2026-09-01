// mobile nav
const hamburger = document.getElementById('hamburger');
const navMobile = document.getElementById('navMobile');
if (hamburger && navMobile) {
  hamburger.addEventListener('click', () => navMobile.classList.toggle('open'));
  navMobile.querySelectorAll('a').forEach(a => a.addEventListener('click', () => navMobile.classList.remove('open')));
}

// reveal on scroll
const observer = new IntersectionObserver((entries) => {
  entries.forEach(e => { if (e.isIntersecting) e.target.classList.add('is-visible'); });
}, { threshold: 0.12 });
document.querySelectorAll('.reveal').forEach(el => observer.observe(el));

// nav blur intensity on scroll
const nav = document.getElementById('nav');
window.addEventListener('scroll', () => {
  const y = window.scrollY;
  if (nav) nav.style.boxShadow = y > 8 ? '0 6px 24px rgba(0,0,0,.22)' : 'none';
}, { passive: true });

// transcript rotation — DUAL: alterna atalho ⇄ voz para o mesmo comando
const dualPairs = [
  { kbd: 'Alt+Y → YouTube', voice: '“abra o youtube em coldplay paradise”', action: '↗ YouTube: <em>coldplay paradise</em> — Alt+Y ou voz' },
  { kbd: 'Alt+V → VS Code', voice: '“abra o vs code”', action: '↗ VS Code — Alt+V ou voz' },
  { kbd: 'Alt+S → Spotify', voice: '“toque lo-fi no spotify”', action: '↗ Spotify: <em>lo-fi</em> — Alt+S ou voz' },
  { kbd: '— só voz', voice: '“aumenta o volume”', action: '↗ Volume +5% — Core Audio (voz)' },
  { kbd: 'Alt+D → Discord', voice: '“abra o discord”', action: '↗ Discord — Alt+D ou voz' },
  { kbd: '— só voz', voice: '“que horas são”', action: '↗ São 14:32 — só voz' },
  { kbd: 'Alt+G → Google', voice: '“pesquise bossa nova no google”', action: '↗ Google: <em>bossa nova</em> — Alt+G ou voz' },
  { kbd: 'Alt+T → Terminal', voice: '“feche o chrome”', action: '↗ Fechei Chrome — voz / atalho do app' },
];
let pi = 0;
const transcript = document.getElementById('transcript');
const micAction = document.getElementById('micAction');
const micLabel = document.getElementById('micLabel');
const micLive = document.getElementById('micLive');
const wave = document.getElementById('wave');

function rotateDual() {
  pi = (pi + 1) % dualPairs.length;
  const p = dualPairs[pi];
  const isVoiceOnly = p.kbd === '— só voz';
  if (transcript) {
    transcript.style.opacity = '0';
    setTimeout(() => {
      // alterna visual entre kbd e voice a cada ciclo para reforçar equivalência
      transcript.textContent = pi % 2 === 0 ? p.kbd : p.voice;
      transcript.style.opacity = '1';
    }, 180);
  }
  if (micLabel) {
    micLabel.style.opacity = '0';
    setTimeout(() => {
      micLabel.textContent = isVoiceOnly ? 'VOZ · F9 SEGURADO' : `ATALHO · ${p.kbd.split('→')[0].trim()}  ⇄  VOZ`;
      micLabel.style.opacity = '1';
    }, 180);
  }
  if (micLive) {
    // alterna badge
    micLive.innerHTML = isVoiceOnly
      ? '<span class="live-dot"></span> OUVINDO'
      : '<span class="live-dot" style="background:#8B7CF7;box-shadow:0 0 0 5px rgba(139,124,247,.20)"></span> OU TECLE';
  }
  if (micAction) {
    micAction.style.opacity = '0';
    setTimeout(() => {
      micAction.innerHTML = p.action;
      micAction.style.opacity = '1';
    }, 180);
  }
  if (wave) {
    wave.style.transform = 'scaleY(1.08)';
    setTimeout(() => wave.style.transform = '', 320);
  }
}
setInterval(rotateDual, 2600);

// smooth anchor
document.querySelectorAll('a[href^="#"]').forEach(a => {
  a.addEventListener('click', (e) => {
    const id = a.getAttribute('href');
    if (!id || id === '#') return;
    const el = document.querySelector(id);
    if (el) { e.preventDefault(); el.scrollIntoView({ behavior: 'smooth', block: 'start' }); }
  });
});

// console demo — DUAL parser: entende Alt+Y e "abra o youtube"
const consoleBody = document.getElementById('consoleBody');
const consoleInput = document.getElementById('consoleInput');
const consoleSend = document.getElementById('consoleSend');

function addLine(html) {
  if (!consoleBody) return;
  const div = document.createElement('div');
  div.className = 'console-line';
  div.innerHTML = html;
  consoleBody.appendChild(div);
  consoleBody.scrollTop = consoleBody.scrollHeight;
}

function normalize(s) {
  return s.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/[,.\-]/g,' ').replace(/\s+/g,' ').trim();
}

// mapa de atalhos reais do config.json
const hotkeyMap = {
  'alt+y': 'YouTube — https://www.youtube.com → também “abra o youtube”',
  'alt+g': 'Google — https://www.google.com.br → também “abra o google” / “pesquise no google”',
  'alt+h': 'WhatsApp Web',
  'alt+i': 'Instagram',
  'alt+f': 'Facebook',
  'alt+x': 'X (Twitter)',
  'alt+k': 'TikTok',
  'alt+n': 'Netflix',
  'alt+m': 'Mercado Livre',
  'alt+l': 'Globo (G1)',
  'alt+v': 'Visual Studio Code → também “abra o vs code”',
  'alt+c': 'Google Chrome',
  'alt+d': 'Discord',
  'alt+w': 'WhatsApp',
  'alt+t': 'Windows Terminal',
  'alt+s': 'Steam / Spotify (site) → Alt+S',
  'alt+e': 'Excel',
  'alt+o': 'Word',
  'alt+p': 'PowerShell',
  'alt+b': 'Bloco de Notas',
};

function simulateCommand(raw) {
  const t = normalize(raw);
  const rawLower = raw.toLowerCase().trim();
  if (!t) return { type:'err', text:'Digite Alt+Y ou “abra o youtube” — mesmo comando, dois gatilhos.' };

  // hotkey direto: Alt+Y
  const hk = rawLower.replace(/\s+/g,'');
  // normaliza "alt + y" "alt y"
  const hkNorm = t.replace(/\s+/g,'').replace('alty','alt+y').replace('alt+','alt+');
  // tenta extrair Alt+letra
  const hkMatch = rawLower.match(/alt\s*\+?\s*([a-z])/i);
  if (hkMatch) {
    const key = 'alt+' + hkMatch[1].toLowerCase();
    if (hotkeyMap[key]) {
      // se tem query junto? ex "Alt+Y coldplay"
      const q = t.replace(/alt\s*\+?\s*[a-z]/,'').replace(/youtube|google|spotify|vs code|visual studio code|chrome|discord/g,'').trim();
      if (q.length > 2) return { type:'ok', text:`⌨ ${key.toUpperCase()} + voz: “${q}” → ${hotkeyMap[key]} com busca {q}` };
      return { type:'ok', text:`⌨ ${key.toUpperCase()} → ${hotkeyMap[key]} (mesmo alvo da voz)` };
    }
  }

  if (t.includes('que horas sao') || t.includes('que horas')) {
    const now = new Date();
    return { type:'ok', text:`🎙 Só voz → São ${now.getHours()}h ${String(now.getMinutes()).padStart(2,'0')} — sem atalho (sistema)` };
  }
  if (t.includes('que dia e hoje') || t.includes('que dia e')) {
    return { type:'ok', text: `🎙 Só voz → ${new Date().toLocaleDateString('pt-BR', { weekday:'long', day:'numeric', month:'long', year:'numeric' })}` };
  }
  if (t.includes('aumenta o volume') || t.includes('aumentar o volume')) return { type:'ok', text:'🎙 Só voz → Volume +5% — Core Audio' };
  if (t.includes('diminui o volume')) return { type:'ok', text:'🎙 Só voz → Volume —5%' };
  if (t.includes('mudo') || t.includes('mutar') || t.includes('sem som')) return { type:'ok', text:'🎙 Só voz → Som mutado' };
  if (t.includes('volume 50') || t.includes('volume cinquenta')) return { type:'ok', text:'🎙 Só voz → Volume 50%' };
  if (t.includes('tema escuro') || t.includes('modo escuro')) return { type:'ok', text:'🎙 Só voz → Tema escuro' };
  if (t.includes('tema claro')) return { type:'ok', text:'🎙 Só voz → Tema claro' };
  if (t.includes('tire um print') || t.includes('screenshot') || t.includes('captura de tela')) return { type:'ok', text:'🎙 Só voz → Print salvo em Imagens/Vox/' };
  if (t.includes('minimize tudo') || t.includes('mostre a area de trabalho')) return { type:'ok', text:'🎙 Só voz → Mostrando área de trabalho (Win+D)' };
  if (t.includes('previsao do tempo') || t.includes('que clima faz')) return { type:'ok', text:'🎙 Só voz → wttr.in — São Paulo +24°C' };
  if (t.match(/me lembre em|lembre em|alarme em|timer de/)) return { type:'ok', text:'🎙 Só voz → Lembrete agendado — “vox cancela”' };
  if (t.includes('leia a area de transferencia') || t.includes('clipboard')) return { type:'ok', text:'🎙 Só voz → Lendo área de transferência' };
  if (t.includes('feche o') || t.includes('fecha o') || t.includes('fechar o')) {
    const name = t.replace(/.*fech[ae].*?o\s*/,'').trim() || 'aplicativo';
    return { type:'ok', text:`⌨/🎙 → Fechei ${name} — via atalho do app ou voz fuzzy` };
  }
  if (t.includes('minimizar') || t.includes('minimize')) return { type:'ok', text:'🎙 → Minimizei — voz / atalho do app' };
  if (t.includes('maximizar') || t.includes('maximiza')) return { type:'ok', text:'🎙 → Maximizei — voz / atalho do app' };
  if (t.includes('foca') || t.includes('traga o')) return { type:'ok', text:'🎙 → Trazendo para frente — voz / atalho' };
  if (t.includes('calcule') || t.includes('quanto e')) return { type:'ok', text:'🎙 Só voz → “calcule 15 vezes 3” = 45' };

  const sites = ['youtube','spotify','google','whatsapp','instagram','facebook','twitter','tiktok','netflix','mercado livre','globo','g1','amazon','wikipedia'];
  const apps = ['vs code','visual studio code','chrome','discord','whatsapp','terminal','steam','excel','word','powershell','bloco de notas'];
  const all = [...sites, ...apps];
  for (const name of all.sort((a,b)=>b.length-a.length)) {
    if (t.includes(name)) {
      const before = t.split(name)[0].replace(/abra|abre|abrir|quero abrir|por favor|o|a|site|app/g,'').trim();
      const after = t.split(name)[1]?.trim() || '';
      const q = `${before} ${after}`.trim();
      const hkHint = name==='youtube'?'Alt+Y':name.includes('vs code')?'Alt+V':name==='spotify'?'Alt+S': name==='google'?'Alt+G':'atalho correspondente';
      if (q.length > 2) return { type:'ok', text:`⌨ ${hkHint} ⇄ 🎙 “${raw}” → mesmo alvo, busca “${q}” via {q}` };
      return { type:'ok', text:`⌨ ${hkHint} ⇄ 🎙 “abra o ${name}” → mesmo comando, dois gatilhos` };
    }
  }
  if (t.includes('abra') || t.includes('abrir') || t.includes('pesquise') || t.includes('toque')) {
    return { type:'action', text:'⌨/🎙 → Adicione em config.json com atalho opcional — vira hotkey + voz automaticamente' };
  }
  return { type:'err', text:'Não reconheci. Tente Alt+Y ou “abra o youtube em lo-fi” — mesmo comando.' };
}

function handleConsole() {
  if (!consoleInput) return;
  const raw = consoleInput.value.trim();
  if (!raw) return;
  addLine(`<span class="c-prompt">›</span> <span style="color:#fff">${raw}</span>`);
  const res = simulateCommand(raw);
  const cls = res.type === 'ok' ? 'c-ok' : res.type === 'action' ? 'c-action' : 'c-err';
  addLine(`<span class="c-prompt">vox ›</span> <span class="${cls}">${res.text}</span>`);
  consoleInput.value = '';
}

if (consoleInput && consoleSend) {
  consoleSend.addEventListener('click', handleConsole);
  consoleInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') handleConsole(); });
}

// demo button scrolls to interactive app preview
const btnDemo = document.getElementById('btnDemo');
if (btnDemo) {
  btnDemo.addEventListener('click', () => {
    const p = document.getElementById('preview');
    if (p) p.scrollIntoView({ behavior:'smooth', block:'start' });
    if (wave) { wave.style.transform='scaleY(1.15)'; setTimeout(()=>wave.style.transform='',400); }
  });
}

// download buttons
function toast(msg) {
  let t = document.getElementById('_toast');
  if (!t) {
    t = document.createElement('div');
    t.id = '_toast';
    t.style.cssText = 'position:fixed;bottom:22px;left:50%;transform:translateX(-50%);background:#161D29;color:#E9EFF7;font-family:JetBrains Mono,monospace;font-size:12px;letter-spacing:.04em;padding:10px 16px;border-radius:99px;border:1px solid #263142;z-index:99;opacity:0;transition:opacity .22s;box-shadow:0 8px 24px rgba(0,0,0,.30)';
    document.body.appendChild(t);
  }
  t.textContent = msg;
  t.style.opacity = '1';
  clearTimeout(t._tm);
  t._tm = setTimeout(()=> t.style.opacity='0', 2200);
}
document.getElementById('dlPrimary')?.addEventListener('click', (e)=>{ e.preventDefault(); toast('Conecte seu repositório — publish.zip será servido aqui'); });
document.getElementById('dlInstaller')?.addEventListener('click', (e)=>{ e.preventDefault(); toast('Gere com ISCC.exe installer.iss'); });

// mic card tilt
const micCard = document.getElementById('micCard');
if (micCard) {
  micCard.addEventListener('mousemove', (e)=>{
    const r = micCard.getBoundingClientRect();
    const x = (e.clientX - r.left)/r.width - .5;
    const y = (e.clientY - r.top)/r.height - .5;
    micCard.style.transform = `perspective(600px) rotateY(${x*4}deg) rotateX(${-y*4}deg)`;
  });
  micCard.addEventListener('mouseleave', ()=> micCard.style.transform='');
  micCard.style.transition='transform .18s';
}

// shot tabs + lightbox (capturas reais)
const shotTabs = document.querySelectorAll('.shot-tab');
const shotAplicativos = document.getElementById('shotAplicativos');
const shotSites = document.getElementById('shotSites');
shotTabs.forEach(btn => {
  btn.addEventListener('click', () => {
    const target = btn.dataset.shot;
    shotTabs.forEach(b => b.classList.toggle('active', b===btn));
    if (shotAplicativos && shotSites) {
      if (target === 'aplicativos') { shotAplicativos.style.display='block'; shotSites.style.display='none'; }
      else { shotAplicativos.style.display='none'; shotSites.style.display='block'; }
    }
  });
});
const lightbox = document.getElementById('lightbox');
const lightboxImg = document.getElementById('lightboxImg');
const lightboxClose = document.getElementById('lightboxClose');
function openLightbox(src, alt){
  if(!lightbox || !lightboxImg) return;
  lightboxImg.src = src;
  lightboxImg.alt = alt||'';
  lightbox.classList.add('open');
  lightbox.setAttribute('aria-hidden','false');
  document.body.style.overflow='hidden';
}
function closeLightbox(){
  if(!lightbox) return;
  lightbox.classList.remove('open');
  lightbox.setAttribute('aria-hidden','true');
  document.body.style.overflow='';
}
document.querySelectorAll('.real-shot-frame, .shot-gallery-frame').forEach(frame=>{
  frame.addEventListener('click', ()=>{
    const img = frame.querySelector('img');
    if(img) openLightbox(img.src, img.alt);
  });
});
lightboxClose?.addEventListener('click', closeLightbox);
lightbox?.addEventListener('click', (e)=>{ if(e.target===lightbox) closeLightbox(); });
document.addEventListener('keydown', (e)=>{ if(e.key==='Escape') closeLightbox(); });

// Highlight gallery item on click — sync with tabs
document.querySelectorAll('.shot-gallery-item').forEach(item=>{
  item.addEventListener('click', ()=>{
    const target = item.dataset.shot;
    shotTabs.forEach(b=> b.classList.toggle('active', b.dataset.shot===target));
    if(shotAplicativos && shotSites){
      if(target==='aplicativos'){ shotAplicativos.style.display='block'; shotSites.style.display='none'; }
      else { shotAplicativos.style.display='none'; shotSites.style.display='block'; }
    }
    // scroll to shot card
    document.querySelector('.real-shot-wrap')?.scrollIntoView({behavior:'smooth', block:'center'});
  });
});

// ===== VOX APP INTERATIVO (réplica WPF) =====
(() => {
  const $ = (s, r=document) => r.querySelector(s);
  const $$ = (s, r=document) => [...r.querySelectorAll(s)];

  // --- dados iniciais (espelho config.json) ---
  const initialData = [
    {description:"Visual Studio Code", target:"%LOCALAPPDATA%\\Programs\\Microsoft VS Code\\Code.exe", category:"app", modifiers:"Alt", key:"V"},
    {description:"Google Chrome", target:"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", category:"app", modifiers:"Alt", key:"C"},
    {description:"Discord", target:"%LOCALAPPDATA%\\Discord\\Update.exe", category:"app", modifiers:"Alt", key:"D"},
    {description:"WhatsApp", target:"shell:AppsFolder\\5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", category:"app", modifiers:"Alt", key:"W"},
    {description:"Windows Terminal", target:"shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", category:"app", modifiers:"Alt", key:"T"},
    {description:"Steam", target:"C:\\Program Files (x86)\\Steam\\steam.exe", category:"app", modifiers:"Alt", key:"S"},
    {description:"Excel", target:"C:\\Program Files\\Microsoft Office\\root\\Office16\\EXCEL.EXE", category:"app", modifiers:"Alt", key:"E"},
    {description:"Word", target:"C:\\Program Files\\Microsoft Office\\root\\Office16\\WINWORD.EXE", category:"app", modifiers:"Alt", key:"O"},
    {description:"PowerShell", target:"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe", category:"app", modifiers:"Alt", key:"P"},
    {description:"Bloco de Notas", target:"C:\\Windows\\System32\\notepad.exe", category:"app", modifiers:"Alt", key:"B"},
    {description:"YouTube", target:"https://www.youtube.com", category:"site", modifiers:"Alt", key:"Y"},
    {description:"Google", target:"https://www.google.com.br", category:"site", modifiers:"Alt", key:"G"},
    {description:"WhatsApp Web", target:"https://web.whatsapp.com", category:"site", modifiers:"Alt", key:"H"},
    {description:"Instagram", target:"https://www.instagram.com", category:"site", modifiers:"Alt", key:"I"},
    {description:"Facebook", target:"https://www.facebook.com", category:"site", modifiers:"Alt", key:"F"},
    {description:"X (Twitter)", target:"https://x.com", category:"site", modifiers:"Alt", key:"X"},
    {description:"TikTok", target:"https://www.tiktok.com", category:"site", modifiers:"Alt", key:"K"},
    {description:"Netflix", target:"https://www.netflix.com", category:"site", modifiers:"Alt", key:"N"},
    {description:"Mercado Livre", target:"https://www.mercadolivre.com.br", category:"site", modifiers:"Alt", key:"M"},
    {description:"Globo (G1)", target:"https://www.globo.com", category:"site", modifiers:"Alt", key:"L"},
  ];

  const fakeApps = [
    "Visual Studio Code","Google Chrome","Discord","WhatsApp","Spotify","Figma","Notion","Slack",
    "VLC media player","Photoshop","Illustrator","Blender","OBS Studio","Steam","Epic Games",
    "Windows Terminal","PowerShell","Bloco de Notas","Calculadora","Paint","Excel","Word","PowerPoint",
    "Telegram","Zoom","Microsoft Edge","Firefox","Brave"
  ];

  let data = (() => {
    try { const s = localStorage.getItem('vox-demo-data'); return s ? JSON.parse(s) : structuredClone(initialData); }
    catch { return structuredClone(initialData); }
  })();
  let history = [];
  let currentTab = 'app';
  let editingIndex = null;
  let addType = 'app';
  let addHotkey = {modifiers:'Alt', key:''}; // empty = sem atalho
  let capturingHotkey = false;

  const appList = $('#appList');
  const appSearch = $('#appSearch');
  const viewList = $('#viewList');
  const viewAdd = $('#viewAdd');
  const viewHistory = $('#viewHistory');
  const viewSettings = $('#viewSettings');
  const addTitle = $('#addTitle');
  const addName = $('#addName');
  const addTarget = $('#addTarget');
  const addNameError = $('#addNameError');
  const addTargetError = $('#addTargetError');
  const addNoHotkey = $('#addNoHotkey');
  const addKeycaps = $('#addKeycaps');
  const previewName = $('#previewName');
  const previewType = $('#previewType');
  const previewIcon = $('#previewIcon');
  const previewHotkeyLabel = $('#previewHotkeyLabel');
  const previewKeycaps = $('#previewKeycaps');
  const previewVoice = $('#previewVoice');
  const previewStatus = $('#previewStatus');
  const addReady = $('#addReady');
  const btnConfirmAdd = $('#btnConfirmAdd');
  const picker = $('#appPicker');
  const pickerList = $('#pickerList');
  const pickerSearch = $('#pickerSearch');
  const micWidget = $('#appMicWidget');
  const micText = $('#appMicText');

  function persist(){ try{ localStorage.setItem('vox-demo-data', JSON.stringify(data)); }catch{} }

  function avatarColor(str){
    let h=0; for(let c of str) h=(h*31 + c.charCodeAt(0))%360;
    return `hsl(${h} 70% 52%)`;
  }
  // --- ícones reais: favicon para sites, brand icon para apps ---
  // ícones: apps via Icons8 color PNG (sem fundo, fiel ao exe), sites via Google S2 favicon (sem fundo)
  const appIconMap = {
    "Visual Studio Code":"https://img.icons8.com/color/48/visual-studio-code-2019.png",
    "Google Chrome":"https://img.icons8.com/color/48/chrome--v1.png",
    "Discord":"https://img.icons8.com/color/48/discord--v2.png",
    "WhatsApp":"https://img.icons8.com/color/48/whatsapp--v1.png",
    "Windows Terminal":"https://img.icons8.com/color/48/console.png",
    "Steam":"https://img.icons8.com/color/48/steam.png",
    "Excel":"https://img.icons8.com/color/48/microsoft-excel-2019--v1.png",
    "Word":"https://img.icons8.com/color/48/microsoft-word-2019--v1.png",
    "PowerShell":"https://img.icons8.com/color/48/powershell.png",
    "Bloco de Notas":"https://img.icons8.com/color/48/notepad.png",
    "Calculadora":"https://img.icons8.com/color/48/calculator.png",
    "Paint":"https://img.icons8.com/color/48/paint.png",
    "PowerPoint":"https://img.icons8.com/color/48/microsoft-powerpoint-2019--v1.png",
    "Spotify":"https://img.icons8.com/color/48/spotify--v1.png",
    "Figma":"https://img.icons8.com/color/48/figma--v1.png",
    "Notion":"https://img.icons8.com/color/48/notion--v1.png",
    "Slack":"https://img.icons8.com/color/48/slack-new.png",
    "VLC media player":"https://img.icons8.com/color/48/vlc.png",
    "Photoshop":"https://img.icons8.com/color/48/adobe-photoshop--v1.png",
    "Illustrator":"https://img.icons8.com/color/48/adobe-illustrator--v1.png",
    "Blender":"https://img.icons8.com/color/48/blender-3d.png",
    "OBS Studio":"https://img.icons8.com/color/48/obs-studio--v1.png",
    "Epic Games":"https://img.icons8.com/color/48/epic-games.png",
    "Telegram":"https://img.icons8.com/color/48/telegram-app--v1.png",
    "Zoom":"https://img.icons8.com/color/48/zoom.png",
    "Microsoft Edge":"https://img.icons8.com/color/48/ms-edge-new.png",
    "Firefox":"https://img.icons8.com/color/48/firefox--v1.png",
    "Brave":"https://img.icons8.com/color/48/brave-web-browser.png"
  };
  const siteDomainMap = {
    "YouTube":"youtube.com",
    "Google":"google.com",
    "WhatsApp Web":"whatsapp.com",
    "Instagram":"instagram.com",
    "Facebook":"facebook.com",
    "X (Twitter)":"x.com",
    "TikTok":"tiktok.com",
    "Netflix":"netflix.com",
    "Mercado Livre":"mercadolivre.com.br",
    "Globo (G1)":"globo.com"
  };
  function extractDomain(target){
    if(!target) return null;
    // se for URL
    try{
      if(target.startsWith('http')){
        const u = new URL(target);
        return u.hostname.replace(/^www\./,'');
      }
      // shell:AppsFolder etc — tenta extrair pelo nome
      return null;
    }catch{ return null; }
  }
  function faviconUrl(domain, sz=64){
    if(!domain) return null;
    return `https://www.google.com/s2/favicons?domain=${domain}&sz=${sz}`;
  }
  function iconForItem(item){
    if(item.icon && item.icon.startsWith('http')) return item.icon;
    // apps: ícone dedicado sem fundo colorido
    if(item.category==='app' && appIconMap[item.description]) return appIconMap[item.description];
    // sites: favicon fiel
    if(item.category==='site'){
      const mapped = siteDomainMap[item.description];
      if(mapped) return faviconUrl(mapped);
      const d = extractDomain(item.target);
      if(d){
        // normaliza subdomínios tipo web.whatsapp.com -> whatsapp.com
        const parts = d.split('.');
        const base = parts.length>2 ? parts.slice(-2).join('.') : d;
        return faviconUrl(base);
      }
    }
    if(item.category==='app'){
      // tenta extrair dominio do target se for url-like (fallback)
      const d2 = extractDomain(item.target);
      if(d2) return faviconUrl(d2);
    }
    return null;
  }
  function avatarHtml(item, sizeClass=''){
    const letter = (item.description||'?').trim()[0]?.toUpperCase()||'?';
    const bg = avatarColor(item.description);
    const iconUrl = iconForItem(item);
    if(iconUrl){
      // apenas ícone, sem fundo — transparente, fiel à marca
      return `<div class="app-card-icon app-card-icon--img ${sizeClass}"><img src="${iconUrl}" alt="" loading="lazy" onerror="this.parentElement.classList.remove('app-card-icon--img'); this.parentElement.style.background='${bg}'; this.parentElement.style.border='none'; this.style.display='none'; this.nextElementSibling.style.display='grid'" /><span style="display:none;place-items:center;width:100%;height:100%;background:${bg};color:#fff;border-radius:8px">${letter}</span></div>`;
    }
    return `<div class="app-card-icon ${sizeClass}" style="background:${bg}">${letter}</div>`;
  }
  function keycapsOf(item){
    if(!item.key) return [];
    const mods = item.modifiers ? item.modifiers.split('+') : [];
    return [...mods, item.key];
  }
  function renderKeycaps(container, caps){
    if(!container) return;
    container.innerHTML = caps.map(k=>`<span class="app-keycap">${k}</span>`).join('');
  }

  function showView(name){
    $$('.app-view').forEach(v=> { v.classList.remove('active'); v.style.display='none'; });
    const map = {list: viewList, add: viewAdd, history: viewHistory, settings: viewSettings};
    const el = map[name];
    if(el){ el.style.display='block'; el.classList.add('active'); }
    $$('.app-nav-pill').forEach(b=> b.classList.toggle('active', b.dataset.view===name));
    if(name==='list') renderList();
    if(name==='history') renderHistory();
  }

  function renderList(){
    if(!appList) return;
    const q = (appSearch?.value||'').toLowerCase().trim();
    let filtered = data.filter(d=> d.category===currentTab);
    if(q) filtered = filtered.filter(d=> (d.description+' '+d.target).toLowerCase().includes(q));

    // empty states
    const emptyApp = $('#appEmptyApp'), emptySite=$('#appEmptySite'), emptySearch=$('#appEmptySearch');
    [emptyApp, emptySite, emptySearch].forEach(e=> e&& (e.style.display='none'));
    appList.style.display = filtered.length ? 'flex' : 'none';

    if(!filtered.length){
      if(q){
        if(emptySearch){ emptySearch.style.display='flex'; $('#appEmptySearchText').textContent = `Sem resultados para “${appSearch.value}”`; }
      } else {
        if(currentTab==='app' && emptyApp) emptyApp.style.display='flex';
        if(currentTab==='site' && emptySite) emptySite.style.display='flex';
      }
      appList.innerHTML='';
      return;
    }

    appList.innerHTML = filtered.map((item, idx) => {
      const realIdx = data.indexOf(item);
      const caps = keycapsOf(item);
      const keysHtml = caps.length ? caps.map(k=>`<span class="app-keycap">${k}</span>`).join('') : `<span class="app-no-keys">Sem atalho</span>`;
      return `<div class="app-card" data-idx="${realIdx}" role="listitem">
        ${avatarHtml(item)}
        <div class="app-card-main"><b>${item.description}</b><span>${item.target}</span></div>
        <div class="app-card-keys">${keysHtml}</div>
        <div class="app-card-actions">
          <button data-act="run">Abrir</button>
          <button data-act="edit">Editar</button>
          <button data-act="remove" class="app-danger">Excluir</button>
        </div>
      </div>`;
    }).join('');

    // bind actions
    $$('.app-card', appList).forEach(card=>{
      const idx = +card.dataset.idx;
      card.querySelector('[data-act="run"]')?.addEventListener('click', ()=> runCommand(idx));
      card.querySelector('[data-act="edit"]')?.addEventListener('click', ()=> openEdit(idx));
      card.querySelector('[data-act="remove"]')?.addEventListener('click', ()=> removeCommand(idx));
      card.addEventListener('dblclick', ()=> runCommand(idx));
    });
  }

  function runCommand(idx){
    const item = data[idx];
    if(!item) return;
    const isSite = item.category==='site';
    const msg = isSite ? `Abrindo ${item.description} → ${item.target}` : `Abrindo ${item.description}`;
    toast(msg);
    history.unshift({name:item.description, target:item.target, time:new Date().toLocaleTimeString('pt-BR',{hour:'2-digit',minute:'2-digit'}), via: Math.random()>.5 ? 'atalho' : 'voz'});
    if(history.length>20) history.pop();
    // flash card
    const card = appList?.querySelector(`[data-idx="${idx}"]`);
    if(card){ card.style.borderColor='var(--focus)'; setTimeout(()=> card.style.borderColor='',600); }
    // simulate opening: if site and has {q} prompt
    if(item.target.includes('{q}')){
      const q = prompt(`Buscar em ${item.description} — digite sua busca (simula “toque X no ${item.description}”):`, '');
      if(q) toast(`Buscando “${q}” em ${item.description} → ${item.target.replace('{q}', encodeURIComponent(q))}`);
    }
    renderHistory();
  }

  function removeCommand(idx){
    const item = data[idx];
    if(!item) return;
    if(!confirm(`Excluir “${item.description}”?`)) return;
    data.splice(idx,1);
    persist();
    renderList();
    toast(`Excluído: ${item.description}`);
  }

  function openAdd(){
    editingIndex = null;
    addTitle.textContent = 'Adicionar aplicativo';
    addName.value=''; addTarget.value='';
    addType='app'; setType('app');
    addHotkey={modifiers:'Alt', key:''};
    updateHotkeyUI();
    updatePreview();
    addNameError.style.display='none'; addTargetError.style.display='none';
    addReady.textContent='Preencha o nome e o destino para continuar';
    btnConfirmAdd.innerHTML='+ Adicionar comando';
    showView('add');
    setTimeout(()=> addName.focus(), 120);
    updateStep(1);
  }
  function openEdit(idx){
    const item = data[idx];
    if(!item) return;
    editingIndex = idx;
    addTitle.textContent = `Editar — ${item.description}`;
    addName.value=item.description;
    addTarget.value=item.target;
    addType=item.category;
    setType(item.category);
    addHotkey={modifiers:item.modifiers||'Alt', key:item.key||''};
    updateHotkeyUI();
    updatePreview();
    showView('add');
    addReady.textContent='Pronto para salvar';
    btnConfirmAdd.textContent='Salvar alterações';
    updateStep(4);
  }

  function setType(t){
    addType=t;
    $$('.app-type-card').forEach(c=> c.classList.toggle('active', c.dataset.type===t));
    previewType.textContent = t==='site' ? 'Site' : t==='app' ? 'Aplicativo' : 'Comando';
    const iconChar = t==='site' ? '◎' : t==='app' ? '◧' : '›_';
    previewIcon.textContent = iconChar;
    updatePreview();
  }

  function updateHotkeyUI(){
    const has = !!addHotkey.key;
    addNoHotkey.style.display = has ? 'none' : 'inline';
    renderKeycaps(addKeycaps, has ? [addHotkey.modifiers, addHotkey.key] : []);
    renderKeycaps(previewKeycaps, has ? [addHotkey.modifiers, addHotkey.key] : []);
    previewHotkeyLabel.textContent = has ? `${addHotkey.modifiers}+${addHotkey.key}` : 'Sem atalho';
    previewHotkeyLabel.style.color = has ? 'var(--fg)' : 'var(--muted-2)';
    updatePreview();
  }

  function updatePreview(){
    const name = addName.value.trim() || 'Novo comando';
    const typeLabel = addType==='site' ? 'Site' : addType==='app' ? 'Aplicativo' : 'Comando';
    previewName.textContent = name;
    previewType.textContent = typeLabel;
    const tmpItem = {description:name, target:addTarget.value.trim()||'https://example.com', category:addType, icon:''};
    const iconUrl = iconForItem(tmpItem);
    const letter = name.trim()[0]?.toUpperCase()||'?';
    const bg = avatarColor(name);
    if(iconUrl){
      previewIcon.innerHTML = `<img src="${iconUrl}" alt="" style="width:22px;height:22px;object-fit:contain" onerror="this.style.display='none'; this.nextElementSibling.style.display='grid'" /><span style="display:none;place-items:center;width:100%;height:100%;background:${bg};color:#fff;border-radius:8px">${letter}</span>`;
      previewIcon.style.background = 'transparent';
      previewIcon.style.border = 'none';
    } else {
      previewIcon.textContent = letter;
      previewIcon.style.background = bg;
      previewIcon.style.border = 'none';
    }
    previewVoice.textContent = `abra o ${name.toLowerCase()}`;
    const ready = addName.value.trim() && addTarget.value.trim();
    previewStatus.textContent = ready ? 'Pronto para usar' : 'Preencha nome e destino';
    previewStatus.style.color = ready ? '#8B7CF7' : 'var(--muted-2)';
    addReady.textContent = ready ? 'Pronto para salvar' : 'Preencha o nome e o destino para continuar';
    btnConfirmAdd.disabled = false;
    btnConfirmAdd.style.opacity = ready ? '1' : '.9';
  }

  function updateStep(n){
    $$('.app-step').forEach(s=> s.classList.toggle('active', +s.dataset.step <= n));
  }

  // picker (apenas ícone, sem fundo)
  function renderPicker(filter=''){
    if(!pickerList) return;
    const q = filter.toLowerCase();
    const list = fakeApps.filter(a=> a.toLowerCase().includes(q)).slice(0,12);
    pickerList.innerHTML = list.map(name=> {
      const tmp = {description:name, category:'app', target:'', icon:''};
      const url = iconForItem(tmp);
      const letter = name[0];
      const bg = avatarColor(name);
      const iconHtml = url ? `<div style="width:28px;height:28px;border-radius:6px;display:grid;place-items:center;overflow:hidden"><img src="${url}" alt="" style="width:18px;height:18px;object-fit:contain" onerror="this.parentElement.style.background='${bg}'; this.parentElement.style.border='1px solid transparent'; this.style.display='none'; this.nextElementSibling.style.display='grid'" /><span style="display:none;place-items:center;color:#fff;font-weight:800;font-size:12px;background:${bg};width:100%;height:100%;border-radius:6px">${letter}</span></div>` : `<div style="width:28px;height:28px;border-radius:6px;background:${bg};display:grid;place-items:center;color:#fff;font-weight:800;font-size:12px">${letter}</div>`;
      return `<div class="app-picker-item" data-name="${name}">${iconHtml}<div><b>${name}</b><span>Programa instalado</span></div></div>`;
    }).join('') || `<div style="padding:12px;color:var(--muted-2);font-size:12px">Nenhum app encontrado</div>`;
    $$('.app-picker-item', pickerList).forEach(el=>{
      el.addEventListener('click', ()=>{
        const name = el.dataset.name;
        addName.value = name;
        addTarget.value = name.toLowerCase().includes('code') ? '%LOCALAPPDATA%\\Programs\\Microsoft VS Code\\Code.exe' : `C:\\Program Files\\${name}\\${name}.exe`;
        picker.style.display='none';
        updatePreview();
      });
    });
  }

  // history (apenas ícone)
  function renderHistory(){
    const list = $('#historyList');
    if(!list) return;
    if(!history.length){
      list.innerHTML = `<div class="app-history-empty">Nenhum histórico ainda — execute um comando na lista (duplo clique ou botão Abrir).</div>`;
      return;
    }
    list.innerHTML = history.map(h=> {
      const tmp = {description:h.name, category: h.target.startsWith('http')?'site':'app', target:h.target};
      const url = iconForItem(tmp);
      const bg = avatarColor(h.name);
      const iconHtml = url ? `<span class="app-card-icon app-card-icon--img" style="width:28px;height:28px;background:transparent;border:none;overflow:hidden"><img src="${url}" alt="" style="width:16px;height:16px;object-fit:contain" onerror="this.parentElement.style.background='${bg}'; this.parentElement.style.border='none'; this.style.display='none'; this.nextElementSibling.style.display='grid'" /><span style="display:none;place-items:center;color:#fff;font-weight:700;font-size:11px;background:${bg};width:100%;height:100%;border-radius:8px">${h.name[0]}</span></span>` : `<span class="app-card-icon" style="width:28px;height:28px;font-size:11px;background:${bg}">${h.name[0]}</span>`;
      return `<div class="app-history-item">${iconHtml}<div style="flex:1"><b>${h.name}</b><span>${h.target}</span></div><span>${h.time}</span><span style="background:var(--accent-ghost);border:1px solid rgba(108,92,231,.14);color:var(--focus);padding:2px 6px;border-radius:99px">${h.via}</span></div>`;
    }).join('');
  }

  // --- bind ---
  $('#btnNewCommand')?.addEventListener('click', openAdd);
  $('#emptyAddApp')?.addEventListener('click', openAdd);
  $('#btnBackList')?.addEventListener('click', ()=> showView('list'));
  $('#btnCancelAdd')?.addEventListener('click', ()=> showView('list'));
  $('#btnBackFromSettings')?.addEventListener('click', ()=> showView('list'));
  $('#btnClearHistory')?.addEventListener('click', ()=>{ history=[]; renderHistory(); toast('Histórico limpo'); });

  $$('.app-nav-pill').forEach(b=> b.addEventListener('click', ()=> showView(b.dataset.view)));
  $$('.app-tab').forEach(b=> b.addEventListener('click', ()=>{
    currentTab = b.dataset.tab;
    $$('.app-tab').forEach(x=> x.classList.toggle('active', x===b));
    renderList();
  }));
  appSearch?.addEventListener('input', renderList);

  // type cards
  $$('.app-type-card').forEach(c=> c.addEventListener('click', ()=> setType(c.dataset.type)));

  // inputs live preview
  addName?.addEventListener('input', updatePreview);
  addTarget?.addEventListener('input', updatePreview);
  addName?.addEventListener('input', ()=> { if(addName.value.trim()) addNameError.style.display='none'; });
  addTarget?.addEventListener('input', ()=> { if(addTarget.value.trim()) addTargetError.style.display='none'; });

  // picker
  $('#btnPickApp')?.addEventListener('click', ()=>{
    const show = picker.style.display==='none' || !picker.style.display;
    picker.style.display = show ? 'block' : 'none';
    if(show){ renderPicker(''); pickerSearch?.focus(); }
  });
  pickerSearch?.addEventListener('input', ()=> renderPicker(pickerSearch.value));

  // hotkey capture
  const btnHotkey = $('#btnHotkey');
  const hotkeyCaptureHint = $('#hotkeyCaptureHint');
  const hotkeyHint = $('#hotkeyHint');
  btnHotkey?.addEventListener('click', ()=>{
    capturingHotkey = true;
    hotkeyCaptureHint.style.display='block';
    hotkeyHint.style.display='none';
    btnHotkey.textContent='⌨ Pressione…';
    btnHotkey.style.background='var(--accent)';
    btnHotkey.style.color='#fff';
    // focus trap
    setTimeout(()=> document.addEventListener('keydown', captureHandler), 60);
  });
  function captureHandler(e){
    if(!capturingHotkey) return;
    e.preventDefault(); e.stopPropagation();
    if(e.key==='Escape'){
      stopCapture();
      return;
    }
    const isAlt = e.altKey;
    const key = e.key.length===1 ? e.key.toUpperCase() : '';
    if(isAlt && key && /^[A-Z]$/.test(key)){
      addHotkey = {modifiers:'Alt', key};
      updateHotkeyUI();
      stopCapture();
      toast(`Atalho definido: Alt+${key}`);
    } else if(key==='Delete' || key==='Backspace'){
      addHotkey={modifiers:'Alt', key:''};
      updateHotkeyUI();
      stopCapture();
      toast('Atalho removido — comando ficará só por voz');
    }
  }
  function stopCapture(){
    capturingHotkey=false;
    hotkeyCaptureHint.style.display='none';
    hotkeyHint.style.display='block';
    btnHotkey.textContent='⌨ Definir teclas';
    btnHotkey.style.background='';
    btnHotkey.style.color='';
    document.removeEventListener('keydown', captureHandler);
  }
  document.addEventListener('keydown', (e)=>{
    if(e.key==='Escape' && capturingHotkey) stopCapture();
  });

  // confirm add
  btnConfirmAdd?.addEventListener('click', ()=>{
    const name = addName.value.trim();
    const target = addTarget.value.trim();
    let ok=true;
    if(!name){ addNameError.style.display='block'; ok=false; }
    else addNameError.style.display='none';
    if(!target){ addTargetError.style.display='block'; ok=false; }
    else addTargetError.style.display='none';
    if(!ok) return;
    const entry = {
      description: name,
      target,
      category: addType,
      modifiers: addHotkey.key ? addHotkey.modifiers : '',
      key: addHotkey.key || ''
    };
    if(editingIndex!==null){
      data[editingIndex]=entry;
      toast(`Atualizado: ${name}`);
    } else {
      data.unshift(entry);
      toast(`Adicionado: ${name} — ${addHotkey.key ? 'Alt+'+addHotkey.key+' ⇄ voz' : 'só voz'}`);
    }
    persist();
    showView('list');
    currentTab = entry.category;
    $$('.app-tab').forEach(b=> b.classList.toggle('active', b.dataset.tab===currentTab));
    renderList();
  });

  // global hotkey inside demo: Alt+letra dispara comando se existir
  document.addEventListener('keydown', (e)=>{
    // ignore when typing in inputs or capturing
    if(capturingHotkey) return;
    const tag = document.activeElement?.tagName;
    if(tag==='INPUT' || tag==='TEXTAREA') return;
    if(e.altKey && e.key.length===1 && /^[a-zA-Z]$/.test(e.key)){
      const k = e.key.toUpperCase();
      const idx = data.findIndex(d=> d.key.toUpperCase()===k && (d.modifiers||'Alt')==='Alt');
      if(idx>-1){
        e.preventDefault();
        runCommand(idx);
        // flash mic widget
        if(micWidget){
          micWidget.style.display='flex';
          micText.textContent = `Alt+${k} → ${data[idx].description}`;
          setTimeout(()=> micWidget.style.display='none', 1800);
        }
      }
    }
    // F9 simulation
    if(e.key==='F9'){
      e.preventDefault();
      simulateVoice();
    }
  });

  // voice simulation for demo — uses Web Speech if available, else prompt
  function simulateVoice(){
    if(micWidget){
      micWidget.style.display='flex';
      micText.textContent='Ouvindo… fale agora';
    }
    const rec = window.SpeechRecognition || window.webkitSpeechRecognition;
    if(rec){
      try{
        const r = new rec();
        r.lang='pt-BR'; r.interimResults=false; r.maxAlternatives=1;
        r.onresult = (ev)=>{
          const text = ev.results[0][0].transcript;
          handleVoiceText(text);
        };
        r.onerror = ()=> handleVoicePrompt();
        r.onend = ()=>{
          setTimeout(()=> { if(micWidget) micWidget.style.display='none'; }, 900);
        };
        r.start();
        setTimeout(()=> { try{r.stop();}catch{} }, 4000);
        return;
      } catch {}
    }
    // fallback prompt
    setTimeout(handleVoicePrompt, 400);
  }
  function handleVoicePrompt(){
    const text = prompt('🎙 Simule a voz — digite o comando falado (ex: “abra o youtube em lo-fi”):', 'abra o youtube');
    if(text) handleVoiceText(text);
    else if(micWidget) micWidget.style.display='none';
  }
  function handleVoiceText(text){
    if(micWidget){ micText.textContent = `“${text}”`; }
    // reuse existing console parser if available, else simple
    const t = text.toLowerCase();
    let found = null;
    // try to find app/site by name
    for(let i=0;i<data.length;i++){
      const name = data[i].description.toLowerCase();
      if(t.includes(name) || t.includes(name.replace('visual studio code','vs code'))){
        found = i; break;
      }
    }
    if(found!==null){
      setTimeout(()=> {
        runCommand(found);
        if(micWidget) micWidget.style.display='none';
      }, 500);
    } else {
      // system commands via toast
      setTimeout(()=>{
        toast(`Voz: “${text}” → executado (simulado)`);
        if(micWidget) micWidget.style.display='none';
      }, 600);
    }
  }
  // expose F9 hint: click mic widget triggers voice
  micWidget?.addEventListener('click', simulateVoice);

  // initial render
  renderList();
  renderHistory();
  updatePreview();
  renderPicker('');

  // expose for ticker demo
  window.voxApp = { data, renderList, simulateVoice };
})();

// echo parallax
let ticking=false;
window.addEventListener('scroll', ()=>{
  if(ticking) return;
  ticking=true;
  requestAnimationFrame(()=>{
    const y=window.scrollY;
    document.querySelectorAll('.echo').forEach((el,i)=>{
      el.style.transform = `translate(${-0.04*(i+1)- y*0.00012*(i+1)}em, ${-0.04*(i+1)- y*0.00008*(i+1)}em)`;
    });
    ticking=false;
  });
},{passive:true});
