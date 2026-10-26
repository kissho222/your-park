# AI / PlayerData 技術調査

対象: Issue #1

## 結論

v0ではユーザー個別の追加学習を採用しない。

現時点の推奨は以下。

1. 既存のPlayerData Frameworkを正本とし、raw Camera / Microphone / Mock Poseの境界を維持する。
2. 身体・表情・手は、追加学習ではなくランドマーク推論→時系列保存→再生で実現する。
3. 本人音声の復旧は、設定時に録音したVoiceSample IDと生マイクルートの復旧で実現する。
4. 固定声のリアルタイムVCは後段の追加機能。RVC / MMVC系を候補にするが、モデルと音源の権利はコードと別に監査する。
5. 任意参照音声によるzero-shot VCは製品必須にしない。Seed-VCはGPLv3かつ2025-11-21にアーカイブ済みのため、製品依存には採用しない。
6. 歌唱はリアルタイム必須にしない。SoulX-Singer-SVCはApache-2.0の候補だが、v0では保留し、オンデマンド生成のPoCだけを検討する。
7. LivePortraitはコードMITだが、付属InsightFace事前学習モデルが非商用。検出モデル置換なしでは商用製品に採用しない。
8. 軽量CVはUnity内推論も候補。重いVC / 歌唱 / PyTorch系は別プロセス+IPCを優先し、Unity本体からGPU寿命を分離する。

この判断により、ゲーム内の「AI学習」という表現は実装事実とずれる可能性が高い。規約・同意文では「取得・解析・加工・再構成・生成」を基本語彙とし、実際に個別学習を導入した場合だけ「学習」を追加する。

## 現在の交換境界との対応

`feat/playerdata-capture-v0` では以下の境界が既にある。

- `IPoseTracker`
- `IAppearanceProcessor`
- `IVoiceConverter`
- `IVoiceGenerator`
- `IMotionReplayer`
- `IPlayerDataStore`

この構造は維持する。Issue #1の候補技術はこれらの実装差替として扱い、PlayerDataの保存・削除契約やゲーム進行へ直接依存させない。

## 技術比較

