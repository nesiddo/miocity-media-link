namespace MioCity.LocalMediaBridge;

public static class CompanionMapPage
{
    // The page needs its inline script/style and a WebSocket back to this loopback server. Images: this server (blip
    // icons, recoloured by the page into blob: images), the public GTA V tiles (LB Phone's host) and the game server's own
    // "番地" tiles, whose address the game sends in map.config (any http(s) host, so img-src cannot list it in advance).
    // Every text the page shows from data is escaped or set as text, so no script can be injected to use that.
    public const string ContentSecurityPolicy =
        "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; " +
        "img-src 'self' blob: https: http:; " +
        "connect-src 'self' ws://127.0.0.1:18765 ws://localhost:18765; " +
        "base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    public static string Create() => Page;

    private const string Page = """
<!doctype html>
<html lang="ja">
<head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="referrer" content="no-referrer">
<title>MioCity Companion Map</title>
<style>
:root{--bg:#0a131af2;--bg2:#0f1c25;--line:#ffffff16;--ink:#e8f6fb;--mute:#8fa7b2;--acc:#0dd4fc;--red:#ff5470;--ok:#72f0c0}
*{box-sizing:border-box}:focus-visible{outline:2px solid var(--acc);outline-offset:2px}html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#07131b;color:var(--ink);font-family:system-ui,"Yu Gothic UI",sans-serif}button,input,textarea{font:inherit;color:inherit}button:disabled{opacity:.35;cursor:default!important}
.card{background:var(--bg);border:1px solid var(--line);box-shadow:0 12px 32px #0009;border-radius:12px}
svg.i{width:20px;height:20px;fill:none;stroke:currentColor;stroke-width:2;stroke-linecap:round;stroke-linejoin:round;flex:none}
#map{position:absolute;inset:0;overflow:hidden;cursor:grab;background:#0d2b4f;user-select:none}#map.dragging{cursor:grabbing}#map.draw{cursor:crosshair}#map.draw.dragging{cursor:grabbing}
.tile{position:absolute;left:0;top:0;width:257px;height:257px;object-fit:cover;pointer-events:none}
#world{position:absolute;left:0;top:0;will-change:transform}#world.smooth{transition:transform .5s linear}#world.smooth .player,#world.smooth .unit{transition:transform .5s linear}
#shapes{position:absolute;left:0;top:0;overflow:visible;pointer-events:none}#shapes .it polygon,#shapes .it .hit,#shapes .it text{pointer-events:visiblePainted;cursor:pointer}#shapes .hit{pointer-events:stroke!important}
#shapes text{font:700 13px system-ui,"Yu Gothic UI",sans-serif;fill:#fff;stroke:#000b;stroke-width:3px;paint-order:stroke;text-anchor:middle;dominant-baseline:middle}
#shapes .sel polygon,#shapes .sel .ln{filter:drop-shadow(0 0 4px #fff) drop-shadow(0 0 2px var(--acc))}#shapes .draft{pointer-events:none}
#map.draw #shapes *{pointer-events:none!important}
#blips.off,#units.off,#items.off,#shapes.off{display:none}
.blip{position:absolute;left:0;top:0;z-index:5;cursor:default;user-select:none}.blip.hide{display:none}
.player{position:absolute;left:0;top:0;z-index:9;width:44px;height:44px;margin:-22px 0 0 -22px;pointer-events:none}.player i{position:absolute;inset:0;transform-origin:22px 22px}.player i:before{content:"";position:absolute;left:12px;top:12px;width:20px;height:20px;box-sizing:border-box;border:3px solid #fff;border-radius:50%;background:var(--acc);box-shadow:0 0 0 2px #061018,0 3px 10px #000c}.player i:after{content:"";position:absolute;left:16px;top:-3px;border-left:6px solid transparent;border-right:6px solid transparent;border-bottom:15px solid #fff;filter:drop-shadow(0 0 1.5px #061018)}
.wp{position:absolute;left:0;top:0;z-index:8;pointer-events:none}.wp i{position:absolute;left:-13px;top:-26px;width:26px;height:26px;border-radius:50% 50% 50% 0;transform:rotate(-45deg);background:#c96bff;box-shadow:inset 0 0 0 2px #fff,0 3px 6px #000a}.wp i:after{content:"";position:absolute;left:9px;top:9px;width:8px;height:8px;border-radius:50%;background:#fff}.wp b{position:absolute;left:16px;top:-26px;padding:2px 7px;border-radius:5px;background:#2a1240e8;border:1px solid #c96bff88;font-size:12px;font-weight:800;white-space:nowrap;font-variant-numeric:tabular-nums}.wp b:empty{display:none}
.unit{position:absolute;left:0;top:0;z-index:7}.unit i{position:absolute;left:-11px;top:-11px;width:22px;height:22px;background:var(--c,#5db6e5);-webkit-mask:var(--m) center/contain no-repeat;mask:var(--m) center/contain no-repeat;filter:drop-shadow(0 1px 2px #000)}.unit b{position:absolute;left:13px;top:-9px;padding:1px 6px;border-radius:4px;background:#061018d8;border-left:3px solid var(--c,#5db6e5);font-size:11px;font-weight:700;white-space:nowrap}.small .unit b,.small .mk b{display:none}
/* markers and text placed by the player */
.mk{position:absolute;left:0;top:0;z-index:8;cursor:pointer}.mk span{position:absolute;left:calc(var(--z)/-2);top:calc(var(--z)*-1.15);width:var(--z);height:var(--z);display:grid;place-items:center;border-radius:50% 50% 50% 0;transform:rotate(-45deg);background:var(--c);box-shadow:inset 0 0 0 2px #fff,0 3px 6px #000a}.mk span svg{width:58%;height:58%;transform:rotate(45deg);fill:none;stroke:var(--on,#061018);stroke-width:2.4;stroke-linecap:round;stroke-linejoin:round}
.mk b{position:absolute;left:calc(var(--z)/2 + 2px);top:calc(var(--z)*-1.15);padding:2px 7px;border-radius:4px;background:#061018e0;font-size:12px;font-weight:700;white-space:nowrap;border-left:3px solid var(--c)}
.mk.sel span{box-shadow:inset 0 0 0 2px #fff,0 0 0 3px var(--acc),0 3px 10px #000}
.tx{position:absolute;left:0;top:0;z-index:8;transform-origin:0 0;cursor:pointer}.tx b{position:absolute;transform:translate(-50%,-50%);white-space:pre;font-weight:800;line-height:1.2;color:var(--c);text-shadow:0 0 3px #000,0 0 2px #000,0 1px 2px #000}.tx.sel b{outline:2px dashed var(--acc);outline-offset:4px;border-radius:3px}
#map.draw .mk,#map.draw .tx{pointer-events:none}
/* right toolbar (map controls) */
.tools{position:absolute;z-index:14;right:18px;top:18px;display:flex;flex-direction:column;gap:8px}.tools .grp{display:flex;flex-direction:column;overflow:hidden;padding:0}
.tb{position:relative;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:3px;width:58px;height:52px;border:0;border-bottom:1px solid #ffffff10;background:transparent;color:#cfe9f2;cursor:pointer}.tb:last-child{border-bottom:0}.tb[hidden]{display:none}
.tb span{font-size:10.5px;font-weight:700;color:#9fb8c3;white-space:nowrap}.tb span.sm{font-size:9.5px;letter-spacing:-.03em}
.tb:hover{background:#ffffff12;color:#fff}.tb:hover span{color:#fff}.tb.active{background:var(--acc);color:#061018}.tb.active span{color:#061018}
.badge{position:absolute;right:7px;top:5px;min-width:17px;height:17px;padding:0 5px;border-radius:9px;background:var(--red);color:#fff;font-size:10.5px;font-style:normal;line-height:17px;font-weight:800}
/* left drawing palette */
.pal{position:absolute;z-index:13;left:18px;top:50%;transform:translateY(-50%);display:flex;flex-direction:column;gap:3px;padding:6px;border-radius:14px}
.pal button{position:relative;display:grid;place-items:center;width:44px;height:44px;border:0;border-radius:10px;background:transparent;color:#cfe9f2;cursor:pointer}.pal button:hover:not(:disabled){background:#ffffff12;color:#fff}
.pal button kbd{position:absolute;right:3px;bottom:2px;font:700 8.5px ui-monospace,Consolas,monospace;color:#6f8893}
.pal button.active{background:var(--acc);color:#061018}.pal button.active kbd{color:#06101899}
.pal hr{width:28px;margin:4px auto;border:0;border-top:1px solid #ffffff1c}
.flyout{position:absolute;z-index:13;left:82px;top:50%;transform:translateY(-50%);width:min(260px,calc(100% - 100px));max-height:calc(100% - 140px);overflow:auto;padding:14px 16px;border-left:3px solid var(--acc)}.flyout[hidden]{display:none}
.flyout .fh{display:flex;align-items:baseline;justify-content:space-between;gap:8px;margin-bottom:4px;font-size:14px;font-weight:800}.flyout .fh kbd{font:700 11px ui-monospace,Consolas,monospace;color:#7f98a3}
.flyout .tip{margin:0;color:#9fb8c3;font-size:12px;line-height:1.55}.flyout .seg{margin-top:10px}
.olab{margin:10px 0 6px;color:#7f98a3;font-size:11px;font-weight:700;letter-spacing:.08em}.olab:first-of-type{margin-top:0}
.sws{display:flex;flex-wrap:wrap;gap:7px;align-items:center}.sw{width:26px;height:26px;border:2px solid #ffffff30;border-radius:50%;cursor:pointer;padding:0}.sw.on{border-color:#fff;box-shadow:0 0 0 2px var(--acc)}
.swc{position:relative;width:26px;height:26px;border:2px dashed #ffffff55;border-radius:50%;overflow:hidden;cursor:pointer}.swc input{position:absolute;inset:-6px;width:40px;height:40px;opacity:0;cursor:pointer}.swc:after{content:"+";position:absolute;inset:0;display:grid;place-items:center;font-weight:800;color:#cfe9f2;pointer-events:none}
.icons{display:flex;flex-wrap:wrap;gap:5px}.icons button{display:flex;align-items:center;gap:5px;padding:5px 9px 5px 7px;border:1px solid #ffffff14;border-radius:8px;background:#ffffff06;cursor:pointer;font-size:12px;color:#b9cfd8}.icons button svg{width:15px;height:15px;fill:none;stroke:currentColor;stroke-width:2;stroke-linecap:round;stroke-linejoin:round}.icons button:hover{background:#ffffff12}.icons button.on{background:#0dd4fc22;border-color:#0dd4fc77;color:#fff}
.range{display:flex;align-items:center;gap:12px}.range input{flex:1;accent-color:var(--acc)}.range b{min-width:44px;text-align:right;font-variant-numeric:tabular-nums;font-size:13px}
/* bottom-left status */
.sbar{position:absolute;z-index:10;left:18px;bottom:18px;display:flex;align-items:center;gap:0;padding:0;border-radius:10px;overflow:hidden}
.sbar>*{display:flex;align-items:center;gap:7px;height:38px;padding:0 13px;border:0;border-right:1px solid #ffffff12;background:transparent;font-size:12.5px;font-weight:700;color:#b9cfd8;white-space:nowrap}.sbar>*:last-child{border-right:0}
.stat:before{content:"";width:8px;height:8px;border-radius:50%;background:#ffcc70}.stat.on:before{background:var(--ok);box-shadow:0 0 0 3px #72f0c033}
.sbar button{cursor:pointer}.sbar button:hover{background:#ffffff10;color:#fff}.sbar button.active{background:#0dd4fc22;color:var(--acc)}.sbar .pk{font-size:10px;letter-spacing:.12em;color:#7f98a3}
.coords{min-width:150px;font:600 12px ui-monospace,Consolas,monospace!important;font-variant-numeric:tabular-nums;white-space:pre;color:#8fa7b2!important}
/* location card */
.loc{position:absolute;z-index:10;left:18px;top:18px;max-width:min(460px,calc(100% - 110px));padding:12px 16px 12px 14px;display:flex;gap:12px;align-items:flex-start}
.loc[hidden]{display:none}.dot{flex:none;width:10px;height:10px;margin-top:9px;border-radius:50%;background:#ffcc70;box-shadow:0 0 0 3px #ffcc7033}.dot.on{background:var(--ok);box-shadow:0 0 0 3px #72f0c033}
.loc .main{font-size:19px;font-weight:800;line-height:1.3;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.postal{display:inline-block;margin-right:8px;padding:0 7px;border-radius:5px;background:var(--acc);color:#061018;font-size:14px;font-weight:900;vertical-align:2px}
.loc .sub{margin-top:3px;color:var(--mute);font-size:13px;display:flex;flex-wrap:wrap;gap:4px 14px}.loc .sub b{color:var(--ink);font-weight:700;font-variant-numeric:tabular-nums}.loc .sub .wpd b{color:#e2b6ff}
/* side panels */
.side{position:absolute;z-index:12;right:90px;top:18px;width:min(370px,calc(100% - 110px));max-height:calc(100% - 36px);overflow:auto;padding:16px 18px;display:none}.side.open{display:block}
.side h3{margin:2px 0 10px;font-size:12px;letter-spacing:.12em;color:var(--acc);display:flex;align-items:center;justify-content:space-between;gap:8px}.side h3+.muted{margin-top:-4px}.side section+section{margin-top:16px;padding-top:14px;border-top:1px solid #ffffff12}
.seg{display:grid;grid-template-columns:repeat(auto-fit,minmax(70px,1fr));gap:6px}.seg button{padding:9px 6px;border:1px solid #ffffff1c;border-radius:7px;background:#ffffff08;cursor:pointer;font-size:13px}.seg button:hover{background:#ffffff14}.seg button.active{background:var(--acc);border-color:var(--acc);color:#061018;font-weight:800}.seg button[hidden]{display:none}
.row{display:flex;align-items:center;gap:10px;padding:8px 4px;border-bottom:1px solid #ffffff0b;font-size:13.5px}.row:hover{background:#ffffff07}label.row{cursor:pointer}.row .grow{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.muted{color:#7f98a3;font-size:12px}
.row .wpb{display:grid;place-items:center;width:28px;height:26px;padding:0!important}.row .wpb svg{width:14px;height:14px;fill:none;stroke:currentColor;stroke-width:2.2;stroke-linecap:round;stroke-linejoin:round}.row .lic{flex:none;width:20px;height:20px;display:grid;place-items:center;border-radius:6px;color:#061018}.row .lic svg{width:13px;height:13px;fill:none;stroke:currentColor;stroke-width:2.6;stroke-linecap:round;stroke-linejoin:round}
.prow .key{flex:none;width:20px;height:20px;display:grid;place-items:center;border-radius:5px;background:#ffffff10;color:#9fb8c3;font-size:11px;font-weight:800}.prow.on{background:#0dd4fc1c;box-shadow:inset 3px 0 0 var(--acc)}.prow.on .key{background:var(--acc);color:#061018}.prow{cursor:pointer}
input[type=checkbox]{accent-color:var(--acc);width:17px;height:17px;flex:none}
.ico{width:20px;height:20px;flex:none}
.search,.field{width:100%;margin:2px 0 8px;padding:8px 10px;border:1px solid #ffffff22;border-radius:7px;background:#06101a;outline:none}.search:focus,.field:focus{border-color:var(--acc)}
.btns{display:flex;flex-wrap:wrap;gap:6px;margin-bottom:6px}.btns button,.row button{padding:5px 10px;border:1px solid #ffffff22;border-radius:6px;background:#ffffff0a;cursor:pointer;font-size:12px}.btns button:hover:not(:disabled),.row button:hover{background:var(--acc);color:#061018}.btns .danger:hover{background:var(--red)!important;color:#fff!important}
.foot{margin-top:14px;color:#6f8893;font-size:11px;line-height:1.55}
/* dispatch */
.alerts{position:absolute;z-index:10;right:90px;bottom:18px;width:min(340px,calc(100% - 110px));max-height:46%;display:none;flex-direction:column;background:#1a0710ec;border:1px solid #ff547044;border-left:3px solid var(--red)}.alerts.open{display:flex}
.alerts .ah{display:flex;align-items:center;gap:8px;padding:10px 10px 8px 14px;color:#ff9aac;font-size:11px;font-weight:900;letter-spacing:.14em}.alerts .ah .n{padding:0 7px;border-radius:9px;background:var(--red);color:#fff;letter-spacing:0}.alerts .ah button{margin-left:auto;display:grid;place-items:center;width:26px;height:26px;border:0;border-radius:6px;background:transparent;color:#e7c3cb;cursor:pointer}.alerts .ah button:hover{background:#ffffff14}.alerts .ah svg{width:16px;height:16px;fill:none;stroke:currentColor;stroke-width:2.2;stroke-linecap:round}
#alertRows{overflow:auto;padding:0 8px 8px}.alerts .a{padding:8px 6px;border-radius:6px;cursor:pointer}.alerts .a+.a{border-top:1px solid #ffffff0e}.alerts .a:hover{background:#ffffff0c}.code{display:inline-block;margin-right:6px;padding:0 6px;border-radius:3px;background:var(--red);color:#fff;font-size:11px;font-weight:900}.alerts .t{font-size:13.5px;font-weight:700}.alerts .s{color:#caa6b0;font-size:11.5px;margin-top:2px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.alert-tip{position:fixed;z-index:13;width:300px;padding:12px 14px;background:#12050bf4;border:1px solid #ff547055;border-left:3px solid var(--red);border-radius:10px;box-shadow:0 14px 40px #000a;pointer-events:none;font-size:13px;line-height:1.5}.alert-tip .h{display:flex;align-items:center;gap:8px;font-size:15px;font-weight:800;margin-bottom:6px}.alert-tip .body{color:#f3dde2;white-space:pre-wrap;word-break:break-word;margin-bottom:8px}.alert-tip .meta{display:grid;grid-template-columns:auto 1fr;gap:2px 10px;color:#c9aab2;font-size:12px}.alert-tip .meta b{color:#8f7680;font-weight:600}.alert-tip .tiphint{margin-top:8px;color:#8f7680;font-size:11px}
/* menu, dialogs, hint, toast */
.menu{position:fixed;z-index:30;min-width:220px;padding:6px;background:#071018f6;border:1px solid #ffffff22;border-radius:10px;box-shadow:0 18px 40px #000b;display:none}.menu.open{display:block}.menu .head{padding:6px 10px 8px;color:#8ca6b3;font-size:11.5px;border-bottom:1px solid #ffffff12;margin-bottom:4px;font-variant-numeric:tabular-nums}.menu button{display:block;width:100%;padding:9px 10px;border:0;border-radius:6px;background:transparent;text-align:left;cursor:pointer}.menu button:hover{background:var(--acc);color:#061018}.menu button[hidden]{display:none}
.dialog{position:fixed;z-index:40;inset:0;display:none;place-items:center;background:#02070b99}.dialog.open{display:grid}.dialog form,.dialog .box{width:min(380px,calc(100% - 40px));padding:20px;background:#0b1922;border:1px solid #ffffff1a;border-top:3px solid var(--acc);border-radius:10px;box-shadow:0 20px 60px #000}.dialog h3{margin:0 0 12px;font-size:15px}.dialog input[type=text]{width:100%;padding:9px 10px;border:1px solid #ffffff22;border-radius:7px;background:#06101a;outline:none}.dialog input[type=text]:focus{border-color:var(--acc)}.dialog .actions{display:flex;justify-content:flex-end;gap:8px;margin-top:12px}.dialog .actions button{padding:8px 14px;border:0;border-radius:7px;cursor:pointer}.dialog .actions .ok{background:var(--acc);color:#061018;font-weight:800}.dialog .actions .cancel{background:#ffffff14}
.dialog textarea{width:100%;height:160px;margin-top:10px;padding:9px 10px;border:1px solid #ffffff22;border-radius:7px;background:#06101a;outline:none;resize:vertical;font:12px/1.5 ui-monospace,Consolas,monospace;color:inherit}.dialog textarea:focus{border-color:var(--acc)}.dialog textarea[hidden],.dialog input[hidden]{display:none}.dialog .note{margin-top:8px;color:#7f98a3;font-size:12px;line-height:1.55}.dialog .note:empty{display:none}
.keys{display:grid;grid-template-columns:1fr auto;gap:6px 16px;font-size:13px}.keys h4{grid-column:1/-1;margin:10px 0 2px;color:var(--acc);font-size:11px;letter-spacing:.12em}.keys h4:first-child{margin-top:0}.keys kbd{display:inline-block;min-width:22px;padding:1px 6px;border:1px solid #ffffff2a;border-bottom-width:2px;border-radius:5px;background:#ffffff0c;font:700 11.5px ui-monospace,Consolas,monospace;text-align:center}
.hint{position:absolute;z-index:11;left:50%;top:18px;transform:translateX(-50%);max-width:calc(100% - 200px);display:flex;align-items:center;gap:10px;padding:9px 10px 9px 16px;font-size:13px}.hint[hidden]{display:none}.hint button{white-space:nowrap;border:0;border-radius:6px;padding:5px 10px;background:#ffffff14;cursor:pointer;font-size:12px}.hint button:hover{background:var(--acc);color:#061018}
.toast{position:fixed;z-index:50;left:50%;top:72px;transform:translateX(-50%);padding:10px 16px;background:#071018f4;border:1px solid #0dd4fc66;border-radius:10px;font-size:13px;box-shadow:0 10px 30px #000a;opacity:0;transition:opacity .2s;pointer-events:none}.toast.show{opacity:1}
.disabled{position:absolute;z-index:20;inset:0;display:grid;place-items:center;background:#061018dd}.disabled[hidden]{display:none}.disabled div{max-width:540px;padding:28px;text-align:center;background:#0b1922;border:1px solid #ffffff16;border-top:3px solid var(--acc);border-radius:12px;box-shadow:0 20px 60px #000}.disabled h2{margin:0 0 10px;font-size:20px}.disabled p{margin:0;color:#94aab5;line-height:1.7}
@media(max-width:760px){.tools{right:10px;top:10px}.tb{width:50px;height:46px}.tb span{display:none}.pal{left:10px;padding:4px}.pal button{width:38px;height:38px}.flyout{left:62px;width:calc(100% - 140px)}.side{right:70px;top:10px;width:calc(100% - 80px)}.alerts{right:10px;bottom:62px;top:auto;width:calc(100% - 20px)}.loc{left:10px;top:10px}.hint{left:10px;right:80px;top:10px;max-width:none;transform:none}.coords{display:none!important}.sbar{left:10px;bottom:10px}}
</style>
</head>
<body>
<main id="map"><div id="world"><div id="tiles"></div><svg id="shapes" xmlns="http://www.w3.org/2000/svg"></svg><div id="blips"></div><div id="units"></div><div id="items"></div><div id="waypoint" class="wp" hidden><i></i><b></b></div><div id="player" class="player" hidden><i></i></div></div></main>
<nav class="tools">
<div class="grp card"><button id="follow" class="tb active" title="自分の位置に戻る（F）"><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="3"/><circle cx="12" cy="12" r="8"/><path d="M12 1v3M12 20v3M1 12h3M20 12h3"/></svg><span>追従</span></button><button id="zoomIn" class="tb" title="拡大（+・ホイール・ダブルクリック）"><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14M5 12h14"/></svg><span>拡大</span></button><button id="zoomOut" class="tb" title="縮小（-）"><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12h14"/></svg><span>縮小</span></button></div>
<div class="grp card"><button id="view" class="tb" title="地図の種類・表示するもの"><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3 2 8l10 5 10-5z"/><path d="m2 13 10 5 10-5"/></svg><span>表示</span></button><button id="pinList" class="tb" title="書き込んだものの一覧"><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M8 6h13M8 12h13M8 18h13"/><path d="M3.5 6h.01M3.5 12h.01M3.5 18h.01"/></svg><span>一覧</span></button><button id="alertToggle" class="tb" title="通報の一覧" hidden><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M6 16V11a6 6 0 0 1 12 0v5l2 2H4z"/><path d="M10 21h4"/></svg><span>通報</span><em id="alertBadge" class="badge" hidden></em></button></div>
<div class="grp card"><button id="fullscreen" class="tb" title="全画面"><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/></svg><span>全画面</span></button></div>
</nav>
<section id="locCard" class="card loc" hidden><span id="conn" class="dot"></span><div><div id="location" class="main">FiveM を待っています</div><div id="sub" class="sub"><span id="zone">MioCity Media Link から現在地を受信します</span></div></div></section>
<nav id="dock" class="pal card" aria-label="書き込み">
<button data-tool="select" class="active" title="選ぶ・動かす（V）　キー操作の一覧は ?"><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M6 3v15l4-4 3 7 2.5-1-3-7H18z"/></svg><kbd>V</kbd></button>
<button data-tool="m" title="ピン（P）"><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 21s-7-6.2-7-11.5A7 7 0 0 1 19 9.5C19 14.8 12 21 12 21z"/><circle cx="12" cy="9.5" r="2.5"/></svg><kbd>P</kbd></button>
<button data-tool="t" title="テキスト（T）"><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M4 7V5h16v2M12 5v14M8.5 19h7"/></svg><kbd>T</kbd></button>
<button data-tool="a" title="範囲（S）"><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><rect x="4" y="5" width="16" height="14" rx="1.5" stroke-dasharray="3 2.5"/></svg><kbd>S</kbd></button>
<button data-tool="l" title="線・矢印（L）"><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><circle cx="5.5" cy="18.5" r="1.8"/><circle cx="18.5" cy="5.5" r="1.8"/><path d="m7 17 10-10"/></svg><kbd>L</kbd></button>
<hr>
<button id="undo" title="元に戻す（Ctrl+Z）" disabled><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M9 14 4 9l5-5"/><path d="M4 9h10.5a5.5 5.5 0 0 1 0 11H11"/></svg></button>
<button id="redo" title="やり直し（Ctrl+Y）" disabled><svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="m15 14 5-5-5-5"/><path d="M20 9H9.5a5.5 5.5 0 0 0 0 11H13"/></svg></button>
</nav>
<div id="toolOpts" class="flyout card" hidden></div>
<div class="sbar card"><span id="stat" class="stat"><span>FiveM を待っています</span></span><button id="presetBtn" title="プリセットの切り替え（キーボードの 1〜9 でも）"><span class="pk">プリセット</span><span id="presetName">メイン</span></button><span id="coords" class="coords">X —    Y —</span></div>
<aside id="viewPanel" class="side card">
<section><h3>地図の種類</h3><div class="seg"><button data-layer="render">写真</button><button data-layer="game">ゲーム</button><button data-layer="print">白地図</button><button data-layer="postal" hidden>番地</button></div></section>
<section><h3>マークの大きさ</h3><div class="range"><input id="iconSize" type="range" min="60" max="200" step="10"><b id="iconSizeVal"></b></div></section>
<section><h3>地図に出すもの</h3><div id="layerToggles"></div></section>
<section><h3>地図アイコンの種類</h3><div class="muted" style="margin-bottom:8px">チェックした種類だけ地図に出ます</div><input id="iconSearch" class="search" type="search" placeholder="種類を探す（例：病院）" autocomplete="off"><div class="btns"><button id="allOn">すべて表示</button><button id="allOff">すべて隠す</button></div><div id="spriteList"><div class="muted">アイコンを受信すると一覧が出ます</div></div></section>
<div id="privacy" class="foot"></div>
</aside>
<aside id="pinPanel" class="side card"><h3>書き込み<span id="pinPreset" class="muted"></span></h3><div class="muted" style="margin-bottom:10px">左のパレットでピン・テキスト・範囲・線を書き込めます。行をクリックするとその場所へ移動します。このPCのブラウザにだけ保存されます。</div><div id="pinRows"></div></aside>
<aside id="presetPanel" class="side card"><h3>プリセット</h3><div class="muted" style="margin-bottom:10px">書き込んだものと「表示」で選んだアイコンの種類をまとめて切り替えます。キーボードの 1〜9 でも切り替えられます。</div><div id="presetRows"></div><div class="btns" style="margin-top:10px"><button id="presetNew">新しく作る</button><button id="presetDup">今のを複製</button></div>
<section><h3>共有・読み込み</h3><div class="muted" style="margin-bottom:8px">今のプリセットを文字にして人に渡せます。座標の一覧（1行に「x, y 名前」）からも作れます。</div><div class="btns"><button id="presetExport">書き出す</button><button id="presetImport">読み込む</button></div></section></aside>
<aside id="editPanel" class="side card"></aside>
<section id="alerts" class="alerts card"><div class="ah">通報<span id="alertCount" class="n"></span><button id="alertClose" title="閉じる（右の「通報」でまた開けます）"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg></button></div><div id="alertRows"></div></section><div id="alertTip" class="alert-tip" hidden></div>
<div id="hint" class="hint card" hidden><span>地図のアイコンは「表示」で種類を選ぶと出ます</span><button id="hintOpen">表示を開く</button><button id="hintClose">閉じる</button></div>
<div id="menu" class="menu"><div id="menuHead" class="head"></div><button id="mWaypoint">ここにウェイポイントを設定</button><button id="mClearWp">ウェイポイントを解除</button><button id="mMarker">ここにピンを立てる</button><button id="mEdit">編集</button><button id="mDelItem">削除</button><button id="mCopy">この場所の座標をコピー</button></div>
<div id="presetDialog" class="dialog"><form id="presetForm"><h3 id="pdTitle"></h3><input id="pdName" type="text" maxlength="24" placeholder="名前（例：初心者案内）" autocomplete="off"><textarea id="pdText" spellcheck="false" hidden></textarea><div id="pdNote" class="note"></div><div class="actions"><button type="button" class="cancel" id="pdCancel">やめる</button><button type="submit" class="ok" id="pdOk"></button></div></form></div>
<div id="helpDialog" class="dialog"><div class="box"><h3>キー操作</h3><div class="keys">
<h4>書き込み</h4><span>選ぶ・動かす</span><span><kbd>V</kbd></span><span>ピン</span><span><kbd>P</kbd></span><span>テキスト</span><span><kbd>T</kbd></span><span>範囲</span><span><kbd>S</kbd></span><span>線・矢印</span><span><kbd>L</kbd></span><span>自由な形を閉じる</span><span><kbd>Enter</kbd></span><span>選んだものを消す</span><span><kbd>Delete</kbd></span><span>ひとつ戻す / 進める</span><span><kbd>Ctrl</kbd>+<kbd>Z</kbd> / <kbd>Ctrl</kbd>+<kbd>Y</kbd></span>
<h4>地図の操作</h4><span>自分の位置へ</span><span><kbd>F</kbd></span><span>拡大・縮小</span><span><kbd>+</kbd> <kbd>-</kbd></span><span>プリセット</span><span><kbd>1</kbd>〜<kbd>9</kbd></span><span>取り消し・閉じる</span><span><kbd>Esc</kbd></span><span>この一覧</span><span><kbd>?</kbd></span>
</div><div class="note">文字を入力している間はキー操作は効きません。</div><div class="actions"><button type="button" class="ok" id="helpClose">閉じる</button></div></div></div>
<div id="toast" class="toast"></div>
<div id="disabled" class="disabled" hidden><div><h2 id="disTitle"></h2><p id="disText"></p></div></div>
<script>
(()=>{"use strict";
// Tiles: the public GTA V map tiles LB Phone uses, or the game server's own "番地" map. Only tile numbers are requested;
// the coordinate JSON, blips, members and calls never leave 127.0.0.1. Blip icons come from this Bridge.
// Right click offers what the pause map offers (set / clear the waypoint; only when the game allows it). What the
// player draws (markers, text, areas, lines, arrows) stays in this browser (localStorage), grouped into presets.
const TILE_BASE="https://assets.loaf-scripts.com/map-tiles/gtav/main/";
const LAYERS=["render","game","print","postal"];
const COLORS={0:"#fefefe",1:"#e03232",2:"#71cb71",3:"#5db6e5",4:"#fefefe",5:"#eec64e",6:"#c25050",7:"#9c6eaf",8:"#fe7ac3",9:"#f59d79",10:"#b18f83",11:"#8dcea7",12:"#70a8ae",13:"#d3d1e7",14:"#8f7e98",15:"#6ac4bf",16:"#d5c398",17:"#ea8e50",18:"#97cae9",19:"#b26287",20:"#8f8d79",21:"#a6755e",22:"#afa8a8",23:"#e78d9a",24:"#bbd65b",25:"#0c7b56",26:"#7ac3fe",27:"#ab3ce6",28:"#cda80c",29:"#4561ab",30:"#29a5b8",31:"#b89b7b",32:"#c8e0fe",33:"#f0f096",34:"#ed8ca1",35:"#f98a8a",36:"#fbeea5",37:"#fefefe",38:"#2c6db8",39:"#9a9a9a",40:"#4c4c4c",41:"#f29d9d",42:"#6cb7d6",43:"#afedae",44:"#ffa75f",45:"#f1f1f1",46:"#ecf029",47:"#ff9a18",48:"#f644a5",49:"#e03a3a",50:"#8a6de3",51:"#ff8b5c",52:"#416c41",53:"#b3ddf3",54:"#3a6479",55:"#a0a0a0",56:"#847232",57:"#65b9e7",58:"#4b4175",59:"#e13b3b",60:"#f0cb58",61:"#cd3f98",62:"#cfcfcf",63:"#276a9f",64:"#d87b1b",65:"#8e8393",66:"#f0cb57",67:"#65b9e7",68:"#65b9e7",69:"#79cd79",70:"#efca57",71:"#efca57",72:"#3d3d3d",73:"#efca57",74:"#65b9e7",75:"#e03232",76:"#782323",77:"#65b9e7",78:"#3a6479",79:"#e03232",80:"#65b9e7",81:"#f2a40c",82:"#a4ccaa",83:"#a854f2",84:"#65b9e7",85:"#3d3d3d"};
// Japanese names for common icons (others show the FiveM reference name)
const JP={level:"汎用マーカー",north:"Nマーク",safehouse:"家・拠点",police_station:"警察署",police_station_blue:"警察署",hospital:"病院",barber:"床屋",car_mod_shop:"改造ショップ",bennys:"ベニーズ",clothes_store:"服屋",tattoo:"タトゥー",gun_shop:"銃砲店",shootingrange_gunshop:"射撃場",bar:"バー",car_wash:"洗車",garage:"ガレージ",garage_for_sale:"ガレージ",jerry_can:"ガソリンスタンド",crim_holdups:"ショップ",property:"物件",property_for_sale:"物件（販売中）",warehouse:"倉庫",dock:"ボート乗り場",taxi:"タクシー",garbage:"ゴミ収集",tow:"レッカー",tow_truck:"レッカー",weapon_health:"回復",hunting:"狩猟",cinema:"映画館",music_venue:"ライブ会場",gang_vehicle:"車両",gang_vehicle_bikers:"バイク",helicopter:"ヘリ",player_plane:"飛行機",player_boat:"船",friend:"フレンド",poi:"スポット",cop_car:"警察車両",camera:"カメラ",airport:"空港",business:"ビジネス",bank:"銀行",dollar_sign:"お金",store:"ショップ",flight_school:"飛行学校",strip_club:"ストリップクラブ",darts:"ダーツ",golf:"ゴルフ",tennis:"テニス",bowling:"ボウリング",gym:"ジム",restaurant:"レストラン",burger_shot:"バーガーショット",cluckin_bell:"クラッキンベル",laptop:"ノートPC",package:"荷物",repair:"修理",pickup_repair:"修理"};
const KIND_SPRITE=[1,225,226,64,423,427];
const PIN_ICONS=["★","⌂","$","✚","⚑","●","♥","!"];
const PIN_COLORS=["#ffcc4d","#0dd4fc","#72f0c0","#ff5470","#c96bff","#ffffff"];
const MIN_ZOOM=2,MAX_ZOOM=7;
// marker icons: [label, svg body on a 24 box]
const ICONS={
  pin:["点",'<circle cx="12" cy="12" r="4.5" fill="currentColor" stroke="none"/>'],
  car:["車",'<path d="M5 16h14v-4l-2-5H7l-2 5z"/><circle cx="8" cy="16.5" r="1.5"/><circle cx="16" cy="16.5" r="1.5"/>'],
  person:["人",'<circle cx="12" cy="7" r="3.2"/><path d="M5.5 20a6.5 6.5 0 0 1 13 0"/>'],
  warning:["注意",'<path d="M12 4 2.8 19.5h18.4z"/><path d="M12 10v4.5M12 17.2v.1"/>'],
  shield:["盾",'<path d="M12 3 5 6v5.5c0 4.2 3 7.6 7 9.5 4-1.9 7-5.3 7-9.5V6z"/>'],
  flag:["旗",'<path d="M6 21V4M6 4h11l-2.5 4L17 12H6"/>'],
  home:["拠点",'<path d="M4 11 12 4l8 7"/><path d="M6.5 9.5V20h11V9.5"/>'],
  star:["星",'<path d="m12 3.5 2.6 5.4 5.9.8-4.3 4.1 1 5.8-5.2-2.8-5.2 2.8 1-5.8-4.3-4.1 5.9-.8z"/>'],
  heart:["ハート",'<path d="M12 20s-7.5-4.6-7.5-10A4.3 4.3 0 0 1 12 7.4 4.3 4.3 0 0 1 19.5 10c0 5.4-7.5 10-7.5 10z"/>'],
  tree:["木",'<path d="M12 3 6 12h3l-3.5 5h13L15 12h3z"/><path d="M12 17v4"/>'],
  medical:["病院",'<path d="M9.5 4h5v5.5H20v5h-5.5V20h-5v-5.5H4v-5h5.5z"/>'],
  food:["食事",'<path d="M7 3v8M5 3v5a2 2 0 0 0 4 0V3M7 11v10M16 21V3c-2.2 1.2-3 3.6-3 6.5V13h3"/>'],
  police:["警察",'<path d="M12 3 4.5 6.5 6 14c.8 3.3 3.2 5.7 6 7 2.8-1.3 5.2-3.7 6-7l1.5-7.5z"/><path d="m12 8.5 1.1 2.2 2.4.3-1.8 1.7.5 2.4-2.2-1.2-2.2 1.2.5-2.4-1.8-1.7 2.4-.3z"/>'],
  gun:["武器",'<path d="M3 8h17v4h-7l-1 2h-3l-1 5H4.5l1.5-7H3z"/>'],
  money:["お金",'<path d="M12 3v18M16.5 7.5c-.8-1.4-2.4-2-4.5-2-2.6 0-4.3 1.2-4.3 3.1 0 4.4 9 2.4 9 6.8 0 2-1.9 3.2-4.7 3.2-2.3 0-4-.8-4.8-2.4"/>'],
  box:["荷物",'<path d="m12 3 8 4.5v9L12 21l-8-4.5v-9z"/><path d="m4 7.5 8 4.5 8-4.5M12 12v9"/>'],
  flask:["素材",'<path d="M9.5 3h5M10.5 3v6L5 19a1.5 1.5 0 0 0 1.3 2h11.4a1.5 1.5 0 0 0 1.3-2l-5.5-10V3"/><path d="M7.5 15h9"/>'],
  leaf:["植物",'<path d="M5 19c0-8 5-13 15-14-1 10-6 15-14 15"/><path d="M5 19 13 11"/>'],
};
const OLD_ICON={"★":"star","⌂":"home","$":"money","✚":"medical","⚑":"flag","●":"pin","♥":"heart","!":"warning"};
const TYPE={m:"ピン",t:"テキスト",a:"範囲",l:"線",r:"矢印"};
const BOUNDS={x0:-5000,x1:6000,y0:-5500,y1:9000};   // the GTA map (the Bridge accepts waypoints only inside it)
const inMap=(x,y)=>x>=BOUNDS.x0&&x<=BOUNDS.x1&&y>=BOUNDS.y0&&y<=BOUNDS.y1;
const SWATCHES=["#ffcc4d","#ff9f43","#ff5470","#ff7ac8","#c96bff","#3b82f6","#0dd4fc","#72f0c0","#f4f7fa","#1b232b"];
const TOOL_TIP={m:"地図をクリックした所にピンが立ちます。続けて何本でも立てられます。",t:"地図をクリックした所に文字を書きます。",l:"2点をクリックすると、その間に線を引きます。1点目のあと右クリックで取り消し。"};
const TOOL_KEY={select:"V",m:"P",t:"T",a:"S",l:"L"};
const $=id=>document.getElementById(id);
const map=$("map"),world=$("world"),tiles=$("tiles"),shapeLayer=$("shapes"),blipLayer=$("blips"),unitLayer=$("units"),itemLayer=$("items"),player=$("player"),waypoint=$("waypoint"),disabled=$("disabled"),menu=$("menu");
let state={active:false,x:450,y:1650,z:0,heading:0,speedKmh:0,street:"",crossing:"",zone:"",inVehicle:false,hasWaypoint:false,waypointX:0,waypointY:0,postal:""};
let cfg={waypoint:false,postal:"",postalMax:6,services:false};
const store=(k,v)=>{try{localStorage.setItem(k,v);return true}catch{return false}},read=k=>{try{return localStorage.getItem(k)}catch{return null}};
const readJson=(k,d)=>{try{const v=JSON.parse(read(k)||"null");return v==null?d:v}catch{return d}};
let layer=LAYERS.includes(read("mio-map-layer"))?read("mio-map-layer"):"render";
const num=v=>Number.isFinite(Number(v))?Number(v):0;
const esc=s=>String(s??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
const svgOf=(name,cls)=>`<svg${cls?` class="${cls}"`:""} viewBox="0 0 24 24" aria-hidden="true">${(ICONS[name]||ICONS.pin)[1]}</svg>`;
// ---------------------------------------------------------------- data clean-up (saved and imported data alike)
// Presets hold the player's items (markers / text / areas / lines / arrows), icon types (shown sprites) and show
// toggles; one is active. 1.6 pins become markers; the pre-1.6 single set (mio-map-pins / -shown / -show) the first preset.
const HEX=/^#[0-9a-f]{6}$/i,ID=/^[a-z0-9]{1,16}$/,MAX_PRESETS=20,MAX_ITEMS=400;
const SHOW_DEFAULT={blips:true,units:true,alerts:true,pins:true,loc:false};
const newId=()=>Date.now().toString(36)+Math.random().toString(36).slice(2,6);
const clampN=(v,lo,hi,d)=>{v=Number(v);return Number.isFinite(v)?Math.max(lo,Math.min(hi,v)):d};
const okXY=(x,y)=>Number.isFinite(x)&&Number.isFinite(y)&&Math.abs(x)<=12000&&Math.abs(y)<=12000;
// a coordinate must really be a number ("", null, true would otherwise become 0)
const coord=v=>typeof v==="number"?v:typeof v==="string"&&v.trim()!==""?Number(v):NaN;
const hasIcon=k=>typeof k==="string"&&Object.prototype.hasOwnProperty.call(ICONS,k);
const r1=v=>Math.round(v*10)/10;
const hexOr=(v,d)=>typeof v==="string"&&HEX.test(v)?v.toLowerCase():d;
const str=(v,n)=>String(v??"").replace(/\s+/g," ").trim().slice(0,n);
function cleanPin(p){if(!p||typeof p!=="object")return null;const x=coord(p.x),y=coord(p.y);if(!okXY(x,y))return null;
  return{id:typeof p.id==="string"&&ID.test(p.id)?p.id:newId(),x:r1(x),y:r1(y),label:str(p.label,40),icon:PIN_ICONS.includes(p.icon)?p.icon:"★",color:hexOr(p.color,PIN_COLORS[0])}}
function cleanPts(a,min,max){if(!Array.isArray(a)||a.length<min||a.length>max)return null;const out=[];for(const p of a){const x=coord(Array.isArray(p)?p[0]:p&&p.x),y=coord(Array.isArray(p)?p[1]:p&&p.y);if(!okXY(x,y))return null;out.push([r1(x),r1(y)])}
  // not a point or a line of zero length / an area without size
  const xs=out.map(p=>p[0]),ys=out.map(p=>p[1]);if(Math.max(...xs)-Math.min(...xs)<.5&&Math.max(...ys)-Math.min(...ys)<.5)return null;return out}
function cleanItem(o){if(!o||typeof o!=="object")return null;const id=typeof o.id==="string"&&ID.test(o.id)?o.id:newId(),t=o.t;
  if(t==="m"||t==="t"){const x=coord(o.x),y=coord(o.y);if(!okXY(x,y))return null;
    return t==="m"?{id,t,x:r1(x),y:r1(y),label:str(o.label,40),icon:hasIcon(o.icon)?o.icon:"pin",color:hexOr(o.color,"#ffcc4d"),size:Math.round(clampN(o.size,20,56,32))}
      :{id,t,x:r1(x),y:r1(y),label:str(o.label,80)||"テキスト",color:hexOr(o.color,"#ffffff"),size:Math.round(clampN(o.size,12,40,18))}}
  if(t==="a"){const pts=cleanPts(o.pts,3,64);return pts&&{id,t,pts,label:str(o.label,40),color:hexOr(o.color,"#3b82f6"),stroke:hexOr(o.stroke,"#ffffff"),opacity:Math.round(clampN(o.opacity,.05,.9,.35)*100)/100}}
  if(t==="l"||t==="r"){const pts=cleanPts(o.pts,2,2);return pts&&{id,t,pts,label:str(o.label,40),color:hexOr(o.color,t==="r"?"#ff5470":"#3b82f6"),width:Math.round(clampN(o.width,1,10,4))}}
  return null}
function pinToItem(p){const c=cleanPin(p);return c&&{id:c.id,t:"m",x:c.x,y:c.y,label:c.label,icon:OLD_ICON[c.icon]||"pin",color:c.color,size:32}}
function cleanPreset(o,name){o=o&&typeof o==="object"?o:{};const show={...SHOW_DEFAULT};if(o.show&&typeof o.show==="object")for(const k in SHOW_DEFAULT)if(typeof o.show[k]==="boolean")show[k]=o.show[k];
  const seen=new Set(),items=[];
  for(const it of [...(Array.isArray(o.items)?o.items.map(cleanItem):[]),...(Array.isArray(o.pins)?o.pins.map(pinToItem):[])]){if(!it)continue;if(seen.has(it.id))it.id=newId();seen.add(it.id);items.push(it);if(items.length>=MAX_ITEMS)break}
  return{id:typeof o.id==="string"&&ID.test(o.id)?o.id:newId(),name:str(o.name||name||"プリセット",24)||"プリセット",items,shown:[...new Set((Array.isArray(o.shown)?o.shown:[]).filter(n=>Number.isInteger(n)&&n>0&&n<2000))].slice(0,1000),show}}
let presets=readJson("mio-map-presets",null);
presets=Array.isArray(presets)?presets.map(p=>cleanPreset(p)).slice(0,MAX_PRESETS):[];
if(!presets.length)presets=[cleanPreset({name:"メイン",pins:readJson("mio-map-pins",[]),shown:readJson("mio-map-shown",[]),show:Object.assign({},readJson("mio-map-show",{}),read("mio-map-blips")==="0"?{blips:false}:{})})];
let active=presets.find(p=>p.id===read("mio-map-preset"))||presets[0];
// 同僚の位置 / 通報 belong to the job, not to a preset: one setting for all presets
const SVC=readJson("mio-map-svc",null);
const show={...active.show,...(SVC&&typeof SVC==="object"?{units:SVC.units!==false,alerts:SVC.alerts!==false}:{})};
const shownSprites=new Set(active.shown);
let items=active.items;
// icon size: one value for every preset (it suits the monitor, not the content)
let iconScale=Math.max(.6,Math.min(2,Number(read("mio-map-iconsize"))||1));
let zoom=Math.max(MIN_ZOOM,Math.min(MAX_ZOOM,Number(read("mio-map-zoom"))||5));
// drawing tool + its options (remembered)
const OPT_DEFAULT={color:{m:"#ffcc4d",t:"#ffffff",a:"#3b82f6",l:"#3b82f6",r:"#ff5470"},icon:"pin",size:32,tsize:18,areaMode:"rect",stroke:"#ffffff",opacity:.35,width:4};
const opts=(()=>{const o=readJson("mio-map-tool",{})||{},c=o.color||{};return{color:{m:hexOr(c.m,OPT_DEFAULT.color.m),t:hexOr(c.t,OPT_DEFAULT.color.t),a:hexOr(c.a,OPT_DEFAULT.color.a),l:hexOr(c.l,OPT_DEFAULT.color.l),r:hexOr(c.r,OPT_DEFAULT.color.r)},
  icon:hasIcon(o.icon)?o.icon:"pin",size:Math.round(clampN(o.size,20,56,32)),arrow:o.arrow===true,tsize:Math.round(clampN(o.tsize,12,40,18)),areaMode:o.areaMode==="poly"?"poly":"rect",stroke:hexOr(o.stroke,"#ffffff"),opacity:clampN(o.opacity,.05,.9,.35),width:Math.round(clampN(o.width,1,10,4))}})();
const saveOpts=()=>store("mio-map-tool",JSON.stringify(opts));
let tool="select",draft=null,hoverAt=null,selId=null;
let itemsDirty=true,saveTimer=0,saveWarnAt=0;
let follow=true,center={x:450,y:1650},drag=null,retry=null,mapEnabled=false,gameConnected=false,frame=0,blips=[],units=[],alerts=[],blipsDirty=true,placedZoom=-1,socket=null,names={},menuAt=null,menuItem=null,toastTimer=0;
const tileCache=new Map(),blipEls=[],unitEls=[];
const U=16384/(9000*128),D=24576/(-13500*128);
function px(x,y){const s=2**zoom;return{x:s*U*(x+4140),y:s*D*(y-8400)}}
function toGame(x,y){const s=2**zoom;return{x:x/(s*U)-4140,y:y/(s*D)+8400}}
function viewOffset(){const c=px(center.x,center.y);return{x:innerWidth/2-c.x,y:innerHeight/2-c.y}}
function screenToGame(cx,cy){const o=viewOffset();return toGame(cx-o.x,cy-o.y)}
function dist(m){return m>=1000?(m/1000).toFixed(m>=10000?0:1)+" km":Math.round(m)+" m"}
function clearTiles(){for(const img of tileCache.values())img.remove();tileCache.clear()}
function toast(text){const t=$("toast");t.textContent=text;t.classList.add("show");clearTimeout(toastTimer);toastTimer=setTimeout(()=>t.classList.remove("show"),2200)}
function send(type,data){if(!socket||socket.readyState!==1)return false;try{socket.send(JSON.stringify({type,data}));return true}catch{return false}}
function render(smooth){if(smooth===false)world.classList.remove("smooth");if(!frame)frame=requestAnimationFrame(()=>{frame=0;draw()})}
function distanceTo(x,y){return Math.hypot(num(state.x)-x,num(state.y)-y)}
function draw(){
  if(!gameConnected||!mapEnabled){clearTiles();player.hidden=true;waypoint.hidden=true;blipLayer.textContent="";blipEls.length=0;unitLayer.textContent="";unitEls.length=0;placedZoom=-1;return}
  if(follow&&state.active)center={x:num(state.x),y:num(state.y)};
  const c=px(center.x,center.y);
  world.style.transform=`translate(${innerWidth/2-c.x}px,${innerHeight/2-c.y}px)`;
  world.classList.toggle("small",zoom<4);
  const zoomChanged=placedZoom!==zoom;
  if(zoomChanged){clearTiles();placedZoom=zoom;blipsDirty=true;itemsDirty=true}
  renderTiles(c);
  const p=px(num(state.x),num(state.y));
  player.hidden=!state.active;
  player.style.transform=`translate(${p.x}px,${p.y}px)`+(iconScale!==1?` scale(${iconScale})`:"");player.firstChild.style.transform=`rotate(${-num(state.heading)}deg)`;
  if(state.hasWaypoint){const w=px(num(state.waypointX),num(state.waypointY));waypoint.hidden=false;waypoint.style.transform=`translate(${w.x}px,${w.y}px)`;
    waypoint.lastChild.textContent=state.active?dist(distanceTo(num(state.waypointX),num(state.waypointY))):""}else waypoint.hidden=true;
  if(blipsDirty){blipsDirty=false;renderBlips();renderUnits()}
  if(itemsDirty){itemsDirty=false;renderItems()}
  $("follow").classList.toggle("active",follow);
  if(follow&&!drag&&!zoomChanged)requestAnimationFrame(()=>world.classList.add("smooth"));else world.classList.remove("smooth");
}
// only tiles around the view exist. The postal map has native tiles up to cfg.postalMax; deeper zooms scale those up.
function renderTiles(c){
  const postal=layer==="postal"&&cfg.postal;
  const z=postal?Math.min(zoom,cfg.postalMax||6):zoom,f=2**(zoom-z),T=256*f;
  const cols=Math.ceil(16384/2**(7-z)/256),rows=Math.ceil(24576/2**(7-z)/256);
  const left=c.x-innerWidth/2,top=c.y-innerHeight/2;
  const minX=Math.max(0,Math.floor(left/T)-1),maxX=Math.min(cols-1,Math.floor((left+innerWidth)/T)+1),minY=Math.max(0,Math.floor(top/T)-1),maxY=Math.min(rows-1,Math.floor((top+innerHeight)/T)+1),wanted=new Set();
  for(let y=minY;y<=maxY;y++)for(let x=minX;x<=maxX;x++){
    const key=postal?`postal/${z}/${x}/${y}`:`${layer}/${z}/${x}/${y}`;wanted.add(key);if(tileCache.has(key))continue;
    const img=document.createElement("img");img.className="tile";img.alt="";img.draggable=false;img.decoding="async";img.referrerPolicy="no-referrer";img.onerror=()=>{img.style.visibility="hidden"};
    img.src=postal?`${cfg.postal}${z}/${x}/${y}.jpg`:`${TILE_BASE}${key}.jpg`;
    img.style.width=img.style.height=(T+1)+"px";img.style.transform=`translate(${x*T}px,${y*T}px)`;tileCache.set(key,img);tiles.appendChild(img)}
  for(const [key,img] of tileCache)if(!wanted.has(key)){img.remove();tileCache.delete(key)}
}
function spriteName(id){const n=names[id];return n?(JP[n.replace(/^radar_/,"")]||n.replace(/^radar_/,"").replace(/_/g," ")):"アイコン #"+id}
// Blip icons: the reference PNGs are mostly a black symbol with a thin white edge. Each sprite is turned once into
// "symbol in the blip colour, edge dark" (light-symbol icons keep their light parts as the symbol) and drawn on a small
// canvas per blip — no CSS filter / mask per blip (those put every icon on its own compositor layer).
const iconSrc=new Map(),iconTint=new Map();
function iconSource(sprite){let e=iconSrc.get(sprite);if(e)return e;e={ready:false,failed:false,data:null,w:0,h:0,wait:[]};iconSrc.set(sprite,e);
  const img=new Image();img.decoding="async";
  img.onload=()=>{try{const c=document.createElement("canvas");c.width=e.w=img.naturalWidth||32;c.height=e.h=img.naturalHeight||32;const g=c.getContext("2d");g.drawImage(img,0,0);
      const d=g.getImageData(0,0,e.w,e.h).data;let solid=0,dark=0;for(let i=0;i<d.length;i+=4)if(d[i+3]>128){solid++;if(d[i]+d[i+1]+d[i+2]<384)dark++}
      // which pixels are the symbol: the dark ones when the icon is mostly dark, otherwise the light ones
      const darkSym=dark>solid/2,m=new Uint8ClampedArray(e.w*e.h);for(let i=0,k=0;i<d.length;i+=4,k++){const l=d[i]+d[i+1]+d[i+2];m[k]=d[i+3]<16?0:(darkSym?l<384:l>=384)?2:1}
      e.mask=m;e.alpha=d.filter((_,i)=>i%4===3);e.ready=true}catch{e.failed=true}
    for(const f of e.wait.splice(0))f()};
  img.onerror=()=>{e.failed=true;for(const f of e.wait.splice(0))f()};img.src=`/blip/${sprite}.png`;return e}
function tinted(sprite,color){const key=sprite+color;let c=iconTint.get(key);if(c)return c;const e=iconSource(sprite);if(!e.ready)return null;
  c=document.createElement("canvas");c.width=e.w;c.height=e.h;const g=c.getContext("2d"),out=g.createImageData(e.w,e.h),o=out.data,n=parseInt(color.slice(1),16),r=n>>16&255,gg=n>>8&255,b=n&255;
  for(let k=0;k<e.mask.length;k++){const v=e.mask[k],i=k*4;if(!v)continue;if(v===2){o[i]=r;o[i+1]=gg;o[i+2]=b}else{o[i]=8;o[i+1]=14;o[i+2]=20}o[i+3]=e.alpha[k]}
  g.putImageData(out,0,0);iconTint.set(key,c);return c}
// each (sprite, colour) becomes one small image (blob: URL) shared by every blip that uses it; plain <img> elements
// stay in the map's own layer (canvases / filters would each get a compositor layer)
const tintUrl=new Map();
function iconUrl(sprite,color,cb){const key=sprite+color;let v=tintUrl.get(key);if(typeof v==="string"){cb(v);return}if(v){v.push(cb);return}v=[cb];tintUrl.set(key,v);
  const e=iconSource(sprite),done=()=>{let c;
    if(e.failed){c=document.createElement("canvas");c.width=c.height=32;const g=c.getContext("2d");g.fillStyle="#08101a";g.beginPath();g.arc(16,16,8,0,7);g.fill();g.fillStyle=color;g.beginPath();g.arc(16,16,6,0,7);g.fill()}else c=tinted(sprite,color);
    c.toBlob(b=>{const url=b?URL.createObjectURL(b):"";tintUrl.set(key,url);for(const f of v)f(url)})};
  if(e.ready||e.failed)done();else e.wait.push(done)}
function paintBlip(el,sprite,color,size,inList){if(el.__px!==size){el.__px=size;el.style.width=el.style.height=size+"px";if(!inList)el.style.margin=`${-size/2}px 0 0 ${-size/2}px`}
  const key=sprite+color;if(el.__key===key)return;el.__key=key;iconUrl(sprite,color,url=>{if(el.__key===key&&url)el.src=url})}
function spriteName(id){const n=names[id];return n?(JP[n.replace(/^radar_/,"")]||n.replace(/^radar_/,"").replace(/_/g," ")):"アイコン #"+id}
// blip elements are reused by index; only changed properties are written
function renderBlips(){
  blipLayer.classList.toggle("off",!show.blips);
  if(!show.blips)return;
  const size=Math.round((zoom>=5?22:zoom>=4?19:16)*iconScale);
  for(let i=0;i<blips.length;i++){
    const b=blips[i];let el=blipEls[i];
    if(!el){el=new Image();el.className="blip";el.alt="";el.decoding="async";el.draggable=false;blipEls[i]=el;blipLayer.appendChild(el)}
    const sprite=num(b[0])|0,color=COLORS[num(b[1])|0]||"#ffffff",p=px(num(b[2]),num(b[3])),rot=num(b[4]),on=shownSprites.has(sprite);
    el.classList.toggle("hide",!on);
    if(el.__sprite!==sprite){el.__sprite=sprite;el.title=spriteName(sprite)}
    if(on)paintBlip(el,sprite,color,size);
    el.style.transform=`translate(${p.x}px,${p.y}px)`+(rot?` rotate(${-rot}deg)`:"");
  }
  while(blipEls.length>blips.length)blipEls.pop().remove();
  updateHint();
}
// public-service members (on-duty police / EMS): their own icon in their colour, heading, and name
function renderUnits(){
  unitLayer.classList.toggle("off",!show.units||!cfg.services);
  for(let i=0;i<units.length;i++){
    const u=units[i];let el=unitEls[i];
    if(!el){el=document.createElement("div");el.className="unit";el.innerHTML="<i></i><b></b>";unitEls[i]=el;unitLayer.appendChild(el)}
    const kind=Math.max(0,Math.min(5,num(u[3])|0)),color=COLORS[num(u[4])|0]||"#5db6e5",label=[u[6],u[5]].filter(Boolean).join(" "),p=px(num(u[0]),num(u[1]));
    const icon=el.firstChild;if(icon.__sprite!==KIND_SPRITE[kind]){icon.__sprite=KIND_SPRITE[kind];icon.style.setProperty("--m",`url(/blip/${KIND_SPRITE[kind]}.png)`)}
    el.style.setProperty("--c",color);
    if(el.lastChild.textContent!==label){el.lastChild.textContent=label;el.title=label}
    el.style.transform=`translate(${p.x}px,${p.y}px)`+(iconScale!==1?` scale(${iconScale})`:"");
    icon.style.transform=kind===0?"":`rotate(${-num(u[2])}deg)`;
  }
  while(unitEls.length>units.length)unitEls.pop().remove();
}
// ---------------------------------------------------------------- the player's items
const lumOf=h=>{const n=parseInt(h.slice(1),16);return(.2126*(n>>16&255)+.7152*(n>>8&255)+.0722*(n&255))/255};
function centerOf(it){if(it.t==="m"||it.t==="t")return{x:it.x,y:it.y};if(it.t==="a"){let x=0,y=0;for(const p of it.pts){x+=p[0];y+=p[1]}return{x:x/it.pts.length,y:y/it.pts.length}}return{x:(it.pts[0][0]+it.pts[1][0])/2,y:(it.pts[0][1]+it.pts[1][1])/2}}
function itemName(it){return it.label||TYPE[it.t]}
const P=(x,y)=>{const p=px(x,y);return p.x.toFixed(1)+","+p.y.toFixed(1)};
function lineSvg(it,cls){const a=px(it.pts[0][0],it.pts[0][1]),b=px(it.pts[1][0],it.pts[1][1]),w=it.width;let h=`<line class="ln" x1="${a.x.toFixed(1)}" y1="${a.y.toFixed(1)}" x2="${b.x.toFixed(1)}" y2="${b.y.toFixed(1)}" stroke="${it.color}" stroke-width="${w}" stroke-linecap="round"/>`;
  if(it.t==="r"){const ang=Math.atan2(b.y-a.y,b.x-a.x),L=Math.max(12,w*3.6),W=Math.max(8,w*2.6),bx=b.x-Math.cos(ang)*L,by=b.y-Math.sin(ang)*L,nx=-Math.sin(ang)*W/2,ny=Math.cos(ang)*W/2;
    h=`<line class="ln" x1="${a.x.toFixed(1)}" y1="${a.y.toFixed(1)}" x2="${(bx+Math.cos(ang)*2).toFixed(1)}" y2="${(by+Math.sin(ang)*2).toFixed(1)}" stroke="${it.color}" stroke-width="${w}" stroke-linecap="round"/><polygon class="ln" points="${b.x.toFixed(1)},${b.y.toFixed(1)} ${(bx+nx).toFixed(1)},${(by+ny).toFixed(1)} ${(bx-nx).toFixed(1)},${(by-ny).toFixed(1)}" fill="${it.color}"/>`}
  if(cls!=="draft")h+=`<line class="hit" x1="${a.x.toFixed(1)}" y1="${a.y.toFixed(1)}" x2="${b.x.toFixed(1)}" y2="${b.y.toFixed(1)}" stroke="transparent" stroke-width="${Math.max(16,w+10)}"/>`;
  if(it.label){const m=centerOf(it),q=px(m.x,m.y);h+=`<text x="${q.x.toFixed(1)}" y="${(q.y-w-9).toFixed(1)}">${esc(it.label)}</text>`}
  return h}
function draftSvg(){if(!draft)return"";const pts=draft.pts.slice();if(hoverAt)pts.push([hoverAt.x,hoverAt.y]);if(!pts.length)return"";
  if(draft.t==="a"){let poly=pts;if(opts.areaMode==="rect"&&pts.length>=2){const[x0,y0]=pts[0],[x1,y1]=pts[pts.length-1];poly=[[x0,y0],[x1,y0],[x1,y1],[x0,y1]]}
    if(poly.length<2){const q=px(poly[0][0],poly[0][1]);return`<circle cx="${q.x}" cy="${q.y}" r="4" fill="${opts.color.a}"/>`}
    return`<polygon points="${poly.map(p=>P(p[0],p[1])).join(" ")}" fill="${opts.color.a}" fill-opacity="${opts.opacity}" stroke="${opts.stroke}" stroke-width="2" stroke-dasharray="6 4"/>`}
  if(pts.length<2)return"";return`<g opacity=".8">${lineSvg({t:draft.t,pts:[pts[0],pts[pts.length-1]],color:opts.color.l,width:opts.width,label:""},"draft")}</g>`}
function shapeSvg(it){if(it.t==="a"){let h=`<g class="it${it.id===selId?" sel":""}" data-item="${it.id}"><polygon points="${it.pts.map(p=>P(p[0],p[1])).join(" ")}" fill="${it.color}" fill-opacity="${it.opacity}" stroke="${it.stroke}" stroke-width="2" stroke-linejoin="round"/>`;
    if(it.label){const m=centerOf(it),q=px(m.x,m.y);h+=`<text x="${q.x.toFixed(1)}" y="${q.y.toFixed(1)}">${esc(it.label)}</text>`}return h+"</g>"}
  return`<g class="it${it.id===selId?" sel":""}" data-item="${it.id}">${lineSvg(it)}</g>`}
function itemEl(it){const p=px(it.x,it.y),el=document.createElement("div");el.dataset.item=it.id;el.style.setProperty("--c",it.color);
  if(it.t==="m"){el.className="mk"+(it.id===selId?" sel":"");el.style.setProperty("--z",it.size+"px");el.style.setProperty("--on",lumOf(it.color)>.5?"#061018":"#ffffff");el.innerHTML=`<span>${svgOf(it.icon)}</span>`+(it.label?"<b></b>":"");if(it.label)el.querySelector("b").textContent=it.label;el.title=itemName(it);
    el.style.transform=`translate(${p.x}px,${p.y}px)`+(iconScale!==1?` scale(${iconScale})`:"")}
  else{el.className="tx"+(it.id===selId?" sel":"");el.innerHTML="<b></b>";el.firstChild.textContent=it.label;el.firstChild.style.fontSize=it.size+"px";el.style.transform=`translate(${p.x}px,${p.y}px)`}
  return el}
// everything (zoom change, edits); the draft preview and a dragged item have their own cheap updates below
function renderItems(){
  const s=2**(zoom-7);shapeLayer.setAttribute("width",16384*s);shapeLayer.setAttribute("height",24576*s);
  shapeLayer.classList.toggle("off",!show.pins);itemLayer.classList.toggle("off",!show.pins);
  let h="";for(const it of items)if(it.t==="a"||it.t==="l"||it.t==="r")h+=shapeSvg(it);
  shapeLayer.innerHTML=`<g id="itemsG">${h}</g><g id="draftG" class="draft">${draftSvg()}</g>`;
  itemLayer.textContent="";const f=document.createDocumentFragment();for(const it of items)if(it.t==="m"||it.t==="t")f.appendChild(itemEl(it));itemLayer.appendChild(f)}
function renderDraft(){const g=document.getElementById("draftG");if(g)g.innerHTML=draftSvg();else renderItems()}
function renderOne(it){if(it.t==="m"||it.t==="t"){const old=itemLayer.querySelector(`[data-item="${it.id}"]`);if(old)old.replaceWith(itemEl(it));else renderItems();return}
  const old=shapeLayer.querySelector(`#itemsG [data-item="${it.id}"]`);if(!old){renderItems();return}const t=document.createElementNS("http://www.w3.org/2000/svg","g");t.innerHTML=shapeSvg(it);old.replaceWith(t.firstChild)}
const itemById=id=>items.find(i=>i.id===id);
// ---------------------------------------------------------------- undo / redo (per preset, snapshots of its items)
let undoStack=[],redoStack=[],editBefore=null;
const snapshot=()=>JSON.stringify(items);
function updateUndo(){$("undo").disabled=!undoStack.length;$("redo").disabled=!redoStack.length}
function afterChange(){saveNow();renderItems();renderPinList();updateUndo();if(selId&&!itemById(selId))select(null);else if(selId)renderEdit()}
function commit(before){if(before===snapshot()){afterChange();return}undoStack.push(before);if(undoStack.length>80)undoStack.shift();redoStack=[];afterChange()}
function editCommit(){if(editBefore==null)return;const b=editBefore;editBefore=null;commit(b)}
function mutate(fn){editCommit();const b=snapshot();fn();commit(b)}
function restore(json){items=(JSON.parse(json)||[]).map(cleanItem).filter(Boolean)}
function undo(){editCommit();if(!undoStack.length)return;redoStack.push(snapshot());restore(undoStack.pop());afterChange();toast("元に戻しました")}
function redo(){editCommit();if(!redoStack.length)return;undoStack.push(snapshot());restore(redoStack.pop());afterChange();toast("やり直しました")}
// ---------------------------------------------------------------- tools
// The palette only picks what to draw. Colour, symbol, size... are set on the selected item in the edit panel, and
// the last values used there become the defaults for the next one of that kind.
function setTool(t){if(t==="r")t="l";if(t!==tool){draft=null;if(t!=="select")select(null)}tool=t;
  document.querySelectorAll("#dock [data-tool]").forEach(b=>b.classList.toggle("active",b.dataset.tool===t));map.classList.toggle("draw",t!=="select");renderToolOpts();renderDraft()}
function swatches(key,cur){return`<div class="sws">${SWATCHES.map(c=>`<button type="button" class="sw${c===cur?" on":""}" style="background:${c}" data-sw="${key}" data-c="${c}" title="${c}"></button>`).join("")}<label class="swc" title="ほかの色"><input type="color" data-cc="${key}" value="${cur}"></label></div>`}
function iconGrid(cur){return`<div class="icons">${Object.entries(ICONS).map(([k,v])=>`<button type="button" class="${k===cur?"on":""}" data-icon="${k}">${svgOf(k)}${v[0]}</button>`).join("")}</div>`}
function range(key,min,max,step,val,fmt){return`<div class="range"><input type="range" data-r="${key}" min="${min}" max="${max}" step="${step}" value="${val}"><b>${fmt(val)}</b></div>`}
const pct=v=>Math.round(v*100)+"%",pxs=v=>v+"px";
function renderToolOpts(){const box=$("toolOpts");if(tool==="select"){box.hidden=true;return}box.hidden=false;
  const tip=tool==="a"?(opts.areaMode==="rect"?"2点をクリックすると、その2点を角にした四角で囲みます。1点目のあと右クリックで取り消し。":"角をクリックしていき、ダブルクリック・Enter・右クリックで閉じます。"):TOOL_TIP[tool];
  let h=`<div class="fh"><span>${tool==="l"?"線・矢印":TYPE[tool]}</span><kbd>${TOOL_KEY[tool]}</kbd></div><div class="tip">${tip}色や形は、置いたあとに右のパネルで変えられます。</div>`;
  if(tool==="a")h+=`<div class="seg"><button type="button" data-mode="rect" class="${opts.areaMode==="rect"?"active":""}">四角</button><button type="button" data-mode="poly" class="${opts.areaMode==="poly"?"active":""}">自由な形</button></div>`;
  if(tool==="l")h+=`<div class="seg"><button type="button" data-arrow="0" class="${opts.arrow?"":"active"}">線</button><button type="button" data-arrow="1" class="${opts.arrow?"active":""}">矢印</button></div>`;
  box.innerHTML=h}
$("toolOpts").addEventListener("click",e=>{const b=e.target.closest("button");if(!b)return;
  if(b.dataset.mode){opts.areaMode=b.dataset.mode;draft=null;saveOpts();renderToolOpts();renderDraft()}
  else if(b.dataset.arrow){opts.arrow=b.dataset.arrow==="1";saveOpts();renderToolOpts();renderDraft()}});
document.querySelectorAll("#dock [data-tool]").forEach(b=>b.onclick=()=>setTool(b.dataset.tool));
// what the edit panel last used becomes the default for the next item of that kind
function rememberStyle(it){const k=it.t==="r"?"l":it.t;opts.color[k]=it.color;
  if(it.t==="m"){opts.icon=it.icon;opts.size=it.size}else if(it.t==="t")opts.tsize=it.size;else if(it.t==="a"){opts.stroke=it.stroke;opts.opacity=it.opacity}else{opts.width=it.width;opts.arrow=it.t==="r"}saveOpts()}
function addItem(o){if(items.length>=MAX_ITEMS){toast(`書き込めるのは${MAX_ITEMS}件までです`);return null}const it=cleanItem(o);if(!it)return null;mutate(()=>items.push(it));return it}
const clampG=g=>({x:Math.max(BOUNDS.x0,Math.min(BOUNDS.x1,g.x)),y:Math.max(BOUNDS.y0,Math.min(BOUNDS.y1,g.y))});
function placeAt(g){g=clampG(g);hoverAt=g;
  if(tool==="m"){const it=addItem({t:"m",x:g.x,y:g.y,icon:opts.icon,color:opts.color.m,size:opts.size});if(it){select(it.id);focusEditLabel(false)}return}
  if(tool==="t"){const it=addItem({t:"t",x:g.x,y:g.y,label:"テキスト",color:opts.color.t,size:opts.tsize});if(it){select(it.id);focusEditLabel(true)}return}
  if(tool==="a"){if(opts.areaMode==="rect"){if(!draft){draft={t:"a",pts:[[g.x,g.y]]}}else{const[x0,y0]=draft.pts[0];draft=null;
        const a=px(x0,y0),b=px(g.x,g.y);if(Math.abs(a.x-b.x)<4||Math.abs(a.y-b.y)<4){renderDraft();return}
        addItem({t:"a",pts:[[x0,y0],[g.x,y0],[g.x,g.y],[x0,g.y]],color:opts.color.a,stroke:opts.stroke,opacity:opts.opacity})}}
    else{draft=draft||{t:"a",pts:[]};draft.pts.push([g.x,g.y]);if(draft.pts.length>=64)finishPoly()}
    renderDraft();return}
  if(tool==="l"){const t=opts.arrow?"r":"l";if(!draft){draft={t,pts:[[g.x,g.y]]};renderDraft();return}const s0=draft.pts[0];draft=null;
    const a=px(s0[0],s0[1]),b=px(g.x,g.y);if(Math.hypot(a.x-b.x,a.y-b.y)<4){renderDraft();return}
    addItem({t,pts:[s0,[g.x,g.y]],color:opts.color.l,width:opts.width});renderDraft()}}
function finishPoly(){if(!draft||draft.t!=="a")return;let pts=draft.pts;
  // a double click adds the same point twice
  while(pts.length>=2){const a=px(pts[pts.length-1][0],pts[pts.length-1][1]),b=px(pts[pts.length-2][0],pts[pts.length-2][1]);if(Math.hypot(a.x-b.x,a.y-b.y)<6)pts=pts.slice(0,-1);else break}
  draft=null;if(pts.length>=3)addItem({t:"a",pts,color:opts.color.a,stroke:opts.stroke,opacity:opts.opacity});else toast("角が足りません（3点以上）");renderDraft()}
// ---------------------------------------------------------------- selection and the edit panel
function select(id){if(id!==selId)editCommit();const prev=selId;selId=id&&itemById(id)?id:null;
  for(const k of [prev,selId]){const it=k&&itemById(k);if(it)renderOne(it)}
  const p=$("editPanel");if(selId){if(!p.classList.contains("open")){closePanels(true);p.classList.add("open")}renderEdit()}else if(p.classList.contains("open"))p.classList.remove("open");layoutPanels()}
function focusEditLabel(selectAll){setTimeout(()=>{const f=$("editLabel");if(f){f.focus();if(selectAll)f.select()}},30)}
function renderEdit(){const it=itemById(selId),p=$("editPanel");if(!it)return;
  // keep what is being typed only when the panel is redrawn for the same item
  const keep=p.dataset.id===it.id&&document.activeElement&&p.contains(document.activeElement)&&document.activeElement.id==="editLabel";p.dataset.id=it.id;
  let h=`<h3>${TYPE[it.t]}を編集<span class="muted">${state.active?dist(distanceTo(centerOf(it).x,centerOf(it).y)):""}</span></h3>`;
  h+=`<div class="olab">${it.t==="t"?"文字":"名前（地図に表示）"}</div><input id="editLabel" class="field" type="text" maxlength="${it.t==="t"?80:40}" placeholder="${it.t==="t"?"文字を入力":"名前を入力（なしでも可）"}" autocomplete="off">`;
  h+=`<div class="olab">${it.t==="a"?"中の色":"色"}</div>${swatches("item:color",it.color)}`;
  if(it.t==="m")h+=`<div class="olab">ピンの絵柄</div>${iconGrid(it.icon)}<div class="olab">大きさ</div>${range("size",20,56,2,it.size,pxs)}`;
  if(it.t==="t")h+=`<div class="olab">大きさ</div>${range("size",12,40,1,it.size,pxs)}`;
  if(it.t==="a")h+=`<div class="olab">ふちの色</div>${swatches("item:stroke",it.stroke)}<div class="olab">中の濃さ</div>${range("opacity",.05,.9,.05,it.opacity,pct)}`;
  if(it.t==="l"||it.t==="r")h+=`<div class="olab">先端</div><div class="seg"><button type="button" data-arrow="0" class="${it.t==="l"?"active":""}">線</button><button type="button" data-arrow="1" class="${it.t==="r"?"active":""}">矢印</button></div><div class="olab">太さ</div>${range("width",1,10,1,it.width,pxs)}`;
  h+=`<div class="btns" style="margin-top:14px">${cfg.waypoint&&gameConnected?'<button type="button" data-act="wp">ウェイポイントにする</button>':""}<button type="button" data-act="go">地図で見る</button><button type="button" data-act="dup">複製</button><button type="button" class="danger" data-act="del">削除</button></div>`;
  if(keep){const v=$("editLabel").value,s=$("editLabel").selectionStart;p.innerHTML=h;$("editLabel").value=v;$("editLabel").focus();try{$("editLabel").setSelectionRange(s,s)}catch{}}else{p.innerHTML=h;$("editLabel").value=it.label}}
function editApply(fn){const it=itemById(selId);if(!it)return;if(editBefore==null)editBefore=snapshot();fn(it);rememberStyle(it);saveSoon();renderOne(it);renderPinList()}
$("editPanel").addEventListener("input",e=>{const t=e.target;
  if(t.id==="editLabel"){editApply(it=>{it.label=it.t==="t"?t.value.slice(0,80):t.value.slice(0,40)});return}
  if(t.dataset.cc&&HEX.test(t.value)){const k=t.dataset.cc.slice(5);editApply(it=>{it[k]=t.value.toLowerCase()});return}
  if(t.dataset.r){const v=num(t.value),k=t.dataset.r;editApply(it=>{it[k]=k==="opacity"?Math.round(v*100)/100:Math.round(v)});t.nextElementSibling.textContent=k==="opacity"?pct(v):pxs(Math.round(v))}});
$("editPanel").addEventListener("change",e=>{const t=e.target;if(t.id==="editLabel"){const it=itemById(selId);if(it&&it.t==="t"&&!it.label.trim())editApply(i=>{i.label="テキスト"})}editCommit();if(t.dataset.cc)renderEdit()});
$("editPanel").addEventListener("keydown",e=>{if(e.key==="Enter"&&e.target.id==="editLabel"){e.preventDefault();e.target.blur()}});
$("editPanel").addEventListener("click",e=>{const b=e.target.closest("button");if(!b)return;const it=itemById(selId);if(!it)return;
  if(b.dataset.sw){const k=b.dataset.sw.slice(5);editApply(i=>{i[k]=b.dataset.c});editCommit();renderEdit();return}
  if(b.dataset.icon){editApply(i=>{i.icon=b.dataset.icon});editCommit();renderEdit();return}
  if(b.dataset.arrow){editApply(i=>{i.t=b.dataset.arrow==="1"?"r":"l"});editCommit();renderEdit();return}
  const act=b.dataset.act;
  if(act==="del"){deleteItem(it);return}
  if(act==="go"){const c=centerOf(it);goTo(c.x,c.y);return}
  if(act==="wp"){const c=it.t==="l"||it.t==="r"?{x:it.pts[1][0],y:it.pts[1][1]}:centerOf(it);setWaypoint(c.x,c.y);return}
  if(act==="dup"){const off=12*2**(5-zoom),copy=JSON.parse(JSON.stringify(it));copy.id=newId();if(copy.pts)copy.pts=copy.pts.map(p=>[p[0]+off,p[1]-off]);else{copy.x+=off;copy.y-=off}const n=addItem(copy);if(n)select(n.id)}});
function deleteItem(it){mutate(()=>{items=items.filter(x=>x!==it)});toast(`${itemName(it)}を削除しました`)}
// ---------------------------------------------------------------- list panel
function renderPinList(){
  const rows=$("pinRows");if(!rows)return;$("pinPreset").textContent=presets.length>1?active.name:"";
  if(!items.length){rows.innerHTML='<div class="muted">まだ何も書き込んでいません。左のパレットから書き込めます。</div>';return}
  rows.innerHTML=items.map((it,i)=>{const c=centerOf(it),badge=it.t==="m"?`<span class="lic" style="background:${it.color};color:${lumOf(it.color)>.5?"#061018":"#fff"}">${svgOf(it.icon)}</span>`:`<span class="lic" style="background:${it.color}33;box-shadow:inset 0 0 0 2px ${it.color}"></span>`;
    return`<div class="row" data-i="${i}">${badge}<span class="grow">${esc(itemName(it))}${it.label?`<span class="muted"> · ${TYPE[it.t]}</span>`:""}</span><span class="muted">${state.active?dist(distanceTo(c.x,c.y)):""}</span>${cfg.waypoint?'<button class="wpb" data-act="wp" title="ウェイポイントにする"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 21V4M6 4h11l-2.5 4L17 12H6"/></svg></button>':""}<button data-act="del" title="削除">✕</button></div>`}).join("")}
$("pinRows").addEventListener("click",e=>{const row=e.target.closest(".row");if(!row)return;const it=items[num(row.dataset.i)];if(!it)return;const act=e.target.dataset.act;
  if(act==="del"){deleteItem(it);return}
  const c=it.t==="l"||it.t==="r"?{x:it.pts[1][0],y:it.pts[1][1]}:centerOf(it);
  if(act==="wp"||e.target.closest("[data-act=wp]")){setWaypoint(c.x,c.y);return}
  goTo(centerOf(it).x,centerOf(it).y);setTool("select");select(it.id)});
// ---------------------------------------------------------------- dispatch calls: a list only (no markers on the map)
function ago(at){const s=Math.max(0,Math.floor(Date.now()/1000-num(at)));return s<60?"たった今":s<3600?Math.floor(s/60)+"分前":Math.floor(s/3600)+"時間前"}
function renderAlerts(){
  const on=show.alerts&&cfg.services;
  $("alertToggle").hidden=!cfg.services;
  const badge=$("alertBadge");badge.hidden=!alerts.length;badge.textContent=alerts.length;
  const box=$("alerts");box.classList.toggle("open",on&&alerts.length>0&&!box.__closed);
  $("alertToggle").classList.toggle("active",box.classList.contains("open"));
  $("alertCount").textContent=alerts.length;
  $("alertRows").innerHTML=alerts.map((a,i)=>`<div class="a" data-i="${i}"><div class="t">${a.code?`<span class="code">${esc(a.code)}</span>`:""}${esc(a.title||"通報")}</div><div class="s">${esc([a.street,a.text].filter(Boolean).join(" · "))} · ${ago(a.at)}</div></div>`).join("");
  if(!box.classList.contains("open"))$("alertTip").hidden=true;
  layoutPanels();
}
function saveState(){active.items=items;active.shown=[...shownSprites];active.show={...show};
  const ok=store("mio-map-presets",JSON.stringify(presets));store("mio-map-preset",active.id);store("mio-map-svc",JSON.stringify({units:show.units,alerts:show.alerts}));
  if(!ok&&performance.now()-saveWarnAt>8000){saveWarnAt=performance.now();toast("保存できませんでした。ブラウザの保存容量がいっぱいです（書き込みやプリセットを減らしてください）")}
  return ok}
function saveNow(){clearTimeout(saveTimer);saveTimer=0;return saveState()}
function saveSoon(){clearTimeout(saveTimer);saveTimer=setTimeout(saveNow,400)}
addEventListener("pagehide",()=>{if(saveTimer)saveNow()});
// ---------------------------------------------------------------- 表示 panel
function renderFilter(){
  const counts=new Map();for(const b of blips){const s=num(b[0])|0;counts.set(s,(counts.get(s)||0)+1)}
  const q=$("iconSearch").value.trim().toLowerCase();
  const list=[...counts.entries()].sort((a,b)=>b[1]-a[1]||a[0]-b[0]).filter(([s])=>!q||spriteName(s).toLowerCase().includes(q));
  $("spriteList").innerHTML=list.length?list.map(([s,n])=>`<label class="row"><input type="checkbox" data-sprite="${s}" ${shownSprites.has(s)?"checked":""}><img class="ico" data-ico="${s}" alt=""><span class="grow">${esc(spriteName(s))}</span><span class="muted">${n}</span></label>`).join(""):`<div class="muted">${counts.size?"見つかりません":"アイコンを受信すると一覧が出ます"}</div>`;
  for(const c of $("spriteList").querySelectorAll("img[data-ico]"))paintBlip(c,num(c.dataset.ico)|0,"#e8f6fb",20,true);
  const toggles=[["blips","地図アイコン"],["pins","書き込み"],["loc","現在地（通り名・速度）"]].concat(cfg.services?[["units","同僚の位置"],["alerts","通報の一覧"]]:[]);
  $("layerToggles").innerHTML=toggles.map(([k,l])=>`<label class="row"><input type="checkbox" data-show="${k}" ${show[k]?"checked":""}><span class="grow">${l}</span></label>`).join("");
}
// "icons are picked in 表示": shown once while nothing is ticked, until closed
function updateHint(){$("hint").hidden=!(blips.length&&show.blips&&shownSprites.size===0&&read("mio-map-hint")!=="1"&&!document.querySelector(".side.open"))}
function updateInfo(){
  const street=[state.street,state.crossing].filter(Boolean).join(" × ");
  $("location").innerHTML=(state.postal?`<span class="postal" title="番地">${esc(state.postal)}</span>`:"")+esc(street||(state.active?"現在地を取得中":"FiveM を待っています"));
  const parts=[`<span>${esc(state.zone||"MioCity")}</span>`];
  if(state.active&&(state.inVehicle||num(state.speedKmh)>2))parts.push(`<span><b>${Math.round(num(state.speedKmh))}</b> km/h</span>`);
  if(state.active&&state.hasWaypoint)parts.push(`<span class="wpd">目的地まで <b>${dist(distanceTo(num(state.waypointX),num(state.waypointY)))}</b></span>`);
  $("sub").innerHTML=parts.join("");
  render();
}
function applyConfig(){
  const postalBtn=document.querySelector('[data-layer="postal"]');postalBtn.hidden=!cfg.postal;
  if(layer==="postal"&&!cfg.postal)layer="render";
  document.querySelectorAll("[data-layer]").forEach(x=>x.classList.toggle("active",x.dataset.layer===layer));
  $("privacy").textContent=layer==="postal"?"背景の「番地」地図はゲームサーバーから、それ以外の地図は外部の配信元から読み込みます。位置などのデータはこのPCの外へ出ません。":"背景の地図は外部の配信元から読み込みます。位置などのデータはこのPCの外へ出ません。";
  clearTiles();blipsDirty=true;renderFilter();renderAlerts();renderPinList();if(selId)renderEdit();render(false);
}
function status(){disabled.hidden=gameConnected&&mapEnabled;
  if(!gameConnected){$("disTitle").textContent="FiveM を待っています";$("disText").innerHTML="Media Link を起動したまま FiveM で MioCity に接続すると、自動で地図が出ます。"}
  else{$("disTitle").textContent="セカンドモニターマップは OFF です";$("disText").innerHTML="ゲーム内の UI 設定で「外部マップを有効にする」を ON にしてください。<br>ZSX UI：UI メニュー → Misc → MioCity Media Link<br>mio_ui：UI 設定 → Media Link"}
  const on=gameConnected&&mapEnabled,label=!gameConnected?"FiveM を待っています":mapEnabled?"接続中":"マップ OFF";
  $("conn").classList.toggle("on",on);$("stat").classList.toggle("on",on);$("stat").firstChild.textContent=label;$("stat").title=label;
  if(!gameConnected){$("location").textContent="FiveM を待っています";$("sub").innerHTML='<span>FiveM で MioCity に接続すると自動でつながります</span>'}render(false)}
function connect(){clearTimeout(retry);const protocol=location.protocol==="https:"?"wss":"ws";const ws=new WebSocket(`${protocol}://${location.host}/map-view${location.search}`);socket=ws;
  ws.onmessage=e=>{let m;try{m=JSON.parse(e.data)}catch{return}const d=m&&m.data||{};
    if(m.type==="bridge.status"){gameConnected=d.connected===true;mapEnabled=d.mapEnabled===true;status()}
    else if(m.type==="map.state"){state=d;updateInfo()}
    else if(m.type==="map.blips"){blips=Array.isArray(d.blips)?d.blips:[];blipsDirty=true;if($("viewPanel").classList.contains("open"))renderFilter();render()}
    else if(m.type==="map.units"){units=Array.isArray(d.units)?d.units:[];renderUnits()}
    else if(m.type==="map.alerts"){const before=alerts.length?alerts[0].id:0;alerts=Array.isArray(d.alerts)?d.alerts:[];if(alerts.length&&alerts[0].id!==before)$("alerts").__closed=false;renderAlerts()}
    else if(m.type==="map.config"){cfg={waypoint:d.waypoint===true,postal:typeof d.postal==="string"&&/^https?:\/\/[^"\\\s]+\/$/.test(d.postal)?d.postal:"",postalMax:Math.max(2,Math.min(7,num(d.postalMax)||6)),services:d.services===true};applyConfig()}};
  ws.onclose=()=>{if(socket===ws)socket=null;gameConnected=false;status();retry=setTimeout(connect,2500)};ws.onerror=()=>ws.close()}
// ---------------------------------------------------------------- commands (set / clear the in-game waypoint)
function setWaypoint(x,y){if(!cfg.waypoint)return;if(!inMap(x,y)){toast("地図の範囲外にはウェイポイントを置けません");return}if(send("map.waypoint",{x:Math.round(x*10)/10,y:Math.round(y*10)/10}))toast("ゲームのウェイポイントを設定しました");else toast("FiveM に接続されていません")}
function clearWaypoint(){if(cfg.waypoint&&send("map.clearWaypoint",{}))toast("ウェイポイントを解除しました")}
// ---------------------------------------------------------------- right-click menu
function closeMenu(){menu.classList.remove("open");menuAt=null;menuItem=null}
function openMenu(e,it,at,head){
  menuAt=at||(it?centerOf(it):screenToGame(e.clientX,e.clientY));menuItem=it||null;
  $("menuHead").textContent=head||(it?itemName(it):(state.active?`ここまで ${dist(distanceTo(menuAt.x,menuAt.y))}`:`${menuAt.x.toFixed(1)}, ${menuAt.y.toFixed(1)}`));
  $("mWaypoint").hidden=!cfg.waypoint||!gameConnected;$("mClearWp").hidden=!cfg.waypoint||!state.hasWaypoint||!gameConnected;
  $("mMarker").hidden=!!it||!!head;$("mEdit").hidden=!it;$("mDelItem").hidden=!it;
  menu.classList.add("open");const r=menu.getBoundingClientRect();
  menu.style.left=Math.min(e.clientX,innerWidth-r.width-8)+"px";menu.style.top=Math.min(e.clientY,innerHeight-r.height-8)+"px";
}
const itemAt=t=>{const el=t&&t.closest&&t.closest("[data-item]");return el?itemById(el.dataset.item):null};
map.addEventListener("contextmenu",e=>{e.preventDefault();if(!gameConnected||!mapEnabled)return;
  if(draft){if(draft.t==="a"&&opts.areaMode==="poly"&&draft.pts.length>=3)finishPoly();else{draft=null;renderDraft()}return}
  openMenu(e,tool==="select"?itemAt(e.target):null)});
$("mWaypoint").onclick=()=>{if(menuAt){const it=menuItem,c=it&&(it.t==="l"||it.t==="r")?{x:it.pts[1][0],y:it.pts[1][1]}:menuAt;setWaypoint(c.x,c.y)}closeMenu()};
$("mClearWp").onclick=()=>{clearWaypoint();closeMenu()};
$("mMarker").onclick=()=>{const at=menuAt&&clampG(menuAt);closeMenu();if(at){const it=addItem({t:"m",x:at.x,y:at.y,icon:opts.icon,color:opts.color.m,size:opts.size});if(it){select(it.id);focusEditLabel(false)}}};
$("mEdit").onclick=()=>{const it=menuItem;closeMenu();if(it){setTool("select");select(it.id);focusEditLabel(false)}};
$("mDelItem").onclick=()=>{const it=menuItem;closeMenu();if(it)deleteItem(it)};
$("mCopy").onclick=()=>{const at=menuAt;closeMenu();if(at)navigator.clipboard?.writeText(`${at.x.toFixed(2)}, ${at.y.toFixed(2)}`).then(()=>toast("座標をコピーしました")).catch(()=>{})};
addEventListener("pointerdown",e=>{if(!menu.contains(e.target))closeMenu()},true);
function goTo(x,y){follow=false;center={x,y};setZoom(Math.max(zoom,5));render(false)}
// ---------------------------------------------------------------- panels
function closePanels(keepEdit){document.querySelectorAll(".side").forEach(s=>{if(!(keepEdit===true&&s.id==="editPanel"))s.classList.remove("open")});document.querySelectorAll("#view,#pinList,#presetBtn").forEach(b=>b.classList.remove("active"));if(keepEdit!==true&&selId){editCommit();const it=itemById(selId);selId=null;if(it)renderOne(it)}updateHint();layoutPanels()}
// a side panel (top right) and the dispatch list (bottom right) share the right edge: the panel stops above the list
function layoutPanels(){const box=$("alerts"),h=box.classList.contains("open")?box.offsetHeight+12:0;for(const s of document.querySelectorAll(".side"))s.style.maxHeight=`calc(100% - ${36+h}px)`}
function togglePanel(id,btn){const el=$(id),open=!el.classList.contains("open");closePanels();if(open){el.classList.add("open");btn.classList.add("active");$("alertTip").hidden=true;layoutPanels()}if(id==="viewPanel"&&open){renderFilter();$("hint").hidden=true}if(id==="pinPanel"&&open)renderPinList();if(id==="presetPanel"&&open)renderPresets()}
$("view").onclick=()=>togglePanel("viewPanel",$("view"));
$("pinList").onclick=()=>togglePanel("pinPanel",$("pinList"));
$("presetBtn").onclick=()=>togglePanel("presetPanel",$("presetBtn"));
$("helpClose").onclick=()=>$("helpDialog").classList.remove("open");
$("alertToggle").onclick=()=>{const box=$("alerts");box.__closed=box.classList.contains("open");renderAlerts()};
$("alertClose").onclick=()=>{$("alerts").__closed=true;renderAlerts()};
$("hintOpen").onclick=()=>{if(!$("viewPanel").classList.contains("open"))togglePanel("viewPanel",$("view"))};
$("hintClose").onclick=()=>{store("mio-map-hint","1");updateHint()};
$("iconSearch").addEventListener("input",renderFilter);
$("undo").onclick=undo;$("redo").onclick=redo;
// hover a call: its details beside the list
const PRIORITY={1:"高",2:"通常",3:"低"};
function clock(at){const d=new Date(num(at)*1000);return isNaN(d)?"":String(d.getHours()).padStart(2,"0")+":"+String(d.getMinutes()).padStart(2,"0")}
function showTip(row){const a=alerts[num(row.dataset.i)],tip=$("alertTip");if(!a){tip.hidden=true;return}
  const meta=[["場所",a.street],["受信",clock(a.at)+" · "+ago(a.at)],["距離",state.active?dist(distanceTo(num(a.x),num(a.y))):""],["優先度",PRIORITY[num(a.priority)]||""]].filter(m=>m[1]);
  tip.innerHTML=`<div class="h">${a.code?`<span class="code">${esc(a.code)}</span>`:""}<span>${esc(a.title||"通報")}</span></div>${a.text?`<div class="body">${esc(a.text)}</div>`:""}<div class="meta">${meta.map(m=>`<b>${m[0]}</b><span>${esc(m[1])}</span>`).join("")}</div><div class="tiphint">クリックで地図をこの場所へ · 右クリックでウェイポイント</div>`;
  tip.hidden=false;const r=row.getBoundingClientRect(),box=$("alerts").getBoundingClientRect(),h=tip.offsetHeight;
  tip.style.left=Math.max(8,box.left-tip.offsetWidth-10)+"px";tip.style.top=Math.max(8,Math.min(innerHeight-h-8,r.top))+"px"}
$("alertRows").addEventListener("mouseover",e=>{const row=e.target.closest(".a");if(row)showTip(row)});
$("alertRows").addEventListener("mouseleave",()=>{$("alertTip").hidden=true});
$("alertRows").addEventListener("click",e=>{const row=e.target.closest(".a");if(!row)return;const a=alerts[num(row.dataset.i)];if(a)goTo(num(a.x),num(a.y))});
$("alertRows").addEventListener("contextmenu",e=>{const row=e.target.closest(".a");if(!row)return;const a=alerts[num(row.dataset.i)];if(!a)return;e.preventDefault();openMenu(e,null,{x:num(a.x),y:num(a.y)},(a.code?a.code+" ":"")+(a.title||"通報"))});
$("viewPanel").addEventListener("change",e=>{const t=e.target;if(t.dataset.show==="pins")itemsDirty=true;
  if(t.dataset.sprite){const s=num(t.dataset.sprite)|0;if(t.checked)shownSprites.add(s);else shownSprites.delete(s);saveState()}
  if(t.dataset.show){show[t.dataset.show]=t.checked;saveState();renderAlerts();$("locCard").hidden=!show.loc}
  blipsDirty=true;render(false)});
$("allOn").onclick=()=>{for(const b of blips)shownSprites.add(num(b[0])|0);saveState();renderFilter();blipsDirty=true;render(false)};
$("allOff").onclick=()=>{shownSprites.clear();saveState();renderFilter();blipsDirty=true;render(false)};
// ---------------------------------------------------------------- presets
function renderPresets(){
  $("presetRows").innerHTML=presets.map((p,i)=>`<div class="row prow${p===active?" on":""}" data-i="${i}"><span class="key">${i<9?i+1:""}</span><span class="grow">${esc(p.name)}</span><span class="muted">${p.items.length}件</span><button data-act="ren" title="名前を変える">名前</button>${presets.length>1?'<button data-act="del" title="削除">✕</button>':""}</div>`).join("");
  $("presetNew").disabled=$("presetDup").disabled=presets.length>=MAX_PRESETS;
  updatePresetUi()}
function updatePresetUi(){$("presetName").textContent=active.name;$("pinPreset").textContent=presets.length>1?active.name:""}
function switchPreset(p){if(!p||p===active)return;
  // a drag in progress belongs to the preset being left
  if(drag){const d=drag;drag=null;map.classList.remove("dragging");if(d.move&&d.moved)commit(d.before)}
  editCommit();if(active)saveNow();active=p;
  const svc={units:show.units,alerts:show.alerts};for(const k in show)delete show[k];Object.assign(show,SHOW_DEFAULT,p.show,svc);shownSprites.clear();for(const s of p.shown)shownSprites.add(s);items=p.items;
  selId=null;draft=null;undoStack=[];redoStack=[];updateUndo();$("editPanel").classList.remove("open");
  saveNow();$("locCard").hidden=!show.loc;blipsDirty=true;renderPresets();renderItems();renderPinList();renderFilter();renderAlerts();render(false)}
function addPreset(p){if(presets.length>=MAX_PRESETS){toast(`プリセットは${MAX_PRESETS}個までです`);return false}const prev=active;presets.push(p);switchPreset(p);
  if(!saveNow()){presets.splice(presets.indexOf(p),1);active=null;switchPreset(prev);saveNow();toast("保存容量が足りないため追加できませんでした");return false}return true}
// show everything a preset holds (after importing it)
function fitItems(list){if(!list.length)return;let x0=1e9,y0=1e9,x1=-1e9,y1=-1e9;for(const it of list)for(const q of it.pts||[[it.x,it.y]]){x0=Math.min(x0,q[0]);x1=Math.max(x1,q[0]);y0=Math.min(y0,q[1]);y1=Math.max(y1,q[1])}
  follow=false;center={x:(x0+x1)/2,y:(y0+y1)/2};let z=MAX_ZOOM;for(;z>MIN_ZOOM;z--){const s=2**z;if((x1-x0)*s*U<innerWidth-260&&(y1-y0)*s*-D<innerHeight-160)break}setZoom(z);render(false)}
let pdMode=null,pdTarget=null;
function openPresetDialog(mode,target){pdMode=mode;pdTarget=target||null;
  const t={new:["新しいプリセット","作る"],dup:["今のプリセットを複製","作る"],ren:["名前を変える","保存"],export:["プリセットを書き出す","コピー"],import:["プリセットを読み込む","読み込む"]}[mode];
  $("pdTitle").textContent=t[0];$("pdOk").textContent=t[1];
  const name=$("pdName"),text=$("pdText");name.hidden=mode==="export";text.hidden=mode!=="export"&&mode!=="import";text.readOnly=mode==="export";
  name.value=mode==="ren"?target.name:mode==="dup"?active.name+" のコピー":"";
  text.value=mode==="export"?exportText(active):"";text.placeholder=mode==="import"?"書き出した文字、または座標の一覧を貼り付け\n例）\n-591.7, -287.5 Noir Cafe\nvector3(1138.2, -982.4, 46.4) 素材A":"";
  $("pdNote").textContent=mode==="export"?`「${active.name}」の書き込み（${items.length}件）と地図アイコンの種類です。この文字を渡すと、相手は「読み込む」で同じものを使えます。`:mode==="import"?"新しいプリセットとして追加します。名前を空にすると書き出し元の名前になります。":mode==="new"?"書き込みは空で、アイコンの種類は今のプリセットと同じから始まります。":"";
  $("presetDialog").classList.add("open");setTimeout(()=>{(mode==="export"||mode==="import"?text:name).focus();if(mode==="export")text.select()},30)}
function closePresetDialog(){$("presetDialog").classList.remove("open");pdMode=null;pdTarget=null}
function exportText(p){const list=p===active?items:p.items;return JSON.stringify({miomap:2,name:p.name,items:list.map(({id,...rest})=>rest),shown:p===active?[...shownSprites]:p.shown,show:p===active?{...show}:p.show})}
// our export (1.6 pins too), or plain lines with two numbers (x, y; a third number = z is ignored) and a name before / after them
function parseImport(text){text=String(text||"").trim();let skipped=0;if(!text)return null;
  if(text[0]==="{"){try{const o=JSON.parse(text);if(o&&typeof o==="object"&&(Array.isArray(o.items)||Array.isArray(o.pins)||Array.isArray(o.shown)))return cleanPreset({...o,id:""})}catch{}}
  if(text.length>200000)return null;   // a coordinate list this long is not one
  const N="-?\\d{1,6}(?:\\.\\d{1,6})?",commaPair=new RegExp(`(${N})\\s*,\\s*(${N})(?:\\s*,\\s*${N})?`),spacePair=new RegExp(`(${N})\\s+(${N})(?:\\s+${N})?`);
  const list=[];for(const raw of text.split(/\r?\n/)){if(!raw.trim())continue;if(raw.length>300){skipped++;continue}
    const line=raw.replace(/vec(?:tor)?[234]?\s*\(/gi," ").replace(/[()\[\]{}"]/g," ");
    // "x, y" wins over numbers in the name ("Apt 3 100, 200"); without a comma the last pair of numbers is used
    let m=line.match(commaPair);if(!m){const all=[...line.matchAll(new RegExp(spacePair.source,"g"))];m=all.length?all[all.length-1]:null}
    if(!m){skipped++;continue}
    const label=(line.slice(0,m.index)+" "+line.slice(m.index+m[0].length)).replace(/^[\s,:;|\-]+|[\s,:;|\-]+$/g,"");
    const it=cleanItem({t:"m",x:+m[1],y:+m[2],label,icon:"pin",color:opts.color.m,size:32});if(it&&inMap(it.x,it.y))list.push(it);else skipped++;if(list.length>=MAX_ITEMS)break}
  if(!list.length)return null;const p=cleanPreset({items:list,shown:[...shownSprites],show:{...show}});p.skipped=skipped;return p}
$("presetForm").onsubmit=e=>{e.preventDefault();const mode=pdMode,name=str($("pdName").value,24);
  if(mode==="export"){const v=$("pdText").value;(navigator.clipboard?navigator.clipboard.writeText(v):Promise.reject()).then(()=>{toast("コピーしました");closePresetDialog()}).catch(()=>{$("pdText").select();toast("Ctrl+C でコピーしてください")});return}
  if(mode==="ren"){if(pdTarget&&name){pdTarget.name=name;saveState();renderPresets()}closePresetDialog();return}
  if(mode==="new"){if(addPreset(cleanPreset({name:name||"プリセット"+(presets.length+1),shown:[...shownSprites],show:{...show}})))toast(`プリセット：${active.name}`);closePresetDialog();return}
  if(mode==="dup"){saveState();if(addPreset(cleanPreset({...JSON.parse(exportText(active)),name:name||active.name+" のコピー"})))toast(`プリセット：${active.name}`);closePresetDialog();return}
  if(mode==="import"){const p=parseImport($("pdText").value);if(!p){toast("読み込める内容がありません");return}if(name)p.name=name;else if(!/^\s*\{/.test($("pdText").value))p.name="読み込み"+(presets.length+1);
    const skipped=p.skipped||0;delete p.skipped;if(addPreset(p)){toast(`「${p.name}」を追加しました（${p.items.length}件${skipped?`、読めなかった行 ${skipped}`:""}）`);closePresetDialog();fitItems(p.items)}}};
$("pdCancel").onclick=closePresetDialog;
$("presetNew").onclick=()=>openPresetDialog("new");$("presetDup").onclick=()=>openPresetDialog("dup");
$("presetExport").onclick=()=>{saveState();openPresetDialog("export")};$("presetImport").onclick=()=>openPresetDialog("import");
$("presetRows").addEventListener("click",e=>{const row=e.target.closest(".row");if(!row)return;const p=presets[num(row.dataset.i)];if(!p)return;const act=e.target.dataset.act;
  if(act==="ren"){openPresetDialog("ren",p);return}
  if(act==="del"){if(presets.length<2||!confirm(`「${p.name}」を削除しますか？（書き込み${p.items.length}件も消えます）`))return;const i=presets.indexOf(p);presets.splice(i,1);if(p===active){active=null;switchPreset(presets[Math.max(0,i-1)])}else{saveState();renderPresets()}return}
  switchPreset(p)});
// icon size (表示)
function showIconSize(){$("iconSize").value=Math.round(iconScale*100);$("iconSizeVal").textContent=Math.round(iconScale*100)+"%"}
$("iconSize").addEventListener("input",e=>{iconScale=Math.max(.6,Math.min(2,num(e.target.value)/100));store("mio-map-iconsize",String(iconScale));showIconSize();blipsDirty=true;itemsDirty=true;render(false)});
// ---------------------------------------------------------------- keyboard
const typing=t=>t&&t.closest&&t.closest("input,textarea,select");
addEventListener("keydown",e=>{
  const dialog=document.querySelector(".dialog.open");
  if(e.key==="Escape"){closeMenu();if(dialog){dialog.classList.remove("open");pdMode=null;return}if(typing(e.target)){const inPanel=e.target.closest(".side:not(#editPanel)");e.target.blur();if(inPanel)closePanels();return}
    if(draft){draft=null;renderDraft();return}if(tool!=="select"){setTool("select");return}if(selId){select(null);return}closePanels();return}
  if(dialog||typing(e.target))return;
  const k=e.key.toLowerCase(),mod=e.ctrlKey||e.metaKey;
  if(drag&&drag.move)return;   // finish the move first
  if(mod&&k==="z"&&!e.shiftKey){e.preventDefault();undo();return}
  if(mod&&(k==="y"||(k==="z"&&e.shiftKey))){e.preventDefault();redo();return}
  if(mod||e.altKey)return;
  if(e.key==="Enter"&&draft){finishPoly();return}
  if((e.key==="Delete"||e.key==="Backspace")&&selId){const it=itemById(selId);if(it){e.preventDefault();deleteItem(it)}return}
  const tools={v:"select",p:"m",t:"t",s:"a",l:"l"};if(tools[k]){setTool(tools[k]);return}
  if(k==="f"){follow=true;render(false);return}
  if(e.key==="+"||e.key===";"||e.key==="="){zoomAt(zoom+1);return}if(e.key==="-"){zoomAt(zoom-1);return}
  if(e.key==="?"){$("helpDialog").classList.add("open");return}
  if(/^[1-9]$/.test(e.key)){const p=presets[+e.key-1];if(p&&p!==active){switchPreset(p);toast(`プリセット：${p.name}`)}}});
// ---------------------------------------------------------------- dragging / zoom / clicks
function setZoom(z){z=Math.max(MIN_ZOOM,Math.min(MAX_ZOOM,z));if(z===zoom)return false;zoom=z;store("mio-map-zoom",String(z));return true}
// zoom keeping the map point under (cx, cy) where it is; while following, around the player
function zoomAt(z,cx,cy){
  if(follow||cx==null){if(setZoom(z))render(false);return}
  const g=screenToGame(cx,cy);if(!setZoom(z))return;
  const p=px(g.x,g.y);center=toGame(p.x-(cx-innerWidth/2),p.y-(cy-innerHeight/2));render(false)}
// double clicks are recognised here (two clicks within 350 ms / 6 px): with pointer capture on the map the browser's
// own dblclick is not dependable
let lastClick={t:0,x:-99,y:-99};
function endDrag(e){const d=drag;drag=null;map.classList.remove("dragging");if(!d)return;
  if(d.move){if(d.moved)commit(d.before);return}
  if(d.moved||!e||e.type!=="pointerup")return;
  const now=performance.now(),dbl=now-lastClick.t<350&&Math.hypot(e.clientX-lastClick.x,e.clientY-lastClick.y)<6;lastClick=dbl?{t:0,x:-99,y:-99}:{t:now,x:e.clientX,y:e.clientY};
  if(tool==="select"){if(dbl){if(!itemAt(d.target)){follow=false;zoomAt(zoom+1,e.clientX,e.clientY)}}else if(!itemAt(d.target))select(null);return}
  if(dbl&&tool==="a"&&draft&&opts.areaMode==="poly"){finishPoly();return}
  if(dbl)return;   // the first click already placed / started something
  placeAt(screenToGame(e.clientX,e.clientY))}
map.addEventListener("pointerdown",e=>{if(e.button!==0)return;closeMenu();
  const it=tool==="select"?itemAt(e.target):null;
  if(it){const was=it.id===selId;if(!was)select(it.id);
    // only an item that is already selected moves with the mouse (dragging over an area otherwise pans the map)
    if(was){editCommit();drag={move:true,item:it,x:e.clientX,y:e.clientY,start:screenToGame(e.clientX,e.clientY),orig:JSON.parse(JSON.stringify(it)),before:snapshot(),moved:false};map.setPointerCapture(e.pointerId);return}}
  drag={x:e.clientX,y:e.clientY,center:{...center},moved:false,target:e.target};world.classList.remove("smooth");map.classList.add("dragging");map.setPointerCapture(e.pointerId)});
map.addEventListener("pointermove",e=>{
  const g=screenToGame(e.clientX,e.clientY);$("coords").textContent=`X ${String(Math.round(g.x)).padStart(5)}  Y ${String(Math.round(g.y)).padStart(5)}`;
  if(draft){hoverAt=g;if(!drag||!drag.moved)scheduleDraft()}
  if(!drag)return;if(!drag.moved&&Math.hypot(e.clientX-drag.x,e.clientY-drag.y)<4)return;drag.moved=true;
  if(drag.move){const it=itemById(drag.item.id);if(!it)return;const dx=g.x-drag.start.x,dy=g.y-drag.start.y,o=drag.orig;
    if(o.pts)it.pts=o.pts.map(p=>{const q=clampG({x:p[0]+dx,y:p[1]+dy});return[r1(q.x),r1(q.y)]});else{const q=clampG({x:o.x+dx,y:o.y+dy});it.x=r1(q.x);it.y=r1(q.y)}scheduleMove(it);return}
  follow=false;const c=px(drag.center.x,drag.center.y);center=toGame(c.x-(e.clientX-drag.x),c.y-(e.clientY-drag.y));render(false)});
let itemsFrame=0;function scheduleDraft(){if(!itemsFrame)itemsFrame=requestAnimationFrame(()=>{itemsFrame=0;renderDraft()})}
let moveFrame=0;function scheduleMove(it){if(!moveFrame)moveFrame=requestAnimationFrame(()=>{moveFrame=0;renderOne(it)})}
map.addEventListener("pointerleave",()=>{$("coords").textContent="X —    Y —";if(draft){hoverAt=null;scheduleDraft()}});
map.addEventListener("pointerup",endDrag);map.addEventListener("pointercancel",()=>endDrag(null));
let wheelAt=0;
map.addEventListener("wheel",e=>{e.preventDefault();const now=performance.now();if(now-wheelAt<120)return;wheelAt=now;zoomAt(zoom+(e.deltaY<0?1:-1),e.clientX,e.clientY)},{passive:false});
$("follow").onclick=()=>{follow=true;render(false)};$("zoomIn").onclick=()=>zoomAt(zoom+1);$("zoomOut").onclick=()=>zoomAt(zoom-1);
$("fullscreen").onclick=()=>document.fullscreenElement?document.exitFullscreen():document.documentElement.requestFullscreen().catch(()=>{});
document.querySelectorAll("[data-layer]").forEach(b=>{b.onclick=()=>{layer=b.dataset.layer;store("mio-map-layer",layer);applyConfig()}});
fetch("/blip/names.json").then(r=>r.ok?r.json():{}).then(n=>{names=n||{};for(const el of blipEls)if(el.__sprite!=null)el.title=spriteName(el.__sprite);if($("viewPanel").classList.contains("open"))renderFilter()}).catch(()=>{});
setInterval(()=>{if(alerts.length)renderAlerts();if($("pinPanel").classList.contains("open"))renderPinList()},30000);
addEventListener("resize",()=>render(false));$("locCard").hidden=!show.loc;showIconSize();updatePresetUi();updateUndo();saveState();renderToolOpts();applyConfig();status();connect();
})();
</script>
</body></html>
""";
}
