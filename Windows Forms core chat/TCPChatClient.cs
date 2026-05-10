using System;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace Windows_Forms_Chat
{
    public class TCPChatClient : TCPChatBase
    {
        private Socket socket;
        private ClientSocket clientSocket;
        private int serverPort;
        private string serverIP;
        private string username;
        private bool isAuthenticated = false;
        private Form1 form1;
        private ClientState state = ClientState.Login;
        private int playerNumber = 0;
        private Form1 form1Instance;
        public TCPChatClient(Form1 form)
        {
            this.form1 = form;
        }

        public enum ClientState
        {
            Login,
            Chatting,
            Playing
        }

        // Factory method to create a TCPChatClient instance
        public static TCPChatClient CreateInstance(int port, int serverPort, string serverIP, TextBox chatTextBox, Form1 form1)
        {
            // Validate input parameters
            if (port <= 0 || port >= 65535 ||
                serverPort <= 0 || serverPort >= 65535 ||
                string.IsNullOrEmpty(serverIP) ||
                chatTextBox == null ||
                form1 == null)
            {
                return null;
            }

            // Create and initialize the TCPChatClient
            TCPChatClient tcp = new TCPChatClient(form1)
            {
                port = port,
                serverPort = serverPort,
                serverIP = serverIP,
                chatTextBox = chatTextBox,
            };

            tcp.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcp.clientSocket = new ClientSocket { socket = tcp.socket };

            return tcp;
        }

        // Connects the client to the server
        public void ConnectToServer()
        {
            int attempts = 0;
            const int MAX_ATTEMPTS = 5;

            while (!socket.Connected && attempts < MAX_ATTEMPTS)
            {
                try
                {
                    attempts++;
                    SetChat($"Connection attempt {attempts}");
                    socket.Connect(serverIP, serverPort);
                }
                catch (SocketException)
                {
                    if (attempts == MAX_ATTEMPTS)
                    {
                        SetChat("Failed to connect to the server. Please try again later.");
                        return;
                    }
                    System.Threading.Thread.Sleep(1000); // Wait for 1 second before retrying
                }
            }

            if (socket.Connected)
            {
                AddToChat("Connected to server. Please login or register.");
                clientSocket.socket.BeginReceive(clientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, clientSocket);
            }
        }

        public void SendLoginCommand(string username, string password)
        {
            SendString($"!login {username} {password}");
        }


        public void SendRegistrationCommand(string username, string password)
        {
            SendString($"!register {username} {password}");
        }
        // Callback method for receiving data from the server
        private void ReceiveCallback(IAsyncResult AR)
        {
            try
            {
                ClientSocket currentClientSocket = (ClientSocket)AR.AsyncState;
                int received = currentClientSocket.socket.EndReceive(AR);

                if (received == 0)
                {
                    AddToChat("Disconnected from server");
                    return;
                }

                byte[] recBuf = new byte[received];
                Array.Copy(currentClientSocket.buffer, recBuf, received);
                string text = Encoding.UTF8.GetString(recBuf);

                ProcessReceivedMessage(text);

                // Continue receiving
                currentClientSocket.socket.BeginReceive(currentClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, currentClientSocket);
            }
            catch (SocketException se)
            {
                AddToChat($"SocketException in ReceiveCallback: ErrorCode={se.ErrorCode}, Message={se.Message}");
            }
            catch (ObjectDisposedException ode)
            {
                AddToChat($"ObjectDisposedException in ReceiveCallback: {ode.Message}");
            }
            catch (Exception ex)
            {
                AddToChat($"Exception in ReceiveCallback: {ex.GetType().Name}, Message={ex.Message}");
            }
        }

        // Processes messages received from the server
        private void ProcessReceivedMessage(string message)
        {
            try
            {
                if (message.StartsWith("Login successful"))
                {
                    isAuthenticated = true;
                    this.username = message.Split(',')[1].Trim().TrimEnd('!');
                    state = ClientState.Chatting;
                    AddToChat($"You are now logged in as: {this.username}");
                }
                else if (message.StartsWith("Login failed"))
                {
                    isAuthenticated = false;
                    state = ClientState.Login;
                    AddToChat("Login failed. Please try again or register a new account.");
                }
                else if (message.StartsWith("Registration successful"))
                {
                    AddToChat("Registration successful. You can now log in with your credentials.");
                    state = ClientState.Login;
                }
                else if (message.StartsWith("Registration failed"))
                {
                    AddToChat("Registration failed. Please try again with a different username.");
                    state = ClientState.Login;
                }
                else if (message.StartsWith("You are now logged in as:"))
                {
                    isAuthenticated = true;
                    this.username = message.Split(':')[1].Trim();
                    state = ClientState.Chatting;
                    AddToChat(message);
                }
                else if (message.StartsWith("!player"))
                {
                    playerNumber = int.Parse(message.Substring(7));
                    state = ClientState.Playing;
                    AddToChat($"You have joined the game as Player {playerNumber}");
                    form1.Invoke(new Action(() => form1.JoinGame(playerNumber)));
                }
                else if (message.StartsWith("GAME_STATE:"))
                {
                    string gameState = message.Substring(11);
                    form1.Invoke(new Action(() => form1.UpdateGameBoard(gameState)));
                }
                else if (message == "It's your turn to move.")
                {
                    AddToChat(message);
                    form1.Invoke(new Action(() => form1.EnableGameBoard(true)));
                }
                else if (message.StartsWith("It's") && message.EndsWith("turn to move."))
                {
                    AddToChat(message);
                    form1.Invoke(new Action(() => form1.EnableGameBoard(false)));
                }
                else
                {
                    AddToChat(message);
                }
            }
            catch (Exception ex)
            {
                AddToChat($"Error processing message: {ex.Message}");
            }
        }

        public void SendChatMessage(string message)
        {
            if (isAuthenticated)
            {
                SendString(message);
                AddToChat($"{username}: {message}"); // Display the user's own message
            }
            else if (message.StartsWith("!login ") || message.StartsWith("!register "))
            {
                SendString(message);
            }
            else
            {
                AddToChat("You are not logged in. Please login or register first.");
            }
        }

        private void HandleGameState(string gameState)
        {
            try
            {
                if (form1 != null)
                {
                    form1.Invoke(new Action(() => form1.UpdateGameBoard(gameState)));
                }
                else
                {
                    AddToChat("ERROR: form1 is null when trying to update game board");
                }
            }
            catch (Exception ex)
            {
                AddToChat($"ERROR in HandleGameState: {ex.GetType().Name}, Message={ex.Message}");
            }
        }

        private void EnableGameBoard(bool enable)
        {
            form1.Invoke(new Action(() => form1.EnableGameBoard(enable)));
        }

        public void SendGameMove(int position)
        {
            if (state == ClientState.Playing)
            {
                SendString($"MOVE:{position}");
            }
        }



        // Handles incoming messages based on client state
        public void HandleIncomingMessage(string message)
        {
            switch (state)
            {
                case ClientState.Login:
                    HandleLoginMessages(message);
                    break;
                case ClientState.Chatting:
                    HandleChatMessages(message);
                    break;
                case ClientState.Playing:
                    HandleGameMessages(message);
                    break;
            }
        }

        private void HandleLoginMessages(string message)
        {
            // Process login-related messages here
            AddToChat(message);
        }

        private void HandleChatMessages(string message)
        {
            if (message.StartsWith("!join"))
            {
                // Join the game
                AddToChat("Attempting to join the game...");
                // Logic to send join request to server goes here
            }
            else
            {
                AddToChat(message);
            }
        }

        private void HandleGameMessages(string message)
        {
            // Handle game-related messages here
            AddToChat(message);
        }

        // Sends a message to the server
        public void SendString(string text)
        {
            if (!socket.Connected)
            {
                AddToChat("Error: Not connected to server");
                return;
            }

            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(text);
                socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, SendCallback, socket);
            }
            catch (Exception ex)
            {
                AddToChat($"Error sending message: {ex.Message}");
            }
        }

        private void SendCallback(IAsyncResult AR)
        {
            try
            {
                socket.EndSend(AR);
            }
            catch (Exception ex)
            {
                AddToChat($"Error in SendCallback: {ex.Message}");
            }
        }


        // Closes the connection to the server
        public void Close()
        {
            if (socket != null && socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
        }
    }
}
