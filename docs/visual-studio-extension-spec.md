# CodeUsageMap Visual Studio Extension 仕様書

## 1. 文書情報

- 文書名: CodeUsageMap Visual Studio Extension 仕様書
- 対象プロダクト: Visual Studio 拡張 `CodeUsageMap`
- 版数: 0.4
- 作成日: 2026-03-15
- 対象読者: プロダクトオーナー、設計者、実装者、レビュー担当

## 2. 目的

`CodeUsageMap` は、Visual Studio 上でカーソル位置のシンボルを起点に使用関係を解析し、コード理解と影響範囲把握を支援する。

主目的:

- 変更影響範囲の把握
- 呼び出し元、呼び出し先、実装、継承、イベント購読の追跡
- interface / override / event / DI を含む関係の可視化
- Windows 版 Visual Studio 向け VSIX として利用できる形で提供

## 3. 方針

- 拡張方式は `VSIX / VSSDK` を採用する
- 解析の中心は Roslyn とする
- 開発は Mac 主体、VSIX のビルドと実機検証は Windows で行う
- UI は段階導入とし、PoC では暫定 relation list UI、目標は graph canvas UI とする
- metadata より source を優先する
- 静的に確定できない関係は推定として扱う

補足:

- Visual Studio for Mac は対象外とする
- `VisualStudio.Extensibility` は現時点では採用しない

## 4. スコープ

### 4.1 対象

- Windows 版 Visual Studio 向け VSIX
- Mac で開発可能な `Core / CLI / Contracts`
- C# ソリューション解析
- `.NET Framework 4.8` を含む Windows 側 `MSBuildWorkspace` 解析
- method / class / interface / property / event 起点の解析
- 参照、呼び出し、実装、継承、イベント購読、イベント発火の可視化
- JSON / ViewModel JSON / DGML 出力

### 4.2 初版対象外

- C++ 本格解析
- reflection や実行時条件を完全に解決する DI
- metadata / decompiled view を正規経路とした追跡
- Visual Studio 以外の IDE 向け正式 UI
- 高機能な自由配置グラフエディタ

## 5. 利用環境

### 5.1 Mac 側

- `CodeUsageMap.Contracts`
- `CodeUsageMap.Core`
- `CodeUsageMap.Cli`
- CLI による解析、JSON / DGML / ViewModel JSON 生成

### 5.2 Windows 側

- `CodeUsageMap.Vsix`
- Visual Studio 上の Tool Window
- 右クリックコマンド
- コードジャンプ
- `MSBuildWorkspace` による solution 解析

## 6. 段階導入

### 6.1 Phase 1

- C# のみ
- method / class 起点
- project 横断参照
- interface / override 展開
- event 購読 / 解除 / 発火推定
- JSON / ViewModel JSON / DGML 出力
- VSIX Tool Window

### 6.2 Phase 2

- DI 登録解析
- 影響範囲分析
- グラフ保存と再利用
- ランキング、スコア、絞り込み強化

### 6.3 Phase 3

- C++ 連携
- mixed solution 可視化
- 差分レビュー支援

## 7. 機能要件

### 7.1 起動

- 右クリックメニューに `使用関係マップを表示` を追加する
- 起動時に現在カーソル位置のシンボルを解決する
- method / class / interface / property / event を root 候補とする

### 7.2 解析

- solution 全体の参照を収集できること
- interface 実装、override、継承を収集できること
- `+=` / `-=` による event 購読と解除を収集できること
- event 発火コードを収集できること
- 参照を `DirectCall`、`InterfaceDispatch`、`DiResolvedCall`、`Reference`、`Implements`、`Overrides`、`InjectedByDi`、`InstantiatedBy`、`ContainsSubscription`、`EventSubscription`、`EventUnsubscription`、`EventHandlerTarget`、`EventRaise`、`EventDispatchEstimated`、`UnknownDynamicDispatch` に分類できること
- `Depth` による段階展開ができること

### 7.3 可視化

- root と関連ノードを Tool Window に表示できること
- inbound と outbound を分けて表示できること
- ノード詳細を表示できること
- diagnostics、partial result、除外情報を表示できること
- 選択ノードから source 定義へジャンプできること

### 7.4 出力

- `UsageGraph` を内部保持できること
- JSON / ViewModel JSON / DGML へ出力できること
- 共通 output envelope を持つこと
- node / edge metadata を出力できること

### 7.5 フィルタ

初版必須:

- `Depth`
- `ExcludeTests`
- `ExcludeGenerated`
- `SearchText`
- `EdgeKind`
- `NodeKind`
- `ProjectName`
- `MinimumConfidence`

次段階:

- 名前空間
- アクセス修飾子
- 外部ライブラリ除外
- `System.*` 非表示
- NuGet 非表示
- interface 強調
- event 強調
- 非同期フロー強調

## 8. 非機能要件

