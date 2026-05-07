using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Project.Core;
using Project.Core.Events;
using Project.Data;
using Project.Systems;

namespace Project.UI
{
    // UI Toolkit 컨트롤러. 정적 구조는 HUD.uxml + HUD.uss로 분리,
    // 트랙 카드 8개 + 가챠 버튼 N개는 코드로 동적 생성하여 컨테이너 슬롯에 추가.
    //
    // 갱신 패턴 (S2 이후):
    //   - GameEventsSO 채널 구독 → Cash/Gems/Rating/DayRevenue/Track/Phase/Staff 변경 시점에만 갱신.
    //   - Update()는 매 프레임 변하는 timeBar/dayTimeLabel 만 담당 (이건 본질적으로 dt 기반).
    //   - 이전 매 프레임 폴링(UpdateHud/UpdateTrackCards/...) 제거 → string allocation 大폭 감소.
    public sealed class UIController : MonoBehaviour
    {
        [SerializeField] PanelSettings panelSettings;
        [SerializeField] VisualTreeAsset hudUxml;
        [SerializeField] StyleSheet hudUss;
        [Tooltip("GameEvents.asset (없으면 폴링 폴백 X — 이벤트 기반 패턴 강제). GameManager 와 같은 SO 를 참조.")]
        [SerializeField] GameEventsSO gameEvents;
        [Tooltip("TrackCard.uxml — 비어있으면 코드 생성 폴백 (이전 동작).")]
        [SerializeField] VisualTreeAsset trackCardTemplate;

        GameManager gm;
        UIDocument doc;
        bool initialized;

        // HUD elements (UXML에서 query)
        Label cashLabel, gemsLabel, ratingLabel;
        Label dayLabel, dayTimeLabel, goalLabel;
        ProgressBar timeBar, goalBar;
        VisualElement dayEndOverlay;
        Label dayEndTitle, dayEndBody;
        VisualElement gachaOverlay;
        Label gachaTitle, gachaBody;
        VisualElement gachaRarityBadge;

        // 하단 탭
        VisualElement upgradesScroll, gachaScroll;
        VisualElement tabUpgrades, tabGacha;

        // 동적 컬렉션
        readonly Dictionary<string, TrackCardEntry> trackCards = new();
        readonly List<(Button btn, StaffPackSO pack)> gachaButtons = new();

        struct TrackCardEntry
        {
            public VisualElement Card;
            public Label LvPill;
            public Label PreviewCurrent;
            public Label PreviewNext;
            public VisualElement UpgradeBtn;
            public Label UpgradeCost;
        }

        void Start()
        {
            if (initialized) return;
            gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null) { Debug.LogError("[UIController] GameManager 없음.", this); enabled = false; return; }

            doc = GetComponent<UIDocument>();
            if (doc == null) doc = gameObject.AddComponent<UIDocument>();
            if (doc.panelSettings == null && panelSettings != null) doc.panelSettings = panelSettings;
            if (doc.panelSettings == null)
            {
                Debug.LogWarning("[UIController] PanelSettings 미설정 — HUD 미표시.", this);
                enabled = false; return;
            }

            if (hudUxml != null) doc.visualTreeAsset = hudUxml;

            var root = doc.rootVisualElement;
            if (hudUss != null && !root.styleSheets.Contains(hudUss)) root.styleSheets.Add(hudUss);
            root.pickingMode = PickingMode.Ignore;

            // UXML이 없으면 코드 폴백 (이전 버전 호환)
            if (hudUxml == null)
            {
                Debug.LogWarning("[UIController] HUD.uxml 미연결 — 코드 폴백으로 빌드. EditorScript 'Build Static Scene' 실행 권장.", this);
                BuildAllCodeFallback(root);
            }
            else
            {
                QueryElements(root);
                BuildDynamicChildren(root);
            }

            initialized = true;

            // 이벤트 구독 후 현재 state 1회 push (initial sync).
            SubscribeEvents();
            SyncAll();
        }

        void OnDisable()
        {
            UnsubscribeEvents();
        }

        void Update()
        {
            // 매 프레임 변경되는 것만 갱신 (timeBar / dayTime 잔여 초). 나머지는 모두 이벤트 기반.
            if (!initialized || gm == null || timeBar == null) return;
            float dayLen = gm.DaySystem.DayLengthSec;
            float elapsed = gm.DaySystem.DayElapsed;
            timeBar.value = (elapsed / Mathf.Max(0.001f, dayLen)) * 100f;
            float remain = Mathf.Max(0f, dayLen - elapsed);
            dayTimeLabel.text = $"{remain:F0}s";
        }

