using System;
using System.Collections.Generic;
using Framework.Engine;

namespace Framework.Minesweeper
{
    //  셀 상태 
    public enum CellState { Hidden, Revealed, Flagged, Question }

    public class Cell
    {
        public bool IsMine;
        public int AdjacentMines; // 0~8
        public CellState State = CellState.Hidden;
    }

    //  PlayScene 
    public class PlayScene : Scene
    {
        public event GameAction PlayAgainRequested;

        //  게임 설정 
        private readonly GameConfig _config;
        private Cell[,] _board;          // [row, col]
        private bool _firstClick;
        private bool _gameOver;
        private bool _victory;
        private int _flagCount;
        private int _revealedCount;
        private float _timer;
        private bool _timerRunning;

        //  커서 (키보드 이동용) 
        private int _cursorCol;
        private int _cursorRow;

        //  화면 레이아웃 
        // 보드 시작 위치 (콘솔 기준)
        private int _boardOriginX;
        private int _boardOriginY;

        // 셀 하나당 콘솔 문자 폭 = 2 (숫자 + 공백)
        private const int CellW = 2;
        private const int CellH = 1;

        public PlayScene(GameConfig config)
        {
            _config = config;
        }

        //  생명주기 
        public override void Load()
        {
            InitBoard();

            // 보드를 화면 중앙에 배치
            int boardPixelW = _config.Cols * CellW;
            int boardPixelH = _config.Rows * CellH;
            _boardOriginX = (MinesweeperApp.ScreenWidth - boardPixelW) / 2;
            _boardOriginY = (MinesweeperApp.ScreenHeight - boardPixelH) / 2 + 1; // +1: 헤더 공간
            // 최소 Y = 3 (헤더) 보드가 너무 커서 계산결과가 헤더랑 겹칠 수 있어서 막음
            if (_boardOriginY < 3) _boardOriginY = 3;

            _cursorCol = _config.Cols / 2;
            _cursorRow = _config.Rows / 2;
        }

        private void InitBoard()
        {
            _board = new Cell[_config.Rows, _config.Cols];
            for (int r = 0; r < _config.Rows; r++)
                for (int c = 0; c < _config.Cols; c++)
                    _board[r, c] = new Cell();

            _firstClick = true;
            _gameOver = false;
            _victory = false;
            _flagCount = 0;
            _revealedCount = 0;
            _timer = 0f;
            _timerRunning = false;
        }

        public override void Unload() { }

        //  Update 
        public override void Update(float deltaTime)
        {
            if (_timerRunning && !_gameOver && !_victory)
                _timer += deltaTime;

            if (_gameOver || _victory)
            {
                HandleEndInput();
                return;
            }

            HandleKeyboard();
            HandleMouse();
        }

        private void HandleEndInput()
        {
            if (Input.IsKeyDown(ConsoleKey.Enter) ||
                Input.IsKeyDown(ConsoleKey.Spacebar) ||
                Input.Mouse.LeftDown)
            {
                PlayAgainRequested?.Invoke();
            }
        }

        private void HandleKeyboard()
        {
            // 이동
            if (Input.IsKeyDown(ConsoleKey.LeftArrow)) _cursorCol = Math.Max(0, _cursorCol - 1);
            if (Input.IsKeyDown(ConsoleKey.RightArrow)) _cursorCol = Math.Min(_config.Cols - 1, _cursorCol + 1);
            if (Input.IsKeyDown(ConsoleKey.UpArrow)) _cursorRow = Math.Max(0, _cursorRow - 1);
            if (Input.IsKeyDown(ConsoleKey.DownArrow)) _cursorRow = Math.Min(_config.Rows - 1, _cursorRow + 1);

            // Enter / Space: 열기
            if (Input.IsKeyDown(ConsoleKey.Enter) || Input.IsKeyDown(ConsoleKey.Spacebar))
                RevealCell(_cursorRow, _cursorCol);

            // F: 깃발
            if (Input.IsKeyDown(ConsoleKey.F))
                ToggleFlag(_cursorRow, _cursorCol);
        }

        private void HandleMouse()
        {
            int mx = Input.Mouse.X;
            int my = Input.Mouse.Y;

            // 마우스 좌표 → 보드 셀 좌표
            int col = (mx - _boardOriginX) / CellW;
            int row = (my - _boardOriginY) / CellH;

            if (row < 0 || row >= _config.Rows || col < 0 || col >= _config.Cols) return;

            // 호버: 커서 이동
            _cursorRow = row;
            _cursorCol = col;

            if (Input.Mouse.LeftDown)
                RevealCell(row, col);

            if (Input.Mouse.RightDown)
                ToggleFlag(row, col);
        }

