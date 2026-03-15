# CodeUsageMap Mac主体 開発ガイド

## 1. 文書情報

- 文書名: CodeUsageMap Mac主体 開発ガイド
- 対象: `CodeUsageMap` PoC 実装担当
- 前提仕様: `visual-studio-extension-spec.md`
- Windows 検証補助資料: `windows-net48-validation-checklist.md`
- 作成日: 2026-03-14

## 2. 目的

本書は、Mac 主体で `CodeUsageMap` の共通解析基盤を先行実装し、後続で Windows 版 Visual Studio 向け VSIX を接続するための実装着手ガイドである。

以下をそのまま開始できる粒度で定義する。

- フォルダ構成
- プロジェクト分割
- 主要クラス一覧
- 依存関係
- PoC 実装手順
- 各ステップの完了条件

## 3. 開発方針

- 解析ロジックは IDE 非依存の `Core` に集約する
- データモデルと DTO は `Contracts` に集約する
- Mac 上では `Cli` により解析結果を確認する
- Windows 側では `Vsix` が `Core` を呼び出して UI に表示する
- 先に CLI で精度と速度を固め、その後に Visual Studio UI を載せる

## 4. 推奨フォルダ構成

```text
CodeUsageMap/
├─ docs/
│  ├─ visual-studio-extension-spec.md
│  └─ mac-first-development-guide.md
├─ src/
│  ├─ CodeUsageMap.Contracts/
│  │  ├─ Graph/
│  │  ├─ Analysis/
│  │  ├─ Serialization/
│  │  └─ Diagnostics/
│  ├─ CodeUsageMap.Core/
│  │  ├─ Symbols/
│  │  ├─ References/
│  │  ├─ Implementations/
│  │  ├─ Events/
│  │  ├─ Di/
│  │  ├─ Graph/
│  │  ├─ Serialization/
│  │  └─ Utilities/
│  ├─ CodeUsageMap.Cli/
│  │  ├─ Commands/
│  │  ├─ Options/
│  │  ├─ Formatting/
│  │  └─ Program.cs
│  └─ CodeUsageMap.Vsix/
│     ├─ Commands/
│     ├─ ToolWindows/
│     ├─ ViewModels/
│     ├─ Services/
│     └─ Resources/
├─ tests/
│  ├─ CodeUsageMap.Core.Tests/
│  ├─ CodeUsageMap.Contracts.Tests/
│  └─ CodeUsageMap.Integration.Tests/
└─ CodeUsageMap.sln
```

## 5. プロジェクト依存関係

依存は以下に固定する。

```text
CodeUsageMap.Contracts
    ↑
CodeUsageMap.Core
    ↑
CodeUsageMap.Cli
    ↑
CodeUsageMap.Vsix

CodeUsageMap.Tests -> Contracts / Core / Cli
```

ルール:

- `Contracts` は下位依存を持たない
- `Core` は Visual Studio API へ依存しない
- `Cli` は入出力と実行制御のみ持つ
- `Vsix` の責務は Visual Studio 連携と表示に限定する

## 6. プロジェクト別の役割

### 6.1 `CodeUsageMap.Contracts`

責務:

- `UsageGraph` の定義
- ノード、エッジ、列挙体
- 解析要求、解析結果 DTO
- シリアライズ契約
- 診断情報の共通定義

置くもの:

- `GraphNode`
- `GraphEdge`
- `UsageGraph`
- `NodeKind`
- `EdgeKind`
- `AnalyzeRequest`
- `AnalyzeOptions`
- `ReferenceInfo`
- `ImplementationInfo`
- `EventUsageInfo`
- `DiRegistrationInfo`
- `AnalysisDiagnostic`

### 6.2 `CodeUsageMap.Core`

責務:

- Roslyn によるシンボル解決
- 参照探索
- interface / override 展開
- イベント購読 / 解除 / 発火検出
- 将来の DI 解析
- `UsageGraph` 構築
- JSON / DGML シリアライズ実装

置くもの:

- `CSharpUsageAnalyzer`
- `RoslynSymbolResolver`
- `RoslynReferenceCollector`
- `RoslynImplementationCollector`
- `RoslynEventUsageCollector`
- `RoslynDiRegistrationAnalyzer`
- `CallGraphBuilder`
- `UsageGraphJsonSerializer`
- `DgmlExporter`