        // ================== 이벤트 구독 ==================

        void SubscribeEvents()
        {
            if (gameEvents == null) return;
            gameEvents.OnCashChanged += HandleCash;
            gameEvents.OnGemsChanged += HandleGems;
            gameEvents.OnRatingChanged += HandleRating;
            gameEvents.OnDayRevenueChanged += HandleDayRevenue;
            gameEvents.OnTrackUpgraded += HandleTrackChanged;
            gameEvents.OnDayPhaseChanged += HandleDayPhaseChanged;
            gameEvents.OnDayChanged += HandleDayChanged;
            gameEvents.OnStaffChanged += HandleStaffChanged;
        }

        void UnsubscribeEvents()
        {
            if (gameEvents == null) return;
            gameEvents.OnCashChanged -= HandleCash;
            gameEvents.OnGemsChanged -= HandleGems;
            gameEvents.OnRatingChanged -= HandleRating;
            gameEvents.OnDayRevenueChanged -= HandleDayRevenue;
            gameEvents.OnTrackUpgraded -= HandleTrackChanged;
            gameEvents.OnDayPhaseChanged -= HandleDayPhaseChanged;
            gameEvents.OnDayChanged -= HandleDayChanged;
            gameEvents.OnStaffChanged -= HandleStaffChanged;
        }

        // 이벤트 구독 직후 1회 호출 — 현재 state를 모든 element에 push.
        void SyncAll()
        {
            var s = gm.State;
            HandleCash(s.Cash);
            HandleGems(s.Gems);
            HandleRating(s.Rating);
            HandleDayRevenue(s.DayRevenue);
            HandleTrackChanged();
            HandleDayChanged();
            HandleStaffChanged();
            HandleDayPhaseChanged();
        }

        // ================== 이벤트 핸들러 (값 기반) ==================

        void HandleCash(float v)
        {
            if (cashLabel != null) cashLabel.text = $"💰 ${v:F0}";
            // Cash 변동은 트랙 affordability 에 영향 → 카드 색 갱신 필요.
            RefreshTrackCardAffordability();
        }

        void HandleGems(int v)
        {
            if (gemsLabel != null) gemsLabel.text = $"💎 {v}";
            RefreshGachaButtons();
        }

        void HandleRating(int v)
        {
            if (ratingLabel != null) ratingLabel.text = $"⭐ {v}";
            RefreshGoalLabel(); // Lv 변경 시 목표값도 변동
        }

        void HandleDayRevenue(float v)
        {
            RefreshGoalLabel();
        }

        void HandleTrackChanged()
        {
            RefreshAllTrackCards();
        }

        void HandleDayChanged()
        {
            if (dayLabel != null) dayLabel.text = $"Day {gm.State.Day}";
            if (timeBar != null) timeBar.value = 0f;
            // Day 시작 시 트랙 효과(seating cap 등)가 변동 가능 (venue lv 등) → 카드 preview 재갱신.
            RefreshAllTrackCards();
        }

        void HandleStaffChanged()
        {
            RefreshGachaButtons();
        }

        void HandleDayPhaseChanged()
        {
            bool show = gm.DaySystem.Phase == DayPhase.Transitioning;
            if (dayEndOverlay != null) dayEndOverlay.EnableInClassList("visible", show);

            if (show)
            {
                if (gachaOverlay != null) gachaOverlay.RemoveFromClassList("visible");
                var r = gm.DaySystem.LastResult;
                if (dayEndTitle != null) dayEndTitle.text = $"Day {r.Day}  " + (r.Achieved ? "🎉 목표 달성" : "목표 미달");
                if (dayEndBody != null)
                    dayEndBody.text =
                        $"💰 ${r.Revenue:F0} / ${r.Goal}\n" +
                        $"💎 +{r.GemReward}" + (r.StarEarned > 0 ? $"   ⭐ +{r.StarEarned}" : "") +
                        (r.LeveledUp ? $"\n🆙 명성 Lv {r.NewLv} 달성!" : "");
            }
        }

        // ================== 갱신 헬퍼 ==================

