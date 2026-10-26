# 開発引き継ぎ

## 更新日時

2026-10-27 JST

## 現在の作業

ユーザーがIssue #1/#2を指定しdotと並行開始を依頼。Codexは#2のPlayerData Capture Framework v0を実装。空の `kissho222/your-park` を `C:/Users/shoho/YourPark` へcloneし `feat/playerdata-capture-v0` を作成。Issue #1の調査資料はdot担当として未編集。元の相談スレッド「あなたが遊べる！ユアパークへようこそ！」へ調査開始・分担を送信し、一時activeを確認。その後idleへ戻り、調査着手の応答・成果物は未確認。

## 変更ファイル

`Packages/com.yourpark.playerdata/Runtime` にデータ・保存・RAMセッション・WAV・mock・Unity機器入力・Motion再生を追加。`Tests/Editor` に回帰テスト。`Assets/YourParkDemo` に固定UXML/USSとScene生成/Windowsビルド用Editor機能。`docs/PLAYER_DATA_FRAMEWORK.md` とREADMEに仕様・責務・確認手順。

## 確認結果

Unity6000.3.10f1 EditMode32/32成功。manifestのnull配列がJsonUtilityに補正される問題を、標準DataContractJsonSerializerの必須フィールド契約で解消。Poseも必須version/ランドマーク名を検査。Windows通常・Developmentビルド成功。合成データだけを使った別々の3プロセスで、保存→再起動後全データ読込/削除→再起動後利用不可を確認し、各exit0。削除後のロード済みRAM利用・StoryProgress内容不変も成功。TestResultsにXML・ビルド・プロセスログ（Git対象外）。

## 未解決・未確認

実カメラ／実マイクは自動起動していない。通常ビルドの画面表示は確認したが、初回Development起動のWindowsネットワーク許可ダイアログ（PickerHost）がUI操作を遮り、ボタンの通し確認は未完了。許可・ファイアウォール設定変更なし。通常ビルドにはPlayerConnection検出ログがなく、検証専用DevelopmentビルドだけにUnity標準接続通信がある。実トラッカー／人物切抜き／AIモデルは範囲外。ゲーム本体・通常セーブはまだないため独立した進行fixtureをデモで使う。Issue #2は実機手動確認待ちで完了/Close扱いにしない。

## 確定した判断

PlayerDataをpersistentDataPath直下の専用フォルダへ分離。ディスク削除後も現在Sessionに明示ロード済みのRAMだけ再生可。新Sessionは削除済みIDを利用不可。自動再保存／隠し退避／外部送信は実装しない。モデル選定とライセンス調査は#1に委ねる。新C#の責務は仕様書へ同期。
