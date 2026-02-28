using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MainServer
{
    public partial class Form1 : Form
    {
        TcpListener server;
        Dictionary<string, TcpClient> clients =
            new Dictionary<string, TcpClient>();
        public Form1()
        {
            InitializeComponent();
        }
        private void btnStart_Click_1(object sender, EventArgs e)
        {
            server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8888);
            server.Start();

            Thread thread = new Thread(ListenClients);
            thread.IsBackground = true;
            thread.Start();

            listBoxLog.Items.Add("Сервер запущен...");
        }

        void ListenClients()
        {
            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Thread thread = new Thread(() => HandleClient(client));
                thread.IsBackground = true;
                thread.Start();
            }
        }

        void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            string userName = "";

            try
            {
                while (true)
                {
                    int bytes = stream.Read(buffer, 0, buffer.Length);
                    string message =
                        Encoding.UTF8.GetString(buffer, 0, bytes);

                    if (message.StartsWith("REGISTER:"))
                    {
                        string name = message.Replace("REGISTER:", "");

                        if (clients.ContainsKey(name))
                        {
                            Send(client, "NAME_TAKEN");
                        }
                        else
                        {
                            userName = name;
                            clients.Add(userName, client);
                            Send(client, "REGISTERED");

                            BroadcastUserList();
                            BroadcastPublic("SYSTEM: " +
                                userName + " подключился");

                            AddLog(userName + " подключился");
                        }
                    }
                    else if (message.StartsWith("PUBLIC:"))
                    {
                        BroadcastPublic(
                            message.Replace("PUBLIC:", ""));
                    }
                    else if (message.StartsWith("PRIVATE:"))
                    {
                        string[] parts = message.Split(':');
                        string sender = parts[1];
                        string receiver = parts[2];
                        string text = parts[3];

                        if (clients.ContainsKey(receiver))
                        {
                            Send(clients[receiver],
                                $"PRIVATE:{sender}:{text}");

                            Send(clients[sender],
                                $"PRIVATE:{sender}:{text}");
                        }
                    }
                }
            }
            catch
            {
                if (userName != "")
                {
                    clients.Remove(userName);
                    BroadcastUserList();
                    BroadcastPublic("SYSTEM: " +
                        userName + " вышел");
                    AddLog(userName + " отключился");
                }
            }
        }

        void Send(TcpClient client, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            client.GetStream().Write(data, 0, data.Length);
        }

        void BroadcastPublic(string message)
        {
            foreach (var c in clients.Values)
                Send(c, "PUBLIC:" + message);

            AddLog(message);
        }

        void BroadcastUserList()
        {
            string users = string.Join(",", clients.Keys);
            foreach (var c in clients.Values)
                Send(c, "USERLIST:" + users);
        }

        void AddLog(string text)
        {
            Invoke((MethodInvoker)(() =>
            {
                listBoxLog.Items.Add(text);
            }));
        }

        private void btnCreateClient_Click_1(object sender, EventArgs e)
        {
            Form2 clientForm = new Form2();
            clientForm.Show();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

    }
}
