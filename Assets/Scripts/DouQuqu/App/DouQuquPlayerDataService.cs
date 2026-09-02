using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DouQuqu
{
    [Serializable]
    /// <summary>一种由抽卡参数 A、B 唯一确定的蟋蟀图鉴条目。</summary>
    public sealed class CricketCollectionEntry
    {
        public int drawA;
        public int drawB;
        public int count;
    }

    [Serializable]
    /// <summary>Demo 服务器保存的玩家资料。</summary>
    public sealed class DouQuquPlayerProfile
    {
        public string playerId;
        public string playerName;
        public long updatedAtUtcTicks;
        public List<CricketCollectionEntry> crickets = new List<CricketCollectionEntry>();
    }

    [Serializable]
    internal sealed class DouQuquPlayerDatabase
    {
        public List<DouQuquPlayerProfile> players = new List<DouQuquPlayerProfile>();
    }

    /// <summary>
    /// Demo 级玩家数据服务。它用与服务器接口相同的“登录、查询、写入”边界，
    /// 当前后端落在 persistentDataPath 的 JSON 数据库中，因此重启游戏后数据仍存在。
    /// 后续若接独立服务器，只需替换本类的存取实现，界面和玩法层无需改动。
    /// </summary>
    public static class DouQuquPlayerDataService
    {
        private const int MaxNameLength = 20;
        private const string DatabaseFileName = "douququ-player-database.json";

        private static DouQuquPlayerDatabase database;

        public static DouQuquPlayerProfile CurrentPlayer { get; private set; }
        public static bool IsLoggedIn => CurrentPlayer != null;
        public static string CurrentPlayerName => CurrentPlayer == null ? string.Empty : CurrentPlayer.playerName;
        public static event Action PlayerDataChanged;

        /// <summary>按玩家名登录；同名玩家会加载旧资料，新名字会创建新资料。</summary>
        public static bool LoginOrCreate(string rawName, out string error)
        {
            string playerName = NormalizeName(rawName);
            if (string.IsNullOrEmpty(playerName))
            {
                error = "请输入玩家名称";
                return false;
            }

            EnsureLoaded();
            CurrentPlayer = database.players.Find(player =>
                player != null && string.Equals(player.playerName, playerName, StringComparison.OrdinalIgnoreCase));
            if (CurrentPlayer == null)
            {
                CurrentPlayer = new DouQuquPlayerProfile
                {
                    playerId = Guid.NewGuid().ToString("N"),
                    playerName = playerName,
                    updatedAtUtcTicks = DateTime.UtcNow.Ticks,
                    crickets = new List<CricketCollectionEntry>()
                };
                database.players.Add(CurrentPlayer);
            }
            else
            {
                CurrentPlayer.playerName = playerName;
                if (CurrentPlayer.crickets == null) CurrentPlayer.crickets = new List<CricketCollectionEntry>();
                CurrentPlayer.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            }

            error = SaveDatabase() ? string.Empty : "玩家数据保存失败，请检查设备存储权限";
            PlayerDataChanged?.Invoke();
            return string.IsNullOrEmpty(error);
        }

        /// <summary>把一次三级合成产生的蟋蟀写入当前玩家图鉴。</summary>
        public static bool RecordCricket(int drawA, int drawB)
        {
            if (CurrentPlayer == null) return false;
            drawA = Mathf.Clamp(drawA, 1, 4);
            drawB = Mathf.Clamp(drawB, 1, 4);
            if (CurrentPlayer.crickets == null) CurrentPlayer.crickets = new List<CricketCollectionEntry>();
            CricketCollectionEntry entry = CurrentPlayer.crickets.Find(item =>
                item != null && item.drawA == drawA && item.drawB == drawB);
            if (entry == null)
            {
                entry = new CricketCollectionEntry { drawA = drawA, drawB = drawB, count = 0 };
                CurrentPlayer.crickets.Add(entry);
            }
            entry.count++;
            CurrentPlayer.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            bool saved = SaveDatabase();
            PlayerDataChanged?.Invoke();
            return saved;
        }

        /// <summary>返回图鉴快照，调用者不能直接修改数据库中的原始对象。</summary>
        public static List<CricketCollectionEntry> GetCollectionSnapshot()
        {
            List<CricketCollectionEntry> result = new List<CricketCollectionEntry>();
            if (CurrentPlayer == null || CurrentPlayer.crickets == null) return result;
            for (int i = 0; i < CurrentPlayer.crickets.Count; i++)
            {
                CricketCollectionEntry source = CurrentPlayer.crickets[i];
                if (source == null || source.count <= 0) continue;
                result.Add(new CricketCollectionEntry
                {
                    drawA = source.drawA,
                    drawB = source.drawB,
                    count = source.count
                });
            }
            result.Sort((left, right) =>
            {
                int a = left.drawA.CompareTo(right.drawA);
                return a != 0 ? a : left.drawB.CompareTo(right.drawB);
            });
            return result;
        }

        public static void Logout()
        {
            CurrentPlayer = null;
            PlayerDataChanged?.Invoke();
        }

        /// <summary>没有登录资料时返回登录页，防止直接打开其他场景产生无主数据。</summary>
        public static bool RequireLogin()
        {
            if (IsLoggedIn) return true;
            DouQuquSceneNames.Load(DouQuquSceneNames.Login);
            return false;
        }

        private static string NormalizeName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;
            string result = rawName.Trim();
            return result.Length <= MaxNameLength ? result : result.Substring(0, MaxNameLength);
        }

        private static void EnsureLoaded()
        {
            if (database != null) return;
            database = new DouQuquPlayerDatabase();
            string path = DatabasePath;
            if (!File.Exists(path)) return;
            try
            {
                DouQuquPlayerDatabase loaded = JsonUtility.FromJson<DouQuquPlayerDatabase>(File.ReadAllText(path));
                if (loaded != null && loaded.players != null) database = loaded;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("玩家数据库读取失败，将使用空数据库：" + exception.Message);
            }
        }

        private static bool SaveDatabase()
        {
            EnsureLoaded();
            try
            {
                string directory = Path.GetDirectoryName(DatabasePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(DatabasePath, JsonUtility.ToJson(database, true));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("玩家数据库保存失败：" + exception.Message);
                return false;
            }
        }

        private static string DatabasePath => Path.Combine(Application.persistentDataPath, DatabaseFileName);
    }
}
