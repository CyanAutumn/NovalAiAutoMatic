# NovalAi3 自動バッチ生成ツール

言語: [简体中文](../README.md) | [English](README.en.md) | 日本語

![platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
![framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?logo=dotnet&logoColor=white)
![release](https://img.shields.io/github/v/release/CyanAutumn/NovalAi3AutoMatic)

NovelAI 向けの Windows 一括生成・ディレクターツールクライアント。複数モデル、Prompt テンプレート、Vibe/Img2Img 参照画像、自動連続実行に対応。

![UI スクリーンショット](source/image.png)

## 目次
- [機能](#機能)
- [動作環境](#動作環境)
- [ダウンロードとインストール](#ダウンロードとインストール)
- [初期設定](#初期設定)
- [使い方](#使い方)
- [パラメータ説明](#パラメータ説明)
- [Anlas 残高と消費量](#anlas-残高と消費量)
- [出力とファイル名](#出力とファイル名)
- [設定とファイル配置](#設定とファイル配置)
- [自動更新](#自動更新)
- [よくある質問](#よくある質問)
- [開発とビルド](#開発とビルド)
- [関連リンク](#関連リンク)
- [ライセンス](#ライセンス)

## 機能
- 一括生成とリズム制御：実行回数と短休/長休の設定。
- 複数モデル対応：NAI3 / NAI3 Furry / NAI4 Preview / NAI4 Full / NAI4.5 Curated / NAI4.5 Full / NAI5 Curated / NAI5 Full。
- Anlas 集計：設定で有効化でき、残高の取得・実消費の記録に加えて、「生成」ボタンの右側に今回の推定消費と残り Anlas を表示します（NAI5 は Opus クォータバーも表示）。
- Prompt テンプレート：固定/ランダム画師、ランダム提示詞、Wildcard 占位符。
- 参照画像：Vibe 複数参照（`.naiv4vibe` 対応）、Img2Img 強度/ノイズ。
- ディレクターツール：背景除去、線画、スケッチ、着色、表情、ゴミ除去。単体/一括に対応。
- 出力と記録：PNG/WEBP、同名 TXT への Prompt 保存、ファイル名形式切替、ログ出力。
- プリセット管理：複数プリセットの保存/切替、前回終了時の自動復元。
- 自動更新（GitHub Releases）。

## 動作環境
- Windows 10/11
- .NET Framework 4.8
- NovelAI アカウント Token（必要に応じて Proxy）

## ダウンロードとインストール
1. Releases からダウンロードして展開：<https://github.com/CyanAutumn/NovalAi3AutoMatic/releases>
2. `AutoNai3Tools.exe` を実行。
3. 初回起動で既定の出力/設定フォルダーが作成されます。

## 初期設定
1. `Token` を設定（NovelAI アカウントから取得）。
2. 必要なら `Proxy` を設定。例：`http://127.0.0.1:7890`。
3. ディレクトリ設定：
   - 出力フォルダー `OutputPath`
   - ランダム提示詞フォルダー `RandomPromptFolderPath`
   - Wildcard フォルダー `WildcardFolderPath`
4. モデル、解像度、Sampler、Steps などを調整。

## 使い方

### 基本的な生成フロー
1. Prompt / Negative Prompt を入力。
2. モデルと生成パラメータ（Steps、Sampler、Scale、CFG など）を選択。
3. 「実行回数」「パラメータ固定回数」を設定。
4. 「生成」をクリック。実行中はいつでも停止可能。

### Prompt テンプレート記法
| 記法 | 説明 | 例 |
| --- | --- | --- |
| `<固定画师>` | 「固定画師」入力欄の内容を使用 | `1girl, <固定画师>` |
| `<随机画师>` | 画師リストからランダム合成 | `1girl, <随机画师>` |
| `<随机提示词>` | ランダム提示詞フォルダーから抽選 | `<随机提示词>` |
| `<随机提示词:顺序>` | ファイル順で巡回 | `<随机提示词:顺序>` |
| `<xxx>` | Wildcard：`wildcard/xxx.txt` から1行 | `<衣服>` |
| `<xxx:顺序>` | Wildcard を順番に | `<衣服:顺序>` |

補足：
- ランダム提示詞フォルダー内の各 `.txt` は 1 つの Prompt 断片（カンマ区切り）。改行はスペース扱い。
- Wildcard は 1 行 1 候補。画面の片段（スニペット）クリックで挿入できます。
- 提示詞ブラックリスト/正規表現ブラックリストは主にランダム提示詞の結果をフィルタします。

### ランダム提示詞フォルダー形式
フォルダー内に複数の `.txt` を配置。各ファイルが 1 つの Prompt 断片です：
```
1girl, solo, masterpiece, best quality
```
`<随机提示词>` はランダム選択、`<随机提示词:顺序>` は順番に巡回します。

### Wildcard フォルダーと管理
`wildcard/` 配下の各 `.txt` が `<xxx>` の占位符になります：
```
wildcard/
  衣服.txt
  发型.txt
```
`衣服.txt` 例（1 行 1 候補）：
```
hoodie
long coat
school uniform
```

### 画師リスト形式と重み
「ランダム画師」は行ごとにグループ化し、行内は `|` で区切ります。各画師に重み指定が可能です：
```
artistA|artistB
artistC,0,2,0,3|artistD
```
重み形式：
- `名前,減重最小,減重最大,加重最小,加重最大`
- `[]` または `{}` で減重/加重をランダム付与。
- `::` で終わる画師（例：`artistE::`）は `x::artistE::` 形式に自動変換。
- 「Artist Modify」を有効にすると `artist:` プレフィックスを自動付与。

### Vibe 参照画像
- 複数参照および `.naiv4vibe` に対応。
- 各画像に `informationExtracted` と `referenceStrength` を設定。
- `.naiv4vibe` は初回にローカルキャッシュを生成し、以後高速化します。

### Img2Img
- 入力画像を選択し、`Strength` と `Noise` を設定。
- 画像未選択時は Img2Img パラメータは送信しません。

### ディレクターツール
- 背景除去、線画、スケッチ、着色、表情、ゴミ除去。
- 単体/フォルダー一括に対応。
- 着色/表情モードは追加 Prompt と Defry を入力可能。
- 出力は `OutputPath` に保存。

### プリセット管理
- プリセットの保存/読み込み/削除。
- 起動時に「前回終了時の自動保存」を読み込み。

### 解像度とパラメータ固定
- 「パラメータ固定回数」はランダム項目の更新頻度を制御：
  - 例：3 の場合、1 回目で更新、2～3 回目は保持、4 回目で再更新。
- ランダム画師/Wildcard/ランダム提示詞/解像度は個別に固定可能。
- 解像度モード：
  - 固定：常に現在の解像度
  - 順序：リスト順に巡回
  - ランダム：リストからランダム選択

## パラメータ説明
| パラメータ | 説明 | 備考 |
| --- | --- | --- |
| Model | モデル選択 | NAI3 / NAI3 Furry / NAI4 Preview / NAI4 Full / NAI4.5 Curated / NAI4.5 Full / NAI5 Curated / NAI5 Full |
| Steps | 生成ステップ数 | 1-28（超過は自動制限） |
| Sampler | サンプラー | `k_euler` / `k_euler_ancestral` / `k_dpmpp_2s_ancestral` / `k_dpmpp_2m_sde` / `k_dpmpp_2m` / `k_dpmpp_sde` / `ddim_v3` |
| Noise Schedule | ノイズ方式 | `native` / `karras` / `exponential` / `polyexponential` |
| Scale | Prompt Guidance | 0-10、小数1桁 |
| CFG Rescale | CFG Rescale | NovelAI と同様 |
| SMEA / DYN | サンプリング最適化 | SMEA オフで DYN もオフ |
| Decrisp | Decrisp | NovelAI と同様 |
| Variety | 多様性 | オフ / オン / カスタム_リスクパラメータ |
| 解像度 | Width / Height | 解像度リストとモードで制御 |
| 解像度リスト | 複数行入力 | 例：`832x1216` |
| 実行回数 | RunNum | クリックあたりの総生成回数 |
| パラメータ固定回数 | RunKeepParams | ランダム更新頻度 |
| 固定シード | FixedSeeds | オフ時は毎回ランダム Seed |
| Seed | Seeds | 固定シード時に有効 |
| 出力形式 | ImageFormat | PNG / WEBP |
| 出力ファイル名形式 | OutputFileNameFormat | NovalAI / 全画師語 / 日付 |
| Prompt 保存 | SavePromptToTxt | 画師あり/なしで保存可能 |
| Proxy | Proxy | 必要時のみ |
| ブラックリスト | PromptBlackList / Regex | ランダム提示詞のフィルタ用 |

> 注：NAI2（`nai-diffusion-2`）は NovelAI 公式で Retired となりモデル一覧から削除されました。サーバーは `model nai-diffusion-2 doesn't exist` を返すため、本ツールでは選択肢から除外しています。NAI2 を保存した旧設定は読み込み時に NAI3 へ自動切替し、ログに記録します。

## NovelAI V5 について
`nai-diffusion-5-curated` / `nai-diffusion-5-full` のリクエストボディは公式ウェブクライアントの規則に合わせて構築し、[公式モデルドキュメント](https://docs.novelai.net/en/image/models) に対応しています。V4.5 との違い：
- `params_version: 4` を使用し、旧 `qualityToggle` / `ucPreset` の代わりに `qualityPresetId`（`standard` / `none`）と `ucPresetId` を送信します。
- ノイズ方式は `karras` 固定（公式クライアントが V5 で強制）のため、Noise Schedule の設定は V5 では反映されません。
- サンプラーが `k_euler_ancestral` のときは brownian ノイズを有効化（`prefer_brownian: true`、`deliberate_euler_ancestral_bug: false`）、それ以外のサンプラーでは逆になります。
- V5 は SMEA / DYN、Decrisp（`dynamic_thresholding`）、Variety（`skip_cfg_above_sigma`）に対応していないため、これらの設定は無視されます。
- 公式クライアントでは V5 の Vibe Transfer は未開放です。Vibe 参照画像を設定した場合はログに警告を出力します（実際に有効かはサーバー次第）。
- 公式デフォルト：Steps 23、Prompt Guidance 7、Sampler `k_euler_ancestral`、解像度 832x1216。

## Anlas 残高と消費量
NovelAI が公開しているのは残高照会のみで、「パラメータごとの消費量を事前に取得する」API はありません（公式フロントエンドに現れる `/ai/generate-image/request-price` は実測 404）。本ツールの扱い：

- **スイッチ**：設定ページの「Anlas 集計を有効化」（既定で有効）。オフにすると残高 API を呼び出さず、「生成」ボタンにも何も表示しません。
- **ボタン表示**：有効時は「生成」ボタンの右側に「推定消費 / 残り」が付きます（例：`生成    Anlas ≈23 / 8992`）。`≈` はフィッティング式による推定値、`≈` がない場合はローカルキャッシュの実測値です。残高が未取得（または API が利用不可）のときは `?` を表示します。生成中は `停止    Anlas …` になります。
- **NAI5 は別計算**：NAI5 は Anlas ではなく Opus 購読クォータを消費するため、`生成    クォータ 0.085% / 100% + Anlas ≈35 / 8992` のように表示します（Anlas 部分は今回のパラメータで実際に Anlas を消費する場合のみ。1088x1088 以下かつ Steps <= 28 は無料枠のためクォータバーのみ）。
- **残高**：`GET {Api}/user/subscription`。Anlas 残高 = `trainingStepsLeft.fixedTrainingStepsLeft + trainingStepsLeft.purchasedTrainingSteps`（公式 Web クライアントと同じ計算式）、クォータバーの百分比は `usage.percent` を使用します。
- **消費量**：生成の直前に残高を取得し、生成後にもう一度取得して差分を今回の実消費として、モデル / サイズ / Steps / 枚数 / サンプラーとともにログへ出力します。
- **ローカルキャッシュ**：実測値は「モデル + サイズ + Steps + 枚数 + アクション/強度」単位で `C:\Users\Public\Documents\auto_nai3_system\anlas_cost_cache.toml` に保存します。ヒットした場合はそのまま再利用し（ボタン表示は推定値ではなく実測値になります）、ミスのときだけ生成前後の残高差で再取得します。Prompt は消費量に影響しないためキーに含めません。キャッシュの有効期限は 8 時間で、期限切れ後は再取得します。
- 「ログ」タブ右上の「Anlas 残高を確認」ボタンで、残高・Opus 使用率・購読の有効期限をいつでも確認できます。
- **リクエスト頻度**：1 枚につき最大 1 回の残高リクエストのみ、30 秒以内は同じ結果を再利用、失敗時は指数バックオフ（2 秒〜最大 60 秒）、5 回連続で失敗するとそのセッションの自動集計を停止、429 の場合は 60 秒バックオフします。サーバーへの負荷は問題ありません。
- この API を持たないサードパーティ中継を Api に設定した場合は、警告を 1 回記録してそのセッションの自動集計を停止します（生成には影響しません）。

### 課金ルール（実 API で実測）
- **無料**：1 枚かつ 幅×高さ <= 1024x1024（1,048,576 ピクセル）かつ Steps <= 28 のときは Anlas を消費しません。
- 無料のサイズ / Steps で複数枚（`n_samples > 1`）を生成する場合、1 枚目は無料で、残りが下記の式で課金されます。
- それ以外：

```
消費 = ceil( メガピクセル × 枚数 × f(steps) × モデル倍率 )
f(steps) = steps × 4/7 + 3.2
モデル倍率：V5（nai-diffusion-5-*）= 1.5、その他 = 1.0
```

実測サンプル（差分実測、Opus / tier 3）：

| モデル | サイズ | Steps | 枚数 | 実測 | 推定 |
| --- | --- | --- | --- | --- | --- |
| NAI4.5 Full | 1024x1024 | 28 | 1 | 0 | 0 |
| NAI4.5 Full | 1024x1024 | 29 | 1 | 21 | 21 |
| NAI4.5 Full | 1088x1088 | 28 | 1 | 23 | 23 |
| NAI4.5 Full | 1088x1088 | 50 | 1 | 38 | 38 |
| NAI4.5 Full | 1472x1472 | 28 | 1 | 42 | 42 |
| NAI4.5 Full | 1472x1472 | 50 | 1 | 69 | 69 |
| NAI4.5 Full | 1088x1088 | 28 | 2 | 46 | 46 |
| NAI4.5 Full | 512x512 | 28 | 2 | 5 | 5 |
| NAI5 Full | 1088x1088 | 28 | 1 | 35 | 35 |
| NAI5 Full | 1472x1472 | 28 | 1 | 63 | 63 |

補足：
- サンプラー、Curated / Full、NAI3 と NAI4.5 の違いは消費量に影響しません。
- 1 リクエストの最大解像度は 1536x2048 で、超えると 400 が返ります。
- Img2Img の Strength の影響は非公開ですが、ログの実測値は正確です。
## 出力とファイル名
出力ファイル名は「出力ファイル名形式」で決まります：
- `NovalAI`：`{prompt} s-{seed}`
- `全画師語`：`{artist_summary}_{seed}`
- `日付`：`yyyyMMdd_HHmmss`

「同名 TXT に Prompt を保存」を有効にすると、画像の横に `.txt` が作成されます。

## 設定とファイル配置
| 項目 | 既定 | 備考 |
| --- | --- | --- |
| 出力フォルダー | `.\output` | 設定で変更可能 |
| Wildcard フォルダー | `.\wildcard` | `*.txt` 断片を保存 |
| ランダム提示詞フォルダー | `.\prompt\prompt_by_风吟` | `*.txt` を保存 |
| プリセット | `C:\Users\Public\Documents\auto_nai3_2\*.toml` | プリセット保存 |
| システム設定 | `C:\Users\Public\Documents\auto_nai3_system\config.toml` | Token、休眠設定など |
| ログ | `logs/mylog.txt` | log4net 出力 |

## 自動更新
非デバッグモード起動時に GitHub Releases から更新確認を行います。

## よくある質問
1. Token が無効/リクエスト失敗  
   Token の期限と NovelAI への接続を確認。

2. Wildcard/ランダム提示詞が動作しない  
   フォルダーが存在し `.txt` があること、パスが空でないことを確認。

3. プレビューが出ないが保存はされる  
   WebP のプレビュー復号に失敗している可能性。保存は行われます。

4. 設定が保存できない  
   `C:\Users\Public\Documents\` への書き込み権限を確認。

## 開発とビルド
- 依存：Visual Studio 2022 + .NET Framework 4.8
- `AutoNai3Tools.sln` を開き、NuGet を復元して `Release` ビルド。
- プロジェクト構成：
  - `controllers/` 生成フローとディレクターツール制御
  - `services/` 設定と Wildcard サービス
  - `utils/` リクエスト、ログ、Prompt 解析、Vibe 処理
  - `body/` モデル別のリクエストボディ

## 関連リンク
- 利用ガイド：<https://cyanautumn.github.io/NovalAi3AutoMaticDoc/>
- NovelAI 公式ドキュメント：<https://docs.novelai.net/en/image/>
- NovelAI 公式モデル一覧：<https://docs.novelai.net/en/image/models>
- Prompt 解析：<https://spell.novelai.dev/>
- WD-Tagger：<https://huggingface.co/spaces/SmilingWolf/wd-tagger>

## ライセンス
ライセンスは未指定です。
