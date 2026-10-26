# ユアパーク：PlayerData Capture Framework v0

Issue [#2](https://github.com/kissho222/your-park/issues/2) のローカル取得・保存・再読込・再生・削除基盤。Unity **6000.3.10f1** / Windows。生成AIモデルは含まない。

## 起動

1. Unity Hubでこのフォルダを追加し、6000.3.10f1で開く。
2. `Assets/YourParkDemo/PlayerDataDemo.unity` を開いてPlay。
3. Sceneがまだない場合は `Your Park > Create PlayerData Demo Scene` で生成する。

別のUnityプロジェクトへ導入する場合は `Packages/com.yourpark.playerdata` を埋込パッケージとしてコピーする。デモとゲーム進行は依存しない。

## デモ操作

すべてクリック操作。起動時にカメラ・マイクは使用しない。実際に録音・撮影する場合だけボタンを押す。保存場所は画面上へ常時表示する。

1. `Start camera` → ライブ映像 → `Save frame` で代表PNGを保存。
2. `Record 10 s` でマイクテスト音声を録音し、WAVで保存。`Cancel capture` で中止。
3. `Save mock motion (10 s)` で**合成したテスト用**ランドマーク列を保存。実際の身体追跡ではない。
4. ドロップダウンで対象を選び `Load selected into RAM` → `Replay selected (RAM)`。
5. `Open PlayerData folder` で保存先を確認。
6. アプリを終了して再起動 → `Read manifest` → 同じ対象をロード・再生。
7. 再生したい対象をRAMへロードしてから `Delete PlayerData only` → `Delete`。
8. 同じ対象の再生は継続可能。未ロードの対象は利用不能。`Check StoryProgress` でデモの進行データが残ることを確認。
9. アプリ終了・再起動後、削除した対象は一覧から消え再生できない。

`StoryProgress.demo.json` はこのデモ専用の独立した検証ファイル。実ゲームのセーブシステムは空リポジトリに存在しないため未接続。

## 保存先と制限

`Application.persistentDataPath/PlayerData/` に `manifest.json`、`samples/<stable-id>.wav|png|json` を保存する。Windowsデモの既定親フォルダは `%USERPROFILE%/AppData/LocalLow/YourPark/YourPark Capture Demo/`。

データは暗号化しない。外部への送信・一般用途のインポート／エクスポート機能はない。保存先をOSや別ソフトがバックアップすることまでは制御しない。RAMの解放はアプリ寿命で管理し、セッション終了時に所有するバイト配列をクリアする。OSスワップやデバイスドライバーの残留までの安全消去は保証しない。

最大30秒/クリップ、16MiB/データ、128件、64MiB/RAMセッション。画像4096×4096以下。Poseは1800フレーム・128ランドマーク以下。WAVはPCM16/1〜2ch/8〜96kHz。

仕様・責務・拡張境界は [docs/PLAYER_DATA_FRAMEWORK.md](docs/PLAYER_DATA_FRAMEWORK.md)、検証結果と残件は [.ai/HANDOFF.md](.ai/HANDOFF.md) を参照。

## 検証コマンド

```powershell
$unityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe'
& $unityEditor -batchmode -nographics -projectPath $PWD -runTests -testPlatform EditMode -testResults "$PWD/TestResults/editmode.xml" -logFile "$PWD/TestResults/editmode.log"
& $unityEditor -batchmode -quit -projectPath $PWD -executeMethod YourPark.Demo.Editor.PlayerDataDemoBuilder.BuildWindows -logFile "$PWD/TestResults/build.log"
# 合成データのプロセス試験用Development Buildは別フォルダに生成。
& $unityEditor -batchmode -quit -projectPath $PWD -executeMethod YourPark.Demo.Editor.PlayerDataDemoBuilder.BuildWindowsDevelopment -logFile "$PWD/TestResults/build-development.log"
# ビルド終了後、別々の3プロセスで保存→再読込/削除→削除済み確認。
& ./tools/verify-process-smoke.ps1
```

WindowsのGUI実行ファイルはPowerShellから起動後も動き続ける場合があるため、プロセス終了とXML/ログの最終結果を確認する。テストデータはOSの一時フォルダに作成し、本物のカメラ・マイクを起動しない。

通常利用する実行ファイルは `Builds/Windows/YourParkCapture.exe`。Development BuildはUnity標準のPlayerConnection検出通信を含むため、合成テスト専用とする。通常ビルドにはプロセス試験用入口も含めない。
