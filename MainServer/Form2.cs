using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MainServer
{
    public partial class Form2 : Form
    {
        TcpClient client;
        NetworkStream stream;
        bool isRegistered = false;
        string userName = "";
        Dictionary<string, PrivateChatForm> privateChats = new Dictionary<string, PrivateChatForm>();

        public Form2()
        {
            InitializeComponent();
            Connect();
        }

        void Connect()
        {
            try
            {
                client = new TcpClient("127.0.0.1", 8888);
                stream = client.GetStream();
                Thread thread = new Thread(Receive) { IsBackground = true };
                thread.Start();
            }
            catch { MessageBox.Show("Ошибка подключения"); }
        }

        void Receive()
        {
            byte[] buffer = new byte[5 * 1024 * 1024];
            try
            {
                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    this.Invoke((MethodInvoker)(() => {
                        if (message.StartsWith("AUTH_SUCCESS:"))
                        {
                            isRegistered = true;
                            MessageBox.Show(message.Substring(13), "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else if (message.StartsWith("AUTH_FAIL:"))
                        {
                            isRegistered = false;
                            MessageBox.Show(message.Substring(10), "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else if (message.StartsWith("PUBLIC:"))
                        {
                            AddTextToChat(message.Substring(7));
                        }
                        else if (message.StartsWith("IMG_PUB:"))
                        {
                            string[] parts = message.Split(new[] { ':' }, 3);
                            AddImageToChat(parts[1], parts[2]);
                        }
                        else if (message.StartsWith("USERLIST:"))
                        {
                            listBoxUsers.Items.Clear();
                            string[] users = message.Substring(9).Split(',');
                            foreach (var u in users)
                                if (!string.IsNullOrEmpty(u) && u != userName) listBoxUsers.Items.Add(u);
                        }
                        else if (message.StartsWith("PRIVATE:"))
                        {
                            string[] parts = message.Split(':');
                            string sender = parts[1];
                            string text = parts[2];
                            OpenPrivateChat(sender).AddMessage(sender, text);
                        }
                        else if (message.StartsWith("IMG_PRIV:"))
                        {
                            string[] parts = message.Split(new[] { ':' }, 3);
                            OpenPrivateChat(parts[1]).AddImage(parts[1], parts[2]);
                        }
                    }));
                }
            }
            catch { }
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Заполните поля!"); return;
            }
            Send($"REGISTER:{txtName.Text}:{txtPassword.Text}");
        }

        private void btnSendPublic_Click(object sender, EventArgs e)
        {
            if (!isRegistered || string.IsNullOrEmpty(txtMessage.Text)) return;
            Send("PUBLIC:" + txtMessage.Text);
            AddTextToChat("Я: " + txtMessage.Text); // Добавляем локально
            txtMessage.Clear();
        }

        private void btnSendPhoto_Click(object sender, EventArgs e)
        {
            if (!isRegistered) return;
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Images|*.jpg;*.png" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string base64 = Convert.ToBase64String(File.ReadAllBytes(ofd.FileName));
                    Send("IMG_PUB:" + base64);
                    AddImageToChat("Я", base64); // Добавляем локально
                }
            }
        }

        private void btnPrivate_Click(object sender, EventArgs e)
        {
            if (listBoxUsers.SelectedItem != null)
                OpenPrivateChat(listBoxUsers.SelectedItem.ToString()).Show();
        }

        PrivateChatForm OpenPrivateChat(string user)
        {
            if (!privateChats.ContainsKey(user))
            {
                privateChats[user] = new PrivateChatForm(user, userName, stream);
            }
            return privateChats[user];
        }

        void AddTextToChat(string text)
        {
            Label lbl = new Label
            {
                Text = $"[{DateTime.Now:HH:mm}] {text}",
                AutoSize = true,
                MaximumSize = new Size(chatPanel.Width - 30, 0)
            };
            chatPanel.Controls.Add(lbl);
            chatPanel.ScrollControlIntoView(lbl);
        }

        void AddImageToChat(string sender, string base64)
        {
            Label lbl = new Label { Text = $"{sender} прислал фото:", AutoSize = true };
            chatPanel.Controls.Add(lbl);
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                using (var ms = new MemoryStream(bytes))
                {
                    PictureBox pb = new PictureBox
                    {
                        Image = Image.FromStream(ms),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Width = 200,
                        Height = 150
                    };
                    chatPanel.Controls.Add(pb);
                    chatPanel.ScrollControlIntoView(pb);
                }
            }
            catch { AddTextToChat("Ошибка загрузки фото"); }
        }

        void Send(string msg)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(msg);
                stream.Write(data, 0, data.Length);
            }
            catch { }
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Заполните поля!"); return;
            }
            userName = txtName.Text; // Запоминаем имя для фильтрации USERLIST
            Send($"LOGIN:{txtName.Text}:{txtPassword.Text}");
        }
    }
}