| 機能 | AI/学習の要否 | 候補技術 | ライセンス/権利 | 遅延・性能 | Unity統合難度 | 製品採用判断 | フォールバック |
| --- | --- | --- | --- | --- | --- | --- | --- |
| raw Camera / Microphone | 不要 | Unity標準API | Unity製品内で利用 | 既存実装 | 低 | **v0採用済み** | 機器なし/拒否時は明示失敗 |
| Motion再生 | 不要 | 保存Pose/Motion + `IMotionReplayer` | 自前 | 既存実装 | 低 | **v0採用済み** | Mock Motion |
| 人物切り抜き | 追加学習不要 | MediaPipe Image Segmenter / Selfie Segmentation系、または商用確認済みONNX | MediaPipeコードはApache-2.0。**配布する正確なモデルartifactは別途ライセンス確認必須** | MediaPipeはselfie effects / video conferencing向けリアルタイム用途を掲げる。正確な4060実測は未取得 | 中〜高 | **後から追加** | raw Camera / 矩形クロップ |
| Pose | 追加学習不要 | MediaPipe Pose Landmarker等 | MediaPipeコードApache-2.0。モデルartifactは個別確認 | live stream APIあり。入力フレームdropを許容可能 | 中〜高 | **後から追加・優先度高** | Mock Pose / Cameraのみ |
| Face / 表情 | 追加学習不要 | MediaPipe Face Landmarker等 | 同上 | live stream向け。常時必須ではない | 中〜高 | **後から追加** | 顔中心座標だけ / 無効化 |
| Hand | 追加学習不要 | MediaPipe Hand Landmarker等 | 同上 | live stream向け | 中〜高 | **後から追加** | Pose手首点のみ |
| 本人Voice復旧 | 不要 | 保存WAV + stable VoiceID + 生マイク復旧 | 自前 | 生マイク相当 | 低 | **v0の本命** | 保存WAV再生のみ |
| 固定ターゲットVC | 推論。ターゲットモデル作成時は学習あり | RVC | コードMIT。ターゲット音声・学習済みモデルの権利は別。公式READMEはVCTKベース事前学習を説明 | 公式README: end-to-end 170ms、ASIO条件で90ms | 高 | **後から追加** | ピッチ/フォルマントのみ |
| 固定ターゲットVC | 推論。モデル作成時は学習あり | MMVC | MMVC本体はMIT準拠。公式配布音声は各提供者の別規約あり | リアルタイム用。ONNX + DirectML設定例あり | 高 | **PoC候補**。既存音声モデルを同梱するなら権利監査必須 | ピッチ/フォルマントのみ |
| 任意参照音声zero-shot VC | 追加学習不要 | Seed-VC | **GPLv3**。2025-11-21 archived | 公式: algorithm ~300ms + device ~100ms | 高 | **製品依存は不採用**。研究比較のみ | 固定VC / 変換なし |
| 歌唱VC / zero-shot歌声 | 追加学習不要 | SoulX-Singer-SVC | 公式README: code/weights Apache-2.0。用途免責で無断なりすましを禁止する趣旨 | 公式READMEはSVC対応を明記。4060 8GB実測値は未提示 | 非常に高 | **保留 / PoC** | 本人の録音断片、非本人の通常歌唱 |
| 1枚絵/顔の駆動 | 追加学習不要 | LivePortrait | コードMIT。ただし付属InsightFaceモデルは非商用。商用利用時は置換必要 | 公式速度表はRTX 4090で主要モジュール合計十数ms級。4060は別途実測必要 | 非常に高 | **保留**。InsightFace置換なしは不採用 | raw cutout + landmarks replay |
| 軽量ONNX推論 | 推論のみ | ONNX Runtime / WinML | ONNX Runtime MIT | Windows公式は新規Windows開発でWinML推奨。DirectMLはsustained engineering | 中 | **有力候補** | CPU EP |
| Unity内NN推論 | 推論のみ | Unity Sentis (`com.unity.ai.inference`) | Unityパッケージ利用条件に従う | Unity 6000系向けリリースあり。frame slicing / quantization / backend選択あり | 中 | **軽量CVで候補** | CPU / 無効化 |
| Python/PyTorch側car | 推論のみ〜学習 | 別プロセス + IPC | 採用モデルごとに異なる | 起動・容量は重いが、UnityからGPU寿命を分離しやすい | 高 | **重いAIでは推奨構造** | worker未起動時は機能off |

## 各候補の詳細

### 1. MediaPipe

MediaPipe本体はApache-2.0。Pose / Face / Hand / Image Segmenterはいずれもlive stream向けAPIを持ち、非同期処理・フレームdrop前提でリアルタイム映像に使える。

ただし、**コードライセンスと配布するモデルファイルの権利を同一視しない**。新しい `.task` / TFLite artifactをゲームへ同梱する場合、採用する正確なファイルについてモデルカード・配布条件を再確認してから固定すること。

Windows native MediaPipeは公式旧ドキュメント上 experimental とされているため、最初のPoCは次のどちらかを推奨する。

- A: 別プロセスのMediaPipe worker → `IPoseTracker` 等へIPC
- B: 商用配布条件が明確なONNX/TFLiteモデルをSentis/ONNX Runtimeへ載せる

プロトタイプ段階でAを使い、製品版でBへ差し替えられるのが現在の境界設計の利点。

### 2. RVC

RVCコードはMIT。公式READMEはリアルタイムVCでend-to-end 170ms、ASIO環境で90msの例を示す。

ユアパークでの適性は「声の保管庫で数種類の固定ボイスを試す」用途。任意参照音声をその場でzero-shot変換する用途ではない。

採用時の条件:

- ターゲット話者の音源は自前または明示許諾済みに限定
- コミュニティ配布の学習済みモデルを無審査で同梱しない
- RVC本体だけでなく、同梱するcontent encoder / F0 / runtime依存のライセンスも固定バージョン単位で監査
- ゲーム外へ変換モデルや変換音声を一般用途でexportしない

### 3. MMVC

MMVC Trainer READMEはMIT準拠、商用利用・再配布を許容する旨を明示する一方、学習元音声の権利許諾を必須としている。また、ずんだもん等の公式配布音声は音声提供者側の別規約に従う。