### 8.1 応答性

- Tool Window 表示: 300ms 目標、500ms 上限目標
- root 最小情報表示: 500ms 以内目標
- depth 1 完了: 2 秒以内目標
- 2 秒超過時は progress 表示必須
- 5 秒超過時は cancel 必須
- UI スレッドは 100ms 以上連続ブロックしないことを目標とする

### 8.2 表示方式

- first paint は最小 root 情報のみで成立させる
- full graph は後続で非同期更新する
- 再解析時は空画面に戻さず既存表示を維持して差し替える

### 8.3 打ち切り

PoC の既定上限:

- 参照取得上限: 500 / symbol
- ノード上限: 1000
- エッジ上限: 2000
- 展開シンボル上限: 200

打ち切り時:

- root は保持する
- diagnostics を出す
- `partialResult=true` を出力する

### 8.4 正確性

- 確定、推定、不明を区別する
- event 経由の実行経路は推定扱いとする
- DI や動的 dispatch は確定扱いしない
- `UnknownDynamicDispatch` は source 解決不能な dynamic invocation を表す低信頼 edge とする

## 9. システム構成

### 9.1 層構成

1. Visual Studio 拡張層
2. 解析エンジン層
3. 可視化層

### 9.2 役割

Visual Studio 拡張層:

- コマンド登録
- カーソル位置のシンボル解決
- Tool Window 表示
- コードジャンプ

解析エンジン層:

- symbol 解決
- reference collect
- implementation collect
- event collect
- graph 構築

可視化層:

- ViewModel 変換
- graph canvas 表示
- relation list 表示
- DGML / JSON 出力

## 10. 推奨ソリューション構成

```text
CodeUsageMap.sln
├─ src/CodeUsageMap.Contracts
├─ src/CodeUsageMap.Core
├─ src/CodeUsageMap.Cli
├─ src/CodeUsageMap.Vsix
├─ tests/CodeUsageMap.Contracts.Tests
├─ tests/CodeUsageMap.Core.Tests
├─ tests/CodeUsageMap.Integration.Tests
└─ tools/
```

## 11. 主要責務

- `CodeUsageMap.Contracts`: 契約、graph model、presentation model
- `CodeUsageMap.Core`: Roslyn 解析、graph 構築、serializer
- `CodeUsageMap.Cli`: Mac での検証と出力
- `CodeUsageMap.Vsix`: Windows 側 UI 統合

## 12. データモデル

### 12.1 Core モデル

- `GraphNode`
- `GraphEdge`
- `UsageGraph`
- `NodeKind`
- `EdgeKind`

必須属性:

- `Id`
- `DisplayName`
- `Kind`
- `ProjectName`
- `FilePath`
- `LineNumber`
- `SymbolKey`
- `Properties`

### 12.2 Presentation モデル

暫定 UI 契約:

- `UsageMapViewModel`
- `UsageMapNodeViewModel`
- `UsageMapRelationViewModel`
- `UsageMapDetailItem`

目標 graph canvas 契約:

- `GraphCanvasViewModel`
- `CanvasNodeViewModel`
- `CanvasEdgeViewModel`
- `ToolbarViewModel`
- `SearchPanelViewModel`
- `DetailsViewModel`
- `LegendViewModel`
- `MiniMapViewModel`

## 13. 解析仕様

### 13.1 基本フロー

1. カーソル位置から symbol 解決
2. solution 読み込み
3. 参照収集
4. implementation / override 収集
5. event 収集
6. outgoing call 収集
7. graph 構築
8. ViewModel / JSON / DGML 生成

### 13.2 event

- `IEventAssignmentOperation` を用いる
- 購読 / 解除は高信頼で扱う
- 発火から handler への edge は推定で扱う

### 13.3 same-solution DLL 参照

- `ProjectReference` を正経路とする
- same-solution DLL 参照は source 正規化を試みる
- source 正規化成功時は source として扱う
- source 正規化失敗時は graph の正規 node に載せず、`unresolved_binary_reference` 診断を返す
- solution 内候補を持たない外部 library は `metadata` として扱う

### 13.4 source 優先ルール

出力 metadata:

- `symbolOrigin`
- `normalizedFromMetadata`
- `normalizationStrategy`
- `assemblyIdentity`
- `limitation`

## 14. UI 仕様

### 14.1 目標 UI

- 上部: 検索、表示モード、フィルタ、レイアウト再配置
- 左: シンボル検索結果
- 中央: graph canvas
- 右: ノード詳細
- 下部: status、progress、warning

### 14.2 graph canvas

- root を中央固定
- inbound を左
- outbound を右
- 初期表示は depth 2
- depth 3 以降は折りたたみまたは追加展開

### 14.3 ノード

表示要素:

- シンボル名
- 種別アイコン
- 所属プロジェクト
- 主要メトリクス

外観方針:

- `Class`: 青系
- `Interface`: 紫系
- `Method`: 緑系
- `Property`: 水色系
- `Event`: オレンジ系
- `External`: 灰色
- `Warning`: 赤系アクセント

### 14.4 エッジ

- 実線: 呼び出し
- 破線: 参照
- 点線: 推定
- ラベル: `calls`、`implements`、`inherits`、`subscribes`、`publishes`、`injects`

### 14.5 操作

- クリック: 選択
- ダブルクリック: コードジャンプ
- ホバー: ツールチップ
- 右クリック: コンテキストメニュー
- ドラッグ: パン
- ホイール: ズーム
- 折りたたみ / 展開

### 14.6 reroot

- 明示 reroot はコンテキストメニューまたは専用コマンドで行う
- graph canvas では `reroot` と `コードジャンプ` を同一操作に割り当てない

### 14.7 Follow caret

`Follow caret` を Tool Window オプションとして提供する。

既定値:

- OFF

挙動:

- OFF: root はコマンド実行、Refresh、明示 reroot のときのみ更新する
- ON: カーソル位置の symbol に root を追従させる
- 追従更新には debounce を入れる

PoC の debounce 方針:

- 300ms を既定値とする

ダブルクリックとの関係:

- ダブルクリックの主動作は `コードジャンプ` とする
- `Follow caret=ON` の場合、ジャンプ後の caret 移動により結果的に root が更新される
- これは `コードジャンプの結果として reroot される` 挙動であり、ダブルクリック自体の意味は変えない

### 14.8 表示モード

- 呼び出しマップ
- 依存マップ
- 継承 / 実装マップ
- イベントフロー

既定:

- 呼び出しマップ

### 14.9 右ペイン詳細

- 完全修飾名
- 定義ファイル
- プロジェクト
- シグネチャ
- summary コメント
- inbound 件数
- outbound 件数
- 継承 / 実装関係
- event 購読 / 発火
- 複雑度

### 14.10 視認性

- ミニマップ
- パンくず
- 凡例
- 直接関係のみ強調する focus mode

### 14.11 暫定 UI

PoC の現行 UI は relation list と graph canvas preview を併用する暫定 UI とする。

構成:

- 上段: graph canvas preview
- 左: `incomingRelations`
- 中央: `root` と `outgoingRelations`
- 右: `relatedRelations`
- 下: `details`

用途:

- Windows 接続確認
- 解析結果検証
- graph canvas preview と relation list の併用による PoC UI

## 15. 出力仕様

### 15.1 共通 output envelope

- `schemaVersion`
- `analysisOptions`
- `workspaceLoader`
- `generatedAt`
- `partialResult`
- `symbolResolution`
- `diagnostics`

### 15.2 JSON

- `{ metadata, graph }`

### 15.3 ViewModel JSON

- `{ metadata, viewModel }`

### 15.4 DGML

- `DirectedGraph` ルート属性に主要 metadata を持つ
- node / edge の `Properties` を属性展開する

## 16. Windows / Mac 差分

### 16.1 Mac

- `AdhocWorkspace` を既定とする
- Core / CLI の開発と検証に使う
- `.NET Framework 4.8` の完全再現は保証しない

### 16.2 Windows

- `MSBuildWorkspace` を既定とする
- VSIX 実行と実機検証を行う
- `.NET Framework 4.8` の正式検証対象とする

## 17. 制約

- event の実行時接続は完全確定できない
- DI は初版では限定対応とする
- metadata 依存を source より優先しない
- graph canvas は preview 実装済みだが、Windows 実機での WPF 実描画・操作検証は未完了である

## 18. 受け入れ条件

- C# symbol から root を生成できる
- 参照、implementation、event、DI、dynamic dispatch を収集できる
- `Depth`、`ExcludeTests`、`ExcludeGenerated` が動作する
- JSON / ViewModel JSON / DGML を出力できる
- Tool Window が 500ms 以内に root 最小情報を表示できる
- cancel と progress が機能する
- `Follow caret` の ON / OFF が機能する
- same-solution DLL 参照で source 正規化が機能する
- current analyzer が出力する edge kind 全種類を回帰検証できる

## 19. 今後の拡張

- DI 解決関係の可視化
- graph canvas 実装
- ミニマップ、パンくず、凡例
- 循環参照ハイライト
- ホットパス強調
- C++ 解析アダプタ
- mixed solution 可視化

## 20. 補足

本仕様書は要求、設計方針、受け入れ条件のみを保持する。実装状況、改善メモ、個別タスク、検証ログは別文書で管理する。

関連文書:

- [open-implementation-tasks.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/open-implementation-tasks.md)
- [validation-coverage-matrix.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/validation-coverage-matrix.md)
- [windows-net48-validation-checklist.md](/Users/funabashiyuuki/programming/CodeUsageMap/docs/windows-net48-validation-checklist.md)