### 6.3 `CodeUsageMap.Cli`

責務:

- 入力引数の解析
- 解析対象の指定
- 出力形式の選択
- 結果の保存
- Mac 上での手元検証

置くもの:

- `Program`
- `CliAnalyzeCommand`
- `CliCommandHandler`
- `AnalyzeCommandOptions`
- `OutputFormat`
- `ConsoleReporter`

### 6.4 `CodeUsageMap.Vsix`

責務:

- 右クリックメニュー
- 現在カーソル位置から対象シンボル取得
- Tool Window 表示
- ノードクリックでコードジャンプ
- `Core` 呼び出し結果の UI 反映

置くもの:

- `CodeUsageMapPackage`
- `ShowUsageMapCommand`
- `UsageMapToolWindow`
- `UsageMapControl`
- `UsageMapViewModel`
- `VisualStudioSymbolContextService`
- `NavigationService`

### 6.5 `CodeUsageMap.Tests`

責務:

- 共通解析ロジックの単体テスト
- CLI の入出力テスト
- 回帰ケース追加
- 将来の性能回帰監視

## 7. 主要クラス一覧

### 7.1 Contracts

| クラス / 型 | 役割 |
| --- | --- |
| `UsageGraph` | 解析結果全体 |
| `GraphNode` | ノード情報 |
| `GraphEdge` | エッジ情報 |
| `AnalyzeRequest` | 解析要求 |
| `AnalyzeOptions` | 深さ、除外条件、出力条件 |
| `ReferenceInfo` | 参照検出結果 |
| `ImplementationInfo` | 実装 / override 情報 |
| `EventUsageInfo` | 購読、解除、発火、推定経路情報 |
| `AnalysisDiagnostic` | 警告や推定理由 |

### 7.2 Core

| クラス / 型 | 役割 |
| --- | --- |
| `CSharpUsageAnalyzer` | C# 解析の統括 |
| `RoslynWorkspaceLoader` | Solution / Project 読み込み |
| `RoslynSymbolResolver` | 入力から `ISymbol` 解決 |
| `RoslynReferenceCollector` | `FindReferencesAsync` ベース参照収集 |
| `RoslynImplementationCollector` | interface / override / 継承探索 |
| `RoslynEventUsageCollector` | `IEventAssignmentOperation` と発火検出 |
| `ReferenceClassifier` | 参照種別を `EdgeKind` に分類 |
| `CallGraphBuilder` | 重複除去とグラフ構築 |
| `UsageGraphJsonSerializer` | JSON 出力 |
| `DgmlExporter` | DGML 出力 |

### 7.3 Cli

| クラス / 型 | 役割 |
| --- | --- |
| `Program` | エントリポイント |
| `CliAnalyzeCommand` | `analyze` コマンド |
| `AnalyzeCommandOptions` | 引数定義 |
| `CliCommandHandler` | 実行フロー制御 |
| `ConsoleReporter` | サマリ出力 |

### 7.4 Vsix

| クラス / 型 | 役割 |
| --- | --- |
| `CodeUsageMapPackage` | VSIX パッケージ |
| `ShowUsageMapCommand` | コンテキストメニュー起点 |
| `VisualStudioSymbolContextService` | 現在位置取得 |
| `UsageMapToolWindow` | ツールウィンドウ |
| `UsageMapViewModel` | 表示データ整形 |
| `NavigationService` | ソースジャンプ |

## 8. CLI の最小仕様

初期 PoC では以下の形式を推奨する。

```bash
dotnet run --project src/CodeUsageMap.Cli -- analyze \
  --solution /path/to/Example.sln \
  --symbol "Namespace.Type.Method" \
  --depth 1 \
  --format json \
  --output /tmp/usage-map.json
```

必須オプション:

- `--solution`
- `--symbol`
- `--format`
- `--output`

任意オプション:

- `--project`
- `--document`
- `--line`
- `--depth`
- `--workspace-loader adhoc|msbuild`
- `--symbol-index <n>`
- `--exclude-tests`
- `--exclude-generated`

