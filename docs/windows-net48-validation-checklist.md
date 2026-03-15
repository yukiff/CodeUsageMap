# CodeUsageMap Windows / .NET Framework 4.8 検証チェックリスト

## 1. 目的

本書は、Windows 版 Visual Studio 上で `CodeUsageMap.Vsix` を用い、`.NET Framework 4.8` を含む C# solution に対して `CodeUsageMap` が実用可能かを確認するための実施チェックリストである。

検証結果の記録には `windows-net48-validation-report-template.md` を使用する。

対象:

- Windows 版 Visual Studio 2022
- `MSBuildWorkspace` 経路
- 従来 csproj を含む `.NET Framework 4.8` project
- VSIX の install / 起動 / Tool Window 表示 / 再解析 / export 前提確認

## 2. 前提条件

- Windows 11 または同等の検証用 Windows 環境
- Visual Studio 2022 17.10 以降
- `.NET Framework 4.8 Developer Pack`
- 対象 solution を Visual Studio で restore / build できること
- `src/CodeUsageMap.Vsix/CodeUsageMap.Vsix.csproj` を Windows 側で開けること

推奨:

- `.NET Framework 4.8` の従来 csproj を最低 1 件含む検証 solution
- `packages.config` 利用 project を最低 1 件含むこと
- `ProjectReference` を跨ぐ solution
- `#if` を含む条件付きコンパイルコード
- event、interface、override を含むコード

## 3. 検証対象 solution の最低構成

最低限、以下のパターンを含める。

1. `.NET Framework 4.8` class library
2. `.NET Framework 4.8` executable project
3. `packages.config` を使う project
4. `ProjectReference` を 1 つ以上持つ project
5. interface 実装と override を持つコード
6. event `+=` / `-=` / `Invoke` を持つコード
7. 条件付きコンパイル `#if DEBUG` などを含むコード

## 4. VSIX ビルド確認

### 4.1 restore / build

- `CodeUsageMap.Vsix.csproj` を Visual Studio で開ける
- NuGet restore が成功する
- VSIX project が build 成功する
- Experimental Instance 起動まで成功する

記録項目:

- Visual Studio version
- `Microsoft.VisualStudio.SDK` version
- `Microsoft.VSSDK.BuildTools` version
- build 成否
- warning / error の要約

### 4.2 install / debug

- Experimental Instance に VSIX がロードされる
- エディタ右クリックに `使用関係マップを表示` が出る
- command 実行で例外ダイアログが出ない

## 5. `.NET Framework 4.8` solution load 確認

### 5.1 `MSBuildWorkspace` 経路

- `.NET Framework 4.8` solution を開いた状態で command を起動できる
- `MSBuildWorkspace` で solution load が成功する
- classic csproj で symbol resolve が成功する
- `ProjectReference` を跨ぐ参照収集が成功する

### 5.2 `packages.config`

- `packages.config` 利用 project で symbol resolve が成功する
- NuGet package 型参照があっても解析が異常終了しない
- package 由来の参照がノイズ過多にならない

### 5.3 条件付きコンパイル

- `#if` を含むコードで symbol resolve が失敗しない
- active configuration に応じた解決結果になる
- configuration 差分がある場合は挙動を記録する

## 6. 機能確認

### 6.1 シンボル解決

- method
- property
- event
- interface method
- override method
- attribute usage

確認観点:

- 右クリック位置で期待する symbol が選ばれる
- overload がある場合に誤解決しない
- generic method でも解決できる

### 6.2 解析結果

- source references が出る
- interface 実装が出る
- override が出る
- event subscription / unsubscription / raise が出る
- `Depth 1 / 2 / 3` で件数が変わる
- `ExcludeTests` / `ExcludeGenerated` が効く

### 6.3 Tool Window

- `root / incoming / outgoing / related / details` が表示される
- `Refresh` が current symbol 優先で再解析する
- `Cancel` が実行中解析を止める
- status / error message が更新される
- node / relation からコードジャンプできる

## 7. 記録すべき失敗パターン

- classic csproj だけ symbol resolve に失敗する
- `packages.config` project だけ load に失敗する
- conditional compilation により誤 symbol 解決する
- Framework 固有 API を含む file で解析が落ちる
- Refresh 後に古い結果が Tool Window を上書きする
- Cancel 後も UI が busy のまま残る

## 8. 判定基準

### 8.1 合格

- `.NET Framework 4.8` solution で command 起動から Tool Window 表示まで成功する
- interface / override / event の主要ケースが取得できる
- `Refresh`、`Cancel`、`Depth`、`ExcludeTests`、`ExcludeGenerated` が期待通り動作する
- 重大例外なしで複数回再解析できる

### 8.2 条件付き合格

- 主要機能は動くが、`packages.config`、条件付きコンパイル、attribute、event など一部ケースで精度課題が残る
- 重大クラッシュはないが、結果にノイズまたは欠落がある

### 8.3 不合格

- `.NET Framework 4.8` solution を安定して load できない
- symbol resolve が高頻度で失敗する
- Tool Window が再解析や cancel で不安定になる
- VSIX 自体の build / install / command 起動が不安定

## 9. 実施結果記録テンプレート

```text
検証日:
担当者:
Windows version:
Visual Studio version:
Target solution:

1. VSIX build:
- restore:
- build:
- Experimental Instance:

2. .NET Framework 4.8 load:
- solution load:
- classic csproj:
- packages.config:
- conditional compilation:

3. Feature checks:
- symbol resolve:
- references:
- implementations / overrides:
- event analysis:
- depth:
- filters:
- refresh:
- cancel:
- navigation:

4. Findings:
- 

5. 判定:
- 合格 / 条件付き合格 / 不合格
```
