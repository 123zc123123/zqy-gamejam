using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class FigmaPlayerCardLayout : MonoBehaviour
{
    [Tooltip("关掉时你可以自由改左边排版；打开后按左边 BattlePlayer 镜像，不要手改右侧内部坐标。")]
    [SerializeField] public bool rightSide;

    RectTransform background;
    RectTransform playerGroup;
    RectTransform avatarFrame;
    RectTransform playerName;
    RectTransform topRow;
    RectTransform cricketRow;
    RectTransform score;
    RectTransform scoreLabel;
    RectTransform cricket1;
    RectTransform cricket2;
    RectTransform cricket3;

    private void OnEnable()
    {
        ApplyLayout();
    }

    private void OnValidate()
    {
        ApplyLayout();
    }

    public void SetSide(bool isRight)
    {
        rightSide = isRight;
        ApplyLayout();
    }

    public void ApplyLayout()
    {
        Bind();
        AlignTexts();
        ApplyBackgroundFlip();
        if (!rightSide) return;

        Transform src = LeftSourceRoot();
        if (src == null) return;

        MirrorFromSource(playerGroup, src, "player");
        MirrorFromSource(topRow, src, "TopRow");
        MirrorFromSource(cricketRow, src, "CricketRow");
        MirrorFromSource(scoreLabel, src, "ScoreLabel");
        MirrorFromSource(score, src, "Score");
        MirrorFromSource(avatarFrame, src, "PlayerFrame");
        MirrorFromSource(playerName, src, "PlayerName");
        // 右卡头像在右侧：三槽左右对调，第 1 只仍贴着头像。
        MirrorFromSource(cricket1, src, "Cricket1");
        MirrorFromSource(cricket2, src, "Cricket2");
        MirrorFromSource(cricket3, src, "Cricket3");
    }

    public void ApplyDefaultLeftLayout()
    {
        Bind();
        EnsureStructure();
        Bind();

        SetLeftEdge(playerGroup, new Vector2(10f, 0f), new Vector2(153.8f, 150.7f));
        SetLeftEdge(topRow, new Vector2(170f, 42f), new Vector2(260f, 42f));
        SetLeftEdge(cricketRow, new Vector2(170f, -25f), new Vector2(240f, 76f));
        SetLeftEdge(scoreLabel, Vector2.zero, new Vector2(60f, 42f));
        SetLeftEdge(score, new Vector2(80f, 0f), new Vector2(180f, 42f));

        SetCenter(FindRect("Cricket1"), new Vector2(-84f, 0f), new Vector2(76f, 76f));
        SetCenter(FindRect("Cricket2"), new Vector2(0f, 0f), new Vector2(76f, 76f));
        SetCenter(FindRect("Cricket3"), new Vector2(84f, 0f), new Vector2(76f, 76f));
        SetCenter(playerName, new Vector2(0f, -60f), new Vector2(150f, 42f));

        rightSide = false;
        AlignTexts();
        ApplyBackgroundFlip();
    }

    void Bind()
    {
        background = FindRect("Background") ?? FindRect("背景");
        playerGroup = FindRect("player") ?? FindRect("Player");
        avatarFrame = FindRect("PlayerFrame") ?? FindRect("AvatarFrame");
        playerName = FindRect("PlayerName");
        topRow = FindRect("TopRow");
        cricketRow = FindRect("CricketRow");
        score = FindRect("Score");
        scoreLabel = FindRect("ScoreLabel");
        cricket1 = FindRect("Cricket1");
        cricket2 = FindRect("Cricket2");
        cricket3 = FindRect("Cricket3");
    }

    void EnsureStructure()
    {
        var root = transform as RectTransform;
        if (root == null) return;

        if (playerGroup == null)
            playerGroup = CreateRect("player", root);
        if (topRow == null)
            topRow = CreateRect("TopRow", root);
        else if (topRow.parent != root)
            topRow.SetParent(root, false);
        if (cricketRow == null)
            cricketRow = CreateRect("CricketRow", root);
        else if (cricketRow.parent != root)
            cricketRow.SetParent(root, false);

        ParentTo(FindRect("PlayerFrame") ?? FindRect("AvatarFrame"), playerGroup);
        ParentTo(FindRect("PlayerName"), playerGroup);
        ParentTo(FindRect("ScoreLabel"), topRow);
        ParentTo(FindRect("Score"), topRow);
        ParentTo(FindRect("Cricket1"), cricketRow);
        ParentTo(FindRect("Cricket2"), cricketRow);
        ParentTo(FindRect("Cricket3"), cricketRow);
    }

    Transform LeftSourceRoot()
    {
#if UNITY_EDITOR
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath != null && stage.assetPath.EndsWith("BattlePlayer.prefab"))
        {
            Transform staged = stage.prefabContentsRoot != null ? stage.prefabContentsRoot.transform : null;
            if (staged != null && staged != transform) return staged;
        }

        GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
        if (source != null && source != gameObject) return source.transform;
#endif
        GameObject loaded = Resources.Load<GameObject>("Battle/Hud/Prefabs/BattlePlayer");
        if (loaded != null && loaded != gameObject) return loaded.transform;
        return null;
    }

    static void MirrorFromSource(RectTransform dest, Transform srcRoot, string childName)
    {
        if (dest == null || srcRoot == null) return;
        var src = FindNamed(srcRoot, childName) as RectTransform;
        if (src == null) return;
        dest.anchorMin = new Vector2(1f - src.anchorMin.x, src.anchorMin.y);
        dest.anchorMax = new Vector2(1f - src.anchorMax.x, src.anchorMax.y);
        dest.pivot = new Vector2(1f - src.pivot.x, src.pivot.y);
        dest.anchoredPosition = new Vector2(-src.anchoredPosition.x, src.anchoredPosition.y);
        dest.sizeDelta = src.sizeDelta;
    }

    void ApplyBackgroundFlip()
    {
        if (background == null) return;

        Vector3 scale = background.localScale;
        float absX = Mathf.Abs(scale.x) < 0.01f ? 1f : Mathf.Abs(scale.x);
        background.localScale = new Vector3(rightSide ? -absX : absX, scale.y, scale.z);
    }

    void AlignTexts()
    {
        SetAlign(playerName, rightSide);
        SetAlign(score, rightSide);
        SetAlign(scoreLabel, rightSide);
    }

    static void SetAlign(RectTransform rect, bool right)
    {
        if (rect == null) return;
        var text = rect.GetComponent<TMP_Text>();
        if (text == null) return;
        text.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
    }

    static void SetLeftEdge(RectTransform rect, Vector2 pos, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
    }

    static void SetCenter(RectTransform rect, Vector2 pos, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
    }

    static void ParentTo(RectTransform child, RectTransform parent)
    {
        if (child == null || parent == null || child.parent == parent) return;
        child.SetParent(parent, false);
    }

    static RectTransform CreateRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return rect;
    }

    RectTransform FindRect(string childName)
    {
        return FindNamed(transform, childName) as RectTransform;
    }

    static Transform FindNamed(Transform root, string childName)
    {
        if (root == null) return null;
        Transform direct = root.Find(childName);
        if (direct != null) return direct;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform nested = FindNamed(root.GetChild(i), childName);
            if (nested != null) return nested;
        }
        return null;
    }
}
