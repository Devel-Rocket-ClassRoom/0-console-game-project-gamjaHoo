using System;
using System.Text;
using System.Runtime.InteropServices;

namespace Framework.Engine
{
    public class ScreenBuffer
    {
        private readonly int _width;
        private readonly int _height;
        private char[,] _chars;
        private ConsoleColor[,] _fgColors;
        private ConsoleColor[,] _bgColors;
        private char[,] _prevChars;
        private ConsoleColor[,] _prevFgColors;
        private ConsoleColor[,] _prevBgColors;
        private bool _firstPresent;
        private readonly StringBuilder _frameBuilder;

        private static readonly int[] s_ansiFg = { 30, 34, 32, 36, 31, 35, 33, 37, 90, 94, 92, 96, 91, 95, 93, 97 };
        private static readonly int[] s_ansiBg = { 40, 44, 42, 46, 41, 45, 43, 47, 100, 104, 102, 106, 101, 105, 103, 107 };

        public int Width => _width;
        public int Height => _height;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr handle, uint mode);

        public ScreenBuffer(int width, int height)
        {
            _width = width;
            _height = height;
            _chars = new char[height, width];
            _fgColors = new ConsoleColor[height, width];
            _bgColors = new ConsoleColor[height, width];
            _prevChars = new char[height, width];
            _prevFgColors = new ConsoleColor[height, width];
            _prevBgColors = new ConsoleColor[height, width];
            _firstPresent = true;
            _frameBuilder = new StringBuilder(width * height * 20);
            Clear();
            EnableVirtualTerminalProcessing();
        }

        private static void EnableVirtualTerminalProcessing()
        {
            try
            {
                const int STD_OUTPUT_HANDLE = -11;
                const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

                IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (GetConsoleMode(handle, out uint mode))
                {
                    SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
                }
            }
            catch
            {
                // Non-Windows or unsupported — ANSI may already work
            }
        }