        void RefreshGoalLabel()
        {
            if (goalLabel == null) return;
            var s = gm.State;
            var rep = Formulas.ComputeRep(s.Rating, gm.Config.RepLevelTable);
            goalLabel.text = $"Lv {rep.Lv}  ${s.DayRevenue:F0} / ${rep.Goal}" + (rep.IsMax ? "  (MAX)" : "");
            if (goalBar != null) goalBar.value = Mathf.Min(100f, s.DayRevenue / Mathf.Max(1f, rep.Goal) * 100f);
        }

        void RefreshAllTrackCards()
        {
            var ts = gm.TrackSystem;
            foreach (var t in gm.Config.Tracks)
            {
                if (t == null) continue;
                if (!trackCards.TryGetValue(t.Id, out var entry)) continue;
                int lv = ts.GetLv(t);
                bool maxed = ts.IsMaxed(t);
                int cost = maxed ? 0 : ts.GetCost(t);
                bool affordable = !maxed && gm.State.Cash >= cost;

                entry.LvPill.text = $"Lv {lv}";
                entry.PreviewCurrent.text = TrackPreview(t, lv);
                entry.PreviewNext.text = maxed ? "MAX" : TrackPreview(t, lv + 1);
                entry.UpgradeCost.text = maxed ? "MAX" : $"${cost}";

                bool dim = maxed || !affordable;
                entry.UpgradeBtn.EnableInClassList("disabled", dim);
                entry.Card.EnableInClassList("maxed", maxed);
            }
        }

        // Cash 변동만으로 cost 변경 X → affordability 색만 다시 평가 (preview/Lv 텍스트 안 건드림).
        void RefreshTrackCardAffordability()
        {
            var ts = gm.TrackSystem;
            foreach (var t in gm.Config.Tracks)
            {
                if (t == null) continue;
                if (!trackCards.TryGetValue(t.Id, out var entry)) continue;
                bool maxed = ts.IsMaxed(t);
                bool affordable = !maxed && gm.State.Cash >= ts.GetCost(t);
                bool dim = maxed || !affordable;
                entry.UpgradeBtn.EnableInClassList("disabled", dim);
            }
        }

        void RefreshGachaButtons()
        {
            int staffCount = gm.StaffSystem.Staffs.Count;
            int maxStaff = gm.Config.MaxStaff;
            bool teamFull = staffCount >= maxStaff;
            int gems = gm.State.Gems;

            for (int i = 0; i < gachaButtons.Count; i++)
            {
                var (btn, pack) = gachaButtons[i];
                bool affordable = gems >= pack.GemCost;
                bool enabled = !teamFull && affordable;
                btn.SetEnabled(enabled);
                btn.text = teamFull
                    ? $"{pack.DisplayName}\n팀 가득 ({maxStaff}/{maxStaff})"
                    : $"💎 {pack.DisplayName}\n{pack.GemCost}";
            }
        }

        // ================== UXML Query ==================

        void QueryElements(VisualElement root)
        {
            cashLabel    = root.Q<Label>("cash");
            gemsLabel    = root.Q<Label>("gems");
            ratingLabel  = root.Q<Label>("rating");
            dayLabel     = root.Q<Label>("day");
            dayTimeLabel = root.Q<Label>("day-time");
            goalLabel    = root.Q<Label>("goal");
            timeBar      = root.Q<ProgressBar>("time-bar");
            goalBar      = root.Q<ProgressBar>("goal-bar");

            dayEndOverlay = root.Q<VisualElement>("day-end-overlay");
            dayEndTitle   = root.Q<Label>("day-end-title");
            dayEndBody    = root.Q<Label>("day-end-body");

            gachaOverlay      = root.Q<VisualElement>("gacha-overlay");
            gachaTitle        = root.Q<Label>("gacha-title");
            gachaBody         = root.Q<Label>("gacha-body");
            gachaRarityBadge  = root.Q<VisualElement>("gacha-rarity-badge");

            var closeBtn = root.Q<Button>("gacha-close-btn");
            if (closeBtn != null)
                closeBtn.clicked += () => gachaOverlay.RemoveFromClassList("visible");

            // 하단 탭
            upgradesScroll = root.Q<VisualElement>("upgrades-scroll");
            gachaScroll    = root.Q<VisualElement>("gacha-scroll");
            tabUpgrades    = root.Q<VisualElement>("tab-upgrades");
            tabGacha       = root.Q<VisualElement>("tab-gacha");
            if (tabUpgrades != null) tabUpgrades.RegisterCallback<ClickEvent>(_ => SetActiveTab("upgrades"));
            if (tabGacha    != null) tabGacha   .RegisterCallback<ClickEvent>(_ => SetActiveTab("gacha"));
            SetActiveTab("upgrades"); // 기본 탭
        }