        // ── 게임 로직 ─────────────────────────────────────────────────────
        private void RevealCell(int row, int col)
        {
            var cell = _board[row, col];
            if (cell.State == CellState.Flagged || cell.State == CellState.Revealed) return;

            // 첫 클릭: 지뢰 배치 (클릭한 셀 제외)
            if (_firstClick)
            {
                PlaceMines(row, col);
                CalcAdjacent();
                _firstClick = false;
                _timerRunning = true;
            }

            if (cell.IsMine)
            {
                // 게임 오버
                cell.State = CellState.Revealed;
                _gameOver = true;
                _timerRunning = false;
                RevealAllMines();
                return;
            }

            FloodReveal(row, col);
            CheckVictory();
        }

        private void ToggleFlag(int row, int col)
        {
            var cell = _board[row, col];
            if (cell.State == CellState.Revealed) return;

            if (cell.State == CellState.Hidden)
            {
                cell.State = CellState.Flagged;
                _flagCount++;
            }
            else if (cell.State == CellState.Flagged)
            {
                cell.State = CellState.Question;
                _flagCount--;
            }
            else if (cell.State == CellState.Question)
            {
                cell.State = CellState.Hidden;
            }
        }

        private void PlaceMines(int safeRow, int safeCol)
        {
            var rng = new Random();
            int placed = 0;
            while (placed < _config.Mines)
            {
                int r = rng.Next(_config.Rows);
                int c = rng.Next(_config.Cols);
                // 첫 클릭 셀 및 주변 8칸은 안전
                if (Math.Abs(r - safeRow) <= 1 && Math.Abs(c - safeCol) <= 1) continue;
                if (_board[r, c].IsMine) continue;
                _board[r, c].IsMine = true;
                placed++;
            }
        }

        private void CalcAdjacent()
        {
            int[] dr = { -1, -1, -1, 0, 0, 1, 1, 1 };
            int[] dc = { -1, 0, 1, -1, 1, -1, 0, 1 };
            for (int r = 0; r < _config.Rows; r++)
                for (int c = 0; c < _config.Cols; c++)
                {
                    if (_board[r, c].IsMine) continue;
                    int cnt = 0;
                    for (int d = 0; d < 8; d++)
                    {
                        int nr = r + dr[d], nc = c + dc[d];
                        if (nr >= 0 && nr < _config.Rows && nc >= 0 && nc < _config.Cols && _board[nr, nc].IsMine)
                            cnt++;
                    }
                    _board[r, c].AdjacentMines = cnt;
                }
        }

        private void FloodReveal(int startRow, int startCol)
        {
            var queue = new Queue<(int r, int c)>();
            queue.Enqueue((startRow, startCol));

            int[] dr = { -1, -1, -1, 0, 0, 1, 1, 1 };
            int[] dc = { -1, 0, 1, -1, 1, -1, 0, 1 };

            while (queue.Count > 0)
            {
                var (r, c) = queue.Dequeue();
                var cell = _board[r, c];
                if (cell.State == CellState.Revealed) continue;
                if (cell.State == CellState.Flagged) continue;

                cell.State = CellState.Revealed;
                _revealedCount++;

                // 숫자가 0인 셀은 주변도 자동 열기
                if (cell.AdjacentMines == 0)
                {
                    for (int d = 0; d < 8; d++)
                    {
                        int nr = r + dr[d], nc = c + dc[d];
                        if (nr >= 0 && nr < _config.Rows && nc >= 0 && nc < _config.Cols)
                            if (_board[nr, nc].State != CellState.Revealed)
                                queue.Enqueue((nr, nc));
                    }
                }
            }
        }

        private void RevealAllMines()
        {
            for (int r = 0; r < _config.Rows; r++)
                for (int c = 0; c < _config.Cols; c++)
                    if (_board[r, c].IsMine && _board[r, c].State != CellState.Flagged)
                        _board[r, c].State = CellState.Revealed;
        }

        private void CheckVictory()
        {
            int safeCells = _config.Rows * _config.Cols - _config.Mines;
            if (_revealedCount >= safeCells)
            {
                _victory = true;
                _timerRunning = false;
            }
        }

        // ── Draw ──────────────────────────────────────────────────────────
        public override void Draw(ScreenBuffer buffer)
        {
            DrawHeader(buffer);
            DrawBoard(buffer);
            DrawFooter(buffer);

            if (_gameOver) DrawOverlay(buffer, false);
            if (_victory) DrawOverlay(buffer, true);
        }

