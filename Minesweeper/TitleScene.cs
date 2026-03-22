using System;
using Framework.Engine;

namespace Framework.Minesweeper
{
    public class TitleScene : Scene
    {
        public event GameAction<GameConfig> GameStartRequested;

        private static readonly GameConfig[] s_difficulties =
        {
            GameConfig.Easy,
            GameConfig.Normal,
            GameConfig.Hard,
        };

        private int _selected; // 0~2

        // 메뉴 항목의 Y 좌표 (화면 중앙 근처)
        private const int MenuStartY = 10;

        public override void Load()
        {
            _selected = 0;
        }

        public override void Update(float deltaTime)
        {
            // 키보드: 위/아래 이동
            if (Input.IsKeyDown(ConsoleKey.UpArrow))
                _selected = (_selected - 1 + s_difficulties.Length) % s_difficulties.Length;

            if (Input.IsKeyDown(ConsoleKey.DownArrow))
                _selected = (_selected + 1) % s_difficulties.Length;

            // 마우스 호버
            int mx = Input.Mouse.X;
            int my = Input.Mouse.Y;
            for (int i = 0; i < s_difficulties.Length; i++)
            {
                int itemY = MenuStartY + i * 2;
                // 메뉴 텍스트 길이에 맞춰 클릭 영역 설정
                int itemX = (MinesweeperApp.ScreenWidth - s_difficulties[i].Label.Length - 4) / 2;
                if (my == itemY && mx >= itemX && mx < itemX + s_difficulties[i].Label.Length + 4)
                {
                    _selected = i;
                    if (Input.Mouse.LeftDown)
                        StartGame();
                }
            }

            // Enter 확인
            if (Input.IsKeyDown(ConsoleKey.Enter))
                StartGame();
        }

        private void StartGame()
        {
            GameStartRequested?.Invoke(s_difficulties[_selected]);
        }

        public override void Draw(ScreenBuffer buffer)
        {
            // 타이틀 MINESWEEPER (68자, 순수 ASCII, 글자별 조립)
            buffer.WriteTextCentered(2, @" /\/\   ___   _  _   ___   ___   _ _   ___   ___   ___   ___   ___ ", ConsoleColor.Cyan);
            buffer.WriteTextCentered(3, @"|    | |_ _| | \| | | __| / __| | | | | __| | __| | _ \ | __| | _ \", ConsoleColor.Cyan);
            buffer.WriteTextCentered(4, @"| /\ |  | |  | .` | | _|  \__ \ | '_| | _|  | _|  |  _/ | _|  |   /", ConsoleColor.Cyan);
            buffer.WriteTextCentered(5, @"|/  \| |___| |_|\_| |___| |___/ |_,_| |___| |___| |_|   |___| |_|_\", ConsoleColor.Cyan);

            buffer.WriteTextCentered(7, "- - - - - - - - - - - - - - - -", ConsoleColor.DarkGray);
            buffer.WriteTextCentered(8, "난이도 선택", ConsoleColor.Gray);

            // 메뉴 항목
            for (int i = 0; i < s_difficulties.Length; i++)
            {
                int itemY = MenuStartY + i * 2;
                bool isSel = (i == _selected);

                string label = "  " + s_difficulties[i].Label + "  ";
                int itemX = (MinesweeperApp.ScreenWidth - label.Length) / 2;

                ConsoleColor fg = isSel ? ConsoleColor.Black : ConsoleColor.White;
                ConsoleColor bg = isSel ? ConsoleColor.Yellow : ConsoleColor.Black;

                if (isSel)
                    buffer.WriteText(itemX - 2, itemY, ">", ConsoleColor.Yellow);

                buffer.WriteText(itemX, itemY, label, fg, bg);
            }

            buffer.WriteTextCentered(17, "[ up/down or mouse ]  [ enter or click to start ]", ConsoleColor.DarkGray);
            buffer.WriteTextCentered(19, "left click: open   right click: flag", ConsoleColor.DarkCyan);
            buffer.WriteTextCentered(21, "ESC: quit", ConsoleColor.DarkGray);
        }

        public override void Unload() { }
    }
}