`--depth` は現在、参照元シンボル、実装シンボル、イベント関連シンボルを再解決して段階展開する。
そのため、Mac の `adhoc` と Windows の `msbuild` で同じ CLI 契約を保ちつつ、Windows 移行後もアルゴリズムを共通化できる。

対応フォーマット:

- `json`
- `dgml`
- `viewmodel-json`
- 曖昧解決時は `symbolResolution.candidates` を見て `--symbol-index` で再実行する
- Windows 側 Tool Window では `Export JSON` / `Export ViewModel JSON` / `Export DGML` を提供する
- `Depth 2+` では reference owner / implementation / event / outgoing call target を段階展開し、PoC では `200 symbols` / `64 candidates per symbol` を上限にする
- 最小 cache は process-local memory cache とし、同一 `solution + timestamp + symbol + options` の再解析を短縮する

## 9. PoC 実装手順

### Step 1. ソリューションとプロジェクト雛形を作る

やること:

- `CodeUsageMap.sln` を作成する
- `src/` と `tests/` の構成を作る
- `Contracts`、`Core`、`Cli` を先に作る
- `Vsix` は Windows 側で追加する前提で空き構成だけ決める

完了条件:

- Mac 上で `dotnet build` が `Contracts`、`Core`、`Cli` まで通る
- 依存方向が `Contracts <- Core <- Cli` に固定されている

### Step 2. Contracts を固める

やること:

- `NodeKind`、`EdgeKind` を定義する
- `UsageGraph`、`GraphNode`、`GraphEdge` を定義する
- `AnalyzeRequest` と `AnalyzeOptions` を定義する
- `ReferenceInfo`、`ImplementationInfo`、`EventUsageInfo` を定義する

完了条件:

- `Core` が未完成でも DTO が確定している
- JSON シリアライズ可能な形になっている

### Step 3. Core の入口を作る

やること:

- `IUsageAnalyzer` を定義する
- `CSharpUsageAnalyzer` を仮実装する
- `AnalyzeAsync` が空グラフを返す最小形を作る

完了条件:

- CLI から `CSharpUsageAnalyzer` を呼び出せる
- `UsageGraph` がファイル出力できる

### Step 4. シンボル解決を実装する

やること:

- solution 読み込みを実装する
- シンボル指定文字列から対象解決を行う
- 将来の VSIX 連携用に `SymbolResolveRequest` を汎用化する

完了条件:

- 指定したメソッド、クラス、event が `ISymbol` に解決できる
- 解決できない場合に診断が返る

### Step 5. 参照探索を実装する

やること:

- `FindReferencesAsync` による参照収集
- 参照元ファイル、行番号、プロジェクト名の保持
- direct call と単純参照の暫定分類

完了条件:

- 対象メソッドの参照一覧を JSON で出せる
- 複数プロジェクトでも収集できる

### Step 6. interface / override 展開を実装する

やること:

- interface 実装を収集する
- override / virtual 関係を収集する
- `Implements`、`Overrides` エッジを付与する

完了条件:

- interface メソッドから実装候補に辿れる
- base / derived 関係のメソッドをグラフへ載せられる

### Step 7. イベント購読解析を実装する

やること:

- `IEventAssignmentOperation` による `+=` / `-=` 収集
- メソッドグループ、ラムダ、匿名メソッドを抽出
- 発火コード検出
- `EventSubscription`、`EventUnsubscription`、`EventRaise`、`EventDispatchEstimated` を付与

完了条件:

- 購読と解除を別エッジで出力できる
- 発火からハンドラへの推定経路を出力できる
- 推定であることを `Confidence` と診断に載せられる

### Step 8. グラフ構築を実装する

やること:

- ノード重複除去
- エッジ重複除去
- `SymbolKey` ベースの識別
- 詳細ペイン向けメタデータ整理

完了条件:

- 同じシンボルが多重に出ない
- JSON / DGML の両方で同じ構造を出せる

### Step 9. JSON / DGML 出力を完成させる

やること:

- `UsageGraphJsonSerializer`
- `DgmlExporter`
- JSON スキーマの安定化
- DGML カテゴリと属性の整理