        // 하단 탭 전환. id: "upgrades" | "gacha"
        void SetActiveTab(string id)
        {
            bool upgrades = id == "upgrades";
            if (upgradesScroll != null) upgradesScroll.EnableInClassList("hidden", !upgrades);
            if (gachaScroll    != null) gachaScroll   .EnableInClassList("hidden", upgrades);
            if (tabUpgrades    != null) tabUpgrades   .EnableInClassList("active", upgrades);
            if (tabGacha       != null) tabGacha      .EnableInClassList("active", !upgrades);
        }

        void BuildDynamicChildren(VisualElement root)
        {
            // 단일 grid 컨테이너에 8 트랙 모두 add (USS flex-wrap으로 2-col 자동 배치).
            // Active 4 → Passive 4 순서로 add해 위쪽 2행 = Active, 아래쪽 2행 = Passive 가 됨.
            var trackGrid = root.Q<VisualElement>("track-grid");
            var gachaGrid = root.Q<VisualElement>("gacha-grid");
            if (trackGrid == null || gachaGrid == null) return;

            foreach (var t in gm.Config.Tracks)
            {
                if (t == null) continue;
                if (t.Axis == TrackAxis.Active) trackGrid.Add(BuildTrackCard(t));
            }
            foreach (var t in gm.Config.Tracks)
            {
                if (t == null) continue;
                if (t.Axis != TrackAxis.Active) trackGrid.Add(BuildTrackCard(t));
            }

            for (int i = 0; i < gm.Config.StaffPacks.Count; i++)
            {
                var pack = gm.Config.StaffPacks[i];
                if (pack == null) continue;
                gachaGrid.Add(BuildGachaButton(pack));
            }
        }

        // 모바일 레퍼런스 카드 레이아웃:
        //   [icon-box] [name | Lv pill]      (i)
        //   [현재값 → 다음값(녹색)]
        //   [Upgrade            $cost]
        //
        // trackCardTemplate (UXML) 가 있으면 Instantiate + Q<>(). 없으면 코드 생성 폴백.
        VisualElement BuildTrackCard(TrackConfigSO t)
        {
            if (trackCardTemplate != null) return BuildTrackCardFromTemplate(t);
            return BuildTrackCardCodeFallback(t);
        }

        VisualElement BuildTrackCardFromTemplate(TrackConfigSO t)
        {
            var card = trackCardTemplate.Instantiate();
            // Instantiate 결과는 wrapper element. 실제 트랙 카드는 child 0 — class "track-card".
            // 직접 wrapper 를 grid 에 add 하면 USS layout 이 어긋날 수 있어 child 를 추출.
            var root = card.childCount > 0 ? card[0] : card;

            // 동적 영역 채우기
            var iconBox = root.Q<VisualElement>("icon-box");
            if (iconBox != null)
            {
                var c = t.Color;
                iconBox.style.backgroundColor = new Color(c.r, c.g, c.b, 0.18f);
            }
            var icon = root.Q<Label>("icon");
            if (icon != null) icon.text = string.IsNullOrEmpty(t.Icon) ? "?" : t.Icon;
            var name = root.Q<Label>("name");
            if (name != null) name.text = t.DisplayName;

            var lvPill = root.Q<Label>("lv-pill");
            var prevCurrent = root.Q<Label>("preview-current");
            var prevNext = root.Q<Label>("preview-next");
            var upgradeBtn = root.Q<VisualElement>("upgrade-btn");
            var upgradeCost = root.Q<Label>("upgrade-cost");

            if (upgradeBtn != null)
                upgradeBtn.RegisterCallback<ClickEvent>(_ => OnTrackClicked(t));

            trackCards[t.Id] = new TrackCardEntry
            {
                Card = root,
                LvPill = lvPill,
                PreviewCurrent = prevCurrent,
                PreviewNext = prevNext,
                UpgradeBtn = upgradeBtn,
                UpgradeCost = upgradeCost,
            };
            return root;
        }

