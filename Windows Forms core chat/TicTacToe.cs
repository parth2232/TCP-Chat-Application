using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Windows_Forms_Chat
{
    public enum TileType
    {
        Blank, Cross, Naught
    }

    public enum GameState
    {
        WaitingForPlayers, Playing, Draw, CrossWins, NaughtWins
    }

    public class TicTacToe
    {
        private const int BOARD_SIZE = 9;
        public GameState CurrentState { get; private set; } = GameState.WaitingForPlayers;
        public TileType CurrentPlayer { get; private set; } = TileType.Cross;
        public List<Button> Buttons { get; } = new List<Button>();
        private TileType[] grid = new TileType[BOARD_SIZE];

        public TicTacToe()
        {
            ResetBoard();
        }

        public string GridToString()
        {
            return new string(grid.Select(t => t switch
            {
                TileType.Blank => '_',
                TileType.Cross => 'X',
                TileType.Naught => 'O',
                _ => throw new InvalidOperationException("Invalid TileType")
            }).ToArray());
        }

        public void StringToGrid(string s)
        {
            if (s.Length != BOARD_SIZE)
                throw new ArgumentException("Invalid grid string length");

            for (int i = 0; i < BOARD_SIZE; i++)
            {
                grid[i] = s[i] switch
                {
                    '_' => TileType.Blank,
                    'X' => TileType.Cross,
                    'O' => TileType.Naught,
                    _ => throw new ArgumentException($"Invalid character in grid string: {s[i]}")
                };
                UpdateButton(i);
            }
        }

        public bool SetTile(int index, TileType tileType)
        {
            if (index < 0 || index >= BOARD_SIZE)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (CurrentState != GameState.Playing)
                return false;

            if (grid[index] != TileType.Blank || tileType != CurrentPlayer)
                return false;

            grid[index] = tileType;
            UpdateButton(index);
            UpdateGameState();
            if (CurrentState == GameState.Playing)
                SwitchPlayer();
            return true;
        }

        public void StartGame()
        {
            if (CurrentState == GameState.WaitingForPlayers)
            {
                CurrentState = GameState.Playing;
                CurrentPlayer = TileType.Cross;
            }
        }

        private void UpdateGameState()
        {
            if (CheckForWin(TileType.Cross))
                CurrentState = GameState.CrossWins;
            else if (CheckForWin(TileType.Naught))
                CurrentState = GameState.NaughtWins;
            else if (CheckForDraw())
                CurrentState = GameState.Draw;
        }

        private bool CheckForWin(TileType t)
        {
            // Check rows, columns, and diagonals
            for (int i = 0; i < 3; i++)
            {
                if ((grid[i * 3] == t && grid[i * 3 + 1] == t && grid[i * 3 + 2] == t) ||
                    (grid[i] == t && grid[i + 3] == t && grid[i + 6] == t))
                    return true;
            }
            return (grid[0] == t && grid[4] == t && grid[8] == t) ||
                   (grid[2] == t && grid[4] == t && grid[6] == t);
        }

        private bool CheckForDraw()
        {
            return !grid.Contains(TileType.Blank);
        }

        public void ResetBoard()
        {
            for (int i = 0; i < BOARD_SIZE; i++)
            {
                grid[i] = TileType.Blank;
                UpdateButton(i);
            }
            CurrentState = GameState.WaitingForPlayers;
            CurrentPlayer = TileType.Cross;
        }

        private void UpdateButton(int index)
        {
            if (index >= 0 && index < BOARD_SIZE && Buttons.Count > index)
            {
                Buttons[index].Text = TileTypeToString(grid[index]);
            }
        }

        private void SwitchPlayer()
        {
            CurrentPlayer = CurrentPlayer == TileType.Cross ? TileType.Naught : TileType.Cross;
        }

        private static string TileTypeToString(TileType t) => t switch
        {
            TileType.Blank => "",
            TileType.Cross => "X",
            TileType.Naught => "O",
            _ => throw new ArgumentOutOfRangeException(nameof(t))
        };
    }
}