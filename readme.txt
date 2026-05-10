This project implements a multi-functional server application that combines chat functionality with a Tic-Tac-Toe game. It's built using C# and Windows Forms, providing a graphical interface for both server management and client interaction.


Features
•	Chat System: Allows multiple clients to connect and communicate in a shared chat room.
•	Tic-Tac-Toe Game: Integrated game functionality where clients can play against each other.
•	User Authentication: Supports user registration and login functionality.
•	Moderation Tools: Includes commands for moderators to manage the chat room.
•	Database Integration: Uses SQLite for persistent storage of user information.


Components
1. Server (TCPChatServer)
•	Manages client connections
•	Handles message routing
•	Implements game logic
•	Provides moderation commands
2. Client (TCPChatClient)
•	Connects to the server
•	Sends and receives messages
•	Participates in games
3. User Interface (Form1)
•	Provides GUI for server setup and client connection
•	Displays chat messages and game board
4. Game Logic (TicTacToe)
•	Implements Tic-Tac-Toe game rules
•	Manages game state
5. Database Access (DatabaseAccess)
•	Handles user registration and authentication
•	Stores user information in SQLite database



Setup and Running
1.	Ensure you have .NET Framework installed on your system.
2.	Clone the repository or download the source files.
3.	Open the solution in Visual Studio.
4.	Build the solution to resolve dependencies.
5.	Run the application.


Usage
Starting the Server
1.	Enter the desired port number in the "My Port" field.
2.	Click "Host Server" to start the server.
Connecting as a Client

1.	Enter the server's IP address in the "Server IP" field.
2.	Enter the server's port in the "Server Port" field.
3.	Click "Join Server" to connect.


Chat Commands
•	!login [username] [password]: Log in to the server
•	!register [username] [password]: Register a new account
•	!who: List connected users
•	!whisper [username] [message]: Send a private message
•	!joke: Receive a random joke
•	!username [new_name]: Change your username


Game Commands
•	!startgame: Start a new Tic-Tac-Toe game
•	!join: Join an existing game


Moderation Commands
•	!mod [username]: Promote/demote a user to/from moderator status (server only)
•	!kick [username]: Kick a user from the server (moderators only)

