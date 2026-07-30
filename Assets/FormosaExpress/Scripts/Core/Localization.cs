using System;
using System.Collections.Generic;

namespace FormosaExpress.Core
{
    public enum Language
    {
        English,
        Japanese,
        SimplifiedChinese,
        TraditionalChinese
    }

    /// <summary>
    /// Every player-facing string, switchable at runtime from the pause menu and persisted in
    /// the save file. Keyed on the English text itself rather than an opaque ID, so a string
    /// nobody has translated yet quietly falls back to readable English instead of a blank box.
    /// Formatted strings keep their "{0}"-style placeholders as part of the key.
    /// </summary>
    public static class Localization
    {
        public static Language Current { get; private set; } = Language.English;

        /// <summary>Fires after <see cref="Current"/> changes, so built UI can re-stamp its
        /// static captions without a full rebuild.</summary>
        public static event Action Changed;

        public static void SetLanguage(Language language)
        {
            if (Current == language) return;
            Current = language;
            Changed?.Invoke();
        }

        public static void Toggle()
        {
            switch (Current)
            {
                case Language.English: SetLanguage(Language.Japanese); break;
                case Language.Japanese: SetLanguage(Language.SimplifiedChinese); break;
                case Language.SimplifiedChinese: SetLanguage(Language.TraditionalChinese); break;
                default: SetLanguage(Language.English); break;
            }
        }

        public static string T(string english)
        {
            Dictionary<string, string> dict = DictFor(Current);
            if (dict != null && dict.TryGetValue(english, out string value)) return value;
            return english;
        }

        static Dictionary<string, string> DictFor(Language language)
        {
            switch (language)
            {
                case Language.Japanese: return _ja;
                case Language.SimplifiedChinese: return _zhHans;
                case Language.TraditionalChinese: return _zhHant;
                default: return null;
            }
        }

