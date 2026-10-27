# Voice Conversion PoC (ChatterboxVC)

Issue #3 用の独立PoC。Unityにはまだ接続しない。

## 目的

`source.wav` の台詞内容・テンポ・感情をできるだけ保ちつつ、`target.wav` の話者らしい声色へ変換できるか確認する。

想定用途:

- `source.wav`: AIくん役として事前収録した固定台詞
- `target.wav`: プレイヤー本人の設定時マイクテスト
- 出力: AIくんの台詞をプレイヤーの声色で喋ったWAV

リアルタイム性はこの段階では評価対象外。まず品質、処理時間、VRAMを見る。

## 採用候補

Resemble AI Chatterbox の公式 `ChatterboxVC` を使う。

- Repository: https://github.com/resemble-ai/chatterbox
- Model: https://huggingface.co/ResembleAI/chatterbox
- Code license: MIT
- Hugging Face model license: MIT 表記
- `ChatterboxVC` は `audio` + `target_voice_path` を入力にする audio-to-audio VC
- 出力には Chatterbox の PerTh watermark が自動適用される

製品同梱を決める段階では、固定したpackage/model versionと全依存ライセンスを再監査する。

## Windows / RTX 4060 セットアップ

既存のStable Diffusion等のPython環境は汚さず、このフォルダ専用venvを作る。

PowerShell:

```powershell
cd tools/voice-conversion-poc
./setup.ps1
```

`setup.ps1` は Python 3.10 のvenvを作り、PyTorch 2.6.0 CUDA 12.4版と `chatterbox-tts==0.1.7` を入れる。

Python 3.10 が `py -3.10` で見つからない場合は、手動でvenvを作って同じpipコマンドを実行する。

初回実行時は公式モデルがHugging Faceからダウンロードされる。VC本体だけでも約1GB級のweightを使うため、初回だけ時間と空き容量が必要。

## 音声素材

### source.wav

- まず5〜15秒程度
- 日本語
- BGM/SEなし
- リバーブなし
- 感情や間を確認したいので、平坦すぎない演技を推奨

例: 明るい案内声、少し笑う、短い間を置く、語尾を強める等。

### target.wav

- プレイヤー本人または明示的に許諾された話者のみ
- 10秒程度の単独音声を推奨
- BGM/SEなし
- 極端なノイズ処理やエフェクトなし

公式実装はtarget referenceを最大約10秒使用する。このPoCでは2秒/5秒/10秒に切って比較できる。

## 基本テスト

```powershell
.venv/Scripts/python.exe vc_test.py `
  --source source.wav `
  --target target.wav `
  --output converted.wav `
  --reference-seconds 10
```

出力:

- `converted.wav`
- `converted.report.json`

reportには model load時間、conversion時間、source/output長、real-time factor、CUDA peak memoryを記録する。

## 参照長比較

```powershell
.venv/Scripts/python.exe vc_test.py `
  --source source.wav `
  --target target.wav `
  --output converted.wav `
  --reference-seconds 2 5 10
```

この場合:

- `converted_ref2s.wav`
- `converted_ref5s.wav`
- `converted_ref10s.wav`
- 各 `.report.json`

が生成される。

## 最初に聞くポイント

1. 台詞内容が壊れていないか
2. 子音・母音が潰れていないか
3. sourceの間・テンポが残るか
4. 笑い・叫び・息・語尾などが残るか
5. target本人に聞こえるか
6. source話者の声色がどの程度残るか
7. 2秒/5秒/10秒で本人らしさがどう変わるか

品質が十分なら次に `IVoiceConverter` sidecar化を検討する。品質不足ならOpenVoice等との比較へ進む。

## 安全上の制約

- 第三者の声を無断で入れない。
- 生成物を一般用途のvoice clone/export機能として公開しない。
- このPoCはローカル処理のみ。
- PlayerDataへ統合する場合、target referenceと派生WAVはPlayerData削除契約の対象にする。