完了条件:

- 同一入力に対して安定した JSON を出力できる
- Visual Studio の DGML Viewer で開ける

### Step 10. CLI の使い勝手を整える

やること:

- `analyze` コマンドを完成させる
- エラーメッセージを明確化する
- サマリ表示を追加する
- `--exclude-tests`、`--exclude-generated`、`--workspace-loader` など PoC 必要オプションを追加する
- `viewmodel-json` 出力を追加する

完了条件:

- 手元で毎回同じ手順で解析できる
- 実装確認を Mac だけで回せる

### Step 11. テストを揃える

やること:

- 代表コードをテスト用 solution として用意する
- interface、override、event、generic の回帰ケースを用意する
- JSON スナップショットか構造比較テストを作る

完了条件:

- Core の主要機能に自動テストがある
- イベント購読解析の回帰が防げる
- same-solution DLL 参照補正は `tools/CodeUsageMap.MetadataNormalizationProbe` で Mac 上から smoke 確認できる

### Step 12. Windows で VSIX scaffold を接続する

やること:

- `src/CodeUsageMap.Vsix/CodeUsageMap.Vsix.csproj` を Windows 版 Visual Studio で開く
- 右クリックコマンドを追加する
- 現在位置のシンボルを解決し `Core` に渡す
- `UsageMapViewModel` を Tool Window に bind する
- ノードクリックでジャンプできるようにする

完了条件:

- Windows 版 Visual Studio 上でコマンドが動く
- Core の結果が Tool Window に表示される
- ノードジャンプが機能する
- `msbuild` loader 経路で解析できる

## 10. 2週間から3週間の現実的スケジュール

### Week 1

- Step 1 から Step 4
- Contracts 固定
- CLI から空グラフと最小解析結果を出力

### Week 2

- Step 5 から Step 8
- 参照、実装、イベント購読までを Core で完成
- JSON / DGML の出力を安定化

### Week 3

- Step 9 から Step 12
- CLI の磨き込み
- テスト追加
- Windows 版 Visual Studio で VSIX 接続

## 11. 実装時のルール

- Visual Studio API 依存コードは `Vsix` へ入れる
- Roslyn 依存コードは `Core` に閉じる
- 文字列ベースの暫定実装を増やしすぎず、`ISymbol` と `SymbolKey` を基準にする
- イベント経由の経路は `DirectCall` と混ぜない
- 推定結果は必ず `Confidence` と診断に反映する
- CLI は開発者の主要検証面として維持する

## 12. 最初に作るべきファイル一覧

最初の 1 日目で作る対象:

- `src/CodeUsageMap.Contracts/Graph/GraphNode.cs`
- `src/CodeUsageMap.Contracts/Graph/GraphEdge.cs`
- `src/CodeUsageMap.Contracts/Graph/UsageGraph.cs`
- `src/CodeUsageMap.Contracts/Graph/NodeKind.cs`
- `src/CodeUsageMap.Contracts/Graph/EdgeKind.cs`
- `src/CodeUsageMap.Contracts/Analysis/AnalyzeRequest.cs`
- `src/CodeUsageMap.Contracts/Analysis/AnalyzeOptions.cs`
- `src/CodeUsageMap.Core/CSharpUsageAnalyzer.cs`
- `src/CodeUsageMap.Core/Symbols/RoslynSymbolResolver.cs`
- `src/CodeUsageMap.Cli/Program.cs`
- `src/CodeUsageMap.Cli/Commands/CliAnalyzeCommand.cs`

## 13. Windows 移行時の引き継ぎ事項

Windows 側へ渡すべきもの:

- サンプル solution
- CLI で成功する入力例
- JSON 出力例
- ViewModel JSON 出力例
- DGML 出力例
- 未解決の精度課題一覧
- Tool Window に必要な表示項目一覧

## 14. 完了判定

このガイドに基づく PoC 完了は、以下を満たした時点とする。

- Mac で `Core` と `Cli` の開発が継続可能
- JSON / ViewModel JSON / DGML の各出力が利用可能
- interface / override / event 購読がグラフ化される
- Windows で VSIX が共通コアを利用して結果表示できる
