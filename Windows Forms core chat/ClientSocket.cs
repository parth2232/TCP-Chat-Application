using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Data.SQLite;

namespace Windows_Forms_Chat
{
    using System.Net.Sockets;

    public enum ClientState
    {
        Login,
        Chatting,
        Playing
    }


    namespace Windows_Forms_Chat
    {
        public class ClientSocket
        {
            public Socket socket;
            public byte[] buffer = new byte[BUFFER_SIZE];
            public string username;
            public const int BUFFER_SIZE = 1024;
            public bool IsModerator { get; set; } = false; 
        }
    }

}
