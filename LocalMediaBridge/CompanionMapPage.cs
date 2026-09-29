namespace MioCity.LocalMediaBridge;

public static class CompanionMapPage
{
    // The page needs its inline script/style, the tile host, and a WebSocket
    // back to this loopback server only.
    public const string ContentSecurityPolicy =
        "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; " +
        "img-src 'self' https://assets.loaf-scripts.com; " +
        "connect-src ws://127.0.0.1:18765 ws://localhost:18765; " +
        "base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    public static string Create() => Page;

    private const string Page = """
<!doctype html>
<html lang="ja">
<head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="referrer" content="no-referrer">
<title>MioCity Companion Map</title>
<style>
*{box-sizing:border-box}html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#07131b;color:#ecf8fc;font-family:system-ui,"Yu Gothic UI",sans-serif}button{font:inherit}
#map{position:absolute;inset:0;overflow:hidden;cursor:grab;background:#0d2b4f;user-select:none}#map.dragging{cursor:grabbing}.tile{position:absolute;left:0;top:0;width:257px;height:257px;object-fit:cover;pointer-events:none}.shade{position:absolute;inset:0;pointer-events:none;background:radial-gradient(circle at center,transparent 52%,#02070b99 100%)}
.topbar{position:absolute;z-index:10;left:22px;right:22px;top:20px;display:flex;align-items:center;gap:12px}.brand,.panel,.tools{background:#071018e8;border:1px solid #ffffff18;box-shadow:0 14px 40px #0008;backdrop-filter:blur(14px)}.brand{display:flex;align-items:center;gap:12px;padding:11px 16px;border-left:3px solid #0dd4fc}.mark{display:grid;place-items:center;width:31px;height:31px;background:#0dd4fc;color:#061018;font-weight:950}.brand strong{display:block;font-size:14px;letter-spacing:.08em}.brand small{display:block;color:#8ca6b3;font-size:10px;letter-spacing:.12em}.connection{margin-left:auto;padding:10px 14px;color:#ffcc70;font-size:12px}.connection.online{color:#72f0c0}
.tools{position:absolute;z-index:10;right:22px;top:86px;display:flex;flex-direction:column;padding:6px}.tools button{width:40px;height:40px;border:0;border-bottom:1px solid #ffffff12;background:transparent;color:#dff7ff;cursor:pointer;font-size:18px}.tools button:hover,.tools button.active{background:#0dd4fc;color:#061018}.tools button:last-child{border:0}
.panel{position:absolute;z-index:10;left:22px;bottom:22px;width:min(390px,calc(100% - 44px));padding:18px 20px;border-left:3px solid #0dd4fc}.eyebrow{color:#0dd4fc;font-size:10px;font-weight:900;letter-spacing:.18em}.location{margin:5px 0 3px;font-size:22px;font-weight:800}.sub{color:#9db1bb;font-size:12px}.stats{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin-top:15px}.stat{padding:9px 10px;background:#ffffff09;border:1px solid #ffffff0e}.stat span{display:block;color:#78909c;font-size:9px;letter-spacing:.12em}.stat strong{font-size:13px;font-variant-numeric:tabular-nums}.privacy{margin-top:12px;color:#78909c;font-size:10px;line-height:1.5}
.player{position:absolute;left:0;top:0;z-index:9;margin:-17px 0 0 -17px;pointer-events:none}.player{width:34px;height:34px;filter:drop-shadow(0 4px 9px #000)}.player:before{content:"";position:absolute;left:9px;top:9px;width:16px;height:16px;border:3px solid #fff;border-radius:50%;background:#0dd4fc}.player:after{content:"";position:absolute;left:14px;top:-3px;border-left:3px solid transparent;border-right:3px solid transparent;border-bottom:13px solid #fff;transform-origin:3px 20px}.waypoint{width:17px;height:17px;border:3px solid #ff5470;border-radius:50%;box-shadow:0 0 0 4px #ff547033,0 0 16px #ff5470}.disabled{position:absolute;z-index:20;inset:0;display:grid;place-items:center;background:#061018dd}.disabled[hidden]{display:none}.disabled div{max-width:520px;padding:30px;text-align:center;background:#0b1922;border:1px solid #ffffff16;border-top:3px solid #0dd4fc;box-shadow:0 20px 60px #000}.disabled h2{margin:0 0 10px}.disabled p{margin:0;color:#94aab5;line-height:1.7}.layer-menu{position:absolute;z-index:11;right:72px;top:86px;display:none;padding:8px;background:#071018ef;border:1px solid #ffffff18}.layer-menu.open{display:flex}.layer-menu button{padding:9px 13px;border:0;background:transparent;color:#afc2cb;cursor:pointer}.layer-menu button.active{background:#0dd4fc;color:#061018;font-weight:800}
#world{position:absolute;left:0;top:0;will-change:transform}#world.smooth{transition:transform .5s linear}#world.smooth .player{transition:transform .5s linear}#blips{filter:drop-shadow(0 1px 1.5px #000d)}#blips.off{display:none}
.blip{position:absolute;left:0;top:0;width:var(--s,20px);height:var(--s,20px);margin:calc(var(--s,20px)/-2) 0 0 calc(var(--s,20px)/-2);background:var(--c,#fff);-webkit-mask:var(--m) center/contain no-repeat;mask:var(--m) center/contain no-repeat;pointer-events:none;z-index:5}.blip img{display:block;width:100%;height:100%;mix-blend-mode:multiply}
.pin{position:absolute;left:0;top:0;z-index:8;width:26px;height:26px;margin:-26px 0 0 -13px;pointer-events:none;filter:drop-shadow(0 3px 5px #000a)}.pin:before{content:"";position:absolute;inset:0;border-radius:50% 50% 50% 0;background:#c96bff;transform:rotate(-45deg);box-shadow:inset 0 0 0 2px #fff}.pin:after{content:"";position:absolute;left:9px;top:8px;width:8px;height:8px;border-radius:50%;background:#fff}
@media(max-width:650px){.topbar{left:10px;right:10px;top:10px}.brand small{display:none}.connection{font-size:10px}.tools{right:10px;top:70px}.panel{left:10px;bottom:10px;width:calc(100% - 20px)}.location{font-size:17px}}
</style>
</head>
<body>
<main id="map"><div id="world"><div id="tiles"></div><div id="blips"></div><div id="waypoint" class="pin" hidden></div><div id="player" class="player" hidden></div></div><div class="shade"></div></main>
<div class="topbar"><div class="brand"><div class="mark">M</div><div><strong>MioCity Companion Map</strong><small>SECOND SCREEN · READ ONLY</small></div></div><div id="connection" class="brand connection">Bridgeへ接続中…</div></div>
<div class="tools"><button id="follow" class="active" title="現在地を追従">◎</button><button id="layers" title="地図スタイル">▱</button><button id="blipToggle" class="active" title="ブリップを表示">⬤</button><button id="zoomIn" title="拡大">＋</button><button id="zoomOut" title="縮小">−</button><button id="copy" title="座標をコピー">⧉</button><button id="fullscreen" title="全画面">⛶</button></div>
<div id="layerMenu" class="layer-menu"><button data-layer="render" class="active">道路</button><button data-layer="game">ゲーム</button><button data-layer="print">明色</button></div>
<section class="panel"><div class="eyebrow">CURRENT POSITION</div><div id="location" class="location">FiveMを待っています</div><div id="zone" class="sub">MioCity Media Linkから現在地を受信します</div><div class="stats"><div class="stat"><span>SPEED</span><strong id="speed">0 km/h</strong></div><div class="stat"><span>HEADING</span><strong id="heading">0°</strong></div><div class="stat"><span>COORDS</span><strong id="coords">0, 0</strong></div></div><div class="privacy">座標データは127.0.0.1内だけで中継します。背景地図はLB Phoneと同じ外部タイル配信元から取得するため、配信元にはIPと表示タイル番号が伝わります。</div></section>
<div id="disabled" class="disabled"><div><h2>セカンドモニターマップはOFFです</h2><p>FiveMの F9 → Misc → MioCity Media Link で「外部マップを有効にする」をONにしてください。設定はこのプレイヤーのPCに保存されます。</p></div></div>
<script>
(()=>{"use strict";
// Tiles are the same public GTA V map tiles LB Phone uses. Only tile numbers are requested; the coordinate JSON and
// the blip list never leave 127.0.0.1. Blip icons come from this Bridge (/blip/<sprite>.png, cached on this PC).
const TILE_BASE="https://assets.loaf-scripts.com/map-tiles/gtav/main/";
const LAYERS=["render","game","print"];
// GTA blip colour ids -> hex (FiveM blip reference)
const COLORS={0:"#fefefe",1:"#e03232",2:"#71cb71",3:"#5db6e5",4:"#fefefe",5:"#eec64e",6:"#c25050",7:"#9c6eaf",8:"#fe7ac3",9:"#f59d79",10:"#b18f83",11:"#8dcea7",12:"#70a8ae",13:"#d3d1e7",14:"#8f7e98",15:"#6ac4bf",16:"#d5c398",17:"#ea8e50",18:"#97cae9",19:"#b26287",20:"#8f8d79",21:"#a6755e",22:"#afa8a8",23:"#e78d9a",24:"#bbd65b",25:"#0c7b56",26:"#7ac3fe",27:"#ab3ce6",28:"#cda80c",29:"#4561ab",30:"#29a5b8",31:"#b89b7b",32:"#c8e0fe",33:"#f0f096",34:"#ed8ca1",35:"#f98a8a",36:"#fbeea5",37:"#fefefe",38:"#2c6db8",39:"#9a9a9a",40:"#4c4c4c",41:"#f29d9d",42:"#6cb7d6",43:"#afedae",44:"#ffa75f",45:"#f1f1f1",46:"#ecf029",47:"#ff9a18",48:"#f644a5",49:"#e03a3a",50:"#8a6de3",51:"#ff8b5c",52:"#416c41",53:"#b3ddf3",54:"#3a6479",55:"#a0a0a0",56:"#847232",57:"#65b9e7",58:"#4b4175",59:"#e13b3b",60:"#f0cb58",61:"#cd3f98",62:"#cfcfcf",63:"#276a9f",64:"#d87b1b",65:"#8e8393",66:"#f0cb57",67:"#65b9e7",68:"#65b9e7",69:"#79cd79",70:"#efca57",71:"#efca57",72:"#3d3d3d",73:"#efca57",74:"#65b9e7",75:"#e03232",76:"#782323",77:"#65b9e7",78:"#3a6479",79:"#e03232",80:"#65b9e7",81:"#f2a40c",82:"#a4ccaa",83:"#a854f2",84:"#65b9e7",85:"#3d3d3d"};
const $=id=>document.getElementById(id);
const map=$("map"),world=$("world"),tiles=$("tiles"),blipLayer=$("blips"),player=$("player"),waypoint=$("waypoint"),disabled=$("disabled"),connection=$("connection");
let state={active:false,x:450,y:1650,z:0,heading:0,speedKmh:0,street:"",crossing:"",zone:"",inVehicle:false,hasWaypoint:false,waypointX:0,waypointY:0};
const store=(k,v)=>{try{localStorage.setItem(k,v)}catch{}},read=k=>{try{return localStorage.getItem(k)}catch{return null}};
let layer=LAYERS.includes(read("mio-map-layer"))?read("mio-map-layer"):"render";
let showBlips=read("mio-map-blips")!=="0";
let zoom=3,follow=true,center={x:450,y:1650},drag=null,retry=null,mapEnabled=false,gameConnected=false,frame=0,blips=[],blipsDirty=true,placedZoom=-1;
const tileCache=new Map(),blipEls=[];
const U=16384/(9000*128),D=24576/(-13500*128);
const num=v=>Number.isFinite(Number(v))?Number(v):0;
// world pixel of a game coordinate at the current zoom; the #world layer is moved, not every marker
function px(x,y){const s=2**zoom;return{x:s*U*(x+4140),y:s*D*(y-8400)}}
function toGame(x,y){const s=2**zoom;return{x:x/(s*U)-4140,y:y/(s*D)+8400}}
function bearing(){return((360-num(state.heading))%360+360)%360}
function cardinal(b){return["N","NE","E","SE","S","SW","W","NW"][Math.round(b/45)%8]}
function clearTiles(){for(const img of tileCache.values())img.remove();tileCache.clear()}
function render(smooth){if(smooth===false)world.classList.remove("smooth");if(!frame)frame=requestAnimationFrame(()=>{frame=0;draw()})}
function draw(){
  if(!gameConnected||!mapEnabled){clearTiles();player.hidden=true;waypoint.hidden=true;blipLayer.textContent="";blipEls.length=0;placedZoom=-1;return}
  if(follow&&state.active)center={x:num(state.x),y:num(state.y)};
  const c=px(center.x,center.y);
  world.style.transform=`translate(${innerWidth/2-c.x}px,${innerHeight/2-c.y}px)`;
  const zoomChanged=placedZoom!==zoom;
  if(zoomChanged){clearTiles();placedZoom=zoom;blipsDirty=true}
  renderTiles(c);
  const p=px(num(state.x),num(state.y));
  player.hidden=!state.active;
  player.style.transform=`translate(${p.x}px,${p.y}px) rotate(${-num(state.heading)}deg)`;
  if(state.hasWaypoint){const w=px(num(state.waypointX),num(state.waypointY));waypoint.hidden=false;waypoint.style.transform=`translate(${w.x}px,${w.y}px)`}else waypoint.hidden=true;
  if(blipsDirty){blipsDirty=false;renderBlips()}
  $("follow").classList.toggle("active",follow);
  // glide between 500 ms position updates while following; jump when dragging / zooming
  if(follow&&!drag&&!zoomChanged)requestAnimationFrame(()=>world.classList.add("smooth"));else world.classList.remove("smooth");
}
// only tiles around the view exist; they sit at fixed world pixels, so moving the view adds / drops a few at the edges
function renderTiles(c){const left=c.x-innerWidth/2,top=c.y-innerHeight/2,mapScale=2**(7-zoom),cols=Math.ceil(16384/mapScale/256),rows=Math.ceil(24576/mapScale/256),minX=Math.max(0,Math.floor(left/256)-1),maxX=Math.min(cols-1,Math.floor((left+innerWidth)/256)+1),minY=Math.max(0,Math.floor(top/256)-1),maxY=Math.min(rows-1,Math.floor((top+innerHeight)/256)+1),wanted=new Set();for(let y=minY;y<=maxY;y++)for(let x=minX;x<=maxX;x++){const key=`${layer}/${zoom}/${x}/${y}`;wanted.add(key);if(tileCache.has(key))continue;const img=document.createElement("img");img.className="tile";img.alt="";img.draggable=false;img.decoding="async";img.referrerPolicy="no-referrer";img.onerror=()=>{img.style.visibility="hidden"};img.src=`${TILE_BASE}${key}.jpg`;img.style.transform=`translate(${x*256}px,${y*256}px)`;tileCache.set(key,img);tiles.appendChild(img)}for(const [key,img] of tileCache)if(!wanted.has(key)){img.remove();tileCache.delete(key)}}
// blip elements are reused by index; only changed properties are written
function renderBlips(){
  blipLayer.classList.toggle("off",!showBlips);
  if(!showBlips)return;
  const size=zoom>=5?22:zoom>=4?19:16;
  for(let i=0;i<blips.length;i++){
    const b=blips[i];let el=blipEls[i];
    if(!el){el=document.createElement("i");el.className="blip";el.appendChild(new Image());el.firstChild.alt="";el.firstChild.decoding="async";blipEls[i]=el;blipLayer.appendChild(el)}
    const sprite=num(b[0])|0,color=COLORS[num(b[1])|0]||"#ffffff",p=px(num(b[2]),num(b[3])),rot=num(b[4]);
    if(el.__sprite!==sprite){el.__sprite=sprite;const url=`/blip/${sprite}.png`;el.style.setProperty("--m",`url(${url})`);el.firstChild.src=url;el.firstChild.onerror=()=>{el.style.setProperty("--m","none");el.firstChild.style.display="none";el.style.borderRadius="50%";el.style.setProperty("--s","10px")}}
    if(el.__color!==color){el.__color=color;el.style.setProperty("--c",color)}
    el.style.setProperty("--s",size+"px");
    el.style.transform=`translate(${p.x}px,${p.y}px)`+(rot?` rotate(${-rot}deg)`:"");
  }
  while(blipEls.length>blips.length)blipEls.pop().remove();
}
function updateInfo(){const street=[state.street,state.crossing].filter(Boolean).join(" × ");$("location").textContent=street||"現在地を取得中";$("zone").textContent=state.zone||(state.inVehicle?"車両で移動中":"MioCity");$("speed").textContent=Math.round(num(state.speedKmh))+" km/h";const b=bearing();$("heading").textContent=Math.round(b)%360+"° "+cardinal(b);$("coords").textContent=`${num(state.x).toFixed(1)}, ${num(state.y).toFixed(1)}`;render()}
function status(){disabled.hidden=gameConnected&&mapEnabled;connection.textContent=!gameConnected?"● FiveM未接続 · 自動休止中":mapEnabled?"● FiveM接続済み":"● マップOFF";connection.classList.toggle("online",gameConnected&&mapEnabled);render(false)}
function connect(){clearTimeout(retry);const protocol=location.protocol==="https:"?"wss":"ws";const socket=new WebSocket(`${protocol}://${location.host}/map-view${location.search}`);socket.onmessage=e=>{let m;try{m=JSON.parse(e.data)}catch{return}const d=m&&m.data||{};if(m.type==="bridge.status"){gameConnected=d.connected===true;mapEnabled=d.mapEnabled===true;status()}else if(m.type==="map.state"){state=d;updateInfo()}else if(m.type==="map.blips"){blips=Array.isArray(d.blips)?d.blips:[];blipsDirty=true;render()}};socket.onclose=()=>{gameConnected=false;status();retry=setTimeout(connect,2500)};socket.onerror=()=>socket.close()}
function endDrag(){drag=null;map.classList.remove("dragging")}
map.addEventListener("pointerdown",e=>{drag={x:e.clientX,y:e.clientY,center:{...center}};follow=false;world.classList.remove("smooth");map.classList.add("dragging");map.setPointerCapture(e.pointerId)});
map.addEventListener("pointermove",e=>{if(!drag)return;const c=px(drag.center.x,drag.center.y);center=toGame(c.x-(e.clientX-drag.x),c.y-(e.clientY-drag.y));render(false)});
map.addEventListener("pointerup",endDrag);map.addEventListener("pointercancel",endDrag);
map.addEventListener("wheel",e=>{e.preventDefault();zoom=Math.max(2,Math.min(7,zoom+(e.deltaY<0?1:-1)));render(false)},{passive:false});
$("follow").onclick=()=>{follow=true;render(false)};$("zoomIn").onclick=()=>{zoom=Math.min(7,zoom+1);render(false)};$("zoomOut").onclick=()=>{zoom=Math.max(2,zoom-1);render(false)};
$("copy").onclick=()=>navigator.clipboard?.writeText(`${num(state.x).toFixed(2)}, ${num(state.y).toFixed(2)}, ${num(state.z).toFixed(2)}`).catch(()=>{});
$("fullscreen").onclick=()=>document.fullscreenElement?document.exitFullscreen():document.documentElement.requestFullscreen().catch(()=>{});
$("layers").onclick=()=>$("layerMenu").classList.toggle("open");
$("blipToggle").classList.toggle("active",showBlips);$("blipToggle").onclick=()=>{showBlips=!showBlips;store("mio-map-blips",showBlips?"1":"0");$("blipToggle").classList.toggle("active",showBlips);blipsDirty=true;render(false)};
document.querySelectorAll("[data-layer]").forEach(b=>{b.classList.toggle("active",b.dataset.layer===layer);b.onclick=()=>{layer=b.dataset.layer;store("mio-map-layer",layer);document.querySelectorAll("[data-layer]").forEach(x=>x.classList.toggle("active",x===b));clearTiles();render(false)}});
addEventListener("resize",()=>render(false));status();connect();
})();
</script>
</body></html>
""";
}
