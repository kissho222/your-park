# ユアパーク Voice Conversion 候補比較と評価仕様

調査日: 2026-10-27。対象: [Issue #4](https://github.com/kissho222/your-park/issues/4)。接続先: [#3 Chatterbox PoC](https://github.com/kissho222/your-park/issues/3)、[#5 sidecar v0](https://github.com/kissho222/your-park/issues/5)。

**結論は ChatterboxVC を最初に実機で試し、OpenVoice V2 を比較し、必要なら CosyVoice の VC 経路を追加すること。製品採用決定ではない。** 短い本人参照音声から固定台詞を変換する要件では、追加話者学習が必要な RVC / MMVC は別枠となる。Seed-VC は研究比較に適するが、archive と GPL 配布義務を未解決のままゲームに同梱しない。

本資料は一次情報による調査と、同じ条件で比較するための評価仕様である。インストール、モデル実行、実音声収集、第三者音声の取得、Unity変更は行っていない。**RTX 4060 8GBでの速度・VRAM・日本語の品質・UnityとOBSの共存は、全候補で未実測。** 以下の「設計判断」「初期予算」「推奨する試験」は公称仕様や実測結果と区別する。

## 1 最初に実機で試す候補

| 順位 | 候補と試す範囲 | 選ぶ理由 | 最初の確認と停止条件 |
|---|---|---|---|
| 1 | ChatterboxVC の公式 audio-to-audio API | #3/#5と接続しやすく、targetの追加学習不要。コードと公式公開モデルにMIT表示 | 10秒参照と通常／長い間／不穏の3台詞から。内容が壊れる、本人らしさが成立しない、8GB単独でOOMなら設定と版を記録して別候補へ。[CB API][CB model] |
| 2 | OpenVoice V2 の ToneColorConverter | 追加学習不要の別方式。V1/V2のMIT・商用可が公式に明示される | 収録source自身のspeaker embeddingを抽出する。標準VADの短参照失敗と、モデルの音質を分離。既定TTS話者用src_seを任意sourceへ流用しない。[OV API][OV README] |
| 3 条件付き | CosyVoice-300M の `inference_vc` | 公式300MのVC使用例と2026年のコード更新履歴がある追加候補。300MモデルにApache-2.0表示 | Windows導入、不要モデル常駐、全資産の条件を先に確認。上位2候補で目的を満たせれば追加しない。[CV example][CV model][CV history] |

CosyVoice-300MとVC入口の対応は確認済み。公式exampleは300MをAutoModelで読み、同じインスタンスで `inference_vc` を呼ぶ。300M配布の `cosyvoice.yaml` も確認した。ただしWindows成功、8GB適合、日本語VC品質の確認とは別である。[CV example][CV API][CV config]

**Seed-VC は品質の研究対照候補であり、上の製品候補3位とは別。** GPLは商用禁止ではないが、ソース提供等の配布義務やUnity製品との構成を検討する必要がある。別プロセス化だけで義務がなくなるとは扱わない。[SD LICENSE][SD model]

短い参照に関して特に重要な制約は、ChatterboxVCの先頭10秒切り詰めと、OpenVoice標準VAD経路で2秒や実発話がちょうど5秒の入力が失敗し得ること。2/5/10/20–30秒を単に同じファイル長へ揃えるだけでは公平にならない。詳細は評価仕様のtarget参照セットに記す。

## 2 候補の機能と実行条件

### 2.1 音声入力と話者学習

| 候補 | audio-to-audio VC | target追加学習 | zero-shotと参照音声 | 日本語について確認できた範囲 |
|---|---|---|---|---|
| ChatterboxVC | 独立した `generate(audio, target_voice_path)` | 不要 | 参照特徴を抽出する | Multilingual TTSには日本語があるがVCは別入口／重み。**日本語VCの公式定量評価は未確認**。[CB API][CB README] |
| OpenVoice V1 | ToneColorConverterが音声を入力 | 不要 | zero-shot。source/target両方のembeddingを使う | cross-lingual設計。同梱base TTSは主に英中。日本語録音VCの品質保証とは別。[OV API][OV paper] |
| OpenVoice V2 | V2 checkpoint/configでconverterを使用 | 不要 | V1と同様 | 全体として日本語native supportを明記。任意の日本語演技sourceの品質保証ではない。[OV README][OV model] |
| RVC | 学習済み話者へ入力音声を変換 | 通常必要 | 標準経路は短いreferenceを都度渡すzero-shotではない | 日本語READMEがあるが、言語別品質保証・定量評価とは別。[RVC README][RVC JP][RVC training] |
| MMVC | Trainerの話者モデルをClientで使用 | 必要 | 学習済みspeaker IDを使う。標準Clientはzero-shotではない | 日本語資料・公式日本語音声ライブラリの運用あり。現行品質の定量保証は未確認。[MM Client][MM Trainer] |
| Seed-VC | source/target音声を入力 | zero-shotでは不要。任意fine-tuningあり | 1–30秒参照を公称 | 日本語READMEだけで品質保証としない。固定日本語台詞の公称品質は未確認。[SD README] |
| CosyVoice VC | 公式300M使用例で `inference_vc(source_wav, prompt_wav)` | 参照VCでは不要 | prompt音声を使う | 同じ300M使用例に日本語cross-lingual TTSの案内があるが、日本語VCの品質・演技保持の保証ではない。HF cardの3.0向け言語数や速度を300Mへ転用しない。[CV example][CV API][CV model] |

### 2.2 source演技の保持と参照長

| 候補 | sourceのprosody・感情・間を保つ設計か | 公式の参照長と今回の扱い |
|---|---|---|
| ChatterboxVC | sourceをトークン化しtarget条件で再合成。完全保存の保証はない | VC専用の最小／推奨値は未確認。確認したAPIは先頭10秒まで。2/5/10秒を比較し、20/30秒は実効10秒以下。[CB API fixed] |
| OpenVoice V1 | 声色とstyleを分離する設計。targetの感情をそのままコピーする方式ではない | 最適秒数は未確定。標準VADの分割数計算が短参照を拒否し得る。10秒を試験基準とするのは本資料の提案。[OV paper][OV QA][OV VAD fixed] |
| OpenVoice V2 | V1の方針を継承し音質向上を表明。日本語の笑い／間は実測する | 2/5秒はストレス条件。20/30秒は実際の有声音と抽出経路を記録して追加。[OV README][OV VAD fixed] |
| RVC | 入力特徴・F0を使う。囁き・笑い・叫び・無音の保持保証はない | reference長2/5/10秒はN/A。公式は低ノイズ学習音声10分以上を推奨。学習素材量と学習時間を別計上。[RVC README] |
| MMVC | リアルタイムVCを意図するが演技・間の完全保持保証はない | reference長2/5/10秒はN/A。今回、学習素材の信頼できる公式最低量は未確認。[MM Client][MM Trainer] |
| Seed-VC | length-adjust/F0/style設定が結果に影響。V2比較ではsource演技保持用に `convert-style=false` を基本条件とする | 公称1–30秒の範囲で2/5/10/20/30秒を試験可能。品質同等の保証ではない。V1/V2を別候補IDにする。[SD README] |
| CosyVoice VC | source音声をトークン化する経路。感情・無音保持は未測定 | VC専用の推奨秒数は未確認。2/5/10秒は探索条件で、公式推奨とは呼ばない。世代別上限確認後に長参照を追加。[CV API] |

### 2.3 速度とメモリ

| 候補 | 公称速度またはRTFとその条件 | VRAM／RAM | WindowsとRTX 4060 8GBの判断 |
|---|---|---|---|
| ChatterboxVC | VC専用のハードウェア付き公称値は未確認。Turbo/Nano TTSやAPIの遅延値を転用しない | 公式VC最低値／peakとも未確認。checkpointサイズはVRAM量ではない | CUDA/CPU経路あり。公式開発試験はDebian11/Python3.11。Windows・8GBでの成功は実機確認待ち。[CB API][CB README] |
| OpenVoice V1 | 論文は少し最適化したbase TTS＋converterをA10Gで約12倍実時間／1秒音声85msと記載。公開VC単体や4060の値ではない | 信頼できる定量要件未確認 | 公式手順はLinux中心、Windowsはコミュニティ手順への案内。再現性試験が必要。[OV paper][OV usage] |
| OpenVoice V2 | V2 converter単体の同条件数値は未確認。V1論文値をV2実測としない | 同上 | Python3.9と固定された旧依存の整合を先に確認。8GB適合未測定。[OV usage][OV deps] |
| RVC | READMEの約170ms、ASIO約90msはリアルタイムのend-to-end表示。機器依存でoffline RTFではない | 固定値未確認。モデル/index/F0方式/精度/長さで変わる | Windows手順あり。現行mainと旧版の依存を混在させない。本人ごとの学習が今回の主要障壁。[RVC README] |
| MMVC | 比較可能な公称RTF・4060値は未確認 | 学習・推論とも固定要件未確認 | Windows Client／ONNX経路あり。古い依存の現行Windowsでの再現性は未確認。[MM Client][MM deps] |
| Seed-VC | realtime系のalgorithm約300ms＋device約100msという表示。offlineモデルのRTFではない | 固定値未確認。25M等の主モデルパラメータだけで総量を推定不可 | Windows/Python3.10推奨。V1 offline・短いsource・単一モデルから試す設計。8GB適合未測定。[SD README][SD inference] |
| CosyVoice VC | VC関数にRTFログあり、比較可能な測定値未確認。TTSの150msをVC値にしない | VC固有値未確認。loaderのLLM/flow/HiFT等常駐に注意 | Linux系中心。native Windowsと8GBの再現性を先に確認する条件付き候補。[CV API] |

### 2.4 依存とoffline動作とUnity OBS同居

| 候補 | 主依存とONNX | offline動作 | Unity＋OBSで確認する懸念 |
|---|---|---|---|
| ChatterboxVC | Python>=3.10、PyTorch/torchaudio、librosa、s3tokenizer、safetensors、Perth。公式VCの完全ONNX経路は未確認 | `from_local`あり。`from_pretrained`はHF取得。全資産事前配置後の無通信起動試験が必要 | GPU中間テンソルとUnity/OBSが競合。CPU watermarkとI/Oも待ち時間へ含める。[CB API][CB deps][Perth implementation] |
| OpenVoice V1/V2 | Python>=3.9、PyTorch、wavmark、whisper系/VAD等。V2のTTSデモはMeloTTS追加。公式本体はPyTorch | ローカルcheckpoint読込可。wavmark/VAD/ASR等の初回取得を別に封じる | 固定録音VCに不要なTTS/ASRを常駐させない。参照抽出と変換のピークを分離。[OV API][OV VAD][OV deps][WavMark API] |
| RVC | 現行mainはPython3.12 x64、Torch2.7.1のCUDA別経路、FAISS、音声解析等。主経路PyTorch、変換形式は版依存 | 資産を事前配置すればローカル推論する構成。完全無通信起動は未検証 | CUDA allocator、indexのRAM、F0処理、音声I/O。リアルタイム公称値から同居成功を推定しない。[RVC README] |
| MMVC | TrainerはTorch1.13.1+cu116、numpy1.23.5、librosa0.8、ONNX1.14/ORT1.15等。Clientはモデル形式で異なる | モデル/configによるローカルClient。完全無通信は未検証 | 古いCUDA/ORT DLL、音声デバイス、GPU競合。学習工程をゲーム中へ持ち込まない。[MM Client][MM deps] |
| Seed-VC | Python3.10、PyTorch、Transformers、HF、Gradio等。requirementsにnightly cu126指定。再現用lockが必要 | 主重み以外にもencoder/vocoder/CAMPPlus等を初回取得。全て配置後に無通信試験 | 複数モデル／AR cache／初回処理のピーク。V1/V2を同時loadしない。[SD deps][SD inference] |
| CosyVoice VC | Python/PyTorch、ONNX Runtime、ModelScope、HyperPyYAML等。TRT/vLLMは初回比較から外す | model_dir不在時にsnapshot_download。完全なローカル資産と無通信試験が必要 | 不要なLLM常駐、CUDA/ORT/TRT、初回構築、OBSとの競合。[CV API] |

「offline可」は必要な資産を揃えたローカル構成があるという意味であり、初回ダウンロードも不要という意味ではない。未確認のRAM量をRTX 4060のVRAMから逆算しない。専用venvと固定依存を使い、既存の画像生成等のPython環境を変更しない。

### 2.5 保守状況と採用判断

| 候補 | 調査時のarchiveと更新根拠 | 製品採用判断 |
|---|---|---|
| ChatterboxVC | archive表示なし。閲覧した履歴で2026-07-21 `5de7a54`を確認 | 第一PoC候補。版固定・付属品監査・実機評価後に判断。[CB history] |
| OpenVoice V1/V2 | archive表示なし。閲覧した履歴先頭は2025-04-19 `74a1d14`。2026年に活発保守中とは断定しない | V2を第二候補、V1は回帰比較に必要な時だけ。[OV history] |
| RVC | archive表示なし。2026-08-04 `81eed5e`の更新を確認 | 許諾済み事前学習話者の将来比較枠。短時間マイクテストからの本人化には不適合。[RVC history] |
| MMVC | 非archive表示。閲覧できた履歴はTrainer 2024-02-28 `5f6b7b0`、Client 2023-04-15 `449138c`。最新性を保証しない | 2026保守中とは断定せず、今回の短参照用途では優先しない。[MM Trainer history][MM Client history] |
| Seed-VC | 公式repoは2025-11-21 archive | 研究対照のみ。GPL配布構成と自前保守責任を解決するまで製品同梱保留。[SD repo] |
| CosyVoice | 現公式はQwenAudio/CosyVoice。2026-05-25 `074ca6d`等の更新を確認 | 第三の条件付き予備候補。非archiveだけを根拠に製品成熟度を判断しない。[CV history] |

更新日は「閲覧した履歴で確認できた日」であり、全リンク・全重みが同一commitであることや、今後の保守保証を意味しない。

## 3 ライセンス監査と再配布ゲート

### 3.1 本体コードとモデル重みを分ける

| 候補 | コード | 公開モデル重み | 商用ゲームへの再配布判断 |
|---|---|---|---|
| ChatterboxVC | MIT [CB LICENSE] | 公式 `ResembleAI/chatterbox` cardはMIT [CB model] | 通知保持を含む各条件の下で候補。S3Gen等の由来とPerthを含む完成配布物を監査するまで最終承認しない |
| OpenVoice V1 | MIT相当の許諾文 [OV LICENSE] | 公式READMEはV1/V2をMIT・商用可と明記 [OV README] | コード、checkpoint zip、WavMark、前処理依存を別々に固定して判断 |
| OpenVoice V2 | 同上 [OV LICENSE] | 公式READMEに加え公式HF cardもMIT [OV model] | V1の許可だけを根拠にせず、V2実ファイルと依存を固定する |
| RVC | MIT [RVC LICENSE] | `lj1995/VoiceConversionWebUI`はMITタグ。card本文は空 [RVC model] | 任意の話者pth/indexや外部重みへMITを自動適用しない。本人モデルのデータ権利と各部品の条件が必要 |
| MMVC | Trainer/ClientともMIT本文 [MM Trainer LICENSE][MM Client LICENSE] | 全配布重みがMITとは確認できない。自前学習／公式音源／第三者音源を分ける | 音声ライブラリ別の条件と権利者許諾が必要。公式キャラクター音源を自由再配布可能とは扱わない [MM Trainer] |
| Seed-VC | GPLv3 [SD LICENSE] | 公式 `Plachta/Seed-VC`はGPL-3.0タグ、card本文は空 [SD model] | 商用禁止ではないが配布義務と依存整合の検討が必要。archiveを含め現段階は保留 |
| CosyVoice VC | Apache-2.0 [CV LICENSE] | `FunAudioLLM/CosyVoice-300M`はApache-2.0表示。他世代へ一律適用しない [CV model] | 選択したモデル世代・実ファイル・NOTICEを監査後に判断 |

### 3.2 encoder vocoder watermark等の追加資産

| 構成 | 別に確認する部品 | 確認できた条件と残課題 |
|---|---|---|
| Chatterbox | S3GenのCosyVoice由来コード、S3Tokenizer | S3GenにApache-2.0ヘッダ、S3TokenizerはApache-2.0。root MITだけのNOTICEにしない。[CB S3Gen][S3Tokenizer] |
| Chatterbox | CAMPPlus speaker encoder、HiFTGenerator等 | S3Genの構成を確認。コード由来・各ヘッダ・追加checkpointを固定版manifestで照合。「全部MIT」と断定しない。[CB S3Gen] |
| Chatterbox | Perth watermark／detectorコードと同梱重み | PerthコードMIT。重みは公式repoのpretrained配下から読む。独立した重み専用cardは今回未確認。配布wheelの実ファイルとlicense収録を確認する。[Perth LICENSE][Perth weights][Perth implementation] |
| OpenVoice | WavMarkコード／watermark重み | コードMITだがcopyright欄はplaceholder。元文を勝手に補わない。公式呼出先 `M4869/WavMark` cardはMIT。実checkpointとrevisionを固定。[WavMark LICENSE][WavMark model][WavMark API] |
| OpenVoice | whisper/faster-whisper、Silero VAD、pydub、任意MeloTTS等 | 実際に同梱する版・重みの個別監査は未完。不要なTTS/ASR経路を省く場合も動作確認が必要。[OV VAD][OV deps][OV usage] |
| RVC | HuBERT/ContentVec、VITS/HiFiGAN由来、RMVPE、FAISS、FFmpeg、任意分離モデル | 公式に関連license案内があるが、選ぶ重みとFFmpegビルドの条件まで確定する必要。歌声分離等は今回不要。[RVC component licenses] |
| MMVC | 学習データ、話者モデル、音声ライブラリ、ONNX/ORT等 | 音源別条件・キャラクター帰属・本人同意を本体MITと分離。選ぶClient/Trainer版の依存監査を行う。[MM Trainer][MM deps] |
| Seed V1 offline | Whisper-small、BigVGAN、CAMPPlus | 配布cardは順にApache-2.0／MIT／Apache-2.0。コード版・モデルrevision・ファイル名の固定が必要。下記リンクの表示だけで全配布物を承認しない。[Whisper model][BigVGAN model][CAMPPlus model][SD inference] |
| Seedの別構成 | HiFT、RMVPE、tiny XLSR、V2 ASTRAL/tokenizer等 | HiFTのCosyVoice-300M cardはApache-2.0、RMVPEのRVC配布にはMIT表示。tiny/V2資産はファイル単位の条件未確定。V1監査を流用しない。[CV model][RVC model][SD inference] |
| CosyVoice | campplus.onnx、speech_tokenizer各世代、flow/HiFT/LLM等 | loaderが使う資産を特定し、由来・NOTICE・世代整合を確認。300M cardの表示を外部資産すべてに転用しない。[CV API][CV model] |

### 3.3 配布前に閉じる項目

- コードcommit、モデルrepository/revision、実ファイルSHA256、依存lock、ライセンス原文、copyright/NOTICE、変更表示、同梱の要否を**ファイル単位のmanifest**へ記録する。
- 「MIT/Apacheタグがある」と「ゲーム配布物の監査が完了」は別。重みのcardが空、部品の条件が不明、取得先が第三者という項目は未解決として残す。
- 実行に必要な部品だけを配布する。不要なTTS、ASR、歌唱分離、WebUI、サンプル音声を一括同梱しない。
- モデルの許諾は第三者の声を使う許諾ではない。本人の録音・変換・評価共有・保存範囲の同意を別に確認する。
- 解決できない部品が残る場合は「研究評価のみ／製品採用保留」。本資料は法的保証ではなく、実物と配布構成を確認するための技術監査資料である。

## 4 Chatterbox watermarkと固定台詞化

ChatterboxVCは生成波形にPerthを適用して返す。watermarkはファイルタグではなく波形処理であり、通常のWAV再生のためにUnity側で専用decoderが必要な形式ではない。ただし**Unityで再生できることと、加工後に透かしが検出できることは別**。Perthの圧縮・リサンプリングへの頑健性の説明は、特定のUnity圧縮、音量、EQ、pitch、reverb、空間化、ミキシング、OBS録画での保証ではない。[CB API][Perth README]

保持したまま使う方針で、次を順に評価する。これは未実行の試験手順であり、意図的除去はしない。

1. watermark済み原WAVを正本とし、検出器版・設定・スコアと聴感を記録する。元source等の通常音声を負例にする。
2. UnityのPCM再生をループバック収録したものと、実際の配布圧縮設定で再生したものを比較する。
3. 本編の音量・空間化・エフェクト・mixを一段ずつ適用し、最後にOBS録画からの音声も確認する。
4. 検出スコア低下を「透かしが元からなかった」と解釈しない。短い出力／無音、検出限界、加工条件を分離する。閾値を結果に合わせて都合よく変えない。
5. 生成時に透かしを付けることと、検出器を毎回ゲームに同梱することは別判断。採用版のコード・重み条件をmanifestへ残す。

Perthの確認した実装は標準CPUロード。GPU推論だけでなくwatermarkのCPU時間もend-to-end待ち時間に含める。[Perth implementation]

固定台詞VCなら、制作済みsourceを使用し、TTS、任意文生成、音声認識、歌唱、リアルタイムstreaming、仮想マイク、OBS音声routingをv0から外せる。target conditioningは必要な範囲で再利用し、batch1の直列生成、場面前の先読み、不要時のworker解放を評価する。これはメモリ節約を保証するものではなく、実測する構成案である。参照更新・取消・削除時には、個人由来embeddingや生成cacheをPlayerDataの寿命契約に従わせる。

## 5 何を確かめるか

source.wav の**言葉・演技・時間構造**を保ち、target.wav の声色へ変換する audio-to-audio VC を比較する。TTS、リアルタイム変換、仮想マイク、歌唱、任意文生成は対象外。最初の成功は「自分の声に感じるのに、自分が言っていない台詞」であり、完全な声紋再現や通常会話品質を要求しない。

ただし、劣化をホラーに使えることと、内容誤変換・意図しない無音・不安定動作を許容することは別。ゲーム台詞として意味が変わる場合は、声が似ていても不合格とする。

判定は三段階に分離する:
1. **構造試験**: Mock で要求・応答・取消・削除・終了が成立するか。音質の根拠にはしない。
2. **VC 実推論試験**: 権利確認済み音声を用い、内容と声色、速度、メモリを実測する。
3. **ゲーム内試験**: Unity と OBS の共存、読み込み待ち、失敗時の演出切替を確認する。

## 6 入力素材・同意・保管

- source は所有者自身、または本評価とゲーム内利用について明示許諾を得た成人の演技音声。target は試験参加者本人の同意済み音声のみ。
- 本仕様を渡しただけでは録音許可を得たことにならない。収録開始は本人操作とし、参照声で未発話の台詞を生成すること、保存期間、削除と再演の範囲を説明する。
- 芸能人・配信者・他ゲーム等の声を収集しない。公開されているだけの音声は許諾済みと扱わない。
- マスターは非圧縮 PCM WAV とし、各モデルへの変換は必要なサンプルレート・チャンネル数に限定する。モデルごとの入力仕様と変換内容を記録する。
- 本人音声、生成音声、話者埋め込み、個人別評価票、ローカル絶対パスを公開リポジトリへコミットしない。公開するのは台本・スキーマ・匿名化した集計・依存識別情報まで。
- 別目的の一般学習・外部送信には使わない。モデル初回取得時の通信と音声処理時の通信を分け、offline 試験では事前取得完了後にモデル固有の追加取得がないか確認する。
- 開発用音声も専用の同意済みテスト領域に隔離する。通常の PlayerData や StoryProgress に試験用削除を実行しない。

## 7 オリジナル source 台詞セット

以下は本評価用に作成したオリジナル文。括弧内は演技指示で、読み上げない。秒数は固定台詞を無理に伸縮して作らず、実際に録音して測定する。囁き・大声・息の演技は無理のない強さとし、痛み・過呼吸・絶叫を要求しない。

| ID | 狙い | 台詞 | 演技・確認点 |
|---|---|---|---|
| JP01 | 普通の案内 | 「ようこそ、ユアパークへ。準備ができたら、青い扉の前でお待ちください。」 | 平常の案内声。語尾と固有名詞を確認。 |
| JP02 | 明るいテンション | 「やった、見つけた！　次はあなたの番です。一緒に最後まで進みましょう！」 | 明るい立ち上がりと抑揚。 |
| JP03 | 囁き | 「ここだけの話です。あの扉は、まだ開けないでくださいね。」 | 小声だが聞き取れる範囲。子音と息の残り方。 |
| JP04 | 怒り | 「待ってください。それは私のものです。勝手に持っていかないで！」 | 低めの怒りから強めの語尾へ。意味を変える欠落を厳しく確認。 |
| JP05 | 笑いを含む | 「ふふっ、そう来ましたか。あはは、それなら私にも考えがあります。」 | 笑いを言葉として読む版ではなく、自然な短い笑い。 |
| JP06 | 息を含む | 「はあ……間に合いました。少し休んでから、続きを始めましょう。」 | 軽い呼気を一回。呼吸を過剰に繰り返さない。 |
| JP07 | 早口 | 「赤い箱を右、青い箱を左、黄色い鍵は真ん中の引き出しです。」 | 普通版と無理のない早口版を両方録り、別 take とする。 |
| JP08 | 長い間 | 「あなたの声は……[約2秒の沈黙]……確かに、ここに残っています。」 | 同じ間の位置と長さが保たれるか。沈黙を自動除去しない。 |
| JP09 | 大きな呼びかけ | 「そっちではありません！　止まって！　出口は、こちらです！」 | 安全な呼びかけ音量。入力クリッピングを避ける。 |
| JP10 | 本編想定の不穏さ | 「返してほしいのですか？　でも、今ここで話しているのは、本当にあなたでしょうか。」 | 穏やかな前半と不穏な後半。演技をやり直してモデル別に変えない。 |

### 5 / 15 / 30 秒の性能用セット

- 音質用10本と性能用クリップ長を分離する。上記の音質用 take は自然な長さのまま保存。
- 性能用は JP01 等を素材に、同じ声・同じ録音条件で約5秒 / 15秒 / 30秒の連続発話を別収録する。文末で切り、必要なら台詞を順に繋ぐ。単に無音を足して30秒にしない。
- 実長は小数秒で記録。内容と無音率を揃えられない差は比較の限界に書く。source_duration と speech_duration の双方を保持する。
- 原音の意図した語、笑い区間、間の開始・終了を手作業で印づける。字幕・台本の自動認識結果を正解として代用しない。

## 8 target 参照セット

普通の声で、例えば次のオリジナル文を自然に収録する:

> 「今日は公園をゆっくり歩きます。赤い花と白い雲を見つけました。入口の時計は十時を指しています。少し休んだら、友達とパンを分けて食べます。帰り道では、川の近くで風の音を聞きました。」

- 2秒、5秒、10秒を基本条件とする。モデルが実際に扱える場合のみ20秒 / 30秒を追加する。
- 初回は同じ長い take の先頭から切り出す nested 条件とし、各候補に同じバイト列を渡す。切断する音節は避け、厳密な整数秒でなければ実長を記録する。
- これで比較するのは「参照長だけの純粋な影響」ではない。長くすると音素の種類・文の情報量も増える。短参照の安定性を再確認する場合は、別の開始位置や別文でも2秒条件を繰り返す。
- 引き伸ばし・反復・無音追加で推奨長を満たしたことにしない。VAD を使う場合は、前後の長さ、残った有声長、区間数を記録する。
- model/backend が内部で参照を切り捨てた場合、指定長と**実際に利用された長さ**を区別する。20秒入力を渡しても内部が先頭10秒のみなら「20秒を学習・利用」と報告しない。
- 公式の標準前処理が短参照を拒否した条件は `preprocess_failed`、明記された非対応条件は `unsupported_condition`。出力がないものに音質1点を付けて平均へ混ぜない。
- 短参照を通すために別の公式 API や独自前処理へ切り替えた場合は、別 protocol / configuration とする。標準経路と同一試験として隠さない。
- 2 / 5秒は「全モデルが保証する要件」ではなく、ゲーム内の短いマイクテストで成立するかを調べる試験点。

### 現時点で必要な候補別の例外

以下は2026-10-27に確認した公式コードの固定revisionに基づく注意点。実行前に実際の採用commitでも再確認する。参照長の推奨値や音質保証とは別。

- **ChatterboxVC**: [公式 vc.py の set_target_voice](https://github.com/resemble-ai/chatterbox/blob/5de7a54/src/chatterbox/vc.py) は DEC_COND_LEN を10秒相当として読み込んだ参照を切り出す。この経路では20–30秒ファイルを渡しても先頭10秒しか使わないため、長参照で音質改善した条件として扱わない。2/5/10秒は実入力と実消費長を記録して比較する。
- **OpenVoice 標準VAD経路**: [公式 se_extractor.py の split_audio_vad](https://github.com/myshell-ai/OpenVoice/blob/74a1d147b17a8c3092dd5430504bd83ef6c7eb23/openvoice/se_extractor.py) はVAD後の発話長を10.0で割り、np.roundから区間数を計算し、正の区間数を要求する。特に実発話2秒やちょうど5秒では区間数0となり失敗する。ファイル自体が10秒でも無音を除いた発話長が短ければ同じ問題になり得る。これは標準前処理の失敗であり、出力の音質評価ではない。
- OpenVoiceで直接 ToneColorConverter.extract_se を使う等の別経路を比較するなら、`reference_preprocess=direct_extract_se` 等で別条件にする。その変更がモデル本来の推奨使用法かも別途確認し、標準経路の合格を装わない。

### 短参照と声域差の劣化仮説

短い参照では音素の種類や安定した有声音が少なくなり、本人らしさの低下、source声の残留、雑音の影響が大きくなる可能性がある。声域差が大きい場合は、sourceの音高・演技を守ることとtargetらしい声域に寄せることが競合し得る。**いずれも今回のモデル別実測結果ではなく、検証する仮説**であり、劣化順位は未確定。

有声音F0の中央値・範囲は参考値として記録し、性別ラベルで代用しない。囁きなどF0が安定しない台詞はF0値で足切りしない。RVCのpitch shiftやSeedのauto-f0-adjustを試す場合は、無補正の演技保持条件と別protocolにし、設定値と副作用を残す。

### 話者の組合せ

まず成人の同意済み source 演者1人と target1人で構造と音質を確認。その後、必要な場合に限って同意済みの複数人へ広げる。

- 本人→本人: source 内容を保持できるかの対照。
- source と target の声域が近い組合せ。
- source が低め、target が高めの組合せと逆向き。
- 同じ参加者の自然な低め／高めの演技は追加条件にできるが、別話者の代わりにはしない。

性別ラベルだけで声域や声質を決めつけない。性別は任意の自己申告で、比較に不要なら収集しない。普段の話声の音高範囲、声の強さ、息・張り等の条件を区別し、大きな声域差を「男女差」と同義にしない。モデルの pitch shift を使う場合は無補正版と分け、値を記録する。

## 9 段階的な実施順序

| 段階 | 実施 | 進行条件 |
|---|---|---|
| A: ライセンス・起動 | 固定 code revision / weight revision / 依存一覧、Mock ライフサイクル、offline 構成確認 | コード・重み・付属品の利用条件と未解決点が分離されている |
| B: 最小音質 | JP01 / JP08 / JP10 × 5秒・10秒参照、候補1個から | 同意済み音声、変換が終了し、出力を安全に再生可能 |
| C: 短参照 | 同じ3台詞 × 2秒参照。対応時のみ20–30秒 | 前処理失敗と出力品質低下を区別 |
| D: 本評価 | 生き残った候補1～3個に全10台詞、参照長比較 | 明らかな内容破壊・声色不成立がない |
| E: 声域・反復 | 少数の追加同意話者、別参照take、再試行 | 1組だけの結果を一般化しない |
| F: 実装共存 | 5/15/30秒 × 単独 / Unity / Unity+OBS | 単独でメモリ不足・破綻がない |

モデルが個別学習を必須とする場合、zero-shot の参照秒数表に無理に入れない。別トラックとして学習素材量・学習時間・保存物・再配布・削除条件を計上し、短時間の初回マイクテストに適合するかを評価する。

## 10 公平な実行条件

- 同じ source / target、同じハードウェア、同じ音量調整方針、同じ取得日を使う。モデルごとに有利な演技 take を選び直さない。
- 一次比較は公式推奨の既定設定。品質調整後は別条件とし、試した全設定と採用理由を残す。
- 初回ロードと warm 推論を分離。cold は sidecar 新規起動ごとに測定するが、OS キャッシュまで消した「完全cold」とは主張しない。プロセスcoldと表記する。
- 各候補の代表条件で process-cold を3回、warm-up を1回除外して同条件を最低5回測定する。warm5回のp95は安定した尾部推定ではないため、中央値・範囲を主に報告。p95を判断に使う条件は20回以上へ拡張し、反復数も併記する。
- 音質の試聴比較は匿名化した候補名をランダム順にし、source原音とtarget参照を先に確認できるようにする。1人の主観と複数人評価を分ける。
- 共通試聴コピーのみ同じ規則でラウドネスを揃え、モデル出力原本は保持。ノイズ除去・EQ・time stretch 等で比較を補修しない。必要な場合は別のゲーム演出版条件として記録する。
- seed が設定できる場合は固定値と少数の別seedを記録。seed固定でも環境を跨ぐ完全再現性を保証しない。
- 音声 watermark の除去を目的とする加工は行わない。検出試験をするなら公式手段と許諾範囲で別途設計し、「聞こえない」ことを「検出不能」と取り違えない。

## 11 1～5点の主観評価票

すべて5点ほど良い。中央値だけでなく最低点、評価者数、聞き直し回数、自由記述を残す。モデル比較を1つの加重総合点だけで決めず、内容保持などの必須ゲートを先に適用する。

| 軸 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| 内容の聞き取り intelligibility | 大半を理解できない／意味が変わる | 複数箇所が不明・欠落 | 一部聞き直せば分かる | ほぼ一度で分かる | 全文を自然に理解できる |
| 本人らしさ target similarity | targetと感じない | ごく一部だけ似る | 参照と比べれば本人らしさがある | 説明なしでも本人らしく感じる | 複数台詞でも一貫して本人らしい |
| 演技の時間構造 prosody | リズム・間・抑揚が別物 | 大きく崩れる | 大枠は残るが一部平坦化 | 小さな差のみ | sourceの間・強調・流れがほぼ保たれる |
| 感情 retention | 感情が逆転・消失 | 狙った感情がほぼ不明 | 感情の種類は分かる | 強さまで概ね保つ | 笑い・囁き等の細部も保つ |
| 音の破綻・ノイズ | 再生に耐えない | 強いノイズや崩れが頻繁 | 明確な加工感はあるが聞ける | 小さな人工音のみ | 問題がほぼ気にならない |
| セットアップ再現性 | 通常手順で起動不可 | 手作業の修復が毎回必要 | 手順で起動するが不安定点あり | 再試行程度で安定 | 別のクリーン環境でも記載手順で再現 |
| ゲーム適合性 | 組込不可／重大な契約違反 | 根本設計の変更が必要 | 制約付きで演出に使える | 小さな調整で使える | 起動・待ち・取消・削除・配布が要求を満たす |

感情 retention は中立台詞など対象外なら `N/A`。本人らしさは所有者本人の採点と、参照を聞いた第三者の採点を混ぜず保存。匿名化していても音声を別評価者へ渡すには参加者の同意が必要。

客観補助として、原台本の主要語の脱落・置換・追加、台詞先頭／末尾の欠落、約2秒の間の実長を記録する。日本語の聞き取りを英語前提のWERだけで判定しない。自動認識・話者類似モデル・F0解析を追加するなら、そのツールの日本語対応・ライセンスを別監査し、補助値で本人の主観を置き換えない。

## 12 性能の測定定義

- `model_load_s`: 初期化開始から health=ready まで。重みダウンロード時間は含めず、別フィールドで記録。
- `convert_s`: モデル呼び出し開始から結果の計算完了まで。GPU非同期処理を使う場合は前後の同期を含める。モデル内部タイマーか呼出側計測かを記録。
- `wall_s`: request受理から output WAV が完全に書かれて応答が返るまで。VAD・リサンプル・watermark・ファイルI/Oの有無を記載。
- `RTF = convert_s / source_duration_s`。ゲームの待ち時間判断には `wall_s / source_duration_s` も併記する。参照長やモデルロードを分母へ足さない。
- `peak_allocated_vram_mib` / `peak_reserved_vram_mib`: frameworkの同一process内計測。load時とconvert時を分離し、各区間前にpeak統計をリセットする。
- `device_used_vram_mib`: GPU全体の使用量。Unity / OBS / desktop分を含むため、framework値とは別列。サンプリング間隔を記録し、短いpeakの見逃しを明記する。
- `process_ram_peak_mib`: sidecar本体に加え子processがあれば含めた範囲を明記。Windows private bytes / working set のどちらかも明記。
- 一部取得できない値は空欄＋理由。0を書いて「消費なし」にしない。
- CPU fallback はCUDAと別条件。GPUの模擬出力をCUDA実推論成功として扱わない。

### RTX 4060 8GB・Unity・OBS 共存

同じWindows起動条件で次のbaselineと変換中を比較する:

1. sidecar単独。普段のdesktop負荷は記録する。
2. 同じUnityビルド／同じシーン＋sidecar。Editorのメモリとは分ける。
3. Unityビルド＋OBSのローカル収録＋sidecar。ライブ配信しない。OBSは本人・他者の実映像を使わず、合成sceneを使う。

Unity解像度、fps上限、品質設定、シーン、OBS解像度/fps/encoder、GPU driver、Windows/Python/PyTorch/CUDA、電源設定、他のGPU使用アプリを記録する。モデルロード時も含めて実施し、steady状態だけの成功にしない。

見る値: OOM、OS異常、Unity frame time p50/p95と長い停止、OBSのrender/encoding遅延・drop、wall time、総VRAM/RAM。4060 8GBというラベルだけから共存成功を推測しない。

## 13 ゲーム上の合格基準（初期提案）

### 必須ゲート

- 使用するコード・重み・付属資産それぞれの用途・再配布条件が解決している。未解決なら「研究のみ可／製品採用保留」とする。
- 意図しない外部通信・音声アップロード、本人の同意を超える利用がない。
- 固定台詞の重要語、否定、指示先、人名等の意味を変えない。重大な言い換え・欠落・追加は1件でもその条件を不合格にする。
- 取消、失敗、process crashがUnityを落とさない。削除後の復活や隠しcacheへの残存がない。
- 試験した運用条件でOOMなし。検証されていない条件へ合格を拡張しない。

### 音質の採用目安

- intelligibility中央値4以上、最低3以上、かつ重要語誤変換なし。
- target similarity中央値3以上。本人が「似ている」と感じることを必ず別途確認。
- prosody中央値3以上。JP08の指定した間とJP10の演技の変化が失われないこと。
- emotion中央値3以上（該当台詞のみ）。
- artifact中央値3以上を通常候補。2でも恐怖演出に合う限定台詞は「演出限定採用」にできるが、内容保持や安全ゲートを緩めない。
- setup / integrationはPoC段階3以上を目安とする。量産採用時は再現性・配布・削除を実機で確認し、4以上を目指す。

### 待ち時間・共存の採用目安

これらは未実測の初期予算。ゲームの実際の待機時間に合わせて見直す。

- メニュー／準備中に変換する初期PoC: warm RTF中央値1以下を目標。1を超えても、場面前に作り終えられるなら保留候補として残す。
- 操作を止めて変換する設計は別評価。5秒のsourceを変換するwall timeを、その場面の許容待ち時間（例として5秒）以内に収められるか実測する。モデルロードを隠せないならその時間も足す。
- 8GB GPUの共存では、最悪条件で総VRAMに約1GiB以上の余裕を残す案を出発点とする。これは普遍的な安全保証ではなく、実際のUnity/OBSのpeakに合わせて変更する。
- Unityの基準fps予算を満たし、変換によるp95 frame time増加をbaseline比10%以内とする案。OBSのrender/encoding overload由来dropを増やさないこと。baseline自体が予算を超える場合は比較前に環境を整える。
- 共存不合格でも、録画前の生成・ロード画面内の直列処理・必要時だけモデル解放で成立するなら「事前生成限定」に分類する。

最終分類は「採用候補」「演出／事前生成限定」「追加検証」「不採用」。平均点の順位だけで決めない。

## 14 Issue #3 / #5 への評価接続

#3 の source.wav + target.wav → converted.wav を最小単位とする。#5 の backend抽象化・JSONL/ファイルパス・request ID をそのまま使い、評価仕様のために本体のinterfaceを無断変更しない。

- 実装側が確定した protocol version と health/statusを記録する。
- 評価runnerは同一のsource/target行を順に送る。最初はserial queueのみ。同時実行で性能を水増ししない。
- 初期化、ready、convert、cancel/timeout、output検証、shutdownを別段階として記録。
- output duration、sample rate、channel数、ファイルサイズ、NaN/Inf、全無音、clip peakを検査。技術的に有効でも音質採点は別途行う。
- request timeoutに達した場合、後から完了した出力を成功に数えない。遅延完了物と一時ファイルの処理を確認する。
- MockBackendはプロトコル・ファイル寿命・失敗経路のみ評価。実モデル名の結果へ混ぜない。
- ChatterboxBackend等の実推論は `execution_kind=real_model` とし、モデルロード・CUDA使用実態が確認できたrunだけ計測へ入れる。
- 不明な入力音声を探して自動試験に使わない。本人の許可済み素材がない場合は「実音声PoC未実施」で正しく止める。

### PlayerData 寿命の専用試験

通常素材を複製した隔離fixtureだけで実施する:

1. sourceは制作アセットか個人由来かを区別し、targetと永続化した個人由来outputがPlayerData配下にあることを確認。
2. 変換中の取消と削除の競合で、遅れて完成したoutputが削除済みデータを復活させない。
3. 既存仕様の「削除時点でロード済みのRAM snapshotだけ現在の起動中は利用可」を維持する。
4. disk削除後は再ロードせず、プロセス再起動後は不存在。新規の生成ジョブや再保存で復活させない。
5. 一時wav・embedding・backendcache・失敗時outputも対象を列挙して検証。一般のモデル重みcacheと個人音声由来cacheを混同しない。
6. StoryProgressを消さない。fixture成功と本編統合成功は別の結果。

## 15 記録スキーマ

実装の既存fieldを優先し、下記との対応表を付ける。schemaVersionを固定する。CSVはUTF-8、1行1run、単位を列名で固定。詳細JSONLを別途保存してもよいが、公開版には個人音声パスや個人情報を含めない。

### runs.csv（機械計測）

```csv
schema_version,run_id,utc_time,execution_kind,model_id,code_revision,weights_revision,weights_sha256,dependency_lock_sha256,protocol_version,backend_config_id,device,driver_version,python_version,torch_version,cuda_version,source_id,source_take,target_id,reference_take,source_duration_s,source_speech_s,reference_requested_s,reference_file_s,reference_effective_s,reference_vad_s,reference_preprocess,source_sample_rate_hz,model_input_rate_hz,output_rate_hz,seed,condition_id,repeat_index,is_warmup,process_cold,model_load_s,convert_s,wall_s,rtf_model,rtf_wall,output_duration_s,output_bytes,peak_allocated_vram_mib,peak_reserved_vram_mib,device_peak_used_vram_mib,ram_peak_mib,ram_metric,resource_sample_interval_ms,unity_build_id,unity_scene_id,unity_p95_frame_ms,obs_profile_id,obs_render_drop,obs_encode_drop,status,error_stage,error_code,notes
```

- `status`: `success`, `preprocess_failed`, `unsupported_condition`, `model_load_failed`, `conversion_failed`, `timeout`, `cancelled`, `output_invalid`, `not_run`。
- `error_stage`: `setup`, `input`, `preprocess`, `load`, `infer`, `write`, `client`, `lifecycle`。
- `notes`に生のstack trace、個人名、絶対パスを入れない。ローカル診断ログと公開集計を分離。
- 参照の実消費長が内部で確認できなければ `reference_effective_s` は空欄とし、「不明」と補足する。推測した長さを書かない。

### ratings.csv（主観票）

```csv
schema_version,run_id,rater_id,rater_role,blind_order,intelligibility,target_similarity,prosody_retention,emotion_retention,artifact_quality,setup_reliability,game_fit,critical_content_error,missing_word_ids,pause_expected_s,pause_observed_s,listen_count,comment
```

`rater_role`はowner / reference-listenerなど。機械計測と主観票をrun_idで結び、音質評価のないrunは「未評価」とする。

### model_manifest（候補ごとの必須情報）

model_id / code URL+revision+license / weights URL+revision+license / bundled componentsと個別license / distribution条件と未解決点 / official source日付 / pretrainedまたは個別学習 / input/output仕様 / target前処理と上限 / watermark有無と既定動作 / network動作 / official benchmarkと今回実測の区別。

## 16 最終報告テンプレート

候補ごとに次を記載する:

1. 実際に試した版とライセンスの未解決点。
2. 成功・失敗した参照長と、内部で使われた長さ。
3. 原音の言葉・演技・本人らしさの採点。代表的な破綻を台詞IDで記す。
4. 4060単独／Unity／Unity+OBSの実測。未実施は未実施。
5. 個別学習・setup・配布・削除に必要な追加作業。
6. 「採用候補／演出限定／追加検証／不採用」と理由。
7. 次に本人が試す最小手順。モデルや参照長を同時に大量変更せず、差が分かる1条件ずつ。

ライセンス監査が通っても音質は保証されず、Mockが通ってもVCができたことにはならない。本仕様だけではモデル採用を決定しない。

## 17 一次情報と版固定の注意

本文のリンクは公式repository、公式model card、公式論文を優先した。`main` / `master` やmodel cardは更新されるため、製品に採る実ファイルのlicense・revision・SHAを別manifestへ固定する。以下の2点は固定commitでも確認している。

- Chatterboxの参照10秒切り詰め: [vc.py / 5de7a54][CB API fixed]。
- OpenVoiceのVAD分割数計算: [se_extractor.py / 74a1d147b17a8c3092dd5430504bd83ef6c7eb23][OV VAD fixed]。

この2点以外の本文全ページ・全重みが、履歴表に示したcommitと完全に一致することまでは確認していない。実装担当は、候補を選んだ時点のcode/weights/依存を同時に固定し、短参照の挙動をその版で再確認する。

**残る実機確認:** 全候補の日本語VC品質、短参照／声域差の破綻傾向、Windowsセットアップ再現性、4060 8GBのload/convert/peak/RAM、Unity＋OBS共存、watermark加工後の検出、無通信起動、配布物の完全な部品監査。これらを推測で合格扱いにしない。

[CB API]: https://github.com/resemble-ai/chatterbox/blob/master/src/chatterbox/vc.py
[CB API fixed]: https://github.com/resemble-ai/chatterbox/blob/5de7a54/src/chatterbox/vc.py
[CB README]: https://github.com/resemble-ai/chatterbox/blob/master/README.md
[CB model]: https://huggingface.co/ResembleAI/chatterbox
[CB deps]: https://github.com/resemble-ai/chatterbox/blob/master/pyproject.toml
[CB LICENSE]: https://github.com/resemble-ai/chatterbox/blob/master/LICENSE
[CB S3Gen]: https://github.com/resemble-ai/chatterbox/blob/master/src/chatterbox/models/s3gen/s3gen.py
[CB history]: https://github.com/resemble-ai/chatterbox/commits/master/
[S3Tokenizer]: https://github.com/xingchensong/S3Tokenizer
[Perth README]: https://github.com/resemble-ai/Perth
[Perth LICENSE]: https://github.com/resemble-ai/Perth/blob/master/LICENSE
[Perth weights]: https://github.com/resemble-ai/Perth/tree/master/src/perth/perth_net/pretrained
[Perth implementation]: https://github.com/resemble-ai/Perth/blob/master/src/perth/perth_net/perth_net_implicit/perth_watermarker.py
[OV README]: https://github.com/myshell-ai/OpenVoice/blob/main/README.md
[OV API]: https://github.com/myshell-ai/OpenVoice/blob/main/openvoice/api.py
[OV VAD]: https://github.com/myshell-ai/OpenVoice/blob/main/openvoice/se_extractor.py
[OV VAD fixed]: https://github.com/myshell-ai/OpenVoice/blob/74a1d147b17a8c3092dd5430504bd83ef6c7eb23/openvoice/se_extractor.py
[OV model]: https://huggingface.co/myshell-ai/OpenVoiceV2
[OV usage]: https://github.com/myshell-ai/OpenVoice/blob/main/docs/USAGE.md
[OV paper]: https://arxiv.org/html/2312.01479v5
[OV QA]: https://github.com/myshell-ai/OpenVoice/blob/main/docs/QA.md
[OV deps]: https://github.com/myshell-ai/OpenVoice/blob/main/setup.py
[OV LICENSE]: https://github.com/myshell-ai/OpenVoice/blob/main/LICENSE
[OV history]: https://github.com/myshell-ai/OpenVoice/commits/main/
[WavMark API]: https://github.com/wavmark/wavmark/blob/main/src/wavmark/__init__.py
[WavMark LICENSE]: https://github.com/wavmark/wavmark/blob/main/LICENSE
[WavMark model]: https://huggingface.co/M4869/WavMark
[RVC README]: https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI/blob/main/README.md
[RVC JP]: https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI/blob/main/docs/jp/README.ja.md
[RVC training]: https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI/blob/main/docs/en/training_tips_en.md
[RVC LICENSE]: https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI/blob/main/LICENSE
[RVC model]: https://huggingface.co/lj1995/VoiceConversionWebUI
[RVC component licenses]: https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI/blob/main/MIT%E5%8D%8F%E8%AE%AE%E6%9A%A8%E7%9B%B8%E5%85%B3%E5%BC%95%E7%94%A8%E5%BA%93%E5%8D%8F%E8%AE%AE
[RVC history]: https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI/commits/main/
[MM Client]: https://github.com/isletennos/MMVC_Client
[MM Trainer]: https://github.com/isletennos/MMVC_Trainer/blob/main/README.md
[MM deps]: https://github.com/isletennos/MMVC_Trainer/blob/main/requirements.txt
[MM Trainer LICENSE]: https://github.com/isletennos/MMVC_Trainer/blob/main/LICENSE
[MM Client LICENSE]: https://github.com/isletennos/MMVC_Client/blob/main/LICENSE
[MM Trainer history]: https://github.com/isletennos/MMVC_Trainer/commits/main/
[MM Client history]: https://github.com/isletennos/MMVC_Client/commits/main/
[SD repo]: https://github.com/Plachtaa/seed-vc
[SD README]: https://github.com/Plachtaa/seed-vc/blob/main/README.md
[SD inference]: https://github.com/Plachtaa/seed-vc/blob/main/inference.py
[SD deps]: https://github.com/Plachtaa/seed-vc/blob/main/requirements.txt
[SD LICENSE]: https://github.com/Plachtaa/seed-vc/blob/main/LICENSE
[SD model]: https://huggingface.co/Plachta/Seed-VC
[Whisper model]: https://huggingface.co/openai/whisper-small
[BigVGAN model]: https://huggingface.co/nvidia/bigvgan_v2_22khz_80band_256x
[CAMPPlus model]: https://huggingface.co/funasr/campplus
[CV API]: https://github.com/QwenAudio/CosyVoice/blob/main/cosyvoice/cli/cosyvoice.py
[CV LICENSE]: https://github.com/QwenAudio/CosyVoice/blob/main/LICENSE
[CV model]: https://huggingface.co/FunAudioLLM/CosyVoice-300M
[CV history]: https://github.com/QwenAudio/CosyVoice/commits/main/
[CV example]: https://github.com/QwenAudio/CosyVoice/blob/main/example.py
[CV config]: https://huggingface.co/FunAudioLLM/CosyVoice-300M/blob/main/cosyvoice.yaml
