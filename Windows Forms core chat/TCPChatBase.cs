using System;
using System.Windows.Forms;

namespace Windows_Forms_Chat
{
    // Base class for both server and client chat functionality
    public class TCPChatBase
    {
        protected TextBox chatTextBox;
        protected int port;

        // Sets the chat text in the UI thread-safely
        protected void SetChat(string str)
        {
            chatTextBox.Invoke((Action)delegate
            {
                chatTextBox.Text = str;
                chatTextBox.AppendText(Environment.NewLine);
            });
        }

        // Appends text to the chat box in the UI thread-safely
        protected void AddToChat(string str)
        {
            chatTextBox.Invoke((Action)delegate
            {
                // Replace '\n' with Environment.NewLine
                str = str.Replace("\n", Environment.NewLine);
                chatTextBox.AppendText(str + Environment.NewLine);
                chatTextBox.ScrollToCaret();
            });
        }
    }
}