MMVC ClientはONNX + `DmlExecutionProvider` の設定例を持つため、Windows固定声のPoCとしては分かりやすい。ただし製品採用時は保守状況と依存バージョンを再確認する。

**「ずんだもんボイスを入れたい」だけでMMVC公式モデルを即同梱してよい、とは判断しない。** キャラクター・音声ライブラリ側の商用ゲーム利用・再配布・表記条件を実装直前に確認する。

### 4. Seed-VC

公式repoは2025-11-21にarchive。licenseはGPLv3。公式READMEでは1〜30秒参照音声のzero-shot VC/SVC、リアルタイムでalgorithm約300ms + device約100msを掲げる。

技術比較としてはユアパークの「拾った声を試着する」に非常に近いが、

- archived
- GPLv3
- Python / model依存が大きい
- 約400ms級の遅延

のため、商用ゲームの中核依存にはしない。別の保守中・permissiveなzero-shot VCが見つかるまでは「任意声試着」は非必須機能として扱う。

### 5. SoulX-Singer / SoulX-Singer-SVC

公式repoはコードとmodel weightsをApache-2.0としている。SoulX-Singer-SVCはraw singing audioから、歌詞/MIDI transcriptionなしでtarget timbreへ変換するzero-shot SVCを掲げる。

ユアパークでは、リアルタイム歌唱ではなく次の用途に限定すると相性がよい。

1. ゲーム側が用意した短いオリジナル歌唱素材
2. プレイヤー本人の参照音声
3. バックグラウンドで変換
4. 完了後にAIコピーが歌う

ただし、RTX 4060 8GB + Unity + OBS同時実行での公式実測は確認できていない。v0採用せず、別プロセス・短時間ロード・処理後unload前提でPoCする。

### 6. LivePortrait

LivePortraitコードはMIT。公式LICENSEが明示的に、InsightFaceコードはMITだが**InsightFaceのモデルはnon-commercial research only**であり、商用利用時にはInsightFace detection modelsをremove/replaceするよう要求している。

したがって標準構成をそのまま商用製品へ入れない。

また、ユアパークでは「以前の本人の動きをコピーが再演する」だけでもホラーが成立するため、LivePortraitを必須にする理由はない。まずcutout + landmark replayで進める。

## Windows / Unity 実装方式

### 推奨: 二層構成

**常時必要な軽量処理**

- Unity側
- Sentis / ONNX Runtime / native plugin候補
- Pose / segmentation等
- 失敗時はその機能だけoff

**重い生成・VC**

- 別プロセスworker
- named pipe / localhost IPC等
- モデルのロード/unloadをworker寿命で管理
- UnityプロセスをAI依存DLLから隔離

現在の `IVoiceConverter` / `IVoiceGenerator` / `IPoseTracker` 等の境界に合う。

### ONNX Runtime / WinML

ONNX RuntimeはMIT。Windows公式は新規Windows開発でWinMLを推奨し、DirectML EPは引き続きサポートされるがsustained engineeringとしている。

そのため、新規のWindows専用推論基盤を作る場合は「DirectML固定」ではなく、

- WinML / ONNX Runtime
- CPU fallback
- 必要ならNVIDIA CUDA

を候補にする。

### Unity Sentis

Unity 6000系で `com.unity.ai.inference` が提供されており、local inference、frame slicing、quantization、backend選択を備える。

軽量Pose/segmentationモデルをONNX化でき、operator互換が取れるならUnity内完結の候補。ただし、RVCやSoulXのような複雑な音声生成スタックを無理にSentisへ移植することは推奨しない。

## RTX 4060 8GB向け性能方針

上流資料だけでは「Unity + OBS + 各AIを同時実行した4060 8GB」の再現可能な数値は得られなかった。したがって以下は**実測前の設計方針**であり、性能保証値ではない。

1. 常時推論はPose / segmentation等の軽量CVだけにする。
2. Face / Handは必要なミニゲーム中だけ有効化する。
3. live stream推論は全フレーム処理を要求せず、結果timestampをPlayerDataへ渡す。15〜30Hz程度から実測する。
4. VCルームでは不要なvisionモデルを停止可能にする。
5. 歌唱・重い生成はゲームプレイ中のリアルタイム必須にせず、処理画面を許容する。
6. heavy workerは処理後にmodel unloadまたはprocess終了できる構造にする。
7. OOM / worker failure時にゲーム本編を落とさない。
8. 実測時はUnity scene / OBS配信 / 1080p camera / AI workerを同時起動し、peak VRAM、GPU utilization、game frame time、audio latency、dropped inference framesを記録する。

