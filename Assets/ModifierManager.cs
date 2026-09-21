using System.Collections.Generic;
using TDTK;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A single modifier the player can pick. prerequisites are ids that must already be selected
// before this one becomes offerable, allowing simple "tech tree" style unlocks.
public class ModifierDef
{
    public string id;
    public string title;
    public string description;
    public List<string> prerequisites;
    public System.Action apply;

    public ModifierDef(string id, string title, string description, System.Action apply, params string[] prereq)
    {
        this.id = id;
        this.title = title;
        this.description = description;
        this.apply = apply;
        prerequisites = new List<string>(prereq);
    }
}

// Shows a modifier-choice popup at the start of certain planning turns (every 3 waves).
// The player picks 1 of 3 semi-randomly chosen options; picks are recorded so prerequisite-gated
// modifiers can unlock. Every option is a +1 gold placeholder for now.
public class ModifierManager : MonoBehaviour
{
    private static ModifierManager instance;
    public static ModifierManager GetInstance() { return instance; }

    private readonly List<ModifierDef> allModifiers = new List<ModifierDef>();
    private readonly HashSet<string> selectedIds = new HashSet<string>();

    // true while the popup is open; TurnManager checks this to block ending the turn
    private static bool popupOpen = false;
    public static bool IsBlocking() { return popupOpen; }

