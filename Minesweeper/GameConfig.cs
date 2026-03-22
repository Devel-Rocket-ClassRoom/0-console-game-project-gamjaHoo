namespace Framework.Minesweeper
{
    public struct GameConfig
    {
        public int Cols;
        public int Rows;
        public int Mines;
        public string Label;

        public static readonly GameConfig Easy = new GameConfig { Cols = 9, Rows = 9, Mines = 10, Label = "쉬움  ( 9x9,  지뢰 10)" };
        public static readonly GameConfig Normal = new GameConfig { Cols = 16, Rows = 16, Mines = 40, Label = "보통  (16x16, 지뢰 40)" };
        public static readonly GameConfig Hard = new GameConfig { Cols = 30, Rows = 16, Mines = 99, Label = "어려움 (30x16, 지뢰 99)" };
    }
}