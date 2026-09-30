namespace MioCity.LocalMediaBridge;

public static class CompanionMapPage
{
    // The page needs its inline script/style and a WebSocket back to this loopback server. Images: this server (blip
    // icons), the public GTA V tiles (LB Phone's host) and the game server's own "番地" tiles, whose address the game
    // sends in map.config (any http(s) host, so img-src cannot list it in advance; images cannot carry data out).
    public const string ContentSecurityPolicy =
        "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; " +
        "img-src 'self' https: http:; " +
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
:root{--bg:#071018ec;--line:#ffffff1a;--ink:#e8f6fb;--mute:#8fa7b2;--acc:#0dd4fc;--red:#ff5470}
*{box-sizing:border-box}html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#07131b;color:var(--ink);font-family:system-ui,"Yu Gothic UI",sans-serif}button,input{font:inherit;color:inherit}
.card{background:var(--bg);border:1px solid var(--line);box-shadow:0 12px 32px #0009;backdrop-filter:blur(12px);border-radius:10px}
#map{position:absolute;inset:0;overflow:hidden;cursor:grab;background:#0d2b4f;user-select:none}#map.dragging{cursor:grabbing}.tile{position:absolute;left:0;top:0;width:257px;height:257px;object-fit:cover;pointer-events:none}
#world{position:absolute;left:0;top:0;will-change:transform}#world.smooth{transition:transform .5s linear}#world.smooth .player,#world.smooth .unit{transition:transform .5s linear}
#blips{filter:drop-shadow(0 1px 1.5px #000d)}#blips.off,#units.off,#pins.off{display:none}
.blip{position:absolute;left:0;top:0;width:var(--s,20px);height:var(--s,20px);margin:calc(var(--s,20px)/-2) 0 0 calc(var(--s,20px)/-2);background:var(--c,#fff);-webkit-mask:var(--m) center/contain no-repeat;mask:var(--m) center/contain no-repeat;z-index:5;cursor:default}.blip img{display:block;width:100%;height:100%;mix-blend-mode:multiply}.blip.hide{display:none}
.player{position:absolute;left:0;top:0;z-index:9;width:34px;height:34px;margin:-17px 0 0 -17px;pointer-events:none;filter:drop-shadow(0 4px 9px #000)}.player:before{content:"";position:absolute;left:9px;top:9px;width:16px;height:16px;border:3px solid #fff;border-radius:50%;background:var(--acc)}.player:after{content:"";position:absolute;left:14px;top:-3px;border-left:3px solid transparent;border-right:3px solid transparent;border-bottom:13px solid #fff;transform-origin:3px 20px}
.wp{position:absolute;left:0;top:0;z-index:8;pointer-events:none}.wp i{position:absolute;left:-13px;top:-26px;width:26px;height:26px;border-radius:50% 50% 50% 0;transform:rotate(-45deg);background:#c96bff;box-shadow:inset 0 0 0 2px #fff,0 3px 6px #000a}.wp i:after{content:"";position:absolute;left:9px;top:9px;width:8px;height:8px;border-radius:50%;background:#fff}.wp b{position:absolute;left:16px;top:-26px;padding:2px 7px;border-radius:5px;background:#2a1240e8;border:1px solid #c96bff88;font-size:12px;font-weight:800;white-space:nowrap;font-variant-numeric:tabular-nums}.wp b:empty{display:none}
.unit{position:absolute;left:0;top:0;z-index:7}.unit i{position:absolute;left:-11px;top:-11px;width:22px;height:22px;background:var(--c,#5db6e5);-webkit-mask:var(--m) center/contain no-repeat;mask:var(--m) center/contain no-repeat;filter:drop-shadow(0 1px 2px #000)}.unit b{position:absolute;left:13px;top:-9px;padding:1px 6px;border-radius:4px;background:#061018d8;border-left:3px solid var(--c,#5db6e5);font-size:11px;font-weight:700;white-space:nowrap}.small .unit b,.small .mypin b{display:none}
.mypin{position:absolute;left:0;top:0;z-index:8;cursor:pointer}.mypin span{position:absolute;left:-13px;top:-30px;width:26px;height:26px;display:grid;place-items:center;border-radius:50% 50% 50% 0;transform:rotate(-45deg);background:var(--c,#ffcc4d);box-shadow:inset 0 0 0 2px #fff,0 3px 6px #000a}.mypin span em{transform:rotate(45deg);font-style:normal;font-size:13px;color:#061018;font-weight:900}.mypin b{position:absolute;left:14px;top:-30px;padding:2px 7px;border-radius:4px;background:#061018e0;font-size:12px;font-weight:700;white-space:nowrap;border-left:3px solid var(--c,#ffcc4d)}
/* location card */
.loc{position:absolute;z-index:10;left:18px;top:18px;max-width:min(460px,calc(100% - 110px));padding:12px 16px 12px 14px;display:flex;gap:12px;align-items:flex-start}
.loc[hidden]{display:none}.dot{flex:none;width:10px;height:10px;margin-top:9px;border-radius:50%;background:#ffcc70;box-shadow:0 0 0 3px #ffcc7033}.dot.on{background:#72f0c0;box-shadow:0 0 0 3px #72f0c033}
.loc .main{font-size:19px;font-weight:800;line-height:1.3;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.postal{display:inline-block;margin-right:8px;padding:0 7px;border-radius:5px;background:var(--acc);color:#061018;font-size:14px;font-weight:900;vertical-align:2px}
.loc .sub{margin-top:3px;color:var(--mute);font-size:13px;display:flex;flex-wrap:wrap;gap:4px 14px}.loc .sub b{color:var(--ink);font-weight:700;font-variant-numeric:tabular-nums}.loc .sub .wpd b{color:#e2b6ff}
/* toolbar */
.tools{position:absolute;z-index:10;right:18px;top:18px;display:flex;flex-direction:column;gap:8px}.tools .grp{display:flex;flex-direction:column;overflow:hidden;padding:0}
.tools button{position:relative;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:3px;width:58px;height:52px;border:0;border-bottom:1px solid #ffffff10;background:transparent;color:#cfe9f2;cursor:pointer}.tools button:last-child{border-bottom:0}.tools button[hidden]{display:none}
.tools svg{width:20px;height:20px;fill:none;stroke:currentColor;stroke-width:2;stroke-linecap:round;stroke-linejoin:round}.tools button span{font-size:10.5px;font-weight:700;color:#9fb8c3}
.tools button:hover{background:#ffffff12;color:#fff}.tools button:hover span{color:#fff}.tools button.active{background:var(--acc);color:#061018}.tools button.active span{color:#061018}
.badge{position:absolute;right:7px;top:5px;min-width:17px;height:17px;padding:0 5px;border-radius:9px;background:var(--red);color:#fff;font-size:10.5px;font-style:normal;line-height:17px;font-weight:800}
/* side panels */
.side{position:absolute;z-index:12;right:90px;top:18px;width:min(360px,calc(100% - 110px));max-height:calc(100% - 36px);overflow:auto;padding:16px 18px;display:none}.side.open{display:block}
.side h3{margin:2px 0 10px;font-size:12px;letter-spacing:.12em;color:var(--acc);display:flex;align-items:center;justify-content:space-between}.side h3+.muted{margin-top:-4px}.side section+section{margin-top:16px;padding-top:14px;border-top:1px solid #ffffff12}
.seg{display:grid;grid-template-columns:repeat(auto-fit,minmax(70px,1fr));gap:6px}.seg button{padding:9px 6px;border:1px solid #ffffff1c;border-radius:7px;background:#ffffff08;cursor:pointer;font-size:13px}.seg button:hover{background:#ffffff14}.seg button.active{background:var(--acc);border-color:var(--acc);color:#061018;font-weight:800}.seg button[hidden]{display:none}
.row{display:flex;align-items:center;gap:10px;padding:8px 4px;border-bottom:1px solid #ffffff0b;font-size:13.5px}.row:hover{background:#ffffff07}label.row{cursor:pointer}.row .grow{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.muted{color:#7f98a3;font-size:12px}
input[type=checkbox]{accent-color:var(--acc);width:17px;height:17px;flex:none}
.ico{width:20px;height:20px;background:var(--c,#dff7ff);-webkit-mask:var(--m) center/contain no-repeat;mask:var(--m) center/contain no-repeat;flex:none}
.search{width:100%;margin:2px 0 8px;padding:8px 10px;border:1px solid #ffffff22;border-radius:7px;background:#06101a;outline:none}.search:focus{border-color:var(--acc)}
.btns{display:flex;gap:6px;margin-bottom:6px}.btns button,.row button{padding:5px 10px;border:1px solid #ffffff22;border-radius:6px;background:#ffffff0a;cursor:pointer;font-size:12px}.btns button:hover,.row button:hover{background:var(--acc);color:#061018}
.foot{margin-top:14px;color:#6f8893;font-size:11px;line-height:1.55}
/* dispatch */
.alerts{position:absolute;z-index:10;right:18px;bottom:18px;width:min(340px,calc(100% - 36px));max-height:46%;display:none;flex-direction:column;background:#1a0710ec;border:1px solid #ff547044;border-left:3px solid var(--red)}.alerts.open{display:flex}.alerts.open.under{display:none}
.alerts .ah{display:flex;align-items:center;gap:8px;padding:10px 10px 8px 14px;color:#ff9aac;font-size:11px;font-weight:900;letter-spacing:.14em}.alerts .ah .n{padding:0 7px;border-radius:9px;background:var(--red);color:#fff;letter-spacing:0}.alerts .ah button{margin-left:auto;display:grid;place-items:center;width:26px;height:26px;border:0;border-radius:6px;background:transparent;color:#e7c3cb;cursor:pointer}.alerts .ah button:hover{background:#ffffff14}.alerts .ah svg{width:16px;height:16px;fill:none;stroke:currentColor;stroke-width:2.2;stroke-linecap:round}
#alertRows{overflow:auto;padding:0 8px 8px}.alerts .a{padding:8px 6px;border-radius:6px;cursor:pointer}.alerts .a+.a{border-top:1px solid #ffffff0e}.alerts .a:hover{background:#ffffff0c}.code{display:inline-block;margin-right:6px;padding:0 6px;border-radius:3px;background:var(--red);color:#fff;font-size:11px;font-weight:900}.alerts .t{font-size:13.5px;font-weight:700}.alerts .s{color:#caa6b0;font-size:11.5px;margin-top:2px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.alert-tip{position:fixed;z-index:13;width:300px;padding:12px 14px;background:#12050bf4;border:1px solid #ff547055;border-left:3px solid var(--red);border-radius:10px;box-shadow:0 14px 40px #000a;pointer-events:none;font-size:13px;line-height:1.5}.alert-tip .h{display:flex;align-items:center;gap:8px;font-size:15px;font-weight:800;margin-bottom:6px}.alert-tip .body{color:#f3dde2;white-space:pre-wrap;word-break:break-word;margin-bottom:8px}.alert-tip .meta{display:grid;grid-template-columns:auto 1fr;gap:2px 10px;color:#c9aab2;font-size:12px}.alert-tip .meta b{color:#8f7680;font-weight:600}.alert-tip .tiphint{margin-top:8px;color:#8f7680;font-size:11px}
/* menu, dialog, hint, toast */
.menu{position:fixed;z-index:30;min-width:220px;padding:6px;background:#071018f6;border:1px solid #ffffff22;border-radius:10px;box-shadow:0 18px 40px #000b;display:none}.menu.open{display:block}.menu .head{padding:6px 10px 8px;color:#8ca6b3;font-size:11.5px;border-bottom:1px solid #ffffff12;margin-bottom:4px;font-variant-numeric:tabular-nums}.menu button{display:block;width:100%;padding:9px 10px;border:0;border-radius:6px;background:transparent;text-align:left;cursor:pointer}.menu button:hover{background:var(--acc);color:#061018}.menu button[hidden]{display:none}
.dialog{position:fixed;z-index:40;inset:0;display:none;place-items:center;background:#02070b99}.dialog.open{display:grid}.dialog form{width:min(360px,calc(100% - 40px));padding:20px;background:#0b1922;border:1px solid #ffffff1a;border-top:3px solid #ffcc4d;border-radius:10px;box-shadow:0 20px 60px #000}.dialog h3{margin:0 0 12px;font-size:15px}.dialog input[type=text]{width:100%;padding:9px 10px;border:1px solid #ffffff22;border-radius:7px;background:#06101a;outline:none}.dialog input[type=text]:focus{border-color:var(--acc)}.choices{display:flex;flex-wrap:wrap;gap:6px;margin:12px 0}.choices button{width:34px;height:34px;border:2px solid transparent;border-radius:7px;background:#ffffff0c;cursor:pointer;font-size:16px}.choices button.on{border-color:#fff}.dialog .actions{display:flex;justify-content:flex-end;gap:8px;margin-top:6px}.dialog .actions button{padding:8px 14px;border:0;border-radius:7px;cursor:pointer}.dialog .actions .ok{background:var(--acc);color:#061018;font-weight:800}.dialog .actions .cancel{background:#ffffff14}
.hint{position:absolute;z-index:11;left:50%;top:18px;transform:translateX(-50%);max-width:calc(100% - 200px);display:flex;align-items:center;gap:10px;padding:9px 10px 9px 16px;font-size:13px}.hint[hidden]{display:none}.hint button{white-space:nowrap;border:0;border-radius:6px;padding:5px 10px;background:#ffffff14;cursor:pointer;font-size:12px}.hint button:hover{background:var(--acc);color:#061018}
.toast{position:fixed;z-index:50;left:50%;top:72px;transform:translateX(-50%);padding:10px 16px;background:#071018f4;border:1px solid #0dd4fc66;border-radius:10px;font-size:13px;box-shadow:0 10px 30px #000a;opacity:0;transition:opacity .2s;pointer-events:none}.toast.show{opacity:1}
.disabled{position:absolute;z-index:20;inset:0;display:grid;place-items:center;background:#061018dd}.disabled[hidden]{display:none}.disabled div{max-width:540px;padding:28px;text-align:center;background:#0b1922;border:1px solid #ffffff16;border-top:3px solid var(--acc);border-radius:12px;box-shadow:0 20px 60px #000}.disabled h2{margin:0 0 10px;font-size:20px}.disabled p{margin:0;color:#94aab5;line-height:1.7}
@media(max-width:650px){.hint{left:10px;right:80px;max-width:none;transform:none}.loc{left:10px;top:10px;max-width:calc(100% - 90px)}.loc .main{font-size:16px}.tools{right:10px;top:10px}.tools button{width:50px;height:46px}.side{right:70px;top:10px;width:calc(100% - 80px)}.alerts{right:10px;bottom:10px;width:calc(100% - 20px)}}
</style>
</head>
<body>
<main id="map"><div id="world"><div id="tiles"></div><div id="blips"></div><div id="units"></div><div id="pins"></div><div id="waypoint" class="wp" hidden><i></i><b></b></div><div id="player" class="player" hidden></div></div></main>
<section id="locCard" class="card loc" hidden><span id="conn" class="dot" title="Bridge へ接続中"></span><div><div id="location" class="main">FiveM を待っています</div><div id="sub" class="sub"><span id="zone">MioCity Media Link から現在地を受信します</span></div></div></section>
<nav class="tools">
<div class="grp card"><button id="follow" class="active" title="自分の位置に戻る"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="3"/><circle cx="12" cy="12" r="8"/><path d="M12 1v3M12 20v3M1 12h3M20 12h3"/></svg><span>追従</span></button><button id="zoomIn" title="拡大（ホイール・ダブルクリックでも）"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14M5 12h14"/></svg><span>拡大</span></button><button id="zoomOut" title="縮小"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12h14"/></svg><span>縮小</span></button></div>
<div class="grp card"><button id="view" title="地図の種類・表示するもの"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3 2 8l10 5 10-5z"/><path d="m2 13 10 5 10-5"/></svg><span>表示</span></button><button id="pinList" title="マイピン"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 21s-7-6.2-7-11.5A7 7 0 0 1 19 9.5C19 14.8 12 21 12 21z"/><circle cx="12" cy="9.5" r="2.5"/></svg><span>ピン</span></button><button id="alertToggle" title="通報の一覧" hidden><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 16V11a6 6 0 0 1 12 0v5l2 2H4z"/><path d="M10 21h4"/></svg><span>通報</span><em id="alertBadge" class="badge" hidden></em></button></div>
<div class="grp card"><button id="fullscreen" title="全画面"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/></svg><span>全画面</span></button></div>
</nav>
<aside id="viewPanel" class="side card">
<section><h3>地図の種類</h3><div class="seg"><button data-layer="render">写真</button><button data-layer="game">ゲーム</button><button data-layer="print">白地図</button><button data-layer="postal" hidden>番地</button></div></section>
<section><h3>地図に出すもの</h3><div id="layerToggles"></div></section>
<section><h3>アイコンの種類</h3><div class="muted" style="margin-bottom:8px">チェックした種類だけ地図に出ます</div><input id="iconSearch" class="search" type="search" placeholder="種類を探す（例：病院）" autocomplete="off"><div class="btns"><button id="allOn">すべて表示</button><button id="allOff">すべて隠す</button></div><div id="spriteList"><div class="muted">アイコンを受信すると一覧が出ます</div></div></section>
<div id="privacy" class="foot"></div>
</aside>
<aside id="pinPanel" class="side card"><h3>マイピン</h3><div class="muted" style="margin-bottom:10px">地図を右クリック →「ここにピンを置く」。このPCのブラウザにだけ保存されます。</div><div id="pinRows"></div></aside>
<section id="alerts" class="alerts card"><div class="ah">DISPATCH · 通報<span id="alertCount" class="n"></span><button id="alertClose" title="閉じる（「通報」でまた開けます）"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg></button></div><div id="alertRows"></div></section><div id="alertTip" class="alert-tip" hidden></div>
<div id="hint" class="hint card" hidden><span>地図のアイコンは「表示」で種類を選ぶと出ます</span><button id="hintOpen">表示を開く</button><button id="hintClose">閉じる</button></div>
<div id="menu" class="menu"><div id="menuHead" class="head"></div><button id="mWaypoint">ここにウェイポイントを設定</button><button id="mClearWp">ウェイポイントを解除</button><button id="mPin">ここにピンを置く</button><button id="mEditPin">ピンを編集</button><button id="mDelPin">ピンを削除</button><button id="mCopy">この場所の座標をコピー</button></div>
<div id="dialog" class="dialog"><form id="pinForm"><h3 id="pinTitle">ピンを置く</h3><input id="pinLabel" type="text" maxlength="40" placeholder="メモ（例：あとで行く店）" autocomplete="off"><div id="pinIcons" class="choices"></div><div id="pinColors" class="choices"></div><div class="actions"><button type="button" class="cancel" id="pinCancel">やめる</button><button type="submit" class="ok">保存</button></div></form></div>
<div id="toast" class="toast"></div>
<div id="disabled" class="disabled" hidden><div><h2 id="disTitle"></h2><p id="disText"></p></div></div>
<script>
(()=>{"use strict";
// Tiles: the public GTA V map tiles LB Phone uses, or the game server's own "番地" map. Only tile numbers are requested;
// the coordinate JSON, blips, members and calls never leave 127.0.0.1. Blip icons come from this Bridge.
// Right click offers what the pause map offers (set / clear the waypoint; only when the game allows it) and personal
// pins, which stay in this browser (localStorage).
const TILE_BASE="https://assets.loaf-scripts.com/map-tiles/gtav/main/";
const LAYERS=["render","game","print","postal"];
const COLORS={0:"#fefefe",1:"#e03232",2:"#71cb71",3:"#5db6e5",4:"#fefefe",5:"#eec64e",6:"#c25050",7:"#9c6eaf",8:"#fe7ac3",9:"#f59d79",10:"#b18f83",11:"#8dcea7",12:"#70a8ae",13:"#d3d1e7",14:"#8f7e98",15:"#6ac4bf",16:"#d5c398",17:"#ea8e50",18:"#97cae9",19:"#b26287",20:"#8f8d79",21:"#a6755e",22:"#afa8a8",23:"#e78d9a",24:"#bbd65b",25:"#0c7b56",26:"#7ac3fe",27:"#ab3ce6",28:"#cda80c",29:"#4561ab",30:"#29a5b8",31:"#b89b7b",32:"#c8e0fe",33:"#f0f096",34:"#ed8ca1",35:"#f98a8a",36:"#fbeea5",37:"#fefefe",38:"#2c6db8",39:"#9a9a9a",40:"#4c4c4c",41:"#f29d9d",42:"#6cb7d6",43:"#afedae",44:"#ffa75f",45:"#f1f1f1",46:"#ecf029",47:"#ff9a18",48:"#f644a5",49:"#e03a3a",50:"#8a6de3",51:"#ff8b5c",52:"#416c41",53:"#b3ddf3",54:"#3a6479",55:"#a0a0a0",56:"#847232",57:"#65b9e7",58:"#4b4175",59:"#e13b3b",60:"#f0cb58",61:"#cd3f98",62:"#cfcfcf",63:"#276a9f",64:"#d87b1b",65:"#8e8393",66:"#f0cb57",67:"#65b9e7",68:"#65b9e7",69:"#79cd79",70:"#efca57",71:"#efca57",72:"#3d3d3d",73:"#efca57",74:"#65b9e7",75:"#e03232",76:"#782323",77:"#65b9e7",78:"#3a6479",79:"#e03232",80:"#65b9e7",81:"#f2a40c",82:"#a4ccaa",83:"#a854f2",84:"#65b9e7",85:"#3d3d3d"};
// Japanese names for common icons (others show the FiveM reference name)
const JP={level:"汎用マーカー",safehouse:"家・拠点",police_station:"警察署",police_station_blue:"警察署",hospital:"病院",barber:"床屋",car_mod_shop:"改造ショップ",bennys:"ベニーズ",clothes_store:"服屋",tattoo:"タトゥー",gun_shop:"銃砲店",shootingrange_gunshop:"射撃場",bar:"バー",car_wash:"洗車",garage:"ガレージ",garage_for_sale:"ガレージ",jerry_can:"ガソリンスタンド",crim_holdups:"ショップ",property:"物件",property_for_sale:"物件（販売中）",warehouse:"倉庫",dock:"ボート乗り場",taxi:"タクシー",garbage:"ゴミ収集",tow:"レッカー",tow_truck:"レッカー",weapon_health:"回復",hunting:"狩猟",cinema:"映画館",music_venue:"ライブ会場",gang_vehicle:"車両",gang_vehicle_bikers:"バイク",helicopter:"ヘリ",player_plane:"飛行機",player_boat:"船",friend:"フレンド",poi:"スポット",cop_car:"警察車両",camera:"カメラ",airport:"空港",business:"ビジネス",bank:"銀行",dollar_sign:"お金",store:"ショップ",flight_school:"飛行学校",strip_club:"ストリップクラブ",darts:"ダーツ",golf:"ゴルフ",tennis:"テニス",bowling:"ボウリング",gym:"ジム",restaurant:"レストラン",burger_shot:"バーガーショット",cluckin_bell:"クラッキンベル",laptop:"ノートPC",package:"荷物",repair:"修理",pickup_repair:"修理"};
const KIND_SPRITE=[1,225,226,64,423,427];
const PIN_ICONS=["★","⌂","$","✚","⚑","●","♥","!"];
const PIN_COLORS=["#ffcc4d","#0dd4fc","#72f0c0","#ff5470","#c96bff","#ffffff"];
const MIN_ZOOM=2,MAX_ZOOM=7;
const $=id=>document.getElementById(id);
const map=$("map"),world=$("world"),tiles=$("tiles"),blipLayer=$("blips"),unitLayer=$("units"),pinLayer=$("pins"),player=$("player"),waypoint=$("waypoint"),disabled=$("disabled"),menu=$("menu");
let state={active:false,x:450,y:1650,z:0,heading:0,speedKmh:0,street:"",crossing:"",zone:"",inVehicle:false,hasWaypoint:false,waypointX:0,waypointY:0,postal:""};
let cfg={waypoint:false,postal:"",postalMax:6,services:false};
const store=(k,v)=>{try{localStorage.setItem(k,v)}catch{}},read=k=>{try{return localStorage.getItem(k)}catch{return null}};
const readJson=(k,d)=>{try{const v=JSON.parse(read(k)||"null");return v==null?d:v}catch{return d}};
let layer=LAYERS.includes(read("mio-map-layer"))?read("mio-map-layer"):"render";
// loc = the street / speed card (off by default: the waypoint pin carries its distance, the overlay the connection)
const show=Object.assign({blips:true,units:true,alerts:true,pins:true,loc:false},readJson("mio-map-show",{}));
if(read("mio-map-blips")==="0")show.blips=false;
// icon types are hidden until ticked in 「表示」 (the player picks what to see)
const shownSprites=new Set((readJson("mio-map-shown",[])||[]).filter(n=>Number.isInteger(n)));
let pins=(readJson("mio-map-pins",[])||[]).filter(p=>p&&Number.isFinite(p.x)&&Number.isFinite(p.y)).slice(0,200);
// starts at street level; the last zoom is remembered
let zoom=Math.max(MIN_ZOOM,Math.min(MAX_ZOOM,Number(read("mio-map-zoom"))||5));
let follow=true,center={x:450,y:1650},drag=null,retry=null,mapEnabled=false,gameConnected=false,frame=0,blips=[],units=[],alerts=[],blipsDirty=true,placedZoom=-1,socket=null,names={},menuAt=null,menuPin=null,editing=null,toastTimer=0;
const tileCache=new Map(),blipEls=[],unitEls=[];
const U=16384/(9000*128),D=24576/(-13500*128);
const num=v=>Number.isFinite(Number(v))?Number(v):0;
const esc=s=>String(s??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
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
  if(zoomChanged){clearTiles();placedZoom=zoom;blipsDirty=true}
  renderTiles(c);
  const p=px(num(state.x),num(state.y));
  player.hidden=!state.active;
  player.style.transform=`translate(${p.x}px,${p.y}px) rotate(${-num(state.heading)}deg)`;
  if(state.hasWaypoint){const w=px(num(state.waypointX),num(state.waypointY));waypoint.hidden=false;waypoint.style.transform=`translate(${w.x}px,${w.y}px)`;
    waypoint.lastChild.textContent=state.active?dist(distanceTo(num(state.waypointX),num(state.waypointY))):""}else waypoint.hidden=true;
  if(blipsDirty){blipsDirty=false;renderBlips();renderUnits();renderPins()}
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
function iconEl(el,sprite){if(el.__sprite===sprite)return;el.__sprite=sprite;const url=`/blip/${sprite}.png`;el.style.setProperty("--m",`url(${url})`);el.title=spriteName(sprite);if(el.firstChild){el.firstChild.src=url;el.firstChild.onerror=()=>{el.style.setProperty("--m","none");el.firstChild.style.display="none";el.style.borderRadius="50%";el.style.setProperty("--s","10px")}}}
// blip elements are reused by index; only changed properties are written
function renderBlips(){
  blipLayer.classList.toggle("off",!show.blips);
  if(!show.blips)return;
  const size=zoom>=5?22:zoom>=4?19:16;
  for(let i=0;i<blips.length;i++){
    const b=blips[i];let el=blipEls[i];
    if(!el){el=document.createElement("i");el.className="blip";el.appendChild(new Image());el.firstChild.alt="";el.firstChild.decoding="async";blipEls[i]=el;blipLayer.appendChild(el)}
    const sprite=num(b[0])|0,color=COLORS[num(b[1])|0]||"#ffffff",p=px(num(b[2]),num(b[3])),rot=num(b[4]);
    iconEl(el,sprite);
    el.classList.toggle("hide",!shownSprites.has(sprite));
    if(el.__color!==color){el.__color=color;el.style.setProperty("--c",color)}
    el.style.setProperty("--s",size+"px");
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
    el.style.transform=`translate(${p.x}px,${p.y}px)`;
    icon.style.transform=kind===0?"":`rotate(${-num(u[2])}deg)`;
  }
  while(unitEls.length>units.length)unitEls.pop().remove();
}
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
}
function savePins(){store("mio-map-pins",JSON.stringify(pins))}
function renderPins(){
  pinLayer.classList.toggle("off",!show.pins);
  pinLayer.textContent="";
  for(const pin of pins){const p=px(pin.x,pin.y),el=document.createElement("div");el.className="mypin";el.dataset.id=pin.id;el.style.setProperty("--c",pin.color||"#ffcc4d");el.innerHTML="<span><em></em></span>"+(pin.label?"<b></b>":"");el.querySelector("em").textContent=pin.icon||"★";if(pin.label)el.querySelector("b").textContent=pin.label;el.title=pin.label||"マイピン";el.style.transform=`translate(${p.x}px,${p.y}px)`;pinLayer.appendChild(el)}
  renderPinList();
}
function renderPinList(){
  const rows=$("pinRows");
  if(!pins.length){rows.innerHTML='<div class="muted">まだピンはありません</div>';return}
  rows.innerHTML=pins.map((p,i)=>`<div class="row" data-i="${i}"><span style="color:${esc(p.color)}">${esc(p.icon||"★")}</span><span class="grow">${esc(p.label||"（メモなし）")}</span><span class="muted">${state.active?dist(distanceTo(p.x,p.y)):""}</span>${cfg.waypoint?'<button data-act="wp" title="ウェイポイントにする">WP</button>':""}<button data-act="del" title="削除">✕</button></div>`).join("");
}
// ---------------------------------------------------------------- 表示 panel
function renderFilter(){
  const counts=new Map();for(const b of blips){const s=num(b[0])|0;counts.set(s,(counts.get(s)||0)+1)}
  const q=$("iconSearch").value.trim().toLowerCase();
  const list=[...counts.entries()].sort((a,b)=>b[1]-a[1]||a[0]-b[0]).filter(([s])=>!q||spriteName(s).toLowerCase().includes(q));
  $("spriteList").innerHTML=list.length?list.map(([s,n])=>`<label class="row"><input type="checkbox" data-sprite="${s}" ${shownSprites.has(s)?"checked":""}><span class="ico" style="--m:url(/blip/${s}.png)"></span><span class="grow">${esc(spriteName(s))}</span><span class="muted">${n}</span></label>`).join(""):`<div class="muted">${counts.size?"見つかりません":"アイコンを受信すると一覧が出ます"}</div>`;
  const toggles=[["blips","マップのアイコン"],["pins","マイピン"],["loc","現在地（通り名・速度）"]].concat(cfg.services?[["units","公務員の位置"],["alerts","通報の一覧"]]:[]);
  $("layerToggles").innerHTML=toggles.map(([k,l])=>`<label class="row"><input type="checkbox" data-show="${k}" ${show[k]?"checked":""}><span class="grow">${l}</span></label>`).join("");
}
// "icons are picked in 表示": shown once while nothing is ticked, until closed
function updateHint(){$("hint").hidden=!(blips.length&&show.blips&&shownSprites.size===0&&read("mio-map-hint")!=="1"&&!$("viewPanel").classList.contains("open"))}
function updateInfo(){
  const street=[state.street,state.crossing].filter(Boolean).join(" × ");
  $("location").innerHTML=(state.postal?`<span class="postal">${esc(state.postal)}</span>`:"")+esc(street||(state.active?"現在地を取得中":"FiveM を待っています"));
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
  $("privacy").textContent=layer==="postal"?"位置などのデータはこの PC の中（127.0.0.1）だけで中継します。「番地」の地図はゲームサーバーから読み込みます（サーバーには IP と表示中のタイル番号が伝わります）。":"位置などのデータはこの PC の中（127.0.0.1）だけで中継します。背景の地図は LB Phone と同じ外部の配信元から読み込みます（配信元には IP と表示中のタイル番号が伝わります）。";
  clearTiles();blipsDirty=true;renderFilter();renderAlerts();render(false);
}
function status(){disabled.hidden=gameConnected&&mapEnabled;
  if(!gameConnected){$("disTitle").textContent="FiveM を待っています";$("disText").innerHTML="Media Link を起動したまま FiveM で MioCity に接続すると、自動で地図が出ます。"}
  else{$("disTitle").textContent="セカンドモニターマップは OFF です";$("disText").innerHTML="ゲーム内の UI 設定で「外部マップを有効にする」を ON にしてください。<br>ZSX UI：UI メニュー → Misc → MioCity Media Link<br>mio_ui：UI 設定 → Media Link"}
  const on=gameConnected&&mapEnabled,c=$("conn");c.classList.toggle("on",on);c.title=!gameConnected?"FiveM 未接続（自動で待機中）":mapEnabled?"FiveM 接続中":"マップ OFF";
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
function setWaypoint(x,y){if(!cfg.waypoint)return;if(send("map.waypoint",{x:Math.round(x*10)/10,y:Math.round(y*10)/10}))toast("ゲームのウェイポイントを設定しました");else toast("FiveM に接続されていません")}
function clearWaypoint(){if(cfg.waypoint&&send("map.clearWaypoint",{}))toast("ウェイポイントを解除しました")}
// ---------------------------------------------------------------- right-click menu
function closeMenu(){menu.classList.remove("open");menuAt=null;menuPin=null}
function openMenu(e,pin,at,head){
  menuAt=at||(pin?{x:pin.x,y:pin.y}:screenToGame(e.clientX,e.clientY));menuPin=pin||null;
  $("menuHead").textContent=head||(pin?(pin.label||"マイピン"):(state.active?`ここまで ${dist(distanceTo(menuAt.x,menuAt.y))}`:`${menuAt.x.toFixed(1)}, ${menuAt.y.toFixed(1)}`));
  $("mWaypoint").hidden=!cfg.waypoint||!gameConnected;$("mClearWp").hidden=!cfg.waypoint||!state.hasWaypoint||!gameConnected;
  $("mPin").hidden=!!pin;$("mEditPin").hidden=!pin;$("mDelPin").hidden=!pin;
  menu.classList.add("open");const r=menu.getBoundingClientRect();
  menu.style.left=Math.min(e.clientX,innerWidth-r.width-8)+"px";menu.style.top=Math.min(e.clientY,innerHeight-r.height-8)+"px";
}
map.addEventListener("contextmenu",e=>{e.preventDefault();if(!gameConnected||!mapEnabled)return;const el=e.target.closest(".mypin");openMenu(e,el?pins.find(p=>p.id===el.dataset.id):null)});
$("mWaypoint").onclick=()=>{if(menuAt)setWaypoint(menuAt.x,menuAt.y);closeMenu()};
$("mClearWp").onclick=()=>{clearWaypoint();closeMenu()};
$("mPin").onclick=()=>{const at=menuAt;closeMenu();if(at)openPinDialog(null,at)};
$("mEditPin").onclick=()=>{const p=menuPin;closeMenu();if(p)openPinDialog(p)};
$("mDelPin").onclick=()=>{const p=menuPin;closeMenu();if(p){pins=pins.filter(x=>x!==p);savePins();renderPins();toast("ピンを削除しました")}};
$("mCopy").onclick=()=>{const at=menuAt;closeMenu();if(at)navigator.clipboard?.writeText(`${at.x.toFixed(2)}, ${at.y.toFixed(2)}`).then(()=>toast("座標をコピーしました")).catch(()=>{})};
addEventListener("pointerdown",e=>{if(!menu.contains(e.target))closeMenu()},true);
addEventListener("keydown",e=>{if(e.key==="Escape"){closeMenu();closeDialog();closePanels()}});
// ---------------------------------------------------------------- personal pins
let pinIcon="★",pinColor=PIN_COLORS[0];
function choice(box,list,cur,set,isColor){box.innerHTML="";for(const v of list){const b=document.createElement("button");b.type="button";if(isColor)b.style.background=v;else b.textContent=v;b.classList.toggle("on",v===cur);b.onclick=()=>{set(v);box.querySelectorAll("button").forEach(x=>x.classList.toggle("on",x===b))};box.appendChild(b)}}
function openPinDialog(pin,at){
  if(!pin&&pins.length>=200){toast("ピンは200個までです");return}
  editing=pin?{pin}:{at};$("pinTitle").textContent=pin?"ピンを編集":"ここにピンを置く";$("pinLabel").value=pin?pin.label||"":"";
  pinIcon=pin?pin.icon||"★":"★";pinColor=pin?pin.color||PIN_COLORS[0]:PIN_COLORS[0];
  choice($("pinIcons"),PIN_ICONS,pinIcon,v=>pinIcon=v,false);choice($("pinColors"),PIN_COLORS,pinColor,v=>pinColor=v,true);
  $("dialog").classList.add("open");setTimeout(()=>$("pinLabel").focus(),30);
}
function closeDialog(){$("dialog").classList.remove("open");editing=null}
$("pinCancel").onclick=closeDialog;
$("pinForm").onsubmit=e=>{e.preventDefault();if(!editing)return;const label=$("pinLabel").value.trim().slice(0,40);
  if(editing.pin)Object.assign(editing.pin,{label,icon:pinIcon,color:pinColor});
  else pins.push({id:Date.now().toString(36)+Math.random().toString(36).slice(2,6),x:Math.round(editing.at.x*10)/10,y:Math.round(editing.at.y*10)/10,label,icon:pinIcon,color:pinColor});
  savePins();closeDialog();renderPins();render(false);toast("ピンを保存しました")};
function goTo(x,y){follow=false;center={x,y};setZoom(Math.max(zoom,5));render(false)}
$("pinRows").addEventListener("click",e=>{const row=e.target.closest(".row");if(!row)return;const p=pins[num(row.dataset.i)];if(!p)return;const act=e.target.dataset.act;
  if(act==="del"){pins=pins.filter(x=>x!==p);savePins();renderPins();return}
  if(act==="wp"){setWaypoint(p.x,p.y);return}
  goTo(p.x,p.y)});
// ---------------------------------------------------------------- panels
function closePanels(){$("alerts").classList.remove("under");document.querySelectorAll(".side").forEach(s=>s.classList.remove("open"));document.querySelectorAll("#view,#pinList").forEach(b=>b.classList.remove("active"));updateHint()}
function togglePanel(id,btn){const el=$(id),open=!el.classList.contains("open");closePanels();if(open){el.classList.add("open");btn.classList.add("active");$("alerts").classList.add("under");$("alertTip").hidden=true}if(id==="viewPanel"&&open){renderFilter();$("hint").hidden=true}if(id==="pinPanel"&&open)renderPinList()}
$("view").onclick=()=>togglePanel("viewPanel",$("view"));
$("pinList").onclick=()=>togglePanel("pinPanel",$("pinList"));
$("alertToggle").onclick=()=>{closePanels();const box=$("alerts");box.__closed=box.classList.contains("open");renderAlerts()};
$("alertClose").onclick=()=>{$("alerts").__closed=true;renderAlerts()};
$("hintOpen").onclick=()=>{if(!$("viewPanel").classList.contains("open"))togglePanel("viewPanel",$("view"))};
$("hintClose").onclick=()=>{store("mio-map-hint","1");updateHint()};
$("iconSearch").addEventListener("input",renderFilter);
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
$("viewPanel").addEventListener("change",e=>{const t=e.target;
  if(t.dataset.sprite){const s=num(t.dataset.sprite)|0;if(t.checked)shownSprites.add(s);else shownSprites.delete(s);store("mio-map-shown",JSON.stringify([...shownSprites]))}
  if(t.dataset.show){show[t.dataset.show]=t.checked;store("mio-map-show",JSON.stringify(show));renderAlerts();$("locCard").hidden=!show.loc}
  blipsDirty=true;render(false)});
$("allOn").onclick=()=>{for(const b of blips)shownSprites.add(num(b[0])|0);store("mio-map-shown",JSON.stringify([...shownSprites]));renderFilter();blipsDirty=true;render(false)};
$("allOff").onclick=()=>{shownSprites.clear();store("mio-map-shown","[]");renderFilter();blipsDirty=true;render(false)};
// ---------------------------------------------------------------- dragging / zoom
function setZoom(z){z=Math.max(MIN_ZOOM,Math.min(MAX_ZOOM,z));if(z===zoom)return false;zoom=z;store("mio-map-zoom",String(z));return true}
// zoom keeping the map point under (cx, cy) where it is; while following, around the player
function zoomAt(z,cx,cy){
  if(follow||cx==null){if(setZoom(z))render(false);return}
  const g=screenToGame(cx,cy);if(!setZoom(z))return;
  const p=px(g.x,g.y);center=toGame(p.x-(cx-innerWidth/2),p.y-(cy-innerHeight/2));render(false)}
function endDrag(){drag=null;map.classList.remove("dragging")}
map.addEventListener("pointerdown",e=>{if(e.button!==0||e.target.closest(".mypin"))return;drag={x:e.clientX,y:e.clientY,center:{...center},moved:false};world.classList.remove("smooth");map.classList.add("dragging");map.setPointerCapture(e.pointerId)});
map.addEventListener("pointermove",e=>{if(!drag)return;if(!drag.moved&&Math.hypot(e.clientX-drag.x,e.clientY-drag.y)<4)return;drag.moved=true;follow=false;const c=px(drag.center.x,drag.center.y);center=toGame(c.x-(e.clientX-drag.x),c.y-(e.clientY-drag.y));render(false)});
map.addEventListener("pointerup",endDrag);map.addEventListener("pointercancel",endDrag);
map.addEventListener("dblclick",e=>{if(e.target.closest(".mypin"))return;follow=false;zoomAt(zoom+1,e.clientX,e.clientY)});
pinLayer.addEventListener("click",e=>{const el=e.target.closest(".mypin");if(!el)return;const p=pins.find(x=>x.id===el.dataset.id);if(p){follow=false;center={x:p.x,y:p.y};render(false)}});
let wheelAt=0;
map.addEventListener("wheel",e=>{e.preventDefault();const now=performance.now();if(now-wheelAt<120)return;wheelAt=now;zoomAt(zoom+(e.deltaY<0?1:-1),e.clientX,e.clientY)},{passive:false});
$("follow").onclick=()=>{follow=true;render(false)};$("zoomIn").onclick=()=>zoomAt(zoom+1);$("zoomOut").onclick=()=>zoomAt(zoom-1);
$("fullscreen").onclick=()=>document.fullscreenElement?document.exitFullscreen():document.documentElement.requestFullscreen().catch(()=>{});
document.querySelectorAll("[data-layer]").forEach(b=>{b.onclick=()=>{layer=b.dataset.layer;store("mio-map-layer",layer);applyConfig()}});
fetch("/blip/names.json").then(r=>r.ok?r.json():{}).then(n=>{names=n||{};for(const el of blipEls){const s=el.__sprite;el.__sprite=null;iconEl(el,s)}if($("viewPanel").classList.contains("open"))renderFilter()}).catch(()=>{});
setInterval(()=>{if(alerts.length)renderAlerts();if($("pinPanel").classList.contains("open"))renderPinList()},30000);
addEventListener("resize",()=>render(false));$("locCard").hidden=!show.loc;applyConfig();status();connect();
})();
</script>
</body></html>
""";
}