    private GameObject popupRoot;
    private TurnManager owner;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        popupOpen = false;
        BuildModifierList();
    }

    // placeholder catalogue: every modifier just grants +1 gold; a few require an earlier pick
    private void BuildModifierList()
    {
        System.Action gold = () => RscManager.GainRsc(new List<int> { 1 });

        allModifiers.Add(new ModifierDef("gold_a", "Gold Cache", "Gain +1 gold.", gold));
        allModifiers.Add(new ModifierDef("gold_b", "Gold Vein", "Gain +1 gold.", gold));
        allModifiers.Add(new ModifierDef("gold_c", "Gold Seam", "Gain +1 gold.", gold));
        // extra base (no-prereq) picks so at least 3 options remain across every offer
        allModifiers.Add(new ModifierDef("gold_g", "Gold Nugget", "Gain +1 gold.", gold));
        allModifiers.Add(new ModifierDef("gold_h", "Gold Lode", "Gain +1 gold.", gold));
        allModifiers.Add(new ModifierDef("gold_i", "Gold Reserve", "Gain +1 gold.", gold));
        allModifiers.Add(new ModifierDef("gold_d", "Refined Cache", "Gain +1 gold. (needs Gold Cache)", gold, "gold_a"));
        allModifiers.Add(new ModifierDef("gold_e", "Deep Vein", "Gain +1 gold. (needs Gold Vein)", gold, "gold_b"));
        allModifiers.Add(new ModifierDef("gold_f", "Master Hoard", "Gain +1 gold. (needs Refined Cache)", gold, "gold_d"));
    }

    // offer on turns 4, 7, 10, ... (i.e. at the start of every 3rd wave after the first three)
    private static bool ShouldOffer(int turnNumber) { return turnNumber >= 4 && (turnNumber - 4) % 3 == 0; }

    public static void OfferIfDue(int turnNumber, TurnManager tm)
    {
        if (instance == null || popupOpen) return;
        if (!ShouldOffer(turnNumber)) return;
        instance.Offer(tm);
    }

    private void Offer(TurnManager tm)
    {
        List<ModifierDef> offerable = GetOfferable();
        if (offerable.Count == 0) return;   // nothing left to unlock

        Shuffle(offerable);
        int count = Mathf.Min(3, offerable.Count);
        List<ModifierDef> choices = offerable.GetRange(0, count);

        owner = tm;
        popupOpen = true;
        if (owner != null) owner.SetEndTurnButtonVisible(false);
        BuildPopup(choices);
    }

    // modifiers not yet taken whose prerequisites are all already selected
    private List<ModifierDef> GetOfferable()
    {
        List<ModifierDef> list = new List<ModifierDef>();
        for (int i = 0; i < allModifiers.Count; i++)
        {
            ModifierDef m = allModifiers[i];
            if (selectedIds.Contains(m.id)) continue;

            bool ok = true;
            for (int p = 0; p < m.prerequisites.Count; p++)
            {
                if (!selectedIds.Contains(m.prerequisites[p])) { ok = false; break; }
            }
            if (ok) list.Add(m);
        }
        return list;
    }

    private void Choose(ModifierDef m)
    {
        if (m.apply != null) m.apply();
        selectedIds.Add(m.id);
        Close();
    }

    private void Close()
    {
        popupOpen = false;
        if (popupRoot != null) { Destroy(popupRoot); popupRoot = null; }
        if (owner != null) owner.SetEndTurnButtonVisible(true);
    }

    private void Shuffle(List<ModifierDef> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            ModifierDef t = list[i]; list[i] = list[j]; list[j] = t;
        }
    }

    // ---------- runtime-built UI ----------

    // palette matched to the game's gold-on-dark fantasy HUD
    private static readonly Color Gold = new Color(0.82f, 0.65f, 0.22f, 1f);
    private static readonly Color GoldBright = new Color(0.96f, 0.82f, 0.38f, 1f);
    private static readonly Color PanelFill = new Color(0.11f, 0.10f, 0.08f, 0.98f);
    private static readonly Color CardFill = new Color(0.17f, 0.14f, 0.10f, 1f);
    private static readonly Color CardHover = new Color(0.28f, 0.22f, 0.13f, 1f);
    private static readonly Color CardPressed = new Color(0.36f, 0.28f, 0.16f, 1f);
    private static readonly Color DescGrey = new Color(0.78f, 0.74f, 0.66f, 1f);

    private void BuildPopup(List<ModifierDef> choices)
    {
        EnsureEventSystem();

        popupRoot = new GameObject("ModifierPopupCanvas");
        Canvas canvas = popupRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = popupRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        popupRoot.AddComponent<GraphicRaycaster>();

        // full-screen dim that also swallows clicks meant for the HUD behind it
        GameObject dim = CreateUIObject("Dim", popupRoot.transform);
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.72f);
        Stretch(dim.GetComponent<RectTransform>());

        // gold-framed dark panel
        GameObject inner;
        GameObject panel = CreateBordered("Panel", popupRoot.transform, Gold, PanelFill, 3, out inner);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(720, 540);

        VerticalLayoutGroup vlg = inner.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(28, 28, 24, 36);
        vlg.spacing = 14;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        GameObject titleGO = CreateText("Title", "Choose a Modifier", 34, inner.transform);
        Text titleTxt = titleGO.GetComponent<Text>();
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.color = GoldBright;
        Shadow ts = titleGO.AddComponent<Shadow>();
        ts.effectColor = new Color(0, 0, 0, 0.6f);
        ts.effectDistance = new Vector2(1.5f, -1.5f);
        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.minHeight = titleLE.preferredHeight = 46;

        GameObject subGO = CreateText("Subtitle", "Pick one \u2014 it applies immediately", 18, inner.transform);
        Text subTxt = subGO.GetComponent<Text>();
        subTxt.alignment = TextAnchor.MiddleCenter;
        subTxt.color = DescGrey;
        LayoutElement subLE = subGO.AddComponent<LayoutElement>();
        subLE.minHeight = subLE.preferredHeight = 22;

        for (int i = 0; i < choices.Count; i++) BuildChoiceButton(inner.transform, choices[i]);
    }

    private void BuildChoiceButton(Transform parent, ModifierDef m)
    {
        // gold-framed card; the inner fill is the button's tinted target graphic
        GameObject fill;
        GameObject card = CreateBordered("Choice_" + m.id, parent, Gold, Color.white, 2, out fill);
        LayoutElement cardLE = card.AddComponent<LayoutElement>();
        cardLE.minHeight = cardLE.preferredHeight = 92;

        Image fillImg = fill.GetComponent<Image>();
        Button btn = card.AddComponent<Button>();
        btn.targetGraphic = fillImg;
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = CardFill;
        cb.highlightedColor = CardHover;
        cb.pressedColor = CardPressed;
        cb.selectedColor = CardFill;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        GameObject titleGO = CreateText("Title", m.title, 24, fill.transform);
        Text titleTxt = titleGO.GetComponent<Text>();
        titleTxt.alignment = TextAnchor.LowerCenter;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.color = GoldBright;
        titleTxt.raycastTarget = false;
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.5f);
        titleRT.anchorMax = new Vector2(1, 1f);
        titleRT.offsetMin = new Vector2(12, 0);
        titleRT.offsetMax = new Vector2(-12, -6);

        GameObject descGO = CreateText("Desc", m.description, 18, fill.transform);
        Text descTxt = descGO.GetComponent<Text>();
        descTxt.alignment = TextAnchor.UpperCenter;
        descTxt.color = DescGrey;
        descTxt.raycastTarget = false;
        RectTransform descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0, 0f);
        descRT.anchorMax = new Vector2(1, 0.5f);
        descRT.offsetMin = new Vector2(12, 6);
        descRT.offsetMax = new Vector2(-12, 0);

        ModifierDef captured = m;
        btn.onClick.AddListener(() => Choose(captured));
    }

    // makes a border-colored image with an inset fill-colored child; returns the outer object and the inner fill
    private GameObject CreateBordered(string name, Transform parent, Color border, Color fill, int thickness, out GameObject inner)
    {
        GameObject outer = CreateUIObject(name, parent);
        Image borderImg = outer.AddComponent<Image>();
        borderImg.color = border;

        inner = CreateUIObject("Fill", outer.transform);
        Image fillImg = inner.AddComponent<Image>();
        fillImg.color = fill;
        RectTransform rt = inner.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(thickness, thickness);
        rt.offsetMax = new Vector2(-thickness, -thickness);
        return outer;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private GameObject CreateText(string name, string content, int size, Transform parent)
    {
        GameObject go = CreateUIObject(name, parent);
        Text t = go.AddComponent<Text>();
        t.text = content;
        t.font = GetFont();
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return go;
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }
}
