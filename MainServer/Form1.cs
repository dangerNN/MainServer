using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
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
        Dictionary<string, TcpClient> clients = new Dictionary<string, TcpClient>();

        Dictionary<string, string> accounts = new Dictionary<string, string>();
        string accountsFile = "accounts.txt";

        const int MAX_BUFFER = 5 * 1024 * 1024;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void btnStart_Click_1(object sender, EventArgs e)
        {
            try
            {
                LoadAccounts();
                server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8888);
                server.Start();
                Thread thread = new Thread(ListenClients) { IsBackground = true };
                thread.Start();
                listBoxLog.Items.Add("Сервер запущен (Буфер 5МБ)...");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void ListenClients()
        {
            while (true)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    new Thread(() => HandleClient(client)) { IsBackground = true }.Start();
                }
                catch { break; }
            }
        }

        byte[] ReadExact(NetworkStream stream, int count)
        {
            byte[] data = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(data, offset, count - offset);
                if (read <= 0) return null; // Соединение разорвано
                offset += read;
            }
            return data;
        }

        void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[MAX_BUFFER];
            string userName = "";

            try
            {
                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // РЕГИСТРАЦИЯ
                    if (message.StartsWith("REGISTER:"))
                    {
                        string[] parts = message.Substring(9).Split(':');
                        if (parts.Length == 2)
                        {
                            string u = parts[0]; string p = parts[1];
                            if (accounts.ContainsKey(u))
                            {
                                Send(client, "AUTH_FAIL:Имя уже занято");
                            }
                            else
                            {
                                SaveAccount(u, p);
                                Send(client, "AUTH_SUCCESS:Регистрация успешна");
                            }
                        }
                    }
                    // ВХОД (LOGIN)
                    else if (message.StartsWith("LOGIN:"))
                    {
                        string[] parts = message.Substring(6).Split(':');
                        if (parts.Length == 2)
                        {
                            string u = parts[0]; string p = parts[1];
                            if (accounts.ContainsKey(u) && accounts[u] == p)
                            {
                                if (clients.ContainsKey(u))
                                {
                                    Send(client, "AUTH_FAIL:Этот аккаунт уже в сети");
                                }
                                else
                                {
                                    userName = u;
                                    clients.Add(userName, client);
                                    Send(client, "AUTH_SUCCESS:Вход выполнен");
                                    AddLog(userName + " вошел в систему");
                                    BroadcastUserList();
                                    BroadcastPublic("SYSTEM: " + userName + " зашел в чат");
                                }
                            }
                            else
                            {
                                Send(client, "AUTH_FAIL:Неверное имя или пароль");
                            }
                        }
                    }
                    // СООБЩЕНИЯ (без дублирования отправителю)
                    else if (message.StartsWith("PUBLIC:") && !string.IsNullOrEmpty(userName))
                    {
                        string text = message.Substring(7);
                        foreach (var c in clients)
                        {
                            if (c.Key != userName) // Не шлем самому себе
                                Send(c.Value, "PUBLIC:" + userName + ": " + text);
                        }
                        AddLog(userName + ": " + text);
                    }
                    // ФОТО (без дублирования отправителю)
                    else if (message.StartsWith("IMG_PUB:") && !string.IsNullOrEmpty(userName))
                    {
                        string base64 = message.Substring(8);
                        foreach (var c in clients)
                        {
                            if (c.Key != userName)
                                Send(c.Value, $"IMG_PUB:{userName}:{base64}");
                        }
                        AddLog(userName + " отправил фото");
                    }
                    // --- ИЗМЕНЕННАЯ ЛОГИКА ДЛЯ ФОТО (ПРИВАТНОЕ) ---
                    else if (message.StartsWith("IMG_PRIV:") && !string.IsNullOrEmpty(userName))
                    {
                        string[] parts = message.Split(new[] { ':' }, 3);
                        if (parts.Length == 3)
                        {
                            string receiver = parts[1];
                            string base64Data = parts[2];
                            if (clients.ContainsKey(receiver))
                            {
                                // Отправляем только получателю
                                Send(clients[receiver], $"IMG_PRIV:{userName}:{base64Data}");
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }
        

        // ОБНОВЛЕННЫЙ МЕТОД ОТПРАВКИ С ПРЕФИКСОМ ДЛИНЫ
        void Send(TcpClient client, string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                byte[] length = BitConverter.GetBytes(data.Length); // 4 байта длины

                NetworkStream ns = client.GetStream();
                ns.Write(length, 0, 4);      // Сначала шлем длину
                ns.Write(data, 0, data.Length); // Потом данные
            }
            catch { }
        }
        

        void LoadAccounts()
        {
            accounts.Clear();
            if (File.Exists(accountsFile))
            {
                string[] lines = File.ReadAllLines(accountsFile);
                foreach (string line in lines)
                {
                    string[] parts = line.Split(':');
                    if (parts.Length == 2) accounts[parts[0]] = parts[1];
                }
            }
        }

        void SaveAccount(string name, string password)
        {
            accounts[name] = password;
            File.AppendAllText(accountsFile, $"{name}:{password}{Environment.NewLine}");
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
            if (listBoxLog.InvokeRequired)
            {
                listBoxLog.Invoke((MethodInvoker)(() => listBoxLog.Items.Add(text)));
            }
            else
            {
                listBoxLog.Items.Add(text);
            }
        }

        private void btnCreateClient_Click_1(object sender, EventArgs e)
        {
            Form2 clientForm = new Form2();
            clientForm.Show();
        }
    }
}