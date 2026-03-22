using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Framework.Engine
{
    // ── 마우스 이벤트 데이터 ──────────────────────────────────────────────
    public struct MouseState
    {
        public int X;           // 콘솔 열(column)
        public int Y;           // 콘솔 행(row)
        public bool LeftDown;   // 이번 프레임에 누름
        public bool RightDown;  // 이번 프레임에 누름
        public bool LeftUp;     // 이번 프레임에 뗌
        public bool RightUp;    // 이번 프레임에 뗌
        public bool LeftHeld;   // 누르고 있는 동안
        public bool RightHeld;
    }

    public static class Input
    {
        // ── P/Invoke ─────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNumberOfConsoleInputEvents(IntPtr hConsoleInput, out uint lpNumberOfEvents);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadConsoleInput(IntPtr hConsoleInput, [Out] INPUT_RECORD[] lpBuffer, uint nLength, out uint lpNumberOfEventsRead);

        private const int STD_INPUT_HANDLE = -10;
        private const uint ENABLE_MOUSE_INPUT = 0x0010;
        private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        private const uint ENABLE_EXTENDED_FLAGS = 0x0080;
        private const ushort MOUSE_EVENT = 0x0002;
        private const uint FROM_LEFT_1ST_BUTTON_PRESSED = 0x0001;
        private const uint RIGHTMOST_BUTTON_PRESSED = 0x0002;

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT_RECORD
        {
            [FieldOffset(0)] public ushort EventType;
            [FieldOffset(4)] public MOUSE_EVENT_RECORD MouseEvent;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSE_EVENT_RECORD
        {
            public COORD dwMousePosition;
            public uint dwButtonState;
            public uint dwControlKeyState;
            public uint dwEventFlags;
        }

        // ── 키보드 상태 ───────────────────────────────────────────────────
        private static readonly HashSet<ConsoleKey> s_currentKeys = new();
        private static readonly HashSet<ConsoleKey> s_previousKeys = new();

        private static readonly ConsoleKey[] s_trackedKeys =
        {
            ConsoleKey.UpArrow, ConsoleKey.DownArrow,
            ConsoleKey.LeftArrow, ConsoleKey.RightArrow,
            ConsoleKey.D0, ConsoleKey.D1, ConsoleKey.D2, ConsoleKey.D3, ConsoleKey.D4,
            ConsoleKey.D5, ConsoleKey.D6, ConsoleKey.D7, ConsoleKey.D8, ConsoleKey.D9,
            ConsoleKey.NumPad0, ConsoleKey.NumPad1, ConsoleKey.NumPad2, ConsoleKey.NumPad3,
            ConsoleKey.NumPad4, ConsoleKey.NumPad5, ConsoleKey.NumPad6, ConsoleKey.NumPad7,
            ConsoleKey.NumPad8, ConsoleKey.NumPad9,
            ConsoleKey.Enter, ConsoleKey.Escape, ConsoleKey.Spacebar,
            ConsoleKey.Tab, ConsoleKey.Backspace,
            ConsoleKey.H, ConsoleKey.S, ConsoleKey.Y, ConsoleKey.N,
            ConsoleKey.W, ConsoleKey.A, ConsoleKey.D, ConsoleKey.F,
        };

        // ── 마우스 상태 ───────────────────────────────────────────────────
        private static int s_mouseX;
        private static int s_mouseY;
        private static bool s_leftHeld;
        private static bool s_rightHeld;
        private static bool s_prevLeftHeld;
        private static bool s_prevRightHeld;

        private static IntPtr s_inputHandle = IntPtr.Zero;
        private static bool s_mouseEnabled;

        public static MouseState Mouse { get; private set; }
        public static bool HasInput => s_currentKeys.Count > 0;

        // ── 초기화 ────────────────────────────────────────────────────────
        public static void EnableMouse()
        {
            try
            {
                s_inputHandle = GetStdHandle(STD_INPUT_HANDLE);
                if (GetConsoleMode(s_inputHandle, out uint mode))
                {
                    // QuickEdit 끄기 + 마우스 입력 켜기
                    mode &= ~ENABLE_QUICK_EDIT_MODE;
                    mode |= ENABLE_EXTENDED_FLAGS;
                    mode |= ENABLE_MOUSE_INPUT;
                    SetConsoleMode(s_inputHandle, mode);
                    s_mouseEnabled = true;
                }
            }
            catch
            {
                s_mouseEnabled = false;
            }
        }

        // ── Poll (매 프레임 엔진이 호출) ──────────────────────────────────
        public static void Poll()
        {
            // 키보드
            s_previousKeys.Clear();
            foreach (var key in s_currentKeys) s_previousKeys.Add(key);
            s_currentKeys.Clear();

            foreach (var key in s_trackedKeys)
            {
                if ((GetAsyncKeyState((int)key) & 0x8000) != 0)
                    s_currentKeys.Add(key);
            }

            // 마우스
            s_prevLeftHeld = s_leftHeld;
            s_prevRightHeld = s_rightHeld;

            if (s_mouseEnabled && s_inputHandle != IntPtr.Zero)
            {
                PollMouseEvents();
            }
            else
            {
                // 마우스 미지원 환경: GetAsyncKeyState 폴백
                s_leftHeld = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
                s_rightHeld = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
            }

            Mouse = new MouseState
            {
                X = s_mouseX,
                Y = s_mouseY,
                LeftHeld = s_leftHeld,
                RightHeld = s_rightHeld,
                LeftDown = s_leftHeld && !s_prevLeftHeld,
                LeftUp = !s_leftHeld && s_prevLeftHeld,
                RightDown = s_rightHeld && !s_prevRightHeld,
                RightUp = !s_rightHeld && s_prevRightHeld,
            };

            // Console 키 버퍼 drain
            while (Console.KeyAvailable) Console.ReadKey(true);
        }

        private static void PollMouseEvents()
        {
            if (!GetNumberOfConsoleInputEvents(s_inputHandle, out uint count) || count == 0)
            {
                // 이벤트 없으면 버튼 상태는 GetAsyncKeyState로 유지
                s_leftHeld = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
                s_rightHeld = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
                return;
            }

            var records = new INPUT_RECORD[count];
            ReadConsoleInput(s_inputHandle, records, count, out uint read);

            for (uint i = 0; i < read; i++)
            {
                if (records[i].EventType == MOUSE_EVENT)
                {
                    var me = records[i].MouseEvent;
                    s_mouseX = me.dwMousePosition.X;
                    s_mouseY = me.dwMousePosition.Y;
                    s_leftHeld = (me.dwButtonState & FROM_LEFT_1ST_BUTTON_PRESSED) != 0;
                    s_rightHeld = (me.dwButtonState & RIGHTMOST_BUTTON_PRESSED) != 0;
                }
            }
        }

        // ── 키보드 API ────────────────────────────────────────────────────
        public static bool IsKey(ConsoleKey key) => s_currentKeys.Contains(key);
        public static bool IsKeyDown(ConsoleKey key) => s_currentKeys.Contains(key) && !s_previousKeys.Contains(key);
        public static bool IsKeyUp(ConsoleKey key) => !s_currentKeys.Contains(key) && s_previousKeys.Contains(key);
    }
}