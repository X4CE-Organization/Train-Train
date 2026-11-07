using System.Collections.Generic;
using UnityEngine;

namespace TrainTrain
{
    public sealed class TrainTrainUI : MonoBehaviour
    {
        private enum Overlay
        {
            None,
            Modes,
            Help,
            About,
            Depot
        }

        private static readonly Color Panel = new Color(0.055f, 0.078f, 0.095f, 0.93f);
        private static readonly Color PanelSoft = new Color(0.07f, 0.095f, 0.11f, 0.88f);
        private static readonly Color Paper = new Color(0.95f, 0.94f, 0.89f);
        private static readonly Color Muted = new Color(0.64f, 0.69f, 0.68f);
        private static readonly Color Danger = new Color(0.94f, 0.29f, 0.31f);
        private static readonly Color Success = new Color(0.3f, 0.82f, 0.63f);

        private readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
        private Texture2D whiteTexture;
        private Font font;
        private TrainTrainApp app;
        private Overlay overlay;

        public static float ScaleForScreen()
        {
            return Mathf.Clamp(Screen.height / 900f, 0.7f, 1.35f);
        }

        public void Initialize(TrainTrainApp owner)
        {
            app = owner;
            whiteTexture = Texture2D.whiteTexture;
            font = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    "PingFang SC",
                    "Hiragino Sans GB",
                    "Noto Sans CJK SC",
                    "Source Han Sans SC",
                    "Microsoft YaHei",
                    "Arial Unicode MS",
                    "Arial"
                },
                26);
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        public void OpenModeOverlay()
        {
            overlay = Overlay.Modes;
        }

        public void OpenDepotOverlay()
        {
            overlay = Overlay.Depot;
        }

