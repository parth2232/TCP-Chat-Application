using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Data.SQLite;

namespace Windows_Forms_Chat
{
    public class ClientSocket
    {
        public Socket socket;
        public byte[] buffer = new byte[BUFFER_SIZE];
        public string username;
        public const int BUFFER_SIZE = 1024;
        public bool IsModerator { get; set; } = false;
        public bool isAuthenticated { get; set; } = false;
        public ClientState State { get; set; } = ClientState.Login;
        public int PlayerNumber { get; set; } = 0;
    }

    public class TCPChatServer : TCPChatBase
    {
        private Socket serverSocket;
        private List<ClientSocket> clientSockets = new List<ClientSocket>();
        private List<ClientSocket> gamePlayers = new List<ClientSocket>();
        new private int port;  // Add 'new' keyword here
        new private TextBox chatTextBox;  // Add 'new' keyword here
        private TicTacToe ticTacToe = new TicTacToe();
        private ClientSocket currentPlayer = null;
        private bool gameInProgress = false;
        private DatabaseAccess dbAccess;
        private object gameLock = new object();
        private Form1 serverForm;

        private TCPChatServer(int port, TextBox chatTextBox, Form1 form)
            : base()
        {
            this.port = port;
            this.chatTextBox = chatTextBox ?? throw new ArgumentNullException(nameof(chatTextBox));
            this.dbAccess = new DatabaseAccess("testDB.db");
            this.serverForm = form ?? throw new ArgumentNullException(nameof(form));
        }

        public static TCPChatServer CreateInstance(int port, TextBox chatTextBox, Form1 form)
        {
            if (port <= 0 || port >= 65535 || chatTextBox == null || form == null)
            {
                return null;
            }
            return new TCPChatServer(port, chatTextBox, form);
        }
        // Server setup and management methods
        public void SetupServer()
        {
            try
            {
                AddToChat("Setting up server...");
                dbAccess.InitializeDatabase();
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                serverSocket.Listen(0);
                serverSocket.BeginAccept(AcceptCallback, null);
                AddToChat("Server setup complete");
            }
            catch (Exception ex)
            {
                AddToChat($"Error setting up server: {ex.Message}");
            }
        }

