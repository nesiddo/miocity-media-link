# MioCity Media Link

FiveM サーバー **MioCity** 専用の Windows 補助アプリです。Windows で再生中のメディア（曲名・アーティスト・ジャケット・再生位置）と、任意で音声の波形を、同じ PC 上のゲーム内 UI（ZSX_UIV2 / mio_ui）に表示します。セカンドモニター用のマップ（現在地・地図アイコン・自分用の書き込み）も表示できます。

Created by nesiddo

## できること

- 再生中メディアの表示と、再生／一時停止・前へ・次へ（Windows のメディア操作 SMTC を使用）
- 音声ビジュアライザー（既定 OFF）
- セカンドモニター／別ウィンドウ用のマップ（現在地・向き・ウェイポイントと距離。既定 OFF）
  - ホイールはカーソルの位置を中心に拡大、ダブルクリックでも拡大。拡大率は次回も同じ
  - 右クリックでウェイポイントの設定・解除（ゲーム内のマップと同じ操作だけ。サーバーが許可している場合）
  - 左のパレットで地図に書き込める: ピン（絵柄18種）・テキスト・範囲（四角／自由な形）・線（矢印にもできる）。選んで編集・移動・削除、元に戻す／やり直し、キー操作（? で一覧）。マップを開いているブラウザにだけ保存
  - プリセット: 書き込みと表示する地図アイコンの種類を用途ごとにまとめて切り替え（キーボードの 1〜9 でも）。文字にして人に渡したり、座標の一覧から作ったりできる
  - アイコンの大きさを 60〜200% で変更
  - 「表示」で地図の種類と、地図に出すアイコンの種類を選ぶ（初期状態はすべて非表示。検索あり、アイコンに合わせると名前）
  - 番地入りの地図（サーバーが配信している場合）
  - 勤務中の公務員向けに、同じ職業のメンバーと通報の一覧（カーソルを合わせると詳細。サーバー側の対応が必要）
- YouTube ライブのコメントを、ゲーム内のコメント欄（mio_ui の配信チャット）に表示（ゲーム内の設定で配信 URL か @チャンネルを入れたときだけ。ログイン・API キー不要）

特定の音楽サービスへのログインや API キーは不要です。SMTC に対応したアプリ（ブラウザ、Spotify デスクトップ版など）の再生情報を表示します。

## プライバシーとセキュリティ

- 待ち受けは `127.0.0.1:18765` のみ。LAN・インターネットには公開しません（ポート開放も不要です）
- 接続を受け付けるのは FiveM のゲーム内 UI（Origin `https://cfx-nui-*`）だけ。初回に `https://cfx-nui-zsx_uiv2` へだけ 32 バイトのランダムな接続キーを自動で渡し、以後はそのキーで認証します
- 接続キーとマップ閲覧キーは各 PC の `%LOCALAPPDATA%\MioCity\LocalMediaBridge\settings.json` にだけ保存されます
- 曲・音声・位置の情報は MioCity のサーバーへ送りません
- FiveM / GTA V のプロセス、メモリ、ゲームファイル、通信には一切アクセスしません
- 波形は ON の間だけ、既定の出力デバイスの音をメモリ上で表示用の強さに変換します。音声そのものは保存・送信しません
- 外部マップからゲームへ送るのは、右クリックでのウェイポイントの設定・解除だけです（サーバーが許可したときのみ、1 秒に数回まで）
- 番地の地図は、接続中の FiveM サーバーが配信するタイルを読み込みます（サーバーから指定された http(s) のアドレスだけ）
- YouTube のコメントは、ゲーム内で配信 URL か @チャンネルを設定している間だけ `www.youtube.com` から読み込みます。読むのは公開のライブチャットだけで、アカウントやログイン情報は使いません。YouTube の Web ページと同じ仕組みを使うため、YouTube 側の変更で読み込めなくなることがあります
- 外部マップの背景は `assets.loaf-scripts.com` の地図タイルを読み込みます（ON のときだけ。配信元にはその PC の IP アドレスと表示中のタイル番号が伝わります）。ブリップのアイコンは初回だけ `docs.fivem.net` から取得して PC に保存します

## インストール（ベータ版）

[Releases](../../releases) から次のどちらかをダウンロードします。

**インストーラー（おすすめ）: `MioCity-Media-Link-<バージョン>-Setup.exe`**

1. ダウンロードした Setup.exe を実行します
   - 署名のないベータ版のため、「Windows によって PC が保護されました」と表示されます。「詳細情報」→「実行」で進めます
   - 管理者権限は不要です（このユーザーだけにインストールします。場所: `%LOCALAPPDATA%\Programs\MioCity Media Link`）
2. 「Windows の起動時に自動で起動する」は、ON にすると PC 起動時に通知領域（画面右下）で待機します
3. スタートメニューの「MioCity Media Link」から起動できます。削除は「設定 → アプリ → インストールされているアプリ」から

**zip 版: `MioCity-Media-Link-<バージョン>-win-x64.zip`**（インストールしたくない場合）

展開して `MioCityMediaLink.exe` を起動します。自動起動はアプリ画面の「Windows起動時」ボタンで切り替えられます（exe を移動したら設定し直してください）。

ダウンロードしたファイルは、Releases に書かれた SHA-256 と一致するか確認できます（PowerShell: `Get-FileHash .\ファイル名`）。

## 使い方

1. アプリを起動したまま FiveM で MioCity に接続します。接続は自動です（キーの入力は不要）
   - mio_ui：`/hudsettings` →「Media Link」
   - ZSX_UIV2：F9 → Misc →「MioCity Media Link」
2. 「プレイヤーを表示」を ON にすると、再生中の曲が HUD に出ます
3. 波形・セカンドモニター用マップも同じ画面で ON にできます

動作環境: Windows 10 2004 以降 / Windows 11（x64）。.NET のインストールは不要です（同梱）。
設定と接続キーは `%LOCALAPPDATA%\MioCity\LocalMediaBridge` に保存され、アンインストールしても残ります（再インストール時に接続し直さなくて済むように）。不要ならフォルダごと削除してください。

## ビルド

.NET 8 SDK が必要です。

```powershell
# 確認用ビルド
dotnet build .\LocalMediaBridge\LocalMediaBridge.csproj -c Release

# 配布用（.NET 同梱の単一 exe）
dotnet publish .\LocalMediaBridge\LocalMediaBridge.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

`EnableWindowsTargeting` を有効にしてあるため、Linux / macOS の .NET 8 SDK からも同じコマンドで Windows 用 exe を作れます。

### インストーラー

[NSIS](https://nsis.sourceforge.io/) 3 で作ります（Windows / Linux どちらでも可）。上の `publish` の exe を `MioCityMediaLink.exe` という名前で置いたフォルダを `SOURCE` に指定します。

```bash
cd installer
makensis -DVERSION=1.8.0 -DSOURCE=../stage MioCityMediaLink.nsi
```

### MSIX（正式版向け）

`LocalMediaBridge.Package.wapproj` を Visual Studio 2022（Windows application development ワークロード）で開き、`Release / x64` でパッケージを作成します。署名前に `Package.appxmanifest` の `Publisher` を署名証明書の Subject に合わせてください。`globalMediaControl` と `runFullTrust` の capability は削除しないでください。

## ライセンス

[MIT License](LICENSE)。同梱・利用しているサードパーティのソフトウェアについては [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を参照してください。
