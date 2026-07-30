# Formosa Express

台湾の夜市エリアを舞台にした、スクーター配達アーケードゲーム。Unity 6 (URP) 製。
密集した交通の中をスクーターで駆け抜け、屋台から料理を受け取り、時間切れになる前に
客へ届ける ―― ただし料理を潰さないように。

## 遊び方

`Assets/FormosaExpress/Scenes/FormosaExpress.unity` を開いて再生（Play）するだけ。
シーンには GameObject が 1 つしか置かれていない。都市も、スクーターも、交通も、UI も、
音も、すべて `GameBootstrap.Awake()` の中でコードから生成される。

### モード

- **CLOCK ON**（キャリアモード）: 制限時間内にノルマ金額を稼ぐ通常営業。シフトを重ねる
  ごとにノルマ・交通量・時間帯が進行する。
- **RIVAL RACE**（対抗レース）: ライバル配達員 AI と同じ注文を奪い合いながら、
  先に規定件数を届けた方が勝ち。

タイトル画面で W/S（上下）を押してモードを選び、Enter で決定。

### 操作

| キー | 動作 |
| --- | --- |
| W / S | アクセル / ブレーキ |
| A / D | ステアリング |
| Space | ドリフト（メニューでは決定） |
| Left Shift | ブースト |
| Tab | 配達先の切り替え |
| E | クラクション |
| Q | 後方確認 |
| C | カメラ切り替え |
| R | シフトのやり直し |
| Escape | ポーズ |

## アートに関する唯一のルール

**すべてが実行時生成であり、art / audio / prefab アセットは一切存在しない。**

都市、スクーター、交通、すべてのマテリアル、UI スプライト、空、効果音 ―― これらは
すべて `GameBootstrap.Awake()` の中でコードにより組み立てられる。これは意図的な制約で、
コードベース全体の設計方針になっている。

- ジオメトリは `Core/MeshBuilder` で組み立てられ、フラットシェーディングの三角形として
  1 つのメッシュに焼き込まれる。
- 色はマテリアルに持たせない。`Core/Palette` は 64×64 のテクスチャアトラスで、1 ピクセル
  = 1 色。ジオメトリはそのピクセルを指す UV を持つだけ。そのため街区 1 ブロック（建物・
  看板・屋台・駐輪スクーター）が **1 メッシュ・1 マテリアル** に収まり、都市全体が
  数百ドローコールで描画される。
- 音声は `Audio/AudioSynth` がオフラインで `AudioClip` を合成する（エンジン音・クラクショ
  ン・クラッシュ音・コイン音・4 小節の BGM ループ）。
- 看板の文字は疑似 CJK グリフをネオン管のストローク形状として描画している
  （`BuildingFactory.AddGlyph`）ため、正しくブルームがかかる。

## 主なゲームプレイシステム

- **リスクこそが経済。** ニアミス・ドリフト・空中滞在がコンボ倍率を上げ、アドレナリンを
  貯めてブーストの燃料にする。クラッシュはコンボを消し、料理にもダメージを与え（さらに
  実際のペナルティ金額も引かれる）、支払いランクを下げる。速さと稼ぎやすさは必ずしも
  一致しない。
- **ハンドリングは意図的な分業。** 並進はダイナミック（縁石・ジャンプ・衝突がしっかり
  効く）だが、回転は `ScooterController` が直接ヨーを駆動するオーサード方式
  （リジッドボディは `FreezeRotation`）。これがアーケードスクーターらしい「スピンしない」
  操作感を生んでいる。
- **経路探索はピュアパースート方式。** ライダーをルートのポリラインに投影し、そこから
  一定の弧長だけ先を狙う。加えて A* のコストにはターン角のペナルティが乗っており、
  距離が同じならより直進的な経路が優先される。
- **時間帯は 1 つのダイヤルで制御。** `EnvironmentDirector.SetNightFactor` が、明るい
  昼（序盤シフト）→ ゴールデンアワー → 夜（終盤シフト）へと、太陽・環境光・霧・空・
  ブルームをまとめてスライドさせる。街灯や店先の灯りは実ライトではなく積層された
  加算ジオメトリで表現され、実行時コストをかけずに夜のムードを作っている。
- **屋台配達地帯らしい交通密度。** NPC の大半は原付（スクーター）で、車・バス・タクシー
  はその合間を縫う脇役。

## ディレクトリ構成

```
Assets/FormosaExpress/Scripts/
  Core/       MeshBuilder, Palette, MaterialLibrary, TextureFactory, InputRouter,
              SaveSystem, Services（サービスロケータ）, Tuning + Art（バランス調整と
              アートディレクション全般）, GameBootstrap（エントリポイント）
  City/       CityBuilder（道路グラフ・街区・敷地・レーン）, CityModel（データ + A* 経路探索）,
              GroundFactory（道路・縁石・区画線）, BuildingFactory, PropFactory,
              SkylineFactory, CityAssembler（統括）
  Vehicle/    ScooterController（操作性）, ScooterVisual（リグ・アニメーション）
  Traffic/    VehicleFactory（共有メッシュ）, TrafficAgent, TrafficSystem, PedestrianSystem
  Gameplay/   GameDirector（ステートマシン）, OrderManager, ComboSystem, RouteService /
              RouteTracker, RivalBrain / RivalCourier（対抗レース AI）, DeliveryBeacon
  Fx/         ChaseCamera, FxDirector（パーティクル・スキッド）, EnvironmentDirector
              （空・太陽・ポストプロセス）
  Audio/      AudioSynth, AudioDirector
  UI/         UiKit, HudRoot, Minimap, ScreenStack
  Dev/        AutoRider ―― ソークテスト用の自動運転。ゲーム機能ではない
```

`Core/Tuning.cs` にゲームプレイの数値がすべて、`Core/Art.cs`（同ファイル内）に色が
すべて入っている。バランス調整はここを触る。

## 開発ツール

Unity エディタの操作には `unity` CLI を使う（`unity --help` で確認）。

- `unity command eval` / `eval_file` はメソッド本体だけを渡す（`using` 不要、
  `UnityEngine` / `UnityEditor` は暗黙 import 済み）。
- ネストしたオブジェクト引数（`settings={...}` 等）は CLI からは渡せないため、
  プロジェクト設定の変更は `eval_file` を使う。
- `unity command screenshot` はカメラのレンダリングのみで画面上 UI を含まない。
  HUD を含めた撮影には `eval` 経由で `ScreenCapture.CaptureScreenshot(path, 2)` を呼ぶ。
- `InputRouter` の `Scripted*` フィールドを使うと、実機の入力なしに `eval` から
  ゲームを操作できる。`Dev/AutoRider` はこれを使った無人ソークテスト用オートパイロット。
