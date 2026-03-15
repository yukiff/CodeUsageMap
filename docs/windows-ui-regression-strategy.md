# CodeUsageMap Windows UI 回帰方針

## 1. 目的

Windows 版 Visual Studio 上で動作する `CodeUsageMap.Vsix` について、UI 回帰を継続的に検出できる形を定義する。

対象:

- command 起動
- Tool Window 表示
- root 最小情報表示
- graph canvas preview
- refresh
- cancel
- diagnostics 表示
- export 導線
- `Follow caret`

## 2. 方針

Windows UI 回帰は次の 3 層で扱う。

1. 非 UI 層の自動回帰
2. VSIX build / install / launch の smoke
3. Visual Studio UI のシナリオ回帰

補足:

- 解析結果の構造回帰は Mac 側 probe で先に吸収する
- Windows では VSIX 統合と Visual Studio 固有挙動に絞る

## 3. レイヤ別の回帰対象

### 3.1 非 UI 層

目的:

- graph、ViewModel、DGML、metadata、symbol normalization の回帰を先に検出する

実行手段:

- `scripts/run_mac_validation.sh`
- `CodeUsageMap.MetadataNormalizationProbe`
- `CodeUsageMap.SerializationProbe`
- `CodeUsageMap.SnapshotRegressionProbe`

### 3.2 VSIX smoke

目的:

- Windows 上で VSIX が restore / build / launch できることを機械的に確認する

対象:

- solution build
- VSIX build
- Experimental Instance 起動
- command 登録確認
- Tool Window 生成確認

### 3.3 UI シナリオ回帰

目的:

- Visual Studio 上の主要操作の破綻を検出する

対象シナリオ:

1. code window で command 実行
2. Tool Window 初期表示
3. root 最小情報表示
4. refresh
5. cancel
6. diagnostics 表示
7. export
8. `Follow caret` OFF
9. `Follow caret` ON
10. symbol candidate 選択
11. graph canvas zoom / pan
12. graph canvas minimap / breadcrumb / legend
13. graph canvas collapse / expand
14. graph canvas context menu / reroot
15. display mode 切替

## 4. 推奨実装方針

### 4.1 Phase A

最初に自動化するもの:

- Windows build script
- VSIX build 成否
- Experimental Instance 起動確認
- ログ採取

理由:

- 最小コストで壊れやすい統合点を確認できる

### 4.2 Phase B

次に自動化するもの:

- UI smoke シナリオ
- command 実行
- Tool Window 表示
- refresh / cancel

方法:

- PowerShell スクリプト
- Visual Studio Experimental Instance
- 可能なら UI Automation

### 4.3 Phase C

最後に自動化するもの:

- `Follow caret`
- export dialog
- candidate selection
- graph canvas 導入後の操作

理由:

- タイミング依存が強く、最初から自動化しにくい

## 5. 最低限の自動シナリオ

### S-001 VSIX build

- solution restore
- solution build
- VSIX build

### S-002 Tool Window smoke

- Experimental Instance 起動
- command 実行
- Tool Window が生成される
- root summary が表示される

### S-003 Refresh / Cancel

- refresh 実行
- status 更新
- cancel 実行
- canceled 状態表示

### S-004 Follow caret

- OFF: caret 移動で root が変わらない
- ON: caret 移動で root が変わる

### S-005 Graph canvas smoke

- graph canvas preview が表示される
- Ctrl + Wheel で zoom できる
- middle drag で pan できる
- minimap が描画される
- node collapse / expand が動く
- context menu の Open / Reroot が使える
- display mode 切替で内容が変わる

## 6. 実装上の注意

- UI 文字列一致に過度に依存しない
- root title、status、diagnostics count など観測点を限定する
- dialog を伴う export は最初は mock か save path 固定で扱う
- debounce を含む `Follow caret` は待機時間を明示する
- graph canvas は座標一致ではなく主要操作の成否と表示要素の有無で確認する

## 7. 推奨成果物

- `scripts/run_windows_validation.ps1`
- `scripts/run_windows_ui_smoke.ps1`
- 検証ログ出力先 `out/validation/windows/`
- 実施結果をまとめるレポート

## 8. 完了条件

- build / VSIX / Tool Window の smoke が自動で通る
- refresh / cancel が自動で確認できる
- `Follow caret` ON / OFF を最低 1 ケースずつ自動確認できる
- 失敗時にログからどの段階で落ちたか分かる