        static readonly Dictionary<string, string> _ja = new Dictionary<string, string>
        {
            // ---- Title ----
            ["NIGHT MARKET DELIVERY"] = "夜市デリバリー",
            ["FIRST DAY ON THE JOB"] = "初出勤",
            ["BEST SCORE {0}     DELIVERIES {1}     CASH {2}{3}"] = "ベストスコア {0}　　配達数 {1}　　所持金 {2}{3}",
            ["     RACES {0}-{1}"] = "　　戦績 {0}勝{1}敗",
            ["Work the shifts. Beat the cash quota before the clock runs out."] = "シフトをこなせ。制限時間内にノルマ金額を稼げ。",
            ["Head to head with another courier for the same orders. First to five wins."] = "同じ注文をライバル配達員と奪い合え。先に5件届けた方が勝ち。",
            ["W / S  choose        ENTER  go"] = "W / S  選択        ENTER  決定",
            ["W / S  throttle & brake        A / D  steer        SPACE  drift        SHIFT  boost\nTAB  switch job        E  horn        C  camera        ESC  pause"]
                = "W / S  アクセル / ブレーキ        A / D  ステアリング        SPACE  ドリフト        SHIFT  ブースト\nTAB  配達先切替        E  クラクション        C  カメラ切替        ESC  ポーズ",

            // ---- Briefing ----
            ["SHIFT"] = "シフト",
            ["Earn {0} before the clock runs out."] = "制限時間内に{0}を稼げ。",
            ["TIME ON SHIFT"] = "制限時間",
            ["BAG CAPACITY"] = "バッグ容量",
            ["ENTER  to start riding"] = "ENTER  で出発",
            ["{0} is working the same streets tonight."] = "{0}も今夜、同じ通りで働いている。",
            ["You are both bidding for the same orders, and only one of you\ncan take each one. Whoever reaches the shop first gets it."]
                = "二人とも同じ注文を狙っており、取れるのは早い者勝ち。\n先に店に着いた方がその注文を獲得する。",
            ["FIRST TO            {0} deliveries"] = "先着                  {0} 件配達",
            ["TIME LIMIT          {0}"] = "制限時間              {0}",
            ["RIVAL SKILL         shift {0} pace"] = "ライバルの実力        シフト{0}相当",
            ["Rush hour. Traffic will not wait for you."] = "ラッシュアワー。交通はお前を待ってくれない。",
            ["The night market is filling up. Watch the pavements."] = "夜市が混み始めた。歩道に気をつけろ。",
            ["Rain earlier means slick asphalt. Brake sooner."] = "さっきまでの雨で路面がぬれている。早めにブレーキを。",
            ["Dispatch is stacking orders tonight. Plan your route."] = "今夜は注文が山積みだ。ルートをよく考えろ。",
            ["Every regular is ordering at once. Go."] = "常連客が一斉に注文している。行け。",
            ["Take the alleys. The main road is jammed."] = "裏路地を使え。大通りは渋滞中だ。",
            ["The lanterns are up. So are expectations."] = "提灯が灯った。期待値も上がっている。",
            ["Late shift. Half the city is hungry."] = "深夜シフト。街の半分が腹を空かせている。",

            // ---- Pause / Controls ----
            ["PAUSED"] = "ポーズ",
            ["RESUME"] = "再開",
            ["CONTROLS"] = "操作方法",
            ["RESTART SHIFT"] = "シフトをやり直す",
            ["QUIT TO TITLE"] = "タイトルに戻る",
            ["LANGUAGE: ENGLISH"] = "言語: ENGLISH",
            ["LANGUAGE: 日本語"] = "言語: 日本語",
            ["ENTER / ESC  back"] = "ENTER / ESC  戻る",
            ["W / S               throttle & brake"] = "W / S               アクセル / ブレーキ",
            ["A / D               steer"] = "A / D               ステアリング",
            ["SPACE               drift"] = "SPACE               ドリフト",
            ["LEFT SHIFT          boost"] = "LEFT SHIFT          ブースト",
            ["TAB                 switch delivery job"] = "TAB                 配達先の切り替え",
            ["E                   horn"] = "E                   クラクション",
            ["Q                   look back"] = "Q                   後方確認",
            ["C                   change camera"] = "C                   カメラ切り替え",
            ["R                   restart shift"] = "R                   シフトのやり直し",
            ["ESC                 pause"] = "ESC                 ポーズ",

            // ---- Results ----
            ["SHIFT COMPLETE"] = "シフト完了",
            ["SHIFT OVER"] = "営業終了",
            ["\nQuota met. Shift {0} unlocked."] = "\nノルマ達成。シフト{0}解放。",
            ["\nQuota met."] = "\nノルマ達成。",
            ["\nQuota missed. The dispatcher is not impressed - try again."] = "\nノルマ未達成。ディスパッチャーはご立腹だ。もう一度。",
            ["EARNINGS"] = "収益",
            ["SCORE"] = "スコア",
            ["DELIVERED"] = "配達数",
            ["PERFECT"] = "パーフェクト",
            ["EXPIRED"] = "失効",
            ["BEST COMBO"] = "ベストコンボ",
            ["NEAR MISSES"] = "ニアミス数",
            ["TOP SPEED"] = "最高速度",
            ["WALLET"] = "所持金",
            ["ENTER  to visit the garage"] = "ENTER  でガレージへ",
            ["YOU WIN"] = "勝利",
            ["YOU LOSE"] = "敗北",
            ["Settled on earnings."] = "獲得金額で決着。",
            ["YOU"] = "自分",
            ["DELIVERIES"] = "配達数",

            // ---- Garage ----
            ["GARAGE"] = "ガレージ",
            ["ENGINE"] = "エンジン",
            ["TYRES"] = "タイヤ",
            ["SUSPENSION"] = "サスペンション",
            ["DELIVERY BAG"] = "配達バッグ",
            ["ADRENALINE TANK"] = "アドレナリンタンク",
            ["Higher top speed and quicker pull away from the lights."] = "最高速度が上がり、発進も速くなる。",
            ["More grip through corners, stronger brakes."] = "コーナリングのグリップとブレーキ性能が向上する。",
            ["Softer landings; kerbs and knocks cost you less."] = "着地が柔らかくなり、縁石や衝突のダメージが減る。",
            ["Carry more orders at once and keep the food intact."] = "同時に運べる注文数が増え、料理も傷みにくくなる。",
            ["A bigger tank, so boost lasts longer."] = "タンクが大きくなり、ブーストが長持ちする。",
            ["MAX"] = "MAX",
            ["W / S  choose        ENTER  buy        TAB  head out for the next shift"]
                = "W / S  選択        ENTER  購入        TAB  次のシフトへ",

            // ---- HUD ----
            ["QUOTA"] = "ノルマ",
            ["TAKEN"] = "確保済み",
            ["TO"] = "届け先",
            ["PICK UP"] = "受け取り",
            ["BOOST"] = "ブースト",
            ["DESTINATION"] = "目的地",
            ["BAG EMPTY"] = "バッグ空",
            ["BAG"] = "バッグ",
            ["W/S throttle & brake   A/D steer   SPACE drift   SHIFT boost   TAB switch job   E horn   C camera"]
                = "W/S アクセル/ブレーキ   A/D ステアリング   SPACE ドリフト   SHIFT ブースト   TAB 配達先切替   E クラクション   C カメラ",
            ["RACE  ·  FIRST TO 5"] = "レース　・　先着5件",

            // ---- Toasts ----
            ["RACE  ·  FIRST TO {0} DELIVERIES"] = "レース　・　先着{0}件配達",
            ["SHIFT {0} - GO"] = "シフト{0}　スタート",
            ["30 SECONDS"] = "残り30秒",
            ["YOU WIN!"] = "勝利！",
            ["BEATEN TO IT"] = "先を越された",
            ["CRASH!"] = "クラッシュ！",
            ["WATCH THE PAVEMENT!"] = "前方注意！",
            ["BACK ON THE ROAD"] = "復帰！",
            ["NEW ORDER  ·  {0}"] = "新規注文　・　{0}",
            ["{0}  ·  TO {1}"] = "{0}　・　届け先 {1}",
            ["GOOD"] = "グッド",
            ["MESSY"] = "崩れ",
            ["RUINED"] = "全壊",
            ["{0} DELIVERY   +{1}"] = "{0}配達　　+{1}",
            ["QUOTA MET - KEEP GOING FOR BONUS"] = "ノルマ達成 - ボーナスのため継続中",
            ["ORDER LOST  ·  -{0}"] = "注文失効　・　-{0}",
            ["SLOW DOWN TO DELIVER"] = "減速して配達しよう",
            ["SLOW DOWN TO COLLECT"] = "減速して受け取ろう",
            ["BAG FULL - DELIVER FIRST"] = "バッグ満杯 - 先に配達しよう",
            ["{0} DELIVERED  ({1})"] = "{0} 配達完了　({1})",
            ["{0} TOOK {1}"] = "{0} が {1} を確保",

            // ---- Combo popups ----
            ["NEAR MISS"] = "ニアミス",
            ["DRIFT"] = "ドリフト",
            ["AIRBORNE"] = "エア中",
            ["DELIVERED!"] = "配達完了！",
            ["CRASH  -{0}"] = "クラッシュ　-{0}",

            // ---- Loading screen ----
            ["LOADING"] = "ロード中",
            ["Laying out the streets..."] = "街路を敷設中...",
            ["Building traffic..."] = "交通を生成中...",
            ["Synthesising audio..."] = "音声を合成中...",
            ["Assembling the HUD..."] = "HUDを構築中...",

            // ---- Food shops ----
            ["Taipei Cafe"] = "台北カフェ",
            ["Ah-Po Braised"] = "阿婆の煮込み",
            ["Boba Palace"] = "タピオカ宮殿",
            ["Shilin Fried Chicken"] = "士林大鶏排",
            ["Formosa Noodle"] = "フォルモサ麺",
            ["Jade Dumpling"] = "翡翠餃子",
            ["Ximen Bakery"] = "西門ベーカリー",
            ["Beitou Hot Pot"] = "北投火鍋",
            ["Tamsui Fish Ball"] = "淡水フィッシュボール",
            ["Sun Moon Tea"] = "日月茶",
            ["Golden Pork Rice"] = "黄金滷肉飯",
            ["Night Market Grill"] = "夜市炭火焼",
            ["Lucky Scallion Pancake"] = "吉祥葱油餅",
            ["Maokong Tea House"] = "猫空茶房",
            ["Keelung Seafood"] = "基隆海鮮",
            ["Zhongshan Sushi"] = "中山寿司",
            ["Da'an Curry"] = "大安カレー",
            ["Longshan Vegetarian"] = "龍山素食",
            ["Pearl Milk Lab"] = "パールミルク研究所",
            ["Bitan Beef Noodle"] = "碧潭牛肉麺",
            ["Uncle Wu Snacks"] = "呉おじさんの屋台",
            ["Sanchong Breakfast"] = "三重早餐店",
            ["Neon Ramen"] = "ネオンラーメン",
            ["Sugar Cane Stand"] = "サトウキビジュース屋台",
            ["Auntie Kuo Dumplings"] = "郭おばさんの水餃子",
            ["Lantern Street BBQ"] = "提灯通り焼肉",
            ["Double Happiness Buns"] = "双喜まんじゅう",
            ["Wanhua Wonton"] = "萬華ワンタン",

            // ---- Residences ----
            ["Chen Residence"] = "陳邸",
            ["Lin Family, 4F"] = "林家 4階",
            ["Apt 12B"] = "アパート12B",
            ["Ms. Kuo, 3F"] = "郭さん宅 3階",
            ["Wang Household"] = "王家",
            ["Hsu Residence, 6F"] = "許邸 6階",
            ["Yang Family"] = "楊家",
            ["Apt 8A"] = "アパート8A",
            ["Tsai Residence"] = "蔡邸",
            ["Old Town Flats"] = "旧市街団地",
            ["Sunrise Apartments"] = "サンライズマンション",
            ["Jasmine Court"] = "ジャスミンコート",
            ["Peony Building, 2F"] = "牡丹ビル 2階",
            ["Bamboo Heights"] = "バンブーハイツ",
            ["Riverside Flats, 5F"] = "リバーサイド団地 5階",
            ["Mr. Ho, 7F"] = "何さん宅 7階",
            ["Cheng Household"] = "鄭家",
            ["Camphor Lane 14"] = "樟樹小径14番",
            ["Plum Blossom, 3F"] = "梅花荘 3階",
            ["Pei Residence"] = "裴邸",

            // ---- Offices ----
            ["Formosa Tech, 9F"] = "フォルモサテック 9階",
            ["Jade Trading Co."] = "翡翠貿易",
            ["Studio Seven"] = "スタジオセブン",
            ["Taipei Print Works"] = "台北印刷工房",
            ["Blue Whale Design"] = "ブルーホエールデザイン",
            ["Hsinchu Semis Office"] = "新竹半導体オフィス",
            ["Lucky Star Logistics"] = "幸運星物流",
            ["Cloud Nine Media"] = "雲上メディア",
            ["Ministry Annex"] = "官庁別館",
            ["Orchid Law Firm"] = "蘭花法律事務所",
            ["Neon Games Studio"] = "ネオンゲームズスタジオ",
            ["Dragon Bank, 12F"] = "龍銀行 12階",

            // ---- Landmarks ----
            ["Temple Gate"] = "廟の門",
            ["Night Market Arch"] = "夜市牌楼",
            ["Old Well Plaza"] = "古井広場",
            ["Bus Depot"] = "バス車庫",
            ["Community Centre"] = "コミュニティセンター",
            ["Riverside Shrine"] = "河畔の祠",
            ["Public Bathhouse"] = "公衆浴場",

            // ---- Dishes ----
            ["Bubble Tea"] = "タピオカミルクティー",
            ["Braised Pork Rice"] = "滷肉飯",
            ["Beef Noodle Soup"] = "牛肉麺",
            ["Xiao Long Bao"] = "小籠包",
            ["Fried Chicken Cutlet"] = "大鶏排",
            ["Scallion Pancake"] = "葱油餅",
            ["Oyster Omelette"] = "牡蠣オムレツ",
            ["Pineapple Cake"] = "パイナップルケーキ",
            ["Stinky Tofu"] = "臭豆腐",
            ["Mango Shaved Ice"] = "マンゴーかき氷",
            ["Sesame Noodles"] = "胡麻麺",
            ["Pork Buns"] = "肉まん",
            ["Winter Melon Tea"] = "冬瓜茶",
            ["Lu Rou Fan"] = "魯肉飯",
            ["Wonton Soup"] = "ワンタンスープ",
            ["Sweet Potato Balls"] = "さつま芋団子",
            ["Salt & Pepper Squid"] = "塩胡椒イカ",
            ["Coffin Bread"] = "棺材板",
            ["Bamboo Rice"] = "竹筒飯",
            ["Taro Milk"] = "タロイモミルク",

            // ---- Rival names ----
            ["KUAI-KUAI EXPRESS"] = "クァイクァイ急便",
            ["LIGHTNING LU"] = "電光ルー",
            ["TURBO TSAI"] = "ターボ蔡",
            ["MIDNIGHT MA"] = "ミッドナイト馬",
            ["NEON NINJA"] = "ネオン忍者",
            ["TYPHOON YANG"] = "台風ヤン",
            ["GHOST RIDER HO"] = "ゴーストライダー何",
            ["A RIVAL"] = "ライバル",
            ["RIVAL"] = "ライバル",
        };