## v0 / 後回し / 保留 / 不採用

### v0で絶対必要

- 現在のraw Camera / Microphone
- PlayerData manifest / stable ID / lifecycle
- 本人VoiceSample
- raw ImageSample
- Mock PoseとMotion replay
- AI交換境界
- PlayerData削除とRAM寿命ルール

### 後から追加

- 実Pose tracker
- 人物segmentation
- Face / Hand tracker
- 固定ターゲットVC（RVC/MMVC等、権利監査後）
- 実写 / Live2D / 3Dアバター入力の共通化

### 技術的に危険なので保留

- SoulX-Singer-SVC
- LivePortrait（InsightFaceモデル置換後のみ再評価）
- zero-shot任意声試着
- ユーザー本人の声で任意歌唱

### 現時点で採用しない

- Seed-VCを製品依存にすること
- InsightFaceの非商用pretrained modelを商用版へ同梱すること
- ユーザー個別のfine-tuningをv0へ入れること
- 第三者の顔/声を任意importして使える一般deepfake機能
- PlayerDataのクラウド送信

## 次の検証

1. 実機Camera/Microphone UI通し確認（Issue #2側）
2. Pose worker PoC: MediaPipeを別プロセスで動かし、`IPoseTracker`へ正規化ランドマークを渡す
3. 正確なMediaPipe model artifactを1つ固定し、そのmodel card / redistribution条件を確認
4. RVCまたはMMVCで、完全に権利を持つテスト音声1名だけ使い、voice roomの遅延を測定
5. SoulX-Singer-SVCは4060 8GBで単独→Unity同時→OBS同時の順にベンチし、無理なら不採用

## 一次情報

- MediaPipe repository / Apache-2.0: https://github.com/google-ai-edge/mediapipe
- MediaPipe Pose Landmarker API: https://ai.google.dev/edge/api/mediapipe/python/mp/tasks/vision/PoseLandmarker
- MediaPipe Face Landmarker API: https://ai.google.dev/edge/api/mediapipe/python/mp/tasks/vision/FaceLandmarker
- MediaPipe Hand Landmarker API: https://ai.google.dev/edge/api/mediapipe/python/mp/tasks/vision/HandLandmarker
- MediaPipe Image Segmenter API: https://ai.google.dev/edge/api/mediapipe/python/mp/tasks/vision/ImageSegmenter
- MediaPipe Selfie Segmentation docs: https://github.com/google-ai-edge/mediapipe/blob/master/docs/solutions/selfie_segmentation.md
- RVC license: https://github.com/RVC-Project/Retrieval-based-Voice-Conversion/blob/develop/LICENSE
- RVC realtime README: https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI/blob/main/docs/en/README.en.md
- MMVC Trainer README / terms: https://github.com/isletennos/MMVC_Trainer/blob/main/README.md
- MMVC Client: https://github.com/isletennos/MMVC_Client
- Seed-VC repo: https://github.com/Plachtaa/seed-vc
- Seed-VC GPLv3: https://github.com/Plachtaa/seed-vc/blob/main/LICENSE
- SoulX-Singer: https://github.com/Soul-AILab/SoulX-Singer
- LivePortrait license: https://github.com/KlingAIResearch/LivePortrait/blob/main/LICENSE
- InsightFace licensing note: https://github.com/deepinsight/insightface/blob/master/README.md
- ONNX Runtime license/readme: https://github.com/microsoft/onnxruntime
- ONNX Runtime Windows: https://onnxruntime.ai/docs/get-started/with-windows.html
- ONNX Runtime DirectML EP: https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html
- Unity Sentis / AI Inference: https://docs.unity3d.com/current/Manual/com.unity.ai.inference.html

## 注意

この文書は技術・ライセンスの一次調査であり法的助言ではない。製品出荷時には、実際に固定したcommit / package version / model artifact / voice assetごとにNOTICEと配布条件を再監査すること。