        private void DrawHeader(ScreenBuffer buffer)
        {
            // 남은 지뢰 수
            int remaining = _config.Mines - _flagCount;
            string mineStr = $"💣 {remaining:D3}";
            buffer.WriteText(2, 1, $"지뢰: {remaining:D3}", ConsoleColor.Red);

            // 타이머
            int secs = (int)_timer;
            buffer.WriteText(MinesweeperApp.ScreenWidth - 14, 1, $"시간: {secs:D4}s", ConsoleColor.Yellow);

            // 난이도
            buffer.WriteTextCentered(1, _config.Label, ConsoleColor.DarkGray);
        }

        private void DrawBoard(ScreenBuffer buffer)
        {
            // 보드 외곽 박스
            buffer.DrawBox(
                _boardOriginX - 1,
                _boardOriginY - 1,
                _config.Cols * CellW + 2,
                _config.Rows * CellH + 2,
                ConsoleColor.DarkGray
            );

            for (int r = 0; r < _config.Rows; r++)
            {
                for (int c = 0; c < _config.Cols; c++)
                {
                    int px = _boardOriginX + c * CellW;
                    int py = _boardOriginY + r * CellH;

                    bool isCursor = (r == _cursorRow && c == _cursorCol);
                    DrawCell(buffer, _board[r, c], px, py, isCursor);
                }
            }
        }

        private void DrawCell(ScreenBuffer buffer, Cell cell, int x, int y, bool cursor)
        {
            ConsoleColor bg = cursor ? ConsoleColor.DarkCyan : ConsoleColor.Black;

            switch (cell.State)
            {
                case CellState.Hidden:
                    buffer.WriteText(x, y, "■ ", cursor ? ConsoleColor.White : ConsoleColor.DarkGray, bg);
                    break;

                case CellState.Flagged:
                    buffer.WriteText(x, y, "F ", ConsoleColor.Red, bg);
                    break;

                case CellState.Question:
                    buffer.WriteText(x, y, "? ", ConsoleColor.Yellow, bg);
                    break;

                case CellState.Revealed:
                    if (cell.IsMine)
                    {
                        buffer.WriteText(x, y, "* ", ConsoleColor.Red, ConsoleColor.DarkRed);
                    }
                    else if (cell.AdjacentMines == 0)
                    {
                        buffer.WriteText(x, y, "  ", ConsoleColor.Gray, ConsoleColor.DarkGray);
                    }
                    else
                    {
                        char ch = (char)('0' + cell.AdjacentMines);
                        ConsoleColor fg = NumberColor(cell.AdjacentMines);
                        buffer.WriteText(x, y, $"{ch} ", fg, ConsoleColor.DarkGray);
                    }
                    break;
            }
        }

        private ConsoleColor NumberColor(int n) => n switch
        {
            1 => ConsoleColor.Cyan,
            2 => ConsoleColor.Green,
            3 => ConsoleColor.Red,
            4 => ConsoleColor.DarkBlue,
            5 => ConsoleColor.DarkRed,
            6 => ConsoleColor.DarkCyan,
            7 => ConsoleColor.Magenta,
            _ => ConsoleColor.White,
        };

        private void DrawFooter(ScreenBuffer buffer)
        {
            if (!_gameOver && !_victory)
            {
                buffer.WriteTextCentered(
                    MinesweeperApp.ScreenHeight - 1,
                    "방향키/마우스: 이동  Enter/좌클릭: 열기  F/우클릭: 깃발  ESC: 종료",
                    ConsoleColor.DarkGray
                );
            }
        }

        private void DrawOverlay(ScreenBuffer buffer, bool win)
        {
            int cy = MinesweeperApp.ScreenHeight / 2;

            if (win)
            {
                buffer.WriteTextCentered(cy - 1, "*** YOU WIN! ***", ConsoleColor.Yellow);
                buffer.WriteTextCentered(cy, $"Clear time: {(int)_timer}s", ConsoleColor.White);
            }
            else
            {
                buffer.WriteTextCentered(cy - 1, "!!! BOOM !!! You hit a mine!", ConsoleColor.Red);
                buffer.WriteTextCentered(cy, $"Time: {(int)_timer}s", ConsoleColor.White);
            }

            buffer.WriteTextCentered(cy + 2, "[ Enter / click to return to title ]", ConsoleColor.DarkGray);
        }
    }
}