        static readonly Dictionary<string, string> _zhHans = new Dictionary<string, string>
        {
            // ---- Title ----
            ["NIGHT MARKET DELIVERY"] = "夜市快递",
            ["FIRST DAY ON THE JOB"] = "初次上岗",
            ["BEST SCORE {0}     DELIVERIES {1}     CASH {2}{3}"] = "最高分 {0}     配送数 {1}     现金 {2}{3}",
            ["     RACES {0}-{1}"] = "     战绩 {0}胜{1}负",
            ["Work the shifts. Beat the cash quota before the clock runs out."] = "完成班次任务，在时间耗尽前达成现金目标。",
            ["Head to head with another courier for the same orders. First to five wins."] = "与另一位骑手争抢同样的订单，先送达5单者获胜。",
            ["W / S  choose        ENTER  go"] = "W / S  选择        ENTER  确定",
            ["W / S  throttle & brake        A / D  steer        SPACE  drift        SHIFT  boost\nTAB  switch job        E  horn        C  camera        ESC  pause"]
                = "W / S  油门与刹车        A / D  转向        SPACE  漂移        SHIFT  加速\nTAB  切换订单        E  喇叭        C  镜头        ESC  暂停",

            // ---- Briefing ----
            ["SHIFT"] = "班次",
            ["Earn {0} before the clock runs out."] = "在时间耗尽前赚到{0}。",
            ["TIME ON SHIFT"] = "班次时限",
            ["BAG CAPACITY"] = "背包容量",
            ["ENTER  to start riding"] = "ENTER  开始骑行",
            ["{0} is working the same streets tonight."] = "{0}今晚也在同样的街道上跑单。",
            ["You are both bidding for the same orders, and only one of you\ncan take each one. Whoever reaches the shop first gets it."]
                = "你们两人都在争抢同样的订单，每一单只有一人能接。\n谁先到店谁就拿下这一单。",
            ["FIRST TO            {0} deliveries"] = "先达成            {0} 单配送",
            ["TIME LIMIT          {0}"] = "时间限制            {0}",
            ["RIVAL SKILL         shift {0} pace"] = "对手实力            相当于班次{0}",
            ["Rush hour. Traffic will not wait for you."] = "高峰时段，车流不会等你。",
            ["The night market is filling up. Watch the pavements."] = "夜市开始热闹起来，注意人行道。",
            ["Rain earlier means slick asphalt. Brake sooner."] = "刚下过雨，路面湿滑，提早刹车。",
            ["Dispatch is stacking orders tonight. Plan your route."] = "调度台今晚订单堆积如山，规划好路线。",
            ["Every regular is ordering at once. Go."] = "老顾客们都在同时下单，出发吧。",
            ["Take the alleys. The main road is jammed."] = "走小巷，大马路塞车了。",
            ["The lanterns are up. So are expectations."] = "灯笼挂起来了，期望值也随之升高。",
            ["Late shift. Half the city is hungry."] = "深夜班次，半座城市都饿了。",

            // ---- Pause / Controls ----
            ["PAUSED"] = "已暂停",
            ["RESUME"] = "继续",
            ["CONTROLS"] = "操作说明",
            ["RESTART SHIFT"] = "重新开始班次",
            ["QUIT TO TITLE"] = "返回标题",
            ["LANGUAGE: ENGLISH"] = "语言: ENGLISH",
            ["LANGUAGE: 简体中文"] = "语言: 简体中文",
            ["ENTER / ESC  back"] = "ENTER / ESC  返回",
            ["W / S               throttle & brake"] = "W / S               油门与刹车",
            ["A / D               steer"] = "A / D               转向",
            ["SPACE               drift"] = "SPACE               漂移",
            ["LEFT SHIFT          boost"] = "LEFT SHIFT          加速",
            ["TAB                 switch delivery job"] = "TAB                 切换配送订单",
            ["E                   horn"] = "E                   喇叭",
            ["Q                   look back"] = "Q                   回头看",
            ["C                   change camera"] = "C                   切换镜头",
            ["R                   restart shift"] = "R                   重新开始班次",
            ["ESC                 pause"] = "ESC                 暂停",

            // ---- Results ----
            ["SHIFT COMPLETE"] = "班次完成",
            ["SHIFT OVER"] = "班次结束",
            ["\nQuota met. Shift {0} unlocked."] = "\n已达成目标。解锁班次{0}。",
            ["\nQuota met."] = "\n已达成目标。",
            ["\nQuota missed. The dispatcher is not impressed - try again."] = "\n未达成目标。调度员很不满意——再试一次。",
            ["EARNINGS"] = "收入",
            ["SCORE"] = "分数",
            ["DELIVERED"] = "已配送",
            ["PERFECT"] = "完美",
            ["EXPIRED"] = "已过期",
            ["BEST COMBO"] = "最佳连击",
            ["NEAR MISSES"] = "险些相撞次数",
            ["TOP SPEED"] = "最高速度",
            ["WALLET"] = "钱包",
            ["ENTER  to visit the garage"] = "ENTER  前往车库",
            ["YOU WIN"] = "你赢了",
            ["YOU LOSE"] = "你输了",
            ["Settled on earnings."] = "以收入决胜负。",
            ["YOU"] = "你",
            ["DELIVERIES"] = "配送数",

            // ---- Garage ----
            ["GARAGE"] = "车库",
            ["ENGINE"] = "引擎",
            ["TYRES"] = "轮胎",
            ["SUSPENSION"] = "悬挂",
            ["DELIVERY BAG"] = "配送背包",
            ["ADRENALINE TANK"] = "肾上腺素槽",
            ["Higher top speed and quicker pull away from the lights."] = "更高的极速，起步加速也更快。",
            ["More grip through corners, stronger brakes."] = "过弯抓地力更强，刹车更有力。",
            ["Softer landings; kerbs and knocks cost you less."] = "落地更柔和，路缘和碰撞造成的伤害更小。",
            ["Carry more orders at once and keep the food intact."] = "可同时携带更多订单，食物也更不易损坏。",
            ["A bigger tank, so boost lasts longer."] = "更大的储量，加速效果持续更久。",
            ["MAX"] = "满级",
            ["W / S  choose        ENTER  buy        TAB  head out for the next shift"]
                = "W / S  选择        ENTER  购买        TAB  前往下一班次",

            // ---- HUD ----
            ["QUOTA"] = "目标额",
            ["TAKEN"] = "已被抢先",
            ["TO"] = "送往",
            ["PICK UP"] = "取货",
            ["BOOST"] = "加速",
            ["DESTINATION"] = "目的地",
            ["BAG EMPTY"] = "背包空空",
            ["BAG"] = "背包",
            ["W/S throttle & brake   A/D steer   SPACE drift   SHIFT boost   TAB switch job   E horn   C camera"]
                = "W/S 油门/刹车   A/D 转向   SPACE 漂移   SHIFT 加速   TAB 切换订单   E 喇叭   C 镜头",
            ["RACE  ·  FIRST TO 5"] = "竞速　・　先达5单",

            // ---- Toasts ----
            ["RACE  ·  FIRST TO {0} DELIVERIES"] = "竞速　・　先达{0}单",
            ["SHIFT {0} - GO"] = "班次{0}　开始",
            ["30 SECONDS"] = "剩余30秒",
            ["YOU WIN!"] = "你赢了！",
            ["BEATEN TO IT"] = "被抢先了",
            ["CRASH!"] = "撞车！",
            ["WATCH THE PAVEMENT!"] = "注意行人道！",
            ["BACK ON THE ROAD"] = "重新上路！",
            ["NEW ORDER  ·  {0}"] = "新订单　・　{0}",
            ["{0}  ·  TO {1}"] = "{0}　・　送往 {1}",
            ["GOOD"] = "完好",
            ["MESSY"] = "散乱",
            ["RUINED"] = "全毁",
            ["{0} DELIVERY   +{1}"] = "{0}配送　　+{1}",
            ["QUOTA MET - KEEP GOING FOR BONUS"] = "已达标 - 继续赚取奖金",
            ["ORDER LOST  ·  -{0}"] = "订单失效　・　-{0}",
            ["SLOW DOWN TO DELIVER"] = "减速以完成配送",
            ["SLOW DOWN TO COLLECT"] = "减速以取货",
            ["BAG FULL - DELIVER FIRST"] = "背包已满 - 请先配送",
            ["{0} DELIVERED  ({1})"] = "{0} 已配送　({1})",
            ["{0} TOOK {1}"] = "{0} 抢走了 {1}",

            // ---- Combo popups ----
            ["NEAR MISS"] = "险些相撞",
            ["DRIFT"] = "漂移",
            ["AIRBORNE"] = "腾空",
            ["DELIVERED!"] = "配送完成！",
            ["CRASH  -{0}"] = "撞车　-{0}",

            // ---- Loading screen ----
            ["LOADING"] = "加载中",
            ["Laying out the streets..."] = "正在铺设街道...",
            ["Building traffic..."] = "正在生成车流...",
            ["Synthesising audio..."] = "正在合成音频...",
            ["Assembling the HUD..."] = "正在构建界面...",

            // ---- Food shops ----
            ["Taipei Cafe"] = "台北咖啡馆",
            ["Ah-Po Braised"] = "阿婆卤味",
            ["Boba Palace"] = "珍珠奶茶宫",
            ["Shilin Fried Chicken"] = "士林大鸡排",
            ["Formosa Noodle"] = "福尔摩沙面馆",
            ["Jade Dumpling"] = "翡翠饺子",
            ["Ximen Bakery"] = "西门烘焙坊",
            ["Beitou Hot Pot"] = "北投火锅",
            ["Tamsui Fish Ball"] = "淡水鱼丸",
            ["Sun Moon Tea"] = "日月茶坊",
            ["Golden Pork Rice"] = "黄金卤肉饭",
            ["Night Market Grill"] = "夜市烧烤",
            ["Lucky Scallion Pancake"] = "吉祥葱油饼",
            ["Maokong Tea House"] = "猫空茶馆",
            ["Keelung Seafood"] = "基隆海鲜",
            ["Zhongshan Sushi"] = "中山寿司",
            ["Da'an Curry"] = "大安咖喱",
            ["Longshan Vegetarian"] = "龙山素食",
            ["Pearl Milk Lab"] = "珍珠奶茶实验室",
            ["Bitan Beef Noodle"] = "碧潭牛肉面",
            ["Uncle Wu Snacks"] = "吴叔小吃",
            ["Sanchong Breakfast"] = "三重早餐店",
            ["Neon Ramen"] = "霓虹拉面",
            ["Sugar Cane Stand"] = "甘蔗汁摊",
            ["Auntie Kuo Dumplings"] = "郭阿姨水饺",
            ["Lantern Street BBQ"] = "灯笼街烧烤",
            ["Double Happiness Buns"] = "双喜包子",
            ["Wanhua Wonton"] = "万华馄饨",

            // ---- Residences ----
            ["Chen Residence"] = "陈宅",
            ["Lin Family, 4F"] = "林家 4楼",
            ["Apt 12B"] = "12B公寓",
            ["Ms. Kuo, 3F"] = "郭小姐 3楼",
            ["Wang Household"] = "王家",
            ["Hsu Residence, 6F"] = "许宅 6楼",
            ["Yang Family"] = "杨家",
            ["Apt 8A"] = "8A公寓",
            ["Tsai Residence"] = "蔡宅",
            ["Old Town Flats"] = "旧城公寓",
            ["Sunrise Apartments"] = "旭日公寓",
            ["Jasmine Court"] = "茉莉苑",
            ["Peony Building, 2F"] = "牡丹大楼 2楼",
            ["Bamboo Heights"] = "翠竹苑",
            ["Riverside Flats, 5F"] = "河畔公寓 5楼",
            ["Mr. Ho, 7F"] = "何先生 7楼",
            ["Cheng Household"] = "郑家",
            ["Camphor Lane 14"] = "樟树巷14号",
            ["Plum Blossom, 3F"] = "梅花苑 3楼",
            ["Pei Residence"] = "裴宅",

            // ---- Offices ----
            ["Formosa Tech, 9F"] = "福尔摩沙科技 9楼",
            ["Jade Trading Co."] = "翡翠贸易公司",
            ["Studio Seven"] = "第七工作室",
            ["Taipei Print Works"] = "台北印刷厂",
            ["Blue Whale Design"] = "蓝鲸设计",
            ["Hsinchu Semis Office"] = "新竹半导体办公室",
            ["Lucky Star Logistics"] = "幸运星物流",
            ["Cloud Nine Media"] = "云端传媒",
            ["Ministry Annex"] = "部会别馆",
            ["Orchid Law Firm"] = "兰花律师事务所",
            ["Neon Games Studio"] = "霓虹游戏工作室",
            ["Dragon Bank, 12F"] = "龙银行 12楼",

            // ---- Landmarks ----
            ["Temple Gate"] = "庙门",
            ["Night Market Arch"] = "夜市牌楼",
            ["Old Well Plaza"] = "古井广场",
            ["Bus Depot"] = "公车总站",
            ["Community Centre"] = "社区中心",
            ["Riverside Shrine"] = "河畔小庙",
            ["Public Bathhouse"] = "公共浴池",

            // ---- Dishes ----
            ["Bubble Tea"] = "珍珠奶茶",
            ["Braised Pork Rice"] = "卤肉饭",
            ["Beef Noodle Soup"] = "牛肉面",
            ["Xiao Long Bao"] = "小笼包",
            ["Fried Chicken Cutlet"] = "大鸡排",
            ["Scallion Pancake"] = "葱油饼",
            ["Oyster Omelette"] = "蚵仔煎",
            ["Pineapple Cake"] = "凤梨酥",
            ["Stinky Tofu"] = "臭豆腐",
            ["Mango Shaved Ice"] = "芒果刨冰",
            ["Sesame Noodles"] = "麻酱面",
            ["Pork Buns"] = "肉包",
            ["Winter Melon Tea"] = "冬瓜茶",
            ["Lu Rou Fan"] = "鲁肉饭",
            ["Wonton Soup"] = "馄饨汤",
            ["Sweet Potato Balls"] = "地瓜球",
            ["Salt & Pepper Squid"] = "椒盐鱿鱼",
            ["Coffin Bread"] = "棺材板",
            ["Bamboo Rice"] = "竹筒饭",
            ["Taro Milk"] = "芋头牛奶",

            // ---- Rival names ----
            ["KUAI-KUAI EXPRESS"] = "快快快递",
            ["LIGHTNING LU"] = "闪电卢",
            ["TURBO TSAI"] = "涡轮蔡",
            ["MIDNIGHT MA"] = "午夜马",
            ["NEON NINJA"] = "霓虹忍者",
            ["TYPHOON YANG"] = "台风杨",
            ["GHOST RIDER HO"] = "幽灵骑士何",
            ["A RIVAL"] = "对手",
            ["RIVAL"] = "对手",
        };

