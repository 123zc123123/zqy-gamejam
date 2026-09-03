using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 纯代码输入桥接器，支持四名本地键盘玩家，并可将当前分配槽位的输入
    /// 透明转发给 DouQuquLanSession。键盘仅供桌面调试；方向与触屏反弹相同：往后按、朝反方向飞。
    /// </summary>
    public sealed class DouQuquKeyboardInput : MonoBehaviour
    {
        [SerializeField] private DouQuquMatchController match;
        [SerializeField] private DouQuquLanSession lan;
        [SerializeField] private int offlinePlayerCount = 4;

        // 保存上一帧按键状态，用来合成只持续一帧的松开事件。
        private readonly bool[] previousHeld = new bool[DouQuquMatchController.MaxPlayers];
        private bool wasStarted;

        private void Awake()
        {
            if (match == null) match = GetComponent<DouQuquMatchController>();
            if (lan == null) lan = GetComponent<DouQuquLanSession>();
        }

        // 主机和单机模式直接写入 MatchController；联机客户端只发送自己分配的槽位，
        // 不能覆盖其他玩家的输入。
        private void Update()
        {
            if (match == null) return;
            if (!match.IsStarted)
            {
                ResetTransientInput();
                return;
            }
            if (!wasStarted) ResetTransientInput();
            wasStarted = true;
            if (lan != null && lan.IsRunning && !lan.IsHost)
            {
                int id = lan.LocalPlayerId;
                if (id >= 0) SendForPlayer(id, true);
                return;
            }

            // 联机主机只拥有 0 号槽位，其他槽位由客户端控制（未加入前由权威 AI 驱动），
            // 因此本地键盘不能误写其他槽位。
            if (lan != null && lan.IsRunning && lan.IsHost)
            {
                SendForPlayer(0, true);
                return;
            }

            int count = Mathf.Clamp(offlinePlayerCount, 1, DouQuquMatchController.MaxPlayers);
            for (int i = 0; i < count; i++) SendForPlayer(i, false);
        }

        // 将键位配置转换成完整输入帧，并发送给本地控制器或局域网会话。
        private void SendForPlayer(int playerId, bool networked)
        {
            KeyCode up;
            KeyCode down;
            KeyCode left;
            KeyCode right;
            KeyCode jump;
            GetKeys(playerId, out up, out down, out left, out right, out jump);

            Vector2 direction = Vector2.zero;
            if (Input.GetKey(up)) direction.y += 1f;
            if (Input.GetKey(down)) direction.y -= 1f;
            if (Input.GetKey(left)) direction.x -= 1f;
            if (Input.GetKey(right)) direction.x += 1f;
            // 反弹：往后按、朝反方向飞。方向为零表示“保持上一次瞄准方向”。
            if (direction.sqrMagnitude > 0.0001f) direction = -direction;

            bool held = Input.GetKey(jump);
            bool released = previousHeld[playerId] && !held;
            previousHeld[playerId] = held;
            if (networked) lan.SendInput(direction, held, released);
            else match.SetInput(playerId, direction, held, released);
        }

        // 首帧和两局之间都会调用，避免上一局仍按住的键在新局产生伪松开事件。
        private void ResetTransientInput()
        {
            for (int i = 0; i < previousHeld.Length; i++) previousHeld[i] = false;
            wasStarted = false;
        }

        // 四套固定键位故意保持纯代码；未来 UI 可以替换此桥接器，
        // 不影响模拟层的输入语义。
        private void GetKeys(int id, out KeyCode up, out KeyCode down, out KeyCode left, out KeyCode right, out KeyCode jump)
        {
            switch (id)
            {
                case 1:
                    up = KeyCode.UpArrow; down = KeyCode.DownArrow; left = KeyCode.LeftArrow; right = KeyCode.RightArrow; jump = KeyCode.RightControl; break;
                case 2:
                    up = KeyCode.I; down = KeyCode.K; left = KeyCode.J; right = KeyCode.L; jump = KeyCode.O; break;
                case 3:
                    up = KeyCode.T; down = KeyCode.G; left = KeyCode.F; right = KeyCode.H; jump = KeyCode.Y; break;
                default:
                    up = KeyCode.W; down = KeyCode.S; left = KeyCode.A; right = KeyCode.D; jump = KeyCode.Space; break;
            }
        }
    }
}
