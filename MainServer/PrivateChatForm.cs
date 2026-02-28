using System;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace MainServer
{
    public partial class PrivateChatForm : Form
    {
        string otherUser;
        string myName;
        NetworkStream stream;

        public PrivateChatForm(string other,
                               string me,
                               NetworkStream s)
        {
            InitializeComponent();
            otherUser = other;
            myName = me;
            stream = s;

            Text = "Чат с " + otherUser;
        }

        public void AddMessage(string msg)
        {
            listBoxMessages.Items.Add(msg);
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            string text = txtMessage.Text;

            string msg =
                $"PRIVATE:{myName}:{otherUser}:{text}";

            byte[] data =
                Encoding.UTF8.GetBytes(msg);

            stream.Write(data, 0, data.Length);

            listBoxMessages.Items.Add("Я: " + text);
            txtMessage.Clear();
        }
    }
}