        // 코드 생성 폴백 — UXML template 미연결 시 동일 구조 동적 빌드.
        VisualElement BuildTrackCardCodeFallback(TrackConfigSO t)
        {
            var card = new VisualElement();
            card.AddToClassList("track-card");

            var header = new VisualElement();
            header.AddToClassList("track-card-header");

            var iconBox = new VisualElement();
            iconBox.AddToClassList("track-card-icon-box");
            var c = t.Color;
            iconBox.style.backgroundColor = new Color(c.r, c.g, c.b, 0.18f);
            var icon = new Label(string.IsNullOrEmpty(t.Icon) ? "?" : t.Icon);
            icon.AddToClassList("track-card-icon");
            iconBox.Add(icon);
            header.Add(iconBox);

            var titleCol = new VisualElement();
            titleCol.AddToClassList("track-card-title-col");
            var name = new Label(t.DisplayName);
            name.AddToClassList("track-card-name");
            titleCol.Add(name);
            var lvPill = new Label("Lv 1");
            lvPill.AddToClassList("track-card-lv-pill");
            titleCol.Add(lvPill);
            header.Add(titleCol);

            var info = new Label("ⓘ");
            info.AddToClassList("track-card-info");
            header.Add(info);
            card.Add(header);

            var prevRow = new VisualElement();
            prevRow.AddToClassList("track-card-preview-row");
            var prevCurrent = new Label("");
            prevCurrent.AddToClassList("track-card-preview-current");
            prevRow.Add(prevCurrent);
            var arrow = new Label("→");
            arrow.AddToClassList("track-card-preview-arrow");
            prevRow.Add(arrow);
            var prevNext = new Label("");
            prevNext.AddToClassList("track-card-preview-next");
            prevRow.Add(prevNext);
            card.Add(prevRow);

            var upgradeBtn = new VisualElement();
            upgradeBtn.AddToClassList("track-upgrade-btn");
            upgradeBtn.RegisterCallback<ClickEvent>(_ => OnTrackClicked(t));
            var upText = new Label("Upgrade");
            upText.AddToClassList("track-upgrade-text");
            upgradeBtn.Add(upText);
            var upCost = new Label("$0");
            upCost.AddToClassList("track-upgrade-cost");
            upgradeBtn.Add(upCost);
            card.Add(upgradeBtn);

            trackCards[t.Id] = new TrackCardEntry
            {
                Card = card,
                LvPill = lvPill,
                PreviewCurrent = prevCurrent,
                PreviewNext = prevNext,
                UpgradeBtn = upgradeBtn,
                UpgradeCost = upCost,
            };
            return card;
        }

        Button BuildGachaButton(StaffPackSO pack)
        {
            var btn = new Button(() => OnGachaClicked(pack));
            btn.AddToClassList("gacha-btn");
            btn.text = $"💎 {pack.DisplayName}\n{pack.GemCost}";
            gachaButtons.Add((btn, pack));
            return btn;
        }

        // ================== 클릭 핸들러 ==================

        void OnTrackClicked(TrackConfigSO track)
        {
            if (gm.DaySystem.Phase != DayPhase.Playing) return;
            var result = gm.TrackSystem.TryUpgrade(track);
            if (result == TrackSystem.BuyResult.Success)
                Debug.Log($"[Track] {track.DisplayName} → Lv {gm.TrackSystem.GetLv(track)} (잔여 ${gm.State.Cash:F0})");
        }

        void OnGachaClicked(StaffPackSO pack)
        {
            if (gm.DaySystem.Phase != DayPhase.Playing) return;
            var result = gm.GachaSystem.Buy(pack, out var rarity, out var name);
            ShowGachaResult(pack, result, rarity, name);
        }

        void ShowGachaResult(StaffPackSO pack, GachaSystem.BuyResult result, RarityConfigSO rarity, string name)
        {
            if (gachaOverlay == null) return;
            string title; string body;
            Color titleColor = new Color(0.12f, 0.16f, 0.22f);
            bool showBadge = false;
            Color badgeColor = Color.gray;

            switch (result)
            {
                case GachaSystem.BuyResult.Success:
                    title = "🎉 " + (rarity != null ? rarity.DisplayName : "");
                    body = $"{name}\n({pack.DisplayName})";
                    if (rarity != null)
                    {
                        titleColor = rarity.Color;
                        badgeColor = rarity.Color;
                        showBadge = true;
                    }
                    break;
                case GachaSystem.BuyResult.NotEnoughGems:
                    title = "💎 부족";
                    body = $"{pack.DisplayName} 비용 {pack.GemCost}\n현재 보유 {gm.State.Gems}";
                    break;
                case GachaSystem.BuyResult.MaxStaffReached:
                    title = "팀 가득";
                    body = $"직원 최대 {gm.Config.MaxStaff}명까지\n현재 {gm.StaffSystem.Staffs.Count}명";
                    break;
                default:
                    title = "오류"; body = result.ToString();
                    break;
            }
            if (gachaTitle != null) { gachaTitle.text = title; gachaTitle.style.color = titleColor; }
            if (gachaBody != null) gachaBody.text = body;
            if (gachaRarityBadge != null)
            {
                gachaRarityBadge.EnableInClassList("visible", showBadge);
                if (showBadge) gachaRarityBadge.style.backgroundColor = badgeColor;
            }
            gachaOverlay.AddToClassList("visible");
        }