        private void HandleRegistration(ClientSocket clientSocket, string username, string password)
        {
            AddToChat($"DEBUG: Handling registration for user: {username}");

            // Check if the username already exists
            if (clientSockets.Any(c => c.username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                SendToClient(clientSocket, "Registration failed. Username already exists.");
                return;
            }

            // Attempt to add the user to the database
            if (dbAccess.AddUser(username, password))
            {
                SendToClient(clientSocket, "Registration successful. You can now login with your credentials.");
                AddToChat($"New user registered: {username}");

                // Automatically log in the user after successful registration
                clientSocket.username = username;
                clientSocket.isAuthenticated = true;
                clientSocket.State = ClientState.Chatting;
                SendToClient(clientSocket, $"You are now logged in as: {username}");
                SendToAll($"{username} has joined the chat.", clientSocket);
            }
            else
            {
                SendToClient(clientSocket, "Registration failed. Please try again.");
                AddToChat($"Failed to register user: {username}");
            }
        }

        public void CloseAllSockets()
        {
            foreach (ClientSocket clientSocket in clientSockets)
            {
                clientSocket.socket.Shutdown(SocketShutdown.Both);
                clientSocket.socket.Close();
            }
            clientSockets.Clear();
            serverSocket.Close();
        }

        // Client connection handling
        private void AcceptCallback(IAsyncResult AR)
        {
            Socket joiningSocket;

            try
            {
                joiningSocket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            ClientSocket newClientSocket = new ClientSocket
            {
                socket = joiningSocket,
                username = "Unknown",
                isAuthenticated = false
            };

            clientSockets.Add(newClientSocket);
            joiningSocket.BeginReceive(newClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, newClientSocket);

            AddToChat("Client connected, waiting for authentication");
            SendToClient(newClientSocket, "Welcome! Please login or register.");
            SendToClient(newClientSocket, "To login: !login [username] [password]");
            SendToClient(newClientSocket, "To register: !register [username] [password]");

            serverSocket.BeginAccept(AcceptCallback, null);
        }



        // Message receiving and processing
        private void ReceiveCallback(IAsyncResult AR)
        {
            ClientSocket currentClientSocket = (ClientSocket)AR.AsyncState;
            try
            {
                int received = currentClientSocket.socket.EndReceive(AR);

                if (received == 0)
                {
                    AddToChat($"Client {currentClientSocket.username} disconnected");
                    HandleClientDisconnection(currentClientSocket);
                    return;
                }

                byte[] recBuf = new byte[received];
                Array.Copy(currentClientSocket.buffer, recBuf, received);
                string text = Encoding.UTF8.GetString(recBuf).Trim();

                ProcessReceivedMessage(currentClientSocket, text);

                currentClientSocket.socket.BeginReceive(currentClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, currentClientSocket);
            }
            catch (Exception ex)
            {
                AddToChat($"Error in ReceiveCallback for {currentClientSocket.username}: {ex.Message}");
                HandleClientDisconnection(currentClientSocket);
            }
        }

        private void ProcessReceivedMessage(ClientSocket clientSocket, string message)
        {
            try
            {
                if (message.StartsWith("!register "))
                {
                    string[] parts = message.Split(' ');
                    if (parts.Length == 3)
                    {
                        HandleRegistration(clientSocket, parts[1], parts[2]);
                    }
                    else
                    {
                        SendToClient(clientSocket, "Invalid registration command. Use: !register [username] [password]");
                    }
                }
                else if (message.StartsWith("!login "))
                {
                    HandleLoginCommand(clientSocket, message);
                }
                else if (!clientSocket.isAuthenticated)
                {
                    SendToClient(clientSocket, "You are not logged in. Please login or register first.");
                }
                else
                {
                    // Rest of the existing code for handling authenticated messages
                    switch (clientSocket.State)
                    {
                        case ClientState.Chatting:
                            HandleChatMessage(clientSocket, message);
                            break;
                        case ClientState.Playing:
                            HandleGameMessage(clientSocket, message);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                AddToChat($"Error in ProcessReceivedMessage: {ex.Message}");
            }
        }
    
    private void HandleLoginCommand(ClientSocket clientSocket, string message)
        {
            string[] parts = message.Split(' ');
            if (parts.Length == 3)
            {
                string username = parts[1];
                string password = parts[2];
                HandleLogin(clientSocket, username, password);
            }
            else
            {
                SendToClient(clientSocket, "Invalid login command. Use: !login [username] [password]");
            }
        }

        private void HandleLoginMessage(ClientSocket clientSocket, string message)
        {
            if (message.StartsWith("!login ") || message.StartsWith("!register "))
            {
                HandleCommand(clientSocket, message);
            }
            else
            {
                SendToClient(clientSocket, "Please login or register first.");
            }
        }

        private void HandleChatMessage(ClientSocket clientSocket, string message)
        {
            if (message.StartsWith("!"))
            {
                HandleCommand(clientSocket, message);
            }
            else
            {
                string messageWithSender = $"{clientSocket.username}: {message}";
                SendToAll(messageWithSender, clientSocket);
                AddToChat(messageWithSender);
            }
        }
        private void HandleGameMessage(ClientSocket clientSocket, string message)
        {
            if (message.StartsWith("MOVE:"))
            {
                HandleGameMove(clientSocket, message.Substring(5));
            }
            else if (message.StartsWith("!"))
            {
                HandleCommand(clientSocket, message);
            }
            else
            {
                // Handle chat messages during game
                string messageWithSender = $"{clientSocket.username}: {message}";
                SendToAll(messageWithSender, clientSocket);
                AddToChat(messageWithSender);
            }
        }

        // Command handling

        private void HandleGameStateMessage(ClientSocket clientSocket, string gameState)
        {
            if (!gameInProgress)
            {
                SendToClient(clientSocket, "No game is in progress. Use !startgame to begin a new game.");
                return;
            }

            if (clientSocket != currentPlayer)
            {
                SendToClient(clientSocket, "It's not your turn.");
                return;
            }

            try
            {
                ticTacToe.StringToGrid(gameState);
                SendToAll($"GAME_STATE:{gameState}", null);

                GameState newState = ticTacToe.CurrentState;
                if (newState == GameState.CrossWins || newState == GameState.NaughtWins || newState == GameState.Draw)
                {
                    string result = newState == GameState.Draw ? "The game is a draw!" : $"{currentPlayer.username} wins!";
                    SendToAll(result, null);
                    gameInProgress = false;
                }
                else
                {
                    SwitchCurrentPlayer();
                }
            }
            catch (ArgumentException ex)
            {
                SendToClient(clientSocket, $"Invalid game state: {ex.Message}");
            }
        }

        private void StartNewGame()
        {
            if (clientSockets.Count < 2)
            {
                SendToAll("Not enough players to start a game.", null);
                return;
            }

            if (gameInProgress)
            {
                SendToAll("A game is already in progress.", null);
                return;
            }

            ticTacToe.ResetBoard();
            ticTacToe.StartGame();
            gameInProgress = true;
            currentPlayer = clientSockets[0]; // First player starts
            string initialState = ticTacToe.GridToString();
            SendToAll($"GAME_STATE:{initialState}", null);
            SendToAll($"New game started. {currentPlayer.username} (X) goes first.", null);
        }

        private void SwitchCurrentPlayer()
        {
            int currentIndex = clientSockets.IndexOf(currentPlayer);
            currentPlayer = clientSockets[(currentIndex + 1) % clientSockets.Count];
            SendToAll($"It's {currentPlayer.username}'s turn.", null);
        }
        public void ServerCommand(string command)
        {
            HandleCommand(null, command);
        }

        private void HandleCommand(ClientSocket sender, string command)
        {
            string[] parts = command.Split(new char[] { ' ' }, 3);
            string cmd = parts[0].ToLower();

            switch (cmd)
            {
                case "!login":
                    if (parts.Length == 3)
                    {
                        HandleLogin(sender, parts[1], parts[2]);
                    }
                    else
                    {
                        SendToClient(sender, "Usage: !login [username] [password]");
                    }
                    break;
                case "!register":
                    if (parts.Length == 3)
                    {
                        HandleRegistration(sender, parts[1], parts[2]);
                    }
                    else
                    {
                        SendToClient(sender, "Usage: !register [username] [password]");
                    }
                    break;
                case "!startgame":
                    StartNewGame();
                    break;

                case "!commands":
                    AddToChat($"{sender.username} requested command list");
                    SendCommandList(sender);
                    break;
                case "!who":
                    AddToChat($"{sender.username} requested user list");
                    SendConnectedUsers(sender);
                    break;
                case "!about":
                    AddToChat($"{sender.username} requested server info");
                    SendAboutInfo(sender);
                    break;
                case "!whisper":
                    if (parts.Length > 1)
                    {
                        string[] whisperParts = parts[1].Split(new char[] { ' ' }, 2);
                        if (whisperParts.Length > 0)
                        {
                            string targetUsername = whisperParts[0];
                            AddToChat($"{sender.username} whispers to {targetUsername}");
                        }
                    }
                    HandleWhisper(sender, parts.Length > 1 ? parts[1] : "");
                    break;
                case "!joke":
                    AddToChat($"{sender.username} requested a joke");
                    SendJoke(sender);
                    break;
                case "!username":
                    if (parts.Length == 2)
                    {
                        string newUsername = parts[1];
                        if (dbAccess.UpdateUsername(sender.username, newUsername))
                        {
                            string oldUsername = sender.username;
                            sender.username = newUsername;
                            SendToAll($"{oldUsername} is now known as {newUsername}", sender);
                            SendToClient(sender, $"Username set to: {newUsername}");
                        }
                        else
                        {
                            SendToClient(sender, "Failed to update username. It may already be in use.");
                        }
                    }
                    else
                    {
                        SendToClient(sender, "Usage: !username [new_username]");
                    }
                    break;
                case "!mod":
                    if (IsServerSocket(sender))
                    {
                        AddToChat("Server is modifying moderator status");
                    }
                    else
                    {
                        AddToChat($"{sender.username} attempted to use mod command (not authorized)");
                    }
                    HandleModCommand(sender, parts.Length > 1 ? parts[1] : "");
                    break;
                case "!kick":
                    if (sender.IsModerator || IsServerSocket(sender))
                    {
                        AddToChat($"{(IsServerSocket(sender) ? "Server" : sender.username)} is attempting to kick a user");
                    }
                    else
                    {
                        AddToChat($"{sender.username} attempted to use kick command (not authorized)");
                    }
                    HandleKickCommand(sender, parts.Length > 1 ? parts[1] : "");
                    break;
                case "!mods":
                    AddToChat($"{sender.username} requested moderator list");
                    SendModeratorList(sender);
                    break;
                case "!join":
                    HandleJoinGame(sender);
                    break;
                case "!scores":
                    SendScoresToClient(sender);
                    break;

                default:
                    
                    SendToClient(sender, "Unknown command. Type !commands for a list of available commands.");
                    break;

            }
        }

        private void HandleLogin(ClientSocket clientSocket, string username, string password)
        {
            AddToChat($"DEBUG: Handling login for user: {username}");
            if (dbAccess.ValidateUser(username, password))
            {
                if (IsUsernameInUse(username))
                {
                    SendToClient(clientSocket, "Login failed. This username is already logged in.");
                    return;
                }
                clientSocket.username = username;
                clientSocket.isAuthenticated = true;
                clientSocket.State = ClientState.Chatting;
                SendToClient(clientSocket, $"Login successful, {username}!");
                SendToAll($"{username} has joined the chat.", clientSocket);
                AddToChat($"{username} has logged in");
            }
            else
            {
                SendToClient(clientSocket, "Login failed. Invalid username or password.");
            }
        }


        // Specific command handlers
        private void HandleUsernameChange(ClientSocket clientSocket, string newUsername)
        {
            if (string.IsNullOrEmpty(newUsername))
            {
                SendToClient(clientSocket, "Invalid username. Usage: !username [new_username]");
                return;
            }

            if (IsUsernameInUse(newUsername))
            {
                SendToClient(clientSocket, "Username already in use. Please choose another.");
                return;
            }

            if (dbAccess.UpdateUsername(clientSocket.username, newUsername))
            {
                string oldUsername = clientSocket.username;
                clientSocket.username = newUsername;
                SendToAll($"{oldUsername} is now known as {newUsername}", clientSocket);
                SendToClient(clientSocket, $"Username set to: {newUsername}");
            }
            else
            {
                SendToClient(clientSocket, "Failed to update username. It may already be in use in the database.");
            }
        }

        private bool IsUsernameInUse(string username)
        {
            return clientSockets.Any(c => c.username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        private void HandleJoinGame(ClientSocket sender)
        {
            try
            {
                AddToChat($"DEBUG: HandleJoinGame called for {sender.username}");

                lock (gameLock)
                {
                    if (sender.State != ClientState.Chatting)
                    {
                        SendToClient(sender, "You must be in the chat to join a game.");
                        return;
                    }

                    if (gamePlayers.Count >= 2)
                    {
                        SendToClient(sender, "The game is full. Please wait for the current game to end.");
                        return;
                    }

                    gamePlayers.Add(sender);
                    sender.State = ClientState.Playing;
                    sender.PlayerNumber = gamePlayers.Count;
                    SendToClient(sender, $"!player{sender.PlayerNumber}");

                    AddToChat($"DEBUG: {sender.username} joined as Player {sender.PlayerNumber}");

                    if (gamePlayers.Count == 2)
                    {
                        StartGame();
                    }
                    else
                    {
                        SendToClient(sender, "Waiting for another player to join...");
                    }
                }
            }
            catch (Exception ex)
            {
                AddToChat($"ERROR in HandleJoinGame: {ex.Message}\n{ex.StackTrace}");
            }
        }
        private void HandleWhisper(ClientSocket sender, string message)
        {
            string[] parts = message.Split(new char[] { ' ' }, 2);
            if (parts.Length < 2)
            {
                SendToClient(sender, "Usage: !whisper [username] [message]");
                return;
            }

            string targetUsername = parts[0];
            string whisperMessage = parts[1];

            ClientSocket target = clientSockets.FirstOrDefault(c => c.username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase));

            if (target != null)
            {
                SendToClient(target, $"[Whisper from {sender.username}]: {whisperMessage}");
                SendToClient(sender, $"[Whisper to {target.username}]: {whisperMessage}");
            }
            else
            {
                SendToClient(sender, $"User '{targetUsername}' not found.");
            }
        }

        private void HandleModCommand(ClientSocket sender, string username)
        {
            AddToChat($"Mod command received for user: {username}");
            if (!IsServerSocket(sender))
            {
                SendToClient(sender, "Only the server can use this command.");
                return;
            }

            ClientSocket target = GetClientByUsername(username);
            if (target == null)
            {
                SendToClient(sender, $"User '{username}' not found.");
                return;
            }

            target.IsModerator = !target.IsModerator;
            string action = target.IsModerator ? "promoted to moderator" : "demoted from moderator";
            string message = $"{target.username} has been {action}.";
            SendToAll(message, null);
            AddToChat(message);
        }

        private void StartGame()
        {
            try
            {
                AddToChat("DEBUG: StartGame called");
                ticTacToe.ResetBoard();
                ticTacToe.StartGame();
                SendToAll("Game started!", null);
                SendGameState();
                NotifyTurn();
            }
            catch (Exception ex)
            {
                AddToChat($"ERROR in StartGame: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void SendGameState()
        {
            try
            {
                string gameState = ticTacToe.GridToString();
                SendToAll($"GAME_STATE:{gameState}", null);
                serverForm.Invoke(new Action(() => serverForm.UpdateServerUI(gameState)));
            }
            catch (Exception ex)
            {
                AddToChat($"Error in SendGameState: {ex.Message}");
            }
        }
        private void UpdateServerGameBoard(string gameState)
        {
            if (serverForm != null)
            {
                serverForm.Invoke(new Action(() => serverForm.UpdateServerUI(gameState)));
            }
        }
        private void NotifyTurn()
        {
            try
            {
                AddToChat("DEBUG: NotifyTurn called");
                ClientSocket currentPlayer = gamePlayers[ticTacToe.CurrentPlayer == TileType.Cross ? 0 : 1];
                SendToClient(currentPlayer, "It's your turn to move.");
                SendToClient(gamePlayers[1 - gamePlayers.IndexOf(currentPlayer)], $"It's {currentPlayer.username}'s turn to move.");
            }
            catch (Exception ex)
            {
                AddToChat($"ERROR in NotifyTurn: {ex.Message}\n{ex.StackTrace}");
            }
        }


        private void HandleGameMove(ClientSocket clientSocket, string move)
        {
            if (!int.TryParse(move, out int position) || position < 0 || position >= 9)
            {
                SendToClient(clientSocket, "Invalid move. Please use a number between 0 and 8.");
                return;
            }

            int playerIndex = gamePlayers.IndexOf(clientSocket);
            if (playerIndex != (ticTacToe.CurrentPlayer == TileType.Cross ? 0 : 1))
            {
                SendToClient(clientSocket, "It's not your turn.");
                return;
            }

            if (ticTacToe.SetTile(position, ticTacToe.CurrentPlayer))
            {
                SendGameState();

                if (ticTacToe.CurrentState == GameState.Playing)
                {
                    NotifyTurn();
                }
                else
                {
                    EndGame();
                }
            }
            else
            {
                SendToClient(clientSocket, "Invalid move. The position is already occupied.");
            }
        }

        private void EndGame()
        {
            string result = ticTacToe.CurrentState switch
            {
                GameState.CrossWins => $"{gamePlayers[0].username} (X) wins!",
                GameState.NaughtWins => $"{gamePlayers[1].username} (O) wins!",
                GameState.Draw => "The game is a draw!",
                _ => "The game has ended unexpectedly."
            };

            // Update player scores in the database
            switch (ticTacToe.CurrentState)
            {
                case GameState.CrossWins:
                    dbAccess.UpdatePlayerScore(gamePlayers[0].username, true, false);
                    dbAccess.UpdatePlayerScore(gamePlayers[1].username, false, false);
                    break;
                case GameState.NaughtWins:
                    dbAccess.UpdatePlayerScore(gamePlayers[0].username, false, false);
                    dbAccess.UpdatePlayerScore(gamePlayers[1].username, true, false);
                    break;
                case GameState.Draw:
                    dbAccess.UpdatePlayerScore(gamePlayers[0].username, false, true);
                    dbAccess.UpdatePlayerScore(gamePlayers[1].username, false, true);
                    break;
            }

            // Inform players of the results
            SendToAll(result, null);

            // Inform players to return to chatting state
            foreach (var player in gamePlayers)
            {
                SendToClient(player, "The game has ended. You are now returned to the chat.");
                player.State = ClientState.Chatting;
            }

            // Reset game state
            ResetGameState();
        }


        private void ResetGameState()
        {
            foreach (var player in gamePlayers)
            {
                player.PlayerNumber = 0;
            }
            gamePlayers.Clear();
            ticTacToe.ResetBoard();
        }

        private void HandleKickCommand(ClientSocket sender, string username)
        {
            if (!sender.IsModerator && !IsServerSocket(sender))
            {
                SendToClient(sender, "You don't have permission to use this command.");
                return;
            }

            ClientSocket target = GetClientByUsername(username);
            if (target == null)
            {
                SendToClient(sender, $"User '{username}' not found.");
                return;
            }

            if (target.IsModerator && !IsServerSocket(sender))
            {
                SendToClient(sender, "You can't kick another moderator.");
                return;
            }

            KickClient(target);
            string message = $"{target.username} has been kicked from the server.";
            SendToAll(message, null);
            AddToChat(message);
        }

        // Utility methods
        public void SendToAll(string str, ClientSocket from)
        {
            foreach (ClientSocket c in clientSockets)
            {
                if (from == null || !from.socket.Equals(c.socket))
                {
                    byte[] data = Encoding.UTF8.GetBytes(str);
                    c.socket.Send(data);
                }
            }
        }

        private void SendToClient(ClientSocket client, string message)
        {
            try
            {
                AddToChat($"DEBUG: Sending to {client.username}: {message}");
                byte[] data = Encoding.UTF8.GetBytes(message);
                client.socket.BeginSend(data, 0, data.Length, SocketFlags.None, SendCallback, client);
            }
            catch (SocketException se)
            {
                AddToChat($"SocketException in SendToClient for {client.username}: ErrorCode={se.ErrorCode}, Message={se.Message}");
                HandleClientDisconnection(client);
            }
            catch (Exception ex)
            {
                AddToChat($"Exception in SendToClient for {client.username}: {ex.GetType().Name}, Message={ex.Message}");
                HandleClientDisconnection(client);
            }
        }

        private void SendCallback(IAsyncResult AR)
        {
            ClientSocket client = (ClientSocket)AR.AsyncState;
            try
            {
                client.socket.EndSend(AR);
            }
            catch (SocketException se)
            {
                AddToChat($"SocketException in SendCallback for {client.username}: ErrorCode={se.ErrorCode}, Message={se.Message}");
                HandleClientDisconnection(client);
            }
            catch (ObjectDisposedException ode)
            {
                AddToChat($"ObjectDisposedException in SendCallback for {client.username}: {ode.Message}");
                HandleClientDisconnection(client);
            }
            catch (Exception ex)
            {
                AddToChat($"Exception in SendCallback for {client.username}: {ex.GetType().Name}, Message={ex.Message}");
                HandleClientDisconnection(client);
            }
        }



        private void HandleClientDisconnection(ClientSocket client)
        {
            try
            {
                AddToChat($"Client {client.username} disconnected");
                lock (gameLock)
                {
                    clientSockets.Remove(client);
                    gamePlayers.Remove(client);

                    if (gamePlayers.Count == 1)
                    {
                        // End the game if only one player is left
                        SendToClient(gamePlayers[0], "Your opponent has disconnected. The game has ended.");
                        gamePlayers.Clear();
                        ticTacToe.ResetBoard();
                    }
                }
            }
            catch (Exception ex)
            {
                AddToChat($"ERROR in HandleClientDisconnection for {client.username}: {ex.Message}");
            }
        }

        new private void AddToChat(string message)
        {
            if (chatTextBox.InvokeRequired)
            {
                chatTextBox.Invoke(new Action(() => AddToChat(message)));
            }
            else
            {
                chatTextBox.AppendText(message + Environment.NewLine);
                chatTextBox.ScrollToCaret();
            }
        }

        private bool IsServerSocket(ClientSocket socket)
        {
            return socket == null;
        }

        private ClientSocket GetClientByUsername(string username)
        {
            return clientSockets.FirstOrDefault(c => c.username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsUsernameAvailable(string username)
        {
            return !clientSockets.Any(c => c.username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        // Helper methods for specific commands
        private void SendCommandList(ClientSocket client)
        {
            string commands = "Available commands:" + Environment.NewLine +
                              "!who - List connected users" + Environment.NewLine +
                              "!about - Server information" + Environment.NewLine +
                              "!whisper [username] [message] - Send a private message" + Environment.NewLine +
                              "!joke - Get a random joke" + Environment.NewLine +
                              "!username [new_name] - Change your username";
            SendToClient(client, commands);
        }
        private void SendScoresToClient(ClientSocket client)
        {
            var scores = dbAccess.GetSortedScores();
            StringBuilder scoreMessage = new StringBuilder("Scores (sorted by wins):\n");

            foreach (var (Username, Wins, Losses, Draws) in scores)
            {
                scoreMessage.AppendLine($"{Username}: Wins: {Wins}, Losses: {Losses}, Draws: {Draws}");
            }

            SendToClient(client, scoreMessage.ToString());
        }

        private void SendConnectedUsers(ClientSocket client)
        {
            string userList = "Connected users:" + string.Join(",", clientSockets.Select(c => c.username));
            SendToClient(client, userList);
        }

        private void SendAboutInfo(ClientSocket client)
        {
            string about = "Chat Server" + Environment.NewLine +
                           "Created by: AnjaliChahar" + Environment.NewLine +
                           "Purpose: For ASSESSMENT 2,Networking Project" + Environment.NewLine +
                           "Year of development: 2024";
            SendToClient(client, about);
        }

        private void SendJoke(ClientSocket client)
        {
            string[] jokes = new string[]
            {
                "Why don't scientists trust atoms? Because they make up everything!",
                "Why did the scarecrow win an award? He was outstanding in his field!",
                "Why don't eggs tell jokes? They'd crack each other up!",
                "Why did the math book look so sad? Because it had too many problems."
            };

            Random random = new Random();
            string joke = jokes[random.Next(jokes.Length)];
            SendToClient(client, $"Here's a joke for you: {joke}");
        }

        private void SendModeratorList(ClientSocket sender)
        {
            var moderators = clientSockets.Where(c => c.IsModerator).Select(c => c.username).ToList();
            string modList = moderators.Count > 0 ? string.Join(", ", moderators) : "No moderators";
            string message = $"Current moderators: {modList}";

            if (IsServerSocket(sender))
            {
                // If it's the server, send to all clients and add to chat
                SendToAll(message, null);
                AddToChat(message);
            }
            else
            {
                // If it's a client, just send to that client
                SendToClient(sender, message);
            }
        }

        private void KickClient(ClientSocket client)
        {
            SendToClient(client, "You have been kicked from the server.");
            client.socket.Shutdown(SocketShutdown.Both);
            client.socket.Close();
            clientSockets.Remove(client);
        }
    }
}