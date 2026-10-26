# PlayerData Matrix

対象: Issue #1 / `PlayerData Capture Framework v0`

## 方針

- PlayerDataは通常のStoryProgressと分離する。
- プレイヤー由来の原データだけでなく、**そこから生成した派生データもPlayerDataとして扱う**。
- PlayerData削除時はディスク上の原データ・派生データ・個別埋め込み・個別モデルをすべて削除対象にする。
- 削除前に明示ロード済みのRAMスナップショットだけは現在セッション中利用可能。アプリ終了後は復元しない。
- static / PlayerPrefs / hidden cache / cloudへ本人データを逃がさない。
- 外部送信は行わない。
- ユーザー個別fine-tuningはv0で作らない。

## データ一覧

| 取得元 | 保存するもの | 用途 | 保存期間 | 削除時挙動 | 外部送信 | 必須/任意 |
| --- | --- | --- | --- | --- | --- | --- |
| マイク設定テスト | PCM16 WAV + metadata + stable VoiceID | 後半の本人音声照合、録音再生、本人Voice復旧 | ストーリー跨ぎで必要な間ローカル保持 | ディスク削除。既にRAMロード済みなら現セッションのみ使用可。再起動後不可 | なし | **コア** |
| 生マイク | 通常は保存しないlive stream | 本人Voice復旧後の会話、音量/ピッチ判定 | セッション中のみ | capture停止、buffer解放 | なし | **コア** |
| マイク較正値 | RMS、基準音量、必要ならpitch range等の最小数値 | 大声判定、音ゲー較正 | 必要ならPlayerDataへ保存 | PlayerData削除と同時削除 | なし | 任意 |
| Webcam代表フレーム | PNG + metadata | カメラ確認、外観処理、後続PoC | ストーリー跨ぎで必要な間 | PlayerData削除対象 | なし | Cameraモードではコア |
| Webcam live frame | RAM texture/frame | ミニゲーム、tracking input | セッション中 | Camera停止/Destroy | なし | Cameraモードではコア |
| Silhouette / segmentation mask | mask imageまたは圧縮binary | グリーンバック系、切り抜き | **原則RAMのみ**。後半演出で必要ならPlayerData | 原画像と同じ削除契約 | なし | 任意 |
| Pose landmarks | timestamp付き正規化座標 | 身体ミニゲーム、コピー、ボス戦 | 必要なclipだけローカル保持 | PlayerData削除対象 | なし | Motion機能ではコア |
| MotionClip | Pose landmarksの時系列/派生clip | 過去動作の再演 | ストーリー跨ぎで必要なら保存 | PlayerData削除対象 | なし | 任意 |
| Face landmarks / blendshape相当値 | timestamp付き最小特徴量 | 表情ミニゲーム、コピー演出 | 必要な区間だけ。常時保存しない | PlayerData削除対象 | なし | 任意 |
| Hand landmarks | timestamp付き座標 | 手振り/ジェスチャーゲーム | 必要な区間だけ | PlayerData削除対象 | なし | 任意 |
| Player cutout frame | raw image + maskから得る派生画像 | コピー表示、後半演出 | 可能なら再生成。保存する場合はPlayerData | 元画像削除時に一緒に削除 | なし | 任意 |
| Voice conversion output | streaming audio / 一時WAV | 声の保管庫での試着 | **RAM/一時のみを推奨**。物語上必要な場合だけPlayerData | buffer/一時file削除。派生物もPlayerData削除対象 | なし | 任意 |
| Player voice embedding / reference feature | ベクトル/特徴量 | zero-shot VC等 | **保存しないのを推奨**。必要ならPlayerData | PlayerData削除対象 | なし | 任意・v0未採用 |
| Player-specific trained weights | model weights | 個別VC/生成 | **作らない** | 該当なし | なし | **v0禁止** |
| Generated singing output | 一時WAV | AIコピーが本人声で歌う演出 | 原則一時。再起動跨ぎが必要ならPlayerData | PlayerData削除対象 | なし | 将来任意 |
| Generated appearance / animation cache | frame/latent/keypoints等 | コピーの再演 | 原則再生成またはRAM。一時保存もPlayerData配下のみ | PlayerData削除対象 | なし | 将来任意 |
| Live2D / VRM / VRC等の元モデル | **原則コピーしない** | 外部アバター入力 | 元アプリ/元ファイル側で管理 | YourParkは削除対象を持たない | なし | 任意 |
| 外部アバターのrender frame / tracking値 | texture / landmarks / parameters | YourPark内表示とミニゲーム | 必要なものだけPlayerData。raw source modelは保存しない | PlayerData分だけ削除 | なし | 任意 |
| サムネイル作成中画像 | RAM texture、必要ならユーザー明示保存 | ミニゲーム | 通常はセッション中のみ | buffer解放 | なし | 任意 |
| 実配信コメント本文 | transient message | コメント削除/本物コメント欄当て | **PlayerDataへ永続保存しない** | RAM破棄 | platform API受信のみ。YourParkから別所へ送らない | 配信モード任意 |
| コメント欄/channel識別ID | connection metadata | 本物コメント欄照合 | 必要最小限。秘密tokenと分離 | 接続解除または設定削除 | platform接続に必要な範囲のみ | 配信モード任意 |
| OAuth token等の認証秘密 | **PlayerDataに入れない** | platform auth | OS/SDKの安全な認証保管を利用 | auth revoke/logoutに従う | platform認証先のみ | 配信モード任意 |
| コメント欄パズルの選択状態 | ID/flagのみ | ストーリー進行 | StoryProgress | PlayerData削除の影響を受けない | なし | 任意 |
| AI workerログ | 本人音声/画像本文を含めない診断情報 | デバッグ | 短期 | PlayerData ID等を匿名化/最小化 | なし | 任意 |