        static readonly Dictionary<string, string> _zhHant = new Dictionary<string, string>
        {
            // ---- Title ----
            ["NIGHT MARKET DELIVERY"] = "夜市快遞",
            ["FIRST DAY ON THE JOB"] = "初次上崗",
            ["BEST SCORE {0}     DELIVERIES {1}     CASH {2}{3}"] = "最高分 {0}     配送數 {1}     現金 {2}{3}",
            ["     RACES {0}-{1}"] = "     戰績 {0}勝{1}負",
            ["Work the shifts. Beat the cash quota before the clock runs out."] = "完成班次任務，在時間耗盡前達成現金目標。",
            ["Head to head with another courier for the same orders. First to five wins."] = "與另一位騎士爭搶同樣的訂單，先送達5單者獲勝。",
            ["W / S  choose        ENTER  go"] = "W / S  選擇        ENTER  確定",
            ["W / S  throttle & brake        A / D  steer        SPACE  drift        SHIFT  boost\nTAB  switch job        E  horn        C  camera        ESC  pause"]
                = "W / S  油門與煞車        A / D  轉向        SPACE  漂移        SHIFT  加速\nTAB  切換訂單        E  喇叭        C  鏡頭        ESC  暫停",

            // ---- Briefing ----
            ["SHIFT"] = "班次",
            ["Earn {0} before the clock runs out."] = "在時間耗盡前賺到{0}。",
            ["TIME ON SHIFT"] = "班次時限",
            ["BAG CAPACITY"] = "背包容量",
            ["ENTER  to start riding"] = "ENTER  開始騎行",
            ["{0} is working the same streets tonight."] = "{0}今晚也在同樣的街道上跑單。",
            ["You are both bidding for the same orders, and only one of you\ncan take each one. Whoever reaches the shop first gets it."]
                = "你們兩人都在爭搶同樣的訂單，每一單只有一人能接。\n誰先到店誰就拿下這一單。",
            ["FIRST TO            {0} deliveries"] = "先達成            {0} 單配送",
            ["TIME LIMIT          {0}"] = "時間限制            {0}",
            ["RIVAL SKILL         shift {0} pace"] = "對手實力            相當於班次{0}",
            ["Rush hour. Traffic will not wait for you."] = "尖峰時段，車流不會等你。",
            ["The night market is filling up. Watch the pavements."] = "夜市開始熱鬧起來，注意人行道。",
            ["Rain earlier means slick asphalt. Brake sooner."] = "剛下過雨，路面濕滑，提早煞車。",
            ["Dispatch is stacking orders tonight. Plan your route."] = "調度台今晚訂單堆積如山，規劃好路線。",
            ["Every regular is ordering at once. Go."] = "老顧客們都在同時下單，出發吧。",
            ["Take the alleys. The main road is jammed."] = "走小巷，大馬路塞車了。",
            ["The lanterns are up. So are expectations."] = "燈籠掛起來了，期望值也隨之升高。",
            ["Late shift. Half the city is hungry."] = "深夜班次，半座城市都餓了。",

            // ---- Pause / Controls ----
            ["PAUSED"] = "已暫停",
            ["RESUME"] = "繼續",
            ["CONTROLS"] = "操作說明",
            ["RESTART SHIFT"] = "重新開始班次",
            ["QUIT TO TITLE"] = "返回標題",
            ["LANGUAGE: ENGLISH"] = "語言: ENGLISH",
            ["LANGUAGE: 繁體中文"] = "語言: 繁體中文",
            ["ENTER / ESC  back"] = "ENTER / ESC  返回",
            ["W / S               throttle & brake"] = "W / S               油門與煞車",
            ["A / D               steer"] = "A / D               轉向",
            ["SPACE               drift"] = "SPACE               漂移",
            ["LEFT SHIFT          boost"] = "LEFT SHIFT          加速",
            ["TAB                 switch delivery job"] = "TAB                 切換配送訂單",
            ["E                   horn"] = "E                   喇叭",
            ["Q                   look back"] = "Q                   回頭看",
            ["C                   change camera"] = "C                   切換鏡頭",
            ["R                   restart shift"] = "R                   重新開始班次",
            ["ESC                 pause"] = "ESC                 暫停",

            // ---- Results ----
            ["SHIFT COMPLETE"] = "班次完成",
            ["SHIFT OVER"] = "班次結束",
            ["\nQuota met. Shift {0} unlocked."] = "\n已達成目標。解鎖班次{0}。",
            ["\nQuota met."] = "\n已達成目標。",
            ["\nQuota missed. The dispatcher is not impressed - try again."] = "\n未達成目標。調度員很不滿意——再試一次。",
            ["EARNINGS"] = "收入",
            ["SCORE"] = "分數",
            ["DELIVERED"] = "已配送",
            ["PERFECT"] = "完美",
            ["EXPIRED"] = "已過期",
            ["BEST COMBO"] = "最佳連擊",
            ["NEAR MISSES"] = "險些相撞次數",
            ["TOP SPEED"] = "最高速度",
            ["WALLET"] = "錢包",
            ["ENTER  to visit the garage"] = "ENTER  前往車庫",
            ["YOU WIN"] = "你贏了",
            ["YOU LOSE"] = "你輸了",
            ["Settled on earnings."] = "以收入決勝負。",
            ["YOU"] = "你",
            ["DELIVERIES"] = "配送數",

            // ---- Garage ----
            ["GARAGE"] = "車庫",
            ["ENGINE"] = "引擎",
            ["TYRES"] = "輪胎",
            ["SUSPENSION"] = "懸吊",
            ["DELIVERY BAG"] = "配送背包",
            ["ADRENALINE TANK"] = "腎上腺素槽",
            ["Higher top speed and quicker pull away from the lights."] = "更高的極速，起步加速也更快。",
            ["More grip through corners, stronger brakes."] = "過彎抓地力更強，煞車更有力。",
            ["Softer landings; kerbs and knocks cost you less."] = "落地更柔和，路緣和碰撞造成的傷害更小。",
            ["Carry more orders at once and keep the food intact."] = "可同時攜帶更多訂單，食物也更不易損壞。",
            ["A bigger tank, so boost lasts longer."] = "更大的儲量，加速效果持續更久。",
            ["MAX"] = "滿級",
            ["W / S  choose        ENTER  buy        TAB  head out for the next shift"]
                = "W / S  選擇        ENTER  購買        TAB  前往下一班次",

            // ---- HUD ----
            ["QUOTA"] = "目標額",
            ["TAKEN"] = "已被搶先",
            ["TO"] = "送往",
            ["PICK UP"] = "取貨",
            ["BOOST"] = "加速",
            ["DESTINATION"] = "目的地",
            ["BAG EMPTY"] = "背包空空",
            ["BAG"] = "背包",
            ["W/S throttle & brake   A/D steer   SPACE drift   SHIFT boost   TAB switch job   E horn   C camera"]
                = "W/S 油門/煞車   A/D 轉向   SPACE 漂移   SHIFT 加速   TAB 切換訂單   E 喇叭   C 鏡頭",
            ["RACE  ·  FIRST TO 5"] = "競速　・　先達5單",

            // ---- Toasts ----
            ["RACE  ·  FIRST TO {0} DELIVERIES"] = "競速　・　先達{0}單",
            ["SHIFT {0} - GO"] = "班次{0}　開始",
            ["30 SECONDS"] = "剩餘30秒",
            ["YOU WIN!"] = "你贏了！",
            ["BEATEN TO IT"] = "被搶先了",
            ["CRASH!"] = "撞車！",
            ["WATCH THE PAVEMENT!"] = "注意行人道！",
            ["BACK ON THE ROAD"] = "重新上路！",
            ["NEW ORDER  ·  {0}"] = "新訂單　・　{0}",
            ["{0}  ·  TO {1}"] = "{0}　・　送往 {1}",
            ["GOOD"] = "完好",
            ["MESSY"] = "散亂",
            ["RUINED"] = "全毀",
            ["{0} DELIVERY   +{1}"] = "{0}配送　　+{1}",
            ["QUOTA MET - KEEP GOING FOR BONUS"] = "已達標 - 繼續賺取獎金",
            ["ORDER LOST  ·  -{0}"] = "訂單失效　・　-{0}",
            ["SLOW DOWN TO DELIVER"] = "減速以完成配送",
            ["SLOW DOWN TO COLLECT"] = "減速以取貨",
            ["BAG FULL - DELIVER FIRST"] = "背包已滿 - 請先配送",
            ["{0} DELIVERED  ({1})"] = "{0} 已配送　({1})",
            ["{0} TOOK {1}"] = "{0} 搶走了 {1}",

            // ---- Combo popups ----
            ["NEAR MISS"] = "險些相撞",
            ["DRIFT"] = "漂移",
            ["AIRBORNE"] = "騰空",
            ["DELIVERED!"] = "配送完成！",
            ["CRASH  -{0}"] = "撞車　-{0}",

            // ---- Loading screen ----
            ["LOADING"] = "載入中",
            ["Laying out the streets..."] = "正在鋪設街道...",
            ["Building traffic..."] = "正在生成車流...",
            ["Synthesising audio..."] = "正在合成音訊...",
            ["Assembling the HUD..."] = "正在構建介面...",

            // ---- Food shops ----
            ["Taipei Cafe"] = "台北咖啡館",
            ["Ah-Po Braised"] = "阿婆滷味",
            ["Boba Palace"] = "珍珠奶茶宮",
            ["Shilin Fried Chicken"] = "士林大雞排",
            ["Formosa Noodle"] = "福爾摩沙麵館",
            ["Jade Dumpling"] = "翡翠餃子",
            ["Ximen Bakery"] = "西門烘焙坊",
            ["Beitou Hot Pot"] = "北投火鍋",
            ["Tamsui Fish Ball"] = "淡水魚丸",
            ["Sun Moon Tea"] = "日月茶坊",
            ["Golden Pork Rice"] = "黃金滷肉飯",
            ["Night Market Grill"] = "夜市燒烤",
            ["Lucky Scallion Pancake"] = "吉祥蔥油餅",
            ["Maokong Tea House"] = "貓空茶館",
            ["Keelung Seafood"] = "基隆海鮮",
            ["Zhongshan Sushi"] = "中山壽司",
            ["Da'an Curry"] = "大安咖哩",
            ["Longshan Vegetarian"] = "龍山素食",
            ["Pearl Milk Lab"] = "珍珠奶茶實驗室",
            ["Bitan Beef Noodle"] = "碧潭牛肉麵",
            ["Uncle Wu Snacks"] = "吳叔小吃",
            ["Sanchong Breakfast"] = "三重早餐店",
            ["Neon Ramen"] = "霓虹拉麵",
            ["Sugar Cane Stand"] = "甘蔗汁攤",
            ["Auntie Kuo Dumplings"] = "郭阿姨水餃",
            ["Lantern Street BBQ"] = "燈籠街燒烤",
            ["Double Happiness Buns"] = "雙喜包子",
            ["Wanhua Wonton"] = "萬華餛飩",

            // ---- Residences ----
            ["Chen Residence"] = "陳宅",
            ["Lin Family, 4F"] = "林家 4樓",
            ["Apt 12B"] = "12B公寓",
            ["Ms. Kuo, 3F"] = "郭小姐 3樓",
            ["Wang Household"] = "王家",
            ["Hsu Residence, 6F"] = "許宅 6樓",
            ["Yang Family"] = "楊家",
            ["Apt 8A"] = "8A公寓",
            ["Tsai Residence"] = "蔡宅",
            ["Old Town Flats"] = "舊城公寓",
            ["Sunrise Apartments"] = "旭日公寓",
            ["Jasmine Court"] = "茉莉苑",
            ["Peony Building, 2F"] = "牡丹大樓 2樓",
            ["Bamboo Heights"] = "翠竹苑",
            ["Riverside Flats, 5F"] = "河畔公寓 5樓",
            ["Mr. Ho, 7F"] = "何先生 7樓",
            ["Cheng Household"] = "鄭家",
            ["Camphor Lane 14"] = "樟樹巷14號",
            ["Plum Blossom, 3F"] = "梅花苑 3樓",
            ["Pei Residence"] = "裴宅",

            // ---- Offices ----
            ["Formosa Tech, 9F"] = "福爾摩沙科技 9樓",
            ["Jade Trading Co."] = "翡翠貿易公司",
            ["Studio Seven"] = "第七工作室",
            ["Taipei Print Works"] = "台北印刷廠",
            ["Blue Whale Design"] = "藍鯨設計",
            ["Hsinchu Semis Office"] = "新竹半導體辦公室",
            ["Lucky Star Logistics"] = "幸運星物流",
            ["Cloud Nine Media"] = "雲端傳媒",
            ["Ministry Annex"] = "部會別館",
            ["Orchid Law Firm"] = "蘭花律師事務所",
            ["Neon Games Studio"] = "霓虹遊戲工作室",
            ["Dragon Bank, 12F"] = "龍銀行 12樓",

            // ---- Landmarks ----
            ["Temple Gate"] = "廟門",
            ["Night Market Arch"] = "夜市牌樓",
            ["Old Well Plaza"] = "古井廣場",
            ["Bus Depot"] = "公車總站",
            ["Community Centre"] = "社區中心",
            ["Riverside Shrine"] = "河畔小廟",
            ["Public Bathhouse"] = "公共浴池",

            // ---- Dishes ----
            ["Bubble Tea"] = "珍珠奶茶",
            ["Braised Pork Rice"] = "滷肉飯",
            ["Beef Noodle Soup"] = "牛肉麵",
            ["Xiao Long Bao"] = "小籠包",
            ["Fried Chicken Cutlet"] = "大雞排",
            ["Scallion Pancake"] = "蔥油餅",
            ["Oyster Omelette"] = "蚵仔煎",
            ["Pineapple Cake"] = "鳳梨酥",
            ["Stinky Tofu"] = "臭豆腐",
            ["Mango Shaved Ice"] = "芒果刨冰",
            ["Sesame Noodles"] = "麻醬麵",
            ["Pork Buns"] = "肉包",
            ["Winter Melon Tea"] = "冬瓜茶",
            ["Lu Rou Fan"] = "魯肉飯",
            ["Wonton Soup"] = "餛飩湯",
            ["Sweet Potato Balls"] = "地瓜球",
            ["Salt & Pepper Squid"] = "椒鹽魷魚",
            ["Coffin Bread"] = "棺材板",
            ["Bamboo Rice"] = "竹筒飯",
            ["Taro Milk"] = "芋頭牛奶",

            // ---- Rival names ----
            ["KUAI-KUAI EXPRESS"] = "快快快遞",
            ["LIGHTNING LU"] = "閃電盧",
            ["TURBO TSAI"] = "渦輪蔡",
            ["MIDNIGHT MA"] = "午夜馬",
            ["NEON NINJA"] = "霓虹忍者",
            ["TYPHOON YANG"] = "颱風楊",
            ["GHOST RIDER HO"] = "幽靈騎士何",
            ["A RIVAL"] = "對手",
            ["RIVAL"] = "對手",
        };
    }
}