        string TrackPreview(TrackConfigSO t, int lv)
        {
            var cfg = gm.Config;
            switch (t.Id)
            {
                case "menu":    return $"가격×{Formulas.PriceMult(lv, cfg):F2}";
                case "staff":   return $"속도×{1f / Formulas.StaffSpdMult(lv, cfg):F2}";
                case "service": return $"식사×{Formulas.EatingSpdMult(lv, cfg):F2}";
                case "venue":   return $"{Formulas.SeatingCap(lv, cfg)}석/{Formulas.QueueCap(lv, cfg)}대기";
                case "takeout": return $"${Formulas.TakeoutPerSec(lv, cfg):F2}/s";
                case "ads":     return $"${Formulas.AdsPerSec(lv, cfg):F2}/s";
                case "tip":     return $"${Formulas.TipPerSec(lv, cfg):F2}/s";
                case "merch":   return $"${Formulas.MerchPerSec(lv, cfg):F2}/s";
            }
            return "";
        }

        // ================== 코드 폴백 (UXML 미연결 시) ==================

        void BuildAllCodeFallback(VisualElement root)
        {
            // UXML 없을 때 최소 HUD만 코드로 띄움 (전체 동작 확인용 안전망). 1080×1920 ref 사이즈.
            var card = new VisualElement();
            card.style.backgroundColor = new Color(1f, 1f, 1f, 0.96f);
            card.style.paddingTop = 28; card.style.paddingBottom = 28;
            card.style.paddingLeft = 28; card.style.paddingRight = 28;
            card.style.marginLeft = 22; card.style.marginRight = 22; card.style.marginTop = 22;

            cashLabel   = NewLabel("💰 $0", 40, true, new Color(0.09f, 0.65f, 0.30f));
            gemsLabel   = NewLabel("💎 0",  40, true, new Color(0.15f, 0.39f, 0.92f));
            ratingLabel = NewLabel("⭐ 0",  40, true, new Color(0.85f, 0.47f, 0.02f));
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.SpaceAround;
            row.Add(cashLabel); row.Add(gemsLabel); row.Add(ratingLabel);
            card.Add(row);

            dayLabel     = NewLabel("Day 1", 28, true, new Color(0.12f, 0.16f, 0.22f));
            dayTimeLabel = NewLabel("75s",   28, true, new Color(0.12f, 0.16f, 0.22f));
            var row2 = new VisualElement(); row2.style.flexDirection = FlexDirection.Row; row2.style.justifyContent = Justify.SpaceBetween; row2.style.marginTop = 22;
            row2.Add(dayLabel); row2.Add(dayTimeLabel);
            card.Add(row2);

            timeBar = new ProgressBar { lowValue = 0, highValue = 100, value = 0 };
            timeBar.style.height = 12; timeBar.style.marginTop = 8;
            card.Add(timeBar);

            goalLabel = NewLabel("Lv 1  $0 / $20", 28, true, new Color(0.12f, 0.16f, 0.22f));
            goalLabel.style.marginTop = 22; card.Add(goalLabel);

            goalBar = new ProgressBar { lowValue = 0, highValue = 100, value = 0 };
            goalBar.style.height = 20; goalBar.style.marginTop = 8;
            card.Add(goalBar);

            root.Add(card);
        }

        static Label NewLabel(string text, int size, bool bold, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.color = color;
            return l;
        }

        // 외부 호출용 (호환)
        public void Init(GameManager gm, PanelSettings ps)
        {
            if (initialized) return;
            this.gm = gm;
            if (panelSettings == null) panelSettings = ps;
            if (gameObject.activeInHierarchy && enabled) Start();
        }
    }
}
