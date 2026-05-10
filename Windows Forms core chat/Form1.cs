using System;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace Windows_Forms_Chat
{
    public partial class Form1 : Form
    {
        private TicTacToe ticTacToe = new TicTacToe();
        private TCPChatServer server = null;
        private TCPChatClient client = null;
        private bool isPlayerTurn = false;
        private TileType playerTileType = TileType.Blank;

        public Form1()
        {
            InitializeComponent();
            InitializeTicTacToeButtons();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Initialization code here
        }

        private void InitializeTicTacToeButtons()
        {
            ticTacToe.Buttons.AddRange(new[] { button1, button2, button3, button4, button5, button6, button7, button8, button9 });
            for (int i = 0; i < ticTacToe.Buttons.Count; i++)
            {
                int index = i;
                ticTacToe.Buttons[i].Click += (sender, e) => AttemptMove(index);
            }
            EnableGameBoard(false);
        }

        private void AttemptMove(int index)
        {
            if (isPlayerTurn && client != null)
            {
                client.SendGameMove(index);
                isPlayerTurn = false;
                EnableGameBoard(false);
            }
        }

        public void UpdateGameBoard(string gameState)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateGameBoard(gameState)));
                return;
            }

            for (int i = 0; i < gameState.Length && i < ticTacToe.Buttons.Count; i++)
            {
                ticTacToe.Buttons[i].Text = gameState[i] == '_' ? "" : gameState[i].ToString();
            }
        }

        public void EnableGameBoard(bool enable)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => EnableGameBoard(enable)));
                return;
            }

            foreach (Button button in ticTacToe.Buttons)
            {
                button.Enabled = enable && string.IsNullOrEmpty(button.Text);
            }
            isPlayerTurn = enable;
        }

        public void JoinGame(int playerNumber)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => JoinGame(playerNumber)));
                return;
            }

            playerTileType = playerNumber == 1 ? TileType.Cross : TileType.Naught;
            ChatTextBox.AppendText($"You have joined the game as Player {playerNumber} ({(playerNumber == 1 ? 'X' : 'O')}){Environment.NewLine}");
            EnableGameBoard(false);
        }

        private void ChatTextBox_TextChanged(object sender, EventArgs e)
        {
            // Code to handle the text changed event
        }

        public void UpdateServerUI(string gameState)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateServerUI), gameState);
                return;
            }

            for (int i = 0; i < gameState.Length && i < ticTacToe.Buttons.Count; i++)
            {
                ticTacToe.Buttons[i].Text = gameState[i] == '_' ? "" : gameState[i].ToString();
            }
        }
        private bool CanHostOrJoin()
        {
            return server == null && client == null;
        }

        private void HostButton_Click(object sender, EventArgs e)
        {
            if (CanHostOrJoin())
            {
                try
                {
                    int port = int.Parse(MyPortTextBox.Text);
                    server = TCPChatServer.CreateInstance(port, ChatTextBox, this);

                    if (server == null)
                        throw new Exception("Failed to create server instance!");

                    server.SetupServer();
                }
                catch (Exception ex)
                {
                    ChatTextBox.AppendText($"Error: {ex.Message}{Environment.NewLine}");
                }
            }
        }

        private void JoinButton_Click(object sender, EventArgs e)
        {
            if (CanHostOrJoin())
            {
                try
                {
                    int port = int.Parse(MyPortTextBox.Text);
                    int serverPort = int.Parse(serverPortTextBox.Text);

                    client = TCPChatClient.CreateInstance(port, serverPort, ServerIPTextBox.Text, ChatTextBox, this);

                    if (client == null)
                        throw new Exception("Invalid client configuration!");

                    client.ConnectToServer();

                    // Prompt user to login or register
                    string action = "";
                    while (action != "login" && action != "register")
                    {
                        action = Interaction.InputBox("Do you want to login or register?", "Login/Register", "login").ToLower();
                        if (action != "login" && action != "register")
                        {
                            MessageBox.Show("Invalid action. Please type 'login' or 'register'.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    string username = Interaction.InputBox("Enter your username:", "Username", "");
                    string password = Interaction.InputBox("Enter your password:", "Password", "");

                    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                    {
                        ChatTextBox.AppendText($"Username and password cannot be empty. Connection aborted.{Environment.NewLine}");
                        return;
                    }

                    if (action == "login")
                    {
                        client.SendLoginCommand(username, password);
                    }
                    else if (action == "register")
                    {
                        client.SendRegistrationCommand(username, password);
                    }
                }
                catch (Exception ex)
                {
                    client = null;
                    ChatTextBox.AppendText($"Error: {ex.Message}{Environment.NewLine}");
                }
            }
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            if (client != null)
            {
                client.SendChatMessage(TypeTextBox.Text);
                TypeTextBox.Clear();
            }
            else if (server != null)
            {
                server.ServerCommand(TypeTextBox.Text);
                TypeTextBox.Clear();
            }
        }
        private void ModButton_Click(object sender, EventArgs e)
        {
            if (server != null)
            {
                string username = TypeTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(username))
                {
                    server.ServerCommand($"!mod {username}");
                    TypeTextBox.Clear();
                }
                else
                {
                    ChatTextBox.AppendText($"Error: Please enter a username to mod/unmod.{Environment.NewLine}");
                }
            }
            else
            {
                ChatTextBox.AppendText($"Error: Server is not running.{Environment.NewLine}");
            }
        }

        // Event handlers for Tic-Tac-Toe buttons
        private void button1_Click(object sender, EventArgs e) => AttemptMove(0);
        private void button2_Click(object sender, EventArgs e) => AttemptMove(1);
        private void button3_Click(object sender, EventArgs e) => AttemptMove(2);
        private void button4_Click(object sender, EventArgs e) => AttemptMove(3);
        private void button5_Click(object sender, EventArgs e) => AttemptMove(4);
        private void button6_Click(object sender, EventArgs e) => AttemptMove(5);
        private void button7_Click(object sender, EventArgs e) => AttemptMove(6);
        private void button8_Click(object sender, EventArgs e) => AttemptMove(7);
        private void button9_Click(object sender, EventArgs e) => AttemptMove(8);
    }
}