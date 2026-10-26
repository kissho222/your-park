# PlayerData Capture Framework v0

## 対象と分担

CodexはIssue #2を実装する。Issue #1のdotによるモデル・ライセンス・性能調査、および `AI_TECH_RESEARCH.md` / `PLAYER_DATA_MATRIX.md` には変更を加えない。モデルを先行採用せず、交換可能な境界だけを用意する。

新規C#の責務を以下に定義する。

| ファイル | 責務 |
| --- | --- |
| `PlayerDataModels.cs` | 版付きmanifest・metadata、Voice/Image/Silhouette/Pose/Motion、結果型 |
| `PlayerDataContracts.cs` | 保存・音声変換/生成・姿勢追跡・再生・外観処理・デバイス一覧の境界 |
| `PlayerDataValidation.cs` | ID/時刻/版/件数/パス/サイズ/構造/整合性検証、Pose JSON変換 |
| `LocalPlayerDataStore.cs` | PlayerData内だけの保存・読込・削除、SHA256、原子的manifest更新 |
| `PlayerDataSession.cs` | 明示ロードしたRAMデータの所有、ディスク削除からの独立、終了時解放 |
| `WavCodec.cs` | 外部ライブラリ不要のPCM16 WAV変換 |
| `MockProcessors.cs` | 合成Pose、raw画像・音声パススルー、音声生成未実装の明示 |
| `UnityCaptureDevices.cs` | 任意開始のカメラ／マイク、タイムアウト・中断・機器解放 |
| `LandmarkMotionReplayer.cs` | Scene配置済みのダミー点への時間ベース再生 |
| `PlayerDataDemoController.cs` | UXMLのイベント接続・一覧・再生・削除確認・進行fixture確認 |
| `PlayerDataDemoBuilder.cs` | 明示実行するEditor専用のScene作成・Windowsビルド |
| `PlayerDataStoreTests.cs` | 保存・削除・再読込・異常系・デバイス境界のEditMode回帰 |
| `PlayerDataProcessSmoke.cs` | Development Build限定・合成データ専用のプロセス間寿命検証 |

ゲーム進行やセーブシステムを参照するRuntimeマネージャーは新設しない。固定UIの見た目は `PlayerDataDemo.uxml/.uss` とSceneを正本にし、Runtimeでボタンや固定レイアウトを生成しない。

## 保存契約

- `LocalPlayerDataStore(gameDataDirectory)` は必ずその直下の `PlayerData` だけを扱う。通常進行は兄弟ファイル／ディレクトリへ保存する。
- metadataは32桁GUID、種類、UTC作成時刻、入力元、version、秒数、フレーム数、ローカル相対パス、バイト数、SHA256を持つ。
- 音声のframeCountはチャンネルごとのサンプルフレーム数。画像は1、Pose/Motionはフレーム列の長さ。作成時刻はUTCのISO 8601、Pose時刻はクリップ相対秒。
- manifestと各データのversionは1。将来版は自動移行や上書きをせず明確な失敗を返す。
- JSON出力はUnity JsonUtility、manifest/Pose入力は標準DataContractJsonSerializerで必須フィールドとnullを厳密に検査する。追加のJSONライブラリは導入しない。Reflectionに必要な型はlink.xmlで保持する。
- 同一IDへの上書きは禁止。データを書き終えてからmanifestを原子的に公開する。電源断時の未公開データは一覧に出さない。次回起動時に勝手に回収・復活させない。
- ローカルファイルパスはIDと種類からのみ決定。パストラバーサル、リンク／ジャンクション、重複ID、長さ／hash不一致を拒否する。
- v0は単一アプリ・単一writerを想定。暗号化／難読化は `IPlayerDataStore` 差替で追加する。敵対的な別プロセスによる同時ファイルシステム改変に対するサンドボックスではない。

## 削除と寿命

```text
保存済みデータ → Session.Load(id) → RAMスナップショット
                       ↓
                DeleteDiskData()
                       ↓
ディスクなし / ロード済みRAMだけ利用可 / 進行セーブは不変
                       ↓
              アプリ終了・Dispose
                       ↓
         新しいSessionは削除済みIDをロード不可
```

ロードしていないファイルは削除後には使えない。RAMスナップショットは再保存しない。static、PlayerPrefs、隠しキャッシュへ退避しない。`Dispose` は所有するバイト配列をクリアする。Unityの音声／Texture派生リソースは利用側で停止・Destroyし、セッション終了時に残さない。

削除に失敗した場合は成功と表示しない。部分削除も失敗として扱い、明示的に再試行できる。通常セーブや親フォルダは削除しない。

## 入力とフォールバック

- CameraとMicrophoneはボタンクリック時だけ開始。OS許可が拒否された場合や機器が存在しない場合は `DataResult` の失敗で返す。開始タイムアウトはCamera8秒、Microphone5秒。録音時間も最大30秒。
- v0カメラはraw PNG。人物切抜き・姿勢・顔・手の実推定は未導入。映像の回転／鏡像補正は今後のカメラ表示・外観アダプターで扱う。
- `MockPoseTracker` は本人の動きと無関係な6点の合成運動を生成。`coordinateSpace=normalized-image-y-up`、ランドマーク名と配列順は全フレームで一定。実トラッカーは `IPoseTracker` を実装して同じ保存経路へ渡す。
- `IVoiceConverter` は元音声をそのまま返す。`IVoiceGenerator` は未提供を返す。`IAppearanceProcessor` はraw画像を返す。これらをAIの品質評価と混同しない。
- 音声録音は `CaptureSource.MicrophoneTest` とstable IDで識別でき、後の本人VoiceIDへ紐付け可能。VoiceIDの自動選択やゲーム固有の紐付けは呼出側の責務。

## 検証範囲

自動テストは合成データと一時保存先だけを使用する。ストア再構築とSession再構築による再起動相当の確認と、実アプリのプロセス終了・再起動確認を区別する。実機カメラ／マイクの録音・撮影はユーザーの明示操作で確認し、テストで勝手に取得しない。Windows実行ファイル、UI実操作の確認状況はHANDOFFに記す。
