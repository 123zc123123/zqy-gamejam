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
    /// <summary>背包里的一只精品虫。图鉴只记种类次数，背包记每一只。</summary>
    public sealed class CricketBackpackEntry
    {
        public string instanceId;
        public int quality;
        public int temperament;
    }

    [Serializable]
    /// <summary>Demo 服务器保存的玩家资料。</summary>
    public sealed class PlayerProfile
    {
        public string playerId;
        public string playerName;
        public long updatedAtUtcTicks;
        public int score;
        public int gold;
        public int eggs;
        public bool economyReady;
        public List<CricketCollectionEntry> crickets = new List<CricketCollectionEntry>();
        public List<CricketBackpackEntry> backpack = new List<CricketBackpackEntry>();
    }

    [Serializable]
    internal sealed class PlayerDatabase
    {
        public List<PlayerProfile> players = new List<PlayerProfile>();
    }

    /// <summary>
    /// Demo 级玩家数据服务。它用与服务器接口相同的“登录、查询、写入”边界，
    /// 当前后端落在 persistentDataPath 的 JSON 数据库中，因此重启游戏后数据仍存在。
    /// 后续若接独立服务器，只需替换本类的存取实现，界面和玩法层无需改动。
    /// </summary>
    public static class PlayerDataService
    {
        private const int MaxNameLength = 20;
        public const int StartScore = 0;
        public const int StartGold = 100;
        public const int StartEggs = 24;
        public const int ScoreCap = 999999;
        public const int GoldCap = 999999;
        public const int EggCap = 99;
        public const int EggShopPrice = 10;
        public const int EggShopCount = 1;
        public static readonly int[] PointsPlace = { 50, 30, 10, 5 };
        public static readonly int[] GoldPlace = { 40, 24, 14, 8 };
        public static readonly int[] EggPlace = { 8, 6, 4, 2 };
        private const string DatabaseFileName = "douququ-player-database.json";

        private static PlayerDatabase database;

        public static PlayerProfile CurrentPlayer { get; private set; }
        public static bool IsLoggedIn => CurrentPlayer != null;
        public static string CurrentPlayerName => CurrentPlayer == null ? string.Empty : CurrentPlayer.playerName;
        public static int Score => CurrentPlayer == null ? 0 : CurrentPlayer.score;
        public static int Gold => CurrentPlayer == null ? 0 : CurrentPlayer.gold;
        public static int Eggs => CurrentPlayer == null ? 0 : CurrentPlayer.eggs;
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
                CurrentPlayer = new PlayerProfile
                {
                    playerId = Guid.NewGuid().ToString("N"),
                    playerName = playerName,
                    updatedAtUtcTicks = DateTime.UtcNow.Ticks,
                    score = StartScore,
                    gold = StartGold,
                    eggs = StartEggs,
                    economyReady = true,
                    crickets = new List<CricketCollectionEntry>(),
                    backpack = new List<CricketBackpackEntry>()
                };
                database.players.Add(CurrentPlayer);
            }
            else
            {
                CurrentPlayer.playerName = playerName;
                if (CurrentPlayer.crickets == null) CurrentPlayer.crickets = new List<CricketCollectionEntry>();
                if (CurrentPlayer.backpack == null) CurrentPlayer.backpack = new List<CricketBackpackEntry>();
                EnsureEconomy(CurrentPlayer);
                CurrentPlayer.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            }

            EnsureStarterBackpack(CurrentPlayer);

            error = SaveDatabase() ? string.Empty : "玩家数据保存失败，请检查设备存储权限";
            PlayerDataChanged?.Invoke();
            return string.IsNullOrEmpty(error);
        }

        public static string FormatGold(int amount)
        {
            return amount.ToString("N0");
        }

        public static bool TrySpendEggs(int amount)
        {
            if (CurrentPlayer == null || amount <= 0) return false;
            if (CurrentPlayer.eggs < amount) return false;
            CurrentPlayer.eggs -= amount;
            return CommitEconomy();
        }

        public static bool TrySpendGold(int amount)
        {
            if (CurrentPlayer == null || amount <= 0) return false;
            if (CurrentPlayer.gold < amount) return false;
            CurrentPlayer.gold -= amount;
            return CommitEconomy();
        }

        public static bool AddEggs(int amount)
        {
            if (CurrentPlayer == null || amount == 0) return false;
            CurrentPlayer.eggs = Mathf.Clamp(CurrentPlayer.eggs + amount, 0, EggCap);
            return CommitEconomy();
        }

        public static bool AddGold(int amount)
        {
            if (CurrentPlayer == null || amount == 0) return false;
            CurrentPlayer.gold = Mathf.Clamp(CurrentPlayer.gold + amount, 0, GoldCap);
            return CommitEconomy();
        }

        public static bool AddScore(int amount)
        {
            if (CurrentPlayer == null || amount == 0) return false;
            CurrentPlayer.score = Mathf.Clamp(CurrentPlayer.score + amount, 0, ScoreCap);
            return CommitEconomy();
        }

        public static int PointsForPlace(int place)
        {
            if (place < 1 || place > PointsPlace.Length) return 0;
            return PointsPlace[place - 1];
        }

        public static int GoldForPlace(int place)
        {
            if (place < 1 || place > GoldPlace.Length) return 0;
            return GoldPlace[place - 1];
        }

        public static int EggsForPlace(int place)
        {
            if (place < 1 || place > EggPlace.Length) return 0;
            return EggPlace[place - 1];
        }

        /// <summary>随机匹配 / 好友组队按名次发放。调用方保证同一局只调一次。</summary>
        public static bool AwardPlaceRewards(int place)
        {
            if (CurrentPlayer == null || place < 1 || place > 4) return false;
            int points = PointsForPlace(place);
            int gold = GoldForPlace(place);
            int eggs = EggsForPlace(place);
            if (points == 0 && gold == 0 && eggs == 0) return false;
            if (points != 0)
                CurrentPlayer.score = Mathf.Clamp(CurrentPlayer.score + points, 0, ScoreCap);
            if (gold != 0)
                CurrentPlayer.gold = Mathf.Clamp(CurrentPlayer.gold + gold, 0, GoldCap);
            if (eggs != 0)
                CurrentPlayer.eggs = Mathf.Clamp(CurrentPlayer.eggs + eggs, 0, EggCap);
            return CommitEconomy();
        }

        public static int SellPrice(int quality)
        {
            quality = Mathf.Clamp(quality, 1, 4);
            if (quality >= 4) return 160;
            if (quality == 3) return 80;
            if (quality == 2) return 40;
            return 20;
        }

        public static bool RemoveFromBackpack(string instanceId)
        {
            if (CurrentPlayer == null || CurrentPlayer.backpack == null || string.IsNullOrEmpty(instanceId))
                return false;
            for (int i = 0; i < CurrentPlayer.backpack.Count; i++)
            {
                CricketBackpackEntry entry = CurrentPlayer.backpack[i];
                if (entry == null || entry.instanceId != instanceId) continue;
                CurrentPlayer.backpack.RemoveAt(i);
                return CommitEconomy();
            }
            return false;
        }

        public static bool TryBuyEggs()
        {
            if (CurrentPlayer == null) return false;
            if (CurrentPlayer.gold < EggShopPrice) return false;
            if (CurrentPlayer.eggs >= EggCap) return false;
            int room = EggCap - CurrentPlayer.eggs;
            int add = Mathf.Min(EggShopCount, room);
            CurrentPlayer.gold -= EggShopPrice;
            CurrentPlayer.eggs += add;
            return CommitEconomy();
        }

        private static bool CommitEconomy()
        {
            CurrentPlayer.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            bool saved = SaveDatabase();
            PlayerDataChanged?.Invoke();
            return saved;
        }

        private static void EnsureEconomy(PlayerProfile player)
        {
            if (player == null || player.economyReady) return;
            player.score = StartScore;
            player.gold = StartGold;
            player.eggs = StartEggs;
            player.economyReady = true;
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

        /// <summary>把一只精品虫收进当前登录名的背包。</summary>
        public static bool AddFinestToBackpack(int quality, int temperament)
        {
            if (CurrentPlayer == null) return false;
            if (CurrentPlayer.backpack == null) CurrentPlayer.backpack = new List<CricketBackpackEntry>();
            CurrentPlayer.backpack.Add(new CricketBackpackEntry
            {
                instanceId = Guid.NewGuid().ToString("N"),
                quality = Mathf.Clamp(quality, 1, 4),
                temperament = Mathf.Clamp(temperament, 1, 4)
            });
            CurrentPlayer.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            bool saved = SaveDatabase();
            PlayerDataChanged?.Invoke();
            return saved;
        }

        /// <summary>退出合成盘时，把盘上所有精品虫收进背包并空出该格。</summary>
        public static bool CollectFinestFromBoard(MergeBoard board)
        {
            if (CurrentPlayer == null || board == null) return false;
            List<MergePiece> taken = board.TakeFinestPieces();
            if (taken == null || taken.Count == 0) return true;
            if (CurrentPlayer.backpack == null) CurrentPlayer.backpack = new List<CricketBackpackEntry>();
            for (int i = 0; i < taken.Count; i++)
            {
                MergePiece piece = taken[i];
                if (piece == null) continue;
                CurrentPlayer.backpack.Add(new CricketBackpackEntry
                {
                    instanceId = Guid.NewGuid().ToString("N"),
                    quality = Mathf.Clamp(piece.drawA, 1, 4),
                    temperament = Mathf.Clamp(piece.drawB, 1, 4)
                });
            }
            CurrentPlayer.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            bool saved = SaveDatabase();
            PlayerDataChanged?.Invoke();
            return saved;
        }

        /// <summary>返回背包快照，调用者不能直接修改数据库中的原始对象。</summary>
        public static List<CricketBackpackEntry> GetBackpackSnapshot()
        {
            List<CricketBackpackEntry> result = new List<CricketBackpackEntry>();
            if (CurrentPlayer == null || CurrentPlayer.backpack == null) return result;
            for (int i = 0; i < CurrentPlayer.backpack.Count; i++)
            {
                CricketBackpackEntry source = CurrentPlayer.backpack[i];
                if (source == null || string.IsNullOrEmpty(source.instanceId)) continue;
                result.Add(new CricketBackpackEntry
                {
                    instanceId = source.instanceId,
                    quality = Mathf.Clamp(source.quality, 1, 4),
                    temperament = Mathf.Clamp(source.temperament, 1, 4)
                });
            }
            return result;
        }

        public static CricketBackpackEntry FindBackpack(string instanceId)
        {
            if (CurrentPlayer == null || CurrentPlayer.backpack == null || string.IsNullOrEmpty(instanceId))
                return null;
            for (int i = 0; i < CurrentPlayer.backpack.Count; i++)
            {
                CricketBackpackEntry entry = CurrentPlayer.backpack[i];
                if (entry != null && entry.instanceId == instanceId) return CloneBackpack(entry);
            }
            return null;
        }

        private static CricketBackpackEntry CloneBackpack(CricketBackpackEntry source)
        {
            if (source == null) return null;
            return new CricketBackpackEntry
            {
                instanceId = source.instanceId,
                quality = source.quality,
                temperament = source.temperament
            };
        }

        /// <summary>开档送凡品 1-1～1-4。旧档背包仍空时补一次，已有虫不补。</summary>
        private static void EnsureStarterBackpack(PlayerProfile player)
        {
            if (player == null) return;
            if (player.backpack == null) player.backpack = new List<CricketBackpackEntry>();
            for (int i = 0; i < player.backpack.Count; i++)
            {
                CricketBackpackEntry existing = player.backpack[i];
                if (existing != null && !string.IsNullOrEmpty(existing.instanceId)) return;
            }

            player.backpack.Clear();
            for (int temperament = 1; temperament <= 4; temperament++)
            {
                player.backpack.Add(new CricketBackpackEntry
                {
                    instanceId = Guid.NewGuid().ToString("N"),
                    quality = 1,
                    temperament = temperament
                });
            }
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
            SceneNames.Load(SceneNames.Login);
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
            database = new PlayerDatabase();
            string path = DatabasePath;
            if (!File.Exists(path)) return;
            try
            {
                PlayerDatabase loaded = JsonUtility.FromJson<PlayerDatabase>(File.ReadAllText(path));
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