        public void Clear()
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    _chars[y, x] = ' ';
                    _fgColors[y, x] = ConsoleColor.Gray;
                    _bgColors[y, x] = ConsoleColor.Black;
                }
            }
        }

        /// <summary>
        /// 다음 Present()에서 화면 전체를 강제로 다시 그리도록 플래그를 리셋합니다.
        /// 씬 전환 등 화면이 완전히 바뀔 때 호출하세요.
        /// </summary>
        public void Invalidate()
        {
            _firstPresent = true;
        }

        public void SetCell(int x, int y, char ch, ConsoleColor color = ConsoleColor.Gray, ConsoleColor bgColor = ConsoleColor.Black)
        {
            if (x >= 0 && x < _width && y >= 0 && y < _height)
            {
                _chars[y, x] = ch;
                _fgColors[y, x] = color;
                _bgColors[y, x] = bgColor;
            }
        }

        public void WriteText(int x, int y, string text, ConsoleColor color = ConsoleColor.Gray, ConsoleColor bgColor = ConsoleColor.Black)
        {
            for (int i = 0; i < text.Length; i++)
            {
                SetCell(x + i, y, text[i], color, bgColor);
            }
        }

        public void WriteTextCentered(int y, string text, ConsoleColor color = ConsoleColor.Gray, ConsoleColor bgColor = ConsoleColor.Black)
        {
            int x = (_width - text.Length) / 2;
            WriteText(x, y, text, color, bgColor);
        }

        public void WriteLines(int x, int y, string[] lines, ConsoleColor color = ConsoleColor.Gray, ConsoleColor bgColor = ConsoleColor.Black)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                WriteText(x, y + i, lines[i], color, bgColor);
            }
        }

        public void DrawHLine(int x, int y, int length, char ch = '-', ConsoleColor color = ConsoleColor.Gray, ConsoleColor bgColor = ConsoleColor.Black)
        {
            for (int i = 0; i < length; i++)
            {
                SetCell(x + i, y, ch, color, bgColor);
            }
        }

        public void DrawVLine(int x, int y, int length, char ch = '|', ConsoleColor color = ConsoleColor.Gray, ConsoleColor bgColor = ConsoleColor.Black)
        {
            for (int i = 0; i < length; i++)
            {
                SetCell(x, y + i, ch, color, bgColor);
            }
        }

        public void DrawBox(int x, int y, int width, int height, ConsoleColor color = ConsoleColor.Gray, ConsoleColor bgColor = ConsoleColor.Black)
        {
            SetCell(x, y, '+', color, bgColor);
            SetCell(x + width - 1, y, '+', color, bgColor);
            SetCell(x, y + height - 1, '+', color, bgColor);
            SetCell(x + width - 1, y + height - 1, '+', color, bgColor);

            DrawHLine(x + 1, y, width - 2, '-', color, bgColor);
            DrawHLine(x + 1, y + height - 1, width - 2, '-', color, bgColor);
            DrawVLine(x, y + 1, height - 2, '|', color, bgColor);
            DrawVLine(x + width - 1, y + 1, height - 2, '|', color, bgColor);
        }

        public void FillRect(int x, int y, int width, int height, char ch = ' ', ConsoleColor color = ConsoleColor.Gray, ConsoleColor bgColor = ConsoleColor.Black)
        {
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    SetCell(x + col, y + row, ch, color, bgColor);
                }
            }
        }

        public void Present()
        {
            _frameBuilder.Clear();

            if (_firstPresent)
            {
                // 최초 프레임: 화면 전체 지우고 전체 렌더링
                _frameBuilder.Append("\x1b[2J\x1b[H");
                Console.CursorVisible = false;

                ConsoleColor currentFg = (ConsoleColor)(-1);
                ConsoleColor currentBg = (ConsoleColor)(-1);

                for (int y = 0; y < _height; y++)
                {
                    for (int x = 0; x < _width; x++)
                    {
                        ConsoleColor fg = _fgColors[y, x];
                        ConsoleColor bg = _bgColors[y, x];

                        if (fg != currentFg || bg != currentBg)
                        {
                            _frameBuilder.Append("\x1b[");
                            _frameBuilder.Append(s_ansiFg[(int)fg]);
                            _frameBuilder.Append(';');
                            _frameBuilder.Append(s_ansiBg[(int)bg]);
                            _frameBuilder.Append('m');
                            currentFg = fg;
                            currentBg = bg;
                        }

                        _frameBuilder.Append(_chars[y, x]);
                        _prevChars[y, x] = _chars[y, x];
                        _prevFgColors[y, x] = fg;
                        _prevBgColors[y, x] = bg;
                    }

                    if (y < _height - 1)
                        _frameBuilder.Append('\n');
                }

                _firstPresent = false;
            }
            else
            {
                // 이후 프레임: 변경이 있는 행만 통째로 재출력 (행 단위 diff)
                // 셀 단위 커서 이동은 전각 문자(▶, █ 등) 때문에 위치가 어긋나므로 사용하지 않음

                for (int y = 0; y < _height; y++)
                {
                    // 이 행에 변경된 셀이 하나라도 있는지 확인
                    bool rowDirty = false;
                    for (int x = 0; x < _width; x++)
                    {
                        if (_chars[y, x] != _prevChars[y, x] ||
                            _fgColors[y, x] != _prevFgColors[y, x] ||
                            _bgColors[y, x] != _prevBgColors[y, x])
                        {
                            rowDirty = true;
                            break;
                        }
                    }
                    if (!rowDirty) continue;

                    // 행 맨 앞으로 커서 이동 후 행 전체 재출력
                    _frameBuilder.Append("\x1b[");
                    _frameBuilder.Append(y + 1);
                    _frameBuilder.Append(";1H");

                    ConsoleColor rowFg = (ConsoleColor)(-1);
                    ConsoleColor rowBg = (ConsoleColor)(-1);

                    for (int x = 0; x < _width; x++)
                    {
                        char ch = _chars[y, x];
                        ConsoleColor fg = _fgColors[y, x];
                        ConsoleColor bg = _bgColors[y, x];

                        if (fg != rowFg || bg != rowBg)
                        {
                            _frameBuilder.Append("\x1b[");
                            _frameBuilder.Append(s_ansiFg[(int)fg]);
                            _frameBuilder.Append(';');
                            _frameBuilder.Append(s_ansiBg[(int)bg]);
                            _frameBuilder.Append('m');
                            rowFg = fg;
                            rowBg = bg;
                        }

                        _frameBuilder.Append(ch);

                        _prevChars[y, x] = ch;
                        _prevFgColors[y, x] = fg;
                        _prevBgColors[y, x] = bg;
                    }
                }
            }

            _frameBuilder.Append("\x1b[0m");

            if (_frameBuilder.Length > 6) // "\x1b[0m" 외 실제 변경이 있을 때만 출력
                Console.Write(_frameBuilder.ToString());
        }
    }
}