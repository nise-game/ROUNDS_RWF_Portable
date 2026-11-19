ROUNDS + RoundsWithFriends ポータブル版
=======================================

Thunderstore / r2modman / Overwolf などのソフトを一切使わずに、
このフォルダだけで RoundsWithFriends（最大16人対戦）入りの ROUNDS を遊べるようにしたものです。
mod 本体と設定はすべてこのフォルダの中にあり、ゲームフォルダには小さなファイルを2つ置くだけです。

対応している ROUNDS: v1.1.2（2025年のアップデート後の現行版）
必要なもの: Steam と ROUNDS 本体だけ


■ 使い方（Windows）
1. このフォルダを好きな場所に置く
   （英数字だけのパスがおすすめ。例: C:\Games\ROUNDS_RWF_Portable）
2. install.bat を実行（最初の1回だけ）
3. play.bat で mod 入り ROUNDS が起動
   Steam から普通に起動すると、今まで通り mod なしで遊べます。

- Steam の ROUNDS → プロパティ → ベータ は「なし」（現行版）にしてください。
  旧バージョン（old-rounds-for-mods など）では動きません。
- 一緒に遊ぶ人全員が、このフォルダ（同じ版の zip）を使って起動してください。
  構成が違う人もロビーには入れてしまいますが、まともに遊べません
  （mod なしの人がいると画面が進まない・止まる、RoundsJPSettings だけ無い人がいると
  カードのピックがずれる、など）。
  ロビーでは名前の横に mod がそろっているかが表示されます。
    ✓ ALL MODS SYNCED … 全員そろっている
    ✗ UNMODDED       … mod なしで起動している
    ✗ （mod の名前）  … その mod が足りない
  ✗ の人がいたら、その人に起動し直してもらってください。
  ホストは ESC メニューの KICK MENU から追い出すこともできます。
- フォルダを別の場所に移しても、install.bat をやり直す必要はありません。


■ 使い方（Linux / Steam Deck・Proton）
1. このフォルダを好きな場所に置く（例: ~/Games/ROUNDS_RWF_Portable）
   スペースや日本語を含まないパスにしてください。
2. ターミナルで  bash install.sh  を実行
3. 表示された起動オプションを、Steam の ROUNDS → プロパティ → 起動オプション に貼る
   [A] WINEDLLOVERRIDES="winhttp=n,b" %command%
       → 普段は mod なし。bash play.sh で起動したときだけ mod 入り
   [B] [A] にこのフォルダの場所を足したもの（install.sh が表示します）
       → Steam から起動すると常に mod 入り。Steam Deck のゲームモードではこちらが便利
4. [A] の場合は  bash play.sh  で起動
- zip を Windows で作ったため実行権限が付いていません。「bash ○○.sh」で実行してください。
- Windows の人と同じフォルダの中身なら、そのまま一緒に遊べます。
- ※ Linux / Proton での動作はまだ確認していません。


■ やめるとき
Windows: uninstall.bat を実行して、このフォルダを削除するだけです。
Linux:   bash uninstall.sh を実行して、Steam の起動オプションも消してください。


■ 入っているもの
- BepInExPack_ROUNDS 5.4.1901（mod ローダー。BepInEx 5.4.19）
- UnboundLib 4.2.7（Bknibb 版・ROUNDS v1.1.2 対応 https://github.com/Bknibb/UnboundLib）
    MMHook（MMHOOK_Assembly-CSharp.dll）と Octokit.dll は UnboundLib 4.2.7 に同梱のもの
- RoundsWithFriends 3.0.10（Bknibb 版・ROUNDS v1.1.2 対応 https://github.com/Bknibb/RoundsWithFriends）
- RoundsJPSettings 1.0.0（自作。ソースは source\RoundsJPSettings）

※ Thunderstore にある元の UnboundLib 3.x / RoundsWithFriends 2.2.2 / MMHook 1.0.0 は
  現行版 ROUNDS では動きません（メニューで文字が消えて操作できなくなります）。


■ Rounds With Friends でできること
- ローカル / オンラインのプライベートロビーで最大16人まで対戦
- チームデスマッチ（いろいろなチーム分け）とデスマッチ（バトルロイヤル）
- 試合後に「続ける」か「リマッチ」を選べる