        private void OnGUI()
        {
            if (app == null)
            {
                return;
            }

            float scale = ScaleForScreen();
            float virtualWidth = Screen.width / scale;
            float virtualHeight = Screen.height / scale;
            Vector2 mouse = Event.current.mousePosition / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            switch (app.State)
            {
                case GameScreen.Title:
                    DrawTitle(virtualWidth, virtualHeight, mouse);
                    break;
                case GameScreen.LevelSelect:
                    DrawLevelSelect(virtualWidth, virtualHeight, mouse);
                    break;
                case GameScreen.Playing:
                    DrawWorldLabels(virtualWidth, virtualHeight);
                    DrawGameHud(virtualWidth, virtualHeight, mouse);
                    break;
                case GameScreen.Paused:
                    DrawWorldLabels(virtualWidth, virtualHeight);
                    DrawGameHud(virtualWidth, virtualHeight, mouse);
                    DrawPause(virtualWidth, virtualHeight, mouse);
                    break;
                case GameScreen.Editor:
                    DrawWorldLabels(virtualWidth, virtualHeight);
                    DrawEditor(virtualWidth, virtualHeight, mouse);
                    break;
                case GameScreen.Result:
                    DrawWorldLabels(virtualWidth, virtualHeight);
                    DrawGameHud(virtualWidth, virtualHeight, mouse);
                    DrawResult(virtualWidth, virtualHeight, mouse);
                    break;
            }

            bool gameplayFooter = app.State == GameScreen.Playing || app.State == GameScreen.Paused;
            GUIStyle copyright = Style(12, gameplayFooter ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter, new Color(0.94f, 0.94f, 0.89f, 0.55f));
            Rect copyrightRect = gameplayFooter
                ? new Rect(virtualWidth - 270f, virtualHeight - 24f, 250f, 18f)
                : new Rect(0f, virtualHeight - 24f, virtualWidth, 18f);
            GUI.Label(copyrightRect, "© 2026 X4CE  ·  Train Train / 混乱火车", copyright);
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawTitle(float width, float height, Vector2 mouse)
        {
            if (overlay != Overlay.None)
            {
                DrawTitlePanel(width, height, mouse);
                DrawInfoOverlay(width, height, mouse);
                return;
            }

            DrawTitlePanel(width, height, mouse);
        }

        private void DrawTitlePanel(float width, float height, Vector2 mouse)
        {
            Color accent = RailPalette.Amber;
            float panelWidth = Mathf.Clamp(width * 0.42f, 470f, 640f);
            float x = Mathf.Clamp(width * 0.07f, 34f, 110f);
            float y = Mathf.Clamp(height * 0.14f, 55f, 140f);
            float panelHeight = Mathf.Min(650f, height - y - 65f);

            DrawShadow(new Rect(x + 10f, y + 12f, panelWidth, panelHeight), 14f);
            Fill(new Rect(x, y, panelWidth, panelHeight), new Color(0.045f, 0.065f, 0.077f, 0.9f));
            Fill(new Rect(x, y, 6f, panelHeight), accent);

            DrawTrainMark(new Rect(x + 42f, y + 36f, 92f, 48f), accent);
            DrawText("2026 · OPEN SOURCE DISPATCH", new Rect(x + 148f, y + 38f, panelWidth - 180f, 22f), 14, Muted, TextAnchor.MiddleLeft);
            DrawText("TRAIN TRAIN", new Rect(x + 36f, y + 102f, panelWidth - 70f, 76f), 60, Paper, TextAnchor.MiddleLeft, true);
            DrawText("混乱火车", new Rect(x + 40f, y + 174f, panelWidth - 80f, 52f), 38, accent, TextAnchor.MiddleLeft, true);
            DrawText("在混乱抵达之前，让每一列车找到自己的颜色。", new Rect(x + 42f, y + 232f, panelWidth - 84f, 46f), 17, new Color(0.77f, 0.81f, 0.79f), TextAnchor.MiddleLeft);

            float buttonY = y + 306f;
            if (Button(new Rect(x + 42f, buttonY, panelWidth - 84f, 58f), "开始调度", accent, RailPalette.Ink, mouse, true))
            {
                overlay = Overlay.Modes;
            }

            if (Button(new Rect(x + 42f, buttonY + 72f, panelWidth - 84f, 52f), "选择关卡  ·  " + LevelCatalog.Levels.Count + " 关", new Color(0.15f, 0.2f, 0.21f), Paper, mouse))
            {
                app.ShowLevelSelect();
            }

            float smallGap = 10f;
            float smallWidth = (panelWidth - 84f - smallGap * 2f) / 3f;
            if (Button(new Rect(x + 42f, buttonY + 137f, smallWidth, 46f), "玩法", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                overlay = Overlay.Help;
            }

            if (Button(new Rect(x + 42f + smallWidth + smallGap, buttonY + 137f, smallWidth, 46f), "列车车库", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                overlay = Overlay.Depot;
            }

            if (Button(new Rect(x + 42f + (smallWidth + smallGap) * 2f, buttonY + 137f, smallWidth, 46f), "关于", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                overlay = Overlay.About;
            }

            DrawText(
                "★  " + PlayerProgress.TotalStars() + " / " + (LevelCatalog.Levels.Count * 3) + "   ·   已解锁 " + (PlayerProgress.HighestUnlocked + 1) + " / " + LevelCatalog.Levels.Count,
                new Rect(x + 43f, y + panelHeight - 54f, panelWidth - 86f, 24f),
                14,
                Muted,
                TextAnchor.MiddleLeft);
        }

        private void DrawInfoOverlay(float width, float height, Vector2 mouse)
        {
            Fill(new Rect(0f, 0f, width, height), new Color(0.02f, 0.03f, 0.04f, 0.72f));
            float w = Mathf.Min(820f, width - 56f);
            float h = Mathf.Min(650f, height - 80f);
            Rect panel = new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);
            DrawShadow(panel, 18f);
            Fill(panel, new Color(0.055f, 0.075f, 0.087f, 0.985f));
            Fill(new Rect(panel.x, panel.y, panel.width, 5f), overlay == Overlay.Help ? RailPalette.Azure : RailPalette.Amber);

            if (Button(new Rect(panel.x + 28f, panel.y + 24f, 92f, 38f), "返回", new Color(0.12f, 0.16f, 0.17f), Paper, mouse))
            {
                overlay = Overlay.None;
            }

            if (overlay == Overlay.Modes)
            {
                DrawModeSelection(panel, mouse);
            }
            else if (overlay == Overlay.Help)
            {
                DrawText("新手调度员手册", new Rect(panel.x + 42f, panel.y + 76f, panel.width - 84f, 48f), 32, Paper, TextAnchor.MiddleLeft, true);
                DrawText("列车会一直前进。你的工作是改变它们抵达的下一条线路。", new Rect(panel.x + 44f, panel.y + 126f, panel.width - 88f, 30f), 16, Muted, TextAnchor.MiddleLeft);
                DrawHelpStep(panel.x + 44f, panel.y + 184f, panel.width - 88f, "01", "点击列车启停", "开局列车会立即出发；点击车身停车，再点一次恢复行驶。", RailPalette.Azure);
                DrawHelpStep(panel.x + 44f, panel.y + 282f, panel.width - 88f, "02", "点击信号台换轨", "道岔会改变所有经过它的列车路线。发光短线显示当前出口。", RailPalette.Amber);
                DrawHelpStep(panel.x + 44f, panel.y + 380f, panel.width - 88f, "03", "自动上下客", "列车停站后会先卸下同色乘客，再装入站台上数量最多的那一种颜色。", RailPalette.Crimson);
                DrawHelpStep(panel.x + 44f, panel.y + 478f, panel.width - 88f, "04", "错峰通过", "交叉口没有自动信号。把列车停进支道，等待对向车流通过后再发车。", Success);
            }
            else if (overlay == Overlay.About)
            {
                DrawText("Train Train / 混乱火车", new Rect(panel.x + 42f, panel.y + 76f, panel.width - 84f, 42f), 30, Paper, TextAnchor.MiddleLeft, true);
                DrawText("一款关于路线、节奏与错误代价的列车调度游戏。", new Rect(panel.x + 44f, panel.y + 126f, panel.width - 88f, 30f), 16, Muted, TextAnchor.MiddleLeft);
                DrawText("8 张地图  ·  48 个关卡  ·  8 种低多边形区域", new Rect(panel.x + 44f, panel.y + 186f, panel.width - 88f, 30f), 18, RailPalette.Amber, TextAnchor.MiddleLeft, true);
                DrawText("所有场景模型、界面与音效均由项目代码实时生成。没有外部美术素材依赖，适合学习、修改与再发布。", new Rect(panel.x + 44f, panel.y + 232f, panel.width - 88f, 74f), 16, Paper, TextAnchor.UpperLeft, false, true);
                Fill(new Rect(panel.x + 44f, panel.y + 326f, panel.width - 88f, 1f), new Color(1f, 1f, 1f, 0.12f));
                DrawText("开源许可", new Rect(panel.x + 44f, panel.y + 354f, panel.width - 88f, 28f), 17, Muted, TextAnchor.MiddleLeft, true);
                DrawText("MIT License", new Rect(panel.x + 44f, panel.y + 390f, panel.width - 88f, 32f), 23, Paper, TextAnchor.MiddleLeft);
                DrawText("Copyright © 2026 X4CE", new Rect(panel.x + 44f, panel.y + 426f, panel.width - 88f, 30f), 17, RailPalette.Amber, TextAnchor.MiddleLeft);
                DrawText("你可以在保留版权与许可声明的前提下学习、修改、分发和商用本项目。", new Rect(panel.x + 44f, panel.y + 478f, panel.width - 88f, 56f), 15, Muted, TextAnchor.UpperLeft, false, true);
            }
            else if (overlay == Overlay.Depot)
            {
                DrawTrainDepot(panel, mouse);
            }
        }

        private void DrawModeSelection(Rect panel, Vector2 mouse)
        {
            DrawText("选择游戏模式", new Rect(panel.x + 42f, panel.y + 76f, panel.width - 84f, 48f), 32, Paper, TextAnchor.MiddleLeft, true);
            DrawText("从当前进度继续，或者在不断增长的车流中挑战最高分。", new Rect(panel.x + 44f, panel.y + 128f, panel.width - 88f, 28f), 15, Muted, TextAnchor.MiddleLeft);

            float gap = 16f;
            float cardWidth = (panel.width - 88f - gap * 2f) / 3f;
            Rect campaign = new Rect(panel.x + 44f, panel.y + 188f, cardWidth, 330f);
            Rect endless = new Rect(campaign.x + cardWidth + gap, campaign.y, cardWidth, 330f);
            Rect editor = new Rect(endless.x + cardWidth + gap, campaign.y, cardWidth, 330f);
            DrawModeCard(campaign, "关卡模式", "CAMPAIGN", "沿世界地图推进 48 个关卡，获得星级并解锁新车。", new Color(0.1f, 0.15f, 0.16f, 0.98f), RailPalette.Amber, true);
            DrawModeCard(endless, "无尽模式", "ENDLESS", "乘客目标持续增长，时间会随阶段完成而补充。", new Color(0.1f, 0.14f, 0.16f, 0.98f), RailPalette.Azure, false);
            DrawModeCard(editor, "关卡编辑器", "LEVEL EDITOR", "铺轨、放置车站和列车，校验后保存或试玩。", new Color(0.1f, 0.14f, 0.16f, 0.98f), RailPalette.Violet, false);

            if (Click(campaign, mouse))
            {
                overlay = Overlay.None;
                app.StartLevel(PlayerProgress.HighestUnlocked);
            }

            if (Click(endless, mouse))
            {
                overlay = Overlay.None;
                app.StartEndless();
            }

            if (Click(editor, mouse))
            {
                overlay = Overlay.None;
                app.ShowEditor();
            }
        }

        private void DrawModeCard(Rect card, string title, string english, string description, Color fill, Color accent, bool campaign)
        {
            Fill(card, fill);
            Fill(new Rect(card.x, card.y, card.width, 6f), accent);
            float iconY = card.y + 34f;
            for (int i = 0; i < 5; i++)
            {
                Fill(new Rect(card.x + 28f + i * 27f, iconY + (i % 2) * 9f, 18f, 18f), new Color(accent.r, accent.g, accent.b, 0.22f));
            }
            if (!campaign)
            {
                for (int i = 0; i < 5; i++)
                {
                    Fill(new Rect(card.x + 31f + i * 27f, iconY + 21f + (i % 2) * 8f, 12f, 12f), accent);
                }
            }

            DrawText(title, new Rect(card.x + 26f, card.y + 112f, card.width - 52f, 40f), 27, Paper, TextAnchor.MiddleLeft, true);
            DrawText(english, new Rect(card.x + 26f, card.y + 154f, card.width - 52f, 22f), 12, accent, TextAnchor.MiddleLeft);
            DrawText(description, new Rect(card.x + 26f, card.y + 196f, card.width - 52f, 68f), 15, Muted, TextAnchor.UpperLeft, false, true);
            DrawText(campaign ? "从第 " + (PlayerProgress.HighestUnlocked + 1) + " 关继续" : "最高分挑战", new Rect(card.x + 26f, card.y + card.height - 48f, card.width - 52f, 24f), 14, accent, TextAnchor.MiddleLeft, true);
        }

        private void DrawTrainDepot(Rect panel, Vector2 mouse)
        {
            DrawText("列车车库", new Rect(panel.x + 42f, panel.y + 74f, panel.width - 84f, 46f), 31, Paper, TextAnchor.MiddleLeft, true);
            DrawText(
                "累计获得星星解锁不同列车。每列车的容量和速度不同。  ★ " + PlayerProgress.TotalStars(),
                new Rect(panel.x + 44f, panel.y + 122f, panel.width - 88f, 28f),
                15,
                Muted,
                TextAnchor.MiddleLeft);

            float gap = 12f;
            float totalWidth = panel.width - 88f;
            float cardWidth = (totalWidth - gap * 3f) / 4f;
            for (int i = 0; i < TrainStyleCatalog.All.Length; i++)
            {
                TrainStyleInfo info = TrainStyleCatalog.All[i];
                bool unlocked = TrainStyleCatalog.IsUnlocked(info.Style);
                bool selected = PlayerProgress.SelectedTrainStyle == info.Style;
                Rect card = new Rect(panel.x + 44f + i * (cardWidth + gap), panel.y + 178f, cardWidth, 350f);
                Fill(card, unlocked ? new Color(0.075f, 0.105f, 0.115f, 0.98f) : new Color(0.055f, 0.067f, 0.073f, 0.96f));
                Fill(new Rect(card.x, card.y, card.width, 6f), selected ? info.Accent : unlocked ? new Color(info.Accent.r, info.Accent.g, info.Accent.b, 0.45f) : new Color(0.32f, 0.35f, 0.35f));
                Fill(new Rect(card.x + 16f, card.y + 30f, card.width - 32f, 98f), new Color(info.Accent.r, info.Accent.g, info.Accent.b, unlocked ? 0.14f : 0.05f));
                DrawTrainGlyph(new Rect(card.x + 22f, card.y + 48f, card.width - 44f, 60f), info.Accent, unlocked);
                DrawText(info.NameCn, new Rect(card.x + 18f, card.y + 142f, card.width - 36f, 28f), 18, unlocked ? Paper : Muted, TextAnchor.MiddleLeft, true);
                DrawText(info.NameEn, new Rect(card.x + 18f, card.y + 170f, card.width - 36f, 19f), 10, Muted, TextAnchor.MiddleLeft);
                DrawText("容量  " + info.Capacity, new Rect(card.x + 18f, card.y + 210f, card.width - 36f, 22f), 13, Paper, TextAnchor.MiddleLeft);
                DrawText("速度  ×" + info.SpeedMultiplier.ToString("F2"), new Rect(card.x + 18f, card.y + 236f, card.width - 36f, 22f), 13, Paper, TextAnchor.MiddleLeft);
                DrawText(info.Description, new Rect(card.x + 18f, card.y + 268f, card.width - 36f, 50f), 12, Muted, TextAnchor.UpperLeft, false, true);

                if (!unlocked)
                {
                    DrawText("需要 ★ " + info.UnlockStars, new Rect(card.x + 18f, card.y + card.height - 50f, card.width - 36f, 24f), 12, Muted, TextAnchor.MiddleLeft, true);
                }
                else if (selected)
                {
                    DrawText("当前使用", new Rect(card.x + 18f, card.y + card.height - 50f, card.width - 36f, 24f), 12, info.Accent, TextAnchor.MiddleLeft, true);
                }

                if (Click(card, mouse) && !selected)
                {
                    if (unlocked)
                    {
                        app.SelectTrainStyle(info.Style);
                    }
                    else
                    {
                        app.ShowToast("需要累计 ★ " + info.UnlockStars + " 才能解锁");
                    }
                }
            }
        }

        private void DrawTrainGlyph(Rect rect, Color color, bool unlocked)
        {
            Color body = unlocked ? color : new Color(0.34f, 0.37f, 0.37f);
            Fill(new Rect(rect.x + 16f, rect.y + 18f, rect.width - 70f, 24f), body);
            Fill(new Rect(rect.x + 29f, rect.y + 5f, rect.width - 95f, 18f), LowPolyAssets.Lighten(body, 0.18f));
            Fill(new Rect(rect.x + 10f, rect.y + 41f, RectMin(rect.width - 38f, 36f), 6f), body);
            Fill(new Rect(rect.x + rect.width - 53f, rect.y + 26f, 18f, 10f), body);
        }

        private float RectMin(float a, float b)
        {
            return a < b ? a : b;
        }

        private void DrawHelpStep(float x, float y, float width, string number, string title, string body, Color color)
        {
            Fill(new Rect(x, y, 58f, 58f), new Color(color.r, color.g, color.b, 0.17f));
            DrawText(number, new Rect(x, y, 58f, 58f), 18, color, TextAnchor.MiddleCenter, true);
            DrawText(title, new Rect(x + 76f, y - 2f, width - 76f, 29f), 20, Paper, TextAnchor.MiddleLeft, true);
            DrawText(body, new Rect(x + 76f, y + 27f, width - 76f, 34f), 14, Muted, TextAnchor.MiddleLeft);
        }

        private void DrawLevelSelect(float width, float height, Vector2 mouse)
        {
            Fill(new Rect(0f, 0f, width, height), new Color(0.02f, 0.03f, 0.04f, 0.82f));
            if (Button(new Rect(34f, 28f, 108f, 44f), "返回", new Color(0.1f, 0.14f, 0.15f), Paper, mouse))
            {
                app.ShowTitle();
            }

            DrawText("世界调度图", new Rect(166f, 26f, width - 380f, 48f), 31, Paper, TextAnchor.MiddleLeft, true);
            DrawText(
                "★ " + PlayerProgress.TotalStars() + "   ·   已解锁 " + (PlayerProgress.HighestUnlocked + 1) + " / " + LevelCatalog.Levels.Count,
                new Rect(width - 360f, 34f, 320f, 28f),
                15,
                Muted,
                TextAnchor.MiddleRight);

            float mapWidth = Mathf.Max(650f, width * 0.64f);
            float mapLeft = 34f;
            float mapTop = 100f;
            float mapHeight = height - mapTop - 64f;
            Vector2[] islandPositions =
            {
                new Vector2(0.08f, 0.70f),
                new Vector2(0.20f, 0.32f),
                new Vector2(0.34f, 0.63f),
                new Vector2(0.47f, 0.27f),
                new Vector2(0.59f, 0.62f),
                new Vector2(0.71f, 0.30f),
                new Vector2(0.82f, 0.58f),
                new Vector2(0.91f, 0.26f)
            };
            Vector2[] centers = new Vector2[islandPositions.Length];
            for (int i = 0; i < centers.Length; i++)
            {
                centers[i] = new Vector2(
                    mapLeft + islandPositions[i].x * mapWidth,
                    mapTop + islandPositions[i].y * mapHeight);
            }

            for (int i = 0; i < centers.Length - 1; i++)
            {
                Vector2 from = centers[i];
                Vector2 to = centers[i + 1];
                for (int dot = 0; dot <= 28; dot++)
                {
                    float t = dot / 28f;
                    Vector2 p = Vector2.Lerp(from, to, t);
                    Fill(new Rect(p.x - 2f, p.y - 2f, 4f, 4f), new Color(0.78f, 0.72f, 0.56f, 0.35f));
                }
            }

            for (int i = 0; i < centers.Length; i++)
            {
                WorldDefinition world = LevelCatalog.Worlds[i];
                RegionDefinition region = world.Region;
                bool selected = i == app.SelectedRegionIndex;
                bool unlocked = PlayerProgress.HighestUnlocked >= LevelCatalog.FirstLevelIndex(i);
                Rect island = new Rect(centers[i].x - 58f, centers[i].y - 34f, 116f, 68f);
                Fill(island, selected
                    ? new Color(region.Accent.r, region.Accent.g, region.Accent.b, 0.92f)
                    : new Color(0.055f, 0.08f, 0.085f, 0.94f));
                Fill(new Rect(island.x + 10f, island.y + island.height - 8f, island.width - 20f, 4f), unlocked ? region.Accent : Muted);
                DrawText("0" + (i + 1), new Rect(island.x + 10f, island.y + 8f, 30f, 17f), 10, selected ? RailPalette.Ink : region.Accent, TextAnchor.MiddleLeft, true);
                DrawText(region.NameCn, new Rect(island.x + 10f, island.y + 25f, island.width - 20f, 28f), 18, selected ? RailPalette.Ink : Paper, TextAnchor.MiddleLeft, true);
                DrawText(world.Levels.Count + " 关", new Rect(island.x + 10f, island.y + 49f, island.width - 20f, 12f), 9, selected ? new Color(0.12f, 0.15f, 0.14f) : Muted, TextAnchor.MiddleLeft);
                if (!unlocked)
                {
                    Fill(island, new Color(0.02f, 0.03f, 0.04f, 0.58f));
                    DrawText("LOCKED", new Rect(island.x, island.y + 26f, island.width, 18f), 9, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, true);
                }
                if (Click(island, mouse))
                {
                    app.PreviewRegion(i);
                    app.Audio.Play(AudioCue.Button);
                }
            }

            WorldDefinition selectedWorld = LevelCatalog.Worlds[app.SelectedRegionIndex];
            RegionDefinition selectedRegion = selectedWorld.Region;
            float panelX = mapLeft + mapWidth + 18f;
            float panelWidth = width - panelX - 34f;
            Rect panel = new Rect(panelX, 98f, panelWidth, height - 160f);
            DrawShadow(panel, 10f);
            Fill(panel, new Color(0.045f, 0.064f, 0.072f, 0.96f));
            Fill(new Rect(panel.x, panel.y, panel.width, 5f), selectedRegion.Accent);
            DrawText(selectedRegion.NameCn, new Rect(panel.x + 24f, panel.y + 18f, panel.width - 48f, 34f), 24, Paper, TextAnchor.MiddleLeft, true);
            DrawText(selectedRegion.TaglineCn, new Rect(panel.x + 24f, panel.y + 53f, panel.width - 48f, 38f), 13, Muted, TextAnchor.UpperLeft, true);

            int count = selectedWorld.Levels.Count;
            float lineX = panel.x + 46f;
            float firstY = panel.y + 112f;
            float spacing = Mathf.Min(66f, (panel.height - 138f) / Mathf.Max(1, count));
            Fill(new Rect(lineX - 1f, firstY, 2f, spacing * (count - 1)), new Color(1f, 1f, 1f, 0.15f));
            for (int i = 0; i < count; i++)
            {
                LevelDefinition level = selectedWorld.Levels[i];
                int levelIndex = level.Index;
                bool unlocked = PlayerProgress.IsUnlocked(levelIndex);
                int stars = PlayerProgress.GetStars(levelIndex);
                float y = firstY + i * spacing;
                Rect node = new Rect(lineX - 22f, y - 22f, 44f, 44f);
                Fill(node, unlocked ? selectedRegion.Accent : new Color(0.19f, 0.22f, 0.22f));
                DrawText((i + 1).ToString("00"), node, 12, unlocked ? RailPalette.Ink : Muted, TextAnchor.MiddleCenter, true);
                Rect row = new Rect(lineX + 34f, y - 26f, panel.width - 84f, 52f);
                Fill(row, unlocked ? new Color(1f, 1f, 1f, 0.04f) : new Color(0f, 0f, 0f, 0.12f));
                DrawText(unlocked ? level.NameCn : "尚未解锁", new Rect(row.x + 12f, row.y + 4f, row.width - 80f, 23f), 15, unlocked ? Paper : Muted, TextAnchor.MiddleLeft, true);
                string starText = (stars > 0 ? "★" : "☆") + (stars > 1 ? "★" : "☆") + (stars > 2 ? "★" : "☆");
                DrawText(starText, new Rect(row.x + row.width - 72f, row.y + 3f, 62f, 22f), 15, stars > 0 ? RailPalette.Amber : Muted, TextAnchor.MiddleRight);
                DrawText(
                    unlocked ? "乘客 " + level.TotalPassengers + "  ·  速度 " + DifficultyBars(level) : "完成前一关后开放",
                    new Rect(row.x + 12f, row.y + 27f, row.width - 24f, 18f),
                    10,
                    Muted,
                    TextAnchor.MiddleLeft);
                if (Click(row, mouse))
                {
                    if (unlocked)
                    {
                        app.StartLevel(levelIndex);
                    }
                    else
                    {
                        app.ShowToast("先完成前一关，才能进入这里。");
                    }
                }
            }
        }

        private void DrawEditor(float width, float height, Vector2 mouse)
        {
            LevelEditorSession editor = app.Editor;
            CustomLevelDocument document = editor.Document;
            Color accent = RailPalette.Violet;

            Rect top = new Rect(18f, 16f, width - 36f, 58f);
            Fill(top, new Color(0.055f, 0.075f, 0.086f, 0.96f));
            Fill(new Rect(top.x, top.y, 5f, top.height), accent);
            DrawText("关卡编辑器", new Rect(top.x + 22f, top.y + 6f, 210f, 26f), 21, Paper, TextAnchor.MiddleLeft, true);
            DrawText("MODEL YARD", new Rect(top.x + 24f, top.y + 32f, 180f, 16f), 10, accent, TextAnchor.MiddleLeft);

            if (Button(new Rect(top.x + top.width - 360f, top.y + 10f, 76f, 36f), "撤销", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                editor.Undo();
                app.RefreshEditorWorld();
            }

            if (Button(new Rect(top.x + top.width - 278f, top.y + 10f, 76f, 36f), "重做", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                editor.Redo();
                app.RefreshEditorWorld();
            }

            if (Button(new Rect(top.x + top.width - 196f, top.y + 10f, 76f, 36f), "保存", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                app.SaveEditorLevel();
            }

            if (Button(new Rect(top.x + top.width - 114f, top.y + 10f, 92f, 36f), "返回", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                app.ShowTitle();
            }

            Rect palette = new Rect(18f, 90f, 214f, height - 202f);
            Fill(palette, new Color(0.045f, 0.064f, 0.073f, 0.96f));
            Fill(new Rect(palette.x, palette.y, 4f, palette.height), accent);
            DrawText("编辑工具", new Rect(palette.x + 18f, palette.y + 12f, 180f, 22f), 14, Muted, TextAnchor.MiddleLeft, true);
            string[] toolLabels = { "地面轨", "高架轨", "隧道", "侧线", "车站", "列车", "删除" };
            toolLabels[5] = "列车 · " + Mathf.Min(4, document.trains.Count) + "/4";
            EditorTool[] tools =
            {
                EditorTool.Track,
                EditorTool.Bridge,
                EditorTool.Tunnel,
                EditorTool.SideTrack,
                EditorTool.Station,
                EditorTool.Train,
                EditorTool.Erase
            };
            for (int i = 0; i < tools.Length; i++)
            {
                bool selected = editor.Tool == tools[i];
                Color fill = selected ? accent : new Color(0.095f, 0.13f, 0.14f);
                if (Button(new Rect(palette.x + 14f, palette.y + 44f + i * 40f, palette.width - 28f, 33f), toolLabels[i], fill, selected ? RailPalette.Ink : Paper, mouse))
                {
                    editor.SetTool(tools[i]);
                }
            }

            if (Button(new Rect(palette.x + 14f, palette.y + 338f, palette.width - 28f, 32f), "车站颜色 · " + RailPalette.NameCn(editor.StationColor), new Color(0.1f, 0.14f, 0.15f), Paper, mouse))
            {
                editor.CycleStationColor();
            }

            if (Button(new Rect(palette.x + 14f, palette.y + 378f, palette.width - 28f, 32f), "列车车型 · " + TrainStyleCatalog.Get(editor.TrainStyle).NameCn, new Color(0.1f, 0.14f, 0.15f), Paper, mouse))
            {
                editor.CycleTrainStyle();
            }

            if (Button(new Rect(palette.x + 14f, palette.y + palette.height - 42f, palette.width - 28f, 30f), "重置为空线", new Color(0.1f, 0.12f, 0.13f), Muted, mouse))
            {
                editor.Reset();
                app.RefreshEditorWorld();
            }

            float inspectorX = width - 222f;
            Rect inspector = new Rect(inspectorX, 90f, 202f, 468f);
            Fill(inspector, new Color(0.045f, 0.064f, 0.073f, 0.96f));
            DrawText("关卡参数", new Rect(inspector.x + 16f, inspector.y + 12f, 170f, 22f), 14, Muted, TextAnchor.MiddleLeft, true);
            Rect nameRect = new Rect(inspector.x + 16f, inspector.y + 45f, inspector.width - 32f, 30f);
            Fill(nameRect, new Color(0.11f, 0.14f, 0.15f));
            GUIStyle nameStyle = Style(13, TextAnchor.MiddleLeft, Paper);
            document.name = GUI.TextField(nameRect, document.name, 24, nameStyle);

            DrawText("主题 · " + LevelCatalog.Regions[document.themeIndex].NameCn, new Rect(inspector.x + 16f, inspector.y + 88f, inspector.width - 32f, 20f), 12, Paper, TextAnchor.MiddleLeft);
            if (Button(new Rect(inspector.x + 16f, inspector.y + 112f, 80f, 28f), "上一图", new Color(0.1f, 0.14f, 0.15f), Paper, mouse))
            {
                document.themeIndex = (document.themeIndex + LevelCatalog.Regions.Count - 1) % LevelCatalog.Regions.Count;
                app.RefreshEditorWorld();
            }
            if (Button(new Rect(inspector.x + 103f, inspector.y + 112f, 82f, 28f), "下一图", new Color(0.1f, 0.14f, 0.15f), Paper, mouse))
            {
                document.themeIndex = (document.themeIndex + 1) % LevelCatalog.Regions.Count;
                app.RefreshEditorWorld();
            }

            DrawParameterStepper(inspector.x + 16f, inspector.y + 154f, inspector.width - 32f, "时间", document.duration.ToString("F0") + " 秒", mouse, delegate { document.duration = Mathf.Clamp(document.duration - 10f, 15f, 180f); }, delegate { document.duration = Mathf.Clamp(document.duration + 10f, 15f, 180f); });
            DrawParameterStepper(inspector.x + 16f, inspector.y + 194f, inspector.width - 32f, "乘客目标", document.passengerGoal.ToString(), mouse, delegate { document.passengerGoal = Mathf.Max(3, document.passengerGoal - 3); }, delegate { document.passengerGoal = Mathf.Min(180, document.passengerGoal + 3); });
            DrawParameterStepper(inspector.x + 16f, inspector.y + 234f, inspector.width - 32f, "站内容量", document.stationCapacity.ToString(), mouse, delegate { document.stationCapacity = Mathf.Max(4, document.stationCapacity - 2); }, delegate { document.stationCapacity = Mathf.Min(40, document.stationCapacity + 2); });
            DrawParameterStepper(inspector.x + 16f, inspector.y + 274f, inspector.width - 32f, "生成间隔", document.stationSpawnInterval.ToString("F1") + " 秒", mouse, delegate { document.stationSpawnInterval = Mathf.Clamp(document.stationSpawnInterval - 0.2f, 0.4f, 6f); }, delegate { document.stationSpawnInterval = Mathf.Clamp(document.stationSpawnInterval + 0.2f, 0.4f, 6f); });

            if (Button(new Rect(inspector.x + 16f, inspector.y + 314f, inspector.width - 32f, 30f), document.syncAllSwitches ? "同步道岔：开启" : "同步道岔：关闭", document.syncAllSwitches ? accent : new Color(0.11f, 0.15f, 0.16f), document.syncAllSwitches ? RailPalette.Ink : Paper, mouse))
            {
                document.syncAllSwitches = !document.syncAllSwitches;
            }

            if (Button(new Rect(inspector.x + 16f, inspector.y + 350f, inspector.width - 32f, 30f), "导出 JSON", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                app.ExportEditorJson();
            }
            if (Button(new Rect(inspector.x + 16f, inspector.y + 386f, inspector.width - 32f, 30f), "导入 JSON", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                app.ImportEditorJson();
            }
            if (Button(new Rect(inspector.x + 16f, inspector.y + 422f, inspector.width - 32f, 28f), "载入上次存档", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                app.LoadLastSavedEditorLevel();
            }
            if (Button(new Rect(inspector.x + 16f, inspector.y + 454f, inspector.width - 32f, 34f), "测试关卡", accent, RailPalette.Ink, mouse, true))
            {
                app.TestEditorLevel();
            }

            Rect status = new Rect(18f, height - 66f, width - 36f, 46f);
            Fill(status, new Color(0.035f, 0.05f, 0.06f, 0.94f));
            DrawText(editor.Status, new Rect(status.x + 16f, status.y, status.width - 32f, status.height), 12, Paper, TextAnchor.MiddleLeft);
            DrawText("铺轨：从起点拖到终点，或点起点后移动并再次点击终点；右键取消", new Rect(status.x + 16f, status.y + 18f, status.width - 32f, 20f), 10, Muted, TextAnchor.MiddleLeft);
        }

        private void DrawParameterStepper(float x, float y, float width, string label, string value, Vector2 mouse, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
        {
            DrawText(label, new Rect(x, y, width - 126f, 24f), 12, Muted, TextAnchor.MiddleLeft);
            if (Button(new Rect(x + width - 116f, y, 30f, 28f), "-", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                minus();
            }
            DrawText(value, new Rect(x + width - 80f, y, 54f, 28f), 12, Paper, TextAnchor.MiddleCenter);
            if (Button(new Rect(x + width - 26f, y, 26f, 28f), "+", new Color(0.11f, 0.15f, 0.16f), Paper, mouse))
            {
                plus();
            }
        }

        private void DrawGameHud(float width, float height, Vector2 mouse)
        {
            LevelDefinition level = app.CurrentLevel;
            if (level == null)
            {
                return;
            }

            Color accent = level.Region.Accent;
            float topX = 18f;
            float topY = 16f;
            float topW = width - 36f;
            DrawShadow(new Rect(topX + 5f, topY + 6f, topW, 72f), 10f);
            Fill(new Rect(topX, topY, topW, 72f), Panel);
            Fill(new Rect(topX, topY, 5f, 72f), accent);

            DrawText(
                "0" + (app.SelectedLevelIndex + 1) + "  ·  " + level.Region.NameCn + (app.CurrentMode == GameMode.Endless ? "  ·  无尽" : string.Empty),
                new Rect(topX + 24f, topY + 10f, 280f, 23f),
                13,
                accent,
                TextAnchor.MiddleLeft,
                true);
            DrawText(level.NameCn, new Rect(topX + 24f, topY + 33f, 330f, 30f), 22, Paper, TextAnchor.MiddleLeft, true);

            string time = Mathf.CeilToInt(app.TimeRemaining).ToString("00");
            Color timeColor = app.TimeRemaining <= 15f ? Danger : Paper;
            DrawText(time, new Rect(width * 0.5f - 40f, topY + 7f, 80f, 44f), 32, timeColor, TextAnchor.MiddleCenter, true);
            DrawText("SEC", new Rect(width * 0.5f - 26f, topY + 48f, 52f, 16f), 10, Muted, TextAnchor.MiddleCenter);

            DrawText("列车 " + app.MovingTrains + " / " + app.Trains.Count, new Rect(width - 365f, topY + 8f, 160f, 30f), 16, accent, TextAnchor.MiddleRight, true);
            DrawText("SCORE  " + app.Score.ToString("N0"), new Rect(width - 365f, topY + 38f, 160f, 22f), 13, Muted, TextAnchor.MiddleRight);

            if (Button(new Rect(width - 178f, topY + 17f, 70f, 38f), "暂停", new Color(0.12f, 0.16f, 0.17f), Paper, mouse))
            {
                app.TogglePause();
            }

            if (Button(new Rect(width - 98f, topY + 17f, 72f, 38f), PlayerProgress.Muted ? "静音" : "声音", new Color(0.12f, 0.16f, 0.17f), PlayerProgress.Muted ? Muted : Paper, mouse))
            {
                app.ToggleMute();
            }

            if (app.Combo > 1)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.04f;
                Rect combo = new Rect(22f, 105f, 150f, 48f);
                Fill(combo, new Color(accent.r, accent.g, accent.b, 0.16f));
                Fill(new Rect(combo.x, combo.y, 4f, combo.height), accent);
                DrawText("连击 ×" + app.Combo, new Rect(combo.x + 14f, combo.y, combo.width - 18f, combo.height), Mathf.RoundToInt(18f * pulse), accent, TextAnchor.MiddleLeft, true);
            }

            float objectiveWidth = Mathf.Min(820f, width - 34f);
            float objectiveX = (width - objectiveWidth) * 0.5f;
            float objectiveY = height - 122f;
            DrawShadow(new Rect(objectiveX + 5f, objectiveY + 6f, objectiveWidth, 90f), 9f);
            Fill(new Rect(objectiveX, objectiveY, objectiveWidth, 90f), Panel);
            DrawText("乘客运输", new Rect(objectiveX + 16f, objectiveY + 7f, 100f, 21f), 13, Muted, TextAnchor.MiddleLeft, true);
            DrawText("行驶 " + app.MovingTrains + "  ·  " + app.Trains.Count + " 列车", new Rect(objectiveX + objectiveWidth - 210f, objectiveY + 7f, 194f, 21f), 13, RailPalette.Amber, TextAnchor.MiddleRight, true);

            Rect progress = new Rect(objectiveX + 16f, objectiveY + 32f, objectiveWidth - 32f, 18f);
            Fill(progress, new Color(1f, 1f, 1f, 0.08f));
            float ratio = Mathf.Clamp01(app.DeliveredTotal / (float)Mathf.Max(1, app.TotalPassengers));
            Fill(new Rect(progress.x, progress.y, progress.width * ratio, progress.height), new Color(accent.r, accent.g, accent.b, 0.88f));
            DrawText(
                app.DeliveredTotal + " / " + app.TotalPassengers,
                progress,
                13,
                Paper,
                TextAnchor.MiddleCenter,
                true);

            float statusGap = 8f;
            float statusWidth = (objectiveWidth - 32f - statusGap * 3f) / 4f;
            for (int colorIndex = 0; colorIndex < 4; colorIndex++)
            {
                RailColor color = (RailColor)colorIndex;
                Color railColor = RailPalette.ForColor(color);
                TrainActor colorTrain = null;
                for (int trainIndex = 0; trainIndex < app.Trains.Count; trainIndex++)
                {
                    if (app.Trains[trainIndex] != null && app.Trains[trainIndex].Color == color)
                    {
                        colorTrain = app.Trains[trainIndex];
                        break;
                    }
                }

                Rect status = new Rect(objectiveX + 16f + colorIndex * (statusWidth + statusGap), objectiveY + 59f, statusWidth, 21f);
                Fill(new Rect(status.x, status.y + 5f, 8f, 8f), railColor);
                string state = colorIndex >= app.FleetSize ? "未上场" : colorTrain == null ? "待编组" : colorTrain.IsMoving ? "行驶" : "待发";
                DrawText(RailPalette.NameCn(color) + "  " + state, new Rect(status.x + 15f, status.y, status.width - 15f, status.height), 11, colorTrain == null ? Muted : Paper, TextAnchor.MiddleLeft);
            }

            DrawText(
                "点击列车启停  ·  点击道岔切轨  ·  数字键 1-4 控制颜色  ·  R 重开",
                new Rect(0f, height - 28f, Mathf.Max(320f, width - 310f), 18f),
                11,
                new Color(0.85f, 0.87f, 0.84f, 0.55f),
                TextAnchor.MiddleCenter);

            if (app.ToastAlpha > 0f)
            {
                float toastWidth = Mathf.Min(660f, width - 70f);
                Rect toast = new Rect((width - toastWidth) * 0.5f, 106f, toastWidth, 43f);
                Fill(toast, new Color(0.035f, 0.05f, 0.06f, 0.9f * app.ToastAlpha));
                Fill(new Rect(toast.x, toast.y, 4f, toast.height), new Color(accent.r, accent.g, accent.b, app.ToastAlpha));
                Color toastText = new Color(Paper.r, Paper.g, Paper.b, app.ToastAlpha);
                DrawText(app.ToastMessage, new Rect(toast.x + 16f, toast.y, toast.width - 32f, toast.height), 15, toastText, TextAnchor.MiddleCenter);
            }

            DrawHoverTooltip(width, height, mouse);
        }

        private void DrawHoverTooltip(float width, float height, Vector2 mouse)
        {
            string text = null;
            if (app.HoveredTrain != null)
            {
                TrainActor train = app.HoveredTrain;
                string cargo = train.CargoColor.HasValue
                    ? RailPalette.NameCn(train.CargoColor.Value) + "乘客 ×" + train.Passengers
                    : "空车";
                text = (train.IsMoving ? "点击停车" : "点击发车") + "  ·  " + cargo;
            }
            else if (app.HoveredSwitch != null)
            {
                text = "切换道岔  ·  当前线路高亮";
            }

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Rect tooltip = new Rect(
                Mathf.Clamp(mouse.x + 18f, 20f, width - 240f),
                Mathf.Clamp(mouse.y - 42f, 96f, height - 155f),
                216f,
                32f);
            Fill(tooltip, new Color(0.035f, 0.05f, 0.06f, 0.94f));
            Fill(new Rect(tooltip.x, tooltip.y, 3f, tooltip.height), RailPalette.Amber);
            DrawText(text, new Rect(tooltip.x + 12f, tooltip.y, tooltip.width - 18f, tooltip.height), 12, Paper, TextAnchor.MiddleLeft);
        }

        private void DrawWorldLabels(float width, float height)
        {
            if (app.World == null || app.World.Camera == null || app.World.Stations == null)
            {
                return;
            }

            Camera camera = app.World.Camera;
            for (int i = 0; i < app.World.Stations.Count; i++)
            {
                StationView station = app.World.Stations[i];
                Vector3 screen = camera.WorldToScreenPoint(station.WorldPosition + Vector3.up * 1.55f);
                if (screen.z < 0f)
                {
                    continue;
                }

                float scale = ScaleForScreen();
                float x = screen.x / scale;
                float y = (Screen.height - screen.y) / scale;
                if (x < -80f || x > width + 80f || y < -30f || y > height + 30f)
                {
                    continue;
                }

                Color color = RailPalette.ForColor(station.Color);
                string label = RailPalette.NameCn(station.Color) + "站 · " + app.WaitingPassengers(station.Position) + "人";
                Rect badge = new Rect(x - 48f, y - 12f, 96f, 24f);
                Fill(badge, new Color(0.035f, 0.05f, 0.058f, 0.88f));
                Fill(new Rect(badge.x, badge.y, 3f, badge.height), color);
                DrawText(label, new Rect(badge.x + 8f, badge.y, badge.width - 10f, badge.height), 11, Paper, TextAnchor.MiddleCenter, true);
            }
        }

        private void DrawPause(float width, float height, Vector2 mouse)
        {
            Fill(new Rect(0f, 0f, width, height), new Color(0.015f, 0.025f, 0.03f, 0.72f));
            Rect panel = new Rect((width - 430f) * 0.5f, (height - 380f) * 0.5f, 430f, 380f);
            DrawShadow(panel, 18f);
            Fill(panel, new Color(0.05f, 0.07f, 0.08f, 0.99f));
            Fill(new Rect(panel.x, panel.y, panel.width, 5f), app.CurrentLevel.Region.Accent);
            DrawText("调度暂停", new Rect(panel.x + 38f, panel.y + 38f, panel.width - 76f, 54f), 34, Paper, TextAnchor.MiddleCenter, true);
            DrawText(app.CurrentLevel.NameCn, new Rect(panel.x + 40f, panel.y + 92f, panel.width - 80f, 28f), 16, Muted, TextAnchor.MiddleCenter);
            if (Button(new Rect(panel.x + 52f, panel.y + 146f, panel.width - 104f, 50f), "继续调度", app.CurrentLevel.Region.Accent, RailPalette.Ink, mouse, true))
            {
                app.TogglePause();
            }

            if (Button(new Rect(panel.x + 52f, panel.y + 210f, panel.width - 104f, 44f), "重新开始", new Color(0.12f, 0.16f, 0.17f), Paper, mouse))
            {
                app.RetryLevel();
            }

            if (Button(new Rect(panel.x + 52f, panel.y + 268f, panel.width - 104f, 44f), "返回关卡选择", new Color(0.09f, 0.12f, 0.13f), Muted, mouse))
            {
                app.ShowLevelSelect();
            }
        }

        private void DrawResult(float width, float height, Vector2 mouse)
        {
            Fill(new Rect(0f, 0f, width, height), new Color(0.015f, 0.025f, 0.03f, 0.74f));
            Rect panel = new Rect((width - 560f) * 0.5f, (height - 510f) * 0.5f, 560f, 510f);
            DrawShadow(panel, 20f);
            Fill(panel, new Color(0.05f, 0.07f, 0.082f, 0.99f));
            Color accent = app.ResultWon ? app.CurrentLevel.Region.Accent : Danger;
            Fill(new Rect(panel.x, panel.y, panel.width, 6f), accent);

            string resultTitle = app.CurrentMode == GameMode.Endless
                ? (app.ResultWon ? "无尽挑战结束" : "挑战中断")
                : (app.ResultWon ? "调度完成" : "线路中断");
            DrawText(resultTitle, new Rect(panel.x + 34f, panel.y + 30f, panel.width - 68f, 58f), 37, Paper, TextAnchor.MiddleCenter, true);
            string resultSubtitle = app.CurrentMode == GameMode.Endless
                ? "无尽模式 · 送达 " + app.DeliveredTotal + " 名乘客"
                : app.CurrentLevel.Region.NameCn + "  /  " + app.CurrentLevel.NameCn;
            DrawText(resultSubtitle, new Rect(panel.x + 34f, panel.y + 88f, panel.width - 68f, 25f), 15, Muted, TextAnchor.MiddleCenter);

            string stars = string.Empty;
            for (int i = 0; i < 3; i++)
            {
                stars += i < app.ResultStars ? "★" : "☆";
            }

            DrawText(stars, new Rect(panel.x + 40f, panel.y + 130f, panel.width - 80f, 66f), 43, app.ResultWon ? RailPalette.Amber : new Color(0.34f, 0.38f, 0.38f), TextAnchor.MiddleCenter);
            DrawText(app.ResultReason, new Rect(panel.x + 44f, panel.y + 203f, panel.width - 88f, 48f), 15, Muted, TextAnchor.MiddleCenter, false, true);

            Rect scoreBox = new Rect(panel.x + 48f, panel.y + 260f, panel.width - 96f, 72f);
            Fill(scoreBox, new Color(1f, 1f, 1f, 0.045f));
            DrawText("SCORE", new Rect(scoreBox.x + 20f, scoreBox.y + 10f, 120f, 20f), 11, Muted, TextAnchor.MiddleLeft, true);
            DrawText(app.Score.ToString("N0"), new Rect(scoreBox.x + 20f, scoreBox.y + 29f, 210f, 34f), 26, Paper, TextAnchor.MiddleLeft, true);
            DrawText(app.ResultNewBest ? "NEW BEST" : "BEST  " + app.ResultBestScore.ToString("N0"), new Rect(scoreBox.x + 260f, scoreBox.y, scoreBox.width - 280f, scoreBox.height), 14, app.ResultNewBest ? RailPalette.Amber : Muted, TextAnchor.MiddleRight, true);

            if (app.CurrentMode == GameMode.Endless)
            {
                if (Button(new Rect(panel.x + 52f, panel.y + 358f, panel.width - 104f, 52f), "再跑一趟", accent, RailPalette.Ink, mouse, true))
                {
                    app.RetryLevel();
                }
            }
            else if (app.ResultWon)
            {
                string primaryLabel = app.IsCustomSession
                    ? "返回编辑器"
                    : app.SelectedLevelIndex + 1 < LevelCatalog.Levels.Count
                        ? "进入下一关"
                        : "全部完成 · 返回选关";
                if (Button(new Rect(panel.x + 52f, panel.y + 358f, panel.width - 104f, 52f), primaryLabel, accent, RailPalette.Ink, mouse, true))
                {
                    app.NextLevel();
                }
            }
            else if (Button(new Rect(panel.x + 52f, panel.y + 358f, panel.width - 104f, 52f), "再试一次", accent, Paper, mouse, true))
            {
                app.RetryLevel();
            }

            if (Button(new Rect(panel.x + 52f, panel.y + 423f, (panel.width - 116f) * 0.5f, 42f), "重新开始", new Color(0.11f, 0.14f, 0.15f), Paper, mouse))
            {
                app.RetryLevel();
            }

            if (Button(new Rect(panel.x + 52f + (panel.width - 116f) * 0.5f + 12f, panel.y + 423f, (panel.width - 116f) * 0.5f, 42f), app.IsCustomSession ? "返回编辑器" : "选择关卡", new Color(0.11f, 0.14f, 0.15f), Muted, mouse))
            {
                if (app.IsCustomSession)
                {
                    app.ShowEditor();
                }
                else
                {
                    app.ShowLevelSelect();
                }
            }
        }

        private bool Button(Rect rect, string label, Color fill, Color textColor, Vector2 mouse, bool large = false)
        {
            bool hover = rect.Contains(mouse);
            Color drawColor = hover ? LowPolyAssets.Lighten(fill, 0.12f) : fill;
            Fill(rect, drawColor);
            if (hover)
            {
                Fill(new Rect(rect.x, rect.y + rect.height - 2f, rect.width, 2f), LowPolyAssets.Lighten(fill, 0.45f));
            }

            DrawText(label, rect, large ? 19 : 15, textColor, TextAnchor.MiddleCenter, true);
            if (Click(rect, mouse))
            {
                app.Audio.Play(AudioCue.Button);
                return true;
            }

            return false;
        }

        private bool Click(Rect rect, Vector2 mouse)
        {
            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rect.Contains(mouse))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        private void DrawTrainMark(Rect rect, Color color)
        {
            Fill(new Rect(rect.x, rect.y + 8f, rect.width, 32f), new Color(color.r, color.g, color.b, 0.18f));
            Fill(new Rect(rect.x + 7f, rect.y + 14f, 50f, 20f), color);
            Fill(new Rect(rect.x + 14f, rect.y + 6f, 30f, 12f), LowPolyAssets.Lighten(color, 0.25f));
            Fill(new Rect(rect.x + 61f, rect.y + 18f, 20f, 13f), new Color(1f, 1f, 1f, 0.85f));
            Fill(new Rect(rect.x + 12f, rect.y + 36f, 8f, 7f), color);
            Fill(new Rect(rect.x + 29f, rect.y + 36f, 8f, 7f), color);
            Fill(new Rect(rect.x + 46f, rect.y + 36f, 8f, 7f), color);
        }

        private void DrawText(
            string text,
            Rect rect,
            int size,
            Color color,
            TextAnchor alignment,
            bool bold = false,
            bool wrap = false)
        {
            GUIStyle style = Style(size, alignment, color, bold, wrap);
            GUI.Label(rect, text, style);
        }

        private GUIStyle Style(int size, TextAnchor alignment, Color color, bool bold = false, bool wrap = false)
        {
            string key = size + "_" + (int)alignment + "_" + ColorUtility.ToHtmlStringRGBA(color) + "_" + bold + "_" + wrap;
            if (styles.TryGetValue(key, out GUIStyle existing))
            {
                return existing;
            }

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = size,
                alignment = alignment,
                wordWrap = wrap,
                clipping = wrap ? TextClipping.Clip : TextClipping.Overflow,
                richText = false,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal
            };
            style.normal.textColor = color;
            styles[key] = style;
            return style;
        }

        private void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = old;
        }

        private void DrawShadow(Rect rect, float spread)
        {
            Fill(new Rect(rect.x - spread * 0.25f, rect.y + spread * 0.45f, rect.width + spread * 0.5f, rect.height + spread * 0.2f), new Color(0f, 0f, 0f, 0.22f));
        }

        private static string DifficultyBars(LevelDefinition level)
        {
            int difficulty = Mathf.Clamp(1 + level.RegionIndex / 2 + level.LocalIndex / 2, 1, 5);
            string result = string.Empty;
            for (int i = 0; i < 5; i++)
            {
                result += i < difficulty ? "■" : "□";
            }

            return result;
        }
    }
}
