// RoundsJPSettings - ROUNDS v1.1.2 + Rounds With Friends (Bknibb fork) 向け
//  - 1回のピックで取れるカード枚数 (Pick N Cards 相当)
//  - 表示されるカード候補の枚数 (Draw N Cards 相当)
//  - ラウンド取得に必要なポイント数 / 試合に勝つのに必要なラウンド数
//  - RWF / UnboundLib の英語表示を日本語化
//
// Pick N Cards / Draw N Cards (Pykess, GPL-3.0, https://github.com/pdcook/PickNCards) の手法を参考にしています。
// このファイルも GPL-3.0 で配布します。
//
// csc (C# 5) でビルドするため、新しい C# 構文は使っていません。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using TMPro;
using UnboundLib;
using UnboundLib.GameModes;
using UnboundLib.Networking;
using UnboundLib.Utils.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RoundsJPSettings
{
    [BepInDependency("com.willis.rounds.unbound", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("io.olavim.rounds.rwf", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(ModId, ModName, Version)]
    [BepInProcess("Rounds.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModId = "local.rounds.jpsettings";
        public const string ModName = "RoundsJPSettings";
        public const string Version = "1.0.0";

        internal const int MaxPicks = 5;
        internal const int MaxDraws = 20;

        internal static Plugin instance;

        internal static ConfigEntry<int> PicksConfig;
        internal static ConfigEntry<int> DrawsConfig;
        internal static ConfigEntry<int> PointsConfig;
        internal static ConfigEntry<int> RoundsConfig;
        internal static ConfigEntry<bool> TranslateConfig;
        internal static ConfigEntry<bool> FixedSidesConfig;

        // 実際に使う値 (オンラインではホストの値で上書きされる)
        internal static int picks = 1;
        internal static int draws = 5;
        internal static int points = 2;
        internal static int rounds = 5;

        private void Awake()
        {
            instance = this;

            PicksConfig = Config.Bind("Game", "Picks", 1, "1回のピックで取れるカードの枚数 (0-5, 標準 1。0 でピックなし)");
            DrawsConfig = Config.Bind("Game", "Draws", 5, "表示されるカード候補の枚数 (1-20, 標準 5)");
            PointsConfig = Config.Bind("Game", "PointsToWinRound", 2, "1ラウンドを取るのに必要なポイント数 (1-10, 標準 2)");
            RoundsConfig = Config.Bind("Game", "RoundsToWinGame", 5, "試合に勝つのに必要なラウンド数 (1-20, 標準 5。RWF デスマッチの標準は 3)");
            FixedSidesConfig = Config.Bind("Game", "FixedSides", false, "毎ラウンドの開始サイドを固定する (チーム番号の小さい順に左から。RWF は標準ではランダム)");
            TranslateConfig = Config.Bind("Language", "TranslateToJapanese", true, "RWF / UnboundLib の英語表示を日本語にする");

            LoadLocalValues();

            new Harmony(ModId).PatchAll(typeof(Plugin).Assembly);
        }

        private void Start()
        {
            Unbound.RegisterCredits(ModName, new string[] { "ROUNDS v1.1.2 向け設定 + 日本語化", "Pick/Draw N Cards の手法: Pykess" }, new string[0], new string[0]);
            Unbound.RegisterMenu("RWF JP SETTINGS", delegate { }, BuildMenu, null, true);
            Unbound.RegisterHandshake(ModId, OnHandshakeCompleted);

            GameModeManager.AddHook(GameModeHooks.HookInitStart, ApplyGameSettings, GameModeHooks.Priority.First);
            GameModeManager.AddHook(GameModeHooks.HookGameStart, ApplyGameSettings, GameModeHooks.Priority.First);
            GameModeManager.AddHook(GameModeHooks.HookPickStart, PickN.ResetPickQueue, GameModeHooks.Priority.First);
            GameModeManager.AddHook(GameModeHooks.HookPickEnd, PickN.ExtraPicks, GameModeHooks.Priority.First);
        }

        private void Update()
        {
            JP.RetryPending();
            MainMenuOrder.Fix();
        }

        internal static void LoadLocalValues()
        {
            picks = Mathf.Clamp(PicksConfig.Value, 0, MaxPicks);
            draws = Mathf.Clamp(DrawsConfig.Value, 1, MaxDraws);
            points = Mathf.Clamp(PointsConfig.Value, 1, 10);
            rounds = Mathf.Clamp(RoundsConfig.Value, 1, 20);
        }

        internal static bool IsHostOrOffline()
        {
            return PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
        }

        // ---------- 同期 ----------
        private void OnHandshakeCompleted()
        {
            SendSettingsToOthers();
        }

        internal static void SendSettingsToOthers()
        {
            if (PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode && PhotonNetwork.IsMasterClient)
            {
                NetworkingManager.RPC_Others(typeof(Plugin), "SyncSettings", new object[] { picks, draws, points, rounds });
            }
        }

        [UnboundRPC]
        private static void SyncSettings(int hostPicks, int hostDraws, int hostPoints, int hostRounds)
        {
            picks = hostPicks;
            draws = hostDraws;
            points = hostPoints;
            rounds = hostRounds;
        }

        // ---------- ラウンド/ポイント ----------
        private static IEnumerator ApplyGameSettings(IGameModeHandler gm)
        {
            if (gm != null && gm.Settings != null)
            {
                if (gm.Settings.ContainsKey("pointsToWinRound"))
                {
                    gm.ChangeSetting("pointsToWinRound", points);
                }
                if (gm.Settings.ContainsKey("roundsToWinGame"))
                {
                    gm.ChangeSetting("roundsToWinGame", rounds);
                }
            }
            yield break;
        }

        // ---------- 設定メニュー ----------
        private void BuildMenu(GameObject menu)
        {
            TextMeshProUGUI tmp;
            Slider slider;
            MenuHandler.CreateText("RWF JP SETTINGS", menu, out tmp, 60);
            MenuHandler.CreateText("Online games use the host's settings", menu, out tmp, 25);
            MenuHandler.CreateText(" ", menu, out tmp, 20);

            MenuHandler.CreateSlider("Cards to pick per round", menu, 30, 0f, MaxPicks, PicksConfig.Value, delegate (float v)
            {
                PicksConfig.Value = Mathf.RoundToInt(v);
                LoadLocalValues();
                SendSettingsToOthers();
            }, out slider, true);

            MenuHandler.CreateSlider("Cards to choose from", menu, 30, 1f, MaxDraws, DrawsConfig.Value, delegate (float v)
            {
                DrawsConfig.Value = Mathf.RoundToInt(v);
                LoadLocalValues();
                SendSettingsToOthers();
            }, out slider, true);

            MenuHandler.CreateSlider("Points to win a round", menu, 30, 1f, 10f, PointsConfig.Value, delegate (float v)
            {
                PointsConfig.Value = Mathf.RoundToInt(v);
                LoadLocalValues();
                SendSettingsToOthers();
            }, out slider, true);

            MenuHandler.CreateSlider("Rounds to win the game", menu, 30, 1f, 20f, RoundsConfig.Value, delegate (float v)
            {
                RoundsConfig.Value = Mathf.RoundToInt(v);
                LoadLocalValues();
                SendSettingsToOthers();
            }, out slider, true);

            MenuHandler.CreateText(" ", menu, out tmp, 20);
            MenuHandler.CreateToggle(FixedSidesConfig.Value, "Fixed starting sides", menu, delegate (bool b) { FixedSidesConfig.Value = b; }, 30);
            MenuHandler.CreateToggle(TranslateConfig.Value, "Translate to Japanese", menu, delegate (bool b) { TranslateConfig.Value = b; }, 30);
        }

        // ---------- ピック待ち合わせ ----------
        [UnboundRPC]
        public static void RPC_RequestSync(int requestingPlayer)
        {
            NetworkingManager.RPC(typeof(Plugin), "RPC_SyncResponse", new object[] { requestingPlayer, PhotonNetwork.LocalPlayer.ActorNumber });
        }

        [UnboundRPC]
        public static void RPC_SyncResponse(int requestingPlayer, int readyPlayer)
        {
            if (PhotonNetwork.LocalPlayer.ActorNumber == requestingPlayer)
            {
                instance.RemovePendingRequest(readyPlayer, "RPC_RequestSync");
            }
        }

        internal IEnumerator WaitForSyncUp()
        {
            if (PhotonNetwork.OfflineMode)
            {
                yield break;
            }
            yield return this.SyncMethod("RPC_RequestSync", null, new object[] { PhotonNetwork.LocalPlayer.ActorNumber });
        }
    }

    // =====================================================================
    //  メインメニューのボタン順を直す
    //  UnboundLib が MOD / クレジットを足したあと名前で並べ直そうとするが、
    //  今のゲームではうまく効かず「ローカル・MOD・クレジット・オンライン…」になる。
    //  ローカル → オンライン → オプション → MOD → クレジット → 終了 に揃える。
    // =====================================================================
    internal static class MainMenuOrder
    {
        private static readonly string[] order = { "local", "online", "options", "mods", "credits", "quit" };
        private static float nextCheck = 0f;
        private static int loggedCount = -1;

        internal static void Fix()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            try
            {
                if (MainMenuHandler.instance == null) return;
                Transform group = MainMenuHandler.instance.transform.Find("Canvas/ListSelector/Main/Group");
                if (group == null) return;

                if (group.childCount != loggedCount)
                {
                    // ボタンが増えたときにだけログに出す (MOD / クレジットは少し遅れて追加される)
                    loggedCount = group.childCount;
                    List<string> names = new List<string>();
                    foreach (Transform c in group) names.Add(c.name);
                    UnityEngine.Debug.Log("[RoundsJPSettings] メインメニューの並び: " + string.Join(", ", names.ToArray()));
                }

                // 並べたいボタンと、それらが今いる位置を集める
                List<Transform> buttons = new List<Transform>();
                List<int> slots = new List<int>();
                foreach (string key in order)
                {
                    foreach (Transform c in group)
                    {
                        if (string.Equals(c.name, key, StringComparison.OrdinalIgnoreCase))
                        {
                            buttons.Add(c);
                            slots.Add(c.GetSiblingIndex());
                            break;
                        }
                    }
                }
                if (buttons.Count < 2) return;
                slots.Sort();

                bool ok = true;
                for (int i = 0; i < buttons.Count; i++)
                {
                    if (buttons[i].GetSiblingIndex() != slots[i]) { ok = false; break; }
                }
                if (ok) return;

                // ほかの子 (タイトルなど) の位置は変えず、ボタン同士だけ入れ替える
                for (int i = 0; i < buttons.Count; i++)
                {
                    buttons[i].SetSiblingIndex(slots[i]);
                }
                List<string> after = new List<string>();
                foreach (Transform c in group) after.Add(c.name);
                UnityEngine.Debug.Log("[RoundsJPSettings] メインメニューを並べ直しました: " + string.Join(", ", after.ToArray()));
            }
            catch (Exception e)
            {
                nextCheck = float.MaxValue;
                UnityEngine.Debug.LogError("[RoundsJPSettings] メニュー順の修正を停止: " + e);
            }
        }
    }

    // =====================================================================
    //  開始サイドの固定
    //  RWF は GeneralizedSpawnPositions.GetSpawnDictionary で毎ラウンドチーム順をシャッフルするので、
    //  どのチームが左右どちらから始まるかが毎回変わる。
    //  RWF が決めたスポーン位置の「まとまり」はそのまま使い、チームの割り当てだけを
    //  チーム番号の小さい順に左から並べ直す。チーム内はプレイヤー番号の小さい順に左から。
    //  まとまりの人数とチームの人数が合わないときは RWF の結果をそのまま使う。
    //  この処理はホストだけで走り、結果は RWF が全員に送るので、オンラインではホストの設定になる。
    // =====================================================================
    [HarmonyPatch]
    internal class GeneralizedSpawnPositions_Patch
    {
        private static bool Prepare()
        {
            return TargetMethod() != null;
        }

        private static System.Reflection.MethodBase TargetMethod()
        {
            Type t = AccessTools.TypeByName("RWF.Algorithms.GeneralizedSpawnPositions");
            return t == null ? null : AccessTools.Method(t, "GetSpawnDictionary");
        }

        private static void Postfix(Dictionary<Player, Vector2> __result)
        {
            if (Plugin.FixedSidesConfig == null || !Plugin.FixedSidesConfig.Value || __result == null || __result.Count < 2)
            {
                return;
            }
            try
            {
                // RWF が作ったチームごとの位置のまとまり
                Dictionary<int, List<Vector2>> groupByTeam = new Dictionary<int, List<Vector2>>();
                foreach (KeyValuePair<Player, Vector2> kv in __result)
                {
                    if (kv.Key == null) return;
                    int team = kv.Key.TeamID;
                    if (!groupByTeam.ContainsKey(team)) groupByTeam[team] = new List<Vector2>();
                    groupByTeam[team].Add(kv.Value);
                }
                if (groupByTeam.Count < 2) return;

                // まとまりを左 (平均 x が小さい) から順に、チームを番号順に
                List<List<Vector2>> groupsLeftToRight = groupByTeam.Values.OrderBy(g => g.Average(v => v.x)).ToList();
                List<int> teamsById = groupByTeam.Keys.OrderBy(id => id).ToList();

                for (int k = 0; k < teamsById.Count; k++)
                {
                    if (groupsLeftToRight[k].Count != groupByTeam[teamsById[k]].Count) return; // 人数が合わない
                }

                Dictionary<Player, Vector2> fixedSpawns = new Dictionary<Player, Vector2>();
                for (int k = 0; k < teamsById.Count; k++)
                {
                    int team = teamsById[k];
                    List<Vector2> positions = groupsLeftToRight[k].OrderBy(v => v.x).ToList();
                    List<Player> players = __result.Keys.Where(p => p.TeamID == team).OrderBy(p => p.PlayerID).ToList();
                    for (int i = 0; i < players.Count; i++)
                    {
                        fixedSpawns[players[i]] = positions[i];
                    }
                }
                foreach (KeyValuePair<Player, Vector2> kv in fixedSpawns)
                {
                    __result[kv.Key] = kv.Value;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[RoundsJPSettings] 開始サイドの固定に失敗 (RWF の配置をそのまま使います): " + e);
            }
        }
    }

    // =====================================================================
    //  Pick N Cards
    // =====================================================================
    internal static class PickN
    {
        internal static bool lockPickQueue = false;
        internal static List<int> playerIDsToPick = new List<int>();
        internal static bool extraPicksInProgress = false;

        internal static IEnumerator ResetPickQueue(IGameModeHandler gm)
        {
            if (!extraPicksInProgress)
            {
                playerIDsToPick = new List<int>();
                lockPickQueue = false;
            }
            yield break;
        }

        internal static IEnumerator ExtraPicks(IGameModeHandler gm)
        {
            if (extraPicksInProgress || Plugin.picks <= 1 || playerIDsToPick.Count < 1)
            {
                yield break;
            }

            lockPickQueue = true;
            extraPicksInProgress = true;
            yield return Plugin.instance.WaitForSyncUp();

            for (int n = 0; n < Plugin.picks - 1; n++)
            {
                yield return Plugin.instance.WaitForSyncUp();
                for (int i = 0; i < playerIDsToPick.Count; i++)
                {
                    yield return Plugin.instance.WaitForSyncUp();
                    int playerID = playerIDsToPick[i];
                    yield return GameModeManager.TriggerHook(GameModeHooks.HookPlayerPickStart);
                    CardChoiceVisuals.instance.Show(playerID, true);
                    yield return CardChoice.instance.DoPick(1, playerID, PickerType.Player);
                    yield return new WaitForSecondsRealtime(0.1f);
                    yield return GameModeManager.TriggerHook(GameModeHooks.HookPlayerPickEnd);
                    yield return new WaitForSecondsRealtime(0.1f);
                }
            }

            CardChoiceVisuals.instance.Hide();
            extraPicksInProgress = false;
        }
    }

    [HarmonyPatch(typeof(CardChoiceVisuals), "Show")]
    [HarmonyPriority(Priority.First)]
    internal class CardChoiceVisuals_Show_Patch
    {
        private static bool Prefix()
        {
            return Plugin.picks != 0;
        }
    }

    [HarmonyPatch(typeof(CardChoice), "DoPick")]
    [HarmonyPriority(Priority.First)]
    internal class CardChoice_DoPick_Patch
    {
        private static bool Prefix()
        {
            return Plugin.picks != 0;
        }

        private static void Postfix(int picketIDToSet)
        {
            if (!PickN.lockPickQueue && !PickN.playerIDsToPick.Contains(picketIDToSet))
            {
                PickN.playerIDsToPick.Add(picketIDToSet);
            }
        }
    }

    // =====================================================================
    //  Draw N Cards
    // =====================================================================
    internal static class DrawN
    {
        // 標準 (5枚) から一度でも変えたら true。標準のままならゲーム本来の配置を一切いじらない。
        internal static bool layoutModified = false;

        private const float arc_A = 0.2102040816f;
        private const float arc_B = 1.959183674f;
        private const float arc_C = -1.959183674f;
        private const float Harc_A = 1f;
        private const float Harc_B = 2f;
        private const float Harc_C = -1.7f;
        private const float xC = 0.5f;
        private const float yC = 0.5f;
        private const float absMaxX = 0.85f;
        private const float defMaxXWorld = 25f;
        internal const float z = -5f;
        private const float maxPhi = 15f;

        internal static bool Active
        {
            get { return Plugin.draws != 5 || layoutModified; }
        }

        private static float Arc(float x, float offset)
        {
            if (offset == 0f)
            {
                return arc_C * x * x + arc_B * x + arc_A + offset;
            }
            else if (offset < 0f)
            {
                return -Mathf.Sqrt(1 + (Harc_B * Harc_B) * Mathf.Pow(x - xC, 2f) / (Harc_A * Harc_A)) - Harc_C + offset;
            }
            else
            {
                return -Mathf.Sqrt(1 + (Harc_B * Harc_B) * Mathf.Pow(x - xC, 2f) / (2f * Harc_A * Harc_A)) - Harc_C + offset;
            }
        }

        internal static List<Vector3> GetPositions(int N, float offset)
        {
            if (N <= 1)
            {
                return new List<Vector3> { new Vector3(xC, yC, z) };
            }
            if (N == 3)
            {
                offset -= 0.025f;
            }
            else if (N > Plugin.MaxDraws / 2)
            {
                int N1 = Mathf.RoundToInt(N / 2);
                int N2 = N - N1;
                int k1 = Math.Max(N1, N2);
                int k2 = Math.Min(N1, N2);
                List<Vector3> p1 = GetPositions(k1, offset - 0.16f);
                List<Vector3> p2 = GetPositions(k2, offset + 0.125f);
                return p1.Concat(p2).ToList();
            }

            float maxX = absMaxX;
            if (N < 4)
            {
                maxX = 0.75f;
            }
            else
            {
                maxX = Mathf.Clamp(maxX + 0.025f * (N - 5), maxX, 0.925f);
            }

            List<float> xs = new List<float>();
            if (N % 2 != 0)
            {
                xs.Add(xC);
                int k = (N - 1) / 2;
                float step = (maxX - xC) / k;
                float x = xC;
                for (int i = 0; i < k; i++)
                {
                    x += step;
                    xs.Add(x);
                    xs.Add(2f * xC - x);
                }
            }
            else
            {
                float xInit = 1f - maxX;
                float step = (maxX - xInit) / (N - 1);
                float x = xInit;
                for (int i = 0; i < N; i++)
                {
                    xs.Add(x);
                    x += step;
                }
            }
            xs.Sort();

            List<Vector3> positions = new List<Vector3>();
            foreach (float x in xs)
            {
                positions.Add(new Vector3(x, Arc(x, offset), z));
            }
            return positions;
        }

        internal static Vector3 GetScale(int N)
        {
            float factor = 1.04f * XWorldPoint(absMaxX) / defMaxXWorld;
            if (N == 5)
            {
                return Vector3.one * factor;
            }
            else if (N < 5)
            {
                return Vector3.one * factor * (1f + 1f / (2f * N));
            }
            else if (N > Plugin.MaxDraws / 2)
            {
                return Vector3.one * factor * Mathf.Clamp(5f / (N / 2 + 2), 3f / 5f, 1f);
            }
            else
            {
                return Vector3.one * factor * Mathf.Clamp(5f / (N - 1), 3f / 5f, 1f);
            }
        }

        internal static List<Quaternion> GetRotations(List<Vector3> screenPositions)
        {
            List<Quaternion> rotations = new List<Quaternion>();
            foreach (Vector3 pos in screenPositions)
            {
                rotations.Add(Quaternion.Euler(0f, 0f, (-maxPhi / (absMaxX - xC)) * (pos.x - xC)));
            }
            return rotations;
        }

        private static Camera Cam()
        {
            return MainCam.instance.transform.GetComponent<Camera>();
        }

        internal static Vector3 WorldPoint(Vector3 v)
        {
            v.x *= Screen.width;
            v.y *= Screen.height;
            Vector3 w = Cam().ScreenToWorldPoint(v);
            w.z = z;
            return w;
        }

        private static float XWorldPoint(float x)
        {
            return WorldPoint(new Vector3(x, 0f, 0f)).x;
        }
    }

    // ピック開始ごとにカード置き場(children)を枚数に合わせて作り直す
    [HarmonyPatch(typeof(CardChoice), "StartPick")]
    internal class CardChoice_StartPick_Patch
    {
        private static GameObject cardVis = null;

        private static void Prefix(CardChoice __instance)
        {
            if (!DrawN.Active)
            {
                return;
            }
            DrawN.layoutModified = true;

            Transform[] current = (Transform[])AccessTools.Field(typeof(CardChoice), "children").GetValue(__instance);
            if (cardVis == null)
            {
                cardVis = current[0].gameObject;
            }
            foreach (Transform child in current)
            {
                if (child != null && child.gameObject != cardVis)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }

            int n = Plugin.draws;
            List<Vector3> screen = DrawN.GetPositions(n, 0f);
            List<Quaternion> rotations = DrawN.GetRotations(screen);
            Vector3 scale = DrawN.GetScale(n);

            List<Transform> children = new List<Transform>();
            cardVis.transform.position = DrawN.WorldPoint(screen[0]);
            cardVis.transform.rotation = rotations[0];
            cardVis.transform.localScale = scale;
            children.Add(cardVis.transform);

            for (int i = 1; i < n; i++)
            {
                GameObject newChild = UnityEngine.Object.Instantiate(cardVis, DrawN.WorldPoint(screen[i]), rotations[i], __instance.transform);
                newChild.name = children.Count.ToString();
                children.Add(newChild.transform);
            }

            AccessTools.Field(typeof(CardChoice), "children").SetValue(__instance, children.ToArray());
        }
    }

    // 出てきたカードの大きさを枚数に合わせる
    [HarmonyPatch(typeof(CardChoice), "Spawn")]
    internal class CardChoice_Spawn_Patch
    {
        private static void Postfix(GameObject __result)
        {
            if (!DrawN.Active || __result == null)
            {
                return;
            }
            __result.transform.localScale = DrawN.GetScale(Plugin.draws);
            PhotonView view = __result.GetComponent<PhotonView>();
            if (view != null)
            {
                NetworkingManager.RPC_Others(typeof(CardChoice_Spawn_Patch), "RPCO_ScaleCard", new object[] { view.ViewID });
            }
        }

        [UnboundRPC]
        private static void RPCO_ScaleCard(int viewID)
        {
            PhotonView view = PhotonView.Find(viewID);
            if (view != null)
            {
                view.gameObject.transform.localScale = DrawN.GetScale(Plugin.draws);
            }
        }
    }

    // カードが画面左側にあるときに腕の向きが崩れないようにする (Draw N Cards と同じ修正)
    [HarmonyPatch(typeof(CardChoiceVisuals), "Update")]
    internal class CardChoiceVisuals_Update_Patch
    {
        private static bool Prefix(CardChoiceVisuals __instance, bool ___isShowinig,
            ref Vector3 ___leftHandVel, ref Vector3 ___rightHandVel, Vector3 ___leftHandRestPos, Vector3 ___rightHandRestPos)
        {
            if (!DrawN.Active)
            {
                return true;
            }
            if (!___isShowinig || Time.unscaledDeltaTime > 0.1f)
            {
                return false;
            }
            if (__instance.currentCardSelected >= __instance.cardParent.transform.childCount || __instance.currentCardSelected < 0)
            {
                return false;
            }
            if (float.IsNaN(__instance.rightHandTarget.position.x) || float.IsNaN(__instance.rightHandTarget.position.y))
            {
                __instance.rightHandTarget.position = Vector3.zero;
                ___rightHandVel = Vector3.zero;
            }
            if (float.IsNaN(__instance.leftHandTarget.position.x) || float.IsNaN(__instance.leftHandTarget.position.y))
            {
                __instance.leftHandTarget.position = Vector3.zero;
                ___leftHandVel = Vector3.zero;
            }

            GameObject selected = __instance.cardParent.transform.GetChild(__instance.currentCardSelected).gameObject;
            Vector3 target = selected.transform.GetChild(0).position;
            float dt = Time.unscaledDeltaTime;
            if (target.x < 0f)
            {
                ___leftHandVel += (target - __instance.leftHandTarget.position) * __instance.spring * dt;
                ___leftHandVel -= ___leftHandVel * dt * __instance.drag;
                ___rightHandVel += (___rightHandRestPos - __instance.rightHandTarget.position) * __instance.spring * dt * 0.5f;
                ___rightHandVel -= ___rightHandVel * dt * __instance.drag * 0.5f;
                ___rightHandVel += __instance.sway * new Vector3(-0.5f + Mathf.PerlinNoise(Time.unscaledTime * __instance.swaySpeed, 0f), -0.5f + Mathf.PerlinNoise(Time.unscaledTime * __instance.swaySpeed + 100f, 0f), 0f) * dt;
                __instance.shieldGem.transform.position = __instance.rightHandTarget.position;
                if (__instance.framesToSnap > 0)
                {
                    __instance.leftHandTarget.position = target;
                }
            }
            else
            {
                ___rightHandVel += (target - __instance.rightHandTarget.position) * __instance.spring * dt;
                ___rightHandVel -= ___rightHandVel * dt * __instance.drag;
                ___leftHandVel += (___leftHandRestPos - __instance.leftHandTarget.position) * __instance.spring * dt * 0.5f;
                ___leftHandVel -= ___leftHandVel * dt * __instance.drag * 0.5f;
                ___leftHandVel += __instance.sway * new Vector3(-0.5f + Mathf.PerlinNoise(Time.unscaledTime * __instance.swaySpeed, Time.unscaledTime * __instance.swaySpeed), -0.5f + Mathf.PerlinNoise(Time.unscaledTime * __instance.swaySpeed + 100f, Time.unscaledTime * __instance.swaySpeed + 100f), 0f) * dt;
                __instance.shieldGem.transform.position = __instance.leftHandTarget.position;
                if (__instance.framesToSnap > 0)
                {
                    __instance.rightHandTarget.position = target;
                }
            }
            __instance.framesToSnap--;
            __instance.leftHandTarget.position += ___leftHandVel * dt;
            __instance.rightHandTarget.position += ___rightHandVel * dt;
            return false;
        }
    }

    // =====================================================================
    //  バニラのバグ修正: 1文字のカード名 (日本語の「毒」など) を取ると止まる
    //
    //  CardBar.AddCard は画面上部の取得カード一覧の略称を作るため
    //  CardName.Substring(0, 2) を無条件に呼ぶ。名前が1文字だと例外になり、
    //  CardChoice.Pick がピック終了の RPC を送る前に中断される。
    //  その結果カード効果だけ付いてピックが終わらず、もう1枚選べてしまう。
    //  Substring を、長さが足りなければ短く切る安全版に差し替える。
    // =====================================================================
    [HarmonyPatch(typeof(CardBar), "AddCard")]
    internal class CardBar_AddCard_Patch
    {
        public static string SafeSubstring(string s, int start, int length)
        {
            if (s == null)
            {
                return "?";
            }
            if (start >= s.Length)
            {
                return s.Length > 0 ? s : "?";
            }
            return s.Substring(start, Math.Min(length, s.Length - start));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            System.Reflection.MethodInfo substring = AccessTools.Method(typeof(string), "Substring", new Type[] { typeof(int), typeof(int) });
            System.Reflection.MethodInfo safe = AccessTools.Method(typeof(CardBar_AddCard_Patch), "SafeSubstring");
            foreach (CodeInstruction ins in instructions)
            {
                if (ins.Calls(substring))
                {
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, safe);
                }
                else
                {
                    yield return ins;
                }
            }
        }
    }

    // =====================================================================
    //  日本語化
    // =====================================================================
    internal static class JP
    {
        private static readonly Dictionary<string, string> exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // ゲームモード
            { "Team Deathmatch", "チームデスマッチ" },
            { "Deathmatch", "デスマッチ" },
            { "Team Deathmatch. Last team standing wins.", "チームデスマッチ：最後まで残ったチームの勝ち|チームデスマッチ：最後のチームが勝利" },
            { "Free For All Deathmatch. Last player standing wins.", "バトルロイヤル：最後まで残ったプレイヤーの勝ち|バトルロイヤル：最後のプレイヤーが勝利" },
            { "GAMEMODE", "ゲームモード" },
            { "ROUNDS WITH FRIENDS", "ROUNDS WITH FRIENDS" },
            // 試合
            { "VICTORY!", "勝利！" },
            { "REMATCH?", "リマッチ？|リマッチ" },
            { "REMATCH", "リマッチ" },
            { "CONTINUE", "続ける" },
            { "EXIT", "やめる" },
            { "LETS GOO!", "いくぞ！|ファイト！" },
            { "FIGHT", "ファイト" },
            { "POINT TO", "ポイント獲得|ポイント" },
            // ロビー
            { "READY", "準備OK" },
            { "JOIN/READY", "参加 / 準備OK" },
            { "UNREADY/LEAVE", "準備解除 / 退出" },
            { "LOCKED", "ロック中" },
            { "CHANGE TEAM", "チーム変更|チームを選ぶ" },
            { "CHANGE FACE", "顔を変更|フェイスを選ぶ" },
            { "Select face", "顔を選ぶ|フェイスを選ぶ" },
            { "EDITING", "編集中" },
            { "TO EDIT", "で編集" },
            { "[E] TO EDIT", "[E] で編集" },
            { "INVITE", "招待" },
            { "Invite friend", "フレンドを招待" },
            { "IN GAME", "ゲーム中" },
            { "CONNECTING...", "接続中..." },
            { "BACK", "戻る" },
            { "Keybinds", "操作" },
            { "Move", "移動" },
            { "Left/right", "左右" },
            { "Switch", "切り替え|スイッチ" },
            { "Interact", "決定|OK" },
            { "Increase Team ID", "チーム番号 +|次のチーム" },
            { "Descrease Team ID", "チーム番号 -|前のチーム" },
            { "Exit Player Menu", "プレイヤーメニューを閉じる|プレイヤーメニューから戻る|戻る" },
            { "Show keybind hints in menus", "メニューに操作ヒントを表示" },
            // この mod の設定メニュー
            { "RWF JP SETTINGS", "RWF 日本語設定|RWF 日本語オプション" },
            { "Online games use the host's settings", "オンラインではホストの設定が使われます|オンラインではホストのオプション" },
            { "Cards to pick per round", "1回に取れるカードの枚数|1回に取れるカードの数" },
            { "Cards to choose from", "カード候補の枚数|表示するカードの数" },
            { "Points to win a round", "ラウンドを取るのに必要なポイント|ラウンド勝利のポイント数" },
            { "Rounds to win the game", "勝利に必要なラウンド数|勝利までのラウンド数" },
            { "Translate to Japanese", "英語表示を日本語にする|日本語で表示する" },
            { "Fixed starting sides", "開始サイドを固定する|チームの左右を固定|チームの左右：ランダムをオフ" },
            // UnboundLib
            { "MODS", "MOD" },
            { "LOCAL", "ローカル" },
            { "Toggle Cards", "カードのオン/オフ" },
            { "Toggle Levels", "マップのオン/オフ" },
            { "CREDITS", "クレジット" },
        };

        private static readonly Dictionary<string, string> colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Orange", "オレンジ" }, { "Blue", "ブルー" }, { "Red", "レッド" }, { "Green", "グリーン" },
            { "Yellow", "イエロー" }, { "Purple", "パープル" }, { "Magenta", "マゼンタ" }, { "Cyan", "シアン" },
            { "Tangerine", "タンジェリン" }, { "Light Blue", "ライトブルー" }, { "Peach", "ピーチ" }, { "Lime", "ライム" },
            { "Light Yellow", "ライトイエロー" }, { "Orchid", "オーキッド" }, { "Aquamarine", "アクアマリン" },
            { "Dark Orange", "ダークオレンジ" }, { "Pink", "ピンク" },
        };

        private static readonly Regex missingTranslation = new Regex(@"^No translation found for '(.+)' in (\S+)$", RegexOptions.Compiled);
        private static readonly Regex waitingFor = new Regex(@"^WAITING FOR (.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex pointTo = new Regex(@"^POINT TO ?(TEAMS?)? ?(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex teamColor = new Regex(@"^TEAM (.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex teamNumber = new Regex(@"^Team (\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string Color(string name)
        {
            string jp;
            return colors.TryGetValue(name.Trim(), out jp) ? jp : name;
        }

        internal static string Translate(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 120)
            {
                return s;
            }
            string trimmed = s.Trim();
            string jp;
            if (exact.TryGetValue(trimmed, out jp))
            {
                return jp;
            }

            Match m = missingTranslation.Match(trimmed);
            if (m.Success)
            {
                return Translate(m.Groups[1].Value);
            }
            m = waitingFor.Match(trimmed);
            if (m.Success)
            {
                return m.Groups[1].Value + " を待っています";
            }
            m = pointTo.Match(trimmed);
            if (m.Success && trimmed.StartsWith("POINT TO", StringComparison.OrdinalIgnoreCase))
            {
                string who = m.Groups[2].Value;
                if (who.Length == 0)
                {
                    return m.Groups[1].Success && m.Groups[1].Value.Length > 0 ? "チームにポイント" : "ポイント獲得|ポイント";
                }
                return (m.Groups[1].Value.Length > 0 ? "チーム " : "") + Color(who) + " にポイント";
            }
            m = teamNumber.Match(trimmed);
            if (m.Success)
            {
                return "チーム " + m.Groups[1].Value;
            }
            m = teamColor.Match(trimmed);
            if (m.Success && colors.ContainsKey(m.Groups[1].Value.Trim()))
            {
                return "チーム " + Color(m.Groups[1].Value);
            }
            if (colors.ContainsKey(trimmed))
            {
                return Color(trimmed);
            }
            return s;
        }

        // ---------- フォント ----------
        // ゲームは言語ごとにフォントを切り替えていて、日本語は NotoSansJP-Light SDF を使う。
        // このフォントはゲーム内で使う字だけを収めた部分フォントなので、訳語に使えない漢字がある。
        //  - 日本語にした文字は、フォントをゲーム標準の日本語フォントに差し替えて見た目を揃える
        //  - 訳語にフォントにない字が混ざっていたら、□ にせず英語のまま出す
        //  - 起動直後はフォントがまだ読み込まれていないので、見つかるまで1秒おきに探し直す
        private static TMP_FontAsset japaneseFont = null;
        private static float nextSearchTime = 0f;
        private static int searchCount = 0;
        private static readonly List<TMP_Text> pending = new List<TMP_Text>();
        private static readonly Dictionary<string, bool> coverCache = new Dictionary<string, bool>();

        internal static bool HasJapanese(string s)
        {
            if (s == null) return false;
            foreach (char c in s)
            {
                if ((c >= '぀' && c <= 'ヿ') || (c >= '一' && c <= '鿿')) return true;
            }
            return false;
        }

        private static char FirstJapanese(string s)
        {
            if (s == null) return '\0';
            foreach (char c in s)
            {
                if ((c >= '぀' && c <= 'ヿ') || (c >= '一' && c <= '鿿')) return c;
            }
            return '\0';
        }

        private static bool FontHas(TMP_FontAsset f, char c)
        {
            try { return f != null && f.HasCharacter(c, true, true); }
            catch (Exception) { return false; }
        }

        private static string MissingChars(TMP_FontAsset f, string s)
        {
            string missing = "";
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c) || missing.IndexOf(c) >= 0) continue;
                if (!FontHas(f, c)) missing += c;
            }
            return missing;
        }

        private static bool JapaneseFontCovers(string s)
        {
            bool ok;
            if (!coverCache.TryGetValue(s, out ok))
            {
                ok = MissingChars(japaneseFont, s).Length == 0;
                coverCache[s] = ok;
            }
            return ok;
        }

        private static TMP_FontAsset FindJapaneseFont()
        {
            // 1) 名前に JP を含む読み込み済みフォント (ゲーム本体の NotoSansJP-Light SDF など)
            foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (f != null && f.name.IndexOf("JP", StringComparison.OrdinalIgnoreCase) >= 0 && FontHas(f, 'ン'))
                {
                    return f;
                }
            }
            // 2) 画面上で日本語が出ている文字のフォント
            foreach (TMP_Text t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t == null || t.font == null || pending.Contains(t)) continue;
                string s = t.text;
                char first = FirstJapanese(s);
                if (first != '\0' && FontHas(t.font, first) && FontHas(t.font, 'ン'))
                {
                    return t.font;
                }
            }
            return null;
        }

        // 訳語ごとに、ゲームの日本語フォントで出せない字をログに書き出す (訳語の調整用)
        private static void ReportMissing()
        {
            List<string> all = new List<string>(exact.Values);
            all.AddRange(colors.Values);
            all.Add("を待っています");
            all.Add("チームにポイント");
            int bad = 0;
            foreach (string entry in all.Distinct())
            {
                // 「第1候補|第2候補|...」のうち出せるものがあれば OK
                string[] candidates = entry.Split('|');
                bool any = false;
                foreach (string c in candidates)
                {
                    if (MissingChars(japaneseFont, c).Length == 0) { any = true; break; }
                }
                if (!any)
                {
                    bad++;
                    UnityEngine.Debug.Log("[RoundsJPSettings] フォントにない字: 「" + MissingChars(japaneseFont, entry.Replace("|", "")) + "」 in 「" + entry + "」");
                }
            }
            UnityEngine.Debug.Log("[RoundsJPSettings] 日本語フォント: " + japaneseFont.name + " / 英語のまま出す訳語: " + bad + " 件");
        }

        // set_text の直前に呼ぶ。表示する文字列を返し、必要ならフォントを差し替える。
        internal static string Process(TMP_Text text, string original)
        {
            string translated = Translate(original);
            if (translated == original)
            {
                return original; // 訳さない文字 (ゲーム本体の日本語表示など) には一切触らない
            }
            string[] candidates = translated.Split('|');
            if (!HasJapanese(candidates[0]))
            {
                return candidates[0];
            }
            if (japaneseFont == null)
            {
                // フォントが見つかるまでは英語のまま出し、あとで出し直す
                if (!pending.Contains(text)) pending.Add(text);
                return original;
            }
            // 候補を順に試し、ゲームの日本語フォントで全部出せる最初のものを使う
            foreach (string c in candidates)
            {
                if (JapaneseFontCovers(c))
                {
                    if (text.font != japaneseFont)
                    {
                        text.font = japaneseFont; // 見た目をゲーム標準の日本語フォントに揃える
                    }
                    return c;
                }
            }
            return original; // □ になるくらいなら英語のまま
        }

        // Plugin.Update から呼ぶ
        internal static void RetryPending()
        {
            if (pending.Count == 0 || Time.unscaledTime < nextSearchTime)
            {
                return;
            }
            nextSearchTime = Time.unscaledTime + 1f;
            try
            {
                if (japaneseFont == null)
                {
                    japaneseFont = FindJapaneseFont();
                    if (japaneseFont == null)
                    {
                        searchCount++;
                        if (searchCount == 10)
                        {
                            List<string> names = new List<string>();
                            foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                            {
                                if (f != null) names.Add(f.name + (FontHas(f, 'ン') ? "(ン有)" : ""));
                            }
                            UnityEngine.Debug.LogWarning("[RoundsJPSettings] 日本語フォントが見つかりません。読み込み済みフォント: " + string.Join(", ", names.ToArray()));
                        }
                        return;
                    }
                    ReportMissing();
                }
                List<TMP_Text> list = new List<TMP_Text>(pending);
                pending.Clear();
                foreach (TMP_Text t in list)
                {
                    if (t == null) continue;
                    t.text = t.text; // set_text を通して翻訳とフォント差し替えをやり直す
                }
            }
            catch (Exception e)
            {
                pending.Clear();
                UnityEngine.Debug.LogError("[RoundsJPSettings] フォント調整に失敗: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(TMP_Text), "text", MethodType.Setter)]
    internal class TMP_Text_SetText_Patch
    {
        private static bool broken = false;

        private static void Prefix(TMP_Text __instance, ref string value)
        {
            if (broken || Plugin.TranslateConfig == null || !Plugin.TranslateConfig.Value || value == null)
            {
                return;
            }
            // 翻訳で何か起きても、ゲームの文字表示は絶対に壊さない
            try
            {
                value = JP.Process(__instance, value);
            }
            catch (Exception e)
            {
                broken = true;
                UnityEngine.Debug.LogError("[RoundsJPSettings] 日本語化を停止しました: " + e);
            }
        }
    }
}