■ RoundsJPSettings でできること
メインメニュー →「MOD」→「RWF 日本語オプション」で変えられます。
BepInEx\config\local.rounds.jpsettings.cfg を直接書き換えても OK です。
（このファイルは初めて起動したときに標準値で作られます。設定はパソコンごとに別々です）

  設定                          範囲      標準
  1回に取れるカードの数          0〜5      1   （0 でピックなし）
  表示するカードの数（候補）      1〜20     5
  ラウンド勝利のポイント数        1〜10     2
  勝利までのラウンド数            1〜20     5
  開始サイドを固定する            オン/オフ  オフ（RWF は毎ラウンド左右がランダム。
                                                オンでチーム番号の小さい順に左から）
  英語表示を日本語にする          オン/オフ  オン

- オンラインではホストの設定が全員に使われます（ただし全員がこの mod を入れている必要があります）。
- ロビー（キャラ選択・準備OK の画面）では、画面右上に「ルール」パネルが出て、今の設定が全員に見えます。
  ホストはパネルの < > で設定を変えられます（マウスで操作）。変えると全員の準備OK が解除されます。
  パネルが邪魔なときは local.rounds.jpsettings.cfg の ShowRulesPanel = false で消せます。
- 日本語化について: ゲームの日本語フォントはゲーム内で使う字しか持っていないため、
  訳語はそのフォントで出せる字だけで表示します。出せない場合は英語のまま出ます
  （現在はチームの色名「マゼンタ」「ピーチ」「ピンク」だけ英語）。
- 直しているゲーム本体（バニラ）のバグ:
    名前が1文字のカード（日本語の「毒」など）を取ると、ピックが終わらずにもう1枚選べてしまう。
    （取得カード一覧の略称を作るときに名前の2文字目が無くてエラーになっていたため）
- メインメニューのボタン順を「ローカル・オンライン・オプション・MOD・クレジット・終了」に直しています。
- UnboundLib が画面右下に出す Discord / Thunderstore のリンク（ただの案内で、Thunderstore は不要）を消しています。
  出したいときは local.rounds.jpsettings.cfg の HideCommunityLinks = false にして再起動してください。
- カードの取り方・候補の数の仕組みは Pick N Cards / Draw N Cards（Pykess）を参考にしています。

RoundsJPSettings を作り直すとき（作った人向け）:
  source\RoundsJPSettings\RoundsJPSettings.cs を編集して、
  別に用意したビルド用フォルダ（RoundsJPSettings\build.ps1）でビルドします。
  Windows 標準の C# コンパイラ（C# 5）を使うので、新しい C# の書き方は使えません。


■ ほかの mod を足したいとき
その mod の dll を BepInEx\plugins\ の中にフォルダを作って入れてください。
- 現行版 ROUNDS（v1.1.2）と Bknibb 版 UnboundLib 4 に対応したものだけが動きます。
  Thunderstore にある古い mod の多くは、そのままでは動きません。
- 一緒に遊ぶ全員が同じものを入れる必要があります（入れていない人がいても参加は止められません）。


■ 仕組み
install.bat は、ゲームフォルダに次の2つのファイルを置きます。
  winhttp.dll           （mod ローダーの入口）
  doorstop_config.ini   （普段は無効 enabled=false）
play.bat は Steam 経由で ROUNDS を起動します。そのとき
「--doorstop-enable true --doorstop-target <このフォルダ>\BepInEx\core\BepInEx.Preloader.dll」
を渡すので、その起動のときだけ mod が読み込まれます（r2modman と同じ方法です）。
mod の設定ファイルやログは、このフォルダの BepInEx\ の中に作られます。

BepInEx\config\BepInEx.cfg の HideManagerGameObject は true にしてあります。
現行版 ROUNDS では、これが false だと最初の画面を読み込んだときに mod が消えてしまい、
メニューの文字が出なくなります。変えないでください。


■ うまくいかないとき
まず BepInEx\LogOutput.log を見てください（起動するたびに作り直されます）。
- mod が効かない（MOD ボタンが出ない）
  → install.bat を実行したか、パスに日本語やスペースが含まれていないか確認してください。
- メニューで文字が出ない・操作できない
  → Steam のベータが「なし」になっているか、BepInEx.cfg の HideManagerGameObject が true か確認してください。
- ゲームのアップデートで動かなくなった
  → mod 側が新しいバージョンに対応するまで待つ必要があります。


■ ライセンス
- BepInEx / UnityDoorstop: LGPL-2.1
- RoundsWithFriends: GPL-3.0
- RoundsJPSettings: GPL-3.0（Pick N Cards の手法を使っているため。ソースは source\ に同梱）
（licenses フォルダを参照。GPL-3.0 の全文は RoundsWithFriends-LICENSE.txt と同じです）
UnboundLib には明示的なライセンスがないため、友達同士での私的な共有にとどめ、
公開の場で再配布しないでください。
