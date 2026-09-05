using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class FigmaPlayerCardLayout : MonoBehaviour
{
    [Tooltip("关掉时你可以自由改左边排版；打开后按左边镜像，不要手改右侧内部坐标。")]
    [SerializeField] public bool rightSide;

    [HideInInspector] [SerializeField] Vector2 leftAvatarPos;
    [HideInInspector] [SerializeField] Vector2 leftInfoPos;
    [HideInInspector] [SerializeField] Vector2 leftNamePos;
    [HideInInspector] [SerializeField] Vector2 leftScorePos;
    [HideInInspector] [SerializeField] bool hasLeftSnapshot;

    RectTransform avatarFrame;
    RectTransform infoColumn;
    RectTransform playerName;
    RectTransform score;

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

        if (!rightSide)
        {
            CaptureLeft();
            return;
        }

        Vector2 avatarPos;
        Vector2 infoPos;
        Vector2 namePos;
        Vector2 scorePos;
        if (!TryReadLeftFromPrefab(out avatarPos, out infoPos, out namePos, out scorePos))
        {
            if (!hasLeftSnapshot) CaptureLeft();
            avatarPos = leftAvatarPos;
            infoPos = leftInfoPos;
            namePos = leftNamePos;
            scorePos = leftScorePos;
        }

        MirrorX(avatarFrame, avatarPos);
        MirrorX(infoColumn, infoPos);
        MirrorX(playerName, namePos);
        MirrorX(score, scorePos);
    }

    public void ApplyDefaultLeftLayout()
    {
        Bind();
        EnsureStructure();
        Bind();

        var root = transform as RectTransform;
        if (root != null)
        {
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(424f, 140f);
        }

        SetRect(avatarFrame, new Vector2(-142f, 0f), new Vector2(124f, 124f));
        SetRect(infoColumn, new Vector2(68f, 0f), new Vector2(272f, 126f));

        var topRow = FindRect("TopRow");
        var cricketRow = FindRect("CricketRow");
        SetRect(topRow, new Vector2(0f, 42f), new Vector2(272f, 42f));
        SetRect(cricketRow, new Vector2(0f, -25f), new Vector2(272f, 76f));

        SetRect(playerName, new Vector2(-70f, 0f), new Vector2(132f, 42f));
        SetRect(score, new Vector2(70f, 0f), new Vector2(132f, 42f));

        SetRect(FindRect("Cricket1"), new Vector2(-84f, 0f), new Vector2(76f, 76f));
        SetRect(FindRect("Cricket2"), new Vector2(0f, 0f), new Vector2(76f, 76f));
        SetRect(FindRect("Cricket3"), new Vector2(84f, 0f), new Vector2(76f, 76f));

        rightSide = false;
        CaptureLeft();
        AlignTexts();
    }

    void Bind()
    {
        avatarFrame = FindRect("AvatarFrame");
        infoColumn = FindRect("InfoColumn");
        playerName = FindRect("PlayerName");
        score = FindRect("Score");
    }

    void EnsureStructure()
    {
        var root = transform as RectTransform;
        if (root == null) return;

        infoColumn = FindRect("InfoColumn");
        if (infoColumn == null)
            infoColumn = CreateRect("InfoColumn", root);

        var topRow = FindRect("TopRow");
        if (topRow == null)
            topRow = CreateRect("TopRow", infoColumn);
        else if (topRow.parent != infoColumn)
            topRow.SetParent(infoColumn, false);

        var cricketRow = FindRect("CricketRow");
        if (cricketRow == null)
            cricketRow = CreateRect("CricketRow", infoColumn);
        else if (cricketRow.parent != infoColumn)
            cricketRow.SetParent(infoColumn, false);

        ParentTo(FindRect("PlayerName"), topRow);
        ParentTo(FindRect("Score"), topRow);
        ParentTo(FindRect("Cricket1"), cricketRow);
        ParentTo(FindRect("Cricket2"), cricketRow);
        ParentTo(FindRect("Cricket3"), cricketRow);
    }

    void CaptureLeft()
    {
        if (avatarFrame != null) leftAvatarPos = avatarFrame.anchoredPosition;
        if (infoColumn != null) leftInfoPos = infoColumn.anchoredPosition;
        if (playerName != null) leftNamePos = playerName.anchoredPosition;
        if (score != null) leftScorePos = score.anchoredPosition;
        hasLeftSnapshot = avatarFrame != null && infoColumn != null;
    }

    bool TryReadLeftFromPrefab(out Vector2 avatarPos, out Vector2 infoPos, out Vector2 namePos, out Vector2 scorePos)
    {
        avatarPos = leftAvatarPos;
        infoPos = leftInfoPos;
        namePos = leftNamePos;
        scorePos = leftScorePos;
#if UNITY_EDITOR
        GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
        if (source == null || source == gameObject) return hasLeftSnapshot;
        Transform srcRoot = source.transform;
        RectTransform srcAvatar = FindNamed(srcRoot, "AvatarFrame") as RectTransform;
        RectTransform srcInfo = FindNamed(srcRoot, "InfoColumn") as RectTransform;
        RectTransform srcName = FindNamed(srcRoot, "PlayerName") as RectTransform;
        RectTransform srcScore = FindNamed(srcRoot, "Score") as RectTransform;
        if (srcAvatar != null && srcInfo != null)
        {
            avatarPos = srcAvatar.anchoredPosition;
            infoPos = srcInfo.anchoredPosition;
            if (srcName != null) namePos = srcName.anchoredPosition;
            if (srcScore != null) scorePos = srcScore.anchoredPosition;
            return true;
        }
#endif
        return hasLeftSnapshot;
    }

    static void MirrorX(RectTransform rect, Vector2 leftPos)
    {
        if (rect == null) return;
        rect.anchoredPosition = new Vector2(-leftPos.x, leftPos.y);
    }

    void AlignTexts()
    {
        var nameText = playerName != null ? playerName.GetComponent<TMP_Text>() : null;
        if (nameText != null)
            nameText.alignment = rightSide ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;

        var scoreText = score != null ? score.GetComponent<TMP_Text>() : null;
        if (scoreText != null)
            scoreText.alignment = rightSide ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
    }

    static void SetRect(RectTransform rect, Vector2 pos, Vector2 size)
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
        Transform found = FindNamed(transform, childName);
        return found as RectTransform;
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
