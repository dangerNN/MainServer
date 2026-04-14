using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace MainServer
{
    public partial class PrivateChatForm : Form
    {
        string otherUser, myName;
        NetworkStream stream;

        public PrivateChatForm(string other, string me, NetworkStream s)
        {
            InitializeComponent();
            otherUser = other; myName = me; stream = s;
            this.Text = "Чат с " + otherUser;
        }

        public void AddMessage(string sender, string msg)
        {
            this.Invoke((MethodInvoker)(() => {
                Label lbl = new Label
                {
                    Text = $"{sender}: {msg}",
                    AutoSize = true,
                    MaximumSize = new Size(messagesPanel.Width - 30, 0)
                };
                messagesPanel.Controls.Add(lbl);
                messagesPanel.ScrollControlIntoView(lbl);
            }));
        }

        public void AddImage(string sender, string base64)
        {
            this.Invoke((MethodInvoker)(() => {
                Label lbl = new Label { Text = $"{sender} отправил фото:", AutoSize = true };
                messagesPanel.Controls.Add(lbl);
                try
                {
                    byte[] bytes = Convert.FromBase64String(base64);
                    using (var ms = new MemoryStream(bytes))
                    {
                        PictureBox pb = new PictureBox
                        {
                            Image = Image.FromStream(ms),
                            SizeMode = PictureBoxSizeMode.Zoom,
                            Width = 150,
                            Height = 120
                        };
                        messagesPanel.Controls.Add(pb);
                        messagesPanel.ScrollControlIntoView(pb);
                    }
                }
                catch { AddMessage("Система", "Ошибка фото"); }
            }));
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtMessage.Text)) return;

            string msg = $"PRIVATE:{myName}:{otherUser}:{txtMessage.Text}";
            byte[] data = Encoding.UTF8.GetBytes(msg);
            stream.Write(data, 0, data.Length);

            // В привате сервер НЕ пересылает сообщение обратно отправителю, 
            // поэтому здесь мы добавляем сообщение в окно САМИ.
            AddMessage("Я", txtMessage.Text);
            txtMessage.Clear();
        }

        private void btnSendPhoto_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Images|*.jpg;*.png" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string base64 = Convert.ToBase64String(File.ReadAllBytes(ofd.FileName));
                    string msg = $"IMG_PRIV:{otherUser}:{base64}";
                    byte[] data = Encoding.UTF8.GetBytes(msg);
                    stream.Write(data, 0, data.Length);

                    // Добавляем себе в окно привата локально
                    AddImage("Я", base64);
                }
            }
        }
    }
}