## 必須ルール

### 派生データの連鎖削除

元VoiceSample / ImageSample / PoseClipから生成した派生物を保存する場合、metadataにsource IDを持たせる。

PlayerData全削除では全派生物を削除する。将来「個別sample削除」を作る場合はsource IDを辿って派生物も削除する。

### RAM例外

ストーリー仕様上、削除後も現在セッションで既にロード済みのデータをAIが使える。

この例外は以下に限定する。

- 削除前に`PlayerDataSession`へ明示ロード済み
- 再保存しない
- hidden cacheへコピーしない
- アプリ終了時に破棄
- 次回起動時にディスクから復活させない

### コメントは本人データと混ぜない

配信コメントは外部プラットフォーム由来の第三者データを含むため、PlayerDataへ保存しない。

コメント欄当てゲームは、

- live受信
- fake comments
- channel/connection ID

を分離する。本物コメント本文はRAM上だけで扱い、AIコピー学習用素材にはしない。

### アバター元ファイルを奪わない

Live2D / VRM / VRCアバター利用時も、可能なら元モデルファイルそのものをYourParkへ複製しない。

必要なのは、

- renderされた本人の見た目
- tracking parameters
- 本人がゲーム中にしたmotion

であり、モデルsource fileの所有・再配布権までゲームへ持ち込まない。

## 同意画面との対応

現仕様では同意文の主語は「学習」より以下が正確。

- 取得
- 解析
- 保存
- 加工
- 再構成
- 生成

例:

> 本ゲームには、カメラ・マイク・アバター等から入力された映像・音声・動作情報を端末内で取得・解析・保存・加工し、ゲーム内演出のために再構成または生成する機能が含まれます。本人由来データは本ゲームの機能・演出以外の目的で外部へ送信しません。

実装にユーザー個別fine-tuningを追加した場合のみ「学習」を追記する。

## 現PlayerData Frameworkとの対応

- stable ID: 既存metadataを使用
- format version: 既存version 1を使用
- local relative path: 既存契約を使用
- SHA256 / size validation: 既存契約を使用
- RAM snapshot: `PlayerDataSession`
- disk delete: `IPlayerDataStore`
- AI境界: `IPoseTracker` / `IAppearanceProcessor` / `IVoiceConverter` / `IVoiceGenerator` / `IMotionReplayer`

今後AI adapterを追加しても、adapterが独自のPlayerData保存先を作らないこと。永続化が必要な派生物は必ず`IPlayerDataStore`経由でPlayerData配下に置く。

## 推奨保持ポリシー

### 前半〜後半開始前

ストーリー跨ぎで必要な本人由来データだけ保持。

### 後半開始後

削除された場合はディスク上データを即座に失う。ロード済みRAMだけ現在セッション中使用可能。

### 再起動

PlayerDataがなければ利用不能。StoryProgressが「後半開始済み」であれば、自力解決ルートへ分岐可能。

### クリア後

安全になったユアパークで再利用する本人データは、再度明示的に許可する設計を推奨。旧ストーリー用PlayerDataを当然のように持ち越さない。

## v0で保存禁止にするもの

- 任意第三者の顔/声ファイル
- platform OAuth token
- 配信コメント履歴
- PlayerDataのcloud copy
- user-specific model weights
- hidden backup
- external AI serviceへ送るpayload

これにより、現v0は「本人由来データをローカルで取得・保存し、本人が削除できる」ことに集中する。