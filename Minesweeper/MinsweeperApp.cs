using System;
using Framework.Engine;

namespace Framework.Minesweeper
{
    public class MinesweeperApp : GameApp
    {
        // 화면: 80x25
        public const int ScreenWidth = 100;
        public const int ScreenHeight = 30;

        private readonly SceneManager<Scene> _scenes;

        public MinesweeperApp() : base(ScreenWidth, ScreenHeight)
        {
            _scenes = new SceneManager<Scene>();
            // 씬이 바뀔 때마다 버퍼를 무효화 → 다음 프레임에서 화면 전체 재출력
            _scenes.SceneChanged += (_) => InvalidateBuffer();
        }

        protected override void Initialize()
        {
            Input.EnableMouse();
            ChangeToTitle();
        }

        protected override void Update(float deltaTime)
        {
            if (Input.IsKeyDown(ConsoleKey.Escape))
            {
                Quit();
                return;
            }
            _scenes.CurrentScene?.Update(deltaTime);
        }

        protected override void Draw()
        {
            _scenes.CurrentScene?.Draw(Buffer);
        }

        // ── 씬 전환 ──────────────────────────────────────────────────────

        private void ChangeToTitle()
        {
            var title = new TitleScene();
            title.GameStartRequested += (config) => ChangeToPlay(config);
            _scenes.ChangeScene(title);
        }

        private void ChangeToPlay(GameConfig config)
        {
            var play = new PlayScene(config);
            play.PlayAgainRequested += () => ChangeToTitle();
            _scenes.ChangeScene(play);
        }
    }
}