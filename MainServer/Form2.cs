using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MainServer
{
    public partial class Form2 : Form
    {
        TcpClient client;
        NetworkStream stream;
        bool isRegistered = false;
        string userName = "";

        Dictionary<string, PrivateChatForm> privateChats =
            new Dictionary<string, PrivateChatForm>();
        public Form2()
        {
            InitializeComponent();
            Connect();
        }
        void Connect()
        {
            client = new TcpClient("127.0.0.1", 8888);
            stream = client.GetStream();

            Thread thread = new Thread(Receive);
            thread.IsBackground = true;
            thread.Start();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void Form2_Load(object sender, EventArgs e)
        {

        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            userName = txtName.Text;
            string msg = "REGISTER:" + userName;
            Send(msg);
        }

        private void btnSendPublic_Click(object sender, EventArgs e)
        {
            if (!isRegistered) return;

            string msg =
                $"PUBLIC:{userName}: {txtMessage.Text}";
            Send(msg);
            txtMessage.Clear();
        }

        private void btnPrivate_Click(object sender, EventArgs e)
        {
            if (listBoxUsers.SelectedItem == null) return;

            string selectedUser =
                listBoxUsers.SelectedItem.ToString();

            if (!privateChats.ContainsKey(selectedUser))
            {
                PrivateChatForm chat =
                    new PrivateChatForm(
                        selectedUser,
                        userName,
                        stream);

                privateChats.Add(selectedUser, chat);
                chat.Show();
            }
        }
        void Receive()
        {
            byte[] buffer = new byte[1024];

            while (true)
            {
                int bytes = stream.Read(buffer, 0, buffer.Length);
                string message =
                    Encoding.UTF8.GetString(buffer, 0, bytes);

                Invoke((MethodInvoker)(() =>
                {
                    if (message == "REGISTERED")
                        isRegistered = true;

                    else if (message == "NAME_TAKEN")
                        MessageBox.Show("Имя занято!");

                    else if (message.StartsWith("USERLIST:"))
                    {
                        listBoxUsers.Items.Clear();
                        string users =
                            message.Replace("USERLIST:", "");

                        foreach (var u in users.Split(','))
                            if (u != userName)
                                listBoxUsers.Items.Add(u);
                    }

                    else if (message.StartsWith("PUBLIC:"))
                        listBoxChat.Items.Add(
                            message.Replace("PUBLIC:", ""));

                    else if (message.StartsWith("PRIVATE:"))
                    {
                        string[] parts = message.Split(':');
                        string sender = parts[1];
                        string text = parts[2];

                        if (!privateChats.ContainsKey(sender))
                        {
                            PrivateChatForm chat =
                                new PrivateChatForm(
                                    sender,
                                    userName,
                                    stream);

                            privateChats.Add(sender, chat);
                            chat.Show();
                        }

                        privateChats[sender]
                            .AddMessage(sender + ": " + text);
                    }
                }));
            }


        }
        void Send(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            stream.Write(data, 0, data.Length);
